using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace JiggleForge.Core;

public sealed partial class ModRuntimeCompiler
{
    private static IReadOnlyDictionary<string, RuntimeDrawAssignment> BuildAssignments(JiggleProjectConfig config)
    {
        Dictionary<string, JiggleDrawConfig> drawById = config.Draws.ToDictionary(
            draw => draw.Id,
            StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> drawGroup = new(StringComparer.OrdinalIgnoreCase);
        foreach (JiggleGroupConfig group in config.Groups)
        {
            foreach (string drawId in group.Draws)
            {
                drawGroup[drawId] = group.Name;
            }
        }

        Dictionary<string, HashSet<string>> directTargets = config.Groups.ToDictionary(
            group => group.Name,
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        foreach (JiggleEdgeConfig edge in config.Edges)
        {
            directTargets[edge.From].Add(edge.To);
        }

        Dictionary<string, HashSet<string>> reachableTargets = new(StringComparer.OrdinalIgnoreCase);
        foreach (JiggleGroupConfig sourceGroup in config.Groups)
        {
            HashSet<string> reachable = new(StringComparer.OrdinalIgnoreCase)
            {
                sourceGroup.Name,
            };
            Queue<string> pending = new();
            pending.Enqueue(sourceGroup.Name);
            while (pending.TryDequeue(out string? current))
            {
                foreach (string target in directTargets[current])
                {
                    if (reachable.Add(target))
                    {
                        pending.Enqueue(target);
                    }
                }
            }
            reachableTargets[sourceGroup.Name] = reachable;
        }

        Dictionary<string, RuntimeGroupLeader> leaders = new(StringComparer.OrdinalIgnoreCase);
        foreach (JiggleGroupConfig group in config.Groups)
        {
            if (config.OriginalParts.DeformationEnabled &&
                string.Equals(
                    group.Name,
                    OriginalPartsConfig.GroupName,
                    StringComparison.OrdinalIgnoreCase))
            {
                leaders[group.Name] = new RuntimeGroupLeader(ObjectId: 1, StateIndex: 0);
                continue;
            }

            JiggleDrawConfig? leader = group.Draws
                .Select(drawId => drawById[drawId])
                .Where(draw => draw.DeformationEnabled)
                .OrderBy(draw => draw.StateIndex)
                .FirstOrDefault();
            if (leader is not null)
            {
                leaders[group.Name] = new RuntimeGroupLeader(leader.ObjectId, leader.StateIndex);
            }
        }

        Dictionary<int, PhysicsSettings> physicsByState = config.Draws.ToDictionary(
            draw => draw.StateIndex,
            _ => config.Physics,
            EqualityComparer<int>.Default);
        foreach (JiggleGroupConfig group in config.Groups)
        {
            if (leaders.TryGetValue(group.Name, out RuntimeGroupLeader? leader))
            {
                physicsByState[leader.StateIndex] = group.Physics ?? config.Physics;
            }
        }

        RuntimeDrawAssignment CreateAssignment(int objectId, IEnumerable<int> states)
        {
            int[] stateIndices = states.Distinct().ToArray();
            PhysicsSettings[] statePhysics = stateIndices
                .Select(stateIndex => physicsByState.TryGetValue(stateIndex, out PhysicsSettings? physics)
                    ? physics
                    : config.Physics)
                .ToArray();
            return new RuntimeDrawAssignment(objectId, stateIndices, statePhysics);
        }

        Dictionary<string, RuntimeDrawAssignment> assignments = new(StringComparer.OrdinalIgnoreCase);
        foreach (JiggleDrawConfig draw in config.Draws)
        {
            if (!draw.DeformationEnabled)
            {
                assignments[draw.Id] = CreateAssignment(draw.ObjectId, [draw.StateIndex]);
                continue;
            }

            if (!drawGroup.TryGetValue(draw.Id, out string? targetGroup))
            {
                assignments[draw.Id] = CreateAssignment(draw.ObjectId, [draw.StateIndex]);
                continue;
            }

            List<int> stateIndices = [];
            foreach (JiggleGroupConfig sourceGroup in config.Groups)
            {
                if (reachableTargets[sourceGroup.Name].Contains(targetGroup) &&
                    leaders.TryGetValue(sourceGroup.Name, out RuntimeGroupLeader? sourceLeader))
                {
                    stateIndices.Add(sourceLeader.StateIndex);
                }
            }

            if (!leaders.TryGetValue(targetGroup, out RuntimeGroupLeader? targetLeader))
            {
                assignments[draw.Id] = CreateAssignment(draw.ObjectId, [draw.StateIndex]);
                continue;
            }

            assignments[draw.Id] = CreateAssignment(targetLeader.ObjectId, stateIndices);
        }

        return assignments;
    }

    private static string BuildMasksIni(
        string root,
        string runtimeMaskRoot,
        JiggleProjectConfig config,
        IDictionary<string, byte[]> generatedBinary)
    {
        Directory.CreateDirectory(runtimeMaskRoot);
        StringBuilder output = new();
        output.AppendLine("; Generated by JiggleForge. Edit JiggleForge.txt instead.");
        output.Append("namespace = jiggle_forge_masks_").Append(config.StateNamespace).AppendLine();

        foreach (JiggleDrawConfig draw in config.Draws)
        {
            output.AppendLine();
            output.Append("[ResourceMask").Append(draw.Id).AppendLine("]");
            if (string.IsNullOrWhiteSpace(draw.Mask))
            {
                output.AppendLine("; No texture is bound. The shader uses mask 1.0.");
                continue;
            }

            string sourceMask = ResolveInsideRoot(root, draw.Mask);
            if (!File.Exists(sourceMask))
            {
                output.AppendLine("; Configured mask is missing. The shader uses mask 1.0.");
                continue;
            }

            string runtimeName = $"{draw.Id}{Path.GetExtension(sourceMask).ToLowerInvariant()}";
            string runtimePath = Path.Combine(runtimeMaskRoot, runtimeName);
            generatedBinary[runtimePath] = File.ReadAllBytes(sourceMask);
            output.Append("filename = Masks\\").AppendLine(runtimeName);
        }

        return output.ToString().ReplaceLineEndings("\r\n");
    }

    private static string BuildInspectorIni(
        JiggleProjectConfig config,
        IReadOnlyDictionary<string, RuntimeDrawAssignment> assignments)
    {
        const int labelStride = 128;
        const int textCapacity = 256;
        int originalPartsOrdinal = config.Draws.Max(draw => DrawOrdinal(draw.Id)) + 1;
        int drawCount = originalPartsOrdinal;
        uint[] labels = new uint[drawCount * labelStride];
        uint[] objectIds = new uint[drawCount];

        void WriteLabel(int ordinal, string label, uint objectId)
        {
            if (label.Length >= labelStride)
            {
                label = label[..(labelStride - 1)];
            }

            int labelBase = (ordinal - 1) * labelStride;
            for (int index = 0; index < label.Length; index++)
            {
                char value = label[index];
                labels[labelBase + index] = value is >= ' ' and <= '~' ? value : '?';
            }

            objectIds[ordinal - 1] = objectId;
        }

        foreach (JiggleDrawConfig draw in config.Draws)
        {
            int ordinal = DrawOrdinal(draw.Id);
            string alias = string.IsNullOrWhiteSpace(draw.Alias) ? string.Empty : $" ({draw.Alias.Trim()})";
            string branch = string.IsNullOrWhiteSpace(draw.Branch)
                ? string.Empty
                : $" | {draw.Branch.Split('>', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Last()}";
            string label = $"{draw.Id}{alias}{branch} | {draw.SourceFile}:{draw.SourceLine} | [{draw.SourceSection}]";
            WriteLabel(ordinal, label, checked((uint)assignments[draw.Id].ObjectId));
        }
        WriteLabel(
            originalPartsOrdinal,
            "OriginalParts (Original game parts) | global fallback StateIndex 0 / ObjectID 1",
            objectId: 1u);

        string inspectorNamespace = $"jiggle_forge_inspector_{config.StateNamespace}";
        StringBuilder output = new();
        output.AppendLine("; Generated by JiggleForge. Edit JiggleForge.txt instead.");
        output.Append("namespace = ").AppendLine(inspectorNamespace).AppendLine();
        output.AppendLine("[Constants]");
        output.Append("global $inspectorEnabled = ").AppendLine(config.Inspector.Enabled ? "1" : "0");
        output.AppendLine("global $drawSeen = 0");
        output.AppendLine();
        output.AppendLine("[ResourceInspectorLabels]");
        output.AppendLine("type = Buffer");
        output.AppendLine("format = R32_UINT");
        output.Append("array = ").AppendLine(labels.Length.ToString(CultureInfo.InvariantCulture));
        output.Append("data = ").AppendLine(string.Join(' ', labels)).AppendLine();
        output.AppendLine("[ResourceInspectorObjectIDs]");
        output.AppendLine("type = Buffer");
        output.AppendLine("format = R32_UINT");
        output.Append("array = ").AppendLine(objectIds.Length.ToString(CultureInfo.InvariantCulture));
        output.Append("data = ").AppendLine(string.Join(' ', objectIds)).AppendLine();
        output.AppendLine("[ResourceInspectorText]");
        output.AppendLine("type = RWBuffer");
        output.AppendLine("format = R32_UINT");
        output.Append("array = ").AppendLine(textCapacity.ToString(CultureInfo.InvariantCulture));
        output.AppendLine("bind_flags = unordered_access shader_resource").AppendLine();
        output.AppendLine("[ResourceInspectorTextParams]");
        output.AppendLine("type = StructuredBuffer");
        output.AppendLine("array = 1");
        output.AppendLine("data = R32_FLOAT -0.96 0.94 0.96 0.72 1.00 0.85 0.20 1.00 0.00 0.00 0.00 0.72 0.01 0.01 1 1 0 0.85").AppendLine();
        output.AppendLine("[CustomShaderBuildInspectorText]");
        output.AppendLine("cs = ./InspectorText.hlsl");
        output.AppendLine("cs-t0 = Resource\\jiggle_forge\\CapturedPick");
        output.AppendLine("cs-t1 = ResourceInspectorLabels");
        output.AppendLine("cs-t2 = ResourceInspectorObjectIDs");
        output.AppendLine("cs-u0 = ResourceInspectorText");
        output.Append("x31 = ").AppendLine(labelStride.ToString(CultureInfo.InvariantCulture));
        output.Append("y31 = ").AppendLine(drawCount.ToString(CultureInfo.InvariantCulture));
        output.Append("z31 = ").AppendLine(originalPartsOrdinal.ToString(CultureInfo.InvariantCulture));
        output.AppendLine("dispatch = 1, 1, 1");
        output.AppendLine("post cs-t0 = null");
        output.AppendLine("post cs-t1 = null");
        output.AppendLine("post cs-t2 = null");
        output.AppendLine("post cs-u0 = null").AppendLine();
        output.AppendLine("[Present]");
        output.AppendLine("if $inspectorEnabled == 1 && $drawSeen == 1 && $\\jiggle_forge\\mouseDown == 1");
        output.AppendLine("    run = CustomShaderBuildInspectorText");
        output.AppendLine("    Resource\\ZZMIv1\\Text = ref ResourceInspectorText");
        output.AppendLine("    Resource\\ZZMIv1\\TextParams = ref ResourceInspectorTextParams");
        output.AppendLine("    run = CommandList\\ZZMIv1\\PrintText");
        output.AppendLine("endif");
        output.AppendLine("$drawSeen = 0");
        return output.ToString().ReplaceLineEndings("\r\n");
    }

    private static string LoadInspectorShader()
    {
        Assembly assembly = typeof(ModRuntimeCompiler).Assembly;
        string resourceName = assembly.GetManifestResourceNames().Single(name =>
            name.EndsWith(".Runtime.InspectorText.hlsl", StringComparison.Ordinal));
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Embedded Draw Inspector shader was not found.");
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd().ReplaceLineEndings("\r\n");
    }

}
