using System.Numerics;

using ImGuiNET;

namespace ByteEngine.Editor;

internal static class EditorTheme
{
    public static readonly Vector4 Accent =
        new(0.26f, 0.55f, 0.84f, 1.0f);

    public static readonly Vector4 AccentHover =
        new(0.32f, 0.63f, 0.94f, 1.0f);

    public static readonly Vector4 Panel =
        new(0.105f, 0.120f, 0.145f, 1.0f);

    public static readonly Vector4 PanelRaised =
        new(0.135f, 0.155f, 0.185f, 1.0f);

    public static readonly Vector4 PanelHovered =
        new(0.170f, 0.195f, 0.235f, 1.0f);

    public static readonly Vector4 Border =
        new(0.205f, 0.230f, 0.270f, 1.0f);

    public static readonly Vector4 Text =
        new(0.88f, 0.91f, 0.95f, 1.0f);

    public static readonly Vector4 TextMuted =
        new(0.50f, 0.55f, 0.62f, 1.0f);
    public static readonly Vector4 Warning =
        new(0.96f, 0.72f, 0.25f, 1.0f);

    public static readonly Vector4 Error =
        new(0.96f, 0.34f, 0.30f, 1.0f);

    public const float SpaceXs = 4.0f;
    public const float SpaceS = 8.0f;
    public const float SpaceM = 12.0f;
    public const float SpaceL = 18.0f;

    public static void ApplyGodotInspired()
    {
        ImGuiStylePtr style =
            ImGui.GetStyle();

        style.WindowRounding = 2.0f;
        style.ChildRounding = 3.0f;
        style.FrameRounding = 4.0f;
        style.PopupRounding = 4.0f;
        style.GrabRounding = 4.0f;
        style.TabRounding = 4.0f;

        style.WindowPadding = new Vector2(10.0f, 9.0f);
        style.FramePadding = new Vector2(9.0f, 6.0f);
        style.ItemSpacing = new Vector2(8.0f, 7.0f);
        style.ItemInnerSpacing = new Vector2(7.0f, 5.0f);

        style.IndentSpacing = 18.0f;
        style.ScrollbarSize = 12.0f;
        style.GrabMinSize = 10.0f;
        style.WindowBorderSize = 1.0f;
        style.ChildBorderSize = 1.0f;
        style.PopupBorderSize = 1.0f;

        style.Colors[(int)ImGuiCol.Text] = Text;
        style.Colors[(int)ImGuiCol.TextDisabled] = TextMuted;
        style.Colors[(int)ImGuiCol.WindowBg] = new Vector4(0.090f, 0.103f, 0.125f, 1.0f);
        style.Colors[(int)ImGuiCol.ChildBg] = Panel;
        style.Colors[(int)ImGuiCol.PopupBg] = new Vector4(0.090f, 0.105f, 0.130f, 0.98f);
        style.Colors[(int)ImGuiCol.Border] = Border;
        style.Colors[(int)ImGuiCol.BorderShadow] = Vector4.Zero;
        style.Colors[(int)ImGuiCol.FrameBg] = new Vector4(0.130f, 0.150f, 0.180f, 1.0f);
        style.Colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.170f, 0.195f, 0.235f, 1.0f);
        style.Colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.190f, 0.225f, 0.275f, 1.0f);
        style.Colors[(int)ImGuiCol.TitleBg] = new Vector4(0.070f, 0.082f, 0.100f, 1.0f);
        style.Colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.105f, 0.135f, 0.175f, 1.0f);
        style.Colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.070f, 0.082f, 0.100f, 1.0f);
        style.Colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.065f, 0.078f, 0.098f, 1.0f);
        style.Colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.075f, 0.088f, 0.108f, 1.0f);
        style.Colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.220f, 0.245f, 0.285f, 0.75f);
        style.Colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.280f, 0.320f, 0.370f, 0.90f);
        style.Colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.320f, 0.370f, 0.430f, 1.0f);
        style.Colors[(int)ImGuiCol.CheckMark] = Accent;
        style.Colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.26f, 0.55f, 0.84f, 0.78f);
        style.Colors[(int)ImGuiCol.SliderGrabActive] = AccentHover;
        style.Colors[(int)ImGuiCol.Button] = new Vector4(0.150f, 0.175f, 0.210f, 1.0f);
        style.Colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.205f, 0.285f, 0.370f, 1.0f);
        style.Colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.230f, 0.390f, 0.560f, 1.0f);
        style.Colors[(int)ImGuiCol.Header] = new Vector4(0.155f, 0.180f, 0.215f, 0.85f);
        style.Colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.205f, 0.315f, 0.430f, 0.95f);
        style.Colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.235f, 0.430f, 0.650f, 1.0f);
        style.Colors[(int)ImGuiCol.Separator] = Border;
        style.Colors[(int)ImGuiCol.SeparatorHovered] = new Vector4(0.26f, 0.55f, 0.84f, 0.80f);
        style.Colors[(int)ImGuiCol.SeparatorActive] = AccentHover;
        style.Colors[(int)ImGuiCol.ResizeGrip] = new Vector4(0.26f, 0.55f, 0.84f, 0.35f);
        style.Colors[(int)ImGuiCol.ResizeGripHovered] = new Vector4(0.32f, 0.63f, 0.94f, 0.70f);
        style.Colors[(int)ImGuiCol.ResizeGripActive] = AccentHover;
        style.Colors[(int)ImGuiCol.Tab] = new Vector4(0.090f, 0.105f, 0.130f, 1.0f);
        style.Colors[(int)ImGuiCol.TabHovered] = new Vector4(0.190f, 0.285f, 0.390f, 1.0f);
        style.Colors[(int)ImGuiCol.TabSelected] = new Vector4(0.145f, 0.235f, 0.335f, 1.0f);
        style.Colors[(int)ImGuiCol.TabDimmed] = new Vector4(0.068f, 0.080f, 0.100f, 1.0f);
        style.Colors[(int)ImGuiCol.TabDimmedSelected] = new Vector4(0.105f, 0.135f, 0.175f, 1.0f);
        style.Colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.26f, 0.55f, 0.84f, 0.30f);
        style.Colors[(int)ImGuiCol.DragDropTarget] = AccentHover;
        style.Colors[(int)ImGuiCol.ModalWindowDimBg] = new Vector4(0.0f, 0.0f, 0.0f, 0.55f);
    }
}

