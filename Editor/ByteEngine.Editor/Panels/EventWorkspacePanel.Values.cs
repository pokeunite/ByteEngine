using System.Numerics;
using System.Reflection;

using ByteEngine.Core;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Core.Classification;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed partial class EventWorkspacePanel
{
    private void DrawAnimationActionArgument(VisualInstruction instruction, EditorState? state, string label = "Action")
    {
        GameObject? target = state != null ? ResolveAnimationTargetForEditor(instruction, state) : null;
        AnimationController? controller = AnimationController.FindForObject(target);
        if (_project == null || controller == null || controller.AnimationProfile.IsEmpty)
        {
            DrawValueArgument(instruction, "action", label + " (manual)", VariableType.String,
                EventValue.String(string.Empty), state, false);
            return;
        }

        AnimationProfile profile;
        try { profile = _project.Assets.LoadAnimationProfile(controller.AnimationProfile); }
        catch
        {
            DrawValueArgument(instruction, "action", label + " (manual)", VariableType.String,
                EventValue.String(string.Empty), state, false);
            return;
        }

        EventValue value = EnsureAnimationSignalString(instruction, "action");
        if (value.Kind != EventValueKind.Constant)
        {
            DrawValueArgument(instruction, "action", label, VariableType.String,
                EventValue.String(string.Empty), state, false);
            return;
        }

        string current = value.Constant.String;
        ImGui.TextDisabled(label);
        ImGui.SetNextItemWidth(-1.0f);
        if (ImGui.BeginCombo("##NamedAnimationAction", string.IsNullOrWhiteSpace(current) ? "Select Action" : current))
        {
            foreach (AnimationActionProfile action in profile.Actions.Where(a => !string.IsNullOrWhiteSpace(a.Name)))
            {
                if (ImGui.Selectable(action.Name, string.Equals(current, action.Name, StringComparison.OrdinalIgnoreCase)))
                    SetAnimationSignalString(instruction, "action", action.Name, "Change Animation Action");
            }
            ImGui.EndCombo();
        }
        if (!string.IsNullOrWhiteSpace(current) && !profile.Actions.Any(a => string.Equals(a.Name, current, StringComparison.OrdinalIgnoreCase)))
            DrawStaleAnimationValueWarning("action", current, "Animation Profile");
    }
    private void DrawAnimationClipArgument(
        VisualInstruction instruction,
        string argumentName,
        string label,
        EditorState? state)
    {
        if (!instruction.Arguments.TryGetValue(
                argumentName,
                out EventValue? value) ||
            value == null)
        {
            value =
                EventValue.String(
                    string.Empty);

            instruction.Arguments[argumentName] =
                value;
        }

        ImGui.PushID(
            $"AnimationClip:{argumentName}");

        ImGui.TextDisabled(
            label);

        string source =
            value.Kind ==
            EventValueKind.Reference
                ? "Reference"
                : "Constant";

        ImGui.SetNextItemWidth(
            -1.0f);

        if (ImGui.BeginCombo(
                "##Source",
                source))
        {
            if (ImGui.Selectable(
                    "Constant",
                    value.Kind ==
                    EventValueKind.Constant))
            {
                value =
                    EventValue.String(
                        string.Empty);

                instruction.Arguments[argumentName] =
                    value;

                _dirty =
                    true;
            }

            if (ImGui.Selectable(
                    "Reference",
                    value.Kind ==
                    EventValueKind.Reference))
            {
                value =
                    new EventValue
                    {
                        Kind =
                            EventValueKind.Reference
                    };

                instruction.Arguments[argumentName] =
                    value;

                _dirty =
                    true;
            }

            ImGui.EndCombo();
        }

        ImGui.Dummy(
            new Vector2(
                0.0f,
                2.0f));

        if (value.Kind ==
            EventValueKind.Reference)
        {
            DrawReferenceValue(
                instruction,
                argumentName,
                value,
                VariableType.String,
                state,
                false);

            ImGui.PopID();

            return;
        }

        if (value.Constant.Type !=
            VariableType.String)
        {
            value =
                EventValue.String(
                    string.Empty);

            instruction.Arguments[argumentName] =
                value;
        }

        string current =
            value.Constant.String;

        GameObject? target =
            state != null
                ? ResolveAnimationTargetForEditor(
                    instruction,
                    state)
                : null;

        AnimationController? controller =
            AnimationController.FindForObject(target);

        IReadOnlyList<string> clipNames =
            controller != null &&
            _project != null
                ? ByteEngine.Editor.AnimationClipDiscovery.GetClipNames(
                    controller,
                    _project)
                : Array.Empty<string>();

        string preview =
            string.IsNullOrWhiteSpace(
                current)
                ? "Select Animation Clip..."
                : current;

        ImGui.SetNextItemWidth(
            -1.0f);

        if (ImGui.BeginCombo(
                "##AnimationClip",
                preview))
        {
            bool noneSelected =
                string.IsNullOrWhiteSpace(
                    current);

            if (ImGui.Selectable(
                    "<None>",
                    noneSelected))
            {
                RecordHistory(
                    "Change Animation Clip");

                instruction.Arguments[argumentName] =
                    EventValue.String(
                        string.Empty);

                current =
                    string.Empty;

                _dirty =
                    true;
            }

            if (clipNames.Count >
                0)
            {
                ImGui.Separator();

                foreach (string clipName
                         in clipNames)
                {
                    bool selected =
                        string.Equals(
                            current,
                            clipName,
                            StringComparison.OrdinalIgnoreCase);

                    if (ImGui.Selectable(
                            clipName,
                            selected))
                    {
                        RecordHistory(
                            "Change Animation Clip");

                        instruction.Arguments[argumentName] =
                            EventValue.String(
                                clipName);

                        current =
                            clipName;

                        _dirty =
                            true;
                    }

                    if (selected)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }
            }
            else
            {
                ImGui.Separator();

                ImGui.TextDisabled(
                    "No animation clips found for the target object.");
            }

            ImGui.EndCombo();
        }

        if (state ==
            null)
        {
            ImGui.TextDisabled(
                "No active editor state.");
        }
        else if (target ==
            null)
        {
            ImGui.TextDisabled(
                "Choose a valid Target Object first.");
        }
        else if (controller ==
            null)
        {
            ImGui.TextDisabled(
                $"'{target.Name}' has no AnimationController.");
        }
        else if (clipNames.Count ==
            0)
        {
            ImGui.TextDisabled(
                "The target AnimationController has no discoverable model clips.");
        }

        ImGui.PopID();
    }

    private GameObject? ResolveAnimationTargetForEditor(
        VisualInstruction instruction,
        EditorState state)
    {
        GameObject? self =
            ResolveSelfContext(
                state);

        string token =
            "Self";

        if (instruction.Arguments.TryGetValue(
                "target",
                out EventValue? targetValue) &&
            targetValue != null &&
            targetValue.Kind ==
                EventValueKind.Constant &&
            targetValue.Constant.Type ==
                VariableType.String &&
            !string.IsNullOrWhiteSpace(
                targetValue.Constant.String))
        {
            token =
                targetValue.Constant.String;
        }

        if (string.Equals(
                token,
                "Self",
                StringComparison.OrdinalIgnoreCase))
        {
            return self;
        }

        if (token.StartsWith(
                "id:",
                StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(
                token[3..],
                out Guid objectId))
        {
            return state.EditorScene.FindGameObject(
                objectId);
        }

        return state.EditorScene.FindGameObject(
            token);
    }

    // ========================================================
    // GENERIC VALUE EDITOR
    // ========================================================

    private VariableType? TryGetSelectedTargetVariableType(
        VisualInstruction instruction,
        EditorState? state)
    {
        if (state == null ||
            !instruction.Arguments.TryGetValue("target", out EventValue? target) ||
            target.Kind != EventValueKind.Reference ||
            target.Reference == null)
            return null;

        VariableReference reference = target.Reference;
        VariableValue? variable = null;
        switch (reference.Scope)
        {
            case VariableScope.Global:
                variable = state.Project.GlobalVariables
                    .FirstOrDefault(item => item.Name.Equals(
                        reference.MemberName, StringComparison.OrdinalIgnoreCase))?.Value;
                break;
            case VariableScope.Scene:
                state.EditorScene.Variables.TryGet(reference.MemberName, out variable);
                break;
            case VariableScope.Self:
                ResolveSelfContext(state)?.Variables.TryGet(reference.MemberName, out variable);
                break;
            case VariableScope.Object:
                GameObject? owner = reference.ObjectId.HasValue
                    ? state.EditorScene.FindGameObject(reference.ObjectId.Value)
                    : state.EditorScene.FindGameObject(reference.ObjectName ?? string.Empty);
                owner?.Variables.TryGet(reference.MemberName, out variable);
                break;
            case VariableScope.Component:
                return TryGetComponentReferenceType(reference);
        }

        return variable?.Type;
    }

    private static VariableType? TryGetComponentReferenceType(VariableReference reference)
    {
        if (string.IsNullOrWhiteSpace(reference.ComponentType) ||
            string.IsNullOrWhiteSpace(reference.MemberName))
            return null;

        Type? type = reference.ComponentType.Equals("Transform", StringComparison.OrdinalIgnoreCase)
            ? typeof(Transform)
            : ComponentReferenceTypes.Value.FirstOrDefault(candidate =>
                candidate.Name.Equals(reference.ComponentType, StringComparison.OrdinalIgnoreCase) ||
                candidate.FullName?.Equals(reference.ComponentType, StringComparison.OrdinalIgnoreCase) == true);
        if (type == null)
            return null;

        string[] path = reference.MemberName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        foreach (string segment in path)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
            type = type.GetProperty(segment, flags)?.PropertyType ??
                   type.GetField(segment, flags)?.FieldType;
            if (type == null)
                return null;
        }

        if (type == typeof(bool)) return VariableType.Boolean;
        if (type == typeof(string)) return VariableType.String;
        if (type == typeof(Vector2)) return VariableType.Vector2;
        if (type == typeof(Vector3)) return VariableType.Vector3;
        if (type.IsPrimitive || type == typeof(decimal)) return VariableType.Number;
        return null;
    }

    private static readonly Lazy<Type[]> ComponentReferenceTypes = new(() =>
    {
        try { return typeof(Component).Assembly.GetTypes(); }
        catch (ReflectionTypeLoadException error)
        {
            return error.Types.OfType<Type>().ToArray();
        }
    });

    private void DrawValueArgument(
        VisualInstruction instruction,
        string argumentName,
        string label,
        VariableType? expectedType,
        EventValue fallback,
        EditorState? state,
        bool writableReference)
    {
        if (!instruction.Arguments.TryGetValue(
                argumentName,
                out EventValue? value) ||
            value == null)
        {
            value =
                fallback;

            instruction.Arguments[argumentName] =
                value;
        }

        ImGui.PushID(
            argumentName);

        ImGui.TextDisabled(
            label);

        string source =
            value.Kind ==
            EventValueKind.Reference
                ? "Reference"
                : "Constant";

        ImGui.SetNextItemWidth(
            -1.0f);

        if (ImGui.BeginCombo(
                "##Source",
                source))
        {
            if (ImGui.Selectable(
                    "Constant",
                    value.Kind ==
                    EventValueKind.Constant))
            {
                VariableType type =
                    expectedType ??
                    value.Constant?.Type ??
                    VariableType.Number;

                value =
                    CreateConstant(
                        type);

                instruction.Arguments[argumentName] =
                    value;

                _dirty =
                    true;
            }

            if (ImGui.Selectable(
                    "Reference",
                    value.Kind ==
                    EventValueKind.Reference))
            {
                value =
                    new EventValue
                    {
                        Kind =
                            EventValueKind.Reference
                    };

                instruction.Arguments[argumentName] =
                    value;

                _dirty =
                    true;
            }

            ImGui.EndCombo();
        }

        ImGui.Dummy(
            new Vector2(
                0.0f,
                2.0f));

        if (value.Kind ==
            EventValueKind.Reference)
        {
            DrawReferenceValue(
                instruction,
                argumentName,
                value,
                expectedType,
                state,
                writableReference);

            ImGui.PopID();

            return;
        }

        DrawConstantValue(
            instruction,
            argumentName,
            value,
            expectedType);

        ImGui.PopID();
    }

    // ========================================================
    // CONSTANT VALUES
    // ========================================================

    private void DrawConstantValue(
        VisualInstruction instruction,
        string argumentName,
        EventValue value,
        VariableType? expectedType)
    {
        if (expectedType.HasValue &&
            value.Constant.Type !=
            expectedType.Value)
        {
            value =
                CreateConstant(
                    expectedType.Value);

            instruction.Arguments[argumentName] =
                value;
            _dirty = true;
        }

        if (!expectedType.HasValue)
        {
            VariableType currentType =
                value.Constant.Type;

            ImGui.SetNextItemWidth(
                -1.0f);

            if (ImGui.BeginCombo(
                    "Type",
                    currentType.ToString()))
            {
                foreach (VariableType type
                         in Enum.GetValues<VariableType>())
                {
                    bool selected =
                        currentType ==
                        type;

                    if (ImGui.Selectable(
                            type.ToString(),
                            selected))
                    {
                        value =
                            CreateConstant(
                                type);

                        instruction.Arguments[argumentName] =
                            value;

                        _dirty =
                            true;

                        currentType =
                            type;
                    }

                    if (selected)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }

                ImGui.EndCombo();
            }
        }

        VariableValue constant =
            value.Constant;

        switch (constant.Type)
        {
            case VariableType.Number:
            {
                float number =
                    (float)constant.Number;

                ImGui.SetNextItemWidth(
                    -1.0f);

                if (ImGui.DragFloat(
                        "Value",
                        ref number,
                        0.05f))
                {
                    instruction.Arguments[argumentName] =
                        EventValue.Number(
                            number);

                    _dirty =
                        true;
                }

                break;
            }

            case VariableType.String:
            {
                string text =
                    constant.String;

                if (ImGui.InputText(
                        "Value",
                        ref text,
                        256))
                {
                    instruction.Arguments[argumentName] =
                        EventValue.String(
                            text);

                    _dirty =
                        true;
                }

                break;
            }

            case VariableType.Boolean:
            {
                bool boolean =
                    constant.Boolean;

                if (ImGui.Checkbox(
                        "Value",
                        ref boolean))
                {
                    instruction.Arguments[argumentName] =
                        EventValue.Boolean(
                            boolean);

                    _dirty =
                        true;
                }

                break;
            }

            case VariableType.Vector2:
            {
                Vector2 vector =
                    constant.Vector2;

                float x =
                    vector.X;

                float y =
                    vector.Y;

                ImGui.SetNextItemWidth(
                    -1.0f);

                bool changed =
                    ImGui.DragFloat(
                        "X",
                        ref x,
                        0.05f);

                ImGui.SetNextItemWidth(
                    -1.0f);

                changed |=
                    ImGui.DragFloat(
                        "Y",
                        ref y,
                        0.05f);

                if (changed)
                {
                    instruction.Arguments[argumentName] =
                        EventValue.Vector2(
                            new Vector2(
                                x,
                                y));

                    _dirty =
                        true;
                }

                break;
            }

            case VariableType.Vector3:
            {
                Vector3 vector =
                    constant.Vector3;

                float x =
                    vector.X;

                float y =
                    vector.Y;

                float z =
                    vector.Z;

                ImGui.SetNextItemWidth(
                    -1.0f);

                bool changed =
                    ImGui.DragFloat(
                        "X",
                        ref x,
                        0.05f);

                ImGui.SetNextItemWidth(
                    -1.0f);

                changed |=
                    ImGui.DragFloat(
                        "Y",
                        ref y,
                        0.05f);

                ImGui.SetNextItemWidth(
                    -1.0f);

                changed |=
                    ImGui.DragFloat(
                        "Z",
                        ref z,
                        0.05f);

                if (changed)
                {
                    instruction.Arguments[argumentName] =
                        EventValue.Vector3(
                            new Vector3(
                                x,
                                y,
                                z));

                    _dirty =
                        true;
                }

                break;
            }
        }
    }

    // ========================================================
    // REFERENCE VALUES
    // ========================================================

    private void DrawReferenceValue(
        VisualInstruction instruction,
        string argumentName,
        EventValue value,
        VariableType? expectedType,
        EditorState? state,
        bool writableOnly)
    {
        string display =
            value.Reference != null
                ? FormatReference(
                    value.Reference)
                : "Choose Reference...";

        if (ImGui.Button(
                display,
                new Vector2(
                    -1.0f,
                    36.0f)))
        {
            ImGui.OpenPopup(
                "ReferencePicker");
        }

        if (state == null)
        {
            ImGui.TextDisabled(
                "No active editor state.");

            return;
        }

        GameObject? self =
            ResolveSelfContext(
                state);

        if (_referencePicker.DrawPopup(
                "ReferencePicker",
                state,
                self,
                expectedType,
                writableOnly,
                out VariableReference? selected,
                stringConvertible: instruction.Id == "ui.setText" &&
                                   argumentName == "text") &&
            selected != null)
        {
            instruction.Arguments[argumentName] =
                EventValue.FromReference(
                    selected);

            _dirty =
                true;
        }
    }

    // ========================================================
    // BLUEPRINT ASSET
    // ========================================================

    private void DrawUiArguments(VisualInstruction instruction, EditorState? state)
    {
        DrawObjectTargetArgument(instruction, "target", "UI Object", state);
        switch (instruction.Id)
        {
            case "ui.setText":
            case "ui.setButtonLabel":
                DrawValueArgument(instruction, "text", "Text", VariableType.String,
                    EventValue.String(string.Empty), state, false);
                break;
            case "ui.setTextFromHealth":
                DrawObjectTargetArgument(instruction, "source", "Health Object", state);
                DrawValueArgument(instruction, "prefix", "Prefix", VariableType.String,
                    EventValue.String("HP "), state, false);
                DrawValueArgument(instruction, "prefixKey", "Prefix Localization Key", VariableType.String,
                    EventValue.String(string.Empty), state, false);
                break;
            case "ui.setBarValue":
            case "ui.setBarMaximum":
            case "ui.barBelow":
            case "ui.barAbove":
                DrawValueArgument(instruction, "value", "Value", VariableType.Number,
                    EventValue.Number(100), state, false);
                break;
            case "ui.setImage":
                DrawMaterialAssetArgument(instruction, "image", "Image Asset", AssetType.Texture2D);
                break;
            case "ui.setBarFromHealth":
                DrawObjectTargetArgument(instruction, "source", "Health Object", state);
                break;
            case "ui.setButtonEnabled":
                DrawValueArgument(instruction, "enabled", "Enabled", VariableType.Boolean,
                    EventValue.Boolean(true), state, false);
                break;
            case "ui.setLanguage":
            case "ui.languageIs":
                DrawValueArgument(instruction, "language", "Language Code", VariableType.String,
                    EventValue.String("en"), state, false);
                break;
            case "ui.setTextKey":
            case "ui.setLabelKey":
                DrawValueArgument(instruction, "key", "Localization Key", VariableType.String,
                    EventValue.String("menu.play"), state, false);
                break;
        }
    }

    private void DrawMaterialActionArguments(VisualInstruction instruction, EditorState? state)
    {
        DrawObjectTargetArgument(instruction, "target", "Object", state);
        DrawValueArgument(instruction, "slot", "Material Slot (0-based)", VariableType.Number,
            EventValue.Number(0), state, false);
        switch (instruction.Id)
        {
            case "material.setMaterial":
                DrawMaterialAssetArgument(instruction, "material", "Material Asset / Instance",
                    AssetType.Material);
                break;
            case "material.setBaseColor":
                DrawValueArgument(instruction, "color", "RGB Color", VariableType.Vector3,
                    EventValue.Vector3(Vector3.One), state, false);
                DrawValueArgument(instruction, "alpha", "Alpha", VariableType.Number,
                    EventValue.Number(1), state, false);
                break;
            case "material.setEmissionColor":
                DrawValueArgument(instruction, "color", "Emission RGB", VariableType.Vector3,
                    EventValue.Vector3(Vector3.One), state, false);
                break;
            case "material.setMetallic":
            case "material.setRoughness":
            case "material.setEmissionIntensity":
                DrawValueArgument(instruction, "value", "Value", VariableType.Number,
                    EventValue.Number(1), state, false);
                break;
            case "material.setTexture":
                string[] names = Enum.GetNames<ByteEngine.Core.Graphics.ThreeD.MaterialTextureSlot>();
                string selected = instruction.Arguments.TryGetValue("textureSlot", out EventValue? slotValue)
                    ? slotValue.Constant.String : "BaseColor";
                int index = Array.FindIndex(names, name => name.Equals(selected,
                    StringComparison.OrdinalIgnoreCase));
                if (index < 0) index = 0;
                if (ImGui.Combo("Texture Slot", ref index, names, names.Length))
                {
                    RecordHistory("Change Material Texture Slot");
                    instruction.Arguments["textureSlot"] = EventValue.String(names[index]);
                    _dirty = true;
                }
                DrawMaterialAssetArgument(instruction, "texture", "Texture Asset",
                    AssetType.Texture2D);
                break;
            case "material.resetOverrides":
                ImGui.TextDisabled("Clears this renderer's runtime material changes.");
                break;
        }
    }

    private void DrawMaterialAssetArgument(VisualInstruction instruction,
        string key, string label, AssetType type)
    {
        if (_project == null) return;
        string token = instruction.Arguments.TryGetValue(key, out EventValue? value) &&
            value.Kind == EventValueKind.Constant ? value.Constant.String : string.Empty;
        AssetRecord? selected = null;
        if (Guid.TryParse(token, out Guid guid))
            _project.AssetDatabase.TryGetAsset(guid, out selected);
        else if (token.Length > 0)
            _project.AssetDatabase.TryGetAsset(token, out selected);
        ImGui.TextDisabled(label);
        string preview = selected?.Type == type ? selected.ProjectPath :
            token.Length == 0 ? "None" : "Missing asset";
        if (ImGui.BeginCombo($"##{key}{instruction.InstanceId}", preview))
        {
            if (ImGui.Selectable("None", token.Length == 0))
            {
                RecordHistory("Change Material Asset");
                instruction.Arguments[key] = EventValue.String(string.Empty);
                _dirty = true;
            }
            foreach (AssetRecord item in _project.AssetDatabase.Assets
                .Where(item => item.Type == type)
                .OrderBy(item => item.ProjectPath, StringComparer.OrdinalIgnoreCase))
            {
                if (ImGui.Selectable(item.ProjectPath, item.Guid == selected?.Guid))
                {
                    RecordHistory("Change Material Asset");
                    instruction.Arguments[key] = EventValue.String(item.Guid.ToString());
                    _dirty = true;
                }
            }
            ImGui.EndCombo();
        }
        if (ImGui.BeginDragDropTarget())
        {
            Guid? dropped = AssetDragDrop.Accept();
            if (dropped.HasValue &&
                _project.AssetDatabase.TryGetAsset(dropped.Value, out AssetRecord? item) &&
                item?.Type == type)
            {
                RecordHistory("Change Material Asset");
                instruction.Arguments[key] = EventValue.String(item.Guid.ToString());
                _dirty = true;
            }
            ImGui.EndDragDropTarget();
        }
    }
    private void DrawBlueprintAssetArgument(
        VisualInstruction instruction,
        string argumentName,
        string label)
    {
        if (!instruction.Arguments.TryGetValue(
                argumentName,
                out EventValue? value) ||
            value ==
                null ||
            value.Kind !=
                EventValueKind.Constant ||
            value.Constant.Type !=
                VariableType.String)
        {
            value =
                EventValue.String(
                    string.Empty);

            instruction.Arguments[argumentName] =
                value;
        }

        string token =
            value.Constant.String;

        AssetRecord? selectedAsset =
            ResolveBlueprintAsset(
                token);

        string preview =
            selectedAsset !=
                null
                ? Path.GetFileNameWithoutExtension(
                    selectedAsset.ProjectPath)
                : string.IsNullOrWhiteSpace(
                    token)
                    ? "Select Blueprint..."
                    : "Missing Blueprint";

        ImGui.TextDisabled(
            label);

        ImGui.SetNextItemWidth(
            -1.0f);

        if (ImGui.BeginCombo(
                "##BlueprintAsset",
                preview))
        {
            bool noneSelected =
                selectedAsset ==
                    null &&
                string.IsNullOrWhiteSpace(
                    token);

            if (ImGui.Selectable(
                    "<None>",
                    noneSelected))
            {
                RecordHistory(
                    "Change Blueprint Asset");

                instruction.Arguments[argumentName] =
                    EventValue.String(
                        string.Empty);

                _dirty =
                    true;
            }

            if (_project !=
                null)
            {
                AssetRecord[] blueprints =
                    _project.AssetDatabase.Assets
                        .Where(
                            asset =>
                                asset.Type ==
                                AssetType.Blueprint)
                        .OrderBy(
                            asset =>
                                asset.ProjectPath,
                            StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                if (blueprints.Length >
                    0)
                {
                    ImGui.Separator();
                }

                foreach (AssetRecord blueprint
                         in blueprints)
                {
                    bool selected =
                        selectedAsset?.Guid ==
                        blueprint.Guid;

                    string displayName =
                        Path.GetFileNameWithoutExtension(
                            blueprint.ProjectPath);

                    string labelText =
                        $"{displayName}##blueprint:{blueprint.Guid}";

                    if (ImGui.Selectable(
                            labelText,
                            selected))
                    {
                        RecordHistory(
                            "Change Blueprint Asset");

                        instruction.Arguments[argumentName] =
                            EventValue.String(
                                blueprint.Guid.ToString());

                        _dirty =
                            true;
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(
                            blueprint.ProjectPath);
                    }

                    if (selected)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }
            }

            ImGui.EndCombo();
        }

        if (selectedAsset !=
            null)
        {
            ImGui.TextDisabled(
                selectedAsset.ProjectPath);
        }
        else if (!string.IsNullOrWhiteSpace(
                     token))
        {
            ImGui.TextColored(
                new Vector4(
                    1.0f,
                    0.38f,
                    0.30f,
                    1.0f),
                "Blueprint asset could not be resolved.");
        }

        ImGui.TextDisabled(
            "Tip: Self.Transform.WorldPosition spawns directly on top of Self.");
    }

    private AssetRecord? ResolveBlueprintAsset(
        string token)
    {
        if (_project ==
                null ||
            string.IsNullOrWhiteSpace(
                token))
        {
            return null;
        }

        if (Guid.TryParse(
                token,
                out Guid guid) &&
            _project.AssetDatabase.TryGetAsset(
                guid,
                out AssetRecord? byGuid) &&
            byGuid?.Type ==
                AssetType.Blueprint)
        {
            return byGuid;
        }

        if (_project.AssetDatabase.TryGetAsset(
                token,
                out AssetRecord? byPath) &&
            byPath?.Type ==
                AssetType.Blueprint)
        {
            return byPath;
        }

        return null;
    }

    // =
    // ========================================================
    // AUDIO CLIP ASSET PICKER
    // ========================================================

    private void DrawAudioClipArgument(
        VisualInstruction instruction)
    {
        EventValue guidValue =
            EnsureAnimationSignalString(
                instruction,
                "clipGuid");
        EventValue pathValue =
            EnsureAnimationSignalString(
                instruction,
                "clipPath");

        string guidText =
            guidValue.Kind == EventValueKind.Constant
                ? guidValue.Constant.String
                : string.Empty;
        string cachedPath =
            pathValue.Kind == EventValueKind.Constant
                ? pathValue.Constant.String
                : string.Empty;

        AssetRecord? selected = null;

        if (_project != null)
        {
            try
            {
                AssetReference reference =
                    Guid.TryParse(guidText, out Guid guid)
                        ? new AssetReference(guid, cachedPath)
                        : !string.IsNullOrWhiteSpace(cachedPath)
                            ? new AssetReference(cachedPath)
                            : AssetReference.Empty;

                if (!reference.IsEmpty)
                {
                    selected =
                        _project.AssetDatabase.Resolve(reference);
                }
            }
            catch
            {
                selected = null;
            }
        }

        string display =
            selected?.Type == AssetType.AudioClip
                ? Path.GetFileName(selected.ProjectPath)
                : string.IsNullOrWhiteSpace(cachedPath)
                    ? "No Audio Clip"
                    : Path.GetFileName(cachedPath);

        ImGui.TextDisabled("Audio Clip");

        if (ImGui.Button(
                display,
                new Vector2(-1.0f, 36.0f)))
        {
            _audioClipSearch = string.Empty;
            ImGui.OpenPopup("AudioClipPicker");
        }

        DrawFullValueTooltip(
            selected?.ProjectPath ?? cachedPath);

        if (!ImGui.BeginPopup("AudioClipPicker"))
        {
            DrawAudioClipResolutionWarning(
                guidText,
                cachedPath,
                selected);
            return;
        }

        ImGui.SetNextItemWidth(320.0f);
        ImGui.InputTextWithHint(
            "##AudioSearch",
            "Search audio...",
            ref _audioClipSearch,
            128);

        ImGui.Separator();

        if (ImGui.Selectable(
                "No Audio Clip",
                string.IsNullOrWhiteSpace(guidText) &&
                string.IsNullOrWhiteSpace(cachedPath)))
        {
            SetAudioClipArguments(
                instruction,
                null);
            ImGui.CloseCurrentPopup();
        }

        AssetRecord[] clips =
            _project?.AssetDatabase.Assets
                .Where(asset => asset.Type == AssetType.AudioClip)
                .Where(asset =>
                    string.IsNullOrWhiteSpace(_audioClipSearch) ||
                    Path.GetFileName(asset.ProjectPath).Contains(
                        _audioClipSearch,
                        StringComparison.OrdinalIgnoreCase) ||
                    asset.ProjectPath.Contains(
                        _audioClipSearch,
                        StringComparison.OrdinalIgnoreCase))
                .OrderBy(
                    asset => Path.GetFileName(asset.ProjectPath),
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    asset => asset.ProjectPath,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray()
            ?? Array.Empty<AssetRecord>();

        if (clips.Length > 0)
        {
            ImGui.Separator();
        }

        foreach (AssetRecord clip in clips)
        {
            bool isSelected =
                selected?.Guid == clip.Guid;

            if (ImGui.Selectable(
                    $"{Path.GetFileName(clip.ProjectPath)}##audio:{clip.Guid}",
                    isSelected))
            {
                SetAudioClipArguments(
                    instruction,
                    clip);
                ImGui.CloseCurrentPopup();
            }

            DrawFullValueTooltip(clip.ProjectPath);
        }

        if (_project == null)
        {
            ImGui.TextDisabled(
                "Open a project to select an Audio Clip.");
        }
        else if (clips.Length == 0)
        {
            ImGui.TextDisabled(
                "No matching Audio Clip assets.");
        }

        ImGui.EndPopup();

        DrawAudioClipResolutionWarning(
            guidText,
            cachedPath,
            selected);
    }

    private void SetAudioClipArguments(
        VisualInstruction instruction,
        AssetRecord? clip)
    {
        RecordHistory("Change Audio Clip");

        instruction.Arguments["clipGuid"] =
            EventValue.String(
                clip?.Guid.ToString() ?? string.Empty);
        instruction.Arguments["clipPath"] =
            EventValue.String(
                clip?.ProjectPath ?? string.Empty);

        _dirty = true;
    }

    private static void DrawAudioClipResolutionWarning(
        string guidText,
        string cachedPath,
        AssetRecord? selected)
    {
        if (string.IsNullOrWhiteSpace(guidText) &&
            string.IsNullOrWhiteSpace(cachedPath))
        {
            return;
        }

        if (selected == null)
        {
            ImGui.TextColored(
                new Vector4(1.0f, 0.72f, 0.28f, 1.0f),
                "Selected Audio Clip no longer resolves.");
        }
        else if (selected.Type != AssetType.AudioClip)
        {
            ImGui.TextColored(
                new Vector4(1.0f, 0.72f, 0.28f, 1.0f),
                "Selected asset is not an Audio Clip.");
        }
    }

    private void DrawAttachmentSocketArgument(VisualInstruction instruction, EditorState? state)
    {
        string current = instruction.Arguments.TryGetValue("socket", out EventValue? value) && value.Constant.Type == VariableType.String ? value.Constant.String : string.Empty;
        GameObject? parent = state == null ? null : ResolveEditorObjectArgument(instruction, "parent", state);
        IReadOnlyList<SkeletalSocketDefinition> sockets = parent == null ? Array.Empty<SkeletalSocketDefinition>() : DiscoverAttachmentSockets(parent);
        bool exists = sockets.Any(item => string.Equals(item.Name, current, StringComparison.OrdinalIgnoreCase));
        ImGui.TextDisabled("Socket");
        if (sockets.Count > 0)
        {
            string preview = string.IsNullOrWhiteSpace(current) ? "Select Socket..." : exists ? current : current + " (Missing)";
            if (ImGui.BeginCombo("##AttachmentSocket", preview))
            {
                foreach (SkeletalSocketDefinition socket in sockets) if (ImGui.Selectable($"{socket.Name}##{socket.Id}", string.Equals(socket.Name,current,StringComparison.OrdinalIgnoreCase))) { instruction.Arguments["socket"]=EventValue.String(socket.Name);_dirty=true; }
                ImGui.EndCombo();
            }
        }
        else
        {
            if(ImGui.InputTextWithHint("##AttachmentSocket","Socket name...",ref current,128)){instruction.Arguments["socket"]=EventValue.String(current);_dirty=true;}
            ImGui.TextDisabled("Parent is unresolved or has no model-owned sockets; manual value is preserved.");
        }
    }

    private static IReadOnlyList<SkeletalSocketDefinition> DiscoverAttachmentSockets(GameObject parent)
    {
        IReadOnlyList<SkeletalSocketDefinition> live=SkeletalSocketResolver.GetSockets(parent); if(live.Count>0)return live;
        EditorProjectContext? project=EditorProjectContext.Active; if(project==null)return live;
        foreach(GameObject item in SelfAndDescendants(parent))
        {
            AnimationController? controller=item.GetComponent<AnimationController>();
            if(controller!=null&&AnimationClipDiscovery.TryGetModel(controller,project,out ModelAsset? model,out _)&&model!=null&&model.Sockets.Count>0)return model.Sockets;
        }
        return live;
    }

    private static IEnumerable<GameObject> SelfAndDescendants(GameObject root)
    {
        yield return root; foreach(GameObject child in root.Children)foreach(GameObject item in SelfAndDescendants(child))yield return item;
    }
    private void DrawAttachmentRuleArgument(VisualInstruction instruction, string argument, string label, string fallback)
    {
        string current=instruction.Arguments.TryGetValue(argument,out EventValue? value)&&value.Constant.Type==VariableType.String?value.Constant.String:fallback;
        string Display(string item)=>item switch{"SnapToTarget"=>"Snap To Target","KeepWorld"=>"Keep World",_=>"Keep Relative"};
        if(ImGui.BeginCombo(label,Display(current)))
        {
            foreach(string item in new[]{"KeepRelative","KeepWorld","SnapToTarget"}) if(ImGui.Selectable(Display(item),string.Equals(item,current,StringComparison.OrdinalIgnoreCase))){instruction.Arguments[argument]=EventValue.String(item);_dirty=true;}
            ImGui.EndCombo();
        }
    }

    private GameObject? ResolveEditorObjectArgument(VisualInstruction instruction, string argument, EditorState state)
    {
        string token=instruction.Arguments.TryGetValue(argument,out EventValue? value)&&value.Constant.Type==VariableType.String?value.Constant.String:"Self";
        if(string.IsNullOrWhiteSpace(token)||string.Equals(token,"Self",StringComparison.OrdinalIgnoreCase))return ResolveSelfContext(state);
        return token.StartsWith("id:",StringComparison.OrdinalIgnoreCase)&&Guid.TryParse(token[3..],out Guid id)?state.EditorScene.FindGameObject(id):state.EditorScene.FindGameObject(token);
    }
    // ========================================================
    // OBJECT TARGETS
    // ========================================================

    private void DrawObjectTargetArgument(
        VisualInstruction instruction,
        string argumentName,
        string label,
        EditorState? state)
    {
        ImGui.PushID(
            $"ObjectTarget:{argumentName}");

        string token =
            "Self";

        if (instruction.Arguments.TryGetValue(
                argumentName,
                out EventValue? value) &&
            value != null &&
            value.Kind ==
                EventValueKind.Constant &&
            value.Constant.Type ==
                VariableType.String &&
            !string.IsNullOrWhiteSpace(
                value.Constant.String))
        {
            token =
                value.Constant.String;
        }

        GameObject? self =
            state != null
                ? ResolveSelfContext(
                    state)
                : null;

        string display =
            GameObjectReferencePicker.GetDisplayName(
                token,
                state,
                self);

        ImGui.TextDisabled(
            label);

        if (ImGui.Button(
                display,
                new Vector2(
                    -1.0f,
                    36.0f)))
        {
            ImGui.OpenPopup(
                "ObjectTargetPicker");
        }

        if (state != null &&
            _objectPicker.DrawPopup(
                "ObjectTargetPicker",
                state,
                self,
                out string? selectedToken) &&
            !string.IsNullOrWhiteSpace(
                selectedToken))
        {
            RecordHistory(
                "Change Object Target");

            instruction.Arguments[argumentName] =
                EventValue.String(
                    selectedToken);

            _dirty =
                true;
        }

        ImGui.PopID();
    }

    // ========================================================
    // TARGET REFERENCES
    // ========================================================

    private void DrawTargetReference(
        VisualInstruction instruction,
        string argumentName,
        string label,
        VariableType? expectedType,
        EditorState? state)
    {
        ImGui.PushID(
            argumentName);

        ImGui.TextDisabled(
            label);

        VariableReference? reference =
            null;

        if (instruction.Arguments.TryGetValue(
                argumentName,
                out EventValue? value) &&
            value?.Kind ==
            EventValueKind.Reference)
        {
            reference =
                value.Reference;
        }

        string display =
            reference != null
                ? FormatReference(
                    reference)
                : "Choose Target...";

        if (ImGui.Button(
                display,
                new Vector2(
                    -1.0f,
                    36.0f)))
        {
            ImGui.OpenPopup(
                "TargetReferencePicker");
        }

        if (state != null)
        {
            GameObject? self =
                ResolveSelfContext(
                    state);

            if (_referencePicker.DrawPopup(
                    "TargetReferencePicker",
                    state,
                    self,
                    expectedType,
                    true,
                    out VariableReference? selected) &&
                selected != null)
            {
                instruction.Arguments[argumentName] =
                    EventValue.FromReference(
                        selected);
                if (instruction.Id == "variable.set" &&
                    argumentName == "target")
                {
                    VariableType? type = TryGetSelectedTargetVariableType(instruction, state);
                    if (type.HasValue &&
                        (!instruction.Arguments.TryGetValue("value", out EventValue? current) ||
                         current.Kind != EventValueKind.Constant ||
                         current.Constant.Type != type.Value))
                        instruction.Arguments["value"] = CreateConstant(type.Value);
                }

                _dirty =
                    true;
            }
        }

        ImGui.PopID();
    }

    // ========================================================
    // COMPARISON OPERATOR
    // ========================================================

    private void DrawComparisonOperator(
        VisualInstruction instruction)
    {
        EventValue value;

        if (!instruction.Arguments.TryGetValue(
                "operator",
                out EventValue? existing) ||
            existing == null)
        {
            value =
                EventValue.String(
                    "==");

            instruction.Arguments["operator"] =
                value;
        }
        else
        {
            value =
                existing;
        }

        string current =
            value.Kind ==
                EventValueKind.Constant &&
            value.Constant.Type ==
                VariableType.String
                ? value.Constant.String
                : "==";

        string[] operators =
        {
            "==",
            "!=",
            "<",
            "<=",
            ">",
            ">="
        };

        ImGui.TextDisabled(
            "Operator");

        ImGui.SetNextItemWidth(
            -1.0f);

        if (ImGui.BeginCombo(
                "##ComparisonOperator",
                current))
        {
            foreach (string operation
                     in operators)
            {
                bool selected =
                    current ==
                    operation;

                if (ImGui.Selectable(
                        operation,
                        selected))
                {
                    instruction.Arguments["operator"] =
                        EventValue.String(
                            operation);

                    _dirty =
                        true;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }
    }

    // ========================================================
    // KEY ARGUMENT
    // ========================================================

    private void DrawMouseButtonArgument(
        VisualInstruction instruction,
        string argumentName,
        string label,
        MouseButton fallback)
    {
        if (!instruction.Arguments.TryGetValue(
                argumentName,
                out EventValue? value) ||
            value ==
                null)
        {
            value =
                EventValue.String(
                    fallback.ToString());

            instruction.Arguments[argumentName] =
                value;
        }

        string current =
            value.Kind ==
                EventValueKind.Constant &&
            value.Constant.Type ==
                VariableType.String
                ? value.Constant.String
                : fallback.ToString();

        ImGui.TextDisabled(
            label);

        ImGui.SetNextItemWidth(
            -1.0f);

        if (!ImGui.BeginCombo(
                "##MouseButton",
                current))
        {
            return;
        }

        foreach (MouseButton button
                 in Enum.GetValues<MouseButton>())
        {
            string name =
                button.ToString();

            bool selected =
                string.Equals(
                    current,
                    name,
                    StringComparison.OrdinalIgnoreCase);

            if (ImGui.Selectable(
                    name,
                    selected))
            {
                instruction.Arguments[argumentName] =
                    EventValue.String(
                        name);

                _dirty =
                    true;
            }

            if (selected)
            {
                ImGui.SetItemDefaultFocus();
            }
        }

        ImGui.EndCombo();
    }

    private void DrawKeyArgument(
        VisualInstruction instruction,
        string argumentName,
        string label,
        Key fallback)
    {
        if (!instruction.Arguments.TryGetValue(
                argumentName,
                out EventValue? value) ||
            value == null)
        {
            value =
                EventValue.String(
                    fallback.ToString());

            instruction.Arguments[argumentName] =
                value;
        }

        string current =
            value.Kind ==
                EventValueKind.Constant &&
            value.Constant.Type ==
                VariableType.String
                ? value.Constant.String
                : fallback.ToString();

        ImGui.TextDisabled(
            label);

        ImGui.SetNextItemWidth(
            -1.0f);

        if (!ImGui.BeginCombo(
                "##Key",
                current))
        {
            return;
        }

        foreach (Key key
                 in Enum.GetValues<Key>())
        {
            string name =
                key.ToString();

            bool selected =
                string.Equals(
                    current,
                    name,
                    StringComparison.OrdinalIgnoreCase);

            if (ImGui.Selectable(
                    name,
                    selected))
            {
                instruction.Arguments[argumentName] =
                    EventValue.String(
                        name);

                _dirty =
                    true;
            }

            if (selected)
            {
                ImGui.SetItemDefaultFocus();
            }
        }

        ImGui.EndCombo();
    }

    private void DrawInputActionArgument(VisualInstruction instruction, string argumentName, string label, string fallback)
    {
        if (!instruction.Arguments.TryGetValue(argumentName, out EventValue? value) || value == null)
        {
            value = EventValue.String(fallback);
            instruction.Arguments[argumentName] = value;
        }
        string current = value.Kind == EventValueKind.Constant && value.Constant.Type == VariableType.String
            ? value.Constant.String : fallback;
        ByteEngine.Core.InputSystem.InputActionDefinition? currentAction =
            EditorProjectContext.Active?.Project.InputMap.Find(Guid.TryParse(current, out Guid currentId) ? currentId : Guid.Empty) ??
            EditorProjectContext.Active?.Project.InputMap.Find(current);
        string preview = currentAction?.DisplayName ?? current;
        ImGui.TextDisabled(label);
        ImGui.SetNextItemWidth(-1f);
        if (!ImGui.BeginCombo("##InputAction" + argumentName, preview)) return;
        foreach (var action in EditorProjectContext.Active?.Project.InputMap.Actions ?? Enumerable.Empty<ByteEngine.Core.InputSystem.InputActionDefinition>())
        {
            bool selected = currentAction?.Id == action.Id;
            if (ImGui.Selectable(action.DisplayName + "##" + action.Id, selected))
            {
                instruction.Arguments[argumentName] = EventValue.String(action.Id.ToString());
                _dirty = true;
            }
            if (selected) ImGui.SetItemDefaultFocus();
        }
        ImGui.EndCombo();
    }

    private static string DefaultActionToken(string name) =>
        EditorProjectContext.Active?.Project.InputMap.Find(name)?.Id.ToString() ?? name;

    private static string DefaultTagToken(string name) =>
        EditorProjectContext.Active?.Project.Classification.FindTag(name)?.Id.ToString() ?? string.Empty;

    private static int DefaultLayerIndex(string name) =>
        EditorProjectContext.Active?.Project.Classification.FindLayer(name)?.Index ?? 0;

    private void DrawTagArgument(VisualInstruction instruction, string argumentName, string label, string fallback)
    {
        ClassificationSettings? settings = EditorProjectContext.Active?.Project.Classification;
        if (settings == null) return;
        if (!instruction.Arguments.TryGetValue(argumentName, out EventValue? value))
            instruction.Arguments[argumentName] = value = EventValue.String(DefaultTagToken(fallback));
        Guid.TryParse(value.Constant.String, out Guid selected);
        if (ByteEngine.Editor.ClassificationPickers.DrawTag(label, settings, ref selected, false))
        {
            instruction.Arguments[argumentName] = EventValue.String(selected.ToString());
            _dirty = true;
        }
    }

    private void DrawLayerArgument(VisualInstruction instruction, string argumentName, string label, string fallback)
    {
        ClassificationSettings? settings = EditorProjectContext.Active?.Project.Classification;
        if (settings == null) return;
        if (!instruction.Arguments.TryGetValue(argumentName, out EventValue? value))
            instruction.Arguments[argumentName] = value = EventValue.Number(DefaultLayerIndex(fallback));
        int selected = value.Constant.Type == VariableType.Number ? (int)value.Constant.Number : DefaultLayerIndex(fallback);
        if (ByteEngine.Editor.ClassificationPickers.DrawLayer(label, settings, ref selected))
        {
            instruction.Arguments[argumentName] = EventValue.Number(selected);
            _dirty = true;
        }
    }

    // ========================================================
    // SELF CONTEXT
    // ========================================================

    private GameObject? ResolveSelfContext(
        EditorState state)
    {
        /*
         * First try to find the GameObject that actually has this
         * Event Module attached.
         *
         * This is important because clicking the .byteevents file
         * in the Assets browser normally clears hierarchy selection.
         */
        if (_asset != null)
        {
            foreach (GameObject gameObject
                     in state.EditorScene.GameObjects)
            {
                EventModuleComponent? component =
                    gameObject.GetComponent<EventModuleComponent>();

                if (component == null)
                {
                    continue;
                }

                bool attached =
                    component.Modules.Any(
                        reference =>
                            reference.Guid ==
                            _asset.Guid);

                if (attached)
                {
                    return gameObject;
                }
            }
        }

        return state.SelectedObject;
    }

    // ========================================================
    // EVENT VALUE HELPERS
    // ========================================================

    private static EventValue CreateConstant(
        VariableType type)
    {
        return type switch
        {
            VariableType.String =>
                EventValue.String(
                    string.Empty),

            VariableType.Boolean =>
                EventValue.Boolean(
                    false),

            VariableType.Vector2 =>
                EventValue.Vector2(
                    Vector2.Zero),

            VariableType.Vector3 =>
                EventValue.Vector3(
                    Vector3.Zero),

            _ =>
                EventValue.Number(
                    0.0)
        };
    }

    private static string FormatReference(
        VariableReference reference)
    {
        return reference.Scope switch
        {
            VariableScope.Global =>
                $"Global.{reference.MemberName}",

            VariableScope.Scene =>
                $"Scene.{reference.MemberName}",

            VariableScope.Self =>
                $"Self.{reference.MemberName}",

            VariableScope.Object =>
                $"{reference.ObjectName}.{reference.MemberName}",

            VariableScope.Component
                when string.IsNullOrWhiteSpace(
                    reference.ObjectName) =>
                $"Self.{reference.ComponentType}.{reference.MemberName}",

            VariableScope.Component =>
                $"{reference.ObjectName}.{reference.ComponentType}.{reference.MemberName}",

            _ =>
                reference.MemberName
        };
    }
}
