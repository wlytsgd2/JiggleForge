namespace JiggleForge.Core;

public sealed record ModLibraryEntry(
    string ModPath,
    string DisplayName,
    ModImportState State,
    int DrawCount,
    IReadOnlyList<string> Messages);

public sealed record ModFolderResolution(
    string RequestedPath,
    string? ResolvedPath,
    IReadOnlyList<string> Candidates,
    bool IsValid,
    bool WasCorrected,
    string Message);

public sealed class ModLibraryService
{
    private const string RuntimeFolderName = "JiggleForgeShaderFix";
    private static readonly System.Text.RegularExpressions.Regex ProjectIniSignalRegex = new(
        @"(?im)^\s*(?:drawindexed\s*=|\[(?:TextureOverride|ShaderOverride)[^\]]*\])",
        System.Text.RegularExpressions.RegexOptions.Compiled |
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    private readonly ModProjectService projectService;

    public ModLibraryService(ModProjectService? projectService = null)
    {
        this.projectService = projectService ?? new ModProjectService();
    }

    public IReadOnlyList<ModLibraryEntry> ScanZzmiRoot(
        string zzmiRoot,
        CancellationToken cancellationToken = default)
    {
        string modsRoot = Path.Combine(Path.GetFullPath(zzmiRoot), "Mods");
        if (!Directory.Exists(modsRoot))
        {
            return [];
        }

        List<string> roots = [];
        foreach (string child in EnumerateDirectoriesSafely(modsRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(Path.GetFileName(child), RuntimeFolderName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            roots.AddRange(FindProjectRoots(child, cancellationToken));
        }

        List<ModLibraryEntry> entries = [];
        foreach (string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ModProjectInspection inspection = projectService.Inspect(root);
            int drawCount = inspection.Configuration?.Draws.Count ?? inspection.DiscoveredDraws.Count;
            entries.Add(new ModLibraryEntry(
                inspection.ModPath,
                Path.GetFileName(inspection.ModPath),
                inspection.State,
                drawCount,
                inspection.Messages));
        }

        return entries
            .OrderBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(entry => entry.ModPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public ModFolderResolution ResolveSelection(string selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return InvalidSelection(string.Empty, "No Mod folder was selected.");
        }

        string requested;
        try
        {
            requested = Path.GetFullPath(selectedPath.Trim().Trim('"'));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return InvalidSelection(selectedPath, $"The selected path is invalid: {exception.Message}");
        }

        if (File.Exists(requested))
        {
            requested = Path.GetDirectoryName(requested) ?? requested;
        }
        if (!Directory.Exists(requested))
        {
            return InvalidSelection(requested, $"The selected folder does not exist: {requested}");
        }

        if (ZzmiPathResolver.IsZzmiRoot(requested))
        {
            return InvalidSelection(
                requested,
                "The selected folder is the ZZMI root, not a single Mod. Choose a Mod from the library.");
        }

        DirectoryInfo? requestedParent = Directory.GetParent(requested);
        if (requestedParent is not null &&
            string.Equals(Path.GetFileName(requested), "Mods", StringComparison.OrdinalIgnoreCase) &&
            ZzmiPathResolver.IsZzmiRoot(requestedParent.FullName))
        {
            IReadOnlyList<string> mods = EnumerateDirectoriesSafely(requested)
                .Where(path => !string.Equals(
                    Path.GetFileName(path),
                    RuntimeFolderName,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return new ModFolderResolution(
                requested,
                null,
                mods,
                IsValid: false,
                WasCorrected: false,
                "The selected folder is the complete Mods library, not a single Mod. Choose one Mod from the library.");
        }

        if (string.Equals(Path.GetFileName(requested), RuntimeFolderName, StringComparison.OrdinalIgnoreCase))
        {
            return InvalidSelection(requested, "JiggleForgeShaderFix is the global runtime and cannot be opened as a Mod project.");
        }

        if (HasProjectRootSignal(requested))
        {
            return ValidSelection(requested, requested, corrected: false);
        }

        IReadOnlyList<string> candidates = FindProjectRoots(requested, CancellationToken.None)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (candidates.Count == 1)
        {
            return ValidSelection(requested, candidates[0], corrected: true);
        }
        if (candidates.Count > 1)
        {
            return new ModFolderResolution(
                requested,
                null,
                candidates,
                IsValid: false,
                WasCorrected: false,
                $"The selected folder contains {candidates.Count} separate Mods. Select one Mod folder instead.");
        }

        return InvalidSelection(requested, "No Mod INI or JiggleForge configuration was found in the selected folder.");
    }

    private static IReadOnlyList<string> FindProjectRoots(
        string branchRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (HasProjectRootSignal(branchRoot))
        {
            return [Path.GetFullPath(branchRoot)];
        }

        List<string> roots = [];
        foreach (string child in EnumerateDirectoriesSafely(branchRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            roots.AddRange(FindProjectRoots(child, cancellationToken));
        }
        return roots;
    }

    private static bool HasProjectRootSignal(string path)
    {
        if (File.Exists(Path.Combine(path, JiggleProjectConfig.DefaultFileName)))
        {
            return true;
        }

        try
        {
            return Directory.EnumerateFiles(path, "*.ini", SearchOption.TopDirectoryOnly)
                .Any(file =>
                {
                    string name = Path.GetFileName(file);
                    if (name.StartsWith("BACKUP", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".bak.ini", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    try
                    {
                        return ProjectIniSignalRegex.IsMatch(File.ReadAllText(file));
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        return false;
                    }
                });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static IEnumerable<string> EnumerateDirectoriesSafely(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path).ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static ModFolderResolution ValidSelection(
        string requested,
        string resolved,
        bool corrected) =>
        new(
            requested,
            resolved,
            [resolved],
            IsValid: true,
            WasCorrected: corrected,
            corrected
                ? $"The selected folder was resolved to the Mod root: {resolved}"
                : "The selected folder is a Mod root.");

    private static ModFolderResolution InvalidSelection(string requested, string message) =>
        new(requested, null, [], IsValid: false, WasCorrected: false, message);
}
