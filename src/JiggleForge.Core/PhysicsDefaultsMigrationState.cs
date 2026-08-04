namespace JiggleForge.Core;

public sealed class PhysicsDefaultsMigrationState
{
    public const int CurrentVersion = 1;
    public const string FileName = "PhysicsDefaultsMigration.txt";

    private readonly string statePath;

    public PhysicsDefaultsMigrationState(string settingsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsDirectory);
        statePath = Path.Combine(Path.GetFullPath(settingsDirectory), FileName);
    }

    public bool IsRequired => ReadAppliedVersion() < CurrentVersion;

    public int ReadAppliedVersion()
    {
        try
        {
            return File.Exists(statePath) &&
                   int.TryParse(File.ReadAllText(statePath).Trim(), out int version)
                ? Math.Max(version, 0)
                : 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    public void MarkApplied()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        File.WriteAllText(statePath, CurrentVersion.ToString());
    }
}
