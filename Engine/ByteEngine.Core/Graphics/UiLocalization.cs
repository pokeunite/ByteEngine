using System.Text.Json;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics;

/// <summary>
/// Small per-canvas string table. The authored text/label remains a safe fallback
/// when a language or key is missing, and malformed drafts never crash rendering.
/// </summary>
public static class UiLocalization
{
    private sealed record Cache(string Source,
        Dictionary<string, Dictionary<string, string>> Languages);
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<UiCanvas, Holder> Tables = new();
    private sealed class Holder { public Cache? Value; }

    public static string Translate(GameObject target, string? key, string fallback)
    {
        if (string.IsNullOrWhiteSpace(key) ||
            !UiLayout.TryGetCanvas(target, out UiCanvas? canvas) || canvas == null)
            return fallback;
        return Translate(canvas, key, fallback);
    }

    public static string Translate(UiCanvas canvas, string? key, string fallback)
    {
        if (string.IsNullOrWhiteSpace(key)) return fallback;
        Dictionary<string, Dictionary<string, string>> languages = GetLanguages(canvas);
        string chosen = string.IsNullOrWhiteSpace(canvas.Language) ? "en" : canvas.Language.Trim();
        if (TryFind(languages, chosen, key, out string? value) ||
            TryFind(languages, canvas.FallbackLanguage, key, out value))
            return value!;
        return fallback;
    }

    public static IReadOnlyList<string> AvailableLanguages(UiCanvas canvas) =>
        GetLanguages(canvas).Keys.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();

    private static bool TryFind(Dictionary<string, Dictionary<string, string>> languages,
        string language, string key, out string? value)
    {
        value = null;
        return languages.TryGetValue(language ?? string.Empty, out var entries) &&
            entries.TryGetValue(key, out value);
    }

    private static Dictionary<string, Dictionary<string, string>> GetLanguages(UiCanvas canvas)
    {
        Holder holder = Tables.GetOrCreateValue(canvas);
        string source = canvas.TranslationsJson ?? string.Empty;
        if (holder.Value?.Source == source) return holder.Value.Languages;
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using JsonDocument document = JsonDocument.Parse(source);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
                foreach (JsonProperty language in document.RootElement.EnumerateObject())
                {
                    if (language.Value.ValueKind != JsonValueKind.Object) continue;
                    var entries = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (JsonProperty entry in language.Value.EnumerateObject())
                        if (entry.Value.ValueKind == JsonValueKind.String)
                            entries[entry.Name] = entry.Value.GetString() ?? string.Empty;
                    result[language.Name] = entries;
                }
        }
        catch (JsonException)
        {
            // Keep authored fallback text visible while a table is being edited.
        }
        holder.Value = new Cache(source, result);
        return result;
    }
}
