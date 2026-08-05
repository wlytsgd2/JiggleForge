using System.Text.RegularExpressions;

namespace JiggleForge.Core.Tests;

[TestClass]
public sealed class RuntimeContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string RuntimeIniPath = Path.Combine(
        RepositoryRoot,
        "StandaloneShaderFixes",
        "JiggleForge.ini");

    [TestMethod]
    public void RuntimeResources_HaveExpectedRecordCounts()
    {
        string ini = File.ReadAllText(RuntimeIniPath);

        AssertSectionArray(ini, "ResourceInputController", 2);
        AssertSectionArray(ini, "ResourceCapturedPick", 7);
        AssertSectionArray(ini, "ResourceGroupParameters", 327680);
        AssertSectionArray(ini, "ResourceMotionStates", 458752);
        AssertSectionArray(ini, "ResourceDefaultParameters", 5);
        AssertSectionArray(ini, "ResourceDiagnosticText", 256);
    }

    [TestMethod]
    public void RuntimeDefaultParameters_MatchRecommendedPhysicsDefaults()
    {
        string ini = File.ReadAllText(RuntimeIniPath);
        string defaults = ReadSection(ini, "ResourceDefaultParameters");

        StringAssert.Contains(
            defaults,
            "data = 1 2 0.12 0.7 1.0 2.0 0.75 0.1 0.02 10.0 0.84 5.0 0.0 5.0 0.02 0.0 0.15 1.0 -1.0 1.0");
    }

    [TestMethod]
    public void Runtime_IsAlwaysActiveAndConsumerAccessIsRestrictedToSupportedSlots()
    {
        string ini = File.ReadAllText(RuntimeIniPath);

        string present = ReadSection(ini, "Present");
        StringAssert.Contains(present, "run = CommandListRuntimeStep");
        Assert.AreEqual(
            14,
            Regex.Matches(
                ini,
                @"(?im)^\s*vs-t75\s*=\s*ResourceMotionStates\s*$").Count,
            "The eleven render paths, two picker paths, and visible-range rebind must use the motion-state table.");
        Assert.AreEqual(
            14,
            Regex.Matches(
                ini,
                @"(?im)^\s*vs-t76\s*=\s*ResourceGroupParameters\s*$").Count,
            "The eleven render paths, two picker paths, and visible-range rebind must use the group-parameter table.");
        Assert.IsFalse(ini.Contains("vs-t66", StringComparison.Ordinal));
    }

    [TestMethod]
    public void GlobalFallback_PicksOnlyBeforeSkin()
    {
        string ini = File.ReadAllText(RuntimeIniPath);

        foreach (string sectionName in new[]
                 {
                     "ShaderOverrideJiggleForgeGlobalStreet2621",
                     "ShaderOverrideJiggleForgeGlobalC280"
                 })
        {
            string section = ReadSection(ini, sectionName);
            int pickIndex = section.IndexOf(
                "$pickPriority = 1",
                StringComparison.Ordinal);
            int skinIndex = section.IndexOf(
                "run = CommandList\\ZZMIv1\\Skin",
                StringComparison.Ordinal);

            Assert.IsTrue(pickIndex >= 0, $"{sectionName} is missing its pre-Skin fallback pick.");
            Assert.IsTrue(skinIndex > pickIndex, $"{sectionName} must pick before Skin.");
            Assert.IsFalse(
                section.Contains("$pickPriority = 2", StringComparison.Ordinal),
                $"{sectionName} must not perform a post-Skin fallback pick.");
            Assert.IsFalse(
                section.Contains("preSkin", StringComparison.OrdinalIgnoreCase),
                $"{sectionName} still contains the retired Skin resource comparison.");
        }
    }

    [TestMethod]
    public void RetiredSolverResourcesAndCompatibilityEntriesAreRemoved()
    {
        string ini = File.ReadAllText(RuntimeIniPath);
        Match present = Regex.Match(
            ini,
            @"(?ms)^\[Present\]\s*$.*?(?=^\[|\z)");

        Assert.IsTrue(present.Success);
        Assert.IsFalse(
            present.Value.Contains(
                "run = CustomShaderJiggleState",
                StringComparison.Ordinal));
        Assert.IsFalse(
            Regex.IsMatch(
                ini,
                @"(?m)^\[CommandListRegisterParams\]\s*$"));
        Assert.IsFalse(
            ini.Contains(
                "run = CustomShaderRegisterParams",
                StringComparison.Ordinal));
        foreach (string removedSection in new[]
                 {
                     "ResourceParams",
                     "ResourceDisabledState",
                     "ResourceJiggleState",
                     "CustomShaderJiggleState"
                 })
        {
            Assert.IsFalse(
                Regex.IsMatch(
                    ini,
                    $@"(?m)^\[{Regex.Escape(removedSection)}\]\s*$"),
                $"The retired section [{removedSection}] is still present.");
        }

        string runtimeRoot = Path.Combine(
            RepositoryRoot,
            "StandaloneShaderFixes",
            "JiggleForge");
        foreach (string removedEntry in new[]
                 {
                     Path.Combine("modules", "jiggle_interaction.hlsl"),
                     Path.Combine("modules", "register_params.hlsl"),
                     Path.Combine("shaders", "1f6_jiggle.hlsl")
                 })
        {
            Assert.IsFalse(
                File.Exists(Path.Combine(runtimeRoot, removedEntry)),
                $"The retired entry shader {removedEntry} is still present.");
        }
    }

    [TestMethod]
    public void Present_UpdatesRuntimeBeforeClearingCurrentFramePick()
    {
        string ini = File.ReadAllText(RuntimeIniPath);
        string present = ReadSection(ini, "Present");
        int runtimeStepIndex = present.IndexOf(
            "run = CommandListRuntimeStep",
            StringComparison.Ordinal);
        int resetIndex = present.IndexOf(
            "run = CustomShaderResetFramePick",
            StringComparison.Ordinal);

        Assert.IsTrue(runtimeStepIndex >= 0, "The runtime update dispatch is missing.");
        Assert.IsTrue(resetIndex >= 0, "The frame-pick reset is missing.");
        Assert.IsTrue(
            runtimeStepIndex < resetIndex,
            "The runtime must consume the current frame pick before it is reset.");

        int diagnosticIndex = present.IndexOf(
            "run = CustomShaderBuildRuntimeDiagnostics",
            StringComparison.Ordinal);
        Assert.IsTrue(
            diagnosticIndex > runtimeStepIndex && diagnosticIndex < resetIndex,
            "The diagnostic snapshot must read the updated state before reset.");
    }

    [TestMethod]
    public void AutomaticCalibration_CapturesBothCompositionRoutesForNextFrame()
    {
        string ini = File.ReadAllText(RuntimeIniPath);

        StringAssert.Contains(ini, "hash = 6443f79de027d780");
        StringAssert.Contains(ini, "hash = edf794e6c2599288");
        StringAssert.Contains(ini, "hash = 1da80a4543267137");
        string edfOverride = ReadSection(
            ini,
            "ShaderOverrideJiggleForgeCalibrationRoleCompositeEDF7");
        StringAssert.Contains(edfOverride, "run = CommandListCaptureRoleCalibration");
        Assert.IsFalse(edfOverride.Contains("ps-t0 ===", StringComparison.Ordinal));

        string producerOverride = ReadSection(
            ini,
            "ShaderOverrideJiggleForgeCalibrationRoleProducer");
        StringAssert.Contains(producerOverride, "ResourceRoleCompositeSource = ref o0");
        StringAssert.Contains(producerOverride, "clear = ResourceAutoCalibrationMap 0.0");
        StringAssert.Contains(producerOverride, "$roleCompositeSourceReady = 1");

        string wallpaperOverride = ReadSection(
            ini,
            "ShaderOverrideJiggleForgeCalibrationRoleComposite857C");
        StringAssert.Contains(wallpaperOverride, "hash = 857cd16250c6142e");
        StringAssert.Contains(wallpaperOverride, "if $roleCompositeSourceReady == 1");
        StringAssert.Contains(
            wallpaperOverride,
            "run = CommandListCaptureFilteredRoleCalibration");

        string roleCapture = ReadSection(
            ini,
            "CommandListCaptureRoleCalibration");
        StringAssert.Contains(roleCapture, "run = CustomShaderCaptureRoleCalibrationMap");
        StringAssert.Contains(roleCapture, "$calibrationRouteThisFrame = 30");

        string filteredRoleCapture = ReadSection(
            ini,
            "CommandListCaptureFilteredRoleCalibration");
        StringAssert.Contains(
            filteredRoleCapture,
            "ResourceRoleCompositeCandidate = ref ps-t0");
        StringAssert.Contains(
            filteredRoleCapture,
            "run = CustomShaderCompareRoleComposite");
        StringAssert.Contains(
            filteredRoleCapture,
            "run = CustomShaderCaptureFilteredRoleCalibrationMap");
        Assert.IsFalse(
            filteredRoleCapture.Contains(
                "ResourceRoleCompositeVB = copy vb0",
                StringComparison.Ordinal));
        Assert.IsFalse(
            filteredRoleCapture.Contains(
                "clear = ResourceAutoCalibrationMap",
                StringComparison.Ordinal));

        string worldCapture = ReadSection(
            ini,
            "CommandListCaptureWorldCalibration");
        StringAssert.Contains(worldCapture, "run = CustomShaderCaptureWorldCalibrationMap");
        StringAssert.Contains(worldCapture, "$calibrationRouteThisFrame = 10");

        string present = ReadSection(ini, "Present");
        int promoteIndex = present.IndexOf(
            "$calibrationRoutePreviousFrame = $calibrationRouteThisFrame",
            StringComparison.Ordinal);
        int clearIndex = present.IndexOf(
            "$calibrationRouteThisFrame = 0",
            StringComparison.Ordinal);
        Assert.IsTrue(promoteIndex >= 0 && clearIndex > promoteIndex);

        string calibrationShader = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "StandaloneShaderFixes",
            "JiggleForge",
            "modules",
            "capture_composite_uv_ps.hlsl"));
        StringAssert.Contains(calibrationShader, "return float4(sourceUV, 1.0, 1.0);");

        string roleCalibrationShader = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "StandaloneShaderFixes",
            "JiggleForge",
            "modules",
            "capture_role_composite_uv_ps.hlsl"));
        StringAssert.Contains(roleCalibrationShader, "float4 inputColor : COLOR0");
        StringAssert.Contains(roleCalibrationShader, "float2 sourceUV : TEXCOORD0");

        string roleCalibrationVertexShader = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "StandaloneShaderFixes",
            "JiggleForge",
            "modules",
            "capture_role_composite_raw_vs.hlsl"));
        StringAssert.Contains(
            roleCalibrationVertexShader,
            "ByteAddressBuffer RoleCompositeVertices : register(t110)");
        StringAssert.Contains(roleCalibrationVertexShader, "IniParams[94]");

        string calibrationResource = ReadSection(ini, "ResourceAutoCalibrationMap");
        StringAssert.Contains(calibrationResource, "width = 320");
        StringAssert.Contains(calibrationResource, "height = 180");

        string roleCaptureShader = ReadSection(
            ini,
            "CustomShaderCaptureRoleCalibrationMap");
        StringAssert.Contains(roleCaptureShader, "draw = 6, 0");
        StringAssert.Contains(
            roleCaptureShader,
            "vs = ./JiggleForge/modules/capture_role_composite_raw_vs.hlsl");
        StringAssert.Contains(roleCaptureShader, "vs-t110 = ResourceRoleCompositeVB");
        StringAssert.Contains(
            roleCaptureShader,
            "ps = ./JiggleForge/modules/capture_role_composite_uv_ps.hlsl");
        Assert.IsFalse(roleCaptureShader.Contains("drawindexed = auto", StringComparison.Ordinal));

        string filteredRoleCaptureShader = ReadSection(
            ini,
            "CustomShaderCaptureFilteredRoleCalibrationMap");
        StringAssert.Contains(
            filteredRoleCaptureShader,
            "ps = ./JiggleForge/modules/capture_filtered_role_composite_uv_ps.hlsl");
        StringAssert.Contains(
            filteredRoleCaptureShader,
            "ps-t111 = ResourceRoleCompositeMatch");
        StringAssert.Contains(filteredRoleCaptureShader, "draw = from_caller");
        Assert.IsFalse(filteredRoleCaptureShader.Contains("vs =", StringComparison.Ordinal));

        string roleComparisonShader = ReadSection(
            ini,
            "CustomShaderCompareRoleComposite");
        StringAssert.Contains(
            roleComparisonShader,
            "cs = ./JiggleForge/modules/compare_role_composite_cs.hlsl");
        StringAssert.Contains(
            roleComparisonShader,
            "cs-t110 = ResourceRoleCompositeCandidate");
        StringAssert.Contains(
            roleComparisonShader,
            "cs-t111 = ResourceRoleCompositeSource");
        StringAssert.Contains(
            roleComparisonShader,
            "cs-u0 = ResourceRoleCompositeMatch");

        string filteredRoleShader = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "StandaloneShaderFixes",
            "JiggleForge",
            "modules",
            "capture_filtered_role_composite_uv_ps.hlsl"));
        StringAssert.Contains(filteredRoleShader, "RoleTextureMatch[0]");
        StringAssert.Contains(filteredRoleShader, "discard;");

        string comparisonShader = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "StandaloneShaderFixes",
            "JiggleForge",
            "modules",
            "compare_role_composite_cs.hlsl"));
        StringAssert.Contains(comparisonShader, "[numthreads(1, 1, 1)]");
        StringAssert.Contains(comparisonShader, "GetDimensions");
        StringAssert.Contains(comparisonShader, "RoleTextureMatch[0] = 1u");

        string worldCaptureShader = ReadSection(
            ini,
            "CustomShaderCaptureWorldCalibrationMap");
        StringAssert.Contains(worldCaptureShader, "draw = from_caller");
        Assert.IsFalse(worldCaptureShader.Contains("vs =", StringComparison.Ordinal));
        Assert.IsFalse(worldCaptureShader.Contains("drawindexed = auto", StringComparison.Ordinal));

        string pickerShader = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "StandaloneShaderFixes",
            "JiggleForge",
            "modules",
            "native_world_pick_split.hlsl"));
        StringAssert.Contains(pickerShader, "SampleCalibrationMap(cursorUV)");
        StringAssert.Contains(pickerShader, "float2(screenUV.x, 1.0 - screenUV.y)");
        StringAssert.Contains(pickerShader, "capturedRoute > 0.0");
        StringAssert.Contains(pickerShader, "cursorUV = float2(-10.0, -10.0)");
        Assert.IsFalse(pickerShader.Contains("activeProfile", StringComparison.Ordinal));
        Assert.IsFalse(ini.Contains("$activePickProfile", StringComparison.Ordinal));
        Assert.IsFalse(ini.Contains("$framePickProfile", StringComparison.Ordinal));
        Assert.IsFalse(ini.Contains("1.011335", StringComparison.Ordinal));
        Assert.IsFalse(ini.Contains("384.984375", StringComparison.Ordinal));
    }

    [TestMethod]
    public void FramePickReset_UsesOneIndependentThreadPerRecord()
    {
        string resetPath = Path.Combine(
            RepositoryRoot,
            "StandaloneShaderFixes",
            "JiggleForge",
            "runtime",
            "reset_frame_pick_cs.hlsl");
        string shader = File.ReadAllText(resetPath);

        StringAssert.Contains(shader, "[numthreads(8, 1, 1)]");
        StringAssert.Contains(shader, "FramePickRecords[recordIndex]");
        StringAssert.Contains(shader, "asfloat(0x7f7fffffu)");
        Assert.IsFalse(shader.Contains("[loop]", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RuntimeEntryShaders_ArePresentAndReferenced()
    {
        string ini = File.ReadAllText(RuntimeIniPath);
        string runtimeDirectory = Path.Combine(
            RepositoryRoot,
            "StandaloneShaderFixes",
            "JiggleForge",
            "runtime");
        string[] entryShaders =
        [
            "update_input_cs.hlsl",
            "build_diagnostic_text_cs.hlsl",
            "update_motion_cs.hlsl",
            "register_draw_parameters_cs.hlsl",
            "register_default_parameters_cs.hlsl",
            "reset_frame_pick_cs.hlsl"
        ];

        foreach (string entryShader in entryShaders)
        {
            Assert.IsTrue(
                File.Exists(Path.Combine(runtimeDirectory, entryShader)),
                $"{entryShader} is missing.");
            StringAssert.Contains(ini, $"./JiggleForge/runtime/{entryShader}");
        }
    }

    [TestMethod]
    public void SupportedShaders_ReadTheBoundStateListContract()
    {
        string consumerPath = Path.Combine(
            RepositoryRoot,
            "StandaloneShaderFixes",
            "ShaderFixes",
            "JiggleForgeRuntime",
            "deformation_field.hlsl");
        string consumer = File.ReadAllText(consumerPath);
        StringAssert.Contains(consumer, "float3 JF_EvaluateGrabField(");
        StringAssert.Contains(consumer, "void JF_ReconstructSurfaceFrame(");

        string boundConsumerPath = Path.Combine(
            RepositoryRoot,
            "StandaloneShaderFixes",
            "ShaderFixes",
            "JiggleForgeRuntime",
            "draw_state_consumer.hlsl");
        string boundConsumer = File.ReadAllText(boundConsumerPath);
        StringAssert.Contains(boundConsumer, "JiggleForgeDirectStateIndex[listIndex]");
        StringAssert.Contains(boundConsumer, "uint motionBase = stateIndex * 7u;");
        StringAssert.Contains(boundConsumer, "uint parameterBase = stateIndex * 5u;");
        StringAssert.Contains(boundConsumer, "JF_MotionState[motionBase + 0u]");
        StringAssert.Contains(boundConsumer, "JF_MotionState[motionBase + 2u]");
        StringAssert.Contains(boundConsumer, "JF_GroupParams[parameterBase + 0u]");
        StringAssert.Contains(boundConsumer, "JF_GroupParams[parameterBase + 1u]");
        StringAssert.Contains(boundConsumer, "JF_GroupParams[parameterBase + 4u]");

        string[] consumerHashes =
        [
            "c280f6945b23a42a",
            "26214fb5eedfcbdd",
            "699981e2a62dd9b4",
            "402766e2987d7821",
            "6883e4375b728e90",
            "1f6ab42231416fdb",
            "aa59281029db3a5a",
            "160b58ea1824c794",
            "a0b37a7c7c2a1905",
            "ad24b1c214866fd7",
            "d0a1a756bd3bde31"
        ];
        foreach (string hash in consumerHashes)
        {
            string replacementPath = Path.Combine(
                RepositoryRoot,
                "StandaloneShaderFixes",
                "ShaderFixes",
                $"{hash}-vs_replace.txt");
            string shader = File.ReadAllText(replacementPath);
            StringAssert.Contains(
                shader,
                "Buffer<float4> JF_MotionState : register(t75);");
            StringAssert.Contains(
                shader,
                "Buffer<float4> JF_GroupParams : register(t76);");
            StringAssert.Contains(
                shader,
                "#include \"JiggleForgeRuntime/draw_state_consumer.hlsl\"");
            StringAssert.Contains(shader, "JF_EvaluateBoundStates(");
            Assert.IsFalse(shader.Contains("register(t66)", StringComparison.Ordinal));
            Assert.IsFalse(shader.Contains("JiggleForgeState", StringComparison.Ordinal));
            Assert.IsFalse(shader.Contains("IniParams[82]", StringComparison.Ordinal));
            Assert.IsFalse(
                shader.Contains(
                    "ComputeJiggleForgeKelvinletGrab",
                    StringComparison.Ordinal));
            Assert.IsFalse(
                shader.Contains(
                    "ComputeJiggleForgeWorldInfluence",
                    StringComparison.Ordinal));
            if (hash is "c280f6945b23a42a"
                or "26214fb5eedfcbdd"
                or "1f6ab42231416fdb")
            {
                StringAssert.Contains(
                    shader,
                    "JF_ReconstructSurfaceFrameFromSamples(");
            }
            Assert.IsFalse(
                shader.Contains("JF_TestRadialWeight", StringComparison.Ordinal),
                "The radial visibility probe must be replaced by the independent deformation field.");
        }
    }

    [TestMethod]
    public void PickerShaders_UseOnlyTheFormalConsumerContract()
    {
        string pickerRoot = Path.Combine(
            RepositoryRoot,
            "StandaloneShaderFixes",
            "JiggleForge",
            "shaders");
        foreach (string picker in new[] { "c280_jiggle.hlsl", "2621_jiggle.hlsl" })
        {
            string shader = File.ReadAllText(Path.Combine(pickerRoot, picker));
            StringAssert.Contains(
                shader,
                "#include \"../runtime/draw_state_consumer.hlsl\"");
            StringAssert.Contains(shader, "JF_EvaluateBoundStates(");
            Assert.IsFalse(shader.Contains("register(t66)", StringComparison.Ordinal));
            Assert.IsFalse(shader.Contains("JiggleForgeState", StringComparison.Ordinal));
        }

        string runtimeRoot = Path.Combine(
            RepositoryRoot,
            "StandaloneShaderFixes",
            "JiggleForge",
            "runtime");
        Assert.IsTrue(File.Exists(Path.Combine(
            runtimeRoot,
            "draw_state_consumer.hlsl")));
        Assert.IsTrue(File.Exists(Path.Combine(
            runtimeRoot,
            "deformation_field.hlsl")));
    }

    private static void AssertSectionArray(
        string ini,
        string sectionName,
        int expectedCount)
    {
        string section = ReadSection(ini, sectionName);
        Assert.IsTrue(
            Regex.IsMatch(
                section,
                $@"(?im)^\s*array\s*=\s*{expectedCount}\s*$"),
            $"{sectionName} must contain array = {expectedCount}.");
    }

    private static string ReadSection(string ini, string sectionName)
    {
        Match match = Regex.Match(
            ini,
            $@"(?ms)^\[{Regex.Escape(sectionName)}\]\s*$\r?\n(?<body>.*?)(?=^\[|\z)");
        Assert.IsTrue(match.Success, $"Section [{sectionName}] was not found.");
        return match.Groups["body"].Value;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "JiggleForge.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the JiggleForge repository root.");
    }
}
