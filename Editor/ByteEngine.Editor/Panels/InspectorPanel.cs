using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.Serialization.SerializationModels;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class InspectorPanel
{
    public bool IsOpen { get; set; } = true;

    public void Draw(EditorState state, EditorProjectContext project)
    {
        bool isOpen = IsOpen;
        ImGui.Begin("Inspector", ref isOpen);
        IsOpen = isOpen;

        if (state.Selection.Count > 1)
        {
            ImGui.Text($"{state.Selection.Count} Objects Selected");
            ImGui.TextDisabled("Use Scene View gizmos to move the selection.");
            ImGui.End();
            return;
        }

        if (state.SelectedObject == null)
        {
            if (state.SelectedAssetId.HasValue || state.SelectedAssetPath != null) DrawAssetOrEmpty(state, project);
            else
            {
                bool variablesReadOnly = state.Mode != EditorMode.Edit;
                ImGui.BeginDisabled(variablesReadOnly);
                DrawStoreVariables("SCENE VARIABLES", state.EditorScene.Variables, state);
                DrawGlobalVariables(state.Project.GlobalVariables, state);
                ImGui.EndDisabled();
            }
            ImGui.End();
            return;
        }

        bool readOnly = state.Mode != EditorMode.Edit;
        if (readOnly) ImGui.TextColored(new Vector4(1f, .75f, .2f, 1f), "Editor values are read-only during Play Mode.");
        ImGui.BeginDisabled(readOnly);

        GameObject selected = state.SelectedObject;
        string oldName = selected.Name;
        string name = oldName;
        bool nameChanged = ImGui.InputText("Name", ref name, 256);
        string nextName = string.IsNullOrWhiteSpace(name) ? "GameObject" : name;
        if (nameChanged) selected.Name = nextName;
        TrackItem(state, "Rename GameObject", nameChanged, () => selected.Name = oldName, () => selected.Name = nextName);

        bool oldActive = selected.Active;
        bool active = oldActive;
        bool activeChanged = ImGui.Checkbox("Active", ref active);
        if (activeChanged) selected.Active = active;
        TrackItem(state, "Set Active", activeChanged, () => selected.Active = oldActive, () => selected.Active = active);

        ImGui.SeparatorText("Transform");
        Vector3 oldPosition = selected.Transform.LocalPosition;
        Vector3 position = oldPosition;
        bool positionChanged = ImGui.DragFloat3("Position", ref position, .05f);
        if (positionChanged) selected.Transform.LocalPosition = position;
        TrackItem(state, "Move GameObject", positionChanged, () => selected.Transform.LocalPosition = oldPosition, () => selected.Transform.LocalPosition = position);

        Vector3 oldRotation = selected.Transform.EulerAngles;
        Vector3 rotation = oldRotation;
        bool rotationChanged = ImGui.DragFloat3("Rotation", ref rotation, .25f);
        if (rotationChanged) selected.Transform.EulerAngles = rotation;
        TrackItem(state, "Rotate GameObject", rotationChanged, () => selected.Transform.EulerAngles = oldRotation, () => selected.Transform.EulerAngles = rotation);

        Vector3 oldScale = selected.Transform.LocalScale;
        Vector3 scale = oldScale;
        bool scaleChanged = ImGui.DragFloat3("Scale", ref scale, .02f, .001f, 10000f);
        Vector3 nextScale = Vector3.Max(scale, new Vector3(.001f));
        if (scaleChanged) selected.Transform.LocalScale = nextScale;
        TrackItem(state, "Scale GameObject", scaleChanged, () => selected.Transform.LocalScale = oldScale, () => selected.Transform.LocalScale = nextScale);

        DrawStoreVariables("OBJECT VARIABLES", selected.Variables, state);

        foreach (Component component in selected.Components.ToArray())
        {
            ImGui.SeparatorText(component.GetType().Name);
            bool oldEnabled = component.Enabled;
            bool enabled = oldEnabled;
            bool enabledChanged = ImGui.Checkbox($"Enabled##{component.GetHashCode()}", ref enabled);
            if (enabledChanged) component.Enabled = enabled;
            TrackItem(state, "Set Component Enabled", enabledChanged, () => component.Enabled = oldEnabled, () => component.Enabled = enabled);
            ImGui.SameLine();
            if (ImGui.SmallButton($"Remove##{component.GetHashCode()}"))
                state.Undo?.Execute(state, $"Remove {component.GetType().Name}", () => selected.RemoveComponent(component));
            DrawComponentProperties(state, project, component);
        }

        ImGui.Separator();
        if (ImGui.Button("Add Component")) ImGui.OpenPopup("Add Component Popup");
        if (ImGui.BeginPopup("Add Component Popup"))
        {
            DrawAddComponentItem<Camera2D>("Camera2D", selected, state, () => new Camera2D());
            DrawAddComponentItem<SpriteRenderer>("SpriteRenderer", selected, state, () => new SpriteRenderer());
            DrawAddComponentItem<MeshRenderer>("MeshRenderer", selected, state, () => new MeshRenderer());
            DrawAddComponentItem<Camera3D>("Camera3D", selected, state, () => new Camera3D());
            DrawAddComponentItem<DirectionalLight>("DirectionalLight", selected, state, () => new DirectionalLight());
            DrawAddComponentItem<BoxCollider3D>("BoxCollider3D", selected, state, () => new BoxCollider3D());
            DrawAddComponentItem<GroundSurface>("GroundSurface", selected, state, () => new GroundSurface());
            DrawAddComponentItem<CharacterController3D>("CharacterController3D", selected, state, () => new CharacterController3D());
            ImGui.EndPopup();
        }

        ImGui.EndDisabled();
        ImGui.End();
    }

    private static void DrawAddComponentItem<T>(string name, GameObject target, EditorState state, Func<T> factory) where T : Component
    {
        bool exists = target.HasComponent<T>();
        if (ImGui.MenuItem(name, string.Empty, false, !exists))
            state.Undo?.Execute(state, $"Add {name}", () => target.AddComponent(factory()));
        if (exists && ImGui.IsItemHovered()) ImGui.SetTooltip("This component is already attached.");
    }

    private static void DrawComponentProperties(EditorState state, EditorProjectContext project, Component component)
    {
        ImGui.Indent();
        if (component is Camera2D camera)
        {
            float oldZoom = camera.Zoom;
            float zoom = oldZoom;
            bool changed = ImGui.DragFloat($"Zoom##{component.GetHashCode()}", ref zoom, .01f, .01f, 100f);
            if (changed) camera.Zoom = zoom;
            TrackItem(state, "Change Camera Zoom", changed, () => camera.Zoom = oldZoom, () => camera.Zoom = zoom);
        }
        else if (component is SpriteRenderer sprite)
        {
            string label = TextureLabel(sprite.TextureReference, project.AssetDatabase);
            ImGui.Button($"Texture: {label}##{component.GetHashCode()}", new Vector2(-55f, 0f));
            if (ImGui.BeginDragDropTarget())
            {
                Guid? guid = AssetDragDrop.Accept();
                if (guid.HasValue && project.AssetDatabase.TryGetAsset(guid.Value, out AssetRecord? asset) && asset?.Type == AssetType.Texture2D)
                {
                    state.Undo?.Execute(state, "Change Sprite Texture", () =>
                    {
                        var reference = new AssetReference(asset.Guid, asset.ProjectPath);
                        sprite.TextureReference = reference;
                        sprite.Texture = project.Assets.LoadTexture(reference);
                    });
                }
                ImGui.EndDragDropTarget();
            }
            ImGui.SameLine();
            if (ImGui.SmallButton($"X##texture{component.GetHashCode()}"))
                state.Undo?.Execute(state, "Clear Sprite Texture", () => { sprite.TextureReference = null; sprite.Texture = null; });

            Vector4 oldTint = sprite.Tint;
            Vector4 tint = oldTint;
            bool tintChanged = ImGui.ColorEdit4($"Tint##{component.GetHashCode()}", ref tint);
            if (tintChanged) sprite.Tint = tint;
            TrackItem(state, "Change Sprite Tint", tintChanged, () => sprite.Tint = oldTint, () => sprite.Tint = tint);

            bool oldVisible = sprite.Visible;
            bool visible = oldVisible;
            bool visibleChanged = ImGui.Checkbox($"Visible##{component.GetHashCode()}", ref visible);
            if (visibleChanged) sprite.Visible = visible;
            TrackItem(state, "Set Sprite Visibility", visibleChanged, () => sprite.Visible = oldVisible, () => sprite.Visible = visible);

            int oldOrder = sprite.OrderInLayer;
            int order = oldOrder;
            bool orderChanged = ImGui.DragInt($"Order In Layer##{component.GetHashCode()}", ref order, 1f);
            if (orderChanged) sprite.OrderInLayer = order;
            TrackItem(state, "Change Sprite Order", orderChanged, () => sprite.OrderInLayer = oldOrder, () => sprite.OrderInLayer = order);

            Vector2 oldSize=sprite.Size,size=oldSize;bool sizeChanged=ImGui.DragFloat2($"Size##{component.GetHashCode()}",ref size,1,.001f,10000);if(sizeChanged)sprite.Size=Vector2.Max(size,new Vector2(.001f));Vector2 finalSize=Vector2.Max(size,new Vector2(.001f));TrackItem(state,"Change Sprite Size",sizeChanged,()=>sprite.Size=oldSize,()=>sprite.Size=finalSize);
        }
        else if(component is MeshRenderer mesh)
        {
            int primitive=(int)mesh.Primitive;if(ImGui.Combo($"Primitive##{component.GetHashCode()}",ref primitive,"Cube\0Plane\0Sphere\0"))state.Undo?.Execute(state,"Change Mesh Primitive",()=>mesh.Primitive=(PrimitiveMeshType)primitive);
            Vector4 old=mesh.Material.BaseColor,color=old;bool changed=ImGui.ColorEdit4($"Base Color##{component.GetHashCode()}",ref color);if(changed)mesh.Material.BaseColor=color;TrackItem(state,"Change Material Color",changed,()=>mesh.Material.BaseColor=old,()=>mesh.Material.BaseColor=color);
            bool visible=mesh.Visible,oldVisible=visible;bool vc=ImGui.Checkbox($"Visible##mesh{component.GetHashCode()}",ref visible);if(vc)mesh.Visible=visible;TrackItem(state,"Set Mesh Visibility",vc,()=>mesh.Visible=oldVisible,()=>mesh.Visible=visible);
        }
        else if(component is Camera3D camera3D)
        {
            float old=camera3D.FieldOfView,value=old;bool changed=ImGui.DragFloat($"Field of View##{component.GetHashCode()}",ref value,.25f,1,179);if(changed)camera3D.FieldOfView=value;TrackItem(state,"Change Field of View",changed,()=>camera3D.FieldOfView=old,()=>camera3D.FieldOfView=value);
            float near=camera3D.NearClip,far=camera3D.FarClip;if(ImGui.DragFloat($"Near Clip##{component.GetHashCode()}",ref near,.01f,.001f,100))camera3D.NearClip=near;if(ImGui.DragFloat($"Far Clip##{component.GetHashCode()}",ref far,1,1,100000))camera3D.FarClip=far;
        }
        else if(component is DirectionalLight light)
        {
            Vector3 color=light.Color;float intensity=light.Intensity;if(ImGui.ColorEdit3($"Color##{component.GetHashCode()}",ref color))light.Color=color;if(ImGui.DragFloat($"Intensity##{component.GetHashCode()}",ref intensity,.02f,0,100))light.Intensity=intensity;
        }
        else if(component is GroundSurface ground)
        {
            bool walkable=ground.Walkable;float friction=ground.Friction;if(ImGui.Checkbox($"Walkable##{component.GetHashCode()}",ref walkable))ground.Walkable=walkable;string surface=ground.SurfaceType;if(ImGui.InputText($"Surface Type##{component.GetHashCode()}",ref surface,64))ground.SurfaceType=surface;if(ImGui.DragFloat($"Friction##{component.GetHashCode()}",ref friction,.02f,0,10))ground.Friction=friction;
        }
        ImGui.Unindent();
    }

    private static void TrackItem(EditorState state, string name, bool changed, Action restore, Action apply)
    {
        if (ImGui.IsItemActivated())
        {
            restore();
            state.Undo?.BeginGesture(state, name);
            apply();
        }
        if (changed && state.Undo == null) state.MarkDirty();
        if (ImGui.IsItemDeactivatedAfterEdit()) state.Undo?.CommitGesture(state);
    }

    private static void DrawStoreVariables(string title,VariableStore store,EditorState state)
    {
        ImGui.SeparatorText(title);
        foreach(var pair in store.ToArray())
        {
            ImGui.PushID(title+pair.Key);string name=pair.Key;
            ImGui.SetNextItemWidth(105);if(ImGui.InputText("##name",ref name,64,ImGuiInputTextFlags.EnterReturnsTrue)&&!string.IsNullOrWhiteSpace(name)&&!store.Contains(name)){string old=pair.Key,next=name.Trim();state.Undo?.Execute(state,"Rename Variable",()=>store.Rename(old,next));}
            ImGui.SameLine();DrawVariableValue(pair.Value,state);ImGui.SameLine();if(ImGui.SmallButton("X")){string key=pair.Key;state.Undo?.Execute(state,"Delete Variable",()=>store.Remove(key));}ImGui.PopID();
        }
        if(ImGui.SmallButton($"+ Variable##{title}")){string name=UniqueName(store.Names,"Variable");state.Undo?.Execute(state,"Add Variable",()=>store.Set(name,VariableValue.FromNumber()));}
    }

    private static void DrawGlobalVariables(List<VariableData> variables,EditorState state)
    {
        ImGui.SeparatorText("GLOBAL VARIABLES");
        foreach(VariableData data in variables.ToArray())
        {
            ImGui.PushID("global"+data.Name);string name=data.Name;ImGui.SetNextItemWidth(105);
            if(ImGui.InputText("##name",ref name,64,ImGuiInputTextFlags.EnterReturnsTrue)&&!string.IsNullOrWhiteSpace(name)&&variables.All(v=>ReferenceEquals(v,data)||!v.Name.Equals(name,StringComparison.OrdinalIgnoreCase))){string next=name.Trim();state.Undo?.Execute(state,"Rename Global Variable",()=>data.Name=next);}
            ImGui.SameLine();DrawVariableValue(data.Value,state);ImGui.SameLine();if(ImGui.SmallButton("X"))state.Undo?.Execute(state,"Delete Global Variable",()=>variables.Remove(data));ImGui.PopID();
        }
        if(ImGui.SmallButton("+ Variable##global")){string name=UniqueName(variables.Select(v=>v.Name),"Variable");state.Undo?.Execute(state,"Add Global Variable",()=>variables.Add(new VariableData{Name=name,Value=VariableValue.FromNumber()}));}
    }

    private static void DrawVariableValue(VariableValue value,EditorState state)
    {
        int type=(int)value.Type;ImGui.SetNextItemWidth(85);if(ImGui.Combo("##type",ref type,"Number\0String\0Boolean\0Vector2\0Vector3\0")){VariableType next=(VariableType)type;state.Undo?.Execute(state,"Change Variable Type",()=>CopyValue(value,VariableValue.Default(next)));}
        ImGui.SameLine();VariableValue old=value.Clone();bool changed=false;
        ImGui.SetNextItemWidth(110);
        switch(value.Type)
        {
            case VariableType.Number:float number=(float)value.Number;changed=ImGui.DragFloat("##value",ref number,.1f);if(changed)value.Number=number;break;
            case VariableType.String:string text=value.String;changed=ImGui.InputText("##value",ref text,128);if(changed)value.String=text;break;
            case VariableType.Boolean:bool boolean=value.Boolean;changed=ImGui.Checkbox("##value",ref boolean);if(changed)value.Boolean=boolean;break;
            case VariableType.Vector2:Vector2 v2=value.Vector2;changed=ImGui.DragFloat2("##value",ref v2,.1f);if(changed)value.Vector2=v2;break;
            case VariableType.Vector3:Vector3 v3=value.Vector3;changed=ImGui.DragFloat3("##value",ref v3,.1f);if(changed)value.Vector3=v3;break;
        }
        VariableValue nextValue=value.Clone();TrackItem(state,"Change Variable Value",changed,()=>CopyValue(value,old),()=>CopyValue(value,nextValue));
    }

    private static void CopyValue(VariableValue target,VariableValue source){target.Type=source.Type;target.Number=source.Number;target.String=source.String;target.Boolean=source.Boolean;target.Vector2=source.Vector2;target.Vector3=source.Vector3;}
    private static string UniqueName(IEnumerable<string> names,string baseName){var set=names.ToHashSet(StringComparer.OrdinalIgnoreCase);if(!set.Contains(baseName))return baseName;int i=2;while(set.Contains(baseName+i))i++;return baseName+i;}

    private static string TextureLabel(AssetReference? reference, AssetDatabase database)
    {
        if (reference == null || reference.IsEmpty) return "None";
        return database.Resolve(reference) is AssetRecord asset ? Path.GetFileName(asset.ProjectPath) : "Missing Asset";
    }

    private static void DrawAssetOrEmpty(EditorState state, EditorProjectContext project)
    {
        AssetRecord? asset = null;
        if (state.SelectedAssetId.HasValue) project.AssetDatabase.TryGetAsset(state.SelectedAssetId.Value, out asset);
        if (asset == null && state.SelectedAssetPath != null) project.AssetDatabase.TryGetAsset(state.SelectedAssetPath, out asset);
        if (asset == null) { ImGui.TextDisabled(state.SelectedAssetPath == null ? "Select a GameObject or asset." : "Missing Asset"); return; }
        ImGui.Text(Path.GetFileName(asset.ProjectPath));
        ImGui.TextDisabled(asset.ProjectPath);
        ImGui.TextDisabled($"GUID: {asset.Guid}");
        if (asset.Type == AssetType.Model3D)
        {
            ImGui.SeparatorText("3D Model");
            ImGui.TextWrapped("The source model is registered in the project. FBX/GLB mesh conversion and scene placement are not implemented yet.");
            return;
        }
        if (asset.Type != AssetType.Texture2D) return;
        ImGui.SeparatorText("Texture Import Settings");
        TextureFilter filter = asset.Metadata.Importer.Filter;
        if (ImGui.BeginCombo("Filter", filter.ToString()))
        {
            foreach (TextureFilter option in Enum.GetValues<TextureFilter>())
            {
                bool selected = filter == option;
                if (ImGui.Selectable(option.ToString(), selected)) project.AssetDatabase.SetTextureFilter(asset.Guid, option);
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
    }
}
