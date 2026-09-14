using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// Reusable depth cubemap framebuffer for one point-light shadow.
/// </summary>
internal sealed class PointShadowMap3D : IDisposable
{
    private int _framebuffer;
    private int _depthCubeTexture;
    private int _resolution;

    public int DepthCubeTextureId =>
        _depthCubeTexture;

    public int Resolution =>
        _resolution;

    public int BeginFace(
        int requestedResolution,
        int faceIndex)
    {
        if (faceIndex is < 0 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(faceIndex));
        }

        GL.GetInteger(
            GetPName.FramebufferBinding,
            out int previousFramebuffer);

        EnsureResolution(requestedResolution);

        GL.BindFramebuffer(
            FramebufferTarget.Framebuffer,
            _framebuffer);

        TextureTarget face =
            (TextureTarget)(
                (int)TextureTarget.TextureCubeMapPositiveX +
                faceIndex);

        GL.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.DepthAttachment,
            face,
            _depthCubeTexture,
            0);

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
            Math.Max(targetWidth, 1),
            Math.Max(targetHeight, 1));
    }

    private void EnsureResolution(
        int requestedResolution)
    {
        int resolution =
            NormalizeResolution(requestedResolution);

        if (_framebuffer != 0 &&
            _depthCubeTexture != 0 &&
            _resolution == resolution)
        {
            return;
        }

        GL.GetInteger(
            GetPName.FramebufferBinding,
            out int previousFramebuffer);

        DestroyResources();

        _resolution = resolution;
        _framebuffer = GL.GenFramebuffer();
        _depthCubeTexture = GL.GenTexture();

        GL.BindTexture(
            TextureTarget.TextureCubeMap,
            _depthCubeTexture);

        for (int faceIndex = 0;
             faceIndex < 6;
             faceIndex++)
        {
            TextureTarget face =
                (TextureTarget)(
                    (int)TextureTarget.TextureCubeMapPositiveX +
                    faceIndex);

            GL.TexImage2D(
                face,
                0,
                PixelInternalFormat.DepthComponent24,
                _resolution,
                _resolution,
                0,
                PixelFormat.DepthComponent,
                PixelType.Float,
                IntPtr.Zero);
        }

        GL.TexParameter(
            TextureTarget.TextureCubeMap,
            TextureParameterName.TextureMinFilter,
            (int)TextureMinFilter.Nearest);

        GL.TexParameter(
            TextureTarget.TextureCubeMap,
            TextureParameterName.TextureMagFilter,
            (int)TextureMagFilter.Nearest);

        GL.TexParameter(
            TextureTarget.TextureCubeMap,
            TextureParameterName.TextureWrapS,
            (int)TextureWrapMode.ClampToEdge);

        GL.TexParameter(
            TextureTarget.TextureCubeMap,
            TextureParameterName.TextureWrapT,
            (int)TextureWrapMode.ClampToEdge);

        GL.TexParameter(
            TextureTarget.TextureCubeMap,
            TextureParameterName.TextureWrapR,
            (int)TextureWrapMode.ClampToEdge);

        GL.BindFramebuffer(
            FramebufferTarget.Framebuffer,
            _framebuffer);

        GL.DrawBuffer(DrawBufferMode.None);
        GL.ReadBuffer(ReadBufferMode.None);

        GL.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.DepthAttachment,
            TextureTarget.TextureCubeMapPositiveX,
            _depthCubeTexture,
            0);

        FramebufferErrorCode status =
            GL.CheckFramebufferStatus(
                FramebufferTarget.Framebuffer);

        GL.BindTexture(
            TextureTarget.TextureCubeMap,
            0);

        GL.BindFramebuffer(
            FramebufferTarget.Framebuffer,
            previousFramebuffer);

        if (status !=
            FramebufferErrorCode.FramebufferComplete)
        {
            DestroyResources();
            throw new InvalidOperationException(
                $"Point shadow framebuffer is incomplete: {status}");
        }
    }

    private static int NormalizeResolution(int value)
    {
        if (value <= 192) return 128;
        if (value <= 384) return 256;
        if (value <= 768) return 512;
        if (value <= 1536) return 1024;
        return 2048;
    }

    private void DestroyResources()
    {
        if (_depthCubeTexture != 0)
        {
            GL.DeleteTexture(_depthCubeTexture);
            _depthCubeTexture = 0;
        }

        if (_framebuffer != 0)
        {
            GL.DeleteFramebuffer(_framebuffer);
            _framebuffer = 0;
        }

        _resolution = 0;
    }

    public void Dispose() =>
        DestroyResources();
}
