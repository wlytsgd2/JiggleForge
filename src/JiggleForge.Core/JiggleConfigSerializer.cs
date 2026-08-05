using System.Globalization;
using System.Text;
using System.Text.Json;

namespace JiggleForge.Core;

public static class JiggleConfigSerializer
{
    public static JiggleProjectConfig Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string text = File.ReadAllText(path, Encoding.UTF8);
        int sourceSchemaVersion = ReadSchemaVersion(text);
        JiggleProjectConfig config = Parse(text);
        if (sourceSchemaVersion < JiggleProjectConfig.CurrentSchemaVersion)
        {
            string backupPath = NextMigrationBackupPath(path, sourceSchemaVersion);
            File.Copy(path, backupPath);
            Save(path, config);
        }

        return config;
    }

    public static void Save(string path, JiggleProjectConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string text = Serialize(config);
        string? parent = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    public static string Serialize(JiggleProjectConfig config)
    {
        IReadOnlyList<string> errors = JiggleConfigValidator.Validate(config);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        }

        StringBuilder output = new();
        output.AppendLine("# JiggleForge project configuration");
        output.AppendLine("# This is the editable source of truth. Runtime INI files are generated from it.");
        output.AppendLine();
        output.AppendLine("[Project]");
        Write(output, "schema", config.SchemaVersion);
        Write(output, "project_id", config.ProjectId.ToString("D"));
        Write(output, "state_namespace", config.StateNamespace);
        output.AppendLine();

        output.AppendLine("[Physics]");
        WritePhysics(output, config.Physics);
        output.AppendLine();

        output.AppendLine("[Inspector]");
        Write(output, "enabled", config.Inspector.Enabled);

        output.AppendLine();
        output.AppendLine("[OriginalParts]");
        Write(output, "deform_enabled", config.OriginalParts.DeformationEnabled);

        foreach (JiggleDrawConfig draw in config.Draws)
        {
            output.AppendLine();
            output.Append("[Draw:").Append(draw.Id).AppendLine("]");
            Write(output, "alias", draw.Alias);
            Write(output, "deform_enabled", draw.DeformationEnabled);
            Write(output, "source_file", NormalizeRelativePath(draw.SourceFile));
            Write(output, "source_section", draw.SourceSection);
            Write(output, "source_line", draw.SourceLine);
            Write(output, "branch", draw.Branch);
            Write(output, "command", draw.Command);
            Write(output, "kind", draw.Kind.ToString().ToLowerInvariant());
            if (draw.Kind == JiggleDrawKind.Numeric)
            {
                Write(output, "count", draw.Count!.Value);
                Write(output, "first_index", draw.FirstIndex!.Value);
                Write(output, "base_vertex", draw.BaseVertex!.Value);
            }

            Write(output, "state_index", draw.StateIndex);
            Write(output, "object_id", draw.ObjectId);
            Write(output, "group", draw.Group);
            Write(output, "mask", NormalizeRelativePath(draw.Mask));
        }

        foreach (JiggleGroupConfig group in config.Groups)
        {
            output.AppendLine();
            output.Append("[Group:").Append(group.Name).AppendLine("]");
            output.Append("draws = ").AppendLine(JsonSerializer.Serialize(group.Draws));
            WritePhysics(output, group.Physics ?? config.Physics);
            if (group.GraphX.HasValue && group.GraphY.HasValue)
            {
                Write(output, "graph_x", group.GraphX.Value);
                Write(output, "graph_y", group.GraphY.Value);
            }
        }

        if (config.Edges.Count > 0)
        {
            output.AppendLine();
            output.AppendLine("[Edges]");
            foreach (JiggleEdgeConfig edge in config.Edges)
            {
                output.Append("edge = ").AppendLine(JsonSerializer.Serialize(new[] { edge.From, edge.To }));
            }
        }

        return output.ToString().ReplaceLineEndings("\r\n");
    }

    public static JiggleProjectConfig Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        JiggleProjectConfig config = new() { ProjectId = Guid.Empty };
        string section = string.Empty;
        JiggleDrawConfig? draw = null;
        JiggleGroupConfig? group = null;
        int lineNumber = 0;

        foreach (string sourceLine in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            lineNumber++;
            string line = sourceLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                draw = null;
                group = null;
                if (section.StartsWith("Draw:", StringComparison.OrdinalIgnoreCase))
                {
                    string id = section[5..].Trim();
                    draw = new JiggleDrawConfig
                    {
                        Id = id,
                        SourceFile = string.Empty,
                        SourceSection = string.Empty,
                        Command = string.Empty,
                    };
                    config.Draws.Add(draw);
                }
                else if (section.StartsWith("Group:", StringComparison.OrdinalIgnoreCase))
                {
                    group = new JiggleGroupConfig { Name = section[6..].Trim() };
                    config.Groups.Add(group);
                }
                else if (!section.Equals("Project", StringComparison.OrdinalIgnoreCase) &&
                         !section.Equals("Physics", StringComparison.OrdinalIgnoreCase) &&
                         !section.Equals("Inspector", StringComparison.OrdinalIgnoreCase) &&
                         !section.Equals("OriginalParts", StringComparison.OrdinalIgnoreCase) &&
                         !section.Equals("Edges", StringComparison.OrdinalIgnoreCase))
                {
                    throw Error(lineNumber, $"Unsupported section [{section}].");
                }

                continue;
            }

            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                throw Error(lineNumber, "Expected key = value.");
            }

            string key = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();
            try
            {
                if (section.Equals("Project", StringComparison.OrdinalIgnoreCase))
                {
                    ParseProject(config, key, value);
                }
                else if (section.Equals("Physics", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryParsePhysics(config.Physics, key, value, config.SchemaVersion))
                    {
                        throw new InvalidDataException($"Unknown Physics key: {key}.");
                    }
                }
                else if (section.Equals("Inspector", StringComparison.OrdinalIgnoreCase))
                {
                    if (!key.Equals("enabled", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"Unknown Inspector key: {key}.");
                    }

                    config.Inspector.Enabled = bool.Parse(value);
                }
                else if (section.Equals("OriginalParts", StringComparison.OrdinalIgnoreCase))
                {
                    switch (key.ToLowerInvariant())
                    {
                        case "deform_enabled":
                            config.OriginalParts.DeformationEnabled = bool.Parse(value);
                            break;
                        case "group":
                            config.OriginalParts.LegacyGroup = ParseString(value);
                            break;
                        default:
                            throw new InvalidDataException($"Unknown OriginalParts key: {key}.");
                    }
                }
                else if (draw is not null)
                {
                    ParseDraw(draw, key, value);
                }
                else if (group is not null)
                {
                    switch (key.ToLowerInvariant())
                    {
                        case "draws":
                            group.Draws.AddRange(ParseStringArray(value));
                            break;
                        case "graph_x":
                            group.GraphX = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
                            break;
                        case "graph_y":
                            group.GraphY = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
                            break;
                        default:
                            group.Physics ??= config.Physics.Clone();
                            if (!TryParsePhysics(group.Physics, key, value, config.SchemaVersion))
                            {
                                throw new InvalidDataException($"Unknown group key: {key}.");
                            }
                            break;
                    }
                }
                else if (section.Equals("Edges", StringComparison.OrdinalIgnoreCase))
                {
                    if (!key.Equals("edge", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"Unknown Edges key: {key}.");
                    }

                    string[] values = ParseStringArray(value);
                    if (values.Length != 2)
                    {
                        throw new InvalidDataException("An edge must contain exactly two group names.");
                    }

                    config.Edges.Add(new JiggleEdgeConfig { From = values[0], To = values[1] });
                }
                else
                {
                    throw new InvalidDataException("A value appears before a supported section.");
                }
            }
            catch (Exception exception) when (exception is FormatException or JsonException or InvalidDataException)
            {
                throw Error(lineNumber, exception.Message, exception);
            }
        }

        if (config.SchemaVersion == 1)
        {
            config.SchemaVersion = JiggleProjectConfig.CurrentSchemaVersion;
        }

        NormalizeOriginalPartsGroup(config);
        foreach (JiggleGroupConfig parsedGroup in config.Groups)
        {
            parsedGroup.Physics ??= config.Physics.Clone();
        }
        IReadOnlyList<string> errors = JiggleConfigValidator.Validate(config);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        }

        return config;
    }

    private static void NormalizeOriginalPartsGroup(JiggleProjectConfig config)
    {
        string legacyName = config.OriginalParts.LegacyGroup.Trim();
        List<JiggleGroupConfig> legacyGroups = config.Groups
            .Where(group =>
                group.Draws.Contains(OriginalPartsConfig.Id, StringComparer.OrdinalIgnoreCase) ||
                (legacyName.Length > 0 &&
                 string.Equals(group.Name, legacyName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        JiggleGroupConfig? fixedGroup = config.Groups.FirstOrDefault(group =>
            string.Equals(group.Name, OriginalPartsConfig.GroupName, StringComparison.OrdinalIgnoreCase));
        bool needsFixedGroup = fixedGroup is not null || legacyGroups.Count > 0;
        if (needsFixedGroup && fixedGroup is null)
        {
            fixedGroup = new JiggleGroupConfig { Name = OriginalPartsConfig.GroupName };
            config.Groups.Add(fixedGroup);
        }

        foreach (JiggleGroupConfig group in legacyGroups)
        {
            group.Draws.RemoveAll(drawId =>
                string.Equals(drawId, OriginalPartsConfig.Id, StringComparison.OrdinalIgnoreCase));
            if (fixedGroup is not null && !ReferenceEquals(group, fixedGroup))
            {
                fixedGroup.Physics ??= group.Physics?.Clone();
                foreach (string drawId in group.Draws)
                {
                    if (!fixedGroup.Draws.Contains(drawId, StringComparer.OrdinalIgnoreCase))
                    {
                        fixedGroup.Draws.Add(drawId);
                    }
                }

                foreach (JiggleDrawConfig draw in config.Draws.Where(draw =>
                             string.Equals(draw.Group, group.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    draw.Group = OriginalPartsConfig.GroupName;
                }

                foreach (JiggleEdgeConfig edge in config.Edges)
                {
                    if (string.Equals(edge.From, group.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        edge.From = OriginalPartsConfig.GroupName;
                    }
                    if (string.Equals(edge.To, group.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        edge.To = OriginalPartsConfig.GroupName;
                    }
                }

                config.Groups.Remove(group);
            }
        }

        config.OriginalParts.LegacyGroup = string.Empty;
    }

    private static void ParseProject(JiggleProjectConfig config, string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "schema": config.SchemaVersion = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "project_id": config.ProjectId = Guid.Parse(ParseString(value)); break;
            case "state_namespace": config.StateNamespace = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "adapted_draws_only":
                // Compatibility with the short-lived project switch that
                // preceded the editable OriginalParts virtual draw.
                config.OriginalParts.DeformationEnabled = !bool.Parse(value);
                break;
            default: throw new InvalidDataException($"Unknown Project key: {key}.");
        }
    }

    private static bool TryParsePhysics(
        PhysicsSettings physics,
        string key,
        string value,
        int schemaVersion)
    {
        double number = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        switch (key.ToLowerInvariant())
        {
            case "radius": physics.Radius = number; return true;
            case "strength": physics.Strength = number; return true;
            case "falloff": physics.Falloff = number; return true;
            case "volume_response": physics.VolumeResponse = number; return true;
            case "drag_scale": physics.DragScale = number; return true;
            case "hold_damping_ratio":
                physics.HoldDampingRatio = number;
                return schemaVersion >= 2;
            case "hold_frequency_hz":
                physics.HoldFrequencyHz = number;
                return schemaVersion >= 2;
            case "release_damping_ratio":
                physics.ReleaseDampingRatio = number;
                return schemaVersion >= 2;
            case "release_frequency_hz":
                physics.ReleaseFrequencyHz = number;
                return schemaVersion >= 2;
            case "release_impulse":
                physics.ReleaseImpulse = number;
                return schemaVersion >= 2;
            case "grab_damping":
                physics.HoldDampingRatio = number;
                return schemaVersion == 1;
            case "grab_spring":
                physics.HoldFrequencyHz = PhysicsSchemaMigration.HoldFrequencyFromV1(number);
                return schemaVersion == 1;
            case "release_damping":
                physics.ReleaseDampingRatio = number;
                return schemaVersion == 1;
            case "release_spring":
                physics.ReleaseFrequencyHz = PhysicsSchemaMigration.ReleaseFrequencyFromV1(number);
                return schemaVersion == 1;
            case "release_kick":
                physics.ReleaseImpulse = PhysicsSchemaMigration.ReleaseImpulseFromV1(number);
                return schemaVersion == 1;
            case "max_offset": physics.MaxOffset = number; return true;
            case "target_follow_seconds":
                physics.TargetFollowSeconds = number;
                return schemaVersion >= 2;
            case "target_follow":
                physics.TargetFollowSeconds = PhysicsSchemaMigration.TargetFollowSecondsFromV1(number);
                return schemaVersion == 1;
            case "wheel_depth_step": physics.WheelDepthStep = number; return true;
            case "wheel_min_depth": physics.WheelMinDepth = number; return true;
            case "wheel_max_depth": physics.WheelMaxDepth = number; return true;
            case "wheel_step": physics.WheelDepthStep = number; return true;
            case "wheel_angle_step":
                // Angle-era configurations used 8 degrees for the same input
                // sensitivity as a 0.02 world-depth notch.
                physics.WheelDepthStep = Math.Clamp(number / 400.0, 0.00025, 0.075);
                return true;
            case "wheel_min_angle":
            case "wheel_max_angle":
                // Angles have no exact independent-depth equivalent. Preserve
                // the signed range used by angle-era projects instead of
                // inheriting a newer application's defaults.
                physics.WheelMinDepth = -0.15;
                physics.WheelMaxDepth = 0.15;
                return true;
            default:
                return false;
        }
    }

    private static void WritePhysics(StringBuilder output, PhysicsSettings physics)
    {
        Write(output, "radius", physics.Radius);
        Write(output, "strength", physics.Strength);
        Write(output, "falloff", physics.Falloff);
        Write(output, "volume_response", physics.VolumeResponse);
        Write(output, "drag_scale", physics.DragScale);
        Write(output, "hold_damping_ratio", physics.HoldDampingRatio);
        Write(output, "hold_frequency_hz", physics.HoldFrequencyHz);
        Write(output, "release_damping_ratio", physics.ReleaseDampingRatio);
        Write(output, "release_frequency_hz", physics.ReleaseFrequencyHz);
        Write(output, "release_impulse", physics.ReleaseImpulse);
        Write(output, "max_offset", physics.MaxOffset);
        Write(output, "target_follow_seconds", physics.TargetFollowSeconds);
        Write(output, "wheel_depth_step", physics.WheelDepthStep);
        Write(output, "wheel_min_depth", physics.WheelMinDepth);
        Write(output, "wheel_max_depth", physics.WheelMaxDepth);
    }

    private static int ReadSchemaVersion(string text)
    {
        string section = string.Empty;
        foreach (string sourceLine in text
                     .Replace("\r\n", "\n", StringComparison.Ordinal)
                     .Replace('\r', '\n')
                     .Split('\n'))
        {
            string line = sourceLine.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                continue;
            }

            if (!section.Equals("Project", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator <= 0 ||
                !line[..separator].Trim().Equals("schema", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return int.Parse(line[(separator + 1)..].Trim(), CultureInfo.InvariantCulture);
        }

        return JiggleProjectConfig.CurrentSchemaVersion;
    }

    private static string NextMigrationBackupPath(string path, int sourceSchemaVersion)
    {
        string basePath = $"{path}.schema{sourceSchemaVersion}.bak";
        if (!File.Exists(basePath))
        {
            return basePath;
        }

        for (int suffix = 2; ; suffix++)
        {
            string candidate = $"{basePath}.{suffix}";
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static void ParseDraw(JiggleDrawConfig draw, string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "alias": draw.Alias = ParseString(value); break;
            case "deform_enabled": draw.DeformationEnabled = bool.Parse(value); break;
            case "source_file": draw.SourceFile = ParseString(value); break;
            case "source_section": draw.SourceSection = ParseString(value); break;
            case "source_line": draw.SourceLine = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "branch": draw.Branch = ParseString(value); break;
            case "command": draw.Command = ParseString(value); break;
            case "kind": draw.Kind = Enum.Parse<JiggleDrawKind>(ParseString(value), ignoreCase: true); break;
            case "count": draw.Count = long.Parse(value, CultureInfo.InvariantCulture); break;
            case "first_index": draw.FirstIndex = long.Parse(value, CultureInfo.InvariantCulture); break;
            case "base_vertex": draw.BaseVertex = long.Parse(value, CultureInfo.InvariantCulture); break;
            case "state_index": draw.StateIndex = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "object_id": draw.ObjectId = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "group": draw.Group = ParseString(value); break;
            case "mask": draw.Mask = ParseString(value); break;
            default: throw new InvalidDataException($"Unknown draw key: {key}.");
        }
    }

    private static string ParseString(string value)
    {
        if (value.StartsWith('"'))
        {
            return JsonSerializer.Deserialize<string>(value) ?? string.Empty;
        }

        return value;
    }

    private static string[] ParseStringArray(string value) =>
        JsonSerializer.Deserialize<string[]>(value) ?? [];

    private static void Write(StringBuilder output, string key, string value) =>
        output.Append(key).Append(" = ").AppendLine(JsonSerializer.Serialize(value));

    private static void Write(StringBuilder output, string key, bool value) =>
        output.Append(key).Append(" = ").AppendLine(value ? "true" : "false");

    private static void Write(StringBuilder output, string key, long value) =>
        output.Append(key).Append(" = ").AppendLine(value.ToString(CultureInfo.InvariantCulture));

    private static void Write(StringBuilder output, string key, double value) =>
        output.Append(key).Append(" = ").AppendLine(value.ToString("0.################", CultureInfo.InvariantCulture));

    private static string NormalizeRelativePath(string value) => value.Replace('/', '\\');

    private static InvalidDataException Error(int line, string message, Exception? inner = null) =>
        new($"JiggleForge.txt line {line}: {message}", inner);
}
