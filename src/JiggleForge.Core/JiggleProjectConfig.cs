namespace JiggleForge.Core;

public sealed class JiggleProjectConfig
{
    public const int CurrentSchemaVersion = 2;
    public const string DefaultFileName = "JiggleForge.txt";

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public Guid ProjectId { get; set; } = Guid.NewGuid();

    public int StateNamespace { get; set; }

    public OriginalPartsConfig OriginalParts { get; set; } = new();

    public PhysicsSettings Physics { get; set; } = new();

    public InspectorSettings Inspector { get; set; } = new();

    public List<JiggleDrawConfig> Draws { get; } = [];

    public List<JiggleGroupConfig> Groups { get; } = [];

    public List<JiggleEdgeConfig> Edges { get; } = [];
}

public sealed class PhysicsSettings
{
    public double Radius { get; set; } = 0.25;

    public double Strength { get; set; } = 0.70;

    public double Falloff { get; set; } = 2.20;

    public double VolumeResponse { get; set; } = 2.50;

    public double DragScale { get; set; } = 0.75;

    public double HoldDampingRatio { get; set; } = 0.84;

    public double HoldFrequencyHz { get; set; } = 10.0;

    public double ReleaseDampingRatio { get; set; } = 0.90;

    public double ReleaseFrequencyHz { get; set; } = 2.20;

    public double ReleaseImpulse { get; set; } = 0.12;

    public double MaxOffset { get; set; } = 0.15;

    public double TargetFollowSeconds { get; set; } = 0.02;

    public double WheelDepthStep { get; set; } = 0.02;

    public double WheelMinDepth { get; set; } = -0.15;

    public double WheelMaxDepth { get; set; } = 0.15;

    public PhysicsSettings Clone() => new()
    {
        Radius = Radius,
        Strength = Strength,
        Falloff = Falloff,
        VolumeResponse = VolumeResponse,
        DragScale = DragScale,
        HoldDampingRatio = HoldDampingRatio,
        HoldFrequencyHz = HoldFrequencyHz,
        ReleaseDampingRatio = ReleaseDampingRatio,
        ReleaseFrequencyHz = ReleaseFrequencyHz,
        ReleaseImpulse = ReleaseImpulse,
        MaxOffset = MaxOffset,
        TargetFollowSeconds = TargetFollowSeconds,
        WheelDepthStep = WheelDepthStep,
        WheelMinDepth = WheelMinDepth,
        WheelMaxDepth = WheelMaxDepth,
    };
}

public sealed class InspectorSettings
{
    public bool Enabled { get; set; }
}

public sealed class OriginalPartsConfig
{
    public const string Id = "OriginalParts";

    public const string GroupName = "OriginalParts";

    public bool DeformationEnabled { get; set; }

    public string LegacyGroup { get; set; } = string.Empty;
}

public enum JiggleDrawKind
{
    Numeric,
    Auto,
}

public sealed class JiggleDrawConfig
{
    public required string Id { get; set; }

    public string Alias { get; set; } = string.Empty;

    public bool DeformationEnabled { get; set; } = true;

    public required string SourceFile { get; set; }

    public required string SourceSection { get; set; }

    public int SourceLine { get; set; }

    public string Branch { get; set; } = string.Empty;

    public required string Command { get; set; }

    public JiggleDrawKind Kind { get; set; }

    public long? Count { get; set; }

    public long? FirstIndex { get; set; }

    public long? BaseVertex { get; set; }

    public int StateIndex { get; set; }

    public int ObjectId { get; set; }

    public string Group { get; set; } = string.Empty;

    public string Mask { get; set; } = string.Empty;
}

public sealed class JiggleGroupConfig
{
    public required string Name { get; set; }

    public List<string> Draws { get; } = [];

    public PhysicsSettings? Physics { get; set; }

    public double? GraphX { get; set; }

    public double? GraphY { get; set; }
}

public sealed class JiggleEdgeConfig
{
    public required string From { get; set; }

    public required string To { get; set; }
}
