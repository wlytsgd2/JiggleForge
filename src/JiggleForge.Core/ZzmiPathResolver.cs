namespace JiggleForge.Core;

public sealed record ZzmiPathResolution(
    string RequestedPath,
    string ResolvedPath,
    bool IsValid,
    bool WasCorrected,
    UserMessage Message);

public static class ZzmiPathResolver
{
    private static readonly string[] InstallationMarkers =
    [
        "d3d11.dll",
        "d3dx.ini",
        "d3dx_user.ini",
    ];

    public static ZzmiPathResolution Resolve(string requestedPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            return Invalid(string.Empty, UserMessage.Of("CoreZzmiPathEmpty"));
        }

        string requested;
        try
        {
            requested = Path.GetFullPath(requestedPath.Trim().Trim('"'));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Invalid(requestedPath, UserMessage.Of("CoreZzmiPathInvalid"));
        }

        if (File.Exists(requested))
        {
            requested = Path.GetDirectoryName(requested) ?? requested;
        }

        if (!Directory.Exists(requested))
        {
            return Invalid(requested, UserMessage.Of("CoreSelectedFolderMissing", requested));
        }

        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        foreach (string candidate in EnumerateCandidates(requested))
        {
            string normalized = Path.GetFullPath(candidate);
            if (!visited.Add(normalized) || !IsZzmiRoot(normalized))
            {
                continue;
            }

            bool corrected = !string.Equals(requested, normalized, StringComparison.OrdinalIgnoreCase);
            return new ZzmiPathResolution(
                requested,
                normalized,
                IsValid: true,
                WasCorrected: corrected,
                corrected
                    ? UserMessage.Of("CoreZzmiPathCorrected", normalized)
                    : UserMessage.Of("CoreZzmiPathValid"));
        }

        return Invalid(requested, UserMessage.Of("CoreZzmiRootNotFound"));
    }

    public static bool IsZzmiRoot(string path)
    {
        if (!Directory.Exists(path) ||
            !Directory.Exists(Path.Combine(path, "Mods")) ||
            !Directory.Exists(Path.Combine(path, "ShaderFixes")))
        {
            return false;
        }

        return Directory.Exists(Path.Combine(path, "Core")) ||
               InstallationMarkers.Any(marker => File.Exists(Path.Combine(path, marker)));
    }

    private static IEnumerable<string> EnumerateCandidates(string requested)
    {
        yield return requested;

        string namedChild = Path.Combine(requested, "ZZMI");
        if (Directory.Exists(namedChild))
        {
            yield return namedChild;
        }

        DirectoryInfo? parent = Directory.GetParent(requested);
        while (parent is not null)
        {
            yield return parent.FullName;
            parent = parent.Parent;
        }
    }

    private static ZzmiPathResolution Invalid(string requestedPath, UserMessage message) =>
        new(requestedPath, requestedPath, IsValid: false, WasCorrected: false, message);
}
