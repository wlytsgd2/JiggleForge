using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JiggleForge.Core;

public sealed partial class ModRuntimeCompiler
{
    private static string PatchIni(
        string source,
        IReadOnlyList<JiggleDrawConfig> draws,
        int stateNamespace,
        IReadOnlyDictionary<string, RuntimeDrawAssignment> assignments,
        bool adaptedDrawsOnly)
    {
        Match[] matches = DrawRegex().Matches(source)
            .Where(match => match.Groups["auto"].Success ||
                            long.Parse(match.Groups["count"].Value, CultureInfo.InvariantCulture) > 0)
            .ToArray();
        if (matches.Length != draws.Count)
        {
            throw new InvalidDataException(
                $"The INI currently contains {matches.Length} supported DrawIndexed commands, but the configuration expects {draws.Count}.");
        }

        StringBuilder output = new(source.Length + (draws.Count * 900));
        int cursor = 0;
        for (int index = 0; index < matches.Length; index++)
        {
            Match match = matches[index];
            JiggleDrawConfig draw = draws[index];
            RuntimeDrawAssignment assignment = assignments[draw.Id];
            string currentCommand = match.Value.Trim().TrimEnd('\r');
            if (!string.Equals(currentCommand, draw.Command, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"{draw.SourceFile}:{draw.SourceLine} changed after analysis. Expected '{draw.Command}', found '{currentCommand}'.");
            }

            string resourceName = $"ResourceJiggleForgeDrawState{DrawOrdinal(draw.Id):D3}";
            IReadOnlyList<RuntimePhysicsBinding> physicsBindings =
                BuildPhysicsBindings(draw, assignment, resourceName);
            string maskNamespace = $"jiggle_forge_masks_{stateNamespace}";
            string maskName = $"Mask{draw.Id}";
            string indent = match.Groups["indent"].Value;

            output.Append(source, cursor, match.Index - cursor);
            output.Append(BuildDrawBlock(
                draw,
                assignment,
                match,
                resourceName,
                physicsBindings,
                maskNamespace,
                maskName,
                stateNamespace,
                indent,
                adaptedDrawsOnly));
            cursor = match.Index + match.Length;

        }

        output.Append(source, cursor, source.Length - cursor);
        output.AppendLine();
        output.Append(BuildStateResourcesBlock(draws, assignments, stateNamespace));
        return output.ToString().ReplaceLineEndings("\r\n");
    }

    private static string BuildDrawBlock(
        JiggleDrawConfig draw,
        RuntimeDrawAssignment assignment,
        Match match,
        string resourceName,
        IReadOnlyList<RuntimePhysicsBinding> physicsBindings,
        string maskNamespace,
        string maskName,
        int stateNamespace,
        string indent,
        bool adaptedDrawsOnly)
    {
        StringBuilder block = new();
        block.Append(indent).Append("; ").Append(ModProjectService.PatchMarker)
            .Append(" BEGIN ").Append(draw.Id)
            .Append(" StateIndex=").Append(draw.StateIndex)
            .Append(" ObjectID=").Append(draw.ObjectId).AppendLine();
        block.Append(indent).Append("; JIGGLEFORGE_STUDIO SOURCE ")
            .Append(draw.SourceFile.Replace('|', '_')).Append('|')
            .Append(draw.SourceLine).Append('|')
            .Append(draw.SourceSection.Replace('|', '_')).AppendLine();
        block.Append(indent).Append("$\\jiggle_forge_inspector_")
            .Append(stateNamespace).Append("\\drawSeen = 1").AppendLine();
        if (adaptedDrawsOnly)
        {
            block.Append(indent).Append("run = CommandList\\jiggle_forge\\EnableAdaptedOnly").AppendLine();
        }

        if (!draw.DeformationEnabled)
        {
            block.Append(indent).Append("vs-t72 = null").AppendLine();
            block.Append(indent).Append("vs-t73 = null").AppendLine();
            AppendPickCommands(block, indent, draw, assignment, resourceName, maskNamespace, maskName, rebindState: false);
            block.Append(match.Value.TrimEnd('\r', '\n')).AppendLine();
            AppendPickObjectReset(block, indent);
            block.Append(indent).Append("; ").Append(ModProjectService.PatchMarker).Append(" END");
            return block.ToString();
        }

        block.Append(indent).Append("vs-t72 = ").Append(resourceName).AppendLine();
        block.Append(indent).Append("vs-t73 = Resource\\").Append(maskNamespace).Append('\\').Append(maskName).AppendLine();
        foreach (RuntimePhysicsBinding binding in physicsBindings)
        {
            AppendPhysicsRegistration(
                block,
                indent,
                binding);
        }
        AppendPickCommands(block, indent, draw, assignment, resourceName, maskNamespace, maskName, rebindState: true);
        AppendConsumerBindings(block, indent);
        block.Append(match.Value.TrimEnd('\r', '\n')).AppendLine();
        AppendConsumerReset(block, indent);
        block.Append(indent).Append("vs-t72 = null").AppendLine();
        block.Append(indent).Append("vs-t73 = null").AppendLine();
        AppendPickObjectReset(block, indent);
        block.Append(indent).Append("; ").Append(ModProjectService.PatchMarker).Append(" END");
        return block.ToString();
    }

    private static void AppendPickCommands(
        StringBuilder block,
        string indent,
        JiggleDrawConfig draw,
        RuntimeDrawAssignment assignment,
        string resourceName,
        string maskNamespace,
        string maskName,
        bool rebindState)
    {
        block.Append(indent).Append("if $\\jiggle_forge\\activePickPipeline > 0").AppendLine();
        block.Append(indent).Append("    $\\jiggle_forge\\pickObjectID = ").Append(assignment.ObjectId).AppendLine();
        block.Append(indent).Append("    $\\jiggle_forge\\pickSourceDraw = ").Append(DrawOrdinal(draw.Id)).AppendLine();
        block.Append(indent).Append("    $\\jiggle_forge\\pickRangeAuto = ").Append(draw.Kind == JiggleDrawKind.Auto ? 1 : 0).AppendLine();
        block.Append(indent).Append("    $\\jiggle_forge\\pickRangeCount = ").Append(draw.Count ?? 0).AppendLine();
        if (draw.Kind == JiggleDrawKind.Numeric)
        {
            block.Append(indent).Append("    $\\jiggle_forge\\pickRangeFirst = ").Append(draw.FirstIndex).AppendLine();
            block.Append(indent).Append("    $\\jiggle_forge\\pickRangeBase = ").Append(draw.BaseVertex).AppendLine();
        }

        block.Append(indent).Append("    run = CommandList\\jiggle_forge\\PickVisibleRange").AppendLine();
        block.Append(indent).Append("    $\\jiggle_forge\\pickRangeAuto = 0").AppendLine();
        block.Append(indent).Append("    $\\jiggle_forge\\pickRangeCount = 0").AppendLine();
        block.Append(indent).Append("    $\\jiggle_forge\\pickSourceDraw = 0").AppendLine();
        if (rebindState)
        {
            block.Append(indent).Append("    vs-t72 = ").Append(resourceName).AppendLine();
            block.Append(indent).Append("    vs-t73 = Resource\\").Append(maskNamespace).Append('\\').Append(maskName).AppendLine();
        }
        else
        {
            block.Append(indent).Append("    vs-t72 = null").AppendLine();
            block.Append(indent).Append("    vs-t73 = null").AppendLine();
        }
        block.Append(indent).Append("endif").AppendLine();
    }

    private static void AppendPickObjectReset(StringBuilder block, string indent)
    {
        block.Append(indent).Append("if $\\jiggle_forge\\activePickPipeline > 0").AppendLine();
        block.Append(indent).Append("    $\\jiggle_forge\\pickObjectID = 1").AppendLine();
        block.Append(indent).Append("endif").AppendLine();
    }

    private static void AppendConsumerBindings(StringBuilder block, string indent)
    {
        block.Append(indent).Append("vs-t75 = Resource\\jiggle_forge\\MotionStates").AppendLine();
        block.Append(indent).Append("vs-t76 = Resource\\jiggle_forge\\GroupParameters").AppendLine();
    }

    private static void AppendConsumerReset(StringBuilder block, string indent)
    {
        block.Append(indent).Append("vs-t75 = null").AppendLine();
        block.Append(indent).Append("vs-t76 = null").AppendLine();
    }

    private static string UpdatePatchedIni(
        string source,
        IReadOnlyCollection<JiggleDrawConfig> draws,
        IReadOnlyDictionary<string, RuntimeDrawAssignment> assignments,
        int stateNamespace,
        bool adaptedDrawsOnly)
    {
        HashSet<string> expected = draws.Select(draw => draw.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> patchedBlocks = new(StringComparer.OrdinalIgnoreCase);
        string updated = MarkerBlockRegex().Replace(source, match =>
        {
            string drawId = match.Groups["id"].Value;
            if (!expected.Contains(drawId))
            {
                return match.Value;
            }

            JiggleDrawConfig draw = draws.Single(candidate =>
                string.Equals(candidate.Id, drawId, StringComparison.OrdinalIgnoreCase));
            RuntimeDrawAssignment assignment = assignments[drawId];
            string body = match.Groups["body"].Value;
            Match[] drawCommands = DrawRegex().Matches(body).Cast<Match>().ToArray();
            if (drawCommands.Length != 1)
            {
                throw new InvalidDataException($"Could not find exactly one DrawIndexed command in the runtime marker for {drawId}.");
            }

            patchedBlocks.Add(drawId);
            string resourceName = $"ResourceJiggleForgeDrawState{DrawOrdinal(drawId):D3}";
            IReadOnlyList<RuntimePhysicsBinding> physicsBindings =
                BuildPhysicsBindings(draw, assignment, resourceName);
            string maskNamespace = $"jiggle_forge_masks_{stateNamespace}";
            string maskName = $"Mask{draw.Id}";
            Match drawCommand = drawCommands[0];
            return BuildDrawBlock(
                draw,
                assignment,
                drawCommand,
                resourceName,
                physicsBindings,
                maskNamespace,
                maskName,
                stateNamespace,
                drawCommand.Groups["indent"].Value,
                adaptedDrawsOnly);
        });

        foreach (JiggleDrawConfig draw in draws)
        {
            if (!patchedBlocks.Contains(draw.Id))
            {
                throw new InvalidDataException($"Could not find the runtime marker for {draw.Id}.");
            }
        }

        int resourceBlockMatches = StateResourcesBlockRegex().Matches(updated).Count;
        string rebuiltResources = BuildStateResourcesBlock(
            draws,
            assignments,
            stateNamespace).TrimEnd();
        if (resourceBlockMatches == 1)
        {
            return StateResourcesBlockRegex().Replace(
                updated,
                rebuiltResources,
                count: 1);
        }
        if (resourceBlockMatches > 1)
        {
            throw new InvalidDataException(
                $"Found more than one generated state resources block: {resourceBlockMatches}.");
        }

        int incompleteTailMatches = StateResourcesTailRegex().Matches(updated).Count;
        if (incompleteTailMatches == 1)
        {
            return StateResourcesTailRegex().Replace(
                updated,
                rebuiltResources,
                count: 1);
        }
        if (incompleteTailMatches > 1)
        {
            throw new InvalidDataException(
                $"Found more than one incomplete generated state resource tail: {incompleteTailMatches}.");
        }

        return updated.TrimEnd() + "\r\n\r\n" + rebuiltResources;
    }

    private static void AppendPhysicsRegistration(
        StringBuilder output,
        string indent,
        RuntimePhysicsBinding binding)
    {
        output.Append(indent).Append("cs-t72 = ").Append(binding.StateResourceName).AppendLine();
        output.Append(indent).Append("cs-t75 = ").Append(binding.PhysicsResourceName).AppendLine();
        output.Append(indent).Append("run = CommandList\\jiggle_forge\\RegisterGroupParameters").AppendLine();
        output.Append(indent).Append("cs-t72 = null").AppendLine();
        output.Append(indent).Append("cs-t75 = null").AppendLine();
    }

    private static string PhysicsResourceName(string drawId) =>
        $"ResourceJiggleForgeDrawPhysics{DrawOrdinal(drawId):D3}";

    private static IReadOnlyList<RuntimePhysicsBinding> BuildPhysicsBindings(
        JiggleDrawConfig draw,
        RuntimeDrawAssignment assignment,
        string mainStateResourceName)
    {
        if (assignment.StateIndices.Count != assignment.StatePhysics.Count)
        {
            throw new InvalidDataException(
                $"Draw {draw.Id} has mismatched state and physics assignment counts.");
        }

        if (assignment.StateIndices.Count == 1)
        {
            return
            [
                new RuntimePhysicsBinding(
                    mainStateResourceName,
                    PhysicsResourceName(draw.Id),
                    assignment.StateIndices[0],
                    assignment.StatePhysics[0]),
            ];
        }

        List<RuntimePhysicsBinding> bindings = [];
        for (int index = 0; index < assignment.StateIndices.Count; index++)
        {
            string suffix = $"{DrawOrdinal(draw.Id):D3}_{index + 1:D3}";
            bindings.Add(new RuntimePhysicsBinding(
                $"ResourceJiggleForgeDrawParamState{suffix}",
                $"ResourceJiggleForgeDrawPhysics{suffix}",
                assignment.StateIndices[index],
                assignment.StatePhysics[index]));
        }
        return bindings;
    }

    private static string BuildStateResourcesBlock(
        IEnumerable<JiggleDrawConfig> draws,
        IReadOnlyDictionary<string, RuntimeDrawAssignment> assignments,
        int stateNamespace)
    {
        List<string> resources = [];
        foreach (JiggleDrawConfig draw in draws)
        {
            RuntimeDrawAssignment assignment = assignments[draw.Id];
            string mainStateResourceName =
                $"ResourceJiggleForgeDrawState{DrawOrdinal(draw.Id):D3}";
            resources.Add(BuildStateResource(mainStateResourceName, assignment.StateIndices));
            foreach (RuntimePhysicsBinding binding in BuildPhysicsBindings(
                         draw,
                         assignment,
                         mainStateResourceName))
            {
                if (!string.Equals(
                        binding.StateResourceName,
                        mainStateResourceName,
                        StringComparison.Ordinal))
                {
                    resources.Add(BuildStateResource(
                        binding.StateResourceName,
                        [binding.StateIndex]));
                }
                resources.Add(BuildPhysicsResource(
                    binding.PhysicsResourceName,
                    binding.Physics));
            }
        }

        StringBuilder output = new();
        output.AppendLine($"; {ModProjectService.PatchMarker} STATE RESOURCES BEGIN");
        output.AppendLine($"; JIGGLEFORGE_STUDIO PROJECT {stateNamespace}");
        output.AppendLine(string.Join("\r\n\r\n", resources));
        output.AppendLine($"; {ModProjectService.PatchMarker} STATE RESOURCES END");
        return output.ToString().ReplaceLineEndings("\r\n");
    }

    private static string BuildStateResource(
        string resourceName,
        IReadOnlyCollection<int> stateIndices) =>
        $"[{resourceName}]\r\n" +
        "type = Buffer\r\n" +
        "format = R32_UINT\r\n" +
        $"array = {stateIndices.Count}\r\n" +
        $"data = {string.Join(' ', stateIndices)}";

    private static string BuildPhysicsResource(string resourceName, PhysicsSettings physics)
    {
        static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        return $"[{resourceName}]\r\n" +
               "type = Buffer\r\n" +
               "format = R32G32B32A32_FLOAT\r\n" +
               "array = 5\r\n" +
               $"data = 0 2 {F(physics.Radius)} {F(physics.Strength)} " +
               $"{F(physics.Falloff)} {F(physics.VolumeResponse)} {F(physics.DragScale)} {F(physics.MaxOffset)} " +
               $"{F(physics.TargetFollowSeconds)} {F(physics.HoldFrequencyHz)} {F(physics.HoldDampingRatio)} {F(physics.ReleaseFrequencyHz)} " +
               $"{F(physics.ReleaseDampingRatio)} {F(physics.ReleaseImpulse)} {F(physics.WheelDepthStep)} {F(physics.WheelMinDepth)} " +
               $"{F(physics.WheelMaxDepth)} 1 -1 1";
    }

}
