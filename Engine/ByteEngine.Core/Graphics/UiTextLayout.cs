using System.Text;
using System.Text.RegularExpressions;

namespace ByteEngine.Core.Graphics;

internal static class UiTextLayout
{
    public static IReadOnlyList<string> WrapLines(string text, float maxWidth, Func<string, float> measure)
    {
        ArgumentNullException.ThrowIfNull(measure);
        var lines = new List<string>();
        foreach (string paragraph in (text ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
        {
            if (maxWidth <= 0f || !float.IsFinite(maxWidth))
            {
                lines.Add(paragraph);
                continue;
            }

            var line = new StringBuilder();
            foreach (Match match in Regex.Matches(paragraph, @"\S+|\s+"))
            {
                string token = match.Value;
                if (string.IsNullOrWhiteSpace(token) && line.Length == 0)
                    continue;

                string candidate = line.ToString() + token;
                if (line.Length > 0 && measure(candidate) > maxWidth)
                {
                    lines.Add(line.ToString().TrimEnd());
                    line.Clear();
                    if (string.IsNullOrWhiteSpace(token)) continue;
                }

                if (measure(token) <= maxWidth || string.IsNullOrWhiteSpace(token))
                {
                    line.Append(token);
                    continue;
                }

                foreach (char character in token)
                {
                    string next = line.ToString() + character;
                    if (line.Length > 0 && measure(next) > maxWidth)
                    {
                        lines.Add(line.ToString().TrimEnd());
                        line.Clear();
                    }
                    line.Append(character);
                }
            }
            lines.Add(line.ToString().TrimEnd());
        }
        return lines;
    }
}
