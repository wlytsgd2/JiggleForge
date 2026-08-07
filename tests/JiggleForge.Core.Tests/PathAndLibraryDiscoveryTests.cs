using JiggleForge.Core;

namespace JiggleForge.Core.Tests;

[TestClass]
public sealed class PathAndLibraryDiscoveryTests
{
    private string? root;

    [TestInitialize]
    public void CreateRoot()
    {
        root = Path.Combine(Path.GetTempPath(), "JiggleForgeDiscoveryTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void DeleteRoot()
    {
        if (root is not null && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ZzmiResolverAcceptsUnicodeAndCorrectsModsOrParentFolder()
    {
        string launcher = Path.Combine(root!, "中文启动器");
        string zzmi = Path.Combine(launcher, "ZZMI");
        Directory.CreateDirectory(Path.Combine(zzmi, "Mods"));
        Directory.CreateDirectory(Path.Combine(zzmi, "ShaderFixes"));
        File.WriteAllText(Path.Combine(zzmi, "d3dx.ini"), string.Empty);

        ZzmiPathResolution exact = ZzmiPathResolver.Resolve(zzmi);
        ZzmiPathResolution child = ZzmiPathResolver.Resolve(Path.Combine(zzmi, "Mods"));
        ZzmiPathResolution parent = ZzmiPathResolver.Resolve(launcher);

        Assert.IsTrue(exact.IsValid);
        Assert.IsFalse(exact.WasCorrected);
        Assert.AreEqual(zzmi, exact.ResolvedPath);
        Assert.IsTrue(child.IsValid);
        Assert.IsTrue(child.WasCorrected);
        Assert.AreEqual(zzmi, child.ResolvedPath);
        Assert.IsTrue(parent.IsValid);
        Assert.AreEqual(zzmi, parent.ResolvedPath);
    }

    [TestMethod]
    public void ZzmiResolverRejectsFolderThatOnlyLooksSimilar()
    {
        Directory.CreateDirectory(Path.Combine(root!, "Mods"));
        Directory.CreateDirectory(Path.Combine(root!, "ShaderFixes"));

        Assert.IsFalse(ZzmiPathResolver.Resolve(root!).IsValid);
    }

    [TestMethod]
    public void LibraryFindsDirectModsAndUnwrapsGenericContainers()
    {
        string zzmi = CreateZzmi();
        string direct = Path.Combine(zzmi, "Mods", "DirectMod");
        CreateMod(direct, "drawindexed = auto");
        File.WriteAllText(Path.Combine(direct, "Toggle.ini"), "[KeyToggle]\r\nkey = F1\r\n");

        string collection = Path.Combine(zzmi, "Mods", "Collection");
        Directory.CreateDirectory(collection);
        File.WriteAllText(Path.Combine(collection, "manager.ini"), "[Constants]\r\nglobal $active = 0\r\n");
        string slotA = Path.Combine(collection, "SlotA");
        string slotB = Path.Combine(collection, "SlotB");
        Directory.CreateDirectory(slotA);
        Directory.CreateDirectory(slotB);
        File.WriteAllText(Path.Combine(slotA, "slot.ini"), "[KeySlot]\r\nkey = F2\r\n");
        File.WriteAllText(Path.Combine(slotB, "slot.ini"), "[CommandListSlot]\r\n$active = 1\r\n");
        CreateMod(Path.Combine(slotA, "NestedOne"), "drawindexed = 30, 0, 0");
        CreateMod(Path.Combine(slotB, "NestedTwo"), "drawindexed = auto");
        CreateMod(Path.Combine(zzmi, "Mods", "JiggleForgeShaderFix"), "drawindexed = auto");

        IReadOnlyList<ModLibraryEntry> entries = new ModLibraryService().ScanZzmiRoot(zzmi);

        CollectionAssert.AreEquivalent(
            new[] { "DirectMod", "NestedOne", "NestedTwo" },
            entries.Select(entry => entry.DisplayName).ToArray());
        Assert.AreEqual(1, entries.Count(entry => entry.DisplayName == "DirectMod"));
        Assert.IsTrue(entries.All(entry => entry.State == ModImportState.FirstImport));
    }

    [TestMethod]
    public void AdaptedProjectSearchIgnoresOrdinaryAndRestoredMods()
    {
        string zzmi = CreateZzmi();
        CreateMod(Path.Combine(zzmi, "Mods", "Ordinary"), "drawindexed = auto");

        string restored = Path.Combine(zzmi, "Mods", "Restored");
        CreateMod(restored, "drawindexed = auto");
        File.WriteAllBytes(Path.Combine(restored, ModBackupService.BackupFileName), [1, 2, 3]);

        string configured = Path.Combine(zzmi, "Mods", "Configured");
        CreateMod(configured, "drawindexed = auto");
        File.WriteAllText(Path.Combine(configured, JiggleProjectConfig.DefaultFileName), string.Empty);

        string repairable = Path.Combine(zzmi, "Mods", "Collection", "Repairable");
        CreateMod(repairable, "drawindexed = auto");
        Directory.CreateDirectory(Path.Combine(repairable, "_JiggleForgeRuntime"));

        IReadOnlyList<string> roots = new ModLibraryService().FindAdaptedProjectRoots(zzmi);

        CollectionAssert.AreEquivalent(
            new[] { Path.GetFullPath(configured), Path.GetFullPath(repairable) },
            roots.ToArray());
    }

    [TestMethod]
    public void AdaptedProjectSearchStopsAtTheFirstAdaptedWrapper()
    {
        string zzmi = CreateZzmi();
        string wrapper = Path.Combine(zzmi, "Mods", "Wrapper");
        string nested = Path.Combine(wrapper, "Nested");
        CreateMod(nested, "drawindexed = auto");
        File.WriteAllText(Path.Combine(wrapper, JiggleProjectConfig.DefaultFileName), string.Empty);
        File.WriteAllText(Path.Combine(nested, JiggleProjectConfig.DefaultFileName), string.Empty);

        IReadOnlyList<string> roots = new ModLibraryService().FindAdaptedProjectRoots(zzmi);

        CollectionAssert.AreEqual(new[] { Path.GetFullPath(wrapper) }, roots.ToArray());
    }

    [TestMethod]
    public void RenderingOverrideIniMarksOneModEvenWhenItHasNoLiteralDraw()
    {
        string zzmi = CreateZzmi();
        string mod = Path.Combine(zzmi, "Mods", "OverrideMod");
        Directory.CreateDirectory(mod);
        File.WriteAllText(
            Path.Combine(mod, "Main.ini"),
            "[TextureOverrideBody]\r\nhash = 01234567\r\nrun = CommandListBody\r\n");
        CreateMod(Path.Combine(mod, "resources"), "drawindexed = auto");

        IReadOnlyList<ModLibraryEntry> entries = new ModLibraryService().ScanZzmiRoot(zzmi);

        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("OverrideMod", entries[0].DisplayName);
        Assert.AreEqual(1, entries[0].DrawCount);
    }

    [TestMethod]
    public void ExplicitSelectionTreatsAllNestedIniBranchesAsOneMod()
    {
        string container = Path.Combine(root!, "Container");
        string first = Path.Combine(container, "First");
        string second = Path.Combine(container, "Second");
        CreateMod(first, "drawindexed = auto");
        CreateMod(second, "drawindexed = auto");
        ModLibraryService service = new();

        ModFolderResolution several = service.ResolveSelection(container);
        Assert.IsTrue(several.IsValid);
        Assert.IsFalse(several.WasCorrected);
        Assert.AreEqual(container, several.ResolvedPath);

        string wrapper = Path.Combine(root!, "Wrapper");
        string only = Path.Combine(wrapper, "OnlyMod");
        CreateMod(only, "drawindexed = auto");
        ModFolderResolution one = service.ResolveSelection(wrapper);
        Assert.IsTrue(one.IsValid);
        Assert.IsFalse(one.WasCorrected);
        Assert.AreEqual(wrapper, one.ResolvedPath);
    }

    [TestMethod]
    public void ExistingBackupKeepsOldMultiFolderAdaptationAsOneRestorableMod()
    {
        string zzmi = CreateZzmi();
        string wrapper = Path.Combine(zzmi, "Mods", "OldAdaptedMod");
        CreateMod(Path.Combine(wrapper, "Body"), "drawindexed = auto");
        CreateMod(Path.Combine(wrapper, "Outfit"), "drawindexed = 30, 0, 0");
        File.WriteAllBytes(Path.Combine(wrapper, ModBackupService.BackupFileName), [1, 2, 3]);

        ModLibraryService service = new();
        IReadOnlyList<ModLibraryEntry> entries = service.ScanZzmiRoot(zzmi);
        ModFolderResolution selection = service.ResolveSelection(wrapper);

        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("OldAdaptedMod", entries[0].DisplayName);
        Assert.AreEqual(wrapper, entries[0].ModPath);
        Assert.IsTrue(selection.IsValid);
        Assert.IsFalse(selection.WasCorrected);
        Assert.AreEqual(wrapper, selection.ResolvedPath);
    }

    [TestMethod]
    public void SelectionRejectsManagerContainersAndSeveralExistingProjects()
    {
        string managed = Path.Combine(root!, "_MANAGED_");
        CreateMod(Path.Combine(managed, "group_1", "First"), "drawindexed = auto");
        ModLibraryService service = new();

        Assert.IsFalse(service.ResolveSelection(managed).IsValid);
        Assert.IsFalse(service.ResolveSelection(Path.Combine(managed, "group_1")).IsValid);

        string wrapper = Path.Combine(root!, "TwoAdaptedMods");
        string first = Path.Combine(wrapper, "First");
        string second = Path.Combine(wrapper, "Second");
        CreateMod(first, "drawindexed = auto");
        CreateMod(second, "drawindexed = auto");
        File.WriteAllText(Path.Combine(first, JiggleProjectConfig.DefaultFileName), string.Empty);
        File.WriteAllText(Path.Combine(second, JiggleProjectConfig.DefaultFileName), string.Empty);

        ModFolderResolution resolution = service.ResolveSelection(wrapper);
        Assert.IsFalse(resolution.IsValid);
        Assert.AreEqual(2, resolution.Candidates.Count);
    }

    [TestMethod]
    public void SelectionNeverTreatsZzmiOrItsModsRootAsOneMod()
    {
        string zzmi = CreateZzmi();
        CreateMod(Path.Combine(zzmi, "Mods", "OnlyMod"), "drawindexed = auto");
        ModLibraryService service = new();

        ModFolderResolution rootSelection = service.ResolveSelection(zzmi);
        ModFolderResolution modsSelection = service.ResolveSelection(Path.Combine(zzmi, "Mods"));

        Assert.IsFalse(rootSelection.IsValid);
        Assert.IsFalse(modsSelection.IsValid);
        Assert.AreEqual(1, modsSelection.Candidates.Count);
    }

    private string CreateZzmi()
    {
        string zzmi = Path.Combine(root!, "ZZMI");
        Directory.CreateDirectory(Path.Combine(zzmi, "Mods"));
        Directory.CreateDirectory(Path.Combine(zzmi, "ShaderFixes"));
        File.WriteAllText(Path.Combine(zzmi, "d3dx.ini"), string.Empty);
        return zzmi;
    }

    private static void CreateMod(string path, string command)
    {
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "Mod.ini"), $"[CommandList]\r\n{command}\r\n");
    }
}
