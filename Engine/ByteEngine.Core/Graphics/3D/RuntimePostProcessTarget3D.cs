using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// Floating-point scene target used by standalone 3D runtime output.
/// The completed HDR scene is presented to the window through PostProcess3D.
/// </summary>
internal sealed class RuntimePostProcessTarget3D
    : IDisposable
{
    private int _framebuffer;
    private int _colorTexture;
    private int _depthRenderbuffer;
    private int _width;
    private int _height;

    private readonly PostProcess3D _postProcess =
        new();

    public void Begin(
        int width,
        int height)
    {
        Resize(
            width,
            height);

        GL.BindFramebuffer(
            FramebufferTarget.Framebuffer,
            _framebuffer);

        GL.Viewport(
            0,
            0,
            _width,
            _height);

        GL.Clear(
            ClearBufferMask.ColorBufferBit |
            ClearBufferMask.DepthBufferBit);
    }

    public void Present(
        float exposure,
        int windowWidth,
        int windowHeight)
    {
        _postProcess.Render(
            _colorTexture,
            0,
            Math.Max(
                windowWidth,
                1),
            Math.Max(
                windowHeight,
                1),
            applyToneMapping:
                true,
            exposure:
                exposure);

        GL.BindFramebuffer(
            FramebufferTarget.Framebuffer,
            0);

        GL.Viewport(
            0,
            0,
            Math.Max(
                windowWidth,
                1),
            Math.Max(
                windowHeight,
                1));
    }

    private void Resize(
        int width,
        int height)
    {
        width =
            Math.Max(
                width,
                1);

        height =
            Math.Max(
                height,
                1);

        if (_framebuffer !=
                0 &&
            _width ==
                width &&
            _height ==
                height)
        {
            return;
        }

        DestroyFramebuffer();

        _width =
            width;

        _height =
            height;

        _framebuffer =
            GL.GenFramebuffer();

        GL.BindFramebuffer(
            FramebufferTarget.Framebuffer,
            _framebuffer);

        _colorTexture =
            GL.GenTexture();

        GL.BindTexture(
            TextureTarget.Texture2D,
            _colorTexture);

        GL.TexImage2D(
            TextureTarget.Texture2D,
            0,
            PixelInternalFormat.Rgba16f,
            _width,
            _height,
            0,
            PixelFormat.Rgba,
            PixelType.Float,
            IntPtr.Zero);

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter,
            (int)TextureMinFilter.Linear);

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter,
            (int)TextureMagFilter.Linear);

        GL.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D,
            _colorTexture,
            0);

        _depthRenderbuffer =
            GL.GenRenderbuffer();

        GL.BindRenderbuffer(
            RenderbufferTarget.Renderbuffer,
            _depthRenderbuffer);

        GL.RenderbufferStorage(
            RenderbufferTarget.Renderbuffer,
            RenderbufferStorage.Depth24Stencil8,
            _width,
            _height);

        GL.FramebufferRenderbuffer(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.DepthStencilAttachment,
            RenderbufferTarget.Renderbuffer,
            _depthRenderbuffer);

        FramebufferErrorCode status =
            GL.CheckFramebufferStatus(
                FramebufferTarget.Framebuffer);

        GL.BindTexture(
            TextureTarget.Texture2D,
            0);

        GL.BindRenderbuffer(
            RenderbufferTarget.Renderbuffer,
            0);

        GL.BindFramebuffer(
            FramebufferTarget.Framebuffer,
            0);

        if (status !=
            FramebufferErrorCode.FramebufferComplete)
        {
            DestroyFramebuffer();

            throw new InvalidOperationException(
                $"Runtime HDR framebuffer is incomplete: {status}");
        }
    }

    private void DestroyFramebuffer()
    {
        if (_depthRenderbuffer !=
            0)
        {
            GL.DeleteRenderbuffer(
                _depthRenderbuffer);

            _depthRenderbuffer =
                0;
        }

        if (_colorTexture !=
            0)
        {
            GL.DeleteTexture(
                _colorTexture);

            _colorTexture =
                0;
        }

        if (_framebuffer !=
            0)
        {
            GL.DeleteFramebuffer(
                _framebuffer);

            _framebuffer =
                0;
        }
    }

    public void Dispose()
    {
        DestroyFramebuffer();
        _postProcess.Dispose();
    }
}
