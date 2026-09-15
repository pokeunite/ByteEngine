using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.Classification;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class InspectorPanel
{
    public bool IsOpen { get; set; } = true;
    private string _search = string.Empty;
    private bool? _setExpansion;
    private bool _showAdvanced;
    private string _addSearch = string.Empty;
    private readonly CameraActivationPromptState _cameraActivationPrompt = new();

    public void Draw(EditorState state, EditorProjectContext project, Action<AssetReference> openBlueprint)
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
            _cameraActivationPrompt.Reset();
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
        if (readOnly)
        {
            ImGui.TextColored(new Vector4(1f, .75f, .2f, 1f), "PLAY MODE — Runtime Values");
            ImGui.TextWrapped("Editing runtime instance. Changes reset when stopped.");
        }
        ImGui.BeginDisabled(readOnly && !ReferenceEquals(state.SelectedObject?.Scene, state.RuntimeScene));

        GameObject selected = state.SelectedObject!;

        if (!readOnly && selected.GetComponent<BlueprintInstance>() is { } blueprintInstance)
        {
            BlueprintOverrideSummary summary = BlueprintInstanceSynchronizer.Analyze(selected, project);
            ImGui.SeparatorText("Blueprint Instance");
            ImGui.TextUnformatted(blueprintInstance.Blueprint.CachedProjectPath ?? blueprintInstance.Blueprint.Guid.ToString());
            ImGui.TextDisabled($"INSTANCE OF {Path.GetFileNameWithoutExtension(blueprintInstance.Blueprint.CachedProjectPath ?? "Blueprint")}");
            if (ImGui.Button("Open Blueprint")) openBlueprint(blueprintInstance.Blueprint);
            ImGui.TextUnformatted($"Overrides: {summary.Total}");
            if (summary.ModifiedProperties > 0) ImGui.TextDisabled($"● Properties modified: {summary.ModifiedProperties}");
            if (summary.AddedComponents > 0) ImGui.TextDisabled($"● Components added: {summary.AddedComponents}");
            if (summary.RemovedComponents > 0) ImGui.TextDisabled($"● Components removed: {summary.RemovedComponents}");
            if (summary.AddedChildren > 0) ImGui.TextDisabled($"● Children added: {summary.AddedChildren}");
            if (summary.RemovedChildren > 0) ImGui.TextDisabled($"● Children removed: {summary.RemovedChildren}");
            if (ImGui.Button("Apply Changes"))
            {
                /*
                 * BlueprintInstanceSynchronizer writes the updated asset and
                 * refreshes placed instances, but an already-open Blueprint
                 * workspace keeps its own in-memory preview. Reopen through
                 * the existing callback after a successful apply so the
                 * Blueprint tab immediately reflects what was written.
                 */
                AssetReference blueprintReference =
                    blueprintInstance.Blueprint;

                GameObject? replacement =
                    BlueprintInstanceSynchronizer.Apply(
                        selected,
                        project);

                if (replacement !=
                    null)
                {
                    state.SelectedObject =
                        replacement;

                    state.MarkDirty();

                    openBlueprint(
                        blueprintReference);
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Revert Changes"))
            {
                GameObject? replacement = BlueprintInstanceSynchronizer.Revert(selected, project);
                if (replacement != null) state.SelectedObject = replacement;
                state.MarkDirty();
            }
            ImGui.Separator();
        }

        ImGui.SetNextItemWidth(-38f);
        ImGui.InputTextWithHint("##InspectorSearch", "Search properties...", ref _search, 128);
        ImGui.SameLine();
        if (ImGui.Button("...")) ImGui.OpenPopup("Inspector Options");
        if (ImGui.BeginPopup("Inspector Options"))
        {
            if (ImGui.MenuItem("Collapse All")) _setExpansion = false;
            if (ImGui.MenuItem("Expand All")) _setExpansion = true;
            ImGui.MenuItem("Show Advanced", string.Empty, ref _showAdvanced);
            ImGui.EndPopup();
        }

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

        DrawClassification(state, project.Project.Classification, selected);

        if (_setExpansion.HasValue) ImGui.SetNextItemOpen(_setExpansion.Value, ImGuiCond.Always);
        bool showTransform = string.IsNullOrWhiteSpace(_search) || "Transform Position Rotation Scale".Contains(_search, StringComparison.OrdinalIgnoreCase);
        if (showTransform && ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
        {
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
        }

        ImGui.BeginDisabled(readOnly);
        DrawStoreVariables("OBJECT VARIABLES", selected.Variables, state);
        ImGui.EndDisabled();

        foreach (Component component in selected.Components.ToArray())
        {
            if (!ComponentMetadataRegistry.Matches(component.GetType(), _search)) continue;
            ComponentMetadata metadata = ComponentMetadataRegistry.Get(component.GetType());
            if (!metadata.BeginnerVisible && !_showAdvanced) continue;
            if (_setExpansion.HasValue) ImGui.SetNextItemOpen(_setExpansion.Value, ImGuiCond.Always);
            ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None;
            if (!ImGui.CollapsingHeader($"{metadata.DisplayName}##{component.GetHashCode()}", flags)) continue;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(metadata.Description);
            bool oldEnabled = component.Enabled;
            bool enabled = oldEnabled;
            bool enabledChanged = ImGui.Checkbox($"Enabled##{component.GetHashCode()}", ref enabled);
            if (enabledChanged) component.Enabled = enabled;
            TrackItem(state, "Set Component Enabled", enabledChanged, () => component.Enabled = oldEnabled, () => component.Enabled = enabled);
            ImGui.SameLine();
            ImGui.BeginDisabled(readOnly);

            bool removeRequested =
                ImGui.SmallButton(
                    $"Remove##{component.GetHashCode()}");

            if (removeRequested)
            {
                ExecutePersistent(
                    state,
                    $"Remove {component.GetType().Name}",
                    () => selected.RemoveComponent(component));
            }

            ImGui.EndDisabled();

            /*
             * RemoveComponent destroys and detaches the component immediately.
             * Never continue drawing properties for an already-destroyed
             * component in the same ImGui frame.
             */
            if (removeRequested &&
                !readOnly)
            {
                continue;
            }

            DrawComponentProperties(state, project, component, _showAdvanced);
        }

        _setExpansion = null;

        DrawPhysicsDiagnostics(
            selected);

        ImGui.Separator();
        ImGui.BeginDisabled(readOnly);
        if (ImGui.Button("Add Component")) ImGui.OpenPopup("Add Component Popup");
        if (ImGui.BeginPopup("Add Component Popup"))
        {
            ImGui.InputTextWithHint("##AddComponentSearch", "Search components...", ref _addSearch, 96);
            if (string.IsNullOrWhiteSpace(_addSearch))
            {
                ImGui.SeparatorText("Recommended");
                if (ImGui.MenuItem("Third Person Character"))
                {
                    Camera3D? previousActive = selected.Scene?.ActiveCamera;
                    ExecutePersistent(state, "Setup Third Person Character", () =>
                    {
                        CameraBoom3D boom = BlueprintAuthoringService.SetupThirdPersonCharacter(selected, project.Assets);
                        Camera3D? camera = selected.Scene?.FindGameObject(boom.CameraObjectId)?.GetComponent<Camera3D>();
                        if (camera != null && previousActive != null && !ReferenceEquals(previousActive, camera))
                        {
                            _cameraActivationPrompt.Begin(CameraActivationPrompt.Create(camera, previousActive));
                        }
                    });
                }
                if (ImGui.MenuItem("Health", string.Empty, false, !selected.HasComponent<HealthComponent>()))
                    ExecutePersistent(state, "Add Health", () => BlueprintAuthoringService.AddComponent(selected, new HealthComponent()));
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Adds reusable damage, healing and death state.");
                if (ImGui.MenuItem("Model", string.Empty, false, !selected.HasComponent<ModelHierarchyInstance>()))
                    ExecutePersistent(state, "Add Model", () => BlueprintAuthoringService.AddComponent(selected, new ModelHierarchyInstance()));
                ImGui.Separator();
            }

            ComponentAddMenu.Draw(selected, _addSearch, (component, displayName) =>
            {
                Camera3D? previousActive = component is Camera3D ? selected.Scene?.ActiveCamera : null;
                ExecutePersistent(state, $"Add {displayName}", () =>
                {
                    Component added = BlueprintAuthoringService.AddComponent(selected, component);
                    if (added is Camera3D camera)
                    {
                        _cameraActivationPrompt.Begin(CameraActivationPrompt.Create(camera, previousActive));
                    }
                });
            });
            ImGui.EndPopup();
        }

        DrawCameraActivationPrompt(state);
        ImGui.EndDisabled();

        ImGui.EndDisabled();
        ImGui.End();
    }

    private static void DrawPhysicsDiagnostics(
        GameObject selected)
    {
        ByteEngine.Core.Scene.Scene? scene =
            selected.Scene;

        if (scene ==
            null)
        {
            return;
        }

        bool hasPhysicsData =
            selected.GetComponent<Rigidbody3D>() !=
                null ||
            selected.Components
                .OfType<Collider3D>()
                .Any() ||
            selected.Children
                .Any(
                    child =>
                        child.Components
                            .OfType<Collider3D>()
                            .Any());

        if (!hasPhysicsData)
        {
            return;
        }

        if (!ImGui.CollapsingHeader(
                "Physics Diagnostics##PhysicsDiagnostics",
                ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        PhysicsObjectDiagnostics3D diagnostics =
            scene.Physics.GetDiagnostics(
                selected);

        ImGui.TextDisabled(
            "Read-only values from the active physics world.");

        ImGui.TextUnformatted(
            $"Selected: {diagnostics.SelectedObjectName}");

        ImGui.SeparatorText(
            "Actual Components");

        if (diagnostics.ComponentTypes.Count ==
            0)
        {
            ImGui.TextDisabled(
                "None");
        }
        else
        {
            foreach (string componentType
                     in diagnostics.ComponentTypes)
            {
                ImGui.BulletText(
                    componentType);
            }
        }

        ImGui.SeparatorText(
            "Rigidbody");

        DrawPhysicsBodyDiagnostics(
            "Local Rigidbody",
            diagnostics.LocalBody);

        DrawPhysicsBodyDiagnostics(
            "Resolved Rigidbody",
            diagnostics.ResolvedBody);

        ImGui.SeparatorText(
            $"Colliders ({diagnostics.Colliders.Count})");

        if (diagnostics.Colliders.Count ==
            0)
        {
            ImGui.TextDisabled(
                "No colliders on selected object or descendants.");
        }
        else
        {
            for (int index =
                     0;
                 index <
                 diagnostics.Colliders.Count;
                 index++)
            {
                PhysicsColliderDiagnostics3D collider =
                    diagnostics.Colliders[index];

                ImGui.PushID(
                    $"PhysicsColliderDiagnostic{index}");

                ImGui.TextUnformatted(
                    $"{collider.ObjectName} / {collider.ColliderType}");

                ImGui.TextDisabled(
                    $"Enabled={collider.Enabled} Trigger={collider.IsTrigger}");

                ImGui.TextDisabled(
                    $"Resolved Body: {collider.ResolvedBodyObject}");

                ImGui.TextDisabled(
                    collider.ResolvedRestitution.HasValue
                        ? $"Body Type={collider.ResolvedBodyType} Restitution={collider.ResolvedRestitution.Value:0.###}"
                        : "Body Type=Static Collider Restitution=0");

                ImGui.PopID();
            }
        }

        ImGui.SeparatorText(
            $"Active Contacts ({diagnostics.Contacts.Count})");

        if (diagnostics.Contacts.Count ==
            0)
        {
            ImGui.TextDisabled(
                "No active contact involving this object/body on the last physics frame.");
        }
        else
        {
            for (int index =
                     0;
                 index <
                 diagnostics.Contacts.Count;
                 index++)
            {
                PhysicsContactDiagnostics3D contact =
                    diagnostics.Contacts[index];

                ImGui.PushID(
                    $"PhysicsContactDiagnostic{index}");

                ImGui.TextUnformatted(
                    $"{contact.ColliderObject} -> {contact.OtherObject}");

                ImGui.TextDisabled(
                    $"{contact.ColliderType} vs {contact.OtherColliderType} Trigger={contact.IsTrigger}");

                ImGui.TextDisabled(
                    $"Bodies: {contact.BodyA} / {contact.BodyB}");

                ImGui.TextDisabled(
                    $"Penetration={contact.Penetration:0.0000}");

                ImGui.TextDisabled(
                    $"Normal=({contact.NormalTowardSelected.X:0.###}, {contact.NormalTowardSelected.Y:0.###}, {contact.NormalTowardSelected.Z:0.###})");

                ImGui.TextDisabled(
                    $"Restitution Used={contact.RestitutionUsed:0.###}");

                ImGui.TextDisabled(
                    $"Relative Normal Velocity={contact.RelativeNormalVelocity:0.###}");

                ImGui.TextDisabled(
                    $"Normal Impulse={contact.NormalImpulseMagnitude:0.###} Applied={contact.SolverAppliedImpulse}");

                ImGui.PopID();
            }
        }
    }

    private static void DrawPhysicsBodyDiagnostics(
        string label,
        PhysicsBodyDiagnostics3D? body)
    {
        if (body ==
            null)
        {
            ImGui.TextDisabled(
                $"{label}: NONE");

            return;
        }

        ImGui.TextUnformatted(
            $"{label}: {body.ObjectName}");

        ImGui.TextDisabled(
            $"Type={body.BodyType} Enabled={body.Enabled} Mass={body.Mass:0.###}");

        ImGui.TextDisabled(
            $"Gravity={body.UseGravity} Scale={body.GravityScale:0.###} Damping={body.LinearDamping:0.###}");

        ImGui.TextDisabled(
            $"Restitution={body.Restitution:0.###} Friction={body.Friction:0.###}");

        ImGui.TextDisabled(
            $"Velocity=({body.Velocity.X:0.###}, {body.Velocity.Y:0.###}, {body.Velocity.Z:0.###})");
    }

    private void DrawCameraActivationPrompt(EditorState state)
    {
        if (_cameraActivationPrompt.ConsumeOpenRequest())
        {
            ImGui.OpenPopup("Active Game Camera##InspectorCameraPrompt");
        }
        if (_cameraActivationPrompt.Request is not { } request) return;
        if (!ImGui.BeginPopupModal("Active Game Camera##InspectorCameraPrompt", ImGuiWindowFlags.AlwaysAutoResize))
        {
            _cameraActivationPrompt.RecoverWhenNotVisible();
            return;
        }
        _cameraActivationPrompt.MarkVisible();

        if (request.Kind == CameraActivationPromptKind.UseAsFirstCamera)
            ImGui.TextWrapped("Use this Camera as the Active Game Camera?");
        else
        {
            ImGui.TextUnformatted($"Active Game Camera: {request.PreviousActiveCamera?.GameObject.Name ?? "None"}");
            ImGui.TextWrapped("Make this Camera active instead?");
        }

        string accept = request.Kind == CameraActivationPromptKind.UseAsFirstCamera ? "Yes" : "Make Active";
        string reject = request.Kind == CameraActivationPromptKind.UseAsFirstCamera ? "No" : "Keep Current";
        if (ImGui.Button(accept))
        {
            CameraActivationPrompt.Apply(request.Camera.GameObject.Scene, request, true);
            state.MarkDirty();
            _cameraActivationPrompt.Reset();
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button(reject))
        {
            _cameraActivationPrompt.Reset();
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private static void DrawClassification(EditorState state, ClassificationSettings settings, GameObject selected)
    {
        ImGui.SeparatorText("Classification");
        ImGui.TextUnformatted("Tags");
        foreach (Guid tagId in selected.Tags.ToArray())
        {
            TagDefinition? tag = settings.FindTag(tagId);
            if (tag == null) ImGui.TextColored(new Vector4(1f, .65f, .2f, 1f), $"Missing Tag: {tagId}");
            else ImGui.TextUnformatted(tag.Name);
            ImGui.SameLine();
            if (ImGui.SmallButton($"�##RemoveTag{tagId}"))
                ApplyClassification(state, $"Remove Tag {tag?.Name ?? tagId.ToString()}", () => selected.RemoveTag(tagId));
        }
        if (ImGui.BeginCombo("##AddTag", "+ Add Tag"))
        {
            foreach (TagDefinition tag in settings.Tags.OrderBy(item => item.Name).Where(item => !selected.HasTag(item.Id)))
                if (ImGui.Selectable($"{tag.Name}##Add{tag.Id}"))
                    ApplyClassification(state, $"Add Tag {tag.Name}", () => selected.AddTag(tag.Id));
            ImGui.EndCombo();
        }

        int layer = selected.Layer;
        if (ClassificationPickers.DrawLayer("Layer", settings, ref layer))
        {
            int selectedLayer = layer;
            ApplyClassification(state, $"Set Layer {settings.FindLayer(layer)?.Name}", () => selected.Layer = selectedLayer);
        }
    }

    private static void ApplyClassification(EditorState state, string name, Action action)
    {
        if (state.Mode == EditorMode.Edit) ExecutePersistent(state, name, action);
        else action();
    }

    private static void DrawComponentProperties(EditorState state, EditorProjectContext project, Component component, bool showAdvanced)
    {
        bool runtime = state.Mode != EditorMode.Edit;
        ComponentPropertyRenderer.Draw(component,
            runtime ? PropertyEditorContext.Runtime : PropertyEditorContext.Scene, showAdvanced,
            () => { if (!runtime) state.Undo?.BeginGesture(state, $"Edit {component.GetType().Name}"); },
            state.MarkDirty,
            () => { if (!runtime) state.Undo?.CommitGesture(state); }, project);
    }

    private static void TrackItem(EditorState state, string name, bool changed, Action restore, Action apply)
    {
        if (state.Mode != EditorMode.Edit) return;
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
            ExecutePersistent(
                state,
                "Add Variable",
                () => store.Set(name, VariableValue.FromNumber()));
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
