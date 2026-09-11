using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Animation;
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
                ExecutePersistent(
                    state,
                    $"Remove {component.GetType().Name}",
                    () => selected.RemoveComponent(component));
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
            DrawAddComponentItem<CapsuleCollider3D>("CapsuleCollider3D", selected, state, () => new CapsuleCollider3D());
            DrawAddComponentItem<GroundSurface>("GroundSurface", selected, state, () => new GroundSurface());
            DrawAddComponentItem<CharacterController3D>("CharacterController3D", selected, state, () => new CharacterController3D());
            DrawAddComponentItem<AnimationController>("AnimationController", selected, state, () => new AnimationController());
            DrawAddComponentItem<SkeletalMeshRenderer>("SkeletalMeshRenderer", selected, state, () => new SkeletalMeshRenderer());
            ImGui.EndPopup();
        }

        ImGui.EndDisabled();
        ImGui.End();
    }

    private static void DrawAddComponentItem<T>(string name, GameObject target, EditorState state, Func<T> factory) where T : Component
    {
        bool exists = target.HasComponent<T>();
        if (ImGui.MenuItem(name, string.Empty, false, !exists))
            ExecutePersistent(state, $"Add {name}", () => target.AddComponent(factory()));
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
                    ExecutePersistent(state, "Change Sprite Texture", () =>
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
                ExecutePersistent(state, "Clear Sprite Texture", () =>
                {
                    sprite.TextureReference = null;
                    sprite.Texture = null;
                });

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

            Vector2 oldSize = sprite.Size;
            Vector2 size = oldSize;
            bool sizeChanged = ImGui.DragFloat2(
                $"Size##{component.GetHashCode()}", ref size, 1f, .001f, 10000f);
            Vector2 finalSize = Vector2.Max(size, new Vector2(.001f));
            if (sizeChanged) sprite.Size = finalSize;
            TrackItem(
                state,
                "Change Sprite Size",
                sizeChanged,
                () => sprite.Size = oldSize,
                () => sprite.Size = finalSize);
        }
        else if (component is MeshRenderer mesh)
        {
            int primitive = (int)mesh.Primitive;
            if (ImGui.Combo(
                    $"Primitive##{component.GetHashCode()}",
                    ref primitive,
                    "Cube\0Plane\0Sphere\0"))
            {
                PrimitiveMeshType next = (PrimitiveMeshType)primitive;
                ExecutePersistent(state, "Change Mesh Primitive", () => mesh.Primitive = next);
            }

            Vector4 oldColor = mesh.Material.BaseColor;
            Vector4 color = oldColor;
            bool colorChanged = ImGui.ColorEdit4($"Base Color##{component.GetHashCode()}", ref color);
            if (colorChanged) mesh.Material.BaseColor = color;
            TrackItem(
                state,
                "Change Material Color",
                colorChanged,
                () => mesh.Material.BaseColor = oldColor,
                () => mesh.Material.BaseColor = color);

            DrawBooleanProperty(
                state,
                $"Visible##mesh{component.GetHashCode()}",
                "Set Mesh Visibility",
                () => mesh.Visible,
                value => mesh.Visible = value);
        }
        else if (component is Camera3D camera3D)
        {
            DrawFloatProperty(state, $"Field of View##{component.GetHashCode()}", "Change Field of View",
                () => camera3D.FieldOfView, value => camera3D.FieldOfView = value, .25f, 1f, 179f);
            DrawFloatProperty(state, $"Near Clip##{component.GetHashCode()}", "Change Near Clip",
                () => camera3D.NearClip, value => camera3D.NearClip = value, .01f, .001f, 100f);
            DrawFloatProperty(state, $"Far Clip##{component.GetHashCode()}", "Change Far Clip",
                () => camera3D.FarClip, value => camera3D.FarClip = value, 1f, 1f, 100000f);
        }
        else if (component is DirectionalLight light)
        {
            Vector3 oldColor = light.Color;
            Vector3 color = oldColor;
            bool colorChanged = ImGui.ColorEdit3($"Color##{component.GetHashCode()}", ref color);
            if (colorChanged) light.Color = color;
            TrackItem(
                state,
                "Change Light Color",
                colorChanged,
                () => light.Color = oldColor,
                () => light.Color = color);

            DrawFloatProperty(state, $"Intensity##{component.GetHashCode()}", "Change Light Intensity",
                () => light.Intensity, value => light.Intensity = value, .02f, 0f, 100f);
            DrawFloatProperty(state, $"Ambient Intensity##{component.GetHashCode()}", "Change Ambient Intensity",
                () => light.AmbientIntensity, value => light.AmbientIntensity = value, .01f, 0f, 1f);
        }
        else if (component is GroundSurface ground)
        {
            DrawBooleanProperty(state, $"Walkable##{component.GetHashCode()}", "Set Surface Walkable",
                () => ground.Walkable, value => ground.Walkable = value);
            DrawStringProperty(state, $"Surface Type##{component.GetHashCode()}", "Change Surface Type",
                () => ground.SurfaceType, value => ground.SurfaceType = value, 64);
            DrawFloatProperty(state, $"Friction##{component.GetHashCode()}", "Change Surface Friction",
                () => ground.Friction, value => ground.Friction = value, .02f, 0f, 10f);
        }
        else if (component is BoxCollider3D collider)
        {
            Vector3 oldSize = collider.Size;
            Vector3 size = oldSize;
            bool sizeChanged = ImGui.DragFloat3(
                $"Size##collider{component.GetHashCode()}", ref size, .02f, .001f, 10000f);
            Vector3 finalSize = Vector3.Max(size, new Vector3(.001f));
            if (sizeChanged) collider.Size = finalSize;
            TrackItem(state, "Change Collider Size", sizeChanged,
                () => collider.Size = oldSize, () => collider.Size = finalSize);

            Vector3 oldCenter = collider.Center;
            Vector3 center = oldCenter;
            bool centerChanged = ImGui.DragFloat3(
                $"Center##collider{component.GetHashCode()}", ref center, .02f);
            if (centerChanged) collider.Center = center;
            TrackItem(state, "Change Collider Center", centerChanged,
                () => collider.Center = oldCenter, () => collider.Center = center);

            DrawBooleanProperty(state, $"Is Trigger##{component.GetHashCode()}", "Set Collider Trigger",
                () => collider.IsTrigger, value => collider.IsTrigger = value);
        }
        else if (component is CapsuleCollider3D capsule)
        {
            DrawFloatProperty(state, $"Radius##{component.GetHashCode()}", "Change Capsule Radius",
                () => capsule.Radius, value => capsule.Radius = value, .01f, .001f, 1000f);
            DrawFloatProperty(state, $"Height##{component.GetHashCode()}", "Change Capsule Height",
                () => capsule.Height, value => capsule.Height = value, .02f, .002f, 1000f);

            Vector3 oldCenter = capsule.Center;
            Vector3 center = oldCenter;
            bool centerChanged = ImGui.DragFloat3(
                $"Center##capsule{component.GetHashCode()}", ref center, .02f);
            if (centerChanged) capsule.Center = center;
            TrackItem(state, "Change Capsule Center", centerChanged,
                () => capsule.Center = oldCenter, () => capsule.Center = center);
            DrawBooleanProperty(state, $"Is Trigger##capsule{component.GetHashCode()}", "Set Capsule Trigger",
                () => capsule.IsTrigger, value => capsule.IsTrigger = value);
        }
        else if (component is CharacterController3D controller)
        {
            DrawFloatProperty(state, $"Move Speed##{component.GetHashCode()}", "Change Move Speed",
                () => controller.MoveSpeed, value => controller.MoveSpeed = value, .05f, 0f, 1000f);
            DrawFloatProperty(state, $"Acceleration##{component.GetHashCode()}", "Change Acceleration",
                () => controller.Acceleration, value => controller.Acceleration = value, .1f, 0f, 1000f);
            DrawFloatProperty(state, $"Deceleration##{component.GetHashCode()}", "Change Deceleration",
                () => controller.Deceleration, value => controller.Deceleration = value, .1f, 0f, 1000f);
            DrawFloatProperty(state, $"Air Control##{component.GetHashCode()}", "Change Air Control",
                () => controller.AirControl, value => controller.AirControl = value, .01f, 0f, 1f);
            DrawFloatProperty(state, $"Jump Force##{component.GetHashCode()}", "Change Jump Force",
                () => controller.JumpForce, value => controller.JumpForce = value, .05f, 0f, 1000f);
            DrawFloatProperty(state, $"Gravity##{component.GetHashCode()}", "Change Gravity",
                () => controller.Gravity, value => controller.Gravity = value, .1f, 0f, 1000f);
            DrawFloatProperty(state, $"Ground Distance##{component.GetHashCode()}", "Change Ground Distance",
                () => controller.GroundDistance, value => controller.GroundDistance = value, .01f, 0f, 100f);
            DrawFloatProperty(state, $"Max Slope##{component.GetHashCode()}", "Change Max Slope",
                () => controller.MaxSlope, value => controller.MaxSlope = value, .25f, 0f, 90f);
            DrawFloatProperty(state, $"Step Height##{component.GetHashCode()}", "Change Step Height",
                () => controller.StepHeight, value => controller.StepHeight = value, .01f, 0f, 100f);
            DrawFloatProperty(state, $"Coyote Time##{component.GetHashCode()}", "Change Coyote Time",
                () => controller.CoyoteTime, value => controller.CoyoteTime = value, .01f, 0f, 10f);
            DrawFloatProperty(state, $"Jump Buffer##{component.GetHashCode()}", "Change Jump Buffer",
                () => controller.JumpBuffer, value => controller.JumpBuffer = value, .01f, 0f, 10f);
            DrawBooleanProperty(state, $"Snap To Ground##{component.GetHashCode()}", "Set Snap To Ground",
                () => controller.SnapToGround, value => controller.SnapToGround = value);
        }
        else if (component is AnimationController animation)
        {
            DrawStringProperty(state, $"Idle##{component.GetHashCode()}", "Change Idle Animation",
                () => animation.Idle, value => animation.Idle = value, 128);
            DrawStringProperty(state, $"Walk##{component.GetHashCode()}", "Change Walk Animation",
                () => animation.Walk, value => animation.Walk = value, 128);
            DrawStringProperty(state, $"Run##{component.GetHashCode()}", "Change Run Animation",
                () => animation.Run, value => animation.Run = value, 128);
            DrawStringProperty(state, $"Jump##{component.GetHashCode()}", "Change Jump Animation",
                () => animation.Jump, value => animation.Jump = value, 128);
            DrawStringProperty(state, $"Fall##{component.GetHashCode()}", "Change Fall Animation",
                () => animation.Fall, value => animation.Fall = value, 128);
            DrawStringProperty(state, $"Land##{component.GetHashCode()}", "Change Land Animation",
                () => animation.Land, value => animation.Land = value, 128);
            DrawFloatProperty(state, $"Run Threshold##{component.GetHashCode()}", "Change Run Threshold",
                () => animation.RunThreshold, value => animation.RunThreshold = value, .05f, 0f, 1000f);
        }
        else if (component is SkeletalMeshRenderer skeletal)
        {
            ImGui.TextDisabled($"Model: {skeletal.Model}");
            ImGui.TextDisabled($"Skeleton: {skeletal.SkeletonKey ?? "None"}");
            DrawBooleanProperty(state, $"Visible##skeletal{component.GetHashCode()}", "Set Skeletal Mesh Visibility",
                () => skeletal.Visible, value => skeletal.Visible = value);
        }
        ImGui.Unindent();
    }

    private static void DrawFloatProperty(
        EditorState state,
        string label,
        string undoName,
        Func<float> read,
        Action<float> write,
        float speed,
        float minimum,
        float maximum)
    {
        float oldValue = read();
        float value = oldValue;
        bool changed = ImGui.DragFloat(label, ref value, speed, minimum, maximum);
        if (changed) write(value);
        TrackItem(state, undoName, changed, () => write(oldValue), () => write(value));
    }

    private static void DrawBooleanProperty(
        EditorState state,
        string label,
        string undoName,
        Func<bool> read,
        Action<bool> write)
    {
        bool oldValue = read();
        bool value = oldValue;
        bool changed = ImGui.Checkbox(label, ref value);
        if (changed) write(value);
        TrackItem(state, undoName, changed, () => write(oldValue), () => write(value));
    }

    private static void DrawStringProperty(
        EditorState state,
        string label,
        string undoName,
        Func<string> read,
        Action<string> write,
        uint maximumLength)
    {
        string oldValue = read();
        string value = oldValue;
        bool changed = ImGui.InputText(label, ref value, maximumLength);
        if (changed) write(value);
        TrackItem(state, undoName, changed, () => write(oldValue), () => write(value));
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

    private static void ExecutePersistent(EditorState state, string name, Action action)
    {
        if (state.Undo != null)
        {
            state.Undo.Execute(state, name, action);
            return;
        }

        action();
        state.MarkDirty();
    }

    private static void DrawStoreVariables(string title, VariableStore store, EditorState state)
    {
        ImGui.SeparatorText(title);
        foreach (KeyValuePair<string, VariableValue> pair in store.ToArray())
        {
            ImGui.PushID(title + pair.Key);
            string name = pair.Key;
            ImGui.SetNextItemWidth(105f);
            if (ImGui.InputText("##name", ref name, 64, ImGuiInputTextFlags.EnterReturnsTrue) &&
                !string.IsNullOrWhiteSpace(name) &&
                !store.Contains(name))
            {
                string oldName = pair.Key;
                string nextName = name.Trim();
                ExecutePersistent(state, "Rename Variable", () => store.Rename(oldName, nextName));
            }

            ImGui.SameLine();
            DrawVariableValue(pair.Value, state);
            ImGui.SameLine();
            if (ImGui.SmallButton("X"))
            {
                string key = pair.Key;
                ExecutePersistent(state, "Delete Variable", () => store.Remove(key));
            }
            ImGui.PopID();
        }

        if (ImGui.SmallButton($"+ Variable##{title}"))
        {
            string name = UniqueName(store.Names, "Variable");
            ExecutePersistent(state, "Add Variable", () => store.Set(name, VariableValue.FromNumber()));
        }
    }

    private static void DrawGlobalVariables(List<VariableData> variables, EditorState state)
    {
        ImGui.SeparatorText("GLOBAL VARIABLES");
        foreach (VariableData data in variables.ToArray())
        {
            ImGui.PushID("global" + data.Name);
            string name = data.Name;
            ImGui.SetNextItemWidth(105f);
            if (ImGui.InputText("##name", ref name, 64, ImGuiInputTextFlags.EnterReturnsTrue) &&
                !string.IsNullOrWhiteSpace(name) &&
                variables.All(value =>
                    ReferenceEquals(value, data) ||
                    !value.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                string nextName = name.Trim();
                ExecutePersistent(state, "Rename Global Variable", () => data.Name = nextName);
            }

            ImGui.SameLine();
            DrawVariableValue(data.Value, state);
            ImGui.SameLine();
            if (ImGui.SmallButton("X"))
                ExecutePersistent(state, "Delete Global Variable", () => variables.Remove(data));
            ImGui.PopID();
        }

        if (ImGui.SmallButton("+ Variable##global"))
        {
            string name = UniqueName(variables.Select(value => value.Name), "Variable");
            ExecutePersistent(
                state,
                "Add Global Variable",
                () => variables.Add(new VariableData { Name = name, Value = VariableValue.FromNumber() }));
        }
    }

    private static void DrawVariableValue(VariableValue value, EditorState state)
    {
        int type = (int)value.Type;
        ImGui.SetNextItemWidth(85f);
        if (ImGui.Combo("##type", ref type, "Number\0String\0Boolean\0Vector2\0Vector3\0"))
        {
            VariableType nextType = (VariableType)type;
            ExecutePersistent(
                state,
                "Change Variable Type",
                () => CopyValue(value, VariableValue.Default(nextType)));
        }

        ImGui.SameLine();
        VariableValue oldValue = value.Clone();
        bool changed = false;
        ImGui.SetNextItemWidth(110f);
        switch (value.Type)
        {
            case VariableType.Number:
                float number = (float)value.Number;
                changed = ImGui.DragFloat("##value", ref number, .1f);
                if (changed) value.Number = number;
                break;
            case VariableType.String:
                string text = value.String;
                changed = ImGui.InputText("##value", ref text, 128);
                if (changed) value.String = text;
                break;
            case VariableType.Boolean:
                bool boolean = value.Boolean;
                changed = ImGui.Checkbox("##value", ref boolean);
                if (changed) value.Boolean = boolean;
                break;
            case VariableType.Vector2:
                Vector2 vector2 = value.Vector2;
                changed = ImGui.DragFloat2("##value", ref vector2, .1f);
                if (changed) value.Vector2 = vector2;
                break;
            case VariableType.Vector3:
                Vector3 vector3 = value.Vector3;
                changed = ImGui.DragFloat3("##value", ref vector3, .1f);
                if (changed) value.Vector3 = vector3;
                break;
        }

        VariableValue nextValue = value.Clone();
        TrackItem(
            state,
            "Change Variable Value",
            changed,
            () => CopyValue(value, oldValue),
            () => CopyValue(value, nextValue));
    }

    private static void CopyValue(VariableValue target, VariableValue source)
    {
        target.Type = source.Type;
        target.Number = source.Number;
        target.String = source.String;
        target.Boolean = source.Boolean;
        target.Vector2 = source.Vector2;
        target.Vector3 = source.Vector3;
    }

    private static string UniqueName(IEnumerable<string> names, string baseName)
    {
        HashSet<string> usedNames = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!usedNames.Contains(baseName)) return baseName;
        int suffix = 2;
        while (usedNames.Contains(baseName + suffix)) suffix++;
        return baseName + suffix;
    }

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
            ImGui.SeparatorText("MODEL IMPORT SETTINGS");
            try
            {
                ModelAsset model = project.Assets.LoadModel(new AssetReference(asset.Guid, asset.ProjectPath));
                ImGui.Text($"Source: {Path.GetFileName(asset.ProjectPath)}");
                ImGui.Text($"Meshes: {model.Meshes.Count}");
                ImGui.Text($"Materials: {model.Materials.Count}");
                ImGui.Text($"Skeleton: {model.Skeleton?.Name ?? "None"}");
                ImGui.Text($"Animations: {model.Animations.Count}");

                float scale = asset.Metadata.ModelImporter.ImportScale;
                bool generateNormals = asset.Metadata.ModelImporter.GenerateNormals;
                bool embeddedMaterials = asset.Metadata.ModelImporter.PreferEmbeddedMaterials;
                bool changed = ImGui.DragFloat("Import Scale", ref scale, .01f, .0001f, 1000f);
                changed |= ImGui.Checkbox("Generate Normals", ref generateNormals);
                changed |= ImGui.Checkbox("Prefer Embedded Materials", ref embeddedMaterials);
                if (changed)
                    project.AssetDatabase.SetModelImporterSettings(
                        asset.Guid, scale, generateNormals, embeddedMaterials);
                if (ImGui.Button("Reimport")) project.Assets.ReimportModel(asset.Guid);
            }
            catch (Exception exception)
            {
                ImGui.TextColored(new Vector4(1f, .35f, .35f, 1f), "Model import failed");
                ImGui.TextWrapped(exception.Message);
            }
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
