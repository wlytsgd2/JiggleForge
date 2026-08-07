using JiggleForge.Core;

namespace JiggleForge.Core.Tests;

[TestClass]
public sealed class ModProjectHistoryServiceTests
{
    private string? root;

    [TestInitialize]
    public void CreateRoot()
    {
        root = Path.Combine(Path.GetTempPath(), "JiggleForgeHistoryTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void DeleteRoot()
    {
        if (root is not null && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void HistoryStoresUniqueProjectsWithMostRecentlyOpenedFirst()
    {
        ModProjectHistoryService service = new(root!);
        string first = Path.Combine(root!, "First");
        string second = Path.Combine(root!, "Second");

        service.Add(first);
        service.Add(second);
        service.Add(first);

        CollectionAssert.AreEqual(new[] { Path.GetFullPath(first), Path.GetFullPath(second) }, service.Load().ToArray());
    }

    [TestMethod]
    public void HistoryCanRemoveAndPruneProjects()
    {
        ModProjectHistoryService service = new(root!);
        string first = Path.Combine(root!, "First");
        string second = Path.Combine(root!, "Second");
        service.Add(first);
        service.Add(second);

        service.Remove(second);
        CollectionAssert.AreEqual(new[] { Path.GetFullPath(first) }, service.Load().ToArray());

        service.Replace([second]);
        CollectionAssert.AreEqual(new[] { Path.GetFullPath(second) }, service.Load().ToArray());
    }
}
