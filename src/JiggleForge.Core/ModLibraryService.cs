namespace JiggleForge.Core;

public sealed record ModLibraryEntry(
    string ModPath,
    string DisplayName,
    ModImportState State,
    int DrawCount,
    IReadOnlyList<UserMessage> Messages);

public sealed record ModFolderResolution(
    string RequestedPath,
    string? ResolvedPath,
    IReadOnlyList<string> Candidates,
    bool IsValid,
    bool WasCorrected,
    UserMessage Message);

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

    /// <summary>
    /// Finds only folders that contain an explicit JiggleForge adaptation
    /// marker. Ordinary Mods are not parsed or returned.
    /// </summary>
    public IReadOnlyList<string> FindAdaptedProjectRoots(
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

            roots.AddRange(FindAdaptedProjectRootsInBranch(child, cancellationToken));
        }

        return roots
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public ModFolderResolution ResolveSelection(string selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return InvalidSelection(string.Empty, UserMessage.Of("CoreModFolderNotSelected"));
        }

        string requested;
        try
        {
            requested = Path.GetFullPath(selectedPath.Trim().Trim('"'));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return InvalidSelection(selectedPath, UserMessage.Of("CoreModPathInvalid"));
        }

        if (File.Exists(requested))
        {
            requested = Path.GetDirectoryName(requested) ?? requested;
        }
        if (!Directory.Exists(requested))
        {
            return InvalidSelection(requested, UserMessage.Of("CoreSelectedFolderMissing", requested));
        }

        if (ZzmiPathResolver.IsZzmiRoot(requested))
        {
            return InvalidSelection(
                requested,
                UserMessage.Of("CoreModSelectionIsZzmiRoot"));
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
                UserMessage.Of("CoreModSelectionIsModsRoot"));
        }

        if (string.Equals(Path.GetFileName(requested), RuntimeFolderName, StringComparison.OrdinalIgnoreCase))
        {
            return InvalidSelection(requested, UserMessage.Of("CoreModSelectionIsRuntime"));
        }

        string requestedName = Path.GetFileName(requested);
        if (string.Equals(requestedName, "_MANAGED_", StringComparison.OrdinalIgnoreCase) ||
            System.Text.RegularExpressions.Regex.IsMatch(
                requestedName,
                @"^group_\d+$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            return InvalidSelection(
                requested,
                UserMessage.Of("CoreModSelectionIsManagerContainer"));
        }

        IReadOnlyList<string> persistentRoots = FindPersistentProjectRoots(requested)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (persistentRoots.Count == 1)
        {
            bool corrected = !string.Equals(
                requested,
                persistentRoots[0],
                StringComparison.OrdinalIgnoreCase);
            return ValidSelection(requested, persistentRoots[0], corrected);
        }
        if (persistentRoots.Count > 1)
        {
            return new ModFolderResolution(
                requested,
                null,
                persistentRoots,
                IsValid: false,
                WasCorrected: false,
                UserMessage.Of("CoreSeveralExistingProjects", persistentRoots.Count));
        }

        if (HasProjectRootSignal(requested))
        {
            return ValidSelection(requested, requested, corrected: false);
        }

        IReadOnlyList<string> candidates = FindProjectRoots(requested, CancellationToken.None)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (candidates.Count > 0)
        {
            // An explicitly selected folder is authoritative. A single Mod may
            // legitimately organize its INI files into several child folders;
            // those folders must not be mistaken for separate projects.
            return ValidSelection(requested, requested, corrected: false);
        }

        return InvalidSelection(requested, UserMessage.Of("CoreNoModConfigurationFound"));
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

    private static IReadOnlyList<string> FindAdaptedProjectRootsInBranch(
        string branchRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (HasActiveAdaptationMarker(branchRoot))
        {
            return [Path.GetFullPath(branchRoot)];
        }

        List<string> roots = [];
        foreach (string child in EnumerateDirectoriesSafely(branchRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            roots.AddRange(FindAdaptedProjectRootsInBranch(child, cancellationToken));
        }
        return roots;
    }

    private static bool HasActiveAdaptationMarker(string path) =>
        File.Exists(Path.Combine(path, JiggleProjectConfig.DefaultFileName)) ||
        Directory.Exists(Path.Combine(path, "_JiggleForgeRuntime")) ||
        File.Exists(Path.Combine(path, "_JiggleForge", "GraphManifest.json"));

    private static IReadOnlyList<string> FindPersistentProjectRoots(string branchRoot)
    {
        if (HasPersistentProjectMarker(branchRoot))
        {
            return [Path.GetFullPath(branchRoot)];
        }

        List<string> roots = [];
        foreach (string child in EnumerateDirectoriesSafely(branchRoot))
        {
            roots.AddRange(FindPersistentProjectRoots(child));
        }
        return roots;
    }

    private static bool HasPersistentProjectMarker(string path) =>
        File.Exists(Path.Combine(path, ModBackupService.BackupFileName)) ||
        File.Exists(Path.Combine(path, JiggleProjectConfig.DefaultFileName));

    private static bool HasProjectRootSignal(string path)
    {
        // Older JiggleForge versions could adapt a wrapper folder whose actual
        // INI files live in several child folders.  The backup is written to
        // that wrapper and is therefore the strongest available root marker.
        // Check it before descending, otherwise one adapted Mod is exposed as
        // several unrelated library entries and none of them can restore the
        // backup stored on the wrapper.
        if (HasPersistentProjectMarker(path))
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
                ? UserMessage.Of("CoreModRootCorrected", resolved)
                : UserMessage.Of("CoreModRootValid"));

    private static ModFolderResolution InvalidSelection(string requested, UserMessage message) =>
        new(requested, null, [], IsValid: false, WasCorrected: false, message);
}
