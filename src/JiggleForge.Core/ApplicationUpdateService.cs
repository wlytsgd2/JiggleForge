using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace JiggleForge.Core;

public sealed class ApplicationUpdateService
{
    public const string IntegrityManifestFileName = "JiggleForge.manifest.sha256";
    public const string DefaultRepositoryOwner = "wlytsgd2";
    public const string DefaultRepositoryName = "JiggleForge";

    private static readonly Regex Sha256Pattern = new(
        "(?<![0-9a-fA-F])[0-9a-fA-F]{64}(?![0-9a-fA-F])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly HttpClient httpClient;
    private readonly Uri latestReleaseUri;

    public ApplicationUpdateService(
        string currentVersion,
        string installationDirectory,
        HttpClient? httpClient = null,
        string repositoryOwner = DefaultRepositoryOwner,
        string repositoryName = DefaultRepositoryName)
    {
        CurrentVersionText = NormalizeVersionText(currentVersion);
        CurrentVersion = ParseVersion(CurrentVersionText);
        InstallationDirectory = Path.GetFullPath(installationDirectory);
        this.httpClient = httpClient ?? CreateHttpClient();
        latestReleaseUri = new Uri(
            $"https://api.github.com/repos/{Uri.EscapeDataString(repositoryOwner)}/{Uri.EscapeDataString(repositoryName)}/releases/latest");
    }

    public string CurrentVersionText { get; }

    public Version CurrentVersion { get; }

    public string InstallationDirectory { get; }

    public async Task<ApplicationUpdateCheckResult> CheckForUpdatesAsync(
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(latestReleaseUri, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        GitHubReleaseDto release = await JsonSerializer.DeserializeAsync<GitHubReleaseDto>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidDataException("GitHub 返回了空的 Release 信息。");

        if (release.Draft || string.IsNullOrWhiteSpace(release.TagName))
        {
            throw new InvalidDataException("GitHub 最新 Release 信息无效。");
        }

        string latestVersionText = NormalizeVersionText(release.TagName);
        Version latestVersion = ParseVersion(latestVersionText);
        string expectedPackageName = $"JiggleForge-win-x64-v{latestVersionText}.zip";
        GitHubAssetDto? package = release.Assets.FirstOrDefault(asset =>
            string.Equals(asset.Name, expectedPackageName, StringComparison.OrdinalIgnoreCase));
        if (package is null)
        {
            throw new InvalidDataException("最新 Release 缺少 Windows x64 更新包。");
        }

        GitHubAssetDto? checksum = release.Assets.FirstOrDefault(asset =>
            string.Equals(asset.Name, package.Name + ".sha256", StringComparison.OrdinalIgnoreCase));
        if (checksum is null)
        {
            throw new InvalidDataException("最新 Release 缺少 SHA-256 校验文件。");
        }

        ApplicationReleaseInfo info = new(
            release.TagName,
            latestVersionText,
            latestVersion,
            string.IsNullOrWhiteSpace(release.Name) ? release.TagName : release.Name,
            release.Body ?? string.Empty,
            ParseHttpsUri(release.HtmlUrl, "Release 页面"),
            package.Name,
            ParseHttpsUri(package.BrowserDownloadUrl, "更新包"),
            ParseHttpsUri(checksum.BrowserDownloadUrl, "校验文件"),
            release.Prerelease);

        return new ApplicationUpdateCheckResult(
            CurrentVersionText,
            CurrentVersion,
            info,
            latestVersion > CurrentVersion);
    }

    public async Task<ApplicationUpdateDownload> DownloadUpdateAsync(
        ApplicationReleaseInfo release,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string updateDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JiggleForge",
            "Updates",
            "v" + release.VersionText);
        Directory.CreateDirectory(updateDirectory);

        string checksumText = await httpClient.GetStringAsync(release.ChecksumUri, cancellationToken)
            .ConfigureAwait(false);
        Match checksumMatch = Sha256Pattern.Match(checksumText);
        if (!checksumMatch.Success)
        {
            throw new InvalidDataException("Release 校验文件中没有有效的 SHA-256 值。");
        }

        string expectedSha256 = checksumMatch.Value.ToLowerInvariant();
        string packagePath = Path.Combine(updateDirectory, Path.GetFileName(release.PackageName));
        string partialPath = packagePath + ".part";
        File.Delete(partialPath);

        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(
                    release.PackageUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            long? totalLength = response.Content.Headers.ContentLength;
            await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await using FileStream destination = new(
                partialPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1024 * 128,
                useAsync: true);
            byte[] buffer = new byte[1024 * 128];
            long copied = 0;
            while (true)
            {
                int read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                copied += read;
                if (totalLength > 0)
                {
                    progress?.Report(Math.Clamp((double)copied / totalLength.Value, 0, 1));
                }
            }
        }
        catch
        {
            File.Delete(partialPath);
            throw;
        }

        string actualSha256 = ComputeSha256(partialPath);
        if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(partialPath);
            throw new InvalidDataException(
                $"更新包 SHA-256 校验失败。期望 {expectedSha256}，实际 {actualSha256}。");
        }

        File.Move(partialPath, packagePath, overwrite: true);
        progress?.Report(1);
        return new ApplicationUpdateDownload(packagePath, expectedSha256, release);
    }

    public ApplicationIntegrityResult VerifyInstallation()
    {
        string manifestPath = Path.Combine(InstallationDirectory, IntegrityManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return new ApplicationIntegrityResult(
                false,
                0,
                0,
                ["当前安装不包含完整性清单。请安装带更新功能的新版本后再校验。"]);
        }

        List<string> errors = [];
        int expectedCount = 0;
        int verifiedCount = 0;
        foreach (string rawLine in File.ReadLines(manifestPath))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            Match match = Regex.Match(
                line,
                "^([0-9a-fA-F]{64})\\s+\\*?(.+)$",
                RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                errors.Add($"完整性清单中存在无效行：{line}");
                continue;
            }

            expectedCount++;
            string relativePath = match.Groups[2].Value.Trim().Replace('/', Path.DirectorySeparatorChar);
            string fullPath;
            try
            {
                fullPath = ResolveContainedPath(InstallationDirectory, relativePath);
            }
            catch (InvalidDataException exception)
            {
                errors.Add(exception.Message);
                continue;
            }

            if (!File.Exists(fullPath))
            {
                errors.Add($"缺少文件：{relativePath}");
                continue;
            }

            string actual = ComputeSha256(fullPath);
            if (!string.Equals(actual, match.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"文件校验失败：{relativePath}");
                continue;
            }

            verifiedCount++;
        }

        if (expectedCount == 0)
        {
            errors.Add("完整性清单没有包含任何文件。");
        }

        return new ApplicationIntegrityResult(true, expectedCount, verifiedCount, errors);
    }

    public static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static Version ParseVersion(string value)
    {
        string normalized = NormalizeVersionText(value);
        string[] parts = normalized.Split('.');
        if (parts.Length is < 1 or > 4 || parts.Any(part => !int.TryParse(part, out _)))
        {
            throw new InvalidDataException($"无法识别版本号：{value}");
        }

        string padded = string.Join('.', parts.Concat(Enumerable.Repeat("0", 4 - parts.Length)));
        return Version.Parse(padded);
    }

    public static string NormalizeVersionText(string value)
    {
        string normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        int suffixIndex = normalized.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0)
        {
            normalized = normalized[..suffixIndex];
        }

        return normalized;
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new() { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("JiggleForge", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private static Uri ParseHttpsUri(string? value, string label)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"{label}地址无效。");
        }

        return uri;
    }

    private static string ResolveContainedPath(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException($"完整性清单包含绝对路径：{relativePath}");
        }

        string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string candidate = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));
        if (!candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"完整性清单包含越界路径：{relativePath}");
        }

        return candidate;
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Body { get; set; }

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        public bool Draft { get; set; }

        public bool Prerelease { get; set; }

        public List<GitHubAssetDto> Assets { get; set; } = [];
    }

    private sealed class GitHubAssetDto
    {
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}

public sealed record ApplicationReleaseInfo(
    string TagName,
    string VersionText,
    Version Version,
    string Name,
    string ReleaseNotes,
    Uri ReleasePageUri,
    string PackageName,
    Uri PackageUri,
    Uri ChecksumUri,
    bool IsPrerelease);

public sealed record ApplicationUpdateCheckResult(
    string CurrentVersionText,
    Version CurrentVersion,
    ApplicationReleaseInfo LatestRelease,
    bool UpdateAvailable);

public sealed record ApplicationUpdateDownload(
    string PackagePath,
    string Sha256,
    ApplicationReleaseInfo Release);

public sealed record ApplicationIntegrityResult(
    bool ManifestFound,
    int ExpectedFileCount,
    int VerifiedFileCount,
    IReadOnlyList<string> Errors)
{
    public bool IsValid => ManifestFound && ExpectedFileCount > 0 && Errors.Count == 0;
}
