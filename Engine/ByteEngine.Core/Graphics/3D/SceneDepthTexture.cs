using OpenTK.Graphics.OpenGL4;
namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>Sampleable depth/stencil attachment shared by editor and standalone output.</summary>
public static class SceneDepthTexture
{
    public static int Create(int width,int height)
    {
        int texture=GL.GenTexture(); GL.BindTexture(TextureTarget.Texture2D,texture);
        GL.TexImage2D(TextureTarget.Texture2D,0,PixelInternalFormat.Depth24Stencil8,width,height,0,PixelFormat.DepthStencil,PixelType.UnsignedInt248,IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMinFilter,(int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMagFilter,(int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapS,(int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapT,(int)TextureWrapMode.ClampToEdge);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer,FramebufferAttachment.DepthStencilAttachment,TextureTarget.Texture2D,texture,0);
        return texture;
    }
}
