using System.Text;
using JiggleForge.Core;

namespace JiggleForge.Core.Tests;

[TestClass]
public sealed class RuntimeEnvironmentServiceTests
{
    private string? root;
    private string? payload;
    private string? zzmi;

    [TestInitialize]
    public void CreateTemporaryEnvironment()
    {
        root = Path.Combine(Path.GetTempPath(), "JiggleForgeRuntimeTests", Guid.NewGuid().ToString("N"));
        payload = Path.Combine(root, "Payload");
        zzmi = Path.Combine(root, "ZZMI");
        Directory.CreateDirectory(Path.Combine(payload, "JiggleForge"));
        Directory.CreateDirectory(Path.Combine(payload, "ShaderFixes"));
        Directory.CreateDirectory(Path.Combine(payload, "ShaderFixes", "JiggleForgeRuntime"));
        Directory.CreateDirectory(Path.Combine(zzmi, "Mods"));
        Directory.CreateDirectory(Path.Combine(zzmi, "ShaderFixes"));
        File.WriteAllText(Path.Combine(payload, "JiggleForge.ini"), """
            namespace = jiggle_forge

            [ResourceDefaultParameters]
            type = Buffer
            format = R32G32B32A32_FLOAT
            array = 5
            data = 1 2 0.25 0.7 2.2 2.5 0.75 0.15 0.02 10 0.84 2.2 0.9 0.12 0.02 -0.15 0.15 1 -1 1

            [Constants]
            ; JIGGLEFORGE_DEFAULT_PHYSICS_BEGIN
            global $defaultRadius = 0.25
            global $defaultStrength = 0.7
            global $defaultFalloff = 2.2
            global $defaultVolumeResponse = 2.5
            global $defaultDragScale = 0.75
            global $defaultGrabDamping = 0.84
            global $defaultGrabSpring = 0.12
            global $defaultReleaseDamping = 0.9
            global $defaultReleaseSpring = 0.1
            global $defaultReleaseKick = 0.3
            global $defaultMaxOffset = 0.15
            global $defaultTargetFollow = 0.3
            global $defaultWheelDepthStep = 0.02
            global $defaultWheelMinDepth = -0.15
            global $defaultWheelMaxDepth = 0.15
            ; JIGGLEFORGE_DEFAULT_PHYSICS_END

            ; JIGGLEFORGE_DRAG_KEY_BEGIN
            [KeyInputManager]
            key = VK_LBUTTON
            type = hold
            $mouseDown = 1
            post $mouseDown = 0
            ; JIGGLEFORGE_DRAG_KEY_END
            """, Encoding.UTF8);
        File.WriteAllText(Path.Combine(payload, "JiggleForge", "WheelBridge.exe"), "test bridge", Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(payload, "JiggleForge", "WheelBridge.txt"),
            "require_drag_button = true\r\ndrag_key = VK_LBUTTON\r\ndrag_keys = VK_LBUTTON\r\n",
            Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(payload, "ShaderFixes", "JiggleForgeRuntime", "deformation_field.hlsl"),
            "// independent runtime deformation include",
            Encoding.UTF8);
        foreach (string hash in RuntimeEnvironmentService.RequiredShaderHashes)
        {
            File.WriteAllText(
                Path.Combine(payload, "ShaderFixes", $"{hash}-vs_replace.txt"),
                $"// independent runtime {hash}\r\n" +
                "Buffer<uint> JiggleForgeDirectStateIndex : register(t72);\r\n" +
                "Buffer<float4> JF_MotionState : register(t75);",
                Encoding.UTF8);
        }
    }

    [TestCleanup]
    public void DeleteTemporaryEnvironment()
    {
        if (root is not null && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void InstallReportsCurrentRuntimeAndPreservesExistingShaderFix()
    {
        string firstHash = RuntimeEnvironmentService.RequiredShaderHashes[0];
        string existing = Path.Combine(zzmi!, "ShaderFixes", $"{firstHash}-vs_replace.txt");
        File.WriteAllText(existing, "original shader fix", Encoding.UTF8);
        RuntimeEnvironmentService service = new(payload!);

        service.Install(zzmi!);
        RuntimeEnvironmentStatus status = service.Inspect(zzmi!);

        Assert.IsTrue(status.Ready, status.ToString());
        Assert.IsTrue(status.RuntimeCurrent);
        Assert.AreEqual(RuntimeEnvironmentService.DefaultDragKey, status.DragKey);
        Assert.AreEqual(RuntimeEnvironmentService.RequiredShaderHashes.Count, status.CurrentShaderCount);
        Assert.AreEqual(1, status.BackupCount);
        string installedInclude = Path.Combine(
            zzmi!,
            "ShaderFixes",
            "JiggleForgeRuntime",
            "deformation_field.hlsl");
        Assert.IsTrue(File.Exists(installedInclude));
        Assert.AreEqual("original shader fix", File.ReadAllText(existing + ".pre_jiggleForge_backup", Encoding.UTF8));

        File.AppendAllText(existing, " changed", Encoding.UTF8);
        RuntimeEnvironmentStatus changed = service.Inspect(zzmi!);
        Assert.IsFalse(changed.Ready);
        Assert.AreEqual(status.RequiredShaderCount - 1, changed.CurrentShaderCount);

        File.AppendAllText(installedInclude, " changed", Encoding.UTF8);
        Assert.IsFalse(service.Inspect(zzmi!).Ready);
    }

    [TestMethod]
    public void DragKeyCanBeSelectedAndUpdatedWithoutMakingRuntimeOutdated()
    {
        RuntimeEnvironmentService service = new(payload!);
        service.Install(zzmi!, "VK_RBUTTON");

        RuntimeEnvironmentStatus installed = service.Inspect(zzmi!);
        Assert.IsTrue(installed.Ready, installed.ToString());
        Assert.AreEqual("VK_RBUTTON", installed.DragKey);

        service.SetDragKey(zzmi!, "X");
        RuntimeEnvironmentStatus updated = service.Inspect(zzmi!);
        Assert.IsTrue(updated.Ready, updated.ToString());
        Assert.AreEqual("X", updated.DragKey);
        string generated = File.ReadAllText(Path.Combine(
            zzmi!,
            "Mods",
            "JiggleForgeShaderFix",
            "JiggleForge.ini"));
        StringAssert.Contains(generated, "key = X");
        string wheelConfig = File.ReadAllText(Path.Combine(
            zzmi!,
            "Mods",
            "JiggleForgeShaderFix",
            "JiggleForge",
            "WheelBridge.txt"));
        StringAssert.Contains(wheelConfig, "drag_keys = X");
        Assert.ThrowsExactly<ArgumentException>(() => service.SetDragKey(zzmi!, "NOT_A_KEY"));
    }

    [TestMethod]
    public void MultipleDragKeysAreWrittenToTheRuntimeAndWheelBridge()
    {
        RuntimeEnvironmentService service = new(payload!);
        string[] keys = ["VK_LBUTTON", "VK_XBUTTON1", "X"];

        service.Install(zzmi!, keys);
        RuntimeEnvironmentStatus installed = service.Inspect(zzmi!);

        Assert.IsTrue(installed.Ready, installed.ToString());
        CollectionAssert.AreEqual(keys, installed.DragKeys!.ToArray());
        string runtimeIni = File.ReadAllText(Path.Combine(
            zzmi!, "Mods", "JiggleForgeShaderFix", "JiggleForge.ini"));
        Assert.AreEqual(3, runtimeIni.Split("[KeyJiggleForgeDrag", StringSplitOptions.None).Length - 1);
        StringAssert.Contains(runtimeIni, "key = VK_LBUTTON");
        StringAssert.Contains(runtimeIni, "key = VK_XBUTTON1");
        StringAssert.Contains(runtimeIni, "key = X");
        string wheelConfig = File.ReadAllText(Path.Combine(
            zzmi!, "Mods", "JiggleForgeShaderFix", "JiggleForge", "WheelBridge.txt"));
        StringAssert.Contains(wheelConfig, "drag_keys = VK_LBUTTON, VK_XBUTTON1, X");
        StringAssert.Contains(wheelConfig, "drag_key = VK_LBUTTON");

        service.SetDragKeys(zzmi!, ["C", "V"]);
        RuntimeEnvironmentStatus updated = service.Inspect(zzmi!);
        Assert.IsTrue(updated.Ready, updated.ToString());
        CollectionAssert.AreEqual(new[] { "C", "V" }, updated.DragKeys!.ToArray());
        Assert.ThrowsExactly<ArgumentException>(() => service.SetDragKeys(zzmi!, []));
    }

    [TestMethod]
    public void DefaultPhysicsCanBeInstalledAndUpdatedWithoutMakingRuntimeOutdated()
    {
        RuntimeEnvironmentService service = new(payload!);
        PhysicsSettings installedDefaults = new()
        {
            Radius = 0.42,
            Strength = 0.81,
            VolumeResponse = 1.9,
            WheelMinDepth = -0.07,
            WheelMaxDepth = 0.12,
        };
        service.Install(zzmi!, RuntimeEnvironmentService.DefaultDragKey, installedDefaults);

        RuntimeEnvironmentStatus installed = service.Inspect(zzmi!);
        Assert.IsTrue(installed.Ready, installed.ToString());
        string runtimeIni = Path.Combine(
            zzmi!, "Mods", "JiggleForgeShaderFix", "JiggleForge.ini");
        string contents = File.ReadAllText(runtimeIni);
        StringAssert.Contains(contents, "global $defaultRadius = 0.42");
        StringAssert.Contains(contents, "global $defaultVolumeResponse = 1.9");
        StringAssert.Contains(contents, "global $defaultWheelMinDepth = -0.07");
        StringAssert.Contains(
            contents,
            "data = 1 2 0.42 0.81 2.2 1.9 0.75 0.15 0.02 10 0.84 2.2 0.9 0.12 0.02 -0.07 0.12 1 -1 1");

        installedDefaults.Radius = 0.18;
        installedDefaults.WheelDepthStep = 0.01;
        service.SetDefaultPhysics(zzmi!, installedDefaults);
        Assert.IsTrue(service.Inspect(zzmi!).Ready);
        contents = File.ReadAllText(runtimeIni);
        StringAssert.Contains(contents, "global $defaultRadius = 0.18");
        StringAssert.Contains(contents, "global $defaultWheelDepthStep = 0.01");
    }

    [TestMethod]
    public void ExistingRuntimeWithoutGeneratedDragKeyCanBeConfiguredInPlace()
    {
        RuntimeEnvironmentService service = new(payload!);
        service.Install(zzmi!);
        string runtimeIni = Path.Combine(
            zzmi!,
            "Mods",
            "JiggleForgeShaderFix",
            "JiggleForge.ini");
        string contents = File.ReadAllText(runtimeIni);
        int begin = contents.IndexOf("; JIGGLEFORGE_DRAG_KEY_BEGIN", StringComparison.Ordinal);
        int end = contents.IndexOf("; JIGGLEFORGE_DRAG_KEY_END", StringComparison.Ordinal) + "; JIGGLEFORGE_DRAG_KEY_END".Length;
        File.WriteAllText(runtimeIni, contents.Remove(begin, end - begin));

        RuntimeEnvironmentStatus legacy = service.Inspect(zzmi!);
        Assert.IsTrue(legacy.RuntimeInstalled);
        Assert.IsFalse(legacy.RuntimeCurrent);
        Assert.IsNull(legacy.DragKey);

        service.SetDragKey(zzmi!, "VK_MBUTTON");
        RuntimeEnvironmentStatus repaired = service.Inspect(zzmi!);
        Assert.IsTrue(repaired.RuntimeInstalled);
        Assert.AreEqual("VK_MBUTTON", repaired.DragKey);
    }

    [TestMethod]
    public void UninstallRemovesRuntimeAndRestoresShaderFixBackup()
    {
        string firstHash = RuntimeEnvironmentService.RequiredShaderHashes[0];
        string existing = Path.Combine(zzmi!, "ShaderFixes", $"{firstHash}-vs_replace.txt");
        File.WriteAllText(existing, "original shader fix", Encoding.UTF8);
        RuntimeEnvironmentService service = new(payload!);
        service.Install(zzmi!);

        service.Uninstall(zzmi!, stopWheelBridge: false);

        Assert.IsFalse(Directory.Exists(Path.Combine(zzmi!, "Mods", "JiggleForgeShaderFix")));
        Assert.IsFalse(Directory.Exists(Path.Combine(zzmi!, "ShaderFixes", "JiggleForgeRuntime")));
        Assert.AreEqual("original shader fix", File.ReadAllText(existing, Encoding.UTF8));
        Assert.IsFalse(File.Exists(existing + ".pre_jiggleForge_backup"));
        foreach (string hash in RuntimeEnvironmentService.RequiredShaderHashes.Skip(1))
        {
            Assert.IsFalse(File.Exists(Path.Combine(zzmi!, "ShaderFixes", $"{hash}-vs_replace.txt")));
        }
    }

    [TestMethod]
    public void InstallRestoresBackupForObsoleteManagedShader()
    {
        const string obsoleteHash = "1b6d08acd285344c";
        string target = Path.Combine(
            zzmi!,
            "ShaderFixes",
            $"{obsoleteHash}-vs_replace.txt");
        string backup = target + ".pre_jiggleForge_backup";
        File.WriteAllText(target, "// JiggleForgeState obsolete replacement", Encoding.UTF8);
        File.WriteAllText(backup, "original exported shader", Encoding.UTF8);
        RuntimeEnvironmentService service = new(payload!);

        service.Install(zzmi!);

        Assert.AreEqual("original exported shader", File.ReadAllText(target, Encoding.UTF8));
        Assert.IsFalse(File.Exists(backup));
    }
}
