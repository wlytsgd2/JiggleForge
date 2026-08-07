using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace JiggleForge.Core;

public sealed record ModBackupInspection(
    string ModPath,
    string BackupPath,
    bool Exists,
    bool IsValid,
    IReadOnlyList<string> Files,
    UserMessage? Error);

public sealed record ModBackupResult(
    string BackupPath,
    bool Created,
    int FileCount);

/// <summary>
/// Creates and restores a byte-exact backup of the files JiggleForge changes.
/// The archive lives in the Mod root and is ignored by 3DMigoto/ZZMI.
/// </summary>
public sealed class ModBackupService
{
    public const string BackupFileName = "JiggleForge.original.zip";
    private const int ManifestVersion = 1;
    private const string ManifestEntryName = "manifest.json";
    private const string RuntimeDirectoryName = "_JiggleForgeRuntime";

    public ModBackupInspection Inspect(string modPath)
    {
        string root = NormalizeRoot(modPath);
        string backupPath = Path.Combine(root, BackupFileName);
        if (!File.Exists(backupPath))
        {
            return new(root, backupPath, false, false, [], null);
        }

        try
        {
            using ZipArchive archive = ZipFile.OpenRead(backupPath);
            BackupManifest manifest = ReadManifest(archive);
            ValidateManifest(manifest);
            List<string> files = manifest.Files
                .Select(entry => entry.Path)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new(root, backupPath, true, true, files, null);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            return new(root, backupPath, true, false, [], UserMessage.Of("CoreBackupInvalid"));
        }
    }

    public ModBackupResult EnsureOriginalBackup(string modPath, JiggleProjectConfig config)
    {
        string root = NormalizeRoot(modPath);
        string backupPath = Path.Combine(root, BackupFileName);
        if (File.Exists(backupPath))
        {
            ModBackupInspection existing = Inspect(root);
            if (!existing.IsValid)
            {
                throw new InvalidDataException(
                    $"备份文件已存在但无效：{backupPath}{Environment.NewLine}{existing.Error}");
            }

            return new(backupPath, false, existing.Files.Count);
        }

        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (JiggleDrawConfig draw in config.Draws)
        {
            string relative = NormalizeRelativePath(draw.SourceFile);
            string fullPath = ResolveInsideRoot(root, relative);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"找不到需要备份的原始 Mod 文件：{relative}", fullPath);
            }

            paths.Add(relative);
        }

        AddIfFileExists(root, paths, JiggleProjectConfig.DefaultFileName);
        foreach (string schemaBackup in Directory.EnumerateFiles(root, "JiggleForge.txt.schema*.bak*"))
        {
            paths.Add(ToArchivePath(root, schemaBackup));
        }

        string runtimeRoot = Path.Combine(root, RuntimeDirectoryName);
        if (Directory.Exists(runtimeRoot))
        {
            foreach (string runtimeFile in Directory.EnumerateFiles(runtimeRoot, "*", SearchOption.AllDirectories))
            {
                paths.Add(ToArchivePath(root, runtimeFile));
            }
        }

        List<BackupFileEntry> entries = [];
        foreach (string relative in paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            string fullPath = ResolveInsideRoot(root, relative);
            byte[] bytes = File.ReadAllBytes(fullPath);
            entries.Add(new(relative, bytes.LongLength, Convert.ToHexString(SHA256.HashData(bytes))));
        }

        BackupManifest manifest = new(
            ManifestVersion,
            DateTimeOffset.UtcNow,
            entries);

        string temporaryPath = $"{backupPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                ZipArchiveEntry manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Fastest);
                using (Stream manifestStream = manifestEntry.Open())
                {
                    JsonSerializer.Serialize(
                        manifestStream,
                        manifest,
                        new JsonSerializerOptions { WriteIndented = true });
                }

                foreach (BackupFileEntry entry in entries)
                {
                    ZipArchiveEntry fileEntry = archive.CreateEntry(entry.Path, CompressionLevel.Optimal);
                    using Stream output = fileEntry.Open();
                    string fullPath = ResolveInsideRoot(root, entry.Path);
                    using FileStream input = File.OpenRead(fullPath);
                    input.CopyTo(output);
                }
            }

            File.Move(temporaryPath, backupPath);
        }
        catch
        {
            TryDeleteFile(temporaryPath);
            throw;
        }

        return new(backupPath, true, entries.Count);
    }

    public void Restore(string modPath)
    {
        string root = NormalizeRoot(modPath);
        string backupPath = Path.Combine(root, BackupFileName);
        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException("没有找到这个 Mod 的原始备份。", backupPath);
        }

        BackupManifest? manifest = null;
        Dictionary<string, byte[]> currentState = [];
        try
        {
            using ZipArchive archive = ZipFile.OpenRead(backupPath);
            manifest = ReadManifest(archive);
            ValidateManifest(manifest);
            foreach (BackupFileEntry entry in manifest.Files)
            {
                ZipArchiveEntry? archiveEntry = archive.GetEntry(entry.Path);
                if (archiveEntry is null)
                {
                    throw new InvalidDataException($"备份缺少文件：{entry.Path}");
                }

                using Stream input = archiveEntry.Open();
                using MemoryStream buffer = new();
                input.CopyTo(buffer);
                byte[] bytes = buffer.ToArray();
                if (bytes.LongLength != entry.Length ||
                    !Convert.ToHexString(SHA256.HashData(bytes)).Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"备份文件校验失败：{entry.Path}");
                }
            }

            foreach (string path in EnumerateAffectedCurrentFiles(root, manifest))
            {
                if (File.Exists(path))
                {
                    currentState[path] = File.ReadAllBytes(path);
                }
            }

            RemoveGeneratedState(root, manifest);
            using ZipArchive restoreArchive = ZipFile.OpenRead(backupPath);
            foreach (BackupFileEntry entry in manifest.Files)
            {
                string target = ResolveInsideRoot(root, entry.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using Stream input = restoreArchive.GetEntry(entry.Path)!.Open();
                using FileStream output = new(target, FileMode.Create, FileAccess.Write, FileShare.None);
                input.CopyTo(output);
            }
        }
        catch
        {
            try
            {
                if (File.Exists(backupPath) && currentState.Count > 0)
                {
                    RemoveGeneratedState(root, manifest!);
                    foreach (KeyValuePair<string, byte[]> item in currentState)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(item.Key)!);
                        File.WriteAllBytes(item.Key, item.Value);
                    }
                }
            }
            catch
            {
                // Preserve the original exception; callers still receive a useful failure.
            }

            throw;
        }
    }

    private static IEnumerable<string> EnumerateAffectedCurrentFiles(string root, BackupManifest manifest)
    {
        foreach (BackupFileEntry entry in manifest.Files)
        {
            yield return ResolveInsideRoot(root, entry.Path);
        }

        string configPath = Path.Combine(root, JiggleProjectConfig.DefaultFileName);
        if (File.Exists(configPath))
        {
            yield return configPath;
        }

        foreach (string schemaBackup in Directory.EnumerateFiles(root, "JiggleForge.txt.schema*.bak*"))
        {
            yield return schemaBackup;
        }

        string runtimeRoot = Path.Combine(root, RuntimeDirectoryName);
        if (Directory.Exists(runtimeRoot))
        {
            foreach (string runtimeFile in Directory.EnumerateFiles(runtimeRoot, "*", SearchOption.AllDirectories))
            {
                yield return runtimeFile;
            }
        }
    }

    private static void RemoveGeneratedState(string root, BackupManifest manifest)
    {
        string configPath = Path.Combine(root, JiggleProjectConfig.DefaultFileName);
        TryDeleteFile(configPath);
        foreach (string schemaBackup in Directory.EnumerateFiles(root, "JiggleForge.txt.schema*.bak*"))
        {
            TryDeleteFile(schemaBackup);
        }

        string runtimeRoot = Path.Combine(root, RuntimeDirectoryName);
        if (Directory.Exists(runtimeRoot))
        {
            Directory.Delete(runtimeRoot, recursive: true);
        }

        // The manifest controls every source/runtime path that may be restored.
        // Remove those paths before restoring to handle files that were created after backup.
        foreach (BackupFileEntry entry in manifest.Files)
        {
            TryDeleteFile(ResolveInsideRoot(root, entry.Path));
        }
    }

    private static BackupManifest ReadManifest(ZipArchive archive)
    {
        ZipArchiveEntry entry = archive.GetEntry(ManifestEntryName)
            ?? throw new InvalidDataException("备份缺少 manifest.json。");
        using Stream stream = entry.Open();
        return JsonSerializer.Deserialize<BackupManifest>(stream)
            ?? throw new InvalidDataException("manifest.json 为空。");
    }

    private static void ValidateManifest(BackupManifest manifest)
    {
        if (manifest.FormatVersion != ManifestVersion)
        {
            throw new InvalidDataException($"不支持的备份版本：{manifest.FormatVersion}。");
        }

        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (BackupFileEntry entry in manifest.Files)
        {
            string normalized = NormalizeRelativePath(entry.Path);
            if (!string.Equals(normalized, entry.Path, StringComparison.Ordinal) ||
                string.Equals(normalized, ManifestEntryName, StringComparison.OrdinalIgnoreCase) ||
                entry.Length < 0 ||
                entry.Sha256.Length != 64 ||
                entry.Sha256.Any(character => !Uri.IsHexDigit(character)) ||
                !paths.Add(normalized))
            {
                throw new InvalidDataException($"备份包含非法或重复路径：{entry.Path}");
            }
        }
    }

    private static void AddIfFileExists(string root, ISet<string> paths, string relative)
    {
        if (File.Exists(ResolveInsideRoot(root, relative)))
        {
            paths.Add(NormalizeRelativePath(relative));
        }
    }

    private static string NormalizeRoot(string modPath)
    {
        if (string.IsNullOrWhiteSpace(modPath))
        {
            throw new ArgumentException("Mod 路径不能为空。", nameof(modPath));
        }

        string root = Path.GetFullPath(modPath);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(root);
        }

        return root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string ResolveInsideRoot(string root, string relative)
    {
        string normalized = NormalizeRelativePath(relative);
        string fullPath = Path.GetFullPath(Path.Combine(root, normalized));
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                        Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"路径超出 Mod 根目录：{relative}");
        }

        return fullPath;
    }

    private static string NormalizeRelativePath(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
        {
            throw new InvalidDataException($"非法的备份路径：{relative}");
        }

        string normalized = relative.Replace('\\', '/').Trim('/');
        if (normalized.Length == 0 ||
            normalized.Split('/').Any(part => part.Length == 0 || part == "." || part == ".."))
        {
            throw new InvalidDataException($"非法的备份路径：{relative}");
        }

        return normalized;
    }

    private static string ToArchivePath(string root, string fullPath) =>
        NormalizeRelativePath(Path.GetRelativePath(root, fullPath));

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // The caller's operation will surface the primary failure if needed.
        }
    }

    private sealed record BackupManifest(
        int FormatVersion,
        DateTimeOffset CreatedAtUtc,
        IReadOnlyList<BackupFileEntry> Files);

    private sealed record BackupFileEntry(string Path, long Length, string Sha256);
}
