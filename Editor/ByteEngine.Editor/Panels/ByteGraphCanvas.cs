using System.Numerics;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class ByteGraphCanvas
{
    private const float MinimumZoom =
        0.25f;

    private const float MaximumZoom =
        2.00f;

    private const float GridSize =
        64.0f;

    private Vector2 _origin;

    private Vector2 _size;

    public Vector2 Pan { get; private set; } =
        new(
            90.0f,
            90.0f);

    public float Zoom { get; private set; } =
        1.0f;

    public Vector2 Size =>
        _size;

    public Vector2 Origin =>
        _origin;

    public bool BackgroundHovered { get; private set; }

    public bool IsMouseInsideCanvas
    {
        get
        {
            Vector2 mouse =
                ImGui.GetIO().MousePos;

            return mouse.X >=
                       _origin.X &&
                   mouse.Y >=
                       _origin.Y &&
                   mouse.X <=
                       _origin.X +
                       _size.X &&
                   mouse.Y <=
                       _origin.Y +
                       _size.Y;
        }
    }

    public Vector2 MouseGraphPosition =>
        ScreenToGraph(
            ImGui.GetIO().MousePos);

    public bool Begin(
        string id)
    {
        Vector2 available =
            ImGui.GetContentRegionAvail();

        available.X =
            Math.Max(
                available.X,
                1.0f);

        available.Y =
            Math.Max(
                available.Y,
                1.0f);

        ImGui.PushStyleColor(ImGuiCol.ChildBg, EditorTheme.Background);

        bool visible =
            ImGui.BeginChild(
                id,
                available,
                ImGuiChildFlags.Borders,
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse);

        ImGui.PopStyleColor();

        _origin =
            ImGui.GetCursorScreenPos();

        _size =
            ImGui.GetContentRegionAvail();

        _size.X =
            Math.Max(
                _size.X,
                1.0f);

        _size.Y =
            Math.Max(
                _size.Y,
                1.0f);

        /*
         * IMPORTANT:
         *
         * Do NOT create a full-canvas InvisibleButton here.
         *
         * The previous ByteGraph version used one giant InvisibleButton
         * underneath the graph. In Dear ImGui that item could interfere
         * with overlapping node child-windows and popup/context input.
         * That is why node selection and the right-click graph menu could
         * feel dead even though the nodes were visible.
         *
         * Canvas hit-testing is now done directly from the canvas bounds.
         */
        BackgroundHovered =
            IsMouseInsideCanvas;

        ImGuiIOPtr io =
            ImGui.GetIO();

        if (IsMouseInsideCanvas &&
            ImGui.IsMouseDragging(
                ImGuiMouseButton.Middle))
        {
            Pan +=
                io.MouseDelta;
        }

        if (IsMouseInsideCanvas &&
            io.MouseWheel !=
            0.0f)
        {
            Vector2 mouse =
                io.MousePos;

            Vector2 graphPoint =
                ScreenToGraph(
                    mouse);

            float nextZoom =
                Math.Clamp(
                    Zoom +
                    io.MouseWheel *
                    0.08f,
                    MinimumZoom,
                    MaximumZoom);

            if (Math.Abs(
                    nextZoom -
                    Zoom) >
                0.0001f)
            {
                Zoom =
                    nextZoom;

                Pan =
                    mouse -
                    _origin -
                    graphPoint *
                    Zoom;
            }
        }

        DrawGrid();

        return visible;
    }

    public void End()
    {
        ImGui.EndChild();
    }

    public void ResetView()
    {
        Pan =
            new Vector2(
                90.0f,
                90.0f);

        Zoom =
            1.0f;
    }

    public void SetZoom(
        float zoom)
    {
        Zoom =
            Math.Clamp(
                zoom,
                MinimumZoom,
                MaximumZoom);
    }

    public void FrameBounds(
        Vector2 minimum,
        Vector2 maximum)
    {
        Vector2 bounds =
            maximum -
            minimum;

        bounds.X =
            Math.Max(
                bounds.X,
                1.0f);

        bounds.Y =
            Math.Max(
                bounds.Y,
                1.0f);

        const float margin =
            90.0f;

        float usableWidth =
            Math.Max(
                _size.X -
                margin *
                2.0f,
                1.0f);

        float usableHeight =
            Math.Max(
                _size.Y -
                margin *
                2.0f,
                1.0f);

        float fitZoom =
            Math.Min(
                usableWidth /
                bounds.X,
                usableHeight /
                bounds.Y);

        Zoom =
            Math.Clamp(
                fitZoom,
                MinimumZoom,
                MaximumZoom);

        Vector2 center =
            (
                minimum +
                maximum
            ) *
            0.5f;

        Pan =
            _size *
            0.5f -
            center *
            Zoom;
    }

    public Vector2 ToScreen(
        Vector2 graphPosition)
    {
        return _origin +
               Pan +
               graphPosition *
               Zoom;
    }

    public Vector2 ScreenToGraph(
        Vector2 screenPosition)
    {
        return (
                   screenPosition -
                   _origin -
                   Pan
               ) /
               Math.Max(
                   Zoom,
                   0.0001f);
    }

    public Vector2 ScaleSize(
        Vector2 logicalSize)
    {
        return logicalSize *
               Zoom;
    }

    public Vector2 ScreenDeltaToGraph(
        Vector2 delta)
    {
        return delta /
               Math.Max(
                   Zoom,
                   0.0001f);
    }

    public bool IsPointHovered(
        Vector2 graphPosition,
        float radius = 11.0f)
    {
        Vector2 delta =
            ImGui.GetIO().MousePos -
            ToScreen(
                graphPosition);

        float screenRadius =
            Math.Max(
                radius *
                Zoom,
                7.0f);

        return delta.LengthSquared() <=
               screenRadius *
               screenRadius;
    }

    public void DrawWire(
        Vector2 graphStart,
        Vector2 graphEnd,
        Vector4 color,
        float thickness = 3.0f)
    {
        Vector2 start =
            ToScreen(
                graphStart);

        Vector2 end =
            ToScreen(
                graphEnd);

        float horizontalDistance =
            Math.Abs(
                end.X -
                start.X);

        float handle =
            Math.Max(
                70.0f *
                Zoom,
                horizontalDistance *
                0.45f);

        Vector2 controlA =
            start +
            new Vector2(
                handle,
                0.0f);

        Vector2 controlB =
            end -
            new Vector2(
                handle,
                0.0f);

        ImDrawListPtr drawList =
            ImGui.GetWindowDrawList();

        uint packedColor =
            ImGui.GetColorU32(
                color);

        Vector2 previous =
            start;

        const int segments =
            24;

        for (int index = 1;
             index <=
             segments;
             index++)
        {
            float t =
                index /
                (float)segments;

            float oneMinusT =
                1.0f -
                t;

            Vector2 point =
                oneMinusT *
                oneMinusT *
                oneMinusT *
                start +
                3.0f *
                oneMinusT *
                oneMinusT *
                t *
                controlA +
                3.0f *
                oneMinusT *
                t *
                t *
                controlB +
                t *
                t *
                t *
                end;

            drawList.AddLine(
                previous,
                point,
                packedColor,
                Math.Max(
                    thickness *
                    Zoom,
                    1.0f));

            previous =
                point;
        }
    }

    public void DrawPin(
        Vector2 graphPosition,
        Vector4 color,
        float radius = 6.0f)
    {
        Vector2 center = ToScreen(graphPosition);
        float screenRadius = Math.Max(radius * Zoom, 3.0f);
        ImDrawListPtr drawList = ImGui.GetWindowDrawList();
        drawList.AddCircleFilled(center, screenRadius + Math.Max(1.5f * Zoom, 1.0f),
            ImGui.GetColorU32(EditorTheme.Border));
        drawList.AddCircleFilled(center, screenRadius, ImGui.GetColorU32(color));
    }

    public void DrawSelectionRectangle(
        Vector2 graphA,
        Vector2 graphB)
    {
        Vector2 a =
            ToScreen(
                graphA);

        Vector2 b =
            ToScreen(
                graphB);

        Vector2 minimum =
            new(
                Math.Min(
                    a.X,
                    b.X),
                Math.Min(
                    a.Y,
                    b.Y));

        Vector2 maximum =
            new(
                Math.Max(
                    a.X,
                    b.X),
                Math.Max(
                    a.Y,
                    b.Y));

        ImDrawListPtr drawList =
            ImGui.GetWindowDrawList();

        drawList.AddRectFilled(
            minimum,
            maximum,
            ImGui.GetColorU32(
                EditorTheme.Selection with { W = 0.12f }));

        drawList.AddRect(
            minimum,
            maximum,
            ImGui.GetColorU32(
                EditorTheme.AccentHover with { W = 0.95f }),
            0.0f,
            ImDrawFlags.None,
            1.5f);
    }

    public void DrawGroupBox(
        Vector2 graphMinimum,
        Vector2 graphMaximum,
        Vector4 fillColor,
        Vector4 borderColor)
    {
        Vector2 minimum =
            ToScreen(
                graphMinimum);

        Vector2 maximum =
            ToScreen(
                graphMaximum);

        ImDrawListPtr drawList =
            ImGui.GetWindowDrawList();

        drawList.AddRectFilled(
            minimum,
            maximum,
            ImGui.GetColorU32(
                fillColor),
            8.0f *
            Zoom);

        drawList.AddRect(
            minimum,
            maximum,
            ImGui.GetColorU32(
                borderColor),
            8.0f *
            Zoom,
            ImDrawFlags.None,
            Math.Max(
                2.0f *
                Zoom,
                1.0f));
    }

    private void DrawGrid()
    {
        ImDrawListPtr drawList =
            ImGui.GetWindowDrawList();

        float spacing =
            GridSize *
            Zoom;

        if (spacing <
            8.0f)
        {
            spacing *=
                MathF.Ceiling(
                    8.0f /
                    Math.Max(
                        spacing,
                        0.001f));
        }

        float startX =
            Pan.X %
            spacing;

        float startY =
            Pan.Y %
            spacing;

        uint minorColor =
            ImGui.GetColorU32(
                EditorTheme.Border with { W = 0.38f });

        uint axisColor =
            ImGui.GetColorU32(
                EditorTheme.TextMuted with { W = 0.34f });

        for (float x =
                 startX;
             x <
             _size.X;
             x +=
             spacing)
        {
            drawList.AddLine(
                _origin +
                new Vector2(
                    x,
                    0.0f),
                _origin +
                new Vector2(
                    x,
                    _size.Y),
                minorColor,
                1.0f);
        }

        for (float y =
                 startY;
             y <
             _size.Y;
             y +=
             spacing)
        {
            drawList.AddLine(
                _origin +
                new Vector2(
                    0.0f,
                    y),
                _origin +
                new Vector2(
                    _size.X,
                    y),
                minorColor,
                1.0f);
        }

        Vector2 zero =
            ToScreen(
                Vector2.Zero);

        if (zero.X >=
                _origin.X &&
            zero.X <=
                _origin.X +
                _size.X)
        {
            drawList.AddLine(
                new Vector2(
                    zero.X,
                    _origin.Y),
                new Vector2(
                    zero.X,
                    _origin.Y +
                    _size.Y),
                axisColor,
                1.5f);
        }

        if (zero.Y >=
                _origin.Y &&
            zero.Y <=
                _origin.Y +
                _size.Y)
        {
            drawList.AddLine(
                new Vector2(
                    _origin.X,
                    zero.Y),
                new Vector2(
                    _origin.X +
                    _size.X,
                    zero.Y),
                axisColor,
                1.5f);
        }
    }
}
