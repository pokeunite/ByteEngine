using ByteEngine.Core;
using ImGuiNET;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

namespace ByteEngine.Editor;

/// <summary>Shared complete keyboard mapping for ImGui-backed editor windows, including detached event sheets.</summary>
internal static class EditorKeyCapture
{
    internal static ImGuiKey ToImGui(Key key)
    {
        string name = key.ToString();
        name = key switch
        {
            Key.Up => "UpArrow", Key.Down => "DownArrow", Key.Left => "LeftArrow", Key.Right => "RightArrow",
            Key.LeftControl => "LeftCtrl", Key.RightControl => "RightCtrl",
            Key.GraveAccent => "GraveAccent", _ => name
        };
        if (name.Length == 2 && name[0] == 'D' && char.IsDigit(name[1])) name = "_" + name[1];
        if (name.StartsWith("KeyPad", StringComparison.Ordinal)) name = "Keypad" + name[6..];
        return Enum.TryParse<ImGuiKey>(name, out var mapped) ? mapped : ImGuiKey.None;
    }

    internal static bool TryGetPressed(out Key key)
    {
        foreach (var candidate in Enum.GetValues<Key>())
        {
            var mapped = ToImGui(candidate);
            if (mapped != ImGuiKey.None && ImGui.IsKeyPressed(mapped, false)) { key = candidate; return true; }
        }
        key = default; return false;
    }

    internal static (Keys OpenTk, ImGuiKey ImGui)[] Mappings => Enum.GetValues<Key>()
        .Select(key => (OpenTk: Enum.Parse<Keys>(key.ToString()), ImGui: ToImGui(key)))
        .Where(pair => pair.ImGui != ImGuiKey.None).ToArray();
}
