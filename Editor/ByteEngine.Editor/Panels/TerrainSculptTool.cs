using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Editor.Gizmos;
using ImGuiNET;
namespace ByteEngine.Editor.Panels;

/// <summary>Scene-view sculpt brush for any heightfield exposing ITerrainSculptSurface.</summary>
internal sealed class TerrainSculptTool
{
    private bool _enabled,_stroke,_sampleHeight=true;
    private float _radius=20,_strength=4,_flattenHeight;
    private int _mode;
    private ITerrainSculptSurface? _target;
    public void DrawToolbar(EditorState state)
    {
        if(state.Mode!=EditorMode.Edit)return;
        var surface=state.SelectedObject?.Components.OfType<ITerrainSculptSurface>().FirstOrDefault()??state.DisplayedScene.GameObjects.SelectMany(o=>o.Components).OfType<ITerrainSculptSurface>().FirstOrDefault();
        if(surface==null)return;
        ImGui.SameLine();if(ImGui.Button(_enabled?"Terrain ON###TerrainTools":"Terrain###TerrainTools")){state.SelectedObject=surface.SculptCollider.GameObject;_enabled=true;ImGui.OpenPopup("TerrainBrushSettings");}
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Select the terrain and open sculpt tools. Edits preserve its material and save with the scene.");
        if(ImGui.BeginPopup("TerrainBrushSettings")){
            ImGui.TextUnformatted("Terrain authoring");ImGui.TextDisabled(surface.SculptCollider.GameObject.Name);ImGui.Checkbox("Enable sculpt brush",ref _enabled);ImGui.SetNextItemWidth(230);ImGui.Combo("Tool",ref _mode,"Raise\0Lower\0Smooth\0Flatten\0");
            ImGui.SetNextItemWidth(230);ImGui.SliderFloat("Radius (m)",ref _radius,.5f,100,"%.1f");
            ImGui.SetNextItemWidth(230);ImGui.SliderFloat("Strength",ref _strength,.1f,30,"%.1f");
            ImGui.Checkbox("Sample flatten height on click",ref _sampleHeight);
            ImGui.BeginDisabled(_sampleHeight);ImGui.SetNextItemWidth(230);ImGui.InputFloat("Flatten height (world m)",ref _flattenHeight,.5f,5);ImGui.EndDisabled();
            ImGui.Separator();ImGui.TextWrapped("Drag: apply brush\nShift: reverse raise/lower\nCtrl: temporary smooth\nCtrl+Z: undo whole stroke\nCtrl+S: save authored terrain with the scene");
            ImGui.EndPopup();
        }

    }
    public void EndStroke(EditorState state){if(!_stroke)return;state.Undo?.CommitGesture(state);_stroke=false;_target=null;}
    public bool Update(EditorState state,bool hovered,Vector2 minimum,Vector2 size)
    {
        var terrain=state.SelectedObject?.Components.OfType<ITerrainSculptSurface>().FirstOrDefault();
        bool active=_enabled&&terrain!=null&&state.Mode==EditorMode.Edit;
        if(_stroke&&(!active||terrain!=_target||!hovered||!ImGui.IsMouseDown(ImGuiMouseButton.Left)||ImGui.GetIO().KeyAlt||ImGui.IsMouseDown(ImGuiMouseButton.Right)||ImGui.IsMouseDown(ImGuiMouseButton.Middle))){state.Undo?.CommitGesture(state);_stroke=false;_target=null;}
        if(!active)return false;if(!hovered)return true;
        var ray=Gizmo3DController.ScreenRay(ImGui.GetMousePos(),state.Camera3D,minimum,size);var collider=terrain!.SculptCollider;
        if(!collider.Cast(ray.Origin,ray.Direction,0,5000,out float distance,out _)||distance<=0)return true;
        Vector3 center=ray.Origin+Vector3.Normalize(ray.Direction)*distance;
        var mode=(TerrainBrushMode)_mode;if(ImGui.GetIO().KeyCtrl)mode=TerrainBrushMode.Smooth;else if(ImGui.GetIO().KeyShift){if(mode==TerrainBrushMode.Raise)mode=TerrainBrushMode.Lower;else if(mode==TerrainBrushMode.Lower)mode=TerrainBrushMode.Raise;}
        uint color=mode==TerrainBrushMode.Lower?0xff7085ffu:0xff78df70u;var draw=ImGui.GetWindowDrawList();draw.PushClipRect(minimum,minimum+size,true);Vector2? prior=null;
        for(int i=0;i<=64;i++){float angle=i*MathF.Tau/64;Vector3 point=center+new Vector3(MathF.Cos(angle)*_radius,0,MathF.Sin(angle)*_radius);if(!collider.TrySampleWorld(point,out var surface,out _)){prior=null;continue;}var projected=Gizmo3DController.Project(surface+Vector3.UnitY*.05f,state.Camera3D,minimum,size);if(float.IsFinite(projected.X)&&float.IsFinite(projected.Y)){if(prior.HasValue)draw.AddLine(prior.Value,projected,color,2);prior=projected;}}
        var screen=Gizmo3DController.Project(center,state.Camera3D,minimum,size);draw.AddCircleFilled(screen,3,color);draw.AddText(screen+new Vector2(12,-18),color,$"{mode} | {_radius:0.#} m");draw.PopClipRect();
        if(ImGui.GetIO().KeyAlt||ImGui.IsMouseDown(ImGuiMouseButton.Right)||ImGui.IsMouseDown(ImGuiMouseButton.Middle))return true;
        if(ImGui.IsMouseClicked(ImGuiMouseButton.Left)){state.Undo?.BeginGesture(state,"Sculpt Terrain");_stroke=true;_target=terrain;if(_sampleHeight)_flattenHeight=center.Y;}
        if(_stroke&&ImGui.IsMouseDown(ImGuiMouseButton.Left)&&terrain.Sculpt(center,_radius,_strength,Math.Clamp(ImGui.GetIO().DeltaTime,.001f,.05f),mode,_flattenHeight)>0)state.MarkDirty();
        return true;
    }
}
