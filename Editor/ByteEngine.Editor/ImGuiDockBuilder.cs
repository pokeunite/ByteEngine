using System.Numerics;
using System.Runtime.InteropServices;

using ImGuiNET;

namespace ByteEngine.Editor;

internal static class ImGuiDockBuilder
{
    private const ImGuiDockNodeFlags DockSpaceNodeFlag =
        (ImGuiDockNodeFlags)(1 << 10);

    public static bool NodeExists(
        uint nodeId)
    {
        return GetNode(nodeId) !=
               IntPtr.Zero;
    }

    public static void BuildDefaultLayout(
        uint dockSpaceId,
        Vector2 size)
    {
        RemoveNode(
            dockSpaceId
        );

        AddNode(
            dockSpaceId,
            DockSpaceNodeFlag
        );

        SetNodeSize(
            dockSpaceId,
            size
        );

        SplitNode(
            dockSpaceId,
            ImGuiDir.Left,
            0.20f,
            out uint leftColumn,
            out uint centerAndRight
        );

        SplitNode(
            centerAndRight,
            ImGuiDir.Right,
            0.24f,
            out uint rightColumn,
            out uint centerColumn
        );

        SplitNode(
            leftColumn,
            ImGuiDir.Down,
            0.28f,
            out uint assetsPanel,
            out uint hierarchyPanel
        );

        SplitNode(
            centerColumn,
            ImGuiDir.Down,
            0.28f,
            out uint consolePanel,
            out uint sceneViewPanel
        );

        DockWindow(
            "Hierarchy",
            hierarchyPanel
        );

        DockWindow(
            "Assets",
            assetsPanel
        );

        DockWindow(
            "Scene View",
            sceneViewPanel
        );

        DockWindow(
            "Game View",
            sceneViewPanel
        );

        DockWindow(
            "Console",
            consolePanel
        );

        DockWindow(
            "Inspector",
            rightColumn
        );

        Finish(
            dockSpaceId
        );
    }

    [DllImport(
        "cimgui",
        EntryPoint = "igDockBuilderGetNode",
        CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr GetNode(
        uint nodeId);

    [DllImport(
        "cimgui",
        EntryPoint = "igDockBuilderRemoveNode",
        CallingConvention = CallingConvention.Cdecl)]
    private static extern void RemoveNode(
        uint nodeId);

    [DllImport(
        "cimgui",
        EntryPoint = "igDockBuilderAddNode",
        CallingConvention = CallingConvention.Cdecl)]
    private static extern uint AddNode(
        uint nodeId,
        ImGuiDockNodeFlags flags);

    [DllImport(
        "cimgui",
        EntryPoint = "igDockBuilderSetNodeSize",
        CallingConvention = CallingConvention.Cdecl)]
    private static extern void SetNodeSize(
        uint nodeId,
        Vector2 size);

    [DllImport(
        "cimgui",
        EntryPoint = "igDockBuilderSplitNode",
        CallingConvention = CallingConvention.Cdecl)]
    private static extern uint SplitNode(
        uint nodeId,
        ImGuiDir direction,
        float ratio,
        out uint nodeAtDirection,
        out uint nodeAtOppositeDirection);

    [DllImport(
        "cimgui",
        EntryPoint = "igDockBuilderDockWindow",
        CallingConvention = CallingConvention.Cdecl)]
    private static extern void DockWindow(
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        string windowName,
        uint nodeId);

    [DllImport(
        "cimgui",
        EntryPoint = "igDockBuilderFinish",
        CallingConvention = CallingConvention.Cdecl)]
    private static extern void Finish(
        uint nodeId);
}
