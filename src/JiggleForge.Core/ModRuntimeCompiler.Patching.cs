using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JiggleForge.Core;

public sealed partial class ModRuntimeCompiler
{
    private static string PatchIni(
        string source,
        IReadOnlyList<JiggleDrawConfig> draws,
        JiggleProjectConfig config)
    {
        Match[] matches = DrawRegex().Matches(source)
            .Where(match => match.Groups["auto"].Success ||
                            long.Parse(match.Groups["count"].Value, CultureInfo.InvariantCulture) > 0)
            .ToArray();
        if (matches.Length != draws.Count)
        {
            throw new InvalidDataException(
                $"The INI currently contains {matches.Length} supported DrawIndexed commands, but the configuration expects {draws.Count}.");
        }

        StringBuilder output = new(source.Length + (draws.Count * 900));
        int cursor = 0;
        for (int index = 0; index < matches.Length; index++)
        {
            Match match = matches[index];
            JiggleDrawConfig draw = draws[index];
            string currentCommand = match.Value.Trim().TrimEnd('\r');
            if (!string.Equals(currentCommand, draw.Command, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"{draw.SourceFile}:{draw.SourceLine} changed after analysis. Expected '{draw.Command}', found '{currentCommand}'.");
            }

            string projectNamespace = ProjectNamespace(config.ProjectId);
            string indent = match.Groups["indent"].Value;

            output.Append(source, cursor, match.Index - cursor);
            output.Append(BuildDrawBlock(
                draw,
                match,
                projectNamespace,
                config,
                indent));
            cursor = match.Index + match.Length;

        }

        output.Append(source, cursor, source.Length - cursor);
        return output.ToString().ReplaceLineEndings("\r\n");
    }

    private static string BuildDrawBlock(
        JiggleDrawConfig draw,
        Match match,
        string projectNamespace,
        JiggleProjectConfig config,
        string indent)
    {
        StringBuilder block = new();
        block.Append(indent).Append("; ").Append(ModProjectService.PatchMarker)
            .Append(" BEGIN ").Append(draw.Id).AppendLine();
        bool requiresRuntime = ModRuntimeRequirements.RequiresDrawRuntime(
            config,
            draw,
            config.Inspector.Enabled);
        if (requiresRuntime)
        {
            block.Append(indent).Append("run = CommandList\\").Append(projectNamespace)
                .Append("\\BeginDraw").Append(DrawOrdinal(draw.Id).ToString("D4", CultureInfo.InvariantCulture)).AppendLine();
        }
        block.Append(match.Value.TrimEnd('\r', '\n')).AppendLine();
        if (requiresRuntime)
        {
            block.Append(indent).Append("run = CommandList\\jiggle_forge\\EndAdaptedDraw").AppendLine();
        }
        block.Append(indent).Append("; ").Append(ModProjectService.PatchMarker).Append(" END");
        return block.ToString();
    }

    private static string UpdatePatchedIni(
        string source,
        IReadOnlyCollection<JiggleDrawConfig> draws,
        JiggleProjectConfig config)
    {
        HashSet<string> expected = draws.Select(draw => draw.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> patchedBlocks = new(StringComparer.OrdinalIgnoreCase);
        string updated = MarkerBlockRegex().Replace(source, match =>
        {
            string drawId = match.Groups["id"].Value;
            if (!expected.Contains(drawId))
            {
                return match.Value;
            }

            JiggleDrawConfig draw = draws.Single(candidate =>
                string.Equals(candidate.Id, drawId, StringComparison.OrdinalIgnoreCase));
            string body = match.Groups["body"].Value;
            Match[] drawCommands = DrawRegex().Matches(body).Cast<Match>().ToArray();
            if (drawCommands.Length != 1)
            {
                throw new InvalidDataException($"Could not find exactly one DrawIndexed command in the runtime marker for {drawId}.");
            }

            patchedBlocks.Add(drawId);
            string projectNamespace = ProjectNamespace(config.ProjectId);
            Match drawCommand = drawCommands[0];
            return BuildDrawBlock(
                draw,
                drawCommand,
                projectNamespace,
                config,
                drawCommand.Groups["indent"].Value);
        });

        foreach (JiggleDrawConfig draw in draws)
        {
            if (!patchedBlocks.Contains(draw.Id))
            {
                throw new InvalidDataException($"Could not find the runtime marker for {draw.Id}.");
            }
        }

        int resourceBlockMatches = StateResourcesBlockRegex().Matches(updated).Count;
        if (resourceBlockMatches == 1)
        {
            return StateResourcesBlockRegex().Replace(updated, string.Empty, count: 1).TrimEnd();
        }
        if (resourceBlockMatches > 1)
        {
            throw new InvalidDataException(
                $"Found more than one generated state resources block: {resourceBlockMatches}.");
        }

        int incompleteTailMatches = StateResourcesTailRegex().Matches(updated).Count;
        if (incompleteTailMatches == 1)
        {
            return StateResourcesTailRegex().Replace(updated, string.Empty, count: 1).TrimEnd();
        }
        if (incompleteTailMatches > 1)
        {
            throw new InvalidDataException(
                $"Found more than one incomplete generated state resource tail: {incompleteTailMatches}.");
        }

        return updated.TrimEnd();
    }

}
