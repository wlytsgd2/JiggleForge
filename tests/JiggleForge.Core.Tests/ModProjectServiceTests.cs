using System.Text;
using System.Text.RegularExpressions;
using JiggleForge.Core;

namespace JiggleForge.Core.Tests;

[TestClass]
public sealed class ModProjectServiceTests
{
    private string? root;

    [TestInitialize]
    public void CreateTemporaryMod()
    {
        root = Path.Combine(Path.GetTempPath(), "JiggleForgeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void DeleteTemporaryMod()
    {
        if (root is not null && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void FirstImportFindsNumericAndConditionalAutoDraws()
    {
        File.WriteAllText(Path.Combine(root!, "Example.ini"), """
            [CommandListBody]
            if $swapvar == 0
                drawindexed = 300, 12, -4
            else if $swapvar == 1
                drawindexed = auto
            endif
            drawindexed = 0, 0, 0
            """, Encoding.UTF8);

        ModProjectService service = new();
        ModProjectInspection inspection = service.Inspect(root!);
        JiggleProjectConfig config = service.CreateInitialConfiguration(inspection);

        Assert.AreEqual(ModImportState.FirstImport, inspection.State);
        Assert.AreEqual(2, config.Draws.Count);
        Assert.AreEqual(JiggleDrawKind.Numeric, config.Draws[0].Kind);
        Assert.AreEqual(-4, config.Draws[0].BaseVertex);
        Assert.AreEqual("else if $swapvar == 1", config.Draws[1].Branch);
        Assert.AreEqual(0, config.Groups.Single(group =>
            string.Equals(group.Name, OriginalPartsConfig.GroupName, StringComparison.OrdinalIgnoreCase)).LocalStateId);
        Assert.IsTrue(config.Inspector.Enabled);
        Assert.IsTrue(config.Groups.Any(group =>
            string.Equals(group.Name, OriginalPartsConfig.GroupName, StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual(1, config.Groups.Count);
        Assert.IsTrue(config.Draws.All(draw => string.IsNullOrEmpty(draw.Group)));
        Assert.AreEqual(0, config.Groups.Single().Draws.Count);
    }

    [TestMethod]
    public void FirstImportCopiesTheSelectedGlobalPhysicsDefaults()
    {
        File.WriteAllText(Path.Combine(root!, "Example.ini"), "[CommandList]\r\ndrawindexed = auto\r\n", Encoding.UTF8);
        PhysicsSettings defaults = new()
        {
            Radius = 0.44,
            Strength = 0.66,
            VolumeResponse = 1.75,
            WheelMinDepth = -0.09,
        };
        ModProjectService service = new();

        JiggleProjectConfig config = service.CreateInitialConfiguration(service.Inspect(root!), defaults);
        defaults.Radius = 0.99;

        Assert.AreEqual(0.44, config.Physics.Radius, 0.0001);
        Assert.AreEqual(0.66, config.Physics.Strength, 0.0001);
        Assert.AreEqual(1.75, config.Physics.VolumeResponse, 0.0001);
        Assert.AreEqual(-0.09, config.Physics.WheelMinDepth, 0.0001);
    }

    [TestMethod]
    public void ConfigurationWithoutRuntimeMarkerNeedsRepair()
    {
        File.WriteAllText(Path.Combine(root!, "Example.ini"), "[CommandList]\r\ndrawindexed = auto\r\n", Encoding.UTF8);
        ModProjectService service = new();
        ModProjectInspection first = service.Inspect(root!);
        Assert.AreEqual(ModImportState.FirstImport, first.State, string.Join(" | ", first.Messages));
        JiggleProjectConfig config = service.CreateInitialConfiguration(first);
        JiggleConfigSerializer.Save(Path.Combine(root!, JiggleProjectConfig.DefaultFileName), config);

        ModProjectInspection second = service.Inspect(root!);

        Assert.AreEqual(ModImportState.RuntimeRepairRequired, second.State);
        Assert.IsNotNull(second.Configuration);
    }

    [TestMethod]
    public void AngleEraWheelSettingsMigrateToIndependentDepth()
    {
        File.WriteAllText(Path.Combine(root!, "Example.ini"), "[CommandList]\r\ndrawindexed = auto\r\n", Encoding.UTF8);
        ModProjectService service = new();
        JiggleProjectConfig config = service.CreateInitialConfiguration(service.Inspect(root!));
        string serialized = JiggleConfigSerializer.Serialize(config);
        string legacy = serialized.Replace(
            "wheel_depth_step = 0.02\r\nwheel_min_depth = 0\r\nwheel_max_depth = 0.15",
            "wheel_angle_step = 8\r\nwheel_min_angle = 0\r\nwheel_max_angle = 60",
            StringComparison.Ordinal);

        JiggleProjectConfig migrated = JiggleConfigSerializer.Parse(legacy);

        Assert.AreEqual(0.02, migrated.Physics.WheelDepthStep, 0.0001);
        Assert.AreEqual(-0.15, migrated.Physics.WheelMinDepth, 0.0001);
        Assert.AreEqual(0.15, migrated.Physics.WheelMaxDepth, 0.0001);
        StringAssert.Contains(JiggleConfigSerializer.Serialize(migrated), "wheel_depth_step = 0.02");
        Assert.IsFalse(JiggleConfigSerializer.Serialize(migrated).Contains("wheel_angle_step", StringComparison.Ordinal));
    }

    [TestMethod]
    public void FirstImportCompilerPatchesInPlaceWithoutCopyingExecutables()
    {
        string iniPath = Path.Combine(root!, "Example.ini");
        File.WriteAllText(iniPath, """
            [CommandListBody]
            drawindexed = 300, 12, -4
            drawindexed = auto
            """, Encoding.UTF8);
        ModProjectService service = new();
        ModProjectInspection inspection = service.Inspect(root!);
        JiggleProjectConfig config = service.CreateInitialConfiguration(inspection);

        RuntimeApplyResult result = new ModRuntimeCompiler().Apply(root!, config);

        string patched = File.ReadAllText(iniPath);
        Assert.AreEqual(2, result.DrawCount);
        StringAssert.Contains(patched, "JIGGLEFORGE_VISIBLE_RANGE BEGIN Draw0001");
        StringAssert.Contains(patched, "JIGGLEFORGE_VISIBLE_RANGE BEGIN Draw0002");
        Assert.IsFalse(patched.Contains("RegisterGroupParameters", StringComparison.Ordinal));
        StringAssert.Contains(patched, $"run = CommandList\\jiggle_forge_project_{config.ProjectId:N}\\BeginDraw0001");
        Assert.IsFalse(patched.Contains("vs-t77 =", StringComparison.Ordinal));
        Assert.IsFalse(patched.Contains("vs-t78 =", StringComparison.Ordinal));
        Assert.IsFalse(patched.Contains("$\\jiggle_forge\\projectStateReadSlot", StringComparison.Ordinal));
        Assert.IsFalse(patched.Contains("$\\jiggle_forge\\activePickProfile", StringComparison.Ordinal));
        Assert.IsFalse(patched.Contains("DrawInfluences001", StringComparison.Ordinal));
        Assert.IsFalse(patched.Contains("DrawContext001", StringComparison.Ordinal));
        Assert.IsFalse(patched.Contains("CommandList\\jiggle_forge\\RegisterParams", StringComparison.Ordinal));
        Assert.IsFalse(patched.Contains("cs-t74 =", StringComparison.Ordinal));
        Assert.IsTrue(File.Exists(Path.Combine(root!, JiggleProjectConfig.DefaultFileName)));
        Assert.IsFalse(File.Exists(Path.Combine(root!, "_JiggleForgeRuntime", "Masks.generated.ini")));
        string projectIniPath = Path.Combine(root!, "_JiggleForgeRuntime", "Project.generated.ini");
        Assert.IsTrue(File.Exists(projectIniPath));
        string initialProjectIni = File.ReadAllText(projectIniPath);
        StringAssert.Contains(initialProjectIni, "[ResourceProjectGroupParameters]");
        StringAssert.Contains(initialProjectIni, "[ResourceProjectMotionStates]");
        Assert.IsFalse(initialProjectIni.Contains("ResourceProjectMotionStates0", StringComparison.Ordinal));
        Assert.IsFalse(initialProjectIni.Contains("ResourceProjectMotionStates1", StringComparison.Ordinal));
        StringAssert.Contains(initialProjectIni, "UpdateProjectMotion");
        Assert.IsFalse(initialProjectIni.Contains(
            "cs-t0 = Resource\\jiggle_forge\\InputController",
            StringComparison.Ordinal));
        Assert.IsFalse(initialProjectIni.Contains(
            "cs-t1 = Resource\\jiggle_forge\\CapturedPick",
            StringComparison.Ordinal));
        Assert.IsFalse(initialProjectIni.Contains("cs-t0 = null", StringComparison.Ordinal));
        Assert.IsFalse(initialProjectIni.Contains("cs-t1 = null", StringComparison.Ordinal));
        Assert.IsFalse(initialProjectIni.Contains("cs-t2 = null", StringComparison.Ordinal));
        Assert.IsFalse(initialProjectIni.Contains("cs-t4 = null", StringComparison.Ordinal));
        Assert.IsFalse(initialProjectIni.Contains("cs-u0 = null", StringComparison.Ordinal));
        string present = GetResourceBody(initialProjectIni, "Present");
        Assert.IsFalse(present.Contains("vs-t75 = null", StringComparison.Ordinal));
        StringAssert.Contains(present, "cs-u0 = ResourceProjectMotionStates");
        Assert.IsFalse(initialProjectIni.Contains("$\\jiggle_forge\\projectStateReadSlot", StringComparison.Ordinal));
        Assert.IsFalse(initialProjectIni.Contains("global $stateReadSlot", StringComparison.Ordinal));
        Assert.IsFalse(initialProjectIni.Contains("ResourceDrawContext", StringComparison.Ordinal));
        string privateDrawBegin = GetResourceBody(initialProjectIni, "CommandListBeginPrivateDraw");
        StringAssert.Contains(privateDrawBegin, "run = CommandList\\jiggle_forge\\BeginAdaptedDraw");
        Assert.IsFalse(privateDrawBegin.Contains("x26 = 3", StringComparison.Ordinal));
        StringAssert.Contains(privateDrawBegin, "vs-t75 = ResourceProjectMotionStates");
        StringAssert.Contains(privateDrawBegin, "vs-t76 = ResourceProjectGroupParameters");
        StringAssert.Contains(privateDrawBegin, "ps-t118 = ResourceProjectIdentity");
        string firstDrawBegin = GetResourceBody(initialProjectIni, "CommandListBeginDraw0001");
        StringAssert.Contains(firstDrawBegin, "run = CommandListBeginPrivateDraw");
        Assert.IsFalse(firstDrawBegin.Contains("x26 = 3", StringComparison.Ordinal));
        StringAssert.Contains(firstDrawBegin, "vs-t72 = ResourceDrawInfluences001");
        Assert.IsFalse(firstDrawBegin.Contains("ps-t118", StringComparison.Ordinal));
        string inspectorIniPath = Path.Combine(root!, "_JiggleForgeRuntime", "Inspector.generated.ini");
        string inspectorShaderPath = Path.Combine(root!, "_JiggleForgeRuntime", "InspectorText.hlsl");
        Assert.IsTrue(File.Exists(inspectorIniPath));
        Assert.IsTrue(File.Exists(inspectorShaderPath));
        string inspectorIni = File.ReadAllText(inspectorIniPath);
        StringAssert.Contains(inspectorIni, "global $drawSeen = 0");
        StringAssert.Contains(inspectorIni, "ResourceInspectorObjectIDs");
        StringAssert.Contains(
            inspectorIni,
            "cs-t0 = Resource\\jiggle_forge\\CapturedPick");
        StringAssert.Contains(inspectorIni, "data = 1 2 0");
        byte[] inspectorShader = File.ReadAllBytes(inspectorShaderPath);
        Assert.IsFalse(
            inspectorShader.Length >= 3 &&
            inspectorShader[0] == 0xEF && inspectorShader[1] == 0xBB && inspectorShader[2] == 0xBF,
            "Generated HLSL must be UTF-8 without BOM for ZZMI compatibility.");
        Assert.AreEqual(0, Directory.EnumerateFiles(root!, "*.cmd", SearchOption.AllDirectories).Count());
        Assert.AreEqual(0, Directory.EnumerateFiles(root!, "*.ps1", SearchOption.AllDirectories).Count());
        Assert.AreEqual(ModImportState.Ready, service.Inspect(root!).State);

        // Simulate a project generated by the earlier Studio build, before
        // per-Mod physics registration existed. Applying the configuration
        // must migrate it in place without requiring the original Mod again.
        string legacyPatched = Regex.Replace(
            patched,
            @"(?m)^[ \t]*cs-t72 = ResourceJiggleForgeDrawState\d+\r?\n[ \t]*cs-t75 = ResourceJiggleForgeDrawPhysics\d+\r?\n[ \t]*run = CommandList\\jiggle_forge\\RegisterGroupParameters\r?\n[ \t]*cs-t72 = null\r?\n[ \t]*cs-t75 = null\r?\n",
            string.Empty);
        legacyPatched = Regex.Replace(
            legacyPatched,
            @"(?ms)^\[ResourceJiggleForgeDrawPhysics\d+\]\s*\r?\n.*?(?=^\[|\z)",
            string.Empty);
        legacyPatched = Regex.Replace(
            legacyPatched,
            @"(?m)^[ \t]*\$\\jiggle_forge_inspector_\d+\\drawSeen\s*=\s*1\r?\n",
            string.Empty);
        File.WriteAllText(iniPath, legacyPatched, Encoding.UTF8);

        JiggleGroupConfig body = new() { Name = "Body" };
        JiggleGroupConfig clothes = new() { Name = "Clothes" };
        config.Groups.Add(body);
        config.Groups.Add(clothes);
        MoveDrawToGroup(config, config.Draws[0], body);
        MoveDrawToGroup(config, config.Draws[1], clothes);
        config.Edges.Add(new JiggleEdgeConfig { From = "Body", To = "Clothes" });
        config.Physics.Radius = 0.123;
        config.Physics.Strength = 0.456;
        config.Physics.VolumeResponse = 1.8;
        config.Physics.WheelDepthStep = 0.01;
        config.Physics.WheelMinDepth = -0.12;
        config.Physics.WheelMaxDepth = 0.14;
        new ModRuntimeCompiler().Apply(root!, config);
        string grouped = File.ReadAllText(iniPath);
        string projectIni = File.ReadAllText(projectIniPath);
        StringAssert.Contains(projectIni, "[ResourceDrawInfluences002]");
        Assert.IsFalse(grouped.Contains("RegisterGroupParameters", StringComparison.Ordinal));
        Assert.IsFalse(grouped.Contains("ProjectMotionStates0", StringComparison.Ordinal));
        Assert.IsFalse(grouped.Contains("ProjectGroupParameters", StringComparison.Ordinal));
        StringAssert.Contains(projectIni, "ProjectMotionStates");
        Assert.IsFalse(projectIni.Contains("ProjectMotionStates0", StringComparison.Ordinal));
        StringAssert.Contains(projectIni, "ProjectGroupParameters");
        Assert.IsFalse(
            grouped.Contains(
                "$\\jiggle_forge\\runtimeEnabled",
                StringComparison.Ordinal));
        Assert.IsFalse(grouped.Contains("x82 =", StringComparison.Ordinal));
        StringAssert.Contains(
            projectIni,
            $"{body.LocalStateId} 2 0.123 0.456 1 1.8 0.75 0.1 0.02 10 0.84 5 0 5 0.01 -0.12 0.14 1 -1 1");

        File.Delete(projectIniPath);
        ModProjectInspection missingRuntime = service.Inspect(root!);
        Assert.AreEqual(ModImportState.RuntimeRepairRequired, missingRuntime.State);
        new ModRuntimeCompiler().Apply(root!, missingRuntime.Configuration!);
        Assert.AreEqual(ModImportState.Ready, service.Inspect(root!).State);

        config.Inspector.Enabled = false;
        new ModRuntimeCompiler().Apply(root!, config);
        Assert.IsFalse(File.Exists(inspectorIniPath));
        Assert.IsFalse(File.Exists(inspectorShaderPath));
        Assert.IsFalse(
            File.ReadAllText(iniPath).Contains("drawSeen = 1", StringComparison.Ordinal));
        projectIni = File.ReadAllText(projectIniPath);
        Assert.IsFalse(projectIni.Contains("drawSeen = 1", StringComparison.Ordinal));
        Assert.IsFalse(
            GetResourceBody(projectIni, "CommandListBeginPrivateDraw")
                .Contains("y26 =", StringComparison.Ordinal));
        Assert.AreEqual(ModImportState.Ready, service.Inspect(root!).State);

        config.Inspector.Enabled = true;
        new ModRuntimeCompiler().Apply(root!, config);
        Assert.IsTrue(File.Exists(inspectorIniPath));
        Assert.IsTrue(File.Exists(inspectorShaderPath));
        projectIni = File.ReadAllText(projectIniPath);
        StringAssert.Contains(GetResourceBody(projectIni, "CommandListBeginPrivateDraw"), "drawSeen = 1");
        StringAssert.Contains(GetResourceBody(projectIni, "CommandListBeginDraw0001"), "y26 = 1");
    }

    [TestMethod]
    public void OriginalDrawWithoutInspectorProducesOnlyStableMarkersUntilAFeatureIsAdded()
    {
        string iniPath = Path.Combine(root!, "Example.ini");
        File.WriteAllText(
            iniPath,
            "[CommandListBody]\r\ndrawindexed = auto\r\n",
            Encoding.UTF8);

        ModProjectService service = new();
        JiggleProjectConfig config = service.CreateInitialConfiguration(service.Inspect(root!));
        config.Inspector.Enabled = false;

        new ModRuntimeCompiler().Apply(root!, config);

        string initialPrivate = GetDrawMarkerBody(File.ReadAllText(iniPath), "Draw0001");
        StringAssert.Contains(initialPrivate, "run = CommandList\\jiggle_forge_project_");
        string initialProjectIni = File.ReadAllText(
            Path.Combine(root!, "_JiggleForgeRuntime", "Project.generated.ini"));
        StringAssert.Contains(initialProjectIni, "[ResourceProjectMotionStates]");

        JiggleGroupConfig original = config.Groups.Single(group =>
            string.Equals(group.Name, OriginalPartsConfig.GroupName, StringComparison.OrdinalIgnoreCase));
        MoveDrawToGroup(config, config.Draws[0], original);

        new ModRuntimeCompiler().Apply(root!, config);

        string markerOnly = GetDrawMarkerBody(File.ReadAllText(iniPath), "Draw0001");
        StringAssert.Contains(markerOnly, "drawindexed = auto");
        Assert.IsFalse(markerOnly.Contains("run =", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(OriginalPartsConfig.GroupName, config.Draws[0].Group);
        CollectionAssert.Contains(
            config.Groups.Single(group => group.Name == OriginalPartsConfig.GroupName).Draws,
            "Draw0001");
        Assert.IsFalse(Directory.Exists(Path.Combine(root!, "_JiggleForgeRuntime")));
        Assert.AreEqual(ModImportState.Ready, service.Inspect(root!).State);

        string sourceMaskDirectory = Path.Combine(root!, "Masks");
        Directory.CreateDirectory(sourceMaskDirectory);
        string maskPath = Path.Combine(sourceMaskDirectory, "Body.dds");
        File.WriteAllBytes(maskPath, [1, 2, 3, 4]);
        config.Draws[0].Mask = "Masks\\Body.dds";
        new ModRuntimeCompiler().Apply(root!, config);

        string projectIniPath = Path.Combine(root!, "_JiggleForgeRuntime", "Project.generated.ini");
        string projectIni = File.ReadAllText(projectIniPath);
        string maskedBegin = GetResourceBody(projectIni, "CommandListBeginDraw0001");
        StringAssert.Contains(maskedBegin, "vs-t72 = ResourceDrawInfluences001");
        StringAssert.Contains(maskedBegin, "vs-t73 = Resource\\jiggle_forge_masks_");
        StringAssert.Contains(maskedBegin, "vs-t77 = Resource\\jiggle_forge\\MotionStates");
        Assert.IsFalse(projectIni.Contains("ResourceProjectIdentity", StringComparison.Ordinal));
        Assert.IsFalse(projectIni.Contains("ResourceProjectMotionStates", StringComparison.Ordinal));
        Assert.IsFalse(maskedBegin.Contains("x26 =", StringComparison.Ordinal));
        string masksIniPath = Path.Combine(root!, "_JiggleForgeRuntime", "Masks.generated.ini");
        Assert.IsTrue(File.Exists(masksIniPath));
        StringAssert.Contains(File.ReadAllText(masksIniPath), "filename = ..\\Masks\\Body.dds");
        Assert.IsFalse(Directory.Exists(Path.Combine(root!, "_JiggleForgeRuntime", "Masks")));

        config.Draws[0].Mask = string.Empty;
        new ModRuntimeCompiler().Apply(root!, config);

        string markerOnlyAgain = GetDrawMarkerBody(File.ReadAllText(iniPath), "Draw0001");
        Assert.IsFalse(markerOnlyAgain.Contains("run =", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(Directory.Exists(Path.Combine(root!, "_JiggleForgeRuntime")));
        Assert.AreEqual(ModImportState.Ready, service.Inspect(root!).State);
    }

    [TestMethod]
    public void DrawsUsingTheSameMaskShareOneDirectResourceWithoutCopies()
    {
        string iniPath = Path.Combine(root!, "Example.ini");
        File.WriteAllText(iniPath, """
            [CommandListBody]
            drawindexed = 300, 0, 0
            drawindexed = 300, 300, 0
            """, Encoding.UTF8);
        string sourceMaskDirectory = Path.Combine(root!, "Textures", "Masks");
        Directory.CreateDirectory(sourceMaskDirectory);
        File.WriteAllBytes(Path.Combine(sourceMaskDirectory, "Shared.dds"), [1, 2, 3, 4]);
        string legacyMaskDirectory = Path.Combine(root!, "_JiggleForgeRuntime", "Masks");
        Directory.CreateDirectory(legacyMaskDirectory);
        File.WriteAllBytes(Path.Combine(legacyMaskDirectory, "Draw0001.dds"), [4, 3, 2, 1]);

        ModProjectService service = new();
        JiggleProjectConfig config = service.CreateInitialConfiguration(service.Inspect(root!));
        config.Inspector.Enabled = false;
        foreach (JiggleDrawConfig draw in config.Draws)
        {
            draw.Mask = "Textures\\Masks\\Shared.dds";
        }

        new ModRuntimeCompiler().Apply(root!, config);

        string masksIni = File.ReadAllText(
            Path.Combine(root!, "_JiggleForgeRuntime", "Masks.generated.ini"));
        Assert.AreEqual(1, Regex.Matches(masksIni, @"(?m)^\[ResourceMaskDraw").Count);
        StringAssert.Contains(masksIni, "filename = ..\\Textures\\Masks\\Shared.dds");
        string projectIni = File.ReadAllText(
            Path.Combine(root!, "_JiggleForgeRuntime", "Project.generated.ini"));
        Assert.AreEqual(2, Regex.Matches(
            projectIni,
            @"vs-t73 = Resource\\jiggle_forge_masks_[^\\]+\\MaskDraw0001").Count);
        Assert.IsFalse(Directory.Exists(Path.Combine(root!, "_JiggleForgeRuntime", "Masks")));
    }

    [TestMethod]
    public void DrawsInOneGroupShareOneProjectPrivateState()
    {
        string iniPath = Path.Combine(root!, "Example.ini");
        File.WriteAllText(iniPath, """
            [CommandListBody]
            drawindexed = 300, 0, 0
            drawindexed = 300, 300, 0
            """, Encoding.UTF8);

        ModProjectService service = new();
        JiggleProjectConfig config = service.CreateInitialConfiguration(service.Inspect(root!));
        JiggleGroupConfig body = new() { Name = "Body" };
        config.Groups.Add(body);
        foreach (JiggleDrawConfig draw in config.Draws)
        {
            MoveDrawToGroup(config, draw, body);
        }

        new ModRuntimeCompiler().Apply(root!, config);

        string projectIni = File.ReadAllText(
            Path.Combine(root!, "_JiggleForgeRuntime", "Project.generated.ini"));
        string patched = File.ReadAllText(iniPath);
        StringAssert.Contains(projectIni, $"namespace = jiggle_forge_project_{config.ProjectId:N}");
        StringAssert.Contains(
            GetResourceBody(projectIni, "ResourceProjectMotionStates"),
            $"array = {body.LocalStateId * 7}");
        Assert.IsFalse(projectIni.Contains("ResourceProjectMotionStates0", StringComparison.Ordinal));
        Assert.IsFalse(projectIni.Contains("ResourceProjectMotionStates1", StringComparison.Ordinal));
        foreach (string resource in new[] { "ResourceDrawInfluences001", "ResourceDrawInfluences002" })
        {
            StringAssert.Contains(
                GetResourceBody(projectIni, resource),
                $"array = 1\r\ndata = {body.LocalStateId}");
        }
        Assert.AreEqual(2, Regex.Matches(
            projectIni,
            Regex.Escape($"z26 = {body.LocalStateId}")).Count);
        Assert.IsFalse(patched.Contains("ResourceJiggleForgeDrawState", StringComparison.Ordinal));
        Assert.IsFalse(patched.Contains("RegisterGroupParameters", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DependencyEdgesApplyTransitivelyAndCyclesTerminate()
    {
        string iniPath = Path.Combine(root!, "Example.ini");
        File.WriteAllText(iniPath, """
            [CommandListBody]
            drawindexed = 300, 0, 0
            drawindexed = 300, 300, 0
            drawindexed = 300, 600, 0
            """, Encoding.UTF8);

        ModProjectService service = new();
        JiggleProjectConfig config = service.CreateInitialConfiguration(service.Inspect(root!));
        string[] groupNames = ["A", "B", "C"];
        for (int index = 0; index < groupNames.Length; index++)
        {
            JiggleDrawConfig draw = config.Draws[index];
            JiggleGroupConfig group = new() { Name = groupNames[index] };
            config.Groups.Add(group);
            MoveDrawToGroup(config, draw, group);
        }

        config.Edges.Add(new JiggleEdgeConfig { From = "A", To = "B" });
        config.Edges.Add(new JiggleEdgeConfig { From = "B", To = "C" });

        new ModRuntimeCompiler().Apply(root!, config);
        string patched = File.ReadAllText(iniPath);
        string projectIni = File.ReadAllText(
            Path.Combine(root!, "_JiggleForgeRuntime", "Project.generated.ini"));
        string stateA = config.Groups.Single(group => group.Name == "A").LocalStateId.ToString();
        string stateB = config.Groups.Single(group => group.Name == "B").LocalStateId.ToString();
        string stateC = config.Groups.Single(group => group.Name == "C").LocalStateId.ToString();

        StringAssert.Contains(GetResourceBody(projectIni, "ResourceDrawInfluences001"), $"array = 1\r\ndata = {stateA}");
        StringAssert.Contains(GetResourceBody(projectIni, "ResourceDrawInfluences002"), $"array = 2\r\ndata = {stateA} {stateB}");
        StringAssert.Contains(GetResourceBody(projectIni, "ResourceDrawInfluences003"), $"array = 3\r\ndata = {stateA} {stateB} {stateC}");

        config.Edges.Add(new JiggleEdgeConfig { From = "C", To = "A" });
        new ModRuntimeCompiler().Apply(root!, config);
        patched = File.ReadAllText(iniPath);
        projectIni = File.ReadAllText(
            Path.Combine(root!, "_JiggleForgeRuntime", "Project.generated.ini"));
        string allStates = $"array = 3\r\ndata = {stateA} {stateB} {stateC}";
        StringAssert.Contains(GetResourceBody(projectIni, "ResourceDrawInfluences001"), allStates);
        StringAssert.Contains(GetResourceBody(projectIni, "ResourceDrawInfluences002"), allStates);
        StringAssert.Contains(GetResourceBody(projectIni, "ResourceDrawInfluences003"), allStates);
    }

    [TestMethod]
    public void DependencyStatesKeepTheirOwnGroupPhysics()
    {
        string iniPath = Path.Combine(root!, "Example.ini");
        File.WriteAllText(iniPath, """
            [CommandListBody]
            drawindexed = 300, 0, 0
            drawindexed = 300, 300, 0
            """, Encoding.UTF8);

        ModProjectService service = new();
        JiggleProjectConfig config = service.CreateInitialConfiguration(service.Inspect(root!));
        JiggleGroupConfig body = new()
        {
            Name = "Body",
            Physics = config.Physics.Clone(),
        };
        body.Physics.Radius = 0.11;
        body.Physics.Strength = 0.22;
        JiggleGroupConfig clothes = new()
        {
            Name = "Clothes",
            Physics = config.Physics.Clone(),
        };
        clothes.Physics.Radius = 0.33;
        clothes.Physics.Strength = 0.44;
        config.Groups.Add(body);
        config.Groups.Add(clothes);
        MoveDrawToGroup(config, config.Draws[0], body);
        MoveDrawToGroup(config, config.Draws[1], clothes);
        config.Edges.Add(new JiggleEdgeConfig { From = "Body", To = "Clothes" });

        new ModRuntimeCompiler().Apply(root!, config);
        string projectIni = File.ReadAllText(
            Path.Combine(root!, "_JiggleForgeRuntime", "Project.generated.ini"));

        StringAssert.Contains(
            GetResourceBody(projectIni, "ResourceProjectGroupParameters"),
            $"{body.LocalStateId} 2 0.11 0.22");
        StringAssert.Contains(
            GetResourceBody(projectIni, "ResourceProjectGroupParameters"),
            $"{clothes.LocalStateId} 2 0.33 0.44");
        StringAssert.Contains(
            GetResourceBody(projectIni, "ResourceDrawInfluences002"),
            $"data = {body.LocalStateId} {clothes.LocalStateId}");
    }

    [TestMethod]
    public void DisabledDrawIsDetectedWithoutBindingDeformationAndCanBeEnabledAgain()
    {
        string iniPath = Path.Combine(root!, "Example.ini");
        File.WriteAllText(iniPath, """
            [CommandListBody]
            drawindexed = 300, 0, 0
            drawindexed = 300, 300, 0
            """, Encoding.UTF8);

        ModProjectService service = new();
        JiggleProjectConfig config = service.CreateInitialConfiguration(service.Inspect(root!));
        JiggleGroupConfig body = new() { Name = "Body" };
        config.Groups.Add(body);
        MoveDrawToGroup(config, config.Draws[0], body);
        MoveDrawToGroup(config, config.Draws[1], body);
        config.Draws[0].DeformationEnabled = false;

        new ModRuntimeCompiler().Apply(root!, config);
        string disabled = File.ReadAllText(iniPath);
        string disabledBody = GetDrawMarkerBody(disabled, "Draw0001");
        string projectIni = File.ReadAllText(
            Path.Combine(root!, "_JiggleForgeRuntime", "Project.generated.ini"));
        string disabledBegin = GetResourceBody(projectIni, "CommandListBeginDraw0001");
        string privateBegin = GetResourceBody(projectIni, "CommandListBeginPrivateDraw");
        StringAssert.Contains(disabledBody, "drawindexed = 300, 0, 0");
        Assert.IsFalse(privateBegin.Contains("x26 = 3", StringComparison.Ordinal));
        StringAssert.Contains(disabledBegin, "y26 = 1");
        StringAssert.Contains(disabledBegin, $"z26 = {body.LocalStateId}");
        StringAssert.Contains(disabledBegin, "run = CommandListBeginPrivateDraw");
        Assert.IsFalse(disabledBody.Contains("PickVisibleRange", StringComparison.Ordinal));
        StringAssert.Contains(privateBegin, "drawSeen = 1");
        StringAssert.Contains(disabledBegin, "vs-t72 = null");
        Assert.IsFalse(disabledBody.Contains("RegisterParams", StringComparison.Ordinal));
        Assert.IsFalse(disabledBody.Contains("vs-t72 = ResourceJiggleForgeDrawState001", StringComparison.Ordinal));
        string enabledSibling = GetResourceBody(projectIni, "CommandListBeginDraw0002");
        StringAssert.Contains(enabledSibling, "run = CommandListBeginPrivateDraw");
        Assert.IsFalse(enabledSibling.Contains("PickVisibleRange", StringComparison.Ordinal));
        StringAssert.Contains(enabledSibling, $"z26 = {body.LocalStateId}");
        Assert.IsFalse(projectIni.Contains("ResourceDrawInfluences001", StringComparison.Ordinal));

        config.Draws[0].DeformationEnabled = true;
        new ModRuntimeCompiler().Apply(root!, config);
        string enabledBody = GetDrawMarkerBody(File.ReadAllText(iniPath), "Draw0001");
        StringAssert.Contains(enabledBody, "run = CommandList\\jiggle_forge\\EndAdaptedDraw");
        Assert.IsFalse(enabledBody.Contains("PickVisibleRange", StringComparison.Ordinal));
        projectIni = File.ReadAllText(
            Path.Combine(root!, "_JiggleForgeRuntime", "Project.generated.ini"));
        string enabledBegin = GetResourceBody(projectIni, "CommandListBeginDraw0001");
        StringAssert.Contains(enabledBegin, "y26 = 1");
        StringAssert.Contains(enabledBegin, $"z26 = {body.LocalStateId}");
        StringAssert.Contains(enabledBegin, "vs-t72");
        StringAssert.Contains(GetResourceBody(projectIni, "CommandListBeginPrivateDraw"), "drawSeen = 1");
        StringAssert.Contains(
            GetResourceBody(projectIni, "ResourceDrawInfluences001"),
            $"data = {body.LocalStateId}");

        config.Draws[0].DeformationEnabled = false;
        new ModRuntimeCompiler().Apply(root!, config);
        string disabledAgain = GetDrawMarkerBody(File.ReadAllText(iniPath), "Draw0001");
        StringAssert.Contains(disabledAgain, $"run = CommandList\\jiggle_forge_project_{config.ProjectId:N}\\BeginDraw0001");
        Assert.IsFalse(disabledAgain.Contains("PickVisibleRange", StringComparison.Ordinal));
        Assert.IsFalse(disabledAgain.Contains("RegisterParams", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AdaptedDrawsNeverSuppressTheDefaultChannel()
    {
        string iniPath = Path.Combine(root!, "Example.ini");
        File.WriteAllText(iniPath, """
            [CommandListBody]
            drawindexed = 300, 0, 0
            drawindexed = auto
            """, Encoding.UTF8);

        ModProjectService service = new();
        JiggleProjectConfig config = service.CreateInitialConfiguration(service.Inspect(root!));

        new ModRuntimeCompiler().Apply(root!, config);
        string patched = File.ReadAllText(iniPath);
        foreach (JiggleDrawConfig draw in config.Draws)
        {
            Assert.IsFalse(
                GetDrawMarkerBody(patched, draw.Id)
                    .Contains("EnableAdaptedOnly", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void OriginalPartsCanShareAGroupAndActAsADependencySource()
    {
        string iniPath = Path.Combine(root!, "Example.ini");
        File.WriteAllText(iniPath, """
            [CommandListBody]
            drawindexed = 300, 0, 0
            drawindexed = 300, 300, 0
            """, Encoding.UTF8);

        ModProjectService service = new();
        JiggleProjectConfig config = service.CreateInitialConfiguration(service.Inspect(root!));
        JiggleGroupConfig original = config.Groups.Single(group =>
            string.Equals(group.Name, OriginalPartsConfig.GroupName, StringComparison.OrdinalIgnoreCase));
        JiggleGroupConfig clothes = new() { Name = "Clothes" };
        config.Groups.Add(clothes);
        MoveDrawToGroup(config, config.Draws[0], original);
        MoveDrawToGroup(config, config.Draws[1], clothes);
        config.Edges.Add(new JiggleEdgeConfig
        {
            From = OriginalPartsConfig.GroupName,
            To = "Clothes",
        });
        config.Inspector.Enabled = false;

        new ModRuntimeCompiler().Apply(root!, config);
        string patched = File.ReadAllText(iniPath);
        string projectIni = File.ReadAllText(
            Path.Combine(root!, "_JiggleForgeRuntime", "Project.generated.ini"));
        string bodyDraw = GetDrawMarkerBody(patched, "Draw0001");
        Assert.IsFalse(bodyDraw.Contains("run =", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(projectIni.Contains("CommandListBeginDraw0001", StringComparison.Ordinal));
        Assert.IsFalse(projectIni.Contains("ResourceDrawInfluences001", StringComparison.Ordinal));
        StringAssert.Contains(
            GetResourceBody(projectIni, "ResourceDrawInfluences002"),
            $"array = 2\r\ndata = 0 {clothes.LocalStateId}");
        Assert.IsFalse(File.Exists(
            Path.Combine(root!, "_JiggleForgeRuntime", "Inspector.generated.ini")));
    }

    [TestMethod]
    public void DependencyEdgeCannotTargetTheOriginalPartsGroup()
    {
        File.WriteAllText(
            Path.Combine(root!, "Example.ini"),
            "[CommandList]\r\ndrawindexed = 300, 0, 0\r\ndrawindexed = 300, 300, 0\r\n",
            Encoding.UTF8);

        ModProjectService service = new();
        JiggleProjectConfig config = service.CreateInitialConfiguration(service.Inspect(root!));
        JiggleGroupConfig source = new() { Name = "Source" };
        source.Draws.Add(config.Draws[0].Id);
        JiggleGroupConfig original = config.Groups.Single(group =>
            string.Equals(group.Name, OriginalPartsConfig.GroupName, StringComparison.OrdinalIgnoreCase));
        original.Draws.Add(config.Draws[1].Id);
        config.Groups.Add(source);
        config.Edges.Add(new JiggleEdgeConfig
        {
            From = "Source",
            To = OriginalPartsConfig.GroupName,
        });

        IReadOnlyList<string> errors = JiggleConfigValidator.Validate(config);

        Assert.IsTrue(errors.Any(error => error.Contains(
            "cannot target the fixed OriginalParts group",
            StringComparison.Ordinal)));
    }

    [TestMethod]
    public void LegacyEditableOriginalPartsRowMigratesToTheFixedGroup()
    {
        File.WriteAllText(
            Path.Combine(root!, "Example.ini"),
            "[CommandList]\r\ndrawindexed = 300, 0, 0\r\ndrawindexed = 300, 300, 0\r\n",
            Encoding.UTF8);

        ModProjectService service = new();
        JiggleProjectConfig config = service.CreateInitialConfiguration(service.Inspect(root!));
        JiggleGroupConfig fixedGroup = config.Groups.Single(group =>
            string.Equals(group.Name, OriginalPartsConfig.GroupName, StringComparison.OrdinalIgnoreCase));
        config.Groups.Remove(fixedGroup);
        config.Draws[0].Group = "Body";
        JiggleGroupConfig legacyBody = new() { Name = "Body" };
        legacyBody.Draws.Add(config.Draws[0].Id);
        config.Groups.Add(legacyBody);
        config.Edges.Add(new JiggleEdgeConfig { From = "Body", To = "Other" });
        JiggleGroupConfig other = new() { Name = "Other" };
        other.Draws.Add(config.Draws[1].Id);
        config.Groups.Add(other);

        string legacy = (JiggleConfigSerializer.Serialize(config) +
                         "\r\n[OriginalParts]\r\ndeform_enabled = false\r\ngroup = \"Body\"\r\n")
            .Replace(
                $"draws = [\"{config.Draws[0].Id}\"]",
                $"draws = [\"{OriginalPartsConfig.Id}\",\"{config.Draws[0].Id}\"]",
                StringComparison.Ordinal);

        JiggleProjectConfig migrated = JiggleConfigSerializer.Parse(legacy);

        JiggleGroupConfig migratedFixed = migrated.Groups.Single(group =>
            string.Equals(group.Name, OriginalPartsConfig.GroupName, StringComparison.OrdinalIgnoreCase));
        CollectionAssert.Contains(migratedFixed.Draws, config.Draws[0].Id);
        Assert.IsFalse(migrated.Groups.Any(group =>
            string.Equals(group.Name, "Body", StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual(OriginalPartsConfig.GroupName, migrated.Draws[0].Group);
        Assert.AreEqual(OriginalPartsConfig.GroupName, migrated.Edges[0].From);
    }

    private static string GetResourceBody(string ini, string resourceName)
    {
        Match match = Regex.Match(
            ini,
            $@"(?ms)^\[{Regex.Escape(resourceName)}\]\s*\r?\n(?<body>.*?)(?=^\[|\z)");
        Assert.IsTrue(match.Success, $"Resource {resourceName} was not generated.");
        return match.Groups["body"].Value;
    }

    private static void MoveDrawToGroup(
        JiggleProjectConfig config,
        JiggleDrawConfig draw,
        JiggleGroupConfig target)
    {
        foreach (JiggleGroupConfig group in config.Groups)
        {
            group.Draws.RemoveAll(drawId =>
                string.Equals(drawId, draw.Id, StringComparison.OrdinalIgnoreCase));
        }
        draw.Group = target.Name;
        target.Draws.Add(draw.Id);
    }

    private static string GetDrawMarkerBody(string ini, string drawId)
    {
        Match match = Regex.Match(
            ini,
            $@"(?ms)^\s*; JIGGLEFORGE_VISIBLE_RANGE BEGIN {Regex.Escape(drawId)}[^\r\n]*\r?\n(?<body>.*?)^\s*; JIGGLEFORGE_VISIBLE_RANGE END");
        Assert.IsTrue(match.Success, $"Runtime marker for {drawId} was not generated.");
        return match.Groups["body"].Value;
    }
}
