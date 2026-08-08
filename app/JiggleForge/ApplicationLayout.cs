namespace JiggleForge;

internal static class ApplicationLayout
{
    internal static string ApplicationDirectory { get; } =
        Path.GetFullPath(AppContext.BaseDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    internal static string InstallationDirectory { get; } = ResolveInstallationDirectory();

    internal static string RuntimePayloadDirectory => IsOrganizedRelease
        ? Path.Combine(InstallationDirectory, "Runtime")
        : Path.Combine(ApplicationDirectory, "RuntimePayload");

    internal static string UpdaterPath =>
        Path.Combine(ApplicationDirectory, "JiggleForge.Updater.exe");

    internal static string RestartExecutableRelativePath => IsOrganizedRelease
        ? "JiggleForge.exe"
        : Path.GetFileName(Environment.ProcessPath) ?? "JiggleForge.exe";

    internal static bool IsOrganizedRelease =>
        !string.Equals(ApplicationDirectory, InstallationDirectory, StringComparison.OrdinalIgnoreCase);

    private static string ResolveInstallationDirectory()
    {
        DirectoryInfo applicationDirectory = new(ApplicationDirectory);
        DirectoryInfo? parent = applicationDirectory.Parent;
        if (parent is not null &&
            string.Equals(applicationDirectory.Name, "App", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(Path.Combine(parent.FullName, "JiggleForge.exe")))
        {
            return parent.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return ApplicationDirectory;
    }
}
