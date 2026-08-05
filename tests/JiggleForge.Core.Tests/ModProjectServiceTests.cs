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
        Assert.AreEqual(config.Draws[0].StateIndex + 1, config.Draws[0].ObjectId);
        Assert.IsTrue(config.Inspector.Enabled);
        Assert.IsFalse(config.OriginalParts.DeformationEnabled);
        Assert.IsTrue(config.Groups.Any(group =>
            string.Equals(group.Name, OriginalPartsConfig.GroupName, StringComparison.OrdinalIgnoreCase)));
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
        StringAssert.Contains(patched, "run = CommandList\\jiggle_forge\\RegisterGroupParameters");
        StringAssert.Contains(patched, "if $\\jiggle_forge\\activePickPipeline > 0");
        Assert.IsFalse(patched.Contains("$\\jiggle_forge\\activePickProfile", StringComparison.Ordinal));
        StringAssert.Contains(patched, "[ResourceJiggleForgeDrawPhysics001]");
        StringAssert.Contains(patched, "data = 0 2 0.12 0.7 1 2 0.75 0.1 0.02 10 0.84 5 0 5 0.02 0 0.15 1 -1 1");
        Assert.IsFalse(patched.Contains("CommandList\\jiggle_forge\\RegisterParams", StringComparison.Ordinal));
        Assert.IsFalse(patched.Contains("cs-t74 =", StringComparison.Ordinal));
        Assert.IsTrue(File.Exists(Path.Combine(root!, JiggleProjectConfig.DefaultFileName)));
        Assert.IsTrue(File.Exists(Path.Combine(root!, "_JiggleForgeRuntime", "Masks.generated.ini")));
        string inspectorIniPath = Path.Combine(root!, "_JiggleForgeRuntime", "Inspector.generated.ini");
        string inspectorShaderPath = Path.Combine(root!, "_JiggleForgeRuntime", "InspectorText.hlsl");
        Assert.IsTrue(File.Exists(inspectorIniPath));
        Assert.IsTrue(File.Exists(inspectorShaderPath));
        string inspectorIni = File.ReadAllText(inspectorIniPath);
        StringAssert.Contains(inspectorIni, "global $inspectorEnabled = 1");
        StringAssert.Contains(inspectorIni, "global $drawSeen = 0");
        StringAssert.Contains(inspectorIni, "ResourceInspectorObjectIDs");
        StringAssert.Contains(
            inspectorIni,
            "cs-t0 = Resource\\jiggle_forge\\CapturedPick");
        StringAssert.Contains(inspectorIni, $"data = {config.Draws[0].ObjectId} {config.Draws[1].ObjectId}");
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

        config.Draws[0].Group = "Body";
        config.Draws[1].Group = "Clothes";
        JiggleGroupConfig body = new() { Name = "Body" };
        body.Draws.Add("Draw0001");
        JiggleGroupConfig clothes = new() { Name = "Clothes" };
        clothes.Draws.Add("Draw0002");
        config.Groups.Add(body);
        config.Groups.Add(clothes);
        config.Edges.Add(new JiggleEdgeConfig { From = "Body", To = "Clothes" });
        config.Physics.Radius = 0.123;
        config.Physics.Strength = 0.456;
        config.Physics.VolumeResponse = 1.8;
        config.Physics.WheelDepthStep = 0.01;
        config.Physics.WheelMinDepth = -0.12;
        config.Physics.WheelMaxDepth = 0.14;
        new ModRuntimeCompiler().Apply(root!, config);
        string grouped = File.ReadAllText(iniPath);
        StringAssert.Contains(grouped, $"array = 2\r\ndata = {config.Draws[0].StateIndex} {config.Draws[1].StateIndex}");
        StringAssert.Contains(grouped, "run = CommandList\\jiggle_forge\\RegisterGroupParameters");
        StringAssert.Contains(grouped, "vs-t75 = Resource\\jiggle_forge\\MotionStates");
        StringAssert.Contains(grouped, "vs-t76 = Resource\\jiggle_forge\\GroupParameters");
        Assert.IsFalse(
            grouped.Contains(
                "$\\jiggle_forge\\runtimeEnabled",
                StringComparison.Ordinal));
        Assert.IsFalse(grouped.Contains("x82 =", StringComparison.Ordinal));
        StringAssert.Contains(
            grouped,
            "data = 0 2 0.123 0.456 1 1.8 0.75 0.1 0.02 10 0.84 5 0 5 0.01 -0.12 0.14 1 -1 1");

        File.Delete(Path.Combine(root!, "_JiggleForgeRuntime", "Masks.generated.ini"));
        ModProjectInspection missingRuntime = service.Inspect(root!);
        Assert.AreEqual(ModImportState.RuntimeRepairRequired, missingRuntime.State);
        new ModRuntimeCompiler().Apply(root!, missingRuntime.Configuration!);
        Assert.AreEqual(ModImportState.Ready, service.Inspect(root!).State);

        config.Inspector.Enabled = false;
        new ModRuntimeCompiler().Apply(root!, config);
        StringAssert.Contains(File.ReadAllText(inspectorIniPath), "global $inspectorEnabled = 0");
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
            draw.Group = groupNames[index];
            JiggleGroupConfig group = new() { Name = groupNames[index] };
            group.Draws.Add(draw.Id);
            config.Groups.Add(group);
        }

        config.Edges.Add(new JiggleEdgeConfig { From = "A", To = "B" });
        config.Edges.Add(new JiggleEdgeConfig { From = "B", To = "C" });

        new ModRuntimeCompiler().Apply(root!, config);
        string patched = File.ReadAllText(iniPath);
        string stateA = config.Draws[0].StateIndex.ToString();
        string stateB = config.Draws[1].StateIndex.ToString();
        string stateC = config.Draws[2].StateIndex.ToString();

        StringAssert.Contains(GetStateResourceBody(patched, 1), $"array = 1\r\ndata = {stateA}");
        StringAssert.Contains(GetStateResourceBody(patched, 2), $"array = 2\r\ndata = {stateA} {stateB}");
        StringAssert.Contains(GetStateResourceBody(patched, 3), $"array = 3\r\ndata = {stateA} {stateB} {stateC}");

        config.Edges.Add(new JiggleEdgeConfig { From = "C", To = "A" });
        new ModRuntimeCompiler().Apply(root!, config);
        patched = File.ReadAllText(iniPath);
        string allStates = $"array = 3\r\ndata = {stateA} {stateB} {stateC}";
        StringAssert.Contains(GetStateResourceBody(patched, 1), allStates);
        StringAssert.Contains(GetStateResourceBody(patched, 2), allStates);
        StringAssert.Contains(GetStateResourceBody(patched, 3), allStates);
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
        config.Draws[0].Group = "Body";
        config.Draws[1].Group = "Clothes";
        JiggleGroupConfig body = new()
        {
            Name = "Body",
            Physics = config.Physics.Clone(),
        };
        body.Physics.Radius = 0.11;
        body.Physics.Strength = 0.22;
        body.Draws.Add(config.Draws[0].Id);
        JiggleGroupConfig clothes = new()
        {
            Name = "Clothes",
            Physics = config.Physics.Clone(),
        };
        clothes.Physics.Radius = 0.33;
        clothes.Physics.Strength = 0.44;
        clothes.Draws.Add(config.Draws[1].Id);
        config.Groups.Add(body);
        config.Groups.Add(clothes);
        config.Edges.Add(new JiggleEdgeConfig { From = "Body", To = "Clothes" });

        new ModRuntimeCompiler().Apply(root!, config);
        string patched = File.ReadAllText(iniPath);

        StringAssert.Contains(
            patched,
            "[ResourceJiggleForgeDrawPhysics002_001]\r\n" +
            "type = Buffer\r\nformat = R32G32B32A32_FLOAT\r\narray = 5\r\n" +
            "data = 0 2 0.11 0.22");
        StringAssert.Contains(
            patched,
            "[ResourceJiggleForgeDrawPhysics002_002]\r\n" +
            "type = Buffer\r\nformat = R32G32B32A32_FLOAT\r\narray = 5\r\n" +
            "data = 0 2 0.33 0.44");
        string drawBody = GetDrawMarkerBody(patched, "Draw0002");
        StringAssert.Contains(drawBody, "cs-t72 = ResourceJiggleForgeDrawParamState002_001");
        StringAssert.Contains(drawBody, "cs-t72 = ResourceJiggleForgeDrawParamState002_002");
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
        config.Draws[0].Group = "Body";
        config.Draws[1].Group = "Body";
        JiggleGroupConfig body = new() { Name = "Body" };
        body.Draws.Add(config.Draws[0].Id);
        body.Draws.Add(config.Draws[1].Id);
        config.Groups.Add(body);
        config.Draws[0].DeformationEnabled = false;

        new ModRuntimeCompiler().Apply(root!, config);
        string disabled = File.ReadAllText(iniPath);
        string disabledBody = GetDrawMarkerBody(disabled, "Draw0001");
        StringAssert.Contains(disabledBody, "drawindexed = 300, 0, 0");
        StringAssert.Contains(disabledBody, "PickVisibleRange");
        StringAssert.Contains(disabledBody, "drawSeen = 1");
        StringAssert.Contains(disabledBody, $"$\\jiggle_forge\\pickObjectID = {config.Draws[0].ObjectId}");
        StringAssert.Contains(disabledBody, "vs-t72 = null");
        Assert.IsFalse(disabledBody.Contains("RegisterParams", StringComparison.Ordinal));
        Assert.IsFalse(disabledBody.Contains("vs-t72 = ResourceJiggleForgeDrawState001", StringComparison.Ordinal));
        string enabledSibling = GetDrawMarkerBody(disabled, "Draw0002");
        StringAssert.Contains(enabledSibling, "PickVisibleRange");
        StringAssert.Contains(enabledSibling, $"$\\jiggle_forge\\pickObjectID = {config.Draws[1].ObjectId}");
        StringAssert.Contains(GetStateResourceBody(disabled, 2), $"data = {config.Draws[1].StateIndex}");

        config.Draws[0].DeformationEnabled = true;
        new ModRuntimeCompiler().Apply(root!, config);
        string enabledBody = GetDrawMarkerBody(File.ReadAllText(iniPath), "Draw0001");
        StringAssert.Contains(enabledBody, "PickVisibleRange");
        StringAssert.Contains(enabledBody, "RegisterGroupParameters");
        StringAssert.Contains(enabledBody, "vs-t72");
        StringAssert.Contains(enabledBody, "drawSeen = 1");

        config.Draws[0].DeformationEnabled = false;
        new ModRuntimeCompiler().Apply(root!, config);
        string disabledAgain = GetDrawMarkerBody(File.ReadAllText(iniPath), "Draw0001");
        StringAssert.Contains(disabledAgain, "PickVisibleRange");
        Assert.IsFalse(disabledAgain.Contains("RegisterParams", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DisablingOriginalPartsAddsAdaptedOnlyModeAndCanBeReversed()
    {
        string iniPath = Path.Combine(root!, "Example.ini");
        File.WriteAllText(iniPath, """
            [CommandListBody]
            drawindexed = 300, 0, 0
            drawindexed = auto
            """, Encoding.UTF8);

        ModProjectService service = new();
        JiggleProjectConfig config = service.CreateInitialConfiguration(service.Inspect(root!));
        config.OriginalParts.DeformationEnabled = false;

        new ModRuntimeCompiler().Apply(root!, config);
        string restricted = File.ReadAllText(iniPath);
        foreach (JiggleDrawConfig draw in config.Draws)
        {
            StringAssert.Contains(
                GetDrawMarkerBody(restricted, draw.Id),
                "run = CommandList\\jiggle_forge\\EnableAdaptedOnly");
        }

        config.OriginalParts.DeformationEnabled = true;
        new ModRuntimeCompiler().Apply(root!, config);
        string unrestricted = File.ReadAllText(iniPath);
        foreach (JiggleDrawConfig draw in config.Draws)
        {
            Assert.IsFalse(
                GetDrawMarkerBody(unrestricted, draw.Id)
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
        config.OriginalParts.DeformationEnabled = true;
        config.Draws[0].Group = OriginalPartsConfig.GroupName;
        config.Draws[1].Group = "Clothes";

        JiggleGroupConfig original = config.Groups.Single(group =>
            string.Equals(group.Name, OriginalPartsConfig.GroupName, StringComparison.OrdinalIgnoreCase));
        original.Draws.Add(config.Draws[0].Id);
        JiggleGroupConfig clothes = new() { Name = "Clothes" };
        clothes.Draws.Add(config.Draws[1].Id);
        config.Groups.Add(clothes);
        config.Edges.Add(new JiggleEdgeConfig
        {
            From = OriginalPartsConfig.GroupName,
            To = "Clothes",
        });

        new ModRuntimeCompiler().Apply(root!, config);
        string patched = File.ReadAllText(iniPath);
        string bodyDraw = GetDrawMarkerBody(patched, config.Draws[0].Id);
        StringAssert.Contains(bodyDraw, "$\\jiggle_forge\\pickObjectID = 1");
        StringAssert.Contains(GetStateResourceBody(patched, 1), "array = 1\r\ndata = 0");
        StringAssert.Contains(
            GetStateResourceBody(patched, 2),
            $"array = 2\r\ndata = 0 {config.Draws[1].StateIndex}");

        string inspector = File.ReadAllText(
            Path.Combine(root!, "_JiggleForgeRuntime", "Inspector.generated.ini"));
        StringAssert.Contains(inspector, "z31 = 3");
        StringAssert.Contains(inspector, "data = 1 ");
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

        string legacy = JiggleConfigSerializer.Serialize(config)
            .Replace(
                "[OriginalParts]\r\ndeform_enabled = true",
                "[OriginalParts]\r\ndeform_enabled = true\r\ngroup = \"Body\"",
                StringComparison.Ordinal)
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

    private static string GetStateResourceBody(string ini, int ordinal)
    {
        Match match = Regex.Match(
            ini,
            $@"(?ms)^\[ResourceJiggleForgeDrawState{ordinal:D3}\]\s*\r?\n(?<body>.*?)(?=^\[|\z)");
        Assert.IsTrue(match.Success, $"State resource {ordinal:D3} was not generated.");
        return match.Groups["body"].Value;
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
