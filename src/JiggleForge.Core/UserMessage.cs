namespace JiggleForge.Core;

/// <summary>
/// A culture-neutral message emitted by the Core layer. The desktop UI maps
/// the key to its active language resource and formats the arguments.
/// </summary>
public sealed record UserMessage(string Key, params object?[] Arguments)
{
    public static UserMessage Of(string key, params object?[] arguments) =>
        new(key, arguments);

    public override string ToString() => Key;
}
