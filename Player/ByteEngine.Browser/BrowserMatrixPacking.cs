using System.Numerics;
using ByteEngine.Core.Graphics.ThreeD;

internal static class BrowserMatrixPacking
{
    internal static void WriteMatrix(Span<float> target,Matrix4x4 m)
    {
        target[0]=m.M11;target[1]=m.M12;target[2]=m.M13;target[3]=m.M14;
        target[4]=m.M21;target[5]=m.M22;target[6]=m.M23;target[7]=m.M24;
        target[8]=m.M31;target[9]=m.M32;target[10]=m.M33;target[11]=m.M34;
        target[12]=m.M41;target[13]=m.M42;target[14]=m.M43;target[15]=m.M44;
    }
    // Version 1: two int32 counts, model matrix, 4 wet-contact vectors,
    // then visible and opaque shadow instance matrices. No float JSON conversion.
    internal static int PacketLength(IReadOnlyList<RenderSubmission> items)
    {
        int shadows=0;foreach(var item in items)if(item.CastShadows&&item.Queue==RenderQueue3D.Opaque)shadows++;
        return checked(8+4*(32+16*((items.Count>1?items.Count:0)+shadows)));
    }
    internal static void WritePacket(Span<byte> bytes,IReadOnlyList<RenderSubmission> items)
    {
        if(items.Count==0||bytes.Length!=PacketLength(items))throw new ArgumentException("Invalid draw packet destination");
        int instances=items.Count>1?items.Count:0,shadows=0;
        foreach(var item in items)if(item.CastShadows&&item.Queue==RenderQueue3D.Opaque)shadows++;
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes,instances);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes[4..],shadows);
        var floats=System.Runtime.InteropServices.MemoryMarshal.Cast<byte,float>(bytes[8..]);
        WriteMatrix(floats,items[0].ModelMatrix);
        var m=items[0].Material;
        static void V(Span<float> span,Vector4 v){span[0]=v.X;span[1]=v.Y;span[2]=v.Z;span[3]=v.W;}
        V(floats[16..],m.WetContact0);V(floats[20..],m.WetContact1);V(floats[24..],m.WetContact2);V(floats[28..],m.WetLevels);
        int offset=32;if(instances>0)foreach(var item in items){WriteMatrix(floats[offset..],item.ModelMatrix);offset+=16;}
        foreach(var item in items)if(item.CastShadows&&item.Queue==RenderQueue3D.Opaque){WriteMatrix(floats[offset..],item.ModelMatrix);offset+=16;}
    }
    internal static int Compare(RenderSubmission a, RenderSubmission b)
    {
        int queue = (a.Material.WaterSurface ? 3500 : (int)a.Queue)
            .CompareTo(b.Material.WaterSurface ? 3500 : (int)b.Queue);
        if (queue != 0) return queue;
        float left = a.Queue == RenderQueue3D.Transparent ? -a.DistanceSquaredToCamera : a.SubmissionIndex;
        float right = b.Queue == RenderQueue3D.Transparent ? -b.DistanceSquaredToCamera : b.SubmissionIndex;
        int order = left.CompareTo(right);
        return order != 0 ? order : a.SubmissionIndex.CompareTo(b.SubmissionIndex);
    }
    // Avoid generic SelectMany<float> fallback and a temporary array per matrix in WASM.
    internal static float[] Instances(IReadOnlyList<RenderSubmission> items, bool shadowOnly = false)
    {
        static bool Shadow(RenderSubmission item) => item.CastShadows && item.Queue == RenderQueue3D.Opaque;
        int count = items.Count;
        if (shadowOnly) { count = 0; for (int i = 0; i < items.Count; i++) if (Shadow(items[i])) count++; }
        if (count == 0) return [];
        var result = new float[checked(count * 16)];
        int offset = 0;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i]; if (shadowOnly && !Shadow(item)) continue;
            Matrix4x4 m = item.ModelMatrix;
            result[offset++] = m.M11; result[offset++] = m.M12; result[offset++] = m.M13; result[offset++] = m.M14;
            result[offset++] = m.M21; result[offset++] = m.M22; result[offset++] = m.M23; result[offset++] = m.M24;
            result[offset++] = m.M31; result[offset++] = m.M32; result[offset++] = m.M33; result[offset++] = m.M34;
            result[offset++] = m.M41; result[offset++] = m.M42; result[offset++] = m.M43; result[offset++] = m.M44;
        }
        return result;
    }
}
