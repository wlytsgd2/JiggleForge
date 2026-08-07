using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace JiggleForge.Core.Tests;

[TestClass]
public sealed class LocalizationResourceTests
{
    [TestMethod]
    public void ChineseAndEnglishResourcesContainExactlyTheSameKeys()
    {
        string root = FindRepositoryRoot();
        HashSet<string> chinese = LoadResourceKeys(Path.Combine(
            root, "app", "JiggleForge", "Strings", "zh-CN", "Resources.resw"));
        HashSet<string> english = LoadResourceKeys(Path.Combine(
            root, "app", "JiggleForge", "Strings", "en-US", "Resources.resw"));

        CollectionAssert.AreEquivalent(chinese.ToArray(), english.ToArray());
    }

    [TestMethod]
    public void EveryStructuredCoreMessageHasBothLanguageResources()
    {
        string root = FindRepositoryRoot();
        HashSet<string> chinese = LoadResourceKeys(Path.Combine(
            root, "app", "JiggleForge", "Strings", "zh-CN", "Resources.resw"));
        HashSet<string> english = LoadResourceKeys(Path.Combine(
            root, "app", "JiggleForge", "Strings", "en-US", "Resources.resw"));
        Regex messageKey = new(
            "UserMessage\\.Of\\(\\s*\\\"(?<key>[^\\\"]+)\\\"",
            RegexOptions.CultureInvariant);

        List<string> missing = [];
        foreach (string source in Directory.EnumerateFiles(
                     Path.Combine(root, "src", "JiggleForge.Core"),
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(source);
            foreach (Match match in messageKey.Matches(text))
            {
                string key = match.Groups["key"].Value;
                if (!chinese.Contains(key) || !english.Contains(key))
                {
                    missing.Add($"{key} ({Path.GetRelativePath(root, source)})");
                }
            }
        }

        Assert.AreEqual(0, missing.Count, string.Join(Environment.NewLine, missing));
    }

    [TestMethod]
    public void EveryLiteralApplicationResourceReferenceExistsInBothLanguages()
    {
        string root = FindRepositoryRoot();
        HashSet<string> chinese = LoadResourceKeys(Path.Combine(
            root, "app", "JiggleForge", "Strings", "zh-CN", "Resources.resw"));
        HashSet<string> english = LoadResourceKeys(Path.Combine(
            root, "app", "JiggleForge", "Strings", "en-US", "Resources.resw"));
        Regex codeKey = new(
            "(?:\\bL|AppLanguageService\\.(?:Get|Format))\\(\\s*\\\"(?<key>[^\\\"]+)\\\"",
            RegexOptions.CultureInvariant);
        Regex xamlKey = new(
            "Localization\\.Key=\\\"(?<key>[^\\\"]+)\\\"",
            RegexOptions.CultureInvariant);

        List<string> missing = [];
        string appRoot = Path.Combine(root, "app", "JiggleForge");
        foreach (string source in Directory.EnumerateFiles(appRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (!IsGeneratedPath(source))
            {
                AddMissingKeys(root, source, codeKey, chinese, english, missing, allowPropertySuffix: false);
            }
        }
        foreach (string source in Directory.EnumerateFiles(appRoot, "*.xaml", SearchOption.AllDirectories))
        {
            if (!IsGeneratedPath(source))
            {
                AddMissingKeys(root, source, xamlKey, chinese, english, missing, allowPropertySuffix: true);
            }
        }

        Assert.AreEqual(0, missing.Count, string.Join(Environment.NewLine, missing));
    }

    private static void AddMissingKeys(
        string root,
        string source,
        Regex keyPattern,
        IReadOnlySet<string> chinese,
        IReadOnlySet<string> english,
        ICollection<string> missing,
        bool allowPropertySuffix)
    {
        string text = File.ReadAllText(source);
        foreach (Match match in keyPattern.Matches(text))
        {
            string key = match.Groups["key"].Value;
            bool hasChinese = chinese.Contains(key) ||
                allowPropertySuffix && chinese.Any(candidate => candidate.StartsWith(key + ".", StringComparison.Ordinal));
            bool hasEnglish = english.Contains(key) ||
                allowPropertySuffix && english.Any(candidate => candidate.StartsWith(key + ".", StringComparison.Ordinal));
            if (!hasChinese || !hasEnglish)
            {
                missing.Add($"{key} ({Path.GetRelativePath(root, source)})");
            }
        }
    }

    private static bool IsGeneratedPath(string path) =>
        path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj");

    private static HashSet<string> LoadResourceKeys(string path)
    {
        XDocument document = XDocument.Load(path);
        string[] keys = document.Root!
            .Elements("data")
            .Select(element => (string?)element.Attribute("name"))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key!)
            .ToArray();
        Assert.AreEqual(keys.Length, keys.Distinct(StringComparer.Ordinal).Count(), $"Duplicate resource key in {path}");
        return keys.ToHashSet(StringComparer.Ordinal);
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
