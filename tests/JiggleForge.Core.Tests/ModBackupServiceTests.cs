using System.Text;
using JiggleForge.Core;

namespace JiggleForge.Core.Tests;

[TestClass]
public sealed class ModBackupServiceTests
{
    private string? root;

    [TestInitialize]
    public void CreateTemporaryMod()
    {
        root = Path.Combine(Path.GetTempPath(), "JiggleForgeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void DeleteTemporaryMod()
    {
        if (root is not null && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void BackupRestoresOriginalFilesAndRemovesGeneratedRuntime()
    {
        string sourcePath = Path.Combine(root!, "resources", "body.ini");
        byte[] originalSource = [0xEF, 0xBB, 0xBF, (byte)'[', (byte)'B', (byte)']', 0x0D, 0x0A, 0xFF, 0x00, 0x7F];
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllBytes(sourcePath, originalSource);

        string originalRuntimePath = Path.Combine(root!, "_JiggleForgeRuntime", "legacy.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(originalRuntimePath)!);
        byte[] originalRuntime = [0x01, 0x02, 0x03, 0x04];
        File.WriteAllBytes(originalRuntimePath, originalRuntime);
        string originalConfig = "legacy config\r\n";
        File.WriteAllText(Path.Combine(root!, JiggleProjectConfig.DefaultFileName), originalConfig, Encoding.UTF8);
        string schemaBackupPath = Path.Combine(root!, "JiggleForge.txt.schema1.bak");
        File.WriteAllText(schemaBackupPath, "legacy schema backup", Encoding.UTF8);

        JiggleProjectConfig config = CreateConfig("resources/body.ini");
        ModBackupService service = new();
        ModBackupResult result = service.EnsureOriginalBackup(root!, config);

        Assert.IsTrue(result.Created);
        Assert.AreEqual(4, result.FileCount);
        ModBackupInspection inspection = service.Inspect(root!);
        Assert.IsTrue(inspection.IsValid, inspection.Error);
        Assert.IsTrue(File.Exists(result.BackupPath));

        File.WriteAllBytes(sourcePath, Encoding.UTF8.GetBytes("patched source"));
        File.WriteAllText(Path.Combine(root!, JiggleProjectConfig.DefaultFileName), "generated config", Encoding.UTF8);
        File.WriteAllText(schemaBackupPath, "changed backup", Encoding.UTF8);
        string generatedRuntimePath = Path.Combine(root!, "_JiggleForgeRuntime", "generated.ini");
        File.WriteAllText(generatedRuntimePath, "generated runtime", Encoding.UTF8);

        service.Restore(root!);

        CollectionAssert.AreEqual(originalSource, File.ReadAllBytes(sourcePath));
        CollectionAssert.AreEqual(originalRuntime, File.ReadAllBytes(originalRuntimePath));
        Assert.AreEqual(originalConfig, File.ReadAllText(Path.Combine(root!, JiggleProjectConfig.DefaultFileName), Encoding.UTF8));
        Assert.AreEqual("legacy schema backup", File.ReadAllText(schemaBackupPath));
        Assert.IsFalse(File.Exists(generatedRuntimePath));
        Assert.IsTrue(File.Exists(result.BackupPath), "The archive remains for another restore.");
    }

    [TestMethod]
    public void ExistingValidBackupIsReusedInsteadOfOverwritten()
    {
        string sourcePath = Path.Combine(root!, "body.ini");
        File.WriteAllText(sourcePath, "original", Encoding.UTF8);
        JiggleProjectConfig config = CreateConfig("body.ini");
        ModBackupService service = new();

        ModBackupResult first = service.EnsureOriginalBackup(root!, config);
        DateTime firstWrite = File.GetLastWriteTimeUtc(first.BackupPath);
        File.WriteAllText(sourcePath, "changed", Encoding.UTF8);
        ModBackupResult second = service.EnsureOriginalBackup(root!, config);

        Assert.IsFalse(second.Created);
        Assert.AreEqual(first.BackupPath, second.BackupPath);
        Assert.AreEqual(firstWrite, File.GetLastWriteTimeUtc(second.BackupPath));
        service.Restore(root!);
        Assert.AreEqual("original", File.ReadAllText(sourcePath, Encoding.UTF8));
    }

    [TestMethod]
    public void InvalidManifestIsRejected()
    {
        string backupPath = Path.Combine(root!, ModBackupService.BackupFileName);
        using (FileStream stream = File.Create(backupPath))
        using (System.IO.Compression.ZipArchive archive = new(stream, System.IO.Compression.ZipArchiveMode.Create))
        {
            using StreamWriter writer = new(archive.CreateEntry("manifest.json").Open());
            writer.Write("""{"FormatVersion":1,"CreatedAtUtc":"2026-01-01T00:00:00Z","Files":[{"Path":"../outside.ini","Length":0,"Sha256":""}]}""");
        }

        ModBackupInspection inspection = new ModBackupService().Inspect(root!);

        Assert.IsTrue(inspection.Exists);
        Assert.IsFalse(inspection.IsValid);
        StringAssert.Contains(inspection.Error!, "非法");
    }

    private static JiggleProjectConfig CreateConfig(string sourceFile)
    {
        JiggleProjectConfig config = new();
        config.Draws.Add(new JiggleDrawConfig
        {
            Id = "Draw0001",
            SourceFile = sourceFile,
            SourceSection = "CommandListBody",
            Command = "drawindexed = auto",
            Kind = JiggleDrawKind.Auto,
            StateIndex = 1,
            ObjectId = 1,
        });
        return config;
    }
}
