using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace JiggleForge.Core.Tests;

[TestClass]
public sealed class LocalizationResourceTests
{
    private static readonly string[] UserFacingAttributeNames =
    [
        "Text",
        "Content",
        "Header",
        "PlaceholderText",
        "OnContent",
        "OffContent",
        "ToolTip",
    ];

    [TestMethod]
    public void ChineseAndEnglishResourcesHaveIdenticalKeys()
    {
        string repositoryRoot = FindRepositoryRoot();
        HashSet<string> chineseKeys = LoadResourceKeys(Path.Combine(
            repositoryRoot,
            "app",
            "JiggleForge",
            "Strings",
            "zh-CN",
            "Resources.resw"));
        HashSet<string> englishKeys = LoadResourceKeys(Path.Combine(
            repositoryRoot,
            "app",
            "JiggleForge",
            "Strings",
            "en-US",
            "Resources.resw"));

        CollectionAssert.AreEquivalent(chineseKeys.ToArray(), englishKeys.ToArray());
    }

    [TestMethod]
    public void EveryXamlLocalizationKeyHasAResourceEntry()
    {
        string repositoryRoot = FindRepositoryRoot();
        string xamlPath = Path.Combine(repositoryRoot, "app", "JiggleForge", "MainWindow.xaml");
        string xaml = File.ReadAllText(xamlPath);
        HashSet<string> resourceKeys = LoadResourceKeys(Path.Combine(
            repositoryRoot,
            "app",
            "JiggleForge",
            "Strings",
            "zh-CN",
            "Resources.resw"));

        Assert.IsFalse(xaml.Contains("x:Uid=", StringComparison.Ordinal));
        MatchCollection localizationKeys = Regex.Matches(
            xaml,
            "local:Localization\\.Key=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.IsGreaterThan(0, localizationKeys.Count);

        foreach (Match match in localizationKeys)
        {
            string prefix = match.Groups[1].Value + ".";
            Assert.IsTrue(
                resourceKeys.Any(key => key.StartsWith(prefix, StringComparison.Ordinal)),
                $"No resource property exists for localization key '{match.Groups[1].Value}'.");
        }
    }

    [TestMethod]
    public void ChineseXamlLiteralsAreBoundToLocalizationKeys()
    {
        string repositoryRoot = FindRepositoryRoot();
        XDocument document = XDocument.Load(Path.Combine(
            repositoryRoot,
            "app",
            "JiggleForge",
            "MainWindow.xaml"));

        foreach (XElement element in document.Descendants())
        {
            bool isChineseLanguageChoice = string.Equals(
                element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Tag")?.Value,
                "zh-CN",
                StringComparison.OrdinalIgnoreCase);
            if (isChineseLanguageChoice)
            {
                continue;
            }

            bool hasLocalizationKey = element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Localization.Key");
            foreach (XAttribute attribute in element.Attributes().Where(attribute =>
                         UserFacingAttributeNames.Contains(attribute.Name.LocalName, StringComparer.Ordinal) &&
                         Regex.IsMatch(attribute.Value, "[\\p{IsCJKUnifiedIdeographs}]")))
            {
                Assert.IsTrue(
                    hasLocalizationKey,
                    $"Chinese XAML literal '{attribute.Value}' is missing local:Localization.Key.");
            }
        }
    }

    private static HashSet<string> LoadResourceKeys(string path)
    {
        XDocument document = XDocument.Load(path);
        return document
            .Root!
            .Elements("data")
            .Select(element => (string?)element.Attribute("name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "JiggleForge.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the JiggleForge repository root.");
    }
}
