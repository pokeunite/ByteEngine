using System.Numerics;

using ImGuiNET;

namespace ByteEngine.Editor;

internal static class EditorTheme
{
    public static readonly Vector4 Background = new(0.055f, 0.061f, 0.073f, 1.0f);
    public static readonly Vector4 BackgroundRaised = new(0.071f, 0.079f, 0.094f, 1.0f);
    public static readonly Vector4 Panel = new(0.082f, 0.091f, 0.108f, 1.0f);
    public static readonly Vector4 PanelRaised = new(0.105f, 0.116f, 0.137f, 1.0f);
    public static readonly Vector4 PanelHover = new(0.135f, 0.151f, 0.178f, 1.0f);
    public static readonly Vector4 PanelHovered = PanelHover;

    public static readonly Vector4 Border = new(0.165f, 0.181f, 0.207f, 1.0f);
    public static readonly Vector4 BorderStrong = new(0.235f, 0.260f, 0.298f, 1.0f);

    public static readonly Vector4 Text = new(0.900f, 0.918f, 0.941f, 1.0f);
    public static readonly Vector4 TextSecondary = new(0.695f, 0.727f, 0.773f, 1.0f);
    public static readonly Vector4 TextMuted = new(0.500f, 0.535f, 0.585f, 1.0f);
    public static readonly Vector4 TextDisabled = new(0.365f, 0.392f, 0.430f, 1.0f);

    public static readonly Vector4 Accent = new(0.235f, 0.482f, 0.745f, 1.0f);
    public static readonly Vector4 AccentHover = new(0.290f, 0.565f, 0.850f, 1.0f);
    public static readonly Vector4 AccentActive = new(0.185f, 0.390f, 0.625f, 1.0f);

    public static readonly Vector4 Success = new(0.310f, 0.705f, 0.455f, 1.0f);
    public static readonly Vector4 Warning = new(0.925f, 0.650f, 0.235f, 1.0f);
    public static readonly Vector4 Error = new(0.900f, 0.315f, 0.300f, 1.0f);

    public static readonly Vector4 Selection = new(0.145f, 0.310f, 0.490f, 1.0f);
    public static readonly Vector4 SelectionHover = new(0.180f, 0.375f, 0.580f, 1.0f);
    public static readonly Vector4 ToolbarBackground = new(0.068f, 0.076f, 0.090f, 1.0f);
    public static readonly Vector4 HeaderBackground = new(0.098f, 0.109f, 0.128f, 1.0f);
    public static readonly Vector4 PropertyBackground = new(0.072f, 0.080f, 0.095f, 1.0f);

    public const float XS = 4.0f;
    public const float S = 7.0f;
    public const float M = 10.0f;
    public const float L = 16.0f;
    public const float XL = 24.0f;
    public const float SpaceXs = XS;
    public const float SpaceS = S;
    public const float SpaceM = M;
    public const float SpaceL = L;

    public const float ToolbarHeight = 32.0f;
    public const float StandardControlHeight = 26.0f;
    public const float InspectorLabelWidth = 112.0f;
    public const float SectionSpacing = 12.0f;
    public const float PanelPadding = 9.0f;
    public const float SmallCornerRadius = 3.0f;

    public static void ApplyByteEngine()
    {
        ImGuiStylePtr style = ImGui.GetStyle();
        style.WindowRounding = 2.0f;
        style.ChildRounding = SmallCornerRadius;
        style.FrameRounding = SmallCornerRadius;
        style.PopupRounding = SmallCornerRadius;
        style.GrabRounding = 2.0f;
        style.TabRounding = SmallCornerRadius;
        style.WindowPadding = new Vector2(PanelPadding, PanelPadding);
        style.FramePadding = new Vector2(7.0f, 4.0f);
        style.ItemSpacing = new Vector2(S, 6.0f);
        style.ItemInnerSpacing = new Vector2(6.0f, 4.0f);
        style.IndentSpacing = 17.0f;
        style.ScrollbarSize = 11.0f;
        style.GrabMinSize = 9.0f;
        style.WindowBorderSize = 1.0f;
        style.ChildBorderSize = 1.0f;
        style.PopupBorderSize = 1.0f;

        style.Colors[(int)ImGuiCol.Text] = Text;
        style.Colors[(int)ImGuiCol.TextDisabled] = TextDisabled;
        style.Colors[(int)ImGuiCol.WindowBg] = Background;
        style.Colors[(int)ImGuiCol.ChildBg] = Panel;
        style.Colors[(int)ImGuiCol.PopupBg] = BackgroundRaised with { W = 0.985f };
        style.Colors[(int)ImGuiCol.Border] = Border;
        style.Colors[(int)ImGuiCol.BorderShadow] = Vector4.Zero;
        style.Colors[(int)ImGuiCol.FrameBg] = PanelRaised;
        style.Colors[(int)ImGuiCol.FrameBgHovered] = PanelHover;
        style.Colors[(int)ImGuiCol.FrameBgActive] = Selection;
        style.Colors[(int)ImGuiCol.TitleBg] = Background;
        style.Colors[(int)ImGuiCol.TitleBgActive] = HeaderBackground;
        style.Colors[(int)ImGuiCol.TitleBgCollapsed] = Background;
        style.Colors[(int)ImGuiCol.MenuBarBg] = ToolbarBackground;
        style.Colors[(int)ImGuiCol.ScrollbarBg] = Background;
        style.Colors[(int)ImGuiCol.ScrollbarGrab] = BorderStrong;
        style.Colors[(int)ImGuiCol.ScrollbarGrabHovered] = TextMuted;
        style.Colors[(int)ImGuiCol.ScrollbarGrabActive] = TextSecondary;
        style.Colors[(int)ImGuiCol.CheckMark] = AccentHover;
        style.Colors[(int)ImGuiCol.SliderGrab] = Accent;
        style.Colors[(int)ImGuiCol.SliderGrabActive] = AccentHover;
        style.Colors[(int)ImGuiCol.Button] = PanelRaised;
        style.Colors[(int)ImGuiCol.ButtonHovered] = PanelHover;
        style.Colors[(int)ImGuiCol.ButtonActive] = Selection;
        style.Colors[(int)ImGuiCol.Header] = HeaderBackground;
        style.Colors[(int)ImGuiCol.HeaderHovered] = Selection;
        style.Colors[(int)ImGuiCol.HeaderActive] = SelectionHover;
        style.Colors[(int)ImGuiCol.Separator] = Border;
        style.Colors[(int)ImGuiCol.SeparatorHovered] = Accent;
        style.Colors[(int)ImGuiCol.SeparatorActive] = AccentHover;
        style.Colors[(int)ImGuiCol.ResizeGrip] = Accent with { W = 0.20f };
        style.Colors[(int)ImGuiCol.ResizeGripHovered] = Accent with { W = 0.55f };
        style.Colors[(int)ImGuiCol.ResizeGripActive] = AccentHover;
        style.Colors[(int)ImGuiCol.Tab] = BackgroundRaised;
        style.Colors[(int)ImGuiCol.TabHovered] = SelectionHover;
        style.Colors[(int)ImGuiCol.TabSelected] = Selection;
        style.Colors[(int)ImGuiCol.TabDimmed] = Background;
        style.Colors[(int)ImGuiCol.TabDimmedSelected] = HeaderBackground;
        style.Colors[(int)ImGuiCol.TextSelectedBg] = Accent with { W = 0.32f };
        style.Colors[(int)ImGuiCol.DragDropTarget] = AccentHover;
        style.Colors[(int)ImGuiCol.NavCursor] = AccentHover;
        style.Colors[(int)ImGuiCol.ModalWindowDimBg] = new Vector4(0f, 0f, 0f, 0.58f);
    }

    [Obsolete("Use ApplyByteEngine().")]
    public static void ApplyGodotInspired() => ApplyByteEngine();
}