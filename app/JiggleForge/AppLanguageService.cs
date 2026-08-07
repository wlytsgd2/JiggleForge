using System.Diagnostics;
using System.Net;
using JiggleForge.Core;
using Microsoft.Windows.ApplicationModel.Resources;

namespace JiggleForge;

internal static class AppLanguageService
{
    internal const string Chinese = "zh-CN";
    internal const string English = "en-US";

    private static readonly string LanguagePreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JiggleForge",
        "Language.txt");

    private static ResourceManager? resourceManager;
    private static ResourceContext? resourceContext;
    private static ResourceMap? resourceMap;
    private static string currentLanguage = Chinese;

    internal static bool HasSavedLanguage => File.Exists(LanguagePreferencePath);

    internal static string CurrentLanguage => currentLanguage;

    internal static void ApplySavedLanguageOrDefault()
    {
        string language = Chinese;
        try
        {
            if (File.Exists(LanguagePreferencePath))
            {
                language = Normalize(File.ReadAllText(LanguagePreferencePath).Trim());
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            language = Chinese;
        }

        currentLanguage = language;
        resourceContext = null;
    }

    internal static void SaveLanguage(string language)
    {
        string normalized = Normalize(language);
        Directory.CreateDirectory(Path.GetDirectoryName(LanguagePreferencePath)!);
        File.WriteAllText(LanguagePreferencePath, normalized);
        currentLanguage = normalized;
        resourceContext = null;
    }

    internal static string Get(string key) =>
        TryGetResourceValue(key, out string value) ? value : key;

    internal static bool TryGetProperty(string resourceKey, string propertyName, out string value) =>
        TryGetResourceValue($"{resourceKey}/{propertyName}", out value);

    internal static string Format(string key, params object?[] arguments) =>
        string.Format(System.Globalization.CultureInfo.CurrentCulture, Get(key), arguments);

    internal static string Localize(UserMessage message) =>
        Format(message.Key, message.Arguments);

    internal static string LocalizeException(Exception exception) => exception switch
    {
        HttpRequestException request when request.StatusCode.HasValue =>
            Format("ErrorHttpStatus", (int)request.StatusCode.Value),
        HttpRequestException => Get("ErrorNetwork"),
        UnauthorizedAccessException => Get("ErrorAccessDenied"),
        FileNotFoundException missing => Format("ErrorFileMissing", missing.FileName ?? Get("Unknown")),
        DirectoryNotFoundException => Get("ErrorDirectoryMissing"),
        System.Text.Json.JsonException or InvalidDataException => Get("ErrorInvalidData"),
        FormatException or ArgumentException => Get("ErrorInvalidInput"),
        IOException => Get("ErrorFileOperation"),
        InvalidOperationException => Get("ErrorOperationFailed"),
        _ => Get("ErrorUnexpected"),
    };

    internal static void RestartApplication()
    {
        string? executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException(Get("LanguageRestartPathUnavailable"));
        }

        Process.Start(new ProcessStartInfo(executablePath)
        {
            UseShellExecute = true,
            WorkingDirectory = AppContext.BaseDirectory,
        });
        Microsoft.UI.Xaml.Application.Current.Exit();
    }

    private static bool TryGetResourceValue(string key, out string value)
    {
        try
        {
            EnsureResourceContext();
            string? candidateValue = resourceMap!.GetValue(key, resourceContext)?.ValueAsString;
            if (!string.IsNullOrWhiteSpace(candidateValue))
            {
                value = candidateValue;
                return true;
            }
        }
        catch (Exception)
        {
        }

        value = string.Empty;
        return false;
    }

    private static void EnsureResourceContext()
    {
        resourceManager ??= new ResourceManager();
        resourceContext ??= resourceManager.CreateResourceContext();
        resourceContext.QualifierValues["Language"] = currentLanguage;
        resourceMap ??= resourceManager.MainResourceMap.GetSubtree("Resources");
    }

    private static string Normalize(string? language) =>
        string.Equals(language, English, StringComparison.OrdinalIgnoreCase)
            ? English
            : Chinese;
}
