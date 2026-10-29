using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ByteEngine.Core.Graphics;
namespace ByteEngine.Tests;
internal static class BrowserTextureTransferTests
{
    static void Field(Texture2D t,string name,object value)=>typeof(Texture2D).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(t,value);
    static Texture2D Make(int w,int h,byte[] bytes,float[]? floats=null){var t=(Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));Field(t,"<Width>k__BackingField",w);Field(t,"<Height>k__BackingField",h);Field(t,"<IsHdr>k__BackingField",floats!=null);Field(t,"_pixels",bytes);if(floats!=null)Field(t,"_floatPixels",floats);return t;}
    public static void Run(){
        var bytes=Enumerable.Range(0,200*180*4).Select(i=>(byte)(i%251)).ToArray();using var texture=Make(200,180,bytes);
        if(!BrowserTextureTransfer.TryRead(texture,128,128,72,52,out var result,out var hdr)||hdr||result.Length!=72*52*4)throw new Exception("Valid partial RGBA upload failed");
        for(int row=0;row<52;row++)if(!result.AsSpan(row*72*4,72*4).SequenceEqual(bytes.AsSpan(((128+row)*200+128)*4,72*4)))throw new Exception("RGBA rows corrupted");
        var destination=new byte[result.Length+16];Array.Fill(destination,(byte)255);
        if(!BrowserTextureTransfer.TryCopy(texture,128,128,72,52,destination.AsSpan(8,result.Length),out hdr)||hdr||!destination.AsSpan(8,result.Length).SequenceEqual(result)||destination[7]!=255||destination[^1]!=255)throw new Exception("Direct pooled RGBA copy changed bounds or rows");
        if(BrowserTextureTransfer.TryCopy(texture,128,128,72,52,destination,out _))throw new Exception("Mismatched destination accepted");
        texture.Dispose();bool baselineThrows=false;try{_ = texture.PixelData.Span.Slice((128*200+128)*4,72*4).Length;}catch(ArgumentOutOfRangeException){baselineThrows=true;}
        if(!baselineThrows||BrowserTextureTransfer.TryRead(texture,128,128,72,52,out _,out _))throw new Exception("Disposed scene texture must discard deferred upload");
        using var resized=Make(16,16,new byte[16*16*4]);if(BrowserTextureTransfer.TryRead(resized,128,128,72,52,out _,out _))throw new Exception("Old region survived texture resize");
        var floats=Enumerable.Range(0,8*4*4).Select(i=>i*.25f).ToArray();using var high=Make(8,4,[],floats);
        if(!BrowserTextureTransfer.TryRead(high,2,1,3,2,out var data,out hdr)||!hdr||data.Length!=3*2*16)throw new Exception("HDR rectangle upload failed");
        if(!data.AsSpan(0,48).SequenceEqual(MemoryMarshal.AsBytes(floats.AsSpan((8+2)*4,12))))throw new Exception("HDR values corrupted");
        var direct=new byte[data.Length];if(!BrowserTextureTransfer.TryCopy(high,2,1,3,2,direct,out hdr)||!hdr||!direct.SequenceEqual(data))throw new Exception("Direct HDR copy corrupted values");
        high.Dispose();if(BrowserTextureTransfer.TryRead(high,2,1,3,2,out _,out _))throw new Exception("Disposed HDR upload survived");
        Console.WriteLine("PASS Deferred browser texture disposal reproduces old crash; valid RGBA/HDR rows and stale resize rejection");
    }
}
