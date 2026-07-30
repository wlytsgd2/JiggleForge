namespace JiggleForge.Core;

public enum ModImportState
{
    FirstImport,
    Ready,
    RuntimeRepairRequired,
    PatchedConfigurationMissing,
    LegacyMigrationRequired,
    Invalid,
}

public sealed class ModProjectInspection
{
    public required string ModPath { get; init; }

    public required ModImportState State { get; init; }

    public JiggleProjectConfig? Configuration { get; init; }

    public IReadOnlyList<JiggleDrawConfig> DiscoveredDraws { get; init; } = [];

    public IReadOnlyList<string> Messages { get; init; } = [];
}
