using System.Numerics;
using ByteEngine.Core.Graphics.ThreeD;

static class MatrixPackingChecks
{
    public static void Run()
    {
        var material = new Material();
        RenderSubmission Draw(Matrix4x4 matrix, int index, bool shadow = true,
            RenderQueue3D queue = RenderQueue3D.Opaque, float distance = 0) =>
            new(null!, material, matrix, default, queue, true, shadow, true, distance, index);
        var matrix = new Matrix4x4(1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16);
        var draws = new[] { Draw(matrix, 0), Draw(Matrix4x4.CreateTranslation(20,30,40), 1, false),
            Draw(Matrix4x4.Identity, 2, true, RenderQueue3D.Transparent) };
        var packed = BrowserMatrixPacking.Instances(draws);
        if (packed.Length != 48 || !packed.AsSpan(0,16).SequenceEqual(Enumerable.Range(1,16).Select(i => (float)i).ToArray()) ||
            packed[28] != 20 || packed[29] != 30 || packed[30] != 40)
            throw new Exception("Browser instance matrix packing changed layout or instance order");
        if (!BrowserMatrixPacking.Instances(draws, true).SequenceEqual(packed.Take(16)))
            throw new Exception("Shadow packing included non-casters or transparent draws");
        if (BrowserMatrixPacking.Instances([]).Length != 0) throw new Exception("Empty instance packing");
        material.WetContact0=new(1,2,3,4);material.WetLevels=new(.1f,.2f,.3f,.4f);
        var packet=new byte[BrowserMatrixPacking.PacketLength(draws)];BrowserMatrixPacking.WritePacket(packet,draws);
        var fields=System.Runtime.InteropServices.MemoryMarshal.Cast<byte,float>(packet.AsSpan(8));
        if(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(packet)!=3||System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(4))!=1||
            !fields[..16].SequenceEqual(packed.AsSpan(0,16))||!fields.Slice(32,48).SequenceEqual(packed)||!fields.Slice(80,16).SequenceEqual(packed.AsSpan(0,16))||fields[19]!=4||fields[31]!=.4f)
            throw new Exception("Binary draw packet changed model/wet/instance/shadow layout");
        var single=new[]{draws[1]};packet=new byte[BrowserMatrixPacking.PacketLength(single)];BrowserMatrixPacking.WritePacket(packet,single);
        if(packet.Length!=136||System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(packet)!=0)throw new Exception("Single non-caster packet contains redundant instances");
        var unsorted = new[] { Draw(matrix, 4, queue:RenderQueue3D.Transparent, distance:5),
            Draw(matrix, 3), Draw(matrix, 1, queue:RenderQueue3D.Transparent, distance:10), Draw(matrix, 2) };
        var expected = unsorted.OrderBy(s=>s.Material.WaterSurface?3500:(int)s.Queue)
            .ThenBy(s=>s.Queue==RenderQueue3D.Transparent?-s.DistanceSquaredToCamera:s.SubmissionIndex).ToArray();
        Array.Sort(unsorted, BrowserMatrixPacking.Compare);
        if (!unsorted.SequenceEqual(expected)) throw new Exception("Browser visibility ordering differs from previous stable order");
        Console.WriteLine("PASS: instance matrix layout, shadow filtering and stable transparent draw order");
    }
}
