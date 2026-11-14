using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using StbImageSharp;
namespace ByteEngine.Core.Assets;

/// <summary>Role-aware browser material preparation, before download.
/// PNG preserves channels and alpha exactly. Original project assets are never modified.</summary>
internal enum WebTextureRole { Data, Color, Normal, UserInterface }
internal static class WebTextureCooker
{
    public static byte[] Optimize(byte[] encoded,int maximumSize=1024,WebTextureRole role=WebTextureRole.Data)
    {
        if(maximumSize<1)throw new ArgumentOutOfRangeException(nameof(maximumSize));
        if(role==WebTextureRole.UserInterface)return encoded;
        // HDR follows the floating-point path; never quantize it to an 8-bit PNG.
        if(encoded.AsSpan().StartsWith("#?RADIANCE"u8)||encoded.AsSpan().StartsWith("#?RGBE"u8))return encoded;
        // Sixteen-bit PNGs commonly carry height/data fields, rather than display colour.
        if(encoded.Length>24&&encoded.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})&&encoded[24]==16)return encoded;
        using var source=new MemoryStream(encoded,false);
        var info=ImageInfo.FromStream(source);
        if(info is not {} header||Math.Max(header.Width,header.Height)<=maximumSize)return encoded;
        source.Position=0;
        var image=ImageResult.FromStream(source,ColorComponents.RedGreenBlueAlpha);
        float scale=(float)maximumSize/Math.Max(image.Width,image.Height);
        int width=Math.Max(1,(int)(image.Width*scale)),height=Math.Max(1,(int)(image.Height*scale));
        var rgba=new byte[width*height*4];
        // Area average rather than nearest-neighbour decimation: preserves fine stone and foliage detail.
        Span<int> sum=stackalloc int[4];Span<float> linear=stackalloc float[3];
        static float Decode(byte v){float x=v/255f;return x<=.04045f?x/12.92f:MathF.Pow((x+.055f)/1.055f,2.4f);}
        static byte Encode(float x)=>(byte)Math.Clamp((int)MathF.Round((x<=.0031308f?12.92f*x:1.055f*MathF.Pow(x,1/2.4f)-.055f)*255),0,255);
        for(int y=0;y<height;y++)for(int x=0;x<width;x++){
            sum.Clear();linear.Clear();int x0=x*image.Width/width,x1=Math.Max(x0+1,(x+1)*image.Width/width),y0=y*image.Height/height,y1=Math.Max(y0+1,(y+1)*image.Height/height);int count=0;
            for(int sy=y0;sy<y1;sy++)for(int sx=x0;sx<x1;sx++){int i=(sy*image.Width+sx)*4;for(int c=0;c<4;c++)sum[c]+=image.Data[i+c];if(role==WebTextureRole.Color)for(int c=0;c<3;c++)linear[c]+=Decode(image.Data[i+c])*image.Data[i+3];count++;}
            int dest=(y*width+x)*4;for(int c=0;c<4;c++)rgba[dest+c]=(byte)(sum[c]/count);
            if(role==WebTextureRole.Color)for(int c=0;c<3;c++)rgba[dest+c]=sum[3]>0?Encode(linear[c]/sum[3]):(byte)0;
            if(role==WebTextureRole.Normal){var v=new System.Numerics.Vector3(rgba[dest]/127.5f-1,rgba[dest+1]/127.5f-1,rgba[dest+2]/127.5f-1);v=v.LengthSquared()>.00001f?System.Numerics.Vector3.Normalize(v):System.Numerics.Vector3.UnitZ;rgba[dest]=(byte)Math.Clamp((v.X+1)*127.5f,0,255);rgba[dest+1]=(byte)Math.Clamp((v.Y+1)*127.5f,0,255);rgba[dest+2]=(byte)Math.Clamp((v.Z+1)*127.5f,0,255);}
        }
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
