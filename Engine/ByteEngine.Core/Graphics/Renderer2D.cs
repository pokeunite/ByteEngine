using OpenTK.Graphics.OpenGL4;

using Matrix4 = OpenTK.Mathematics.Matrix4;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace ByteEngine.Core.Graphics;

public sealed class Renderer2D : IDisposable
{
    private int _vertexArray;

    private int _vertexBuffer;

    private Shader? _shader;

    private bool _initialized;

    private readonly Dictionary<(string Path, int Size), FontAtlas> _fonts = new();

    public static string? FontProjectRoot { get; set; }

    private int _viewportWidth;

    private int _viewportHeight;

    internal void Initialize(
        int width,
        int height)
    {
        if (_initialized)
        {
            return;
        }

        float[] vertices =
        {
            // Position       UV

            -0.5f, -0.5f,    0.0f, 0.0f,
             0.5f, -0.5f,    1.0f, 0.0f,
             0.5f,  0.5f,    1.0f, 1.0f,

             0.5f,  0.5f,    1.0f, 1.0f,
            -0.5f,  0.5f,    0.0f, 1.0f,
            -0.5f, -0.5f,    0.0f, 0.0f
        };

        _vertexArray =
            GL.GenVertexArray();

        _vertexBuffer =
            GL.GenBuffer();

        GL.BindVertexArray(
            _vertexArray
        );

        GL.BindBuffer(
            BufferTarget.ArrayBuffer,
            _vertexBuffer
        );

        GL.BufferData(
            BufferTarget.ArrayBuffer,
            vertices.Length * sizeof(float),
            vertices,
            BufferUsageHint.StaticDraw
        );

        int stride =
            4 * sizeof(float);

        GL.EnableVertexAttribArray(
            0
        );

        GL.VertexAttribPointer(
            0,
            2,
            VertexAttribPointerType.Float,
            false,
            stride,
            0
        );

        GL.EnableVertexAttribArray(
            1
        );

        GL.VertexAttribPointer(
            1,
            2,
            VertexAttribPointerType.Float,
            false,
            stride,
            2 * sizeof(float)
        );

        GL.BindBuffer(
            BufferTarget.ArrayBuffer,
            0
        );

        GL.BindVertexArray(
            0
        );

        _shader =
            new Shader(
                VertexShaderSource,
                FragmentShaderSource
            );

        GL.Enable(
            EnableCap.Blend
        );

        GL.BlendFunc(
            BlendingFactor.SrcAlpha,
            BlendingFactor.OneMinusSrcAlpha
        );

        GL.Disable(
            EnableCap.DepthTest
        );

        _initialized =
            true;

        Resize(
            width,
            height
        );

        ResetCamera();
        _shader.SetVector2("uUvMin", Vector2.Zero);
        _shader.SetVector2("uUvMax", Vector2.One);

        Console.WriteLine(
            "Renderer2D initialized successfully."
        );
    }

    internal void Resize(
        int width,
        int height)
    {
        if (!_initialized ||
            _shader == null)
        {
            return;
        }

        _viewportWidth =
            Math.Max(
                1,
                width
            );

        _viewportHeight =
            Math.Max(
                1,
                height
            );

        Matrix4 projection =
            Matrix4.CreateOrthographicOffCenter(
                0.0f,
                _viewportWidth,
                _viewportHeight,
                0.0f,
                -1.0f,
                1.0f
            );

        _shader.Use();

        _shader.SetMatrix4(
            "uProjection",
            projection
        );

        _shader.SetVector2(
            "uViewportSize",
            new Vector2(
                _viewportWidth,
                _viewportHeight
            )
        );
    }

    internal void SetCamera(
        Vector2 position,
        float zoom)
    {
        if (_shader == null)
        {
            return;
        }

        zoom =
            Math.Max(
                0.01f,
                zoom
            );

        _shader.Use();

        _shader.SetVector2(
            "uCameraPosition",
            position
        );

        _shader.SetFloat(
            "uCameraZoom",
            zoom
        );
    }

    internal void ResetCamera()
    {
        SetCamera(
            new Vector2(
                _viewportWidth / 2.0f,
                _viewportHeight / 2.0f
            ),
            1.0f
        );
    }

    internal void BeginUi()
    {
        ResetCamera();GL.Enable(EnableCap.Blend);GL.BlendFunc(BlendingFactor.SrcAlpha,BlendingFactor.OneMinusSrcAlpha);GL.Disable(EnableCap.DepthTest);GL.Disable(EnableCap.CullFace);
    }

    internal void SetUiClip(Vector4? rectangle)
    {
        if(rectangle is not {} r){GL.Disable(EnableCap.ScissorTest);return;}
        GL.Enable(EnableCap.ScissorTest);
        int x=(int)MathF.Floor(r.X), y=(int)MathF.Floor(r.Y);
        int width=Math.Max(0,(int)MathF.Ceiling(r.X+r.Z)-x),height=Math.Max(0,(int)MathF.Ceiling(r.Y+r.W)-y);
        GL.Scissor(x,_viewportHeight-y-height,width,height);
    }
    public Vector2 MeasureText(string text,string? fontPath,int fontSize)
    {
        string? path=ResolveFontPath(fontPath);if(path==null)return Vector2.Zero;
        int size=Math.Clamp(fontSize,8,96);var key=(path,size);
        if(!_fonts.TryGetValue(key,out var atlas)){atlas=FontAtlas.Load(path,size);_fonts.Add(key,atlas);}
        return atlas.Measure(text,path.EndsWith(".fnt",StringComparison.OrdinalIgnoreCase)?size/Math.Max(1,atlas.LineHeight):1);
    }

    public void DrawText(string value, string? fontPath, int fontSize, Vector2 anchorPoint,
        Vector4 color, float maxWidth = 0f, UiAnchor anchor = UiAnchor.TopLeft)
    {
        if (string.IsNullOrEmpty(value)) return;
        string? resolved = ResolveFontPath(fontPath);
        if (resolved == null) return;
        int size = Math.Clamp(fontSize, 8, 96);
        var key = (resolved, size);
        if (!_fonts.TryGetValue(key, out FontAtlas? atlas))
        {
            try { atlas = FontAtlas.Load(resolved, size); _fonts.Add(key, atlas); }
            catch (Exception error) { Console.Error.WriteLine($"Text font load failed: {error.Message}"); return; }
        }

        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Disable(EnableCap.CullFace);

        float scale = resolved.EndsWith(".fnt", StringComparison.OrdinalIgnoreCase)
            ? size / Math.Max(1f, atlas.LineHeight) : 1f;
        float Measure(string line)
        {
            float width = 0f;
            int previous = -1;
            foreach (char c in line)
            {
                int codepoint = c;
                if (!atlas.TryGetGlyph(codepoint, out FontAtlas.Glyph glyph) &&
                    !atlas.TryGetGlyph('?', out glyph)) continue;
                width += (glyph.Advance + (previous < 0 ? 0 : atlas.GetKerning(previous, codepoint))) * scale;
                previous = codepoint;
            }
            return width;
        }

        IReadOnlyList<string> lines = UiTextLayout.WrapLines(value, maxWidth, Measure);
        float horizontal = anchor is UiAnchor.TopCenter or UiAnchor.Center or UiAnchor.BottomCenter
            ? .5f : anchor is UiAnchor.TopRight or UiAnchor.BottomRight ? 1f : 0f;
        float vertical = anchor == UiAnchor.Center ? .5f :
            anchor is UiAnchor.BottomLeft or UiAnchor.BottomCenter or UiAnchor.BottomRight ? 1f : 0f;
        float lineHeight = atlas.LineHeight * scale;
        float top = anchorPoint.Y - lines.Count * lineHeight * vertical;

        for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            string line = lines[lineIndex];
            float x = anchorPoint.X - Measure(line) * horizontal;
            float baseline = top + lineIndex * lineHeight + atlas.BaseLine * scale;
            int previous = -1;
            foreach (char c in line)
            {
                int codepoint = c;
                if (!atlas.TryGetGlyph(codepoint, out FontAtlas.Glyph glyph) &&
                    !atlas.TryGetGlyph('?', out glyph)) continue;
                x += (previous < 0 ? 0 : atlas.GetKerning(previous, codepoint)) * scale;
                if (glyph.Width > 0 && glyph.Height > 0)
                {
                    var center = new Vector2(
                        x + (glyph.OffsetX + glyph.Width * .5f) * scale,
                        baseline + (glyph.OffsetY + glyph.Height * .5f) * scale);
                    DrawGlyph(atlas.Texture, center,
                        new Vector2(glyph.Width * scale, glyph.Height * scale), color,
                        new Vector2((float)glyph.X / atlas.Width, (float)glyph.Y / atlas.Height),
                        new Vector2((float)(glyph.X + glyph.Width) / atlas.Width,
                            (float)(glyph.Y + glyph.Height) / atlas.Height));
                }
                x += glyph.Advance * scale;
                previous = codepoint;
            }
        }
    }

    private string? ResolveFontPath(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            string candidate = Path.IsPathRooted(path) ? path :
                Path.Combine(FontProjectRoot ?? Directory.GetCurrentDirectory(), path);
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        }
        string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        foreach (string name in new[] { "segoeui.ttf", "arial.ttf", "DejaVuSans.ttf" })
        {
            string candidate = Path.Combine(fonts, name);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private void DrawGlyph(Texture2D texture, Vector2 position, Vector2 size, Vector4 color, Vector2 uvMin, Vector2 uvMax)
    {
        if (_shader == null) return;
        _shader.Use();
        _shader.SetVector2("uUvMin", uvMin);
        _shader.SetVector2("uUvMax", uvMax);
        try { DrawInternal(texture, position, size, 0f, color); }
        finally
        {
            _shader.Use();
            _shader.SetVector2("uUvMin", Vector2.Zero);
            _shader.SetVector2("uUvMax", Vector2.One);
        }
    }

    public void DrawQuad(
        Vector2 position,
        Vector2 size,
        Vector4 color,
        float rotation = 0.0f)
    {
        DrawInternal(
            null,
            position,
            size,
            rotation,
            color
        );
    }

    public void DrawSprite(
        Texture2D texture,
        Vector2 position,
        Vector2 size,
        float rotation = 0.0f)
    {
        DrawSprite(
            texture,
            position,
            size,
            rotation,
            Vector4.One
        );
    }

    public void DrawSprite(
        Texture2D texture,
        Vector2 position,
        Vector2 size,
        float rotation,
        Vector4 tint)
    {
        ArgumentNullException.ThrowIfNull(
            texture
        );

        DrawInternal(
            texture,
            position,
            size,
            rotation,
            tint
        );
    }

    private void DrawInternal(
        Texture2D? texture,
        Vector2 position,
        Vector2 size,
        float rotation,
        Vector4 color)
    {
        GL.Disable(EnableCap.DepthTest);
        if (!_initialized ||
            _shader == null)
        {
            throw new InvalidOperationException(
                "Renderer2D has not been initialized."
            );
        }

        _shader.Use();

        _shader.SetVector2(
            "uPosition",
            position
        );

        _shader.SetVector2(
            "uSize",
            size
        );

        _shader.SetFloat(
            "uRotation",
            rotation
        );

        _shader.SetVector4(
            "uColor",
            color
        );

        if (texture != null)
        {
            texture.Bind(
                0
            );

            _shader.SetInt(
                "uTexture",
                0
            );

            _shader.SetInt(
                "uUseTexture",
                1
            );
        }
        else
        {
            _shader.SetInt(
                "uUseTexture",
                0
            );
        }

        GL.BindVertexArray(
            _vertexArray
        );

        GL.DrawArrays(
            PrimitiveType.Triangles,
            0,
            6
        );

        GL.BindVertexArray(
            0
        );
    }

    public void Dispose()
    {
        if (!_initialized)
        {
            return;
        }

        foreach (FontAtlas font in _fonts.Values) font.Dispose();
        _fonts.Clear();
        _shader?.Dispose();

        GL.DeleteBuffer(
            _vertexBuffer
        );

        GL.DeleteVertexArray(
            _vertexArray
        );

        _vertexBuffer = 0;

        _vertexArray = 0;

        _shader = null;

        _initialized = false;
    }

    private const string VertexShaderSource =
        """
        #version 330 core

        layout(location = 0) in vec2 aPosition;
        layout(location = 1) in vec2 aTexCoord;

        uniform mat4 uProjection;

        uniform vec2 uPosition;
        uniform vec2 uSize;

        uniform float uRotation;

        uniform vec2 uCameraPosition;
        uniform vec2 uViewportSize;
        uniform float uCameraZoom;

        uniform vec2 uUvMin;
        uniform vec2 uUvMax;

        out vec2 vTexCoord;

        void main()
        {
            vec2 localPosition =
                aPosition *
                uSize;

            float angle =
                radians(
                    uRotation
                );

            float cosine =
                cos(angle);

            float sine =
                sin(angle);

            vec2 rotatedPosition =
                vec2(
                    localPosition.x * cosine -
                    localPosition.y * sine,

                    localPosition.x * sine +
                    localPosition.y * cosine
                );

            vec2 worldPosition =
                rotatedPosition +
                uPosition;

            vec2 viewportCenter =
                uViewportSize *
                0.5;

            vec2 screenPosition =
                (
                    worldPosition -
                    uCameraPosition
                ) *
                uCameraZoom +
                viewportCenter;

            gl_Position =
                uProjection *
                vec4(
                    screenPosition,
                    0.0,
                    1.0
                );

            vTexCoord = mix(uUvMin, uUvMax, aTexCoord);
        }
        """;

    private const string FragmentShaderSource =
        """
        #version 330 core

        in vec2 vTexCoord;

        out vec4 FragColor;

        uniform sampler2D uTexture;

        uniform vec4 uColor;

        uniform int uUseTexture;

        void main()
        {
            vec4 baseColor =
                vec4(1.0);

            if (uUseTexture == 1)
            {
                baseColor =
                    texture(
                        uTexture,
                        vTexCoord
                    );
            }

            FragColor =
                baseColor *
                uColor;
        }
        """;
}
