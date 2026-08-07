using System.Text.Json;

namespace JiggleForge.Core;

/// <summary>
/// Persists the Mod roots that the user has actually opened and adapted.
/// This deliberately does not inspect or enumerate the ZZMI Mods directory.
/// </summary>
public sealed class ModProjectHistoryService
{
    public const string FileName = "AdaptedMods.json";
    private const int FormatVersion = 1;
    private readonly string historyPath;

    public ModProjectHistoryService(string settingsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsDirectory);
        historyPath = Path.Combine(Path.GetFullPath(settingsDirectory), FileName);
    }

    public IReadOnlyList<string> Load()
    {
        if (!File.Exists(historyPath))
        {
            return [];
        }

        try
        {
            ProjectHistoryFile? history = JsonSerializer.Deserialize<ProjectHistoryFile>(
                File.ReadAllText(historyPath));
            if (history is null || history.FormatVersion != FormatVersion)
            {
                return [];
            }

            return Normalize(history.ModPaths);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    public void Add(string modPath)
    {
        string normalized = NormalizePath(modPath);
        List<string> paths = Load()
            .Where(path => !string.Equals(path, normalized, StringComparison.OrdinalIgnoreCase))
            .ToList();
        paths.Insert(0, normalized);
        Save(paths);
    }

    public void Remove(string modPath)
    {
        string normalized = NormalizePath(modPath);
        IReadOnlyList<string> existing = Load();
        List<string> remaining = existing
            .Where(path => !string.Equals(path, normalized, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (remaining.Count != existing.Count)
        {
            Save(remaining);
        }
    }

    public void Replace(IEnumerable<string> modPaths) => Save(Normalize(modPaths));

    private void Save(IEnumerable<string> modPaths)
    {
        string? directory = Path.GetDirectoryName(historyPath);
        Directory.CreateDirectory(directory!);
        ProjectHistoryFile history = new(FormatVersion, Normalize(modPaths));
        string temporary = historyPath + ".tmp";
        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, historyPath, overwrite: true);
    }

    private static IReadOnlyList<string> Normalize(IEnumerable<string>? paths)
    {
        if (paths is null)
        {
            return [];
        }

        List<string> normalized = [];
        foreach (string path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            string fullPath;
            try
            {
                fullPath = NormalizePath(path);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }

            if (!normalized.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
            {
                normalized.Add(fullPath);
            }
        }
        return normalized;
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path.Trim().Trim('"'))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private sealed record ProjectHistoryFile(int FormatVersion, IReadOnlyList<string> ModPaths);
}
