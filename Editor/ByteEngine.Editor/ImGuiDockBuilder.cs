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
        return GetNode(
                   nodeId) !=
               IntPtr.Zero;
    }

    public static void BuildDefaultLayout(
        uint dockSpaceId,
        Vector2 size)
    {
        RemoveNode(
            dockSpaceId);

        AddNode(
            dockSpaceId,
            DockSpaceNodeFlag);

        SetNodeSize(
            dockSpaceId,
            size);

        /*
         * Default ByteEngine workspace:
         *
         *  +-----------+---------------------------+-------------+
         *  | Hierarchy |  Scene View | Game View   | Inspector   |
         *  |           |                           |             |
         *  |           +---------------------------+             |
         *  |           | Assets / Console / Perf   |             |
         *  +-----------+
         *
         * This intentionally matches the 3D-first authoring layout and puts
         * Assets in the main bottom workspace instead of the left column.
         */

        SplitNode(
            dockSpaceId,
            ImGuiDir.Left,
            0.11f,
            out uint leftColumn,
            out uint centerAndRight);

        SplitNode(
            centerAndRight,
            ImGuiDir.Right,
            0.23f,
            out uint rightColumn,
            out uint centerColumn);

        SplitNode(
            centerColumn,
            ImGuiDir.Down,
            0.34f,
            out uint bottomWorkspace,
            out uint centralWorkspace);

        DockWindow(
            "Hierarchy",
            leftColumn);

        DockWindow(
            "Game View",
            centralWorkspace);

        DockWindow(
            "Scene View",
            centralWorkspace);

        DockWindow(
            "Inspector",
            rightColumn);

        /*
         * Assets is docked last so it becomes the selected tab in the bottom
         * workspace on a freshly-created/default layout.
         */
        DockWindow(
            "Console",
            bottomWorkspace);

        DockWindow(
            "Performance",
            bottomWorkspace);

        DockWindow(
            "Assets",
            bottomWorkspace);

        Finish(
            dockSpaceId);
    }

    public static void BuildNativeDocumentLayout(
        uint dockSpaceId,
        Vector2 size,
        string mainWindowName,
        string? inspectorWindowName,
        out uint mainNodeId,
        out uint inspectorNodeId)
    {
        RemoveNode(
            dockSpaceId);

        AddNode(
            dockSpaceId,
            DockSpaceNodeFlag);

        SetNodeSize(
            dockSpaceId,
            size);

        mainNodeId =
            dockSpaceId;

        inspectorNodeId =
            0;

        if (!string.IsNullOrWhiteSpace(
                inspectorWindowName))
        {
            SplitNode(
                dockSpaceId,
                ImGuiDir.Right,
                0.24f,
                out inspectorNodeId,
                out mainNodeId);
        }

        DockWindow(
            mainWindowName,
            mainNodeId);

        if (inspectorNodeId != 0 &&
            !string.IsNullOrWhiteSpace(
                inspectorWindowName))
        {
            DockWindow(
                inspectorWindowName,
                inspectorNodeId);
        }

        Finish(
            dockSpaceId);
    }

    [DllImport(
        "cimgui",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "igDockBuilderGetNode")]
    private static extern IntPtr GetNode(
        uint nodeId);

    [DllImport(
        "cimgui",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "igDockBuilderRemoveNode")]
    private static extern void RemoveNode(
        uint nodeId);

    [DllImport(
        "cimgui",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "igDockBuilderAddNode")]
    private static extern uint AddNode(
        uint nodeId,
        ImGuiDockNodeFlags flags);

    [DllImport(
        "cimgui",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "igDockBuilderSetNodeSize")]
    private static extern void SetNodeSize(
        uint nodeId,
        Vector2 size);

    [DllImport(
        "cimgui",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "igDockBuilderSplitNode")]
    private static extern uint SplitNode(
        uint nodeId,
        ImGuiDir splitDirection,
        float sizeRatioForNodeAtDirection,
        out uint outIdAtDirection,
        out uint outIdAtOppositeDirection);

    [DllImport(
        "cimgui",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "igDockBuilderDockWindow")]
    private static extern void DockWindow(
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        string windowName,
        uint nodeId);

    [DllImport(
        "cimgui",
        CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "igDockBuilderFinish")]
    private static extern void Finish(
        uint nodeId);
}
