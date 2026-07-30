using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace JiggleForge.Core;

public sealed partial class ModRuntimeCompiler
{
    private static void ApplyGeneratedFiles(
        IReadOnlyDictionary<string, string> generatedText,
        IReadOnlyDictionary<string, byte[]> generatedBinary,
        IReadOnlyDictionary<string, byte[]?> originals)
    {
        List<string> committed = [];
        try
        {
            foreach ((string path, string text) in generatedText)
            {
                string? parent = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                string temporary = path + ".jiggleForge_tmp";
                bool isShader = string.Equals(Path.GetExtension(path), ".hlsl", StringComparison.OrdinalIgnoreCase);
                File.WriteAllText(
                    temporary,
                    text,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: !isShader));
                File.Move(temporary, path, overwrite: true);
                committed.Add(path);
            }
            foreach ((string path, byte[] data) in generatedBinary)
            {
                string? parent = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                string temporary = path + ".jiggleForge_tmp";
                File.WriteAllBytes(temporary, data);
                File.Move(temporary, path, overwrite: true);
                committed.Add(path);
            }
        }
        catch
        {
            foreach (string path in committed.AsEnumerable().Reverse())
            {
                byte[]? original = originals[path];
                if (original is null)
                {
                    File.Delete(path);
                }
                else
                {
                    File.WriteAllBytes(path, original);
                }
            }

            throw;
        }
    }

    private static string ResolveInsideRoot(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException($"Path must be relative to the Mod folder: {relativePath}");
        }

        string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string candidate = Path.GetFullPath(Path.Combine(rootFull, relativePath));
        string prefix = rootFull + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Path leaves the Mod folder: {relativePath}");
        }

        return candidate;
    }

    private static int DrawOrdinal(string drawId)
    {
        if (drawId.Length <= 4 || !int.TryParse(drawId[4..], NumberStyles.None, CultureInfo.InvariantCulture, out int ordinal))
        {
            throw new InvalidDataException($"Invalid draw ID: {drawId}");
        }

        return ordinal;
    }

}
