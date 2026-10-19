using System.Numerics;
using System.Buffers.Binary;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Tests;
internal static class BrowserDrawPacketTests
{
    public static void Run(string directory)
    {
        Directory.CreateDirectory(directory);
        float[] matrix = [1,0,0,0,0,1,0,0,0,0,1,0,3,4,5,1];
        BrowserDrawCommand Command() => new() { id=7,material=12,model=matrix,wet=new float[16],receiveShadows=true,
            instances=[..matrix,..matrix],shadowInstances=matrix };
        var a=Command();var b=Command();b.id=9;b.instances=[];b.shadowInstances=[];b.receiveShadows=false;
        b.water=BrowserWaterParameters.Capture(new Material{WaterSurface=true,WaterColumn=new Vector4(3,1,.8f,0),WaterSize=new(5,6),WaterReflections=true},23);
        var bytes=BrowserDrawPacket.Encode([a,b]);
        var reused=new byte[BrowserDrawPacket.EncodedSize([a,b])];BrowserDrawPacket.Encode([a,b],reused);
        if(!bytes.SequenceEqual(reused))throw new Exception("Reused draw destination changed packet contents");
        var pool=new FrameArrayPool<byte>(128,4);var first=pool.Rent(64);var second=pool.Rent(64);
        if(ReferenceEquals(first,second))throw new Exception("Live frame buffers alias");
        first[0]=13;pool.BeginFrame();var next=pool.Rent(64);if(!ReferenceEquals(next,first)&&!ReferenceEquals(next,second))throw new Exception("Exact-length buffers were not reused");
        for(int i=0;i<20;i++)pool.Rent(1000);pool.BeginFrame();if(pool.RetainedElements>128)throw new Exception("Frame buffer pool exceeded its memory bound");
        var stable=new FrameArrayPool<float>(1024);stable.Rent(16);stable.BeginFrame();long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<1000;i++){stable.Rent(16);stable.BeginFrame();}
        if(GC.GetAllocatedBytesForCurrentThread()-before>1024)throw new Exception("Stable frame buffer reuse allocates");
        var startup=new FrameArrayPool<byte>(128);startup.Rent(128);startup.BeginFrame();var gameplay=startup.Rent(32);startup.BeginFrame();
        if(!ReferenceEquals(gameplay,startup.Rent(32)))throw new Exception("Unused startup buffers starved steady gameplay reuse");
        if(BinaryPrimitives.ReadInt32LittleEndian(bytes)!=BrowserDrawPacket.Magic||bytes.Length!=684)throw new Exception("Draw packet layout changed unexpectedly");
        File.WriteAllBytes(Path.Combine(directory,"draw-packet.bin"),bytes);
        if(BrowserDrawPacket.Encode([]).Length!=8)throw new Exception("Empty draw frame is invalid");
        a.instances=[1];bool rejected=false;try{BrowserDrawPacket.Encode([a]);}catch(InvalidOperationException){rejected=true;}
        if(!rejected)throw new Exception("Incomplete instance accepted");
        Console.WriteLine("PASS Binary draw producer: water, instances, shadows, empty frame and invalid dimensions.");
    }
}

