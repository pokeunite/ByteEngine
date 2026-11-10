using System.Buffers.Binary;
using System.Numerics;

namespace ByteEngine.Core.Net;

/// <summary>Player intent. The host derives position and combat results; clients never submit them.</summary>
public readonly record struct PlayerInput(uint Tick, uint Sequence, Vector2 Move, Vector2 Look, uint Buttons);

/// <summary>Explicit, little-endian wire format shared by native and browser peers.</summary>
public static class NetworkInputCodec
{
    public const int PacketSize = 36;
    public const ushort ProtocolVersion = 1;
    private const uint Magic = 0x314E4542; // BEN1
    public static bool IsNewer(uint value, uint previous) => unchecked((int)(value - previous)) > 0;

    public static bool TryWrite(in PlayerInput input, Span<byte> target)
    {
        if (target.Length < PacketSize || !Valid(input)) return false;
        BinaryPrimitives.WriteUInt32LittleEndian(target, Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(target[4..], ProtocolVersion);
        target[6] = 1; // input packet
        target[7] = 0; // reserved
        BinaryPrimitives.WriteUInt32LittleEndian(target[8..], input.Tick);
        BinaryPrimitives.WriteUInt32LittleEndian(target[12..], input.Sequence);
        Write(target[16..], input.Move.X); Write(target[20..], input.Move.Y);
        Write(target[24..], input.Look.X); Write(target[28..], input.Look.Y);
        BinaryPrimitives.WriteUInt32LittleEndian(target[32..], input.Buttons);
        return true;
    }
    public static bool TryRead(ReadOnlySpan<byte> source, out PlayerInput input)
    {
        input = default;
        if (source.Length != PacketSize || BinaryPrimitives.ReadUInt32LittleEndian(source) != Magic ||
            BinaryPrimitives.ReadUInt16LittleEndian(source[4..]) != ProtocolVersion || source[6] != 1 || source[7] != 0) return false;
        input = new(BinaryPrimitives.ReadUInt32LittleEndian(source[8..]), BinaryPrimitives.ReadUInt32LittleEndian(source[12..]),
            new(Read(source[16..]), Read(source[20..])), new(Read(source[24..]), Read(source[28..])), BinaryPrimitives.ReadUInt32LittleEndian(source[32..]));
        if (Valid(input)) return true;
        input = default; return false;
    }
    private static bool Valid(in PlayerInput input) => float.IsFinite(input.Move.X) && float.IsFinite(input.Move.Y) &&
        input.Move.LengthSquared() <= 1.00001f && float.IsFinite(input.Look.X) && float.IsFinite(input.Look.Y) &&
        Math.Abs(input.Look.X) <= MathF.PI && Math.Abs(input.Look.Y) <= MathF.PI / 2;
    private static void Write(Span<byte> bytes, float value) => BinaryPrimitives.WriteInt32LittleEndian(bytes, BitConverter.SingleToInt32Bits(value));
    private static float Read(ReadOnlySpan<byte> bytes) => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes));
}
