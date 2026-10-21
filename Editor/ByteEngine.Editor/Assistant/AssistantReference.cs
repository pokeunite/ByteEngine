using System.Reflection;
using System.Text.RegularExpressions;

namespace ByteEngine.Editor.Assistant;

/// <summary>Local retrieval over the exact engine sources bundled at build time. No model call.</summary>
internal static class AssistantReference
{
    private sealed record Chunk(string Source, int Line, string Text);
    private sealed record Document(string Source, string[] Lines);
    private static readonly Lazy<Document[]> Documents = new(ReadDocuments);
    private static readonly Lazy<Chunk[]> Index = new(Build);
    private static Document[] ReadDocuments()
    {
        var assembly = typeof(AssistantReference).Assembly;
        var documents = new List<Document>();
        foreach (string name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("ByteAssistant.Reference.", StringComparison.Ordinal)))
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
            string[] lines = reader.ReadToEnd().Replace("\r", "").Split('\n');
            documents.Add(new(name[24..].Replace('\\', '/'), lines));
        }
        return documents.ToArray();
    }
    private static Chunk[] Build() => Documents.Value.SelectMany(d => Enumerable.Range(0, (d.Lines.Length + 23) / 24)
        .Select(i => new Chunk(d.Source, i * 24 + 1, string.Join('\n', d.Lines.Skip(i * 24).Take(32))))).ToArray();

    internal static object Search(string query, string source = "", int line = 1)
    {
        if (source.Length > 0)
        {
            var matches = Documents.Value.Where(d => d.Source.Equals(source.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1) return new { note = "Use an exact source name returned by reference search." };
            var document = matches[0]; line = Math.Clamp(line, 1, Math.Max(1, document.Lines.Length));
            string text = string.Join('\n', document.Lines.Skip(line - 1).Take(60));
            return new { source = document.Source, line, totalLines = document.Lines.Length, nextLine = line + 60, excerpt = text[..Math.Min(text.Length, 6000)] };
        }
        string[] words = Regex.Matches(query, @"[\w.]+") .Select(m => m.Value).Where(w => w.Length > 2).Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToArray();
        if (words.Length == 0) return new { note = "Search exact component, property, action ID or engine topic. Sources are read-only implementation references, not a code-execution tool." };
        var weights = words.ToDictionary(w => w, w => Math.Log(1 + (double)Index.Value.Length / (1 + Index.Value.Count(c => c.Text.Contains(w, StringComparison.OrdinalIgnoreCase)))));
        var ranked = Index.Value.Select(c => new { c.Source, c.Line, c.Text, score = words.Sum(w => weights[w] * ((c.Source.Contains(w, StringComparison.OrdinalIgnoreCase) ? .5 : 0) + (c.Text.Contains(w, StringComparison.OrdinalIgnoreCase) ? 3 : 0))) })
            .Where(c => c.score > 0).OrderByDescending(c => c.score).Take(3)
            .Select(c => new { source = c.Source, line = c.Line, excerpt = c.Text[..Math.Min(c.Text.Length, 1800)] }).ToArray();
        return new { matches = ranked, note = "Build-matched engine reference. Use native assistant operations, not generated code. Loaded plugin APIs are in byte_catalog." };
    }
}
