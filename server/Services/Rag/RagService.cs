using System.Text;
using System.Text.RegularExpressions;
using server.Contracts;

namespace server.Services.Rag;

public sealed partial class RagService(RagDocumentRepository repository)
{
    public RagContextResult BuildContext(string userPrompt, DashboardSnapshotDto snapshot, int topK = 4)
    {
        var queryTerms = Tokenize($"{userPrompt} {snapshot.Title} {string.Join(' ', snapshot.Kpis.Select(k => k.Label))}");
        var scored = repository
            .GetChunks()
            .Select(chunk => new { Chunk = chunk, Score = ScoreChunk(chunk.Text, queryTerms) })
            .Where(entry => entry.Score > 0)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Chunk.DocumentName)
            .Take(topK)
            .Select(entry => entry.Chunk)
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine("DASHBOARD SNAPSHOT:");
        builder.AppendLine($"- Title: {snapshot.Title}");
        builder.AppendLine($"- GeneratedAtIso: {snapshot.GeneratedAtIso}");
        foreach (var kpi in snapshot.Kpis)
        {
            builder.AppendLine($"- KPI {kpi.Label}: {kpi.Value} {kpi.Unit} (trend {kpi.TrendPercent:+0.0;-0.0;0.0}%)");
        }

        builder.AppendLine();
        builder.AppendLine("RETRIEVED DOCUMENT CONTEXT:");
        foreach (var chunk in scored)
        {
            builder.AppendLine($"[{chunk.DocumentName}#{chunk.Index}] {chunk.Text}");
        }

        return new RagContextResult(builder.ToString(), scored);
    }

    private static int ScoreChunk(string text, IReadOnlySet<string> terms)
    {
        if (terms.Count == 0)
        {
            return 0;
        }

        var normalized = text.ToLowerInvariant();
        var score = 0;
        foreach (var term in terms)
        {
            if (normalized.Contains(term, StringComparison.Ordinal))
            {
                score += 1;
            }
        }

        return score;
    }

    private static IReadOnlySet<string> Tokenize(string input)
    {
        var words = WordRegex()
            .Matches(input.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(word => word.Length >= 3)
            .ToHashSet();

        return words;
    }

    [GeneratedRegex("[a-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex WordRegex();
}
