using System.Numerics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
using ByteEngine.Editor.Gizmos;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

/// <summary>Editor-only surface brush. One serialized scene undo transaction per stroke.</summary>
internal sealed class FoliagePaintTool
{
    private bool _enabled, _stroke;
    private float _radius = 1, _density = 3;
    private Vector3? _lastStamp;
    private readonly Random _random = new();
    private FoliagePatch? _target;

    public void DrawToolbar(EditorState state)
    {
        if (state.SelectedObject?.GetComponent<FoliagePatch>() is not { } patch ||
            state.Mode != EditorMode.Edit) return;
        ImGui.SameLine();
        ImGui.Checkbox(_enabled ? "Brush ON - Paint Foliage###FoliageBrush" :
            "Brush OFF - Paint Foliage###FoliageBrush", ref _enabled);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Turns the cursor brush on/off. This does not delete your painted plants. Click/drag to paint; Shift-drag to erase.");
        if (!_enabled) return;
        ImGui.SameLine();
        ImGui.TextDisabled($"Radius {patch.BrushRadius:0.##}m | Density {patch.PaintDensity:0.##}/m2 | Size {patch.PlantHeight:0.##}m");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Adjust Paint Brush Radius, Paint Density and Plant Size in the Foliage Patch Inspector.");
        ImGui.SameLine(); ImGui.TextDisabled($"{patch.PaintedPlants.Count}/2000");
    }

    public bool Update(EditorState state, bool hovered, Vector2 minimum, Vector2 size)
    {
        var patch = state.SelectedObject?.GetComponent<FoliagePatch>();
        bool active = _enabled && patch != null && state.Mode == EditorMode.Edit;
        if (_stroke && (!active || patch != _target || !ImGui.IsMouseDown(ImGuiMouseButton.Left)))
        {
            state.Undo?.CommitGesture(state);
            _stroke = false; _lastStamp = null; _target = null;
        }
        if (!active) return false;
        _radius = patch!.BrushRadius; _density = patch.PaintDensity;
        if (!hovered) return true;
        var ray = Gizmo3DController.ScreenRay(ImGui.GetMousePos(),state.Camera3D,minimum,size);
        if (!GameplayQuery3D.Raycast(state.DisplayedScene,ray.Origin,ray.Direction,out RaycastHit3D hit,
            2000,ignore:patch!.GameObject,includeTriggers:false)) return true;
        bool erase = ImGui.GetIO().KeyShift;
        // Editor overlay only: the translucent volume is never a scene object or collider.
        uint color = erase ? 0xff5050ff : 0xff50ff70;
        uint fill = erase ? 0x125050ffu : 0x1250ff70u;
        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(minimum, minimum+size, true);
        Vector3 center = hit.Point + Vector3.UnitY * .025f;
        Vector3 tip = center + Vector3.UnitY * Math.Max(.3f, _radius * 1.25f);
        Vector2 apex = Gizmo3DController.Project(tip,state.Camera3D,minimum,size);
        Vector2 centerScreen = Gizmo3DController.Project(center,state.Camera3D,minimum,size);
        var ring = new Vector2[48];
        for (int i = 0; i < ring.Length; i++)
        {
            float angle = i * MathF.Tau / ring.Length;
            Vector3 point = center + new Vector3(MathF.Cos(angle)*_radius,0,MathF.Sin(angle)*_radius);
            ring[i] = Gizmo3DController.Project(point,state.Camera3D,minimum,size);
        }
        for (int i = 0; i < ring.Length; i++)
        {
            Vector2 next = ring[(i+1)%ring.Length];
            draw.AddTriangleFilled(apex,ring[i],next,fill);
            draw.AddTriangleFilled(centerScreen,ring[i],next,fill);
        }
        for (int i = 0; i < ring.Length; i++)
        {
            draw.AddLine(ring[i],ring[(i+1)%ring.Length],color,2);
            if (i % 12 == 0) draw.AddLine(apex,ring[i],color,1);
        }
        draw.AddCircleFilled(centerScreen,3,color);
        draw.PopClipRect();
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            state.Undo?.BeginGesture(state,erase?"Erase Foliage":"Paint Foliage");
            _stroke=true; _target=patch; _lastStamp=null;
            patch.UsePaintedLayout=true;
        }
        if (!_stroke || !ImGui.IsMouseDown(ImGuiMouseButton.Left)) return true;
        if (_lastStamp.HasValue && Vector3.DistanceSquared(_lastStamp.Value,hit.Point)<_radius*_radius*.0625f) return true;
        _lastStamp=hit.Point;
        if(erase) patch.ErasePlants(hit.Point,_radius);
        else
        {
            float spacing = .75f/MathF.Sqrt(_density);
            int count=Math.Clamp((int)MathF.Ceiling(MathF.PI*_radius*_radius*_density),1,256);
            for(int i=0;i<count;i++)
            {
                float angle=(float)_random.NextDouble()*MathF.Tau;
                float distance=i==0?0:MathF.Sqrt((float)_random.NextDouble())*_radius;
                Vector3 position=hit.Point+new Vector3(MathF.Cos(angle)*distance,0,MathF.Sin(angle)*distance);
                if(GameplayQuery3D.Raycast(state.DisplayedScene,position+Vector3.UnitY*(_radius+1),
                    -Vector3.UnitY,out RaycastHit3D surface,2*(_radius+1),ignore:patch.GameObject,includeTriggers:false))
                    patch.PaintPlant(surface.Point,(float)_random.NextDouble()*MathF.Tau,spacing);
            }
        }
        patch.Rebuild();
        state.MarkDirty();
        return true;
    }
}
