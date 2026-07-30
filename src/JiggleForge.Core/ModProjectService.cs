using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace JiggleForge.Core;

public sealed partial class ModProjectService
{
    public const string PatchMarker = "JIGGLEFORGE_VISIBLE_RANGE";

    public ModProjectInspection Inspect(string modPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modPath);
        string root = Path.GetFullPath(modPath.Trim().Trim('"'));
        if (!Directory.Exists(root))
        {
            return Invalid(root, "The selected Mod folder does not exist.");
        }

        string configPath = Path.Combine(root, JiggleProjectConfig.DefaultFileName);
        string legacyManifest = Path.Combine(root, "_JiggleForge", "GraphManifest.json");
        IniScanResult scan = ScanIniFiles(root);

        if (File.Exists(configPath))
        {
            try
            {
                JiggleProjectConfig config = JiggleConfigSerializer.Load(configPath);
                if (!scan.HasPatchMarker)
                {
                    return new ModProjectInspection
                    {
                        ModPath = root,
                        State = ModImportState.RuntimeRepairRequired,
                        Configuration = config,
                        Messages = ["JiggleForge.txt exists, but the runtime patch marker is missing."],
                    };
                }

                if (scan.MarkerDrawCount != config.Draws.Count)
                {
                    return new ModProjectInspection
                    {
                        ModPath = root,
                        State = ModImportState.RuntimeRepairRequired,
                        Configuration = config,
                        Messages = [$"Configuration contains {config.Draws.Count} draws, but runtime INI files contain {scan.MarkerDrawCount}."],
                    };
                }

                string masksIni = Path.Combine(root, "_JiggleForgeRuntime", "Masks.generated.ini");
                if (!File.Exists(masksIni))
                {
                    return new ModProjectInspection
                    {
                        ModPath = root,
                        State = ModImportState.RuntimeRepairRequired,
                        Configuration = config,
                        Messages = ["Runtime mask bindings are missing and can be regenerated from JiggleForge.txt."],
                    };
                }

                string inspectorIni = Path.Combine(root, "_JiggleForgeRuntime", "Inspector.generated.ini");
                string inspectorShader = Path.Combine(root, "_JiggleForgeRuntime", "InspectorText.hlsl");
                if (!File.Exists(inspectorIni) || !File.Exists(inspectorShader))
                {
                    return new ModProjectInspection
                    {
                        ModPath = root,
                        State = ModImportState.RuntimeRepairRequired,
                        Configuration = config,
                        Messages = ["Draw Inspector runtime files are missing and can be regenerated from JiggleForge.txt."],
                    };
                }

                return new ModProjectInspection
                {
                    ModPath = root,
                    State = ModImportState.Ready,
                    Configuration = config,
                    Messages = ["Existing JiggleForge project is ready."],
                };
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                return Invalid(root, exception.Message);
            }
        }

        if (File.Exists(legacyManifest))
        {
            return new ModProjectInspection
            {
                ModPath = root,
                State = ModImportState.LegacyMigrationRequired,
                DiscoveredDraws = scan.Draws,
                Messages = ["A legacy GraphManifest.json was found. Its Graph, Mask and Draw names can be migrated into JiggleForge.txt."],
            };
        }

        if (scan.HasPatchMarker)
        {
            return new ModProjectInspection
            {
                ModPath = root,
                State = ModImportState.PatchedConfigurationMissing,
                DiscoveredDraws = scan.Draws,
                Messages = ["JiggleForge runtime markers exist, but JiggleForge.txt is missing and must be reconstructed."],
            };
        }

        if (scan.Draws.Count == 0)
        {
            return Invalid(root, "No supported numeric or auto DrawIndexed commands were found.");
        }

        return new ModProjectInspection
        {
            ModPath = root,
            State = ModImportState.FirstImport,
            DiscoveredDraws = scan.Draws,
            Messages = [$"First import: {scan.Draws.Count} supported DrawIndexed commands were found."],
        };
    }

    public JiggleProjectConfig CreateInitialConfiguration(
        ModProjectInspection inspection,
        PhysicsSettings? defaultPhysics = null)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        if (inspection.State != ModImportState.FirstImport)
        {
            throw new InvalidOperationException("An initial configuration can only be created for a first import.");
        }

        Guid projectId = Guid.NewGuid();
        byte[] idBytes = SHA256.HashData(projectId.ToByteArray());
        int stateNamespace = idBytes[0];
        JiggleProjectConfig config = new()
        {
            ProjectId = projectId,
            StateNamespace = stateNamespace,
            Physics = defaultPhysics?.Clone() ?? new PhysicsSettings(),
        };
        config.Inspector.Enabled = true;
        config.Groups.Add(new JiggleGroupConfig
        {
            Name = OriginalPartsConfig.GroupName,
            Physics = config.Physics.Clone(),
        });

        int ordinal = 0;
        foreach (JiggleDrawConfig discovered in inspection.DiscoveredDraws)
        {
            ordinal++;
            int stateIndex = (stateNamespace * 256) + ordinal;
            config.Draws.Add(new JiggleDrawConfig
            {
                Id = $"Draw{ordinal:D4}",
                SourceFile = discovered.SourceFile,
                SourceSection = discovered.SourceSection,
                SourceLine = discovered.SourceLine,
                Branch = discovered.Branch,
                Command = discovered.Command,
                Kind = discovered.Kind,
                Count = discovered.Count,
                FirstIndex = discovered.FirstIndex,
                BaseVertex = discovered.BaseVertex,
                StateIndex = stateIndex,
                ObjectId = stateIndex + 1,
            });
        }

        return config;
    }

    private static IniScanResult ScanIniFiles(string root)
    {
        List<JiggleDrawConfig> draws = [];
        bool hasMarker = false;
        int markerDrawCount = 0;
        foreach (string path in Directory.EnumerateFiles(root, "*.ini", SearchOption.AllDirectories))
        {
            string fileName = Path.GetFileName(path);
            if (fileName.StartsWith("DISABLED", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("BACKUP", StringComparison.OrdinalIgnoreCase) ||
                path.Contains($"{Path.DirectorySeparatorChar}_JiggleForge{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (IOException)
            {
                continue;
            }

            if (text.Contains(PatchMarker, StringComparison.Ordinal))
            {
                hasMarker = true;
                markerDrawCount += MarkerBeginRegex().Matches(text).Count;
                continue;
            }

            string relativePath = Path.GetRelativePath(root, path);
            foreach (Match match in DrawRegex().Matches(text))
            {
                if (!match.Groups["auto"].Success &&
                    long.Parse(match.Groups["count"].Value, System.Globalization.CultureInfo.InvariantCulture) < 1)
                {
                    continue;
                }

                bool auto = match.Groups["auto"].Success;
                draws.Add(new JiggleDrawConfig
                {
                    Id = $"Candidate{draws.Count + 1:D4}",
                    SourceFile = relativePath,
                    SourceSection = GetSectionAt(text, match.Index),
                    SourceLine = CountLines(text, match.Index),
                    Branch = GetBranchAt(text, match.Index),
                    Command = match.Value.Trim(),
                    Kind = auto ? JiggleDrawKind.Auto : JiggleDrawKind.Numeric,
                    Count = auto ? null : long.Parse(match.Groups["count"].Value, System.Globalization.CultureInfo.InvariantCulture),
                    FirstIndex = auto ? null : long.Parse(match.Groups["first"].Value, System.Globalization.CultureInfo.InvariantCulture),
                    BaseVertex = auto ? null : long.Parse(match.Groups["base"].Value, System.Globalization.CultureInfo.InvariantCulture),
                });
            }
        }

        return new IniScanResult(draws, hasMarker, markerDrawCount);
    }

    private static string GetSectionAt(string text, int index)
    {
        MatchCollection matches = SectionRegex().Matches(text[..index]);
        return matches.Count == 0 ? "<global>" : matches[^1].Groups[1].Value.Trim();
    }

    private static int CountLines(string text, int index) => NewlineRegex().Matches(text[..index]).Count + 1;

    private static string GetBranchAt(string text, int index)
    {
        string prefix = text[..index];
        MatchCollection sections = SectionRegex().Matches(prefix);
        int start = sections.Count == 0 ? 0 : sections[^1].Index + sections[^1].Length;
        List<string> stack = [];
        foreach (string sourceLine in NewlineRegex().Split(prefix[start..]))
        {
            string line = sourceLine.Trim();
            Match branch = IfRegex().Match(line);
            if (branch.Success)
            {
                stack.Add($"if {branch.Groups[1].Value.Trim()}");
                continue;
            }

            branch = ElseIfRegex().Match(line);
            if (branch.Success && stack.Count > 0)
            {
                stack[^1] = $"else if {branch.Groups[1].Value.Trim()}";
                continue;
            }

            if (ElseRegex().IsMatch(line) && stack.Count > 0)
            {
                stack[^1] = "else";
                continue;
            }

            if (EndIfRegex().IsMatch(line) && stack.Count > 0)
            {
                stack.RemoveAt(stack.Count - 1);
            }
        }

        return string.Join(" > ", stack);
    }

    private static ModProjectInspection Invalid(string root, string message) => new()
    {
        ModPath = root,
        State = ModImportState.Invalid,
        Messages = [message],
    };

    private sealed record IniScanResult(IReadOnlyList<JiggleDrawConfig> Draws, bool HasPatchMarker, int MarkerDrawCount);

    [GeneratedRegex(@"(?im)^(?<indent>[ \t]*)drawindexed\s*=\s*(?:(?<auto>auto)|(?<count>\d+)\s*,\s*(?<first>\d+)\s*,\s*(?<base>-?\d+))(?<tail>[ \t]*(?:;[^\r\n]*)?)\r?$")]
    private static partial Regex DrawRegex();

    [GeneratedRegex(@"(?m)^\s*\[([^\]]+)\]\s*$")]
    private static partial Regex SectionRegex();

    [GeneratedRegex("\\r\\n|\\n|\\r")]
    private static partial Regex NewlineRegex();

    [GeneratedRegex(@"^(?i:if)\s+(.+?)\s*(?:;.*)?$")]
    private static partial Regex IfRegex();

    [GeneratedRegex(@"^(?i:else\s+if|elif)\s+(.+?)\s*(?:;.*)?$")]
    private static partial Regex ElseIfRegex();

    [GeneratedRegex(@"^(?i:else)\s*(?:;.*)?$")]
    private static partial Regex ElseRegex();

    [GeneratedRegex(@"^(?i:endif)\b")]
    private static partial Regex EndIfRegex();

    [GeneratedRegex(@"(?m)^\s*;\s*JIGGLEFORGE_VISIBLE_RANGE BEGIN Draw\d{4}\b")]
    private static partial Regex MarkerBeginRegex();
}
