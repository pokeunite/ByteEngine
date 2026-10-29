using System.Runtime.InteropServices;
using ByteEngine.Core.Graphics;

internal static class BrowserTextureTransfer
{
    // Uploads are deferred across frames. Scene teardown can dispose their
    // textures before the queue drains, so validate the current backing store.
    internal static bool TryRead(Texture2D texture,int x,int y,int width,int height,out byte[] pixels,out bool hdr)
    {
        pixels=[];hdr=texture.IsHdr;
        if(!TryGetLength(texture,x,y,width,height,out int length,out hdr))return false;
        pixels=new byte[length];return TryCopy(texture,x,y,width,height,pixels,out hdr);
    }
    internal static bool TryGetLength(Texture2D texture,int x,int y,int width,int height,out int length,out bool hdr)
    {
        length=0;hdr=texture.IsHdr;
        if(x<0||y<0||width<=0||height<=0||x>texture.Width-width||y>texture.Height-height)return false;
        ReadOnlySpan<byte> source=hdr?MemoryMarshal.AsBytes(texture.FloatPixelData.Span):texture.PixelData.Span;
        int pixelBytes=hdr?16:4;
        if(source.Length!=(long)texture.Width*texture.Height*pixelBytes)return false;
        length=checked(width*height*pixelBytes);return true;
    }
    internal static bool TryCopy(Texture2D texture,int x,int y,int width,int height,Span<byte> pixels,out bool hdr)
    {
        if(!TryGetLength(texture,x,y,width,height,out int length,out hdr)||pixels.Length!=length)return false;
        ReadOnlySpan<byte> source=hdr?MemoryMarshal.AsBytes(texture.FloatPixelData.Span):texture.PixelData.Span;
        int pixelBytes=hdr?16:4;
        for(int row=0;row<height;row++)source.Slice(((y+row)*texture.Width+x)*pixelBytes,width*pixelBytes).CopyTo(pixels.Slice(row*width*pixelBytes,width*pixelBytes));
        return true;
    }
}
