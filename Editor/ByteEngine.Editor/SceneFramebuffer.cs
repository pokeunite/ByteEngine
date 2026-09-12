using System.Numerics;

using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Editor;

internal sealed class SceneFramebuffer
    : IDisposable
{
    private int _framebuffer;

    private int _colorTexture;
    private int _depthRenderbuffer;

    private int _width;

    private int _height;

    public nint TextureId =>
        _colorTexture;

    public void Render(
        Renderer2D renderer,
        Renderer3D renderer3D,
        Scene scene,
        EditorMode mode,
        EditorCamera camera,
        EditorCamera3D camera3D,
        bool is3D,
        int width,
        int height,
        int windowWidth,
        int windowHeight,
        bool drawGrid3D = true)
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
            ClearBufferMask.ColorBufferBit |
            ClearBufferMask.DepthBufferBit
        );

        renderer.Resize(
            _width,
            _height
        );

        RenderContext context;

        if (is3D)
        {
            context =
                new RenderContext(
                    renderer,
                    renderer3D,
                    scene,
                    _width,
                    _height,
                    viewMatrix3D:
                        camera3D.View,
                    projectionMatrix3D:
                        camera3D.Projection(
                            (float)_width /
                            _height)
                );

            if (drawGrid3D)
            {
                DrawGrid3D(
                    renderer3D,
                    camera3D
                );
            }
        }
        else
        {
            renderer.SetCamera(
                camera.Position,
                camera.Zoom
            );

            DrawGrid(
                renderer,
                camera
            );

            context =
                new RenderContext(
                    renderer,
                    renderer3D,
                    scene,
                    _width,
                    _height
                );
        }

        if (mode ==
            EditorMode.Edit)
        {
            scene.RenderEditorInternal(
                context
            );
        }
        else
        {
            scene.RenderInternal(
                context
            );
        }

        GL.BindFramebuffer(
            FramebufferTarget.Framebuffer,
            0
        );

        GL.Viewport(
            0,
            0,
            Math.Max(
                windowWidth,
                1),
            Math.Max(
                windowHeight,
                1)
        );

        renderer.Resize(
            Math.Max(
                windowWidth,
                1),
            Math.Max(
                windowHeight,
                1)
        );
    }

    public void RenderGame(
        Renderer2D renderer,
        Renderer3D renderer3D,
        Scene scene,
        EditorMode mode,
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
            0.025f,
            0.025f,
            0.035f,
            1.0f
        );

        GL.Clear(
            ClearBufferMask.ColorBufferBit |
            ClearBufferMask.DepthBufferBit
        );

        renderer.Resize(
            _width,
            _height
        );

        Camera3D? camera3D = scene.ActiveCamera;

        Camera2D? camera =
            camera3D ==
            null
                ? scene.FindComponent<Camera2D>()
                : null;

        if (camera !=
            null)
        {
            renderer.SetCamera(
                camera.Transform.Position,
                camera.Zoom
            );
        }
        else
        {
            renderer.ResetCamera();
        }

        var context =
            new RenderContext(
                renderer,
                renderer3D,
                scene,
                _width,
                _height,
                camera,
                camera3D
            );

        if (mode ==
            EditorMode.Edit)
        {
            scene.RenderEditorInternal(
                context
            );
        }
        else
        {
            scene.RenderInternal(
                context
            );
        }

        GL.BindFramebuffer(
            FramebufferTarget.Framebuffer,
            0
        );

        GL.Viewport(
            0,
            0,
            Math.Max(
                windowWidth,
                1),
            Math.Max(
                windowHeight,
                1)
        );

        renderer.Resize(
            Math.Max(
                windowWidth,
                1),
            Math.Max(
                windowHeight,
                1)
        );
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

        if (_framebuffer !=
                0 &&
            width ==
                _width &&
            height ==
                _height)
        {
            return;
        }

        DestroyResources();

        _width =
            width;

        _height =
            height;

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

        _depthRenderbuffer =
            GL.GenRenderbuffer();

        GL.BindRenderbuffer(
            RenderbufferTarget.Renderbuffer,
            _depthRenderbuffer
        );

        GL.RenderbufferStorage(
            RenderbufferTarget.Renderbuffer,
            RenderbufferStorage.Depth24Stencil8,
            _width,
            _height
        );

        GL.FramebufferRenderbuffer(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.DepthStencilAttachment,
            RenderbufferTarget.Renderbuffer,
            _depthRenderbuffer
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
        float gridSize =
            16.0f;

        while (gridSize *
               camera.Zoom <
               12.0f)
        {
            gridSize *=
                2.0f;
        }

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

        for (float x =
                 firstX;
             x <=
             maximumX;
             x +=
             gridSize)
        {
            renderer.DrawQuad(
                new Vector2(
                    x,
                    camera.Position.Y
                ),
                new Vector2(
                    lineWidth,
                    halfWorldHeight *
                    2.0f
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

        for (float y =
                 firstY;
             y <=
             maximumY;
             y +=
             gridSize)
        {
            renderer.DrawQuad(
                new Vector2(
                    camera.Position.X,
                    y
                ),
                new Vector2(
                    halfWorldWidth *
                    2.0f,
                    lineWidth
                ),
                gridColor
            );
        }

        Vector4 xAxisColor =
            new(
                .24f,
                .48f,
                .28f,
                1f
            );

        Vector4 yAxisColor =
            new(
                .52f,
                .24f,
                .24f,
                1f
            );

        if (minimumY <=
                0f &&
            maximumY >=
                0f)
        {
            renderer.DrawQuad(
                new Vector2(
                    camera.Position.X,
                    0f
                ),
                new Vector2(
                    halfWorldWidth *
                    2f,
                    lineWidth *
                    2f
                ),
                xAxisColor
            );
        }

        if (minimumX <=
                0f &&
            maximumX >=
                0f)
        {
            renderer.DrawQuad(
                new Vector2(
                    0f,
                    camera.Position.Y
                ),
                new Vector2(
                    lineWidth *
                    2f,
                    halfWorldHeight *
                    2f
                ),
                yAxisColor
            );
        }
    }

    private void DrawGrid3D(
        Renderer3D renderer,
        EditorCamera3D camera)
    {
        Matrix4x4 view =
            camera.View;

        Matrix4x4 projection =
            camera.Projection(
                (float)_width /
                _height
            );

        Mesh cube =
            renderer.GetPrimitive(
                PrimitiveMeshType.Cube
            );

        for (int coordinate =
                 -10;
             coordinate <=
             10;
             coordinate++)
        {
            Vector4 xColor =
                coordinate ==
                0
                    ? new Vector4(
                        .25f,
                        .45f,
                        .9f,
                        1f
                    )
                    : new Vector4(
                        .18f,
                        .2f,
                        .24f,
                        1f
                    );

            Matrix4x4 xTransform =
                Matrix4x4.CreateScale(
                    .012f,
                    .005f,
                    20f
                ) *
                Matrix4x4.CreateTranslation(
                    coordinate,
                    0f,
                    0f
                );

            renderer.Draw(
                cube,
                new Material
                {
                    BaseColor =
                        xColor
                },
                xTransform,
                view,
                projection,
                new Vector3(
                    0f,
                    -1f,
                    0f
                ),
                Vector3.One,
                0f,
                1f
            );

            Vector4 zColor =
                coordinate ==
                0
                    ? new Vector4(
                        .9f,
                        .25f,
                        .22f,
                        1f
                    )
                    : new Vector4(
                        .18f,
                        .2f,
                        .24f,
                        1f
                    );

            Matrix4x4 zTransform =
                Matrix4x4.CreateScale(
                    20f,
                    .005f,
                    .012f
                ) *
                Matrix4x4.CreateTranslation(
                    0f,
                    0f,
                    coordinate
                );

            renderer.Draw(
                cube,
                new Material
                {
                    BaseColor =
                        zColor
                },
                zTransform,
                view,
                projection,
                new Vector3(
                    0f,
                    -1f,
                    0f
                ),
                Vector3.One,
                0f,
                1f
            );
        }

        Matrix4x4 yTransform =
            Matrix4x4.CreateScale(
                .012f,
                2f,
                .012f
            ) *
            Matrix4x4.CreateTranslation(
                0f,
                1f,
                0f
            );

        renderer.Draw(
            cube,
            new Material
            {
                BaseColor =
                    new Vector4(
                        .2f,
                        1f,
                        .3f,
                        1f
                    )
            },
            yTransform,
            view,
            projection,
            new Vector3(
                0f,
                -1f,
                0f
            ),
            Vector3.One,
            0f,
            1f
        );
    }

    private void DestroyResources()
    {
        if (_depthRenderbuffer !=
            0)
        {
            GL.DeleteRenderbuffer(
                _depthRenderbuffer
            );

            _depthRenderbuffer =
                0;
        }

        if (_colorTexture !=
            0)
        {
            GL.DeleteTexture(
                _colorTexture
            );

            _colorTexture =
                0;
        }

        if (_framebuffer !=
            0)
        {
            GL.DeleteFramebuffer(
                _framebuffer
            );

            _framebuffer =
                0;
        }
    }

    public void Dispose()
    {
        DestroyResources();
    }
}
