using System.Text;
using System.Text.RegularExpressions;

namespace JiggleForge.Core;

public sealed partial class RuntimeEnvironmentService
{
    private const string BoneWeightBegin = "; JIGGLEFORGE_BONE_WEIGHT_BEGIN";
    private const string BoneWeightEnd = "; JIGGLEFORGE_BONE_WEIGHT_END";
    private static readonly Regex BoneWeightBlock = new(
        @"; JIGGLEFORGE_BONE_WEIGHT_BEGIN\r?\n.*?; JIGGLEFORGE_BONE_WEIGHT_END",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    public bool SupportsBoneWeights => File.Exists(Path.Combine(payloadRoot, "JiggleForge.ini")) &&
        BoneWeightBlock.IsMatch(File.ReadAllText(Path.Combine(payloadRoot, "JiggleForge.ini")));

    public void SetBoneWeightEnabled(string zzmiRoot, bool enabled)
    {
        string path = Path.Combine(NormalizeRoot(zzmiRoot), "Mods", RuntimeFolderName, "JiggleForge.ini");
        WriteBoneWeightEnabled(path, enabled);
    }

    private static bool? ReadBoneWeightEnabled(string path)
    {
        if (!File.Exists(path)) return null;
        Match block = BoneWeightBlock.Match(File.ReadAllText(path));
        if (!block.Success) return null;
        Match value = Regex.Match(block.Value, @"global\s+\$boneWeightEnabled\s*=\s*([01])\b");
        return value.Success ? value.Groups[1].Value == "1" : null;
    }

    private static string BuildBoneWeightBlock(bool enabled) =>
        $"{BoneWeightBegin}\r\nglobal $boneWeightEnabled = {(enabled ? 1 : 0)}\r\ny115 = {(enabled ? 0 : 1)}\r\n{BoneWeightEnd}";

    private static void WriteBoneWeightEnabled(string path, bool enabled)
    {
        string text = File.ReadAllText(path);
        if (!BoneWeightBlock.IsMatch(text))
            throw new InvalidOperationException("The installed runtime does not support the bone-weight option.");
        File.WriteAllText(path, BoneWeightBlock.Replace(text, BuildBoneWeightBlock(enabled), 1), new UTF8Encoding(false));
    }

    private static string NormalizeBoneWeightOption(string text) =>
        BoneWeightBlock.Replace(text, BuildBoneWeightBlock(true), 1);
}
