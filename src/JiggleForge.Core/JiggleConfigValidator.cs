namespace JiggleForge.Core;

public static class JiggleConfigValidator
{
    public static IReadOnlyList<string> Validate(JiggleProjectConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        List<string> errors = [];
        if (config.SchemaVersion != JiggleProjectConfig.CurrentSchemaVersion)
        {
            errors.Add($"Unsupported schema version: {config.SchemaVersion}.");
        }

        if (config.ProjectId == Guid.Empty)
        {
            errors.Add("Project ID cannot be empty.");
        }

        if (config.StateNamespace is < 0 or > 255)
        {
            errors.Add("State namespace must be between 0 and 255.");
        }

        ValidatePhysics(config.Physics, "Physics", errors);

        HashSet<string> drawIds = new(StringComparer.OrdinalIgnoreCase);
        HashSet<int> stateIndices = [];
        HashSet<int> objectIds = [];
        foreach (JiggleDrawConfig draw in config.Draws)
        {
            if (string.IsNullOrWhiteSpace(draw.Id) || !drawIds.Add(draw.Id))
            {
                errors.Add($"Draw ID is empty or duplicated: {draw.Id}.");
            }

            if (string.IsNullOrWhiteSpace(draw.SourceFile) || Path.IsPathRooted(draw.SourceFile))
            {
                errors.Add($"Draw {draw.Id} must use a relative source file path.");
            }

            if (draw.SourceLine < 1)
            {
                errors.Add($"Draw {draw.Id} has an invalid source line.");
            }

            if (!stateIndices.Add(draw.StateIndex))
            {
                errors.Add($"State index is duplicated: {draw.StateIndex}.");
            }

            if (!objectIds.Add(draw.ObjectId))
            {
                errors.Add($"Object ID is duplicated: {draw.ObjectId}.");
            }

            if (!string.IsNullOrWhiteSpace(draw.Mask) &&
                (Path.IsPathRooted(draw.Mask) || HasParentTraversal(draw.Mask)))
            {
                errors.Add($"Draw {draw.Id} mask path must remain inside the Mod folder.");
            }

            if (draw.Kind == JiggleDrawKind.Numeric &&
                (draw.Count is null or < 1 || draw.FirstIndex is null or < 0 || draw.BaseVertex is null))
            {
                errors.Add($"Draw {draw.Id} has an invalid numeric DrawIndexed range.");
            }
        }

        HashSet<string> groupNames = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> assignedDraws = new(StringComparer.OrdinalIgnoreCase);
        foreach (JiggleGroupConfig group in config.Groups)
        {
            if (string.IsNullOrWhiteSpace(group.Name) || !groupNames.Add(group.Name))
            {
                errors.Add($"Group name is empty or duplicated: {group.Name}.");
            }

            if (group.GraphX.HasValue != group.GraphY.HasValue ||
                (group.GraphX.HasValue &&
                 (!double.IsFinite(group.GraphX.Value) || !double.IsFinite(group.GraphY!.Value))))
            {
                errors.Add($"Group {group.Name} graph position must contain two finite coordinates.");
            }

            ValidatePhysics(group.Physics ?? config.Physics, $"Group {group.Name} physics", errors);

            foreach (string drawId in group.Draws)
            {
                if (!drawIds.Contains(drawId))
                {
                    errors.Add($"Group {group.Name} references unknown draw {drawId}.");
                }
                else if (assignedDraws.TryGetValue(drawId, out string? existing))
                {
                    errors.Add($"Draw {drawId} belongs to both {existing} and {group.Name}.");
                }
                else
                {
                    assignedDraws[drawId] = group.Name;
                }
            }
        }

        foreach (JiggleEdgeConfig edge in config.Edges)
        {
            if (!groupNames.Contains(edge.From) || !groupNames.Contains(edge.To))
            {
                errors.Add($"Edge {edge.From} -> {edge.To} references an unknown group.");
            }
            else if (string.Equals(edge.From, edge.To, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Self edge {edge.From} -> {edge.To} is unnecessary.");
            }
            else if (string.Equals(
                         edge.To,
                         OriginalPartsConfig.GroupName,
                         StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Edge {edge.From} -> {edge.To} cannot target the fixed OriginalParts group. " +
                    "Original parts can share a group or act as an edge source, but cannot read another group's private state.");
            }
        }

        return errors;
    }

    private static void ValidatePhysics(
        PhysicsSettings physics,
        string prefix,
        ICollection<string> errors)
    {
        ValidatePositive(physics.Radius, $"{prefix} radius", errors);
        if (!double.IsFinite(physics.Strength))
        {
            errors.Add($"{prefix} strength must be finite.");
        }
        ValidatePositive(physics.Falloff, $"{prefix} falloff", errors);
        ValidateNonNegative(physics.VolumeResponse, $"{prefix} volume response", errors);
        ValidatePositive(physics.DragScale, $"{prefix} drag scale", errors);
        ValidateUnit(physics.HoldDampingRatio, $"{prefix} hold damping ratio", errors);
        ValidateNonNegative(physics.HoldFrequencyHz, $"{prefix} hold frequency", errors);
        ValidateUnit(physics.ReleaseDampingRatio, $"{prefix} release damping ratio", errors);
        ValidateNonNegative(physics.ReleaseFrequencyHz, $"{prefix} release frequency", errors);
        ValidateNonNegative(physics.ReleaseImpulse, $"{prefix} release impulse", errors);
        ValidatePositive(physics.MaxOffset, $"{prefix} max offset", errors);
        ValidateNonNegative(physics.TargetFollowSeconds, $"{prefix} target follow time", errors);
        ValidatePositive(physics.WheelDepthStep, $"{prefix} wheel depth step", errors);
        if (!double.IsFinite(physics.WheelMinDepth))
        {
            errors.Add($"{prefix} wheel minimum depth must be finite.");
        }
        if (!double.IsFinite(physics.WheelMaxDepth))
        {
            errors.Add($"{prefix} wheel maximum depth must be finite.");
        }
        if (double.IsFinite(physics.WheelMinDepth) &&
            double.IsFinite(physics.WheelMaxDepth) &&
            physics.WheelMinDepth > physics.WheelMaxDepth)
        {
            errors.Add($"{prefix} wheel minimum depth cannot exceed its maximum depth.");
        }
    }

    private static void ValidatePositive(double value, string name, ICollection<string> errors)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            errors.Add($"{name} must be a positive finite number.");
        }
    }

    private static void ValidateNonNegative(double value, string name, ICollection<string> errors)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            errors.Add($"{name} must be finite and at least zero.");
        }
    }

    private static void ValidateUnit(double value, string name, ICollection<string> errors)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
        {
            errors.Add($"{name} must be between zero and one.");
        }
    }

    private static bool HasParentTraversal(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part == "..");
}
