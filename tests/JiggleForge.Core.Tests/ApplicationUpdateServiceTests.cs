using System.Net;
using System.Security.Cryptography;
using System.Text;
using JiggleForge.Core;

namespace JiggleForge.Core.Tests;

[TestClass]
public sealed class ApplicationUpdateServiceTests
{
    private string? root;

    [TestInitialize]
    public void CreateTemporaryDirectory()
    {
        root = Path.Combine(Path.GetTempPath(), "JiggleForgeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void DeleteTemporaryDirectory()
    {
        if (root is not null && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task CheckForUpdatesFindsNewerReleaseAndRequiredAssets()
    {
        const string json = """
            {
              "tag_name": "v1.2.0",
              "name": "JiggleForge 1.2.0",
              "body": "notes",
              "html_url": "https://github.com/wlytsgd2/JiggleForge/releases/tag/v1.2.0",
              "draft": false,
              "prerelease": false,
              "assets": [
                {"name":"JiggleForge-win-x64-v1.2.0.zip","browser_download_url":"https://github.com/package.zip"},
                {"name":"JiggleForge-win-x64-v1.2.0.zip.sha256","browser_download_url":"https://github.com/package.sha256"}
              ]
            }
            """;
        using HttpClient client = new(new DelegateHandler(_ => JsonResponse(json)));
        ApplicationUpdateService service = new("1.1.9", root!, client);

        ApplicationUpdateCheckResult result = await service.CheckForUpdatesAsync();

        Assert.IsTrue(result.UpdateAvailable);
        Assert.AreEqual("1.2.0", result.LatestRelease.VersionText);
        Assert.AreEqual("JiggleForge-win-x64-v1.2.0.zip", result.LatestRelease.PackageName);
    }

    [TestMethod]
    public async Task DownloadRejectsPackageWhenSha256DoesNotMatch()
    {
        byte[] package = Encoding.UTF8.GetBytes("corrupt package");
        string wrongHash = new('0', 64);
        using HttpClient client = new(new DelegateHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(wrongHash + "  package.zip"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(package),
            };
        }));
        ApplicationUpdateService service = new("1.0.0", root!, client);
        ApplicationReleaseInfo release = CreateRelease("9876.5432.1");

        string updateDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JiggleForge",
            "Updates",
            "v" + release.VersionText);
        try
        {
            await Assert.ThrowsExactlyAsync<InvalidDataException>(
                () => service.DownloadUpdateAsync(release));
        }
        finally
        {
            if (Directory.Exists(updateDirectory))
            {
                Directory.Delete(updateDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task DownloadAcceptsPackageWhenSha256Matches()
    {
        byte[] package = Encoding.UTF8.GetBytes("valid update package");
        string expectedHash = Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant();
        using HttpClient client = new(new DelegateHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(expectedHash + "  package.zip"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(package),
            };
        }));
        ApplicationUpdateService service = new("1.0.0", root!, client);
        ApplicationReleaseInfo release = CreateRelease("9876.5432.2");
        string updateDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JiggleForge",
            "Updates",
            "v" + release.VersionText);
        try
        {
            ApplicationUpdateDownload result = await service.DownloadUpdateAsync(release);

            Assert.AreEqual(expectedHash, result.Sha256);
            CollectionAssert.AreEqual(package, File.ReadAllBytes(result.PackagePath));
        }
        finally
        {
            if (Directory.Exists(updateDirectory))
            {
                Directory.Delete(updateDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void VerifyInstallationReportsMissingAndChangedFiles()
    {
        string validPath = Path.Combine(root!, "valid.bin");
        string changedPath = Path.Combine(root!, "changed.bin");
        File.WriteAllText(validPath, "valid", Encoding.UTF8);
        File.WriteAllText(changedPath, "changed", Encoding.UTF8);
        string validHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(validPath))).ToLowerInvariant();
        string expectedChangedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("original"))).ToLowerInvariant();
        File.WriteAllLines(
            Path.Combine(root!, ApplicationUpdateService.IntegrityManifestFileName),
            [
                $"{validHash} *valid.bin",
                $"{expectedChangedHash} *changed.bin",
                $"{validHash} *missing.bin",
            ]);
        ApplicationUpdateService service = new("1.0.0", root!);

        ApplicationIntegrityResult result = service.VerifyInstallation();

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(3, result.ExpectedFileCount);
        Assert.AreEqual(1, result.VerifiedFileCount);
        Assert.AreEqual(2, result.Errors.Count);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("changed.bin", StringComparison.Ordinal)));
        Assert.IsTrue(result.Errors.Any(error => error.Contains("missing.bin", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void SemanticVersionsAreComparedNumerically()
    {
        Assert.IsTrue(
            ApplicationUpdateService.ParseVersion("v0.1.10") >
            ApplicationUpdateService.ParseVersion("0.1.9"));
        Assert.AreEqual("1.2.3", ApplicationUpdateService.NormalizeVersionText("v1.2.3-beta+5"));
    }

    private static ApplicationReleaseInfo CreateRelease(string version) => new(
        "v" + version,
        version,
        ApplicationUpdateService.ParseVersion(version),
        "Test release",
        string.Empty,
        new Uri("https://github.com/release"),
        "JiggleForge-win-x64-v" + version + ".zip",
        new Uri("https://github.com/package.zip"),
        new Uri("https://github.com/package.sha256"),
        false);

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(handler(request));
    }
}
