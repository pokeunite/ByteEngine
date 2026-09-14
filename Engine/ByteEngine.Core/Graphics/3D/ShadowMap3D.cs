using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// Reusable depth-only framebuffer for the directional shadow pass.
///
/// Resource creation and shadow rendering both preserve the framebuffer that
/// was already bound by the editor/game viewport.
/// </summary>
internal sealed class ShadowMap3D : IDisposable
{
    private int _framebuffer;

    private int _depthTexture;

    private int _resolution;

    public int DepthTextureId =>
        _depthTexture;

    public int Resolution =>
        _resolution;

    public int Begin(
        int requestedResolution)
    {
        GL.GetInteger(
            GetPName.FramebufferBinding,
            out int previousFramebuffer);

        EnsureResolution(
            requestedResolution);

        GL.BindFramebuffer(
            FramebufferTarget.Framebuffer,
            _framebuffer);

        GL.Viewport(
            0,
            0,
            _resolution,
            _resolution);

        GL.Clear(
            ClearBufferMask.DepthBufferBit);

        return previousFramebuffer;
    }

    public void End(
        int previousFramebuffer,
        int targetWidth,
        int targetHeight)
    {
        GL.BindFramebuffer(
            FramebufferTarget.Framebuffer,
            previousFramebuffer);

        GL.Viewport(
            0,
            0,
            Math.Max(
                targetWidth,
                1),
            Math.Max(
                targetHeight,
                1));
    }

    private void EnsureResolution(
        int requestedResolution)
    {
        int resolution =
            NormalizeResolution(
                requestedResolution);

        if (_framebuffer !=
                0 &&
            _depthTexture !=
                0 &&
            _resolution ==
                resolution)
        {
            return;
        }

        /*
         * This method can be called while SceneFramebuffer/GameFramebuffer is
         * bound. Preserve that binding while allocating the depth target.
         */
        GL.GetInteger(
            GetPName.FramebufferBinding,
            out int previousFramebuffer);

        DestroyResources();

        _resolution =
            resolution;

        _framebuffer =
            GL.GenFramebuffer();

        _depthTexture =
            GL.GenTexture();

        GL.BindTexture(
            TextureTarget.Texture2D,
            _depthTexture);

        GL.TexImage2D(
            TextureTarget.Texture2D,
            0,
            PixelInternalFormat.DepthComponent24,
            _resolution,
            _resolution,
            0,
            PixelFormat.DepthComponent,
            PixelType.Float,
            IntPtr.Zero);

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter,
            (int)TextureMinFilter.Nearest);

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter,
            (int)TextureMagFilter.Nearest);

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureWrapS,
            (int)TextureWrapMode.ClampToEdge);

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureWrapT,
            (int)TextureWrapMode.ClampToEdge);

        GL.BindFramebuffer(
            FramebufferTarget.Framebuffer,
            _framebuffer);

        GL.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.DepthAttachment,
            TextureTarget.Texture2D,
            _depthTexture,
            0);

        GL.DrawBuffer(
            DrawBufferMode.None);

        GL.ReadBuffer(
            ReadBufferMode.None);

        FramebufferErrorCode status =
            GL.CheckFramebufferStatus(
                FramebufferTarget.Framebuffer);

        GL.BindTexture(
            TextureTarget.Texture2D,
            0);

        GL.BindFramebuffer(
            FramebufferTarget.Framebuffer,
            previousFramebuffer);

        if (status !=
            FramebufferErrorCode.FramebufferComplete)
        {
            DestroyResources();

            throw new InvalidOperationException(
                $"Directional shadow framebuffer is incomplete: {status}");
        }
    }

    private static int NormalizeResolution(
        int value)
    {
        if (value <= 384)
        {
            return 256;
        }

        if (value <= 768)
        {
            return 512;
        }

        if (value <= 1536)
        {
            return 1024;
        }

        if (value <= 3072)
        {
            return 2048;
        }

        return 4096;
    }

    private void DestroyResources()
    {
        if (_depthTexture !=
            0)
        {
            GL.DeleteTexture(
                _depthTexture);

            _depthTexture =
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

        _resolution =
            0;
    }

    public void Dispose()
    {
        DestroyResources();
    }
}
