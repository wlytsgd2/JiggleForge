using System.Text.RegularExpressions;

namespace JiggleForge.Core;

public sealed partial class ModRuntimeCompiler
{
    [GeneratedRegex(@"(?im)^(?<indent>[ \t]*)drawindexed\s*=\s*(?:(?<auto>auto)|(?<count>\d+)\s*,\s*(?<first>\d+)\s*,\s*(?<base>-?\d+))(?<tail>[ \t]*(?:;[^\r\n]*)?)\r?$")]
    private static partial Regex DrawRegex();

    [GeneratedRegex(@"(?m)^\s*;\s*JIGGLEFORGE_VISIBLE_RANGE BEGIN Draw\d{4}\b")]

    private static partial Regex MarkerBeginRegex();

    [GeneratedRegex(@"(?ms)^(?<begin>[ \t]*;\s*JIGGLEFORGE_VISIBLE_RANGE BEGIN (?<id>Draw\d{4})[^\r\n]*\r?\n)(?<body>.*?)(?<end>^[ \t]*;\s*JIGGLEFORGE_VISIBLE_RANGE END[^\r\n]*\r?$)")]

    private static partial Regex MarkerBlockRegex();

    [GeneratedRegex(@"(?ms)^;\s*JIGGLEFORGE_VISIBLE_RANGE STATE RESOURCES BEGIN\r?\n.*?^;\s*JIGGLEFORGE_VISIBLE_RANGE STATE RESOURCES END\r?$")]

    private static partial Regex StateResourcesBlockRegex();

    [GeneratedRegex(@"(?ms)^;\s*JIGGLEFORGE_VISIBLE_RANGE STATE RESOURCES BEGIN\r?\n.*\z")]

    private static partial Regex StateResourcesTailRegex();

}
