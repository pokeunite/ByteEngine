using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using StbImageSharp;
namespace ByteEngine.Core.Assets;

/// <summary>Prepare the same 1024-pixel image the browser would generate, before download.
/// PNG preserves channels and alpha exactly. Original project assets are never modified.</summary>
internal static class WebTextureCooker
{
    public static byte[] Optimize(byte[] encoded)
    {
        // HDR follows the floating-point path; never quantize it to an 8-bit PNG.
        if(encoded.AsSpan().StartsWith("#?RADIANCE"u8)||encoded.AsSpan().StartsWith("#?RGBE"u8))return encoded;
        // Sixteen-bit PNGs commonly carry height/data fields, rather than display colour.
        if(encoded.Length>24&&encoded.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})&&encoded[24]==16)return encoded;
        using var source=new MemoryStream(encoded,false);
        var info=ImageInfo.FromStream(source);
        if(info is not {} header||Math.Max(header.Width,header.Height)<=1024)return encoded;
        source.Position=0;
        var image=ImageResult.FromStream(source,ColorComponents.RedGreenBlueAlpha);
        float scale=1024f/Math.Max(image.Width,image.Height);
        int width=Math.Max(1,(int)(image.Width*scale)),height=Math.Max(1,(int)(image.Height*scale));
        var rgba=new byte[width*height*4];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            image.Data.AsSpan(((y*image.Height/height)*image.Width+x*image.Width/width)*4,4).CopyTo(rgba.AsSpan((y*width+x)*4,4));
        return Png(width,height,rgba);
    }
    internal static byte[] Png(int width,int height,byte[] rgba)
    {
        using var output=new MemoryStream();output.Write(new byte[]{137,80,78,71,13,10,26,10});
        var header=new byte[13];BinaryPrimitives.WriteInt32BigEndian(header,width);BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4),height);header[8]=8;header[9]=6;
        Chunk(output,"IHDR",header);
        using var compressed=new MemoryStream();
        using(var zlib=new ZLibStream(compressed,CompressionLevel.Optimal,true))for(int y=0;y<height;y++){zlib.WriteByte(0);zlib.Write(rgba.AsSpan(y*width*4,width*4));}
        Chunk(output,"IDAT",compressed.ToArray());Chunk(output,"IEND",[]);return output.ToArray();
    }
    static void Chunk(Stream output,string kind,byte[] data)
    {
        Span<byte> size=stackalloc byte[4];BinaryPrimitives.WriteInt32BigEndian(size,data.Length);output.Write(size);
        var type=Encoding.ASCII.GetBytes(kind);output.Write(type);output.Write(data);uint crc=uint.MaxValue;
        foreach(byte value in type.Concat(data)){crc^=value;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0);}
        BinaryPrimitives.WriteUInt32BigEndian(size,~crc);output.Write(size);
    }
}
