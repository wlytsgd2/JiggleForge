using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace JiggleForge.Updater;

internal static class Program
{
    private const string IntegrityManifestFileName = "JiggleForge.manifest.sha256";
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JiggleForge",
        "Updater.log");

    [STAThread]
    private static void Main(string[] args)
    {
        string operation = "update";
        string language = "zh-CN";
        try
        {
            Dictionary<string, string> options = ParseArguments(args);
            options.TryGetValue("operation", out operation);
            operation = string.IsNullOrWhiteSpace(operation) ? "update" : operation;
            options.TryGetValue("language", out language);
            language = string.Equals(language, "en-US", StringComparison.OrdinalIgnoreCase)
                ? "en-US"
                : "zh-CN";

            if (string.Equals(operation, "uninstall", StringComparison.OrdinalIgnoreCase))
            {
                RunUninstall(options, language);
            }
            else
            {
                RunUpdate(options);
            }
        }
        catch (Exception exception)
        {
            bool uninstalling = string.Equals(operation, "uninstall", StringComparison.OrdinalIgnoreCase);
            Log((uninstalling ? "Uninstall" : "Update") + " failed: " + exception);
            string title = uninstalling
                ? language == "en-US" ? "JiggleForge uninstall failed" : "JiggleForge 卸载失败"
                : "JiggleForge 更新失败 / Update failed";
            string message = uninstalling
                ? language == "en-US"
                    ? "JiggleForge could not remove all application files.\r\n\r\n" + exception.Message +
                      "\r\n\r\nLog: " + LogPath
                    : "JiggleForge 无法删除全部应用文件。\r\n\r\n" + exception.Message +
                      "\r\n\r\n日志：" + LogPath
                : "JiggleForge 更新失败，原有程序文件已尽可能恢复。\r\n\r\n" + exception.Message +
                  "\r\n\r\n日志：" + LogPath +
                  "\r\n\r\nJiggleForge update failed. Existing files were restored where possible." +
                  "\r\n\r\nLog: " + LogPath;
            MessageBox.Show(
                message,
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Environment.ExitCode = 1;
        }
    }

    private static void RunUpdate(IReadOnlyDictionary<string, string> options)
    {
        int parentProcessId = int.Parse(Required(options, "parent"));
        string packagePath = Path.GetFullPath(Required(options, "package"));
        string targetDirectory = Path.GetFullPath(Required(options, "target"));
        string executableName = Required(options, "executable");
        string expectedSha256 = Required(options, "sha256");

        Log($"Starting update. Package={packagePath}; Target={targetDirectory}");
        WaitForParent(parentProcessId);
        VerifyPackage(packagePath, expectedSha256);
        ApplyPackage(packagePath, targetDirectory);
        TryDeleteFile(packagePath);
        StartApplication(targetDirectory, executableName);
        Log("Update completed successfully.");
    }

    private static void RunUninstall(IReadOnlyDictionary<string, string> options, string language)
    {
        int parentProcessId = int.Parse(Required(options, "parent"));
        string targetDirectory = Path.GetFullPath(Required(options, "target"))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        List<string> ownedFiles = ValidateInstalledApplication(targetDirectory);

        Log($"Starting application removal. Target={targetDirectory}; Files={ownedFiles.Count}");
        WaitForParent(parentProcessId);
        foreach (string relativePath in ownedFiles.OrderByDescending(path => path.Length))
        {
            DeleteFileWithRetry(ContainedPath(targetDirectory, relativePath));
        }

        DeleteFileWithRetry(Path.Combine(targetDirectory, IntegrityManifestFileName));
        RemoveEmptyDirectories(targetDirectory);
        bool folderRemoved = !Directory.Exists(targetDirectory);
        Log(folderRemoved
            ? "Application removal completed successfully."
            : "Application files were removed; unrelated files kept the target folder in place.");

        string message = language == "en-US"
            ? folderRemoved
                ? "JiggleForge has been uninstalled."
                : "JiggleForge files were removed. The folder was kept because it contains unrelated files."
            : folderRemoved
                ? "JiggleForge 已卸载。"
                : "JiggleForge 文件已删除。由于目录中还有无关文件，文件夹本身被保留。";
        MessageBox.Show(
            message,
            language == "en-US" ? "JiggleForge uninstalled" : "JiggleForge 已卸载",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static List<string> ValidateInstalledApplication(string targetDirectory)
    {
        string manifestPath = Path.Combine(targetDirectory, IntegrityManifestFileName);
        if (!File.Exists(manifestPath) || !File.Exists(Path.Combine(targetDirectory, "JiggleForge.exe")))
        {
            throw new InvalidDataException("The selected folder is not a verifiable JiggleForge installation.");
        }

        List<string> paths = ReadManifestPaths(targetDirectory).ToList();
        if (paths.Count == 0 || !paths.Any(path =>
                string.Equals(path, "JiggleForge.exe", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("The JiggleForge integrity manifest is empty or invalid.");
        }

        return paths;
    }

    private static void DeleteFileWithRetry(string path)
    {
        Exception lastException = null;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                }
                return;
            }
            catch (IOException exception)
            {
                lastException = exception;
            }
            catch (UnauthorizedAccessException exception)
            {
                lastException = exception;
            }

            Thread.Sleep(250);
        }

        throw new IOException("Unable to remove application file: " + path, lastException);
    }

    private static void RemoveEmptyDirectories(string targetDirectory)
    {
        if (!Directory.Exists(targetDirectory))
        {
            return;
        }

        foreach (string directory in Directory.GetDirectories(targetDirectory, "*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length))
        {
            if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0)
            {
                Directory.Delete(directory);
            }
        }

        if (Directory.GetFileSystemEntries(targetDirectory).Length == 0)
        {
            Directory.Delete(targetDirectory);
        }
    }

    private static void ApplyPackage(string packagePath, string targetDirectory)
    {
        string localRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JiggleForge");
        string stagingDirectory = Path.Combine(localRoot, "UpdateStaging", Guid.NewGuid().ToString("N"));
        string backupDirectory = Path.Combine(
            localRoot,
            "UpdateBackups",
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(stagingDirectory);
        Directory.CreateDirectory(backupDirectory);

        List<string> createdFiles = new();
        try
        {
            ExtractSafely(packagePath, stagingDirectory);
            VerifyExtractedPackage(stagingDirectory);
            string[] packageFiles = Directory.GetFiles(stagingDirectory, "*", SearchOption.AllDirectories);
            HashSet<string> packageRelativePaths = new(
                packageFiles.Select(path => RelativePath(stagingDirectory, path)),
                StringComparer.OrdinalIgnoreCase);
            foreach (string obsoleteRelativePath in ReadManifestPaths(targetDirectory)
                         .Where(path => !packageRelativePaths.Contains(path)))
            {
                string obsoletePath = ContainedPath(targetDirectory, obsoleteRelativePath);
                if (!File.Exists(obsoletePath))
                {
                    continue;
                }

                string backupPath = ContainedPath(backupDirectory, obsoleteRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
                File.Copy(obsoletePath, backupPath, true);
                File.Delete(obsoletePath);
            }

            foreach (string sourcePath in packageFiles)
            {
                string relativePath = RelativePath(stagingDirectory, sourcePath);
                string targetPath = ContainedPath(targetDirectory, relativePath);
                string backupPath = ContainedPath(backupDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                if (File.Exists(targetPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
                    File.Copy(targetPath, backupPath, true);
                }
                else
                {
                    createdFiles.Add(targetPath);
                }

                CopyWithRetry(sourcePath, targetPath);
            }

            PruneOldBackups(Path.GetDirectoryName(backupDirectory), backupDirectory);
        }
        catch
        {
            RollBack(targetDirectory, backupDirectory, createdFiles);
            throw;
        }
        finally
        {
            TryDeleteDirectory(stagingDirectory);
        }
    }

    private static void ExtractSafely(string packagePath, string destinationDirectory)
    {
        string normalizedRoot = destinationDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string normalizedEntry = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            string outputPath = Path.GetFullPath(Path.Combine(normalizedRoot, normalizedEntry));
            if (!outputPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("更新包包含越界路径：" + entry.FullName);
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(outputPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            entry.ExtractToFile(outputPath, true);
        }
    }

    private static void VerifyExtractedPackage(string root)
    {
        string manifestPath = Path.Combine(root, IntegrityManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new InvalidDataException("更新包缺少完整性清单。");
        }

        int verified = 0;
        foreach (string rawLine in File.ReadLines(manifestPath))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            Match match = Regex.Match(line, "^([0-9a-fA-F]{64})\\s+\\*?(.+)$");
            if (!match.Success)
            {
                throw new InvalidDataException("完整性清单中存在无效行。");
            }

            string fullPath = ContainedPath(root, match.Groups[2].Value.Trim().Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                throw new InvalidDataException("更新包缺少文件：" + match.Groups[2].Value.Trim());
            }

            string actual = ComputeSha256(fullPath);
            if (!string.Equals(actual, match.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("更新包内文件校验失败：" + match.Groups[2].Value.Trim());
            }

            verified++;
        }

        if (verified == 0 || !File.Exists(Path.Combine(root, "JiggleForge.exe")))
        {
            throw new InvalidDataException("更新包内容不完整。");
        }
    }

    private static IEnumerable<string> ReadManifestPaths(string root)
    {
        string manifestPath = Path.Combine(root, IntegrityManifestFileName);
        if (!File.Exists(manifestPath))
        {
            yield break;
        }

        foreach (string rawLine in File.ReadLines(manifestPath))
        {
            Match match = Regex.Match(rawLine.Trim(), "^[0-9a-fA-F]{64}\\s+\\*?(.+)$");
            if (!match.Success)
            {
                continue;
            }

            string relativePath = match.Groups[1].Value.Trim().Replace('/', Path.DirectorySeparatorChar);
            _ = ContainedPath(root, relativePath);
            yield return relativePath;
        }
    }

    private static void RollBack(string targetDirectory, string backupDirectory, IEnumerable<string> createdFiles)
    {
        Log("Rolling back update.");
        foreach (string createdFile in createdFiles.Reverse())
        {
            try
            {
                File.Delete(createdFile);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        if (!Directory.Exists(backupDirectory))
        {
            return;
        }

        foreach (string backupPath in Directory.GetFiles(backupDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = RelativePath(backupDirectory, backupPath);
            string targetPath = ContainedPath(targetDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
            CopyWithRetry(backupPath, targetPath);
        }
    }

    private static void CopyWithRetry(string sourcePath, string targetPath)
    {
        Exception lastException = null;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                File.Copy(sourcePath, targetPath, true);
                return;
            }
            catch (IOException exception)
            {
                lastException = exception;
            }
            catch (UnauthorizedAccessException exception)
            {
                lastException = exception;
            }

            Thread.Sleep(250);
        }

        throw new IOException("无法替换文件：" + targetPath, lastException);
    }

    private static void VerifyPackage(string packagePath, string expectedSha256)
    {
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("找不到已下载的更新包。", packagePath);
        }

        string actual = ComputeSha256(packagePath);
        if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("更新包 SHA-256 校验失败。");
        }
    }

    private static void WaitForParent(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            if (!process.WaitForExit(60_000))
            {
                throw new TimeoutException("等待 JiggleForge 退出超时。");
            }
        }
        catch (ArgumentException)
        {
        }
    }

    private static void StartApplication(string targetDirectory, string executableName)
    {
        string executablePath = ContainedPath(targetDirectory, executableName);
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("更新后找不到 JiggleForge.exe。", executablePath);
        }

        Process.Start(new ProcessStartInfo(executablePath)
        {
            WorkingDirectory = targetDirectory,
            UseShellExecute = true,
        });
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < args.Length; index += 2)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
            {
                throw new ArgumentException("更新器启动参数无效。");
            }

            values[args[index].Substring(2)] = args[index + 1];
        }

        return values;
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string name)
    {
        if (!values.TryGetValue(name, out string value) || string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("缺少更新器参数：" + name);
        }

        return value;
    }

    private static string ContainedPath(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException("不允许绝对路径：" + relativePath);
        }

        string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string result = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));
        if (!result.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("路径越界：" + relativePath);
        }

        return result;
    }

    private static string RelativePath(string root, string path)
    {
        string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("路径不属于指定目录。");
        }

        return fullPath.Substring(normalizedRoot.Length);
    }

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using SHA256 sha256 = SHA256.Create();
        return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
            string parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(parent) &&
                Directory.Exists(parent) &&
                Directory.GetFileSystemEntries(parent).Length == 0)
            {
                Directory.Delete(parent);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void PruneOldBackups(string backupRoot, string currentBackup)
    {
        if (string.IsNullOrWhiteSpace(backupRoot) || !Directory.Exists(backupRoot))
        {
            return;
        }

        foreach (DirectoryInfo directory in new DirectoryInfo(backupRoot)
                     .GetDirectories()
                     .Where(directory => !string.Equals(
                         directory.FullName,
                         currentBackup,
                         StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(directory => directory.LastWriteTimeUtc))
        {
            TryDeleteDirectory(directory.FullName);
        }
    }

    private static void Log(string message)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
        File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\r\n");
    }
}
