using System.Text.RegularExpressions;

namespace JiggleForge.Core;

public sealed partial class RuntimeEnvironmentService
{
    private static readonly Regex WeightSizeBlock = new(
        @"(?m)^[ \t]*; JIGGLEFORGE_WEIGHT_SIZE_BEGIN[ \t]*\r?\n[\s\S]*?^[ \t]*; JIGGLEFORGE_WEIGHT_SIZE_END[ \t]*(?:\r?$)",
        RegexOptions.CultureInvariant);

    // Capabilities belong to the bundled payload, not the application version:
    // the same app can still be built with a normal, non-experimental runtime.
    public bool SupportsWeightSize => File.Exists(Path.Combine(payloadRoot, "JiggleForge.ini")) &&
        WeightSizeBlock.IsMatch(File.ReadAllText(Path.Combine(payloadRoot, "JiggleForge.ini")));

    public PhysicsSettings CreateDefaultPhysics() => new()
    {
        MaxOffset = SupportsWeightSize ? 0.05 : new PhysicsSettings().MaxOffset,
    };
}
