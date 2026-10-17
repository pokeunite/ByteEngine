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
        if(BinaryPrimitives.ReadInt32LittleEndian(bytes)!=BrowserDrawPacket.Magic||bytes.Length!=684)throw new Exception("Draw packet layout changed unexpectedly");
        File.WriteAllBytes(Path.Combine(directory,"draw-packet.bin"),bytes);
        if(BrowserDrawPacket.Encode([]).Length!=8)throw new Exception("Empty draw frame is invalid");
        a.instances=[1];bool rejected=false;try{BrowserDrawPacket.Encode([a]);}catch(InvalidOperationException){rejected=true;}
        if(!rejected)throw new Exception("Incomplete instance accepted");
        Console.WriteLine("PASS Binary draw producer: water, instances, shadows, empty frame and invalid dimensions.");
    }
}

