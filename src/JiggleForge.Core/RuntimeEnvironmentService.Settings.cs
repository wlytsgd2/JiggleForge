using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JiggleForge.Core;

public sealed partial class RuntimeEnvironmentService
{
    public void SetDragKey(string zzmiRoot, string dragKey)
    {
        SetDragKeys(zzmiRoot, [dragKey]);
    }

    public void SetDragKeys(string zzmiRoot, IReadOnlyCollection<string> dragKeys)
    {
        IReadOnlyList<string> validatedDragKeys = ValidateDragKeys(dragKeys);
        string root = NormalizeRoot(zzmiRoot);
        string targetRoot = Path.Combine(root, "Mods", RuntimeFolderName);
        if (!File.Exists(Path.Combine(targetRoot, "JiggleForge.ini")))
        {
            throw new InvalidOperationException("The global runtime is not installed. Install it before changing the drag key.");
        }

        WriteDragKeys(Path.Combine(targetRoot, "JiggleForge.ini"), validatedDragKeys);
        WriteWheelBridgeDragKeys(
            Path.Combine(targetRoot, "JiggleForge", WheelBridgeConfigName),
            validatedDragKeys);
    }

    public void SetDefaultPhysics(string zzmiRoot, PhysicsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string root = NormalizeRoot(zzmiRoot);
        string runtimeIni = Path.Combine(
            root,
            "Mods",
            RuntimeFolderName,
            "JiggleForge.ini");
        if (!File.Exists(runtimeIni))
        {
            throw new InvalidOperationException(
                "The global runtime is not installed. Install it before changing the default physics settings.");
        }

        WriteDefaultPhysics(runtimeIni, settings);
    }

    private static string ValidateDragKey(string dragKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dragKey);
        string? canonical = SupportedDragKeys.FirstOrDefault(
            candidate => string.Equals(candidate, dragKey.Trim(), StringComparison.OrdinalIgnoreCase));
        return canonical ?? throw new ArgumentException($"Unsupported drag key: {dragKey}", nameof(dragKey));
    }

    private static IReadOnlyList<string> ValidateDragKeys(IEnumerable<string> dragKeys)
    {
        ArgumentNullException.ThrowIfNull(dragKeys);
        List<string> result = [];
        foreach (string dragKey in dragKeys)
        {
            string canonical = ValidateDragKey(dragKey);
            if (!result.Contains(canonical, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(canonical);
            }
        }

        return result.Count > 0
            ? result
            : throw new ArgumentException("At least one drag key must be selected.", nameof(dragKeys));
    }

    private static void WriteDragKeys(string path, IReadOnlyList<string> dragKeys)
    {
        string contents = File.ReadAllText(path);
        string block = BuildDragKeyBlock(dragKeys);
        if (MarkedDragKeyRegex().IsMatch(contents))
        {
            contents = MarkedDragKeyRegex().Replace(contents, block, count: 1);
        }
        else if (LegacyDragKeyRegex().IsMatch(contents))
        {
            contents = LegacyDragKeyRegex().Replace(contents, block + "\r\n", count: 1);
        }
        else
        {
            Match namespaceLine = NamespaceLineRegex().Match(contents);
            int insertion = namespaceLine.Success ? namespaceLine.Index + namespaceLine.Length : 0;
            string prefix = insertion > 0 ? "\r\n" : string.Empty;
            contents = contents.Insert(insertion, prefix + block + "\r\n");
        }

        File.WriteAllText(path, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteWheelBridgeDragKeys(string path, IReadOnlyList<string> dragKeys)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("WheelBridge configuration is missing.", path);
        }

        string contents = File.ReadAllText(path);
        string value = string.Join(", ", dragKeys);
        contents = WheelBridgeDragKeysRegex().Replace(contents, string.Empty).TrimEnd();
        contents += $"\r\n\r\ndrag_key = {dragKeys[0]}\r\ndrag_keys = {value}\r\n";

        File.WriteAllText(path, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static IReadOnlyList<string>? ReadDragKeys(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            string contents = File.ReadAllText(path);
            Match section = MarkedDragKeyRegex().Match(contents);
            if (!section.Success)
            {
                section = LegacyDragKeyRegex().Match(contents);
            }

            Match[] keys = KeyLineRegex().Matches(section.Value).Cast<Match>().ToArray();
            if (keys.Length > 0)
            {
                return ValidateDragKeys(keys.Select(key => key.Groups[1].Value));
            }
        }
        catch (IOException)
        {
        }
        catch (ArgumentException)
        {
        }

        return null;
    }

    private static string BuildDragKeyBlock(IReadOnlyList<string> dragKeys)
    {
        StringBuilder output = new();
        output.AppendLine(DragKeyBeginMarker);
        for (int index = 0; index < dragKeys.Count; index++)
        {
            output.Append("[KeyJiggleForgeDrag").Append(index + 1).AppendLine("]");
            output.Append("key = ").AppendLine(dragKeys[index]);
            output.AppendLine("type = hold");
            output.AppendLine("$mouseDown = 1");
            output.AppendLine("post $mouseDown = 0");
            if (index + 1 < dragKeys.Count)
            {
                output.AppendLine();
            }
        }
        output.AppendLine(DragKeyEndMarker);
        return output.ToString().ReplaceLineEndings("\r\n");
    }

    private static string BuildDefaultPhysicsBlock(PhysicsSettings physics)
    {
        ValidateDefaultPhysics(physics);
        double grabSpring = PhysicsSchemaMigration.GrabSpringForV1(physics.HoldFrequencyHz);
        double releaseSpring = PhysicsSchemaMigration.ReleaseSpringForV1(physics.ReleaseFrequencyHz);
        double releaseKick = PhysicsSchemaMigration.ReleaseKickForV1(physics.ReleaseImpulse);
        double targetFollow = PhysicsSchemaMigration.TargetFollowForV1(physics.TargetFollowSeconds);
        return DefaultPhysicsBeginMarker + "\r\n" +
               $"global $defaultRadius = {Format(physics.Radius)}\r\n" +
               $"global $defaultStrength = {Format(physics.Strength)}\r\n" +
               $"global $defaultFalloff = {Format(physics.Falloff)}\r\n" +
               $"global $defaultVolumeResponse = {Format(physics.VolumeResponse)}\r\n" +
               $"global $defaultDragScale = {Format(physics.DragScale)}\r\n" +
               $"global $defaultGrabDamping = {Format(physics.HoldDampingRatio)}\r\n" +
               $"global $defaultGrabSpring = {Format(grabSpring)}\r\n" +
               $"global $defaultReleaseDamping = {Format(physics.ReleaseDampingRatio)}\r\n" +
               $"global $defaultReleaseSpring = {Format(releaseSpring)}\r\n" +
               $"global $defaultReleaseKick = {Format(releaseKick)}\r\n" +
               $"global $defaultMaxOffset = {Format(physics.MaxOffset)}\r\n" +
               $"global $defaultTargetFollow = {Format(targetFollow)}\r\n" +
               $"global $defaultWheelDepthStep = {Format(physics.WheelDepthStep)}\r\n" +
               $"global $defaultWheelMinDepth = {Format(physics.WheelMinDepth)}\r\n" +
               $"global $defaultWheelMaxDepth = {Format(physics.WheelMaxDepth)}\r\n" +
               DefaultPhysicsEndMarker + "\r\n";
    }

    private static void WriteDefaultPhysics(string path, PhysicsSettings physics)
    {
        string text = File.ReadAllText(path);
        Regex marker = MarkedDefaultPhysicsRegex();
        if (!marker.IsMatch(text))
        {
            throw new InvalidDataException(
                "The installed runtime does not contain the default-physics configuration block. Update the runtime first.");
        }

        text = marker.Replace(text, BuildDefaultPhysicsBlock(physics), count: 1);
        text = ReplaceDefaultPhysicsResource(text, physics, requireResource: true);
        File.WriteAllText(path, text);
    }

    private static void ValidateDefaultPhysics(PhysicsSettings physics)
    {
        JiggleProjectConfig validationConfig = new() { Physics = physics.Clone() };
        IReadOnlyList<string> errors = JiggleConfigValidator.Validate(validationConfig);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        }
    }

    private static string Format(double value) =>
        value.ToString("0.################", CultureInfo.InvariantCulture);

    private static string ReplaceDefaultPhysicsResource(
        string text,
        PhysicsSettings physics,
        bool requireResource)
    {
        Regex resource = DefaultPhysicsResourceRegex();
        if (!resource.IsMatch(text))
        {
            if (requireResource)
            {
                throw new InvalidDataException(
                    "The installed runtime does not contain the default-physics resource. Update the runtime first.");
            }

            return text;
        }

        string data = "1 2 " +
                      $"{Format(physics.Radius)} {Format(physics.Strength)} " +
                      $"{Format(physics.Falloff)} {Format(physics.VolumeResponse)} " +
                      $"{Format(physics.DragScale)} {Format(physics.MaxOffset)} " +
                      $"{Format(physics.TargetFollowSeconds)} {Format(physics.HoldFrequencyHz)} " +
                      $"{Format(physics.HoldDampingRatio)} {Format(physics.ReleaseFrequencyHz)} " +
                      $"{Format(physics.ReleaseDampingRatio)} {Format(physics.ReleaseImpulse)} " +
                      $"{Format(physics.WheelDepthStep)} {Format(physics.WheelMinDepth)} " +
                      $"{Format(physics.WheelMaxDepth)} 1 -1 1";
        return resource.Replace(
            text,
            match => match.Groups["prefix"].Value + data,
            count: 1);
    }

    private static Regex MarkedDragKeyRegex() => new(
        @"(?ms)^[ \t]*; JIGGLEFORGE_DRAG_KEY_BEGIN[ \t]*\r?\n.*?^[ \t]*; JIGGLEFORGE_DRAG_KEY_END[ \t]*(?:\r?\n)?",
        RegexOptions.CultureInvariant);

    private static Regex MarkedDefaultPhysicsRegex() => new(
        @"(?ms)^[ \t]*; JIGGLEFORGE_DEFAULT_PHYSICS_BEGIN[ \t]*\r?\n.*?^[ \t]*; JIGGLEFORGE_DEFAULT_PHYSICS_END[ \t]*(?:\r?\n)?",
        RegexOptions.CultureInvariant);

    private static Regex DefaultPhysicsResourceRegex() => new(
        @"(?ms)(?<prefix>^\[ResourceDefaultParameters\][ \t]*\r?\n(?:(?!^\[).)*?^[ \t]*data[ \t]*=[ \t]*)[^\r\n]*",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static Regex LegacyDragKeyRegex() => new(
        @"(?ms)^\[KeyInputManager\][ \t]*\r?\n.*?(?=^\[|\z)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static Regex NamespaceLineRegex() => new(
        @"(?m)^namespace[ \t]*=.*?(?:\r?\n|$)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static Regex KeyLineRegex() => new(
        @"(?mi)^[ \t]*key[ \t]*=[ \t]*([^\r\n;]+?)\s*$",
        RegexOptions.CultureInvariant);

    private static Regex WheelBridgeDragKeysRegex() => new(
        @"(?mi)^[ \t]*drag_keys?[ \t]*=[^\r\n]*(?:\r?\n|$)",
        RegexOptions.CultureInvariant);

}
