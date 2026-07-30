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

        IReadOnlyList<string> validationErrors = JiggleConfigValidator.Validate(config);
        if (validationErrors.Count > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, validationErrors));
        }

        Dictionary<string, byte[]?> originalFiles = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> generatedText = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, byte[]> generatedBinary = new(StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, RuntimeDrawAssignment> assignments = BuildAssignments(config);
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
                    assignments,
                    config.StateNamespace,
                    !config.OriginalParts.DeformationEnabled);
                generatedText[iniPath] = updated;
                continue;
            }

            string patched = PatchIni(
                text,
                fileGroup.OrderBy(draw => draw.SourceLine).ToArray(),
                config.StateNamespace,
                assignments,
                !config.OriginalParts.DeformationEnabled);
            generatedText[iniPath] = patched;
            patchedIniCount++;
        }

        string configPath = Path.Combine(root, JiggleProjectConfig.DefaultFileName);
        generatedText[configPath] = JiggleConfigSerializer.Serialize(config);

        string runtimeRoot = Path.Combine(root, "_JiggleForgeRuntime");
        string runtimeMaskRoot = Path.Combine(runtimeRoot, "Masks");
        string masksIniPath = Path.Combine(runtimeRoot, "Masks.generated.ini");
        generatedText[masksIniPath] = BuildMasksIni(root, runtimeMaskRoot, config, generatedBinary);
        string inspectorIniPath = Path.Combine(runtimeRoot, "Inspector.generated.ini");
        string inspectorShaderPath = Path.Combine(runtimeRoot, "InspectorText.hlsl");
        generatedText[inspectorIniPath] = BuildInspectorIni(config, assignments);
        generatedText[inspectorShaderPath] = LoadInspectorShader();

        foreach ((string path, string _) in generatedText)
        {
            originalFiles.TryAdd(path, File.Exists(path) ? File.ReadAllBytes(path) : null);
        }
        foreach ((string path, byte[] _) in generatedBinary)
        {
            originalFiles.TryAdd(path, File.Exists(path) ? File.ReadAllBytes(path) : null);
        }

        ApplyGeneratedFiles(generatedText, generatedBinary, originalFiles);
        return new RuntimeApplyResult(root, patchedIniCount, config.Draws.Count, masksIniPath);
    }

}

public sealed record RuntimeApplyResult(
    string ModPath,
    int PatchedIniFiles,
    int DrawCount,
    string MasksIniPath);

internal sealed record RuntimeDrawAssignment(
    int ObjectId,
    IReadOnlyList<int> StateIndices,
    IReadOnlyList<PhysicsSettings> StatePhysics);

internal sealed record RuntimeGroupLeader(int ObjectId, int StateIndex);

internal sealed record RuntimePhysicsBinding(
    string StateResourceName,
    string PhysicsResourceName,
    int StateIndex,
    PhysicsSettings Physics);
