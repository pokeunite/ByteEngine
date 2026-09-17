using System.Numerics;

using ByteEngine.Core.Assets;

using ImGuiNET;

namespace ByteEngine.Editor;

internal enum EditorIconKind
{
    File,
    Folder,
    Blueprint,
    EventModule,
    Scene,
    Model,
    Animation,
    Audio,
    Texture
}

/// <summary>
/// Lightweight vector editor icons rendered directly through ImGui.
/// No external icon font or texture atlas is required.
/// </summary>
internal static class EditorIcons
{
    public static EditorIconKind ForAsset(
        AssetType? type) =>
        type switch
        {
            AssetType.Texture2D => EditorIconKind.Texture,
            AssetType.AudioClip => EditorIconKind.Audio,
            AssetType.Scene => EditorIconKind.Scene,
            AssetType.Model3D => EditorIconKind.Model,
            AssetType.EventModule => EditorIconKind.EventModule,
            AssetType.Blueprint => EditorIconKind.Blueprint,
            AssetType.AnimationEvents => EditorIconKind.Animation,
            AssetType.AnimationProfile => EditorIconKind.Animation,
            _ => EditorIconKind.File
        };

    public static string AssetTypeName(
        AssetType? type) =>
        type switch
        {
            AssetType.Texture2D => "Texture",
            AssetType.AudioClip => "Audio",
            AssetType.Scene => "Scene",
            AssetType.Model3D => "3D Model",
            AssetType.EventModule => "Events",
            AssetType.Blueprint => "Blueprint",
            AssetType.AnimationEvents => "Animation",
            AssetType.AnimationProfile => "Animation Profile",
            _ => "File"
        };

    public static Vector4 Color(
        EditorIconKind kind) =>
        kind switch
        {
            EditorIconKind.Folder => new Vector4(0.86f, 0.65f, 0.28f, 1.0f),
            EditorIconKind.Blueprint => new Vector4(0.24f, 0.58f, 0.92f, 1.0f),
            EditorIconKind.EventModule => new Vector4(0.93f, 0.52f, 0.25f, 1.0f),
            EditorIconKind.Scene => new Vector4(0.45f, 0.78f, 0.53f, 1.0f),
            EditorIconKind.Model => new Vector4(0.58f, 0.50f, 0.90f, 1.0f),
            EditorIconKind.Animation => new Vector4(0.88f, 0.42f, 0.63f, 1.0f),
            EditorIconKind.Audio => new Vector4(0.35f, 0.78f, 0.78f, 1.0f),
            EditorIconKind.Texture => new Vector4(0.53f, 0.73f, 0.95f, 1.0f),
            _ => new Vector4(0.58f, 0.62f, 0.69f, 1.0f)
        };

    public static void DrawTileIcon(
        EditorIconKind kind,
        Vector2 minimum,
        float size)
    {
        var drawList =
            ImGui.GetWindowDrawList();

        Vector4 color =
            Color(kind);

        uint fill =
            ImGui.GetColorU32(color);

        uint bright =
            ImGui.GetColorU32(
                new Vector4(
                    Math.Min(color.X + 0.16f, 1.0f),
                    Math.Min(color.Y + 0.16f, 1.0f),
                    Math.Min(color.Z + 0.16f, 1.0f),
                    1.0f));

        uint light =
            ImGui.GetColorU32(
                new Vector4(
                    0.94f,
                    0.97f,
                    1.0f,
                    1.0f));

        Vector2 center =
            minimum +
            new Vector2(
                size * 0.5f,
                size * 0.5f);

        switch (kind)
        {
            case EditorIconKind.Folder:
                drawList.AddRectFilled(
                    minimum + new Vector2(size * 0.08f, size * 0.18f),
                    minimum + new Vector2(size * 0.48f, size * 0.38f),
                    bright,
                    size * 0.05f);

                drawList.AddRectFilled(
                    minimum + new Vector2(size * 0.06f, size * 0.30f),
                    minimum + new Vector2(size * 0.94f, size * 0.84f),
                    fill,
                    size * 0.06f);
                break;

            case EditorIconKind.Model:
                DrawCube(
                    drawList,
                    minimum,
                    size,
                    fill,
                    bright);
                break;

            case EditorIconKind.Blueprint:
                DrawNodeGraph(
                    drawList,
                    minimum,
                    size,
                    fill,
                    light);
                break;

            case EditorIconKind.EventModule:
                DrawEventBolt(
                    drawList,
                    minimum,
                    size,
                    fill,
                    light);
                break;

            case EditorIconKind.Scene:
                DrawSceneLayers(
                    drawList,
                    minimum,
                    size,
                    fill,
                    bright);
                break;

            case EditorIconKind.Animation:
                DrawAnimation(
                    drawList,
                    minimum,
                    size,
                    fill,
                    light);
                break;

            case EditorIconKind.Audio:
                DrawAudio(
                    drawList,
                    minimum,
                    size,
                    fill,
                    light);
                break;

            case EditorIconKind.Texture:
                DrawTexture(
                    drawList,
                    minimum,
                    size,
                    fill,
                    bright);
                break;

            default:
                drawList.AddRectFilled(
                    minimum + new Vector2(size * 0.18f, size * 0.10f),
                    minimum + new Vector2(size * 0.82f, size * 0.90f),
                    fill,
                    size * 0.04f);

                drawList.AddLine(
                    minimum + new Vector2(size * 0.30f, size * 0.38f),
                    minimum + new Vector2(size * 0.70f, size * 0.38f),
                    light,
                    Math.Max(size * 0.05f, 1.0f));

                drawList.AddLine(
                    minimum + new Vector2(size * 0.30f, size * 0.55f),
                    minimum + new Vector2(size * 0.66f, size * 0.55f),
                    light,
                    Math.Max(size * 0.05f, 1.0f));
                break;
        }
    }

    private static void DrawCube(
        ImDrawListPtr drawList,
        Vector2 minimum,
        float size,
        uint fill,
        uint bright)
    {
        Vector2 a = minimum + new Vector2(size * 0.22f, size * 0.34f);
        Vector2 b = minimum + new Vector2(size * 0.50f, size * 0.18f);
        Vector2 c = minimum + new Vector2(size * 0.78f, size * 0.34f);
        Vector2 d = minimum + new Vector2(size * 0.50f, size * 0.50f);
        Vector2 e = minimum + new Vector2(size * 0.22f, size * 0.66f);
        Vector2 f = minimum + new Vector2(size * 0.50f, size * 0.82f);
        Vector2 g = minimum + new Vector2(size * 0.78f, size * 0.66f);

        drawList.AddTriangleFilled(a, b, d, bright);
        drawList.AddTriangleFilled(b, c, d, fill);
        drawList.AddTriangleFilled(a, d, e, fill);
        drawList.AddTriangleFilled(d, f, e, fill);
        drawList.AddTriangleFilled(d, c, g, bright);
        drawList.AddTriangleFilled(d, g, f, bright);
    }

    private static void DrawNodeGraph(
        ImDrawListPtr drawList,
        Vector2 minimum,
        float size,
        uint fill,
        uint light)
    {
        Vector2 left = minimum + new Vector2(size * 0.26f, size * 0.50f);
        Vector2 top = minimum + new Vector2(size * 0.66f, size * 0.28f);
        Vector2 bottom = minimum + new Vector2(size * 0.68f, size * 0.72f);

        drawList.AddLine(left, top, light, Math.Max(size * 0.06f, 1.0f));
        drawList.AddLine(left, bottom, light, Math.Max(size * 0.06f, 1.0f));

        drawList.AddCircleFilled(left, size * 0.13f, fill);
        drawList.AddCircleFilled(top, size * 0.12f, fill);
        drawList.AddCircleFilled(bottom, size * 0.12f, fill);
    }

    private static void DrawEventBolt(
        ImDrawListPtr drawList,
        Vector2 minimum,
        float size,
        uint fill,
        uint light)
    {
        Vector2 p1 = minimum + new Vector2(size * 0.54f, size * 0.10f);
        Vector2 p2 = minimum + new Vector2(size * 0.28f, size * 0.52f);
        Vector2 p3 = minimum + new Vector2(size * 0.48f, size * 0.52f);
        Vector2 p4 = minimum + new Vector2(size * 0.38f, size * 0.90f);
        Vector2 p5 = minimum + new Vector2(size * 0.76f, size * 0.40f);
        Vector2 p6 = minimum + new Vector2(size * 0.55f, size * 0.40f);

        drawList.AddTriangleFilled(p1, p2, p3, fill);
        drawList.AddTriangleFilled(p3, p4, p5, fill);
        drawList.AddTriangleFilled(p3, p5, p6, light);
    }

    private static void DrawSceneLayers(
        ImDrawListPtr drawList,
        Vector2 minimum,
        float size,
        uint fill,
        uint bright)
    {
        drawList.AddTriangleFilled(
            minimum + new Vector2(size * 0.18f, size * 0.34f),
            minimum + new Vector2(size * 0.50f, size * 0.16f),
            minimum + new Vector2(size * 0.82f, size * 0.34f),
            bright);

        drawList.AddTriangleFilled(
            minimum + new Vector2(size * 0.18f, size * 0.34f),
            minimum + new Vector2(size * 0.50f, size * 0.52f),
            minimum + new Vector2(size * 0.82f, size * 0.34f),
            fill);

        drawList.AddLine(
            minimum + new Vector2(size * 0.22f, size * 0.54f),
            minimum + new Vector2(size * 0.50f, size * 0.70f),
            fill,
            Math.Max(size * 0.09f, 1.0f));

        drawList.AddLine(
            minimum + new Vector2(size * 0.50f, size * 0.70f),
            minimum + new Vector2(size * 0.78f, size * 0.54f),
            fill,
            Math.Max(size * 0.09f, 1.0f));

        drawList.AddLine(
            minimum + new Vector2(size * 0.22f, size * 0.70f),
            minimum + new Vector2(size * 0.50f, size * 0.86f),
            bright,
            Math.Max(size * 0.07f, 1.0f));

        drawList.AddLine(
            minimum + new Vector2(size * 0.50f, size * 0.86f),
            minimum + new Vector2(size * 0.78f, size * 0.70f),
            bright,
            Math.Max(size * 0.07f, 1.0f));
    }

    private static void DrawAnimation(
        ImDrawListPtr drawList,
        Vector2 minimum,
        float size,
        uint fill,
        uint light)
    {
        drawList.AddRect(
            minimum + new Vector2(size * 0.16f, size * 0.18f),
            minimum + new Vector2(size * 0.84f, size * 0.82f),
            fill,
            size * 0.05f,
            ImDrawFlags.None,
            Math.Max(size * 0.07f, 1.0f));

        drawList.AddTriangleFilled(
            minimum + new Vector2(size * 0.42f, size * 0.34f),
            minimum + new Vector2(size * 0.42f, size * 0.68f),
            minimum + new Vector2(size * 0.70f, size * 0.51f),
            light);
    }

    private static void DrawAudio(
        ImDrawListPtr drawList,
        Vector2 minimum,
        float size,
        uint fill,
        uint light)
    {
        drawList.AddRectFilled(
            minimum + new Vector2(size * 0.18f, size * 0.40f),
            minimum + new Vector2(size * 0.38f, size * 0.62f),
            fill);

        drawList.AddTriangleFilled(
            minimum + new Vector2(size * 0.38f, size * 0.40f),
            minimum + new Vector2(size * 0.38f, size * 0.62f),
            minimum + new Vector2(size * 0.60f, size * 0.78f),
            fill);

        drawList.AddLine(
            minimum + new Vector2(size * 0.64f, size * 0.38f),
            minimum + new Vector2(size * 0.74f, size * 0.50f),
            light,
            Math.Max(size * 0.05f, 1.0f));

        drawList.AddLine(
            minimum + new Vector2(size * 0.74f, size * 0.50f),
            minimum + new Vector2(size * 0.64f, size * 0.64f),
            light,
            Math.Max(size * 0.05f, 1.0f));
    }

    private static void DrawTexture(
        ImDrawListPtr drawList,
        Vector2 minimum,
        float size,
        uint fill,
        uint bright)
    {
        float cell = size * 0.22f;
        Vector2 start = minimum + new Vector2(size * 0.17f, size * 0.17f);

        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                drawList.AddRectFilled(
                    start + new Vector2(x * cell, y * cell),
                    start + new Vector2((x + 1) * cell, (y + 1) * cell),
                    (x + y) % 2 == 0
                        ? fill
                        : bright);
            }
        }
    }
}
