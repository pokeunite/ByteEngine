using System.Runtime.InteropServices;
using ImGuiNET;

namespace ByteEngine.Editor;

internal static class GameObjectDragDrop
{
    private const string PayloadType = "BYTEENGINE_GAMEOBJECT_GUID";

    public static unsafe void Set(Guid guid)
    {
        Span<byte> bytes = stackalloc byte[16];
        guid.TryWriteBytes(bytes);
        fixed (byte* pointer = bytes) ImGui.SetDragDropPayload(PayloadType, (nint)pointer, 16, ImGuiCond.Once);
    }

    public static unsafe Guid? Accept()
    {
        ImGuiPayloadPtr payload = ImGui.AcceptDragDropPayload(PayloadType);
        if (payload.NativePtr == null) return null;
        if (payload.Data == IntPtr.Zero || payload.DataSize != 16 || !payload.Delivery) return null;
        byte[] bytes = new byte[16];
        Marshal.Copy(payload.Data, bytes, 0, 16);
        return new Guid(bytes);
    }
}
