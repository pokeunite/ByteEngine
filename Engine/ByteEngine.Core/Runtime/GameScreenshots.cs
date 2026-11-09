using System.Buffers.Binary;
using System.IO.Compression;

namespace ByteEngine.Core.Runtime;

/// <summary>Native hosts capture the next completed frame. Encoding and disk writes run off the render thread.</summary>
public static class GameScreenshots
{
    private static int pending, busy;
    public static bool IsBusy => Volatile.Read(ref pending)!=0 || Volatile.Read(ref busy)!=0;
    public static string LastMessage { get; private set; } = "";
    public static DateTime MessageUntilUtc { get; private set; }
    public static string OutputDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),"ByteEngine","Screenshots");
    public static bool Request()
    {
        if(OperatingSystem.IsBrowser()){Notify("Screenshots are available in the Windows game.");return false;}
        if(IsBusy)return false;
        Interlocked.Exchange(ref pending,1);return true;
    }
    internal static bool BeginCapture()=>Interlocked.Exchange(ref pending,0)!=0 && Interlocked.CompareExchange(ref busy,1,0)==0;
    internal static void Failed(Exception e){Notify("Screenshot failed: "+e.Message);Interlocked.Exchange(ref busy,0);}
    internal static void Save(byte[] bottomUpRgba,int width,int height)
    {
        _=Task.Run(()=>{
            try{
                string folder=OutputDirectory;
                Directory.CreateDirectory(folder);
                string file=Path.Combine(folder,$"HazardPay-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid().ToString("N")[..6]}.png");
                using(var stream=new FileStream(file,FileMode.CreateNew))WritePng(stream,bottomUpRgba,width,height);
                Notify("Screenshot saved: "+folder);
            }catch(Exception e){Notify("Screenshot failed: "+e.Message);}
            finally{Interlocked.Exchange(ref busy,0);}
        });
    }
    private static void Notify(string message){LastMessage=message;MessageUntilUtc=DateTime.UtcNow.AddSeconds(5);}
    internal static void WritePng(Stream output,byte[] rgba,int width,int height)
    {
        if(width<1||height<1||rgba.Length!=checked(width*height*4))throw new ArgumentException("Invalid screenshot dimensions.");
        output.Write(new byte[]{137,80,78,71,13,10,26,10});
        var header=new byte[13];BinaryPrimitives.WriteInt32BigEndian(header,width);BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4),height);header[8]=8;header[9]=6;
        Chunk(output,"IHDR",header);
        using var compressed=new MemoryStream();
        using(var z=new ZLibStream(compressed,CompressionLevel.Fastest,true))
            for(int y=height-1;y>=0;y--){z.WriteByte(0);for(int x=0;x<width;x++)rgba[(y*width+x)*4+3]=255;z.Write(rgba,y*width*4,width*4);}
        Chunk(output,"IDAT",compressed.ToArray());Chunk(output,"IEND",Array.Empty<byte>());
    }
    private static void Chunk(Stream output,string type,byte[] data)
    {
        Span<byte> number=stackalloc byte[4];BinaryPrimitives.WriteInt32BigEndian(number,data.Length);output.Write(number);
        byte[] name=System.Text.Encoding.ASCII.GetBytes(type);output.Write(name);output.Write(data);
        uint crc=0xffffffff;
        foreach(byte b in name)Update(b);
        foreach(byte b in data)Update(b);
        BinaryPrimitives.WriteUInt32BigEndian(number,~crc);output.Write(number);
        void Update(byte b){crc^=b;for(int k=0;k<8;k++)crc=(crc&1)!=0?0xedb88320^(crc>>1):crc>>1;}
    }
}
