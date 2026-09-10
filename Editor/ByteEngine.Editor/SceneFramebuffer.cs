using System.Numerics;

using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;

using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Editor;

internal sealed class SceneFramebuffer
    : IDisposable
{
    private int _framebuffer;

    private int _colorTexture;

    private int _width;

    private int _height;

    public nint TextureId =>
        _colorTexture;

    public void Render(
        Renderer2D renderer,
        Scene scene,
        EditorMode mode,
        EditorCamera camera,
        int width,
        int height,
        int windowWidth,
        int windowHeight)
    {
        Resize(
            width,
            height
        );

        GL.BindFramebuffer(
            FramebufferTarget.Framebuffer,
            _framebuffer
        );

        GL.Viewport(
            0,
            0,
            _width,
            _height
        );

        GL.ClearColor(
            0.055f,
            0.065f,
            0.085f,
            1.0f
        );

        GL.Clear(
            ClearBufferMask.ColorBufferBit
        );

        renderer.Resize(
            _width,
            _height
        );

        renderer.SetCamera(
            camera.Position,
            camera.Zoom
        );

        DrawGrid(
            renderer,
            camera
        );

        if (mode == EditorMode.Edit)
        {
            scene.RenderEditorInternal(
                renderer
            );
        }
        else
        {
            scene.RenderInternal(
                renderer
            );
        }

        GL.BindFramebuffer(
            FramebufferTarget.Framebuffer,
            0
        );

        GL.Viewport(
            0,
            0,
            Math.Max(windowWidth, 1),
            Math.Max(windowHeight, 1)
        );

        renderer.Resize(
            Math.Max(windowWidth, 1),
            Math.Max(windowHeight, 1)
        );
    }

    public void RenderGame(
        Renderer2D renderer,
        Scene scene,
        EditorMode mode,
        int width,
        int height,
        int windowWidth,
        int windowHeight)
    {
        Resize(width, height);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        GL.Viewport(0, 0, _width, _height);
        GL.ClearColor(0.025f, 0.025f, 0.035f, 1.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        renderer.Resize(_width, _height);

        Camera2D? camera = scene.FindComponent<Camera2D>();
        if (camera != null) renderer.SetCamera(camera.Transform.Position, camera.Zoom);
        else renderer.ResetCamera();

        if (mode == EditorMode.Edit) scene.RenderEditorInternal(renderer);
        else scene.RenderInternal(renderer);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.Viewport(0, 0, Math.Max(windowWidth, 1), Math.Max(windowHeight, 1));
        renderer.Resize(Math.Max(windowWidth, 1), Math.Max(windowHeight, 1));
    }

    private void Resize(
        int width,
        int height)
    {
        width =
            Math.Max(
                width,
                1
            );

        height =
            Math.Max(
                height,
                1
            );

        if (_framebuffer != 0 &&
            width == _width &&
            height == _height)
        {
            return;
        }

        DestroyResources();

        _width = width;
        _height = height;

        _framebuffer =
            GL.GenFramebuffer();

        GL.BindFramebuffer(
            FramebufferTarget.Framebuffer,
            _framebuffer
        );

        _colorTexture =
            GL.GenTexture();

        GL.BindTexture(
            TextureTarget.Texture2D,
            _colorTexture
        );

        GL.TexImage2D(
            TextureTarget.Texture2D,
            0,
            PixelInternalFormat.Rgba8,
            _width,
            _height,
            0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            IntPtr.Zero
        );

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter,
            (int)TextureMinFilter.Linear
        );

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter,
            (int)TextureMagFilter.Linear
        );

        GL.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D,
            _colorTexture,
            0
        );

        FramebufferErrorCode status =
            GL.CheckFramebufferStatus(
                FramebufferTarget.Framebuffer
            );

        GL.BindTexture(
            TextureTarget.Texture2D,
            0
        );

        GL.BindFramebuffer(
            FramebufferTarget.Framebuffer,
            0
        );

        if (status !=
            FramebufferErrorCode.FramebufferComplete)
        {
            throw new InvalidOperationException(
                $"Scene framebuffer is incomplete: {status}"
            );
        }
    }

    private void DrawGrid(
        Renderer2D renderer,
        EditorCamera camera)
    {
        float gridSize = 16.0f;
        while (gridSize * camera.Zoom < 12.0f) gridSize *= 2.0f;

        float halfWorldWidth =
            _width /
            camera.Zoom /
            2.0f;

        float halfWorldHeight =
            _height /
            camera.Zoom /
            2.0f;

        float minimumX =
            camera.Position.X -
            halfWorldWidth;

        float maximumX =
            camera.Position.X +
            halfWorldWidth;

        float minimumY =
            camera.Position.Y -
            halfWorldHeight;

        float maximumY =
            camera.Position.Y +
            halfWorldHeight;

        float lineWidth =
            1.0f /
            camera.Zoom;

        Vector4 gridColor =
            new(
                0.16f,
                0.18f,
                0.22f,
                1.0f
            );

        float firstX =
            MathF.Floor(
                minimumX /
                gridSize
            ) *
            gridSize;

        for (float x = firstX;
             x <= maximumX;
             x += gridSize)
        {
            renderer.DrawQuad(
                new Vector2(
                    x,
                    camera.Position.Y
                ),
                new Vector2(
                    lineWidth,
                    halfWorldHeight * 2.0f
                ),
                gridColor
            );
        }

        float firstY =
            MathF.Floor(
                minimumY /
                gridSize
            ) *
            gridSize;

        for (float y = firstY;
             y <= maximumY;
             y += gridSize)
        {
            renderer.DrawQuad(
                new Vector2(
                    camera.Position.X,
                    y
                ),
                new Vector2(
                    halfWorldWidth * 2.0f,
                    lineWidth
                ),
                gridColor
            );
        }

        Vector4 xAxisColor = new(.24f, .48f, .28f, 1f);
        Vector4 yAxisColor = new(.52f, .24f, .24f, 1f);
        if (minimumY <= 0f && maximumY >= 0f)
            renderer.DrawQuad(new Vector2(camera.Position.X, 0f), new Vector2(halfWorldWidth * 2f, lineWidth * 2f), xAxisColor);
        if (minimumX <= 0f && maximumX >= 0f)
            renderer.DrawQuad(new Vector2(0f, camera.Position.Y), new Vector2(lineWidth * 2f, halfWorldHeight * 2f), yAxisColor);
    }

    private void DestroyResources()
    {
        if (_colorTexture != 0)
        {
            GL.DeleteTexture(
                _colorTexture
            );

            _colorTexture = 0;
        }

        if (_framebuffer != 0)
        {
            GL.DeleteFramebuffer(
                _framebuffer
            );

            _framebuffer = 0;
        }
    }

    public void Dispose()
    {
        DestroyResources();
    }
}
