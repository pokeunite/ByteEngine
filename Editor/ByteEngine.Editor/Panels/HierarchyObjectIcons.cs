using System.Numerics;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal enum HierarchyObjectKind { Empty, Mesh, Sprite, Canvas, Text, Panel, Image, Button, Slider, Progress, Camera, Light, Audio, Component }

internal static class HierarchyObjectIcons
{
    internal static HierarchyObjectKind Kind(GameObject obj)
    {
        if (obj.GetComponent<UiCanvas>() != null) return HierarchyObjectKind.Canvas;
        if (obj.GetComponent<UiText>() != null) return HierarchyObjectKind.Text;
        if (obj.GetComponent<UiWidget>() is { } widget)
            return widget.Kind switch
            {
                UiWidgetKind.Image => HierarchyObjectKind.Image,
                UiWidgetKind.Button => HierarchyObjectKind.Button,
                UiWidgetKind.Slider => HierarchyObjectKind.Slider,
                UiWidgetKind.ProgressBar or UiWidgetKind.SegmentedBar => HierarchyObjectKind.Progress,
                _ => HierarchyObjectKind.Panel
            };
        if (obj.GetComponent<Camera3D>() != null || obj.GetComponent<Camera2D>() != null) return HierarchyObjectKind.Camera;
        if (obj.GetComponent<DirectionalLight>() != null || obj.GetComponent<PointLight>() != null) return HierarchyObjectKind.Light;
        if (obj.GetComponent<SkeletalMeshRenderer>() != null || obj.GetComponent<MeshRenderer>() != null || obj.GetComponent<ModelHierarchyInstance>() != null)
            return HierarchyObjectKind.Mesh;
        if (obj.GetComponent<SpriteRenderer>() != null) return HierarchyObjectKind.Sprite;
        if (obj.GetComponent<AudioSource3D>() != null || obj.GetComponent<AudioListener3D>() != null) return HierarchyObjectKind.Audio;
        return obj.Components.Count > 0 ? HierarchyObjectKind.Component : HierarchyObjectKind.Empty;
    }

    internal static void Draw(GameObject obj, Vector2 position, float size)
    {
        var kind = Kind(obj);
        var draw = ImGui.GetWindowDrawList();
        uint color = ImGui.GetColorU32(obj.ActiveInHierarchy ? new Vector4(.65f, .84f, .96f, 1) : new Vector4(.42f, .45f, .5f, 1));
        Vector2 P(float x, float y) => position + new Vector2(x, y) * size;
        void Line(float x, float y, float xx, float yy) => draw.AddLine(P(x, y), P(xx, yy), color, 1.3f);
        void Box(float x, float y, float xx, float yy) => draw.AddRect(P(x, y), P(xx, yy), color, 1);
        switch (kind)
        {
            case HierarchyObjectKind.Text:
                Line(.15f, .17f, .85f, .17f); Line(.5f, .17f, .5f, .85f); Line(.32f, .85f, .68f, .85f);
                break;
            case HierarchyObjectKind.Mesh:
                Line(.5f, .05f, .95f, .28f); Line(.95f, .28f, .95f, .73f); Line(.95f, .73f, .5f, .97f);
                Line(.5f, .97f, .05f, .73f); Line(.05f, .73f, .05f, .28f); Line(.05f, .28f, .5f, .05f);
                Line(.05f, .28f, .5f, .5f); Line(.5f, .5f, .95f, .28f); Line(.5f, .5f, .5f, .97f);
                break;
            case HierarchyObjectKind.Camera:
                Box(.05f, .2f, .65f, .8f); Line(.65f, .4f, .95f, .2f); Line(.95f, .2f, .95f, .8f); Line(.95f, .8f, .65f, .6f);
                break;
            case HierarchyObjectKind.Light:
                draw.AddCircle(P(.5f, .5f), size * .2f, color);
                for (int i = 0; i < 8; i++)
                {
                    var axis = new Vector2(MathF.Cos(i * MathF.PI / 4), MathF.Sin(i * MathF.PI / 4));
                    draw.AddLine(P(.5f, .5f) + axis * size * .32f, P(.5f, .5f) + axis * size * .48f, color);
                }
                break;
            case HierarchyObjectKind.Slider:
                Line(.05f, .5f, .95f, .5f); Box(.3f, .2f, .5f, .8f);
                break;
            case HierarchyObjectKind.Progress:
                Box(.02f, .25f, .98f, .75f); draw.AddRectFilled(P(.1f, .34f), P(.58f, .66f), color);
                break;
            case HierarchyObjectKind.Button:
                Box(.02f, .2f, .98f, .8f); Line(.24f, .5f, .76f, .5f);
                break;
            case HierarchyObjectKind.Image:
            case HierarchyObjectKind.Sprite:
                Box(.02f, .1f, .98f, .9f); Line(.08f, .8f, .42f, .42f); Line(.42f, .42f, .68f, .78f);
                Line(.68f, .78f, .94f, .55f); draw.AddCircleFilled(P(.72f, .32f), size * .08f, color);
                break;
            case HierarchyObjectKind.Canvas:
                Box(.04f, .04f, .96f, .96f); Line(.3f, 0, .3f, 1); Line(0, .3f, 1, .3f);
                break;
            case HierarchyObjectKind.Panel:
                Box(.02f, .1f, .98f, .9f); Line(.02f, .3f, .98f, .3f);
                break;
            case HierarchyObjectKind.Audio:
                Box(.04f, .35f, .28f, .65f); Line(.28f, .35f, .58f, .1f); Line(.58f, .1f, .58f, .9f);
                Line(.58f, .9f, .28f, .65f); Line(.75f, .25f, .94f, .5f); Line(.94f, .5f, .75f, .75f);
                break;
            case HierarchyObjectKind.Component:
                Box(.2f, .2f, .8f, .8f); Line(.4f, .35f, .4f, .65f); Line(.6f, .35f, .6f, .65f);
                break;
            default:
                draw.AddCircle(P(.5f, .5f), size * .23f, color);
                break;
        }
        if (ImGui.IsItemHovered() && ImGui.IsMouseHoveringRect(position, position + new Vector2(size)))
            ImGui.SetTooltip(kind.ToString());
    }
}
