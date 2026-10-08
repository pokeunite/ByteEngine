using System.Globalization;
using System.Text.RegularExpressions;
using StbTrueTypeSharp;

namespace ByteEngine.Core.Graphics;

// GPU atlas shared by all text using the same file and size in one renderer.
internal sealed class FontAtlas : IDisposable
{
    internal readonly record struct Glyph(int X, int Y, int Width, int Height, float OffsetX, float OffsetY, float Advance);

    private readonly Dictionary<int, Glyph> _glyphs = new();
    private readonly Dictionary<(int First, int Second), int> _kernings = new();

    public Texture2D Texture { get; }
    public int Width => Texture.Width;
    public int Height => Texture.Height;
    public float LineHeight { get; }
    public float BaseLine { get; }

    private FontAtlas(Texture2D texture, float lineHeight, float baseLine)
    {
        Texture = texture;
        LineHeight = lineHeight;
        BaseLine = baseLine;
    }

    public bool TryGetGlyph(int codepoint, out Glyph glyph) => _glyphs.TryGetValue(codepoint, out glyph);
    public float GetKerning(int first, int second) => _kernings.GetValueOrDefault((first, second));
    public void Dispose() => Texture.Dispose();

    public static FontAtlas Load(string path, int size)
    {
        if (path.EndsWith(".fnt", StringComparison.OrdinalIgnoreCase))
            return LoadBitmap(path);
        return LoadTrueType(path, size);
    }

    private static FontAtlas LoadTrueType(string path, int size)
    {
        byte[] font = File.ReadAllBytes(path);
        int atlasSize = OperatingSystem.IsBrowser()&&size<=48?1024:2048;
        byte[] coverage = new byte[atlasSize * atlasSize];
        var chars = new StbTrueType.stbtt_bakedchar[224];
        if (!StbTrueType.stbtt_BakeFontBitmap(font, 0, size, coverage, atlasSize, atlasSize, 32, chars.Length, chars))
            throw new InvalidDataException($"Font glyphs do not fit in the {atlasSize}px atlas: {path}");

        byte[] rgba = new byte[coverage.Length * 4];
        for (int i = 0; i < coverage.Length; i++)
        {
            rgba[i * 4] = 255;
            rgba[i * 4 + 1] = 255;
            rgba[i * 4 + 2] = 255;
            rgba[i * 4 + 3] = coverage[i];
        }

        var atlas = new FontAtlas(Texture2D.FromPixels(atlasSize, atlasSize, rgba), size * 1.25f, size);
        for (int i = 0; i < chars.Length; i++)
        {
            var ch = chars[i];
            atlas._glyphs[i + 32] = new Glyph(
                ch.x0, ch.y0, ch.x1 - ch.x0, ch.y1 - ch.y0,
                ch.xoff, ch.yoff, ch.xadvance);
        }
        return atlas;
    }

    private static FontAtlas LoadBitmap(string path)
    {
        string source = File.ReadAllText(path);
        Match page = Regex.Match(source, @"(?m)^page\s+id=0\s+file=""([^""]+)""");
        if (!page.Success)
            throw new InvalidDataException("Only single-page BMFont text atlases are supported.");
        string texturePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, page.Groups[1].Value));
        if (!File.Exists(texturePath))
            throw new FileNotFoundException("BMFont atlas image is missing.", texturePath);

        Match common = Regex.Match(source, @"(?m)^common\s+[^\r\n]*");
        float lineHeight = ReadInt(common.Value, "lineHeight", 32);
        float baseline = ReadInt(common.Value, "base", (int)lineHeight);
        var atlas = new FontAtlas(new Texture2D(texturePath, TextureFilter.Nearest), lineHeight, baseline);
        foreach (Match row in Regex.Matches(source, @"(?m)^char\s+[^\r\n]*"))
        {
            string line = row.Value;
            int id = ReadInt(line, "id", -1);
            if (id < 0) continue;
            atlas._glyphs[id] = new Glyph(
                ReadInt(line, "x", 0),
                ReadInt(line, "y", 0),
                ReadInt(line, "width", 0),
                ReadInt(line, "height", 0),
                ReadInt(line, "xoffset", 0),
                ReadInt(line, "yoffset", 0) - baseline,
                ReadInt(line, "xadvance", 0));
        }
        foreach (Match row in Regex.Matches(source, @"(?m)^kerning\s+[^\r\n]*"))
        {
            string line = row.Value;
            atlas._kernings[(ReadInt(line, "first", 0), ReadInt(line, "second", 0))] = ReadInt(line, "amount", 0);
        }
        return atlas;
    }

    private static int ReadInt(string line, string key, int fallback)
    {
        Match value = Regex.Match(line, @"(?:^|\s)" + Regex.Escape(key) + @"=(-?\d+)(?:\s|$)");
        return value.Success && int.TryParse(value.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed : fallback;
    }
}
