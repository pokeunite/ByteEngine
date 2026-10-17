using System.Buffers.Binary;
using System.Runtime.InteropServices;

/// <summary>Little-endian draw records. Large matrix arrays never pass through JSON strings.</summary>
internal static class BrowserDrawPacket
{
    public const int Magic = 0x31445742; // BWD1
    public static byte[] Encode(IReadOnlyList<BrowserDrawCommand> commands)
    {
        int size = 8;
        foreach (var c in commands)
            size = checked(size + 148 + (c.water != null ? 188 : 0) + (c.instances.Length + c.shadowInstances.Length) * 4);
        var result = new byte[size];
        var w = new Writer(result);
        w.Int(Magic); w.Int(commands.Count);
        foreach (var c in commands)
        {
            var water = c.water;
            int flags = (c.receiveShadows ? 1 : 0) | (water != null ? 2 : 0) |
                (water?.surface == true ? 4 : 0) | (water?.reflections == true ? 8 : 0);
            if (c.instances.Length % 16 != 0 || c.shadowInstances.Length % 16 != 0)
                throw new InvalidOperationException("Instance records must contain complete matrices.");
            w.Int(c.id); w.Int(c.material); w.Int(flags); w.Int(c.shadowInstances.Length); w.Int(c.instances.Length);
            w.Floats(c.model, 16); w.Floats(c.wet, 16);
            if (water != null)
            {
                w.Int(water.depthTexture); w.Floats(water.size, 2);
                w.Floats(water.column, 4); w.Floats(water.motion, 4); w.Floats(water.flow, 4);
                w.Floats(water.impact, 4); w.Floats(water.wake, 4); w.Floats(water.absorption, 4);
                w.Floats(water.scattering, 4); w.Floats(water.style, 4); w.Floats(water.waves, 4);
                w.Floats(water.depthRect, 4); w.Floats(water.depthSettings, 4);
            }
            w.Floats(c.shadowInstances, c.shadowInstances.Length); w.Floats(c.instances, c.instances.Length);
        }
        return result;
    }
    private ref struct Writer(Span<byte> destination)
    {
        private Span<byte> remaining = destination;
        public void Int(int value) { BinaryPrimitives.WriteInt32LittleEndian(remaining, value); remaining = remaining[4..]; }
        public void Floats(float[] values, int count)
        {
            if (values.Length != count) throw new InvalidOperationException("Invalid draw record dimensions.");
            if (BitConverter.IsLittleEndian) MemoryMarshal.AsBytes(values.AsSpan()).CopyTo(remaining);
            else for (int i = 0; i < count; i++) BinaryPrimitives.WriteSingleLittleEndian(remaining[(i * 4)..], values[i]);
            remaining = remaining[(count * 4)..];
        }
    }
}
