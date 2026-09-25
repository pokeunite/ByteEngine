using System.Numerics;
using ByteEngine.Core.Animation;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed partial class AnimationProfileWorkspacePanel
{
    private readonly Dictionary<AnimationBlendSpace, int> _selectedBlendSamples = new();
    private AnimationBlendSpace? _draggedBlendSpace;
    private int _draggedBlendSample = -1;
    private int _draggedGraphState = -1;
    private string _selectedGraphState = string.Empty;
    private string _pendingGraphTransition = string.Empty;

    private bool DrawBlendSpaceCanvas(AnimationBlendSpace space)
    {
        bool changed = false;
        float width = Math.Clamp(ImGui.GetContentRegionAvail().X, 320f, 900f);
        float height = space.TwoDimensional ? 360f : 150f;
        Vector2 origin = ImGui.GetCursorScreenPos();
        Vector2 size = new(width, height);
        ImGui.InvisibleButton("##BlendSpaceCanvas", size);
        bool hovered = ImGui.IsItemHovered();
        Vector2 mouse = ImGui.GetMousePos();
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        uint background = ImGui.GetColorU32(EditorTheme.Background);
        uint grid = ImGui.GetColorU32(EditorTheme.Border);
        uint text = ImGui.GetColorU32(EditorTheme.TextMuted);
        uint accent = ImGui.GetColorU32(EditorTheme.AccentHover);
        uint selectedColor = ImGui.GetColorU32(EditorTheme.SelectionHover);
        draw.AddRectFilled(origin, origin + size, background, 4f);
        draw.AddRect(origin, origin + size, grid, 4f);

        const float leftMargin = 45f, rightMargin = 20f, topMargin = 20f, bottomMargin = 30f;
        float left = origin.X + leftMargin, right = origin.X + width - rightMargin;
        float top = origin.Y + topMargin, bottom = origin.Y + height - bottomMargin;
        float spanX = Math.Max(space.MaxX - space.MinX, .0001f);
        float spanY = Math.Max(space.MaxY - space.MinY, .0001f);
        float ToScreenX(float value) => left + (value - space.MinX) / spanX * (right - left);
        float ToScreenY(float value) => space.TwoDimensional
            ? bottom - (value - space.MinY) / spanY * (bottom - top)
            : (top + bottom) * .5f;
        float FromScreenX(float value) => space.MinX + Math.Clamp((value - left) / (right - left), 0f, 1f) * spanX;
        float FromScreenY(float value) => space.MinY + Math.Clamp((bottom - value) / (bottom - top), 0f, 1f) * spanY;

        for (int tick = 0; tick <= 4; tick++)
        {
            float x = left + (right - left) * tick / 4f;
            draw.AddLine(new Vector2(x, top), new Vector2(x, bottom), grid);
            draw.AddText(new Vector2(x - 10f, bottom + 5f), text,
                (space.MinX + spanX * tick / 4f).ToString("0.##"));
            if (!space.TwoDimensional) continue;
            float y = top + (bottom - top) * tick / 4f;
            draw.AddLine(new Vector2(left, y), new Vector2(right, y), grid);
            draw.AddText(new Vector2(origin.X + 4f, y - 7f), text,
                (space.MaxY - spanY * tick / 4f).ToString("0.##"));
        }
        if (space.MinX <= 0f && space.MaxX >= 0f)
            draw.AddLine(new Vector2(ToScreenX(0f), top), new Vector2(ToScreenX(0f), bottom), accent, 1.5f);
        if (space.TwoDimensional && space.MinY <= 0f && space.MaxY >= 0f)
            draw.AddLine(new Vector2(left, ToScreenY(0f)), new Vector2(right, ToScreenY(0f)), accent, 1.5f);
        draw.AddText(origin + new Vector2(5f, 3f), text,
            space.TwoDimensional ? "Click to add; drag points to move. X: Right / Y: Forward"
                                 : "Click to add; drag points to move. X: Speed");

        int nearest = -1;
        float nearestDistance = 13f * 13f;
        for (int i = 0; i < space.Samples.Count; i++)
        {
            AnimationBlendSample sample = space.Samples[i];
            Vector2 point = new(ToScreenX(sample.X), ToScreenY(sample.Y));
            float distance = Vector2.DistanceSquared(point, mouse);
            if (distance < nearestDistance) { nearest = i; nearestDistance = distance; }
            bool selected = _selectedBlendSamples.TryGetValue(space, out int selectedIndex) && selectedIndex == i;
            draw.AddCircleFilled(point, selected ? 9f : 7f, selected ? selectedColor : accent);
            draw.AddCircle(point, selected ? 9f : 7f, background, 0, 2f);
            string label = string.IsNullOrWhiteSpace(sample.Clip) ? "Choose clip" : sample.Clip;
            if (label.Length > 18) label = label[..18] + "...";
            draw.AddText(point + new Vector2(10f, -8f), text, label);
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            if (nearest >= 0)
            {
                _selectedBlendSamples[space] = nearest;
                _draggedBlendSpace = space;
                _draggedBlendSample = nearest;
            }
            else if (mouse.X >= left && mouse.X <= right && mouse.Y >= top && mouse.Y <= bottom)
            {
                string clip = GetAnimationClipNames().FirstOrDefault() ?? string.Empty;
                space.Samples.Add(new AnimationBlendSample
                {
                    Clip = clip, X = FromScreenX(mouse.X),
                    Y = space.TwoDimensional ? FromScreenY(mouse.Y) : 0f
                });
                _selectedBlendSamples[space] = space.Samples.Count - 1;
                _draggedBlendSpace = space;
                _draggedBlendSample = space.Samples.Count - 1;
                changed = true;
            }
        }
        if (ReferenceEquals(_draggedBlendSpace, space) && _draggedBlendSample >= 0 &&
            _draggedBlendSample < space.Samples.Count && ImGui.IsMouseDown(ImGuiMouseButton.Left) &&
            ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            AnimationBlendSample sample = space.Samples[_draggedBlendSample];
            sample.X = FromScreenX(mouse.X);
            if (space.TwoDimensional) sample.Y = FromScreenY(mouse.Y);
            changed = true;
        }
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            _draggedBlendSpace = null;
            _draggedBlendSample = -1;
        }
        if (hovered)
        {
            float probeX = FromScreenX(mouse.X);
            float probeY = space.TwoDimensional ? FromScreenY(mouse.Y) : 0f;
            float[] weights = new float[space.Samples.Count];
            AnimationBlendWeights.Evaluate(space, probeX, probeY, weights);
            ImGui.BeginTooltip();
            ImGui.Text($"Input: {probeX:0.00}, {probeY:0.00}");
            for (int i = 0; i < weights.Length; i++)
                if (weights[i] > .005f)
                    ImGui.Text($"{space.Samples[i].Clip}: {weights[i] * 100f:0}%");
            ImGui.EndTooltip();
        }
        return changed;
    }

    private bool DrawStateGraphCanvas(AnimationStateGraph graph)
    {
        bool changed = false;
        float width = Math.Clamp(ImGui.GetContentRegionAvail().X, 400f, 1000f);
        const float height = 400f, nodeWidth = 156f, nodeHeight = 64f;
        Vector2 origin = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##StateGraphCanvas", new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        Vector2 mouse = ImGui.GetMousePos();
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        uint background = ImGui.GetColorU32(EditorTheme.Background);
        uint grid = ImGui.GetColorU32(EditorTheme.Border);
        uint text = ImGui.GetColorU32(EditorTheme.TextMuted);
        uint accent = ImGui.GetColorU32(EditorTheme.AccentHover);
        uint selection = ImGui.GetColorU32(EditorTheme.SelectionHover);
        draw.AddRectFilled(origin, origin + new Vector2(width, height), background, 4f);
        draw.AddRect(origin, origin + new Vector2(width, height), grid, 4f);
        for (float x = origin.X; x < origin.X + width; x += 40f)
            draw.AddLine(new Vector2(x, origin.Y), new Vector2(x, origin.Y + height), grid);
        for (float y = origin.Y; y < origin.Y + height; y += 40f)
            draw.AddLine(new Vector2(origin.X, y), new Vector2(origin.X + width, y), grid);
        draw.AddText(origin + new Vector2(8f, 5f), text,
            "Drag states to arrange. Click an output dot, then another state to connect.");

        Vector2 Position(int index)
        {
            Vector2 authored = graph.States[index].EditorPosition;
            return authored == Vector2.Zero
                ? new Vector2(20f + index % 3 * 205f, 40f + index / 3 * 105f)
                : authored;
        }
        for (int i = 0; i < graph.Transitions.Count; i++)
        {
            AnimationGraphTransition transition = graph.Transitions[i];
            int from = graph.States.FindIndex(state => state.Name == transition.From);
            int to = graph.States.FindIndex(state => state.Name == transition.To);
            if (from < 0 || to < 0) continue;
            Vector2 start = origin + Position(from) + new Vector2(nodeWidth, nodeHeight * .5f);
            Vector2 end = origin + Position(to) + new Vector2(0f, nodeHeight * .5f);
            draw.AddLine(start, end, accent, 2f);
            Vector2 direction = end - start;
            if (direction.LengthSquared() > 1f)
            {
                direction = Vector2.Normalize(direction);
                Vector2 wing = new(-direction.Y, direction.X);
                draw.AddLine(end, end - direction * 10f + wing * 5f, accent, 2f);
                draw.AddLine(end, end - direction * 10f - wing * 5f, accent, 2f);
            }
        }
        int hitNode = -1, hitOutput = -1;
        for (int i = 0; i < graph.States.Count; i++)
        {
            AnimationGraphState state = graph.States[i];
            Vector2 topLeft = origin + Position(i);
            Vector2 bottomRight = topLeft + new Vector2(nodeWidth, nodeHeight);
            bool selected = state.Name == _selectedGraphState;
            draw.AddRectFilled(topLeft, bottomRight, selected ? selection : grid, 6f);
            draw.AddRect(topLeft, bottomRight, accent, 6f);
            draw.AddText(topLeft + new Vector2(10f, 8f), text,
                string.IsNullOrWhiteSpace(state.Name) ? "(unnamed)" : state.Name);
            draw.AddText(topLeft + new Vector2(10f, 32f), text,
                state.SourceKind == AnimationPoseSourceKind.Clip ? "Clip: " + state.Source : "Blend: " + state.Source);
            Vector2 output = topLeft + new Vector2(nodeWidth, nodeHeight * .5f);
            draw.AddCircleFilled(output, 7f, accent);
            if (Vector2.DistanceSquared(mouse, output) <= 11f * 11f) hitOutput = i;
            else if (mouse.X >= topLeft.X && mouse.X <= bottomRight.X &&
                     mouse.Y >= topLeft.Y && mouse.Y <= bottomRight.Y) hitNode = i;
        }
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            if (hitOutput >= 0)
            {
                _pendingGraphTransition = graph.States[hitOutput].Name;
                _draggedGraphState = -1;
            }
            else if (hitNode >= 0)
            {
                AnimationGraphState state = graph.States[hitNode];
                if (!string.IsNullOrWhiteSpace(_pendingGraphTransition) &&
                    !string.Equals(_pendingGraphTransition, state.Name, StringComparison.OrdinalIgnoreCase))
                {
                    graph.Transitions.Add(new AnimationGraphTransition
                    { From = _pendingGraphTransition, To = state.Name });
                    _pendingGraphTransition = string.Empty;
                    changed = true;
                }
                else
                {
                    _selectedGraphState = state.Name;
                    _draggedGraphState = hitNode;
                }
            }
            else { _pendingGraphTransition = string.Empty; _draggedGraphState = -1; }
        }
        if (_draggedGraphState >= 0 && _draggedGraphState < graph.States.Count &&
            ImGui.IsMouseDown(ImGuiMouseButton.Left) && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            AnimationGraphState state = graph.States[_draggedGraphState];
            state.EditorPosition = Vector2.Clamp(Position(_draggedGraphState) + ImGui.GetIO().MouseDelta,
                new Vector2(4f, 30f), new Vector2(width - nodeWidth - 4f, height - nodeHeight - 4f));
            changed = true;
        }
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) _draggedGraphState = -1;
        if (!string.IsNullOrWhiteSpace(_pendingGraphTransition))
            ImGui.TextDisabled("Connecting from " + _pendingGraphTransition + " — click destination state.");
        return changed;
    }
}
