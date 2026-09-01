using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace JiggleForge.Core;

public sealed partial class ModRuntimeCompiler
{
    public RuntimeApplyResult Apply(string modPath, JiggleProjectConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modPath);
        ArgumentNullException.ThrowIfNull(config);

        string root = Path.GetFullPath(modPath.Trim().Trim('"'));
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Mod folder was not found: {root}");
        }

        JiggleConfigSerializer.EnsureLocalStateIds(config);
        IReadOnlyList<string> validationErrors = JiggleConfigValidator.Validate(config);
        if (validationErrors.Count > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, validationErrors));
        }

        Dictionary<string, byte[]?> originalFiles = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> generatedText = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> deletedPaths = new(StringComparer.OrdinalIgnoreCase);
        RuntimeProjectLayout layout = BuildRuntimeLayout(config);
        if (layout.MaximumGroupId > JiggleProjectConfig.MaximumProjectGroupId)
        {
            throw new InvalidDataException(
                $"This project requires group ID {layout.MaximumGroupId}, but the runtime supports at most " +
                $"{JiggleProjectConfig.MaximumProjectGroupId} project groups.");
        }
        int patchedIniCount = 0;

        foreach (IGrouping<string, JiggleDrawConfig> fileGroup in config.Draws.GroupBy(
                     draw => draw.SourceFile,
                     StringComparer.OrdinalIgnoreCase))
        {
            string iniPath = ResolveInsideRoot(root, fileGroup.Key);
            if (!File.Exists(iniPath))
            {
                throw new FileNotFoundException($"Source INI was not found: {fileGroup.Key}", iniPath);
            }

            string text = File.ReadAllText(iniPath);
            int existingMarkers = MarkerBeginRegex().Matches(text).Count;
            int expectedMarkers = fileGroup.Count();
            if (existingMarkers > 0)
            {
                if (existingMarkers != expectedMarkers)
                {
                    throw new InvalidDataException(
                        $"{fileGroup.Key} contains {existingMarkers} JiggleForge draws, but the configuration expects {expectedMarkers}.");
                }

                string updated = UpdatePatchedIni(
                    text,
                    fileGroup.ToArray(),
                    config);
                generatedText[iniPath] = updated;
                continue;
            }

            string patched = PatchIni(
                text,
                fileGroup.OrderBy(draw => draw.SourceLine).ToArray(),
                config);
            generatedText[iniPath] = patched;
            patchedIniCount++;
        }

        string configPath = Path.Combine(root, JiggleProjectConfig.DefaultFileName);
        generatedText[configPath] = JiggleConfigSerializer.Serialize(config);

        string runtimeRoot = Path.Combine(root, "_JiggleForgeRuntime");
        string runtimeMaskRoot = Path.Combine(runtimeRoot, "Masks");
        string masksIniPath = Path.Combine(runtimeRoot, "Masks.generated.ini");
        string projectIniPath = Path.Combine(runtimeRoot, "Project.generated.ini");
        string inspectorIniPath = Path.Combine(runtimeRoot, "Inspector.generated.ini");
        string inspectorShaderPath = Path.Combine(runtimeRoot, "InspectorText.hlsl");

        if (ModRuntimeRequirements.RequiresMaskRuntime(config))
        {
            generatedText[masksIniPath] = BuildMasksIni(root, runtimeRoot, config);
        }
        else
        {
            deletedPaths.Add(masksIniPath);
        }
        if (Directory.Exists(runtimeMaskRoot))
        {
            foreach (string existingMask in Directory.EnumerateFiles(runtimeMaskRoot, "*", SearchOption.AllDirectories))
            {
                deletedPaths.Add(existingMask);
            }
        }

        if (ModRuntimeRequirements.RequiresProjectRuntime(config))
        {
            generatedText[projectIniPath] = BuildProjectIni(config, layout);
        }
        else
        {
            deletedPaths.Add(projectIniPath);
        }

        if (config.Inspector.Enabled)
        {
            generatedText[inspectorIniPath] = BuildInspectorIni(config, layout.Assignments);
            generatedText[inspectorShaderPath] = LoadInspectorShader();
        }
        else
        {
            deletedPaths.Add(inspectorIniPath);
            deletedPaths.Add(inspectorShaderPath);
        }

        foreach ((string path, string _) in generatedText)
        {
            originalFiles.TryAdd(path, File.Exists(path) ? File.ReadAllBytes(path) : null);
        }
        foreach (string path in deletedPaths)
        {
            originalFiles.TryAdd(path, File.Exists(path) ? File.ReadAllBytes(path) : null);
        }

        ApplyGeneratedFiles(generatedText, deletedPaths, originalFiles);
        DeleteDirectoryIfEmpty(runtimeMaskRoot);
        DeleteDirectoryIfEmpty(runtimeRoot);
        return new RuntimeApplyResult(root, patchedIniCount, config.Draws.Count, masksIniPath);
    }

}

public sealed record RuntimeApplyResult(
    string ModPath,
    int PatchedIniFiles,
    int DrawCount,
    string MasksIniPath);

internal sealed record RuntimeDrawAssignment(
    int PickGroupId,
    IReadOnlyList<int> InfluenceGroupIds);

internal sealed record RuntimeProjectGroup(
    int GroupId,
    string Name,
    PhysicsSettings Physics,
    bool IsImplicit);

internal sealed record RuntimeProjectLayout(
    IReadOnlyDictionary<string, RuntimeDrawAssignment> Assignments,
    IReadOnlyList<RuntimeProjectGroup> Groups,
    int MaximumGroupId);

internal static class ModRuntimeRequirements
{
    public static bool IsOriginalDraw(
        JiggleProjectConfig config,
        JiggleDrawConfig draw) =>
        config.Groups.Any(group =>
            string.Equals(
                group.Name,
                OriginalPartsConfig.GroupName,
                StringComparison.OrdinalIgnoreCase) &&
            group.Draws.Contains(draw.Id, StringComparer.OrdinalIgnoreCase));

    public static bool RequiresDrawRuntime(
        JiggleProjectConfig config,
        JiggleDrawConfig draw,
        bool inspectorEnabled) =>
        inspectorEnabled ||
        !IsOriginalDraw(config, draw) ||
        !draw.DeformationEnabled ||
        !string.IsNullOrWhiteSpace(draw.Mask);

    public static bool RequiresProjectRuntime(JiggleProjectConfig config) =>
        config.Draws.Any(draw => RequiresDrawRuntime(config, draw, config.Inspector.Enabled));

    public static bool RequiresPrivateState(JiggleProjectConfig config) =>
        config.Draws.Any(draw => !IsOriginalDraw(config, draw));

    public static bool RequiresMaskRuntime(JiggleProjectConfig config) =>
        config.Draws.Any(draw =>
            draw.DeformationEnabled &&
            !string.IsNullOrWhiteSpace(draw.Mask));
}
