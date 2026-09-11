using System.Numerics;

using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class EventWorkspacePanel
{
    private readonly EventModuleSerializer _serializer =
        new();

    private readonly VisualLogicRegistry _registry =
        VisualLogicRegistry.CreateDefault();

    private readonly VariableReferencePicker _referencePicker =
        new();

    private readonly EventModuleHistory _history =
        new();

    private AssetRecord? _asset;

    private EventModuleDefinition? _module;

    private bool _open;

    private bool _dirty;

    private bool _requestFocus;

    public bool IsOpen =>
        _open;

    public Guid AssetGuid =>
        _asset?.Guid ??
        Guid.Empty;

    internal bool CanUndo =>
        _history.CanUndo;

    internal bool CanRedo =>
        _history.CanRedo;

    internal string? UndoName =>
        _history.UndoName;

    internal string? RedoName =>
        _history.RedoName;

    internal bool Undo()
    {
        if (_module ==
            null)
        {
            return false;
        }

        if (!_history.TryUndo(
                _module,
                out EventModuleDefinition restored))
        {
            return false;
        }

        _module =
            restored;

        _dirty =
            _history.IsDirty(
                _module);

        return true;
    }

    internal bool Redo()
    {
        if (_module ==
            null)
        {
            return false;
        }

        if (!_history.TryRedo(
                _module,
                out EventModuleDefinition restored))
        {
            return false;
        }

        _module =
            restored;

        _dirty =
            _history.IsDirty(
                _module);

        return true;
    }

    private void RecordHistory(
        string name)
    {
        if (_module ==
            null)
        {
            return;
        }

        _history.Record(
            name,
            _module);
    }

    // ========================================================
    // OPEN
    // ========================================================

    public void Open(
        AssetRecord asset,
        EditorLog log)
    {
        ArgumentNullException.ThrowIfNull(
            asset);

        if (_asset?.Guid ==
                asset.Guid &&
            _module !=
                null)
        {
            _open = true;

            _requestFocus = true;

            return;
        }

        try
        {
            _module =
                _serializer.Load(
                    asset.FullPath);

            _asset =
                asset;

            _history.Reset(
                _module);

            _open =
                true;

            _dirty =
                false;

            _requestFocus =
                true;

            log.Info(
                $"Opened Event Module '{asset.ProjectPath}'.");
        }
        catch (Exception exception)
        {
            log.Error(
                $"Could not open Event Module '{asset.ProjectPath}': {exception.Message}");
        }
    }

    // ========================================================
    // DRAW
    // ========================================================

    public void Draw(
        EditorLog log)
    {
        if (!_open ||
            _module == null ||
            _asset == null)
        {
            return;
        }

        EditorState? state =
            EditorState.Active;

        uint sceneDockId =
            EditorWorkspaceDocking.SceneDocumentDockId;

        if (sceneDockId != 0)
        {
            ImGui.SetNextWindowDockID(
                sceneDockId,
                ImGuiCond.FirstUseEver);
        }

        ImGui.SetNextWindowSize(
            new Vector2(
                1100.0f,
                720.0f),
            ImGuiCond.FirstUseEver);

        if (_requestFocus)
        {
            ImGui.SetNextWindowFocus();

            _requestFocus =
                false;
        }

        string dirtyMarker =
            _dirty
                ? " *"
                : string.Empty;

        bool open =
            _open;

        bool visible =
            ImGui.Begin(
                $"{_module.Name}{dirtyMarker}##EventWorkspace:{_asset.Guid}",
                ref open);

        _open =
            open;

        if (ImGui.IsWindowFocused(
                ImGuiFocusedFlags.RootAndChildWindows))
        {
            EventWorkspaceUndoRouter.SetFocused(
                this);
        }
        else
        {
            EventWorkspaceUndoRouter.ClearFocused(
                this);
        }

        if (!_open)
        {
            EventWorkspaceUndoRouter.ClearFocused(
                this);
        }

        if (!visible)
        {
            ImGui.End();

            return;
        }

        ImGui.PushStyleVar(
            ImGuiStyleVar.ItemSpacing,
            new Vector2(
                10.0f,
                10.0f));

        ImGui.PushStyleVar(
            ImGuiStyleVar.FramePadding,
            new Vector2(
                8.0f,
                6.0f));

        DrawDocumentToolbar(
            log);

        ImGui.Dummy(
            new Vector2(
                0.0f,
                5.0f));

        DrawModuleSettings();

        ImGui.Dummy(
            new Vector2(
                0.0f,
                10.0f));

        DrawRules(
            state);

        ImGui.PopStyleVar(
            2);

        ImGui.End();
    }

    // ========================================================
    // TOOLBAR
    // ========================================================

    private void DrawDocumentToolbar(
        EditorLog log)
    {
        ImGui.TextDisabled(
            "EVENT MODULE");

        ImGui.SameLine();

        ImGui.Text(
            _module!.Name);

        ImGui.SameLine();

        ImGui.BeginDisabled(
            !CanUndo);

        if (ImGui.Button(
                UndoName is string undoName
                    ? $"Undo {undoName}"
                    : "Undo"))
        {
            Undo();
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        ImGui.BeginDisabled(
            !CanRedo);

        if (ImGui.Button(
                RedoName is string redoName
                    ? $"Redo {redoName}"
                    : "Redo"))
        {
            Redo();
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        ImGui.BeginDisabled(
            !_dirty);

        if (ImGui.Button(
                "Save"))
        {
            Save(
                log);
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        if (ImGui.Button(
                "+ Add Event"))
        {
            RecordHistory(
                "Add Event");

            _module.Rules.Add(
                new EventRuleDefinition());

            _dirty =
                true;
        }

        ImGui.SameLine();

        ImGui.TextDisabled(
            _asset!.ProjectPath);
    }

    private void Save(
        EditorLog log)
    {
        if (_module == null ||
            _asset == null)
        {
            return;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(
                    _module.Name))
            {
                _module.Name =
                    Path.GetFileNameWithoutExtension(
                        _asset.FullPath);
            }

            _serializer.Save(
                _module,
                _asset.FullPath);

            _history.MarkSaved(
                _module);

            _dirty =
                false;

            log.Info(
                $"Saved Event Module '{_asset.ProjectPath}'.");
        }
        catch (Exception exception)
        {
            log.Error(
                $"Could not save Event Module '{_asset.ProjectPath}': {exception.Message}");
        }
    }

    // ========================================================
    // MODULE SETTINGS
    // ========================================================

    private void DrawModuleSettings()
    {
        if (_module == null)
        {
            return;
        }

        if (!ImGui.CollapsingHeader(
                "Module Settings"))
        {
            return;
        }

        ImGui.Indent(
            12.0f);

        string name =
            _module.Name;

        if (ImGui.InputText(
                "Name",
                ref name,
                128))
        {
            _module.Name =
                name;

            _dirty =
                true;
        }

        ImGui.TextDisabled(
            $"Asset ID: {_module.Id}");

        ImGui.TextDisabled(
            $"Format Version: {_module.Version}");

        if (_module.TargetBlueprintGuid.HasValue)
        {
            ImGui.Text(
                $"Target Blueprint: {_module.TargetBlueprintGuid}");
        }
        else
        {
            ImGui.TextDisabled(
                "Target Blueprint: Generic");
        }

        ImGui.SeparatorText(
            "Required Components");

        if (_module.RequiredComponents.Count ==
            0)
        {
            ImGui.TextDisabled(
                "None");
        }
        else
        {
            foreach (string component
                     in _module.RequiredComponents)
            {
                ImGui.BulletText(
                    component);
            }
        }

        ImGui.Unindent(
            12.0f);
    }

    // ========================================================
    // RULES
    // ========================================================

    private void DrawRules(
        EditorState? state)
    {
        if (_module == null)
        {
            return;
        }

        if (_module.Rules.Count ==
            0)
        {
            ImGui.Dummy(
                new Vector2(
                    0.0f,
                    25.0f));

            ImGui.TextDisabled(
                "This module has no events.");

            ImGui.TextDisabled(
                "Click '+ Add Event' to begin.");

            return;
        }

        for (int index = 0;
             index < _module.Rules.Count;
             index++)
        {
            EventRuleDefinition rule =
                _module.Rules[index];

            ImGui.PushID(
                rule.Id.ToString());

            bool remove =
                DrawRule(
                    rule,
                    index,
                    state);

            ImGui.PopID();

            if (remove)
            {
                RecordHistory(
                    "Delete Event");

                _module.Rules.RemoveAt(
                    index);

                _dirty =
                    true;

                index--;
            }

            ImGui.Dummy(
                new Vector2(
                    0.0f,
                    18.0f));
        }
    }

    private bool DrawRule(
        EventRuleDefinition rule,
        int index,
        EditorState? state)
    {
        ImGui.SeparatorText(
            $"EVENT {index + 1}");

        ImGui.Dummy(
            new Vector2(
                0.0f,
                4.0f));

        bool enabled =
            rule.Enabled;

        if (ImGui.Checkbox(
                "Enabled",
                ref enabled))
        {
            RecordHistory(
                enabled
                    ? "Enable Event"
                    : "Disable Event");

            rule.Enabled =
                enabled;

            _dirty =
                true;
        }

        ImGui.SameLine();

        ImGui.TextDisabled(
            $"ID: {rule.Id.ToString()[..8]}");

        float available =
            ImGui.GetContentRegionAvail().X;

        if (available >
            120.0f)
        {
            ImGui.SameLine(
                ImGui.GetCursorPosX() +
                available -
                110.0f);
        }

        bool remove =
            ImGui.Button(
                "Delete Event");

        ImGui.Dummy(
            new Vector2(
                0.0f,
                6.0f));

        if (rule.Conditions.Count ==
            0)
        {
            ImGui.TextColored(
                new Vector4(
                    1.0f,
                    0.75f,
                    0.25f,
                    1.0f),
                "No conditions - this event executes every frame.");

            ImGui.Dummy(
                new Vector2(
                    0.0f,
                    5.0f));
        }

        if (ImGui.BeginTable(
                "EventColumns",
                2,
                ImGuiTableFlags.BordersInnerV |
                ImGuiTableFlags.Resizable |
                ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableSetupColumn(
                "Conditions",
                ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableSetupColumn(
                "Actions",
                ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();

            ImGui.TableNextColumn();

            DrawColumnHeader(
                "CONDITIONS",
                "When all conditions are true...");

            DrawConditionList(
                rule.Conditions,
                state);

            ImGui.TableNextColumn();

            DrawColumnHeader(
                "ACTIONS",
                "...run these actions in order.");

            DrawActionList(
                rule.Actions,
                state);

            ImGui.EndTable();
        }

        return remove;
    }

    private static void DrawColumnHeader(
        string title,
        string description)
    {
        ImGui.Text(
            title);

        ImGui.TextDisabled(
            description);

        ImGui.Dummy(
            new Vector2(
                0.0f,
                7.0f));
    }

    // ========================================================
    // CONDITIONS
    // ========================================================

    private void DrawConditionList(
        List<VisualInstruction> conditions,
        EditorState? state)
    {
        for (int index = 0;
             index < conditions.Count;
             index++)
        {
            VisualInstruction instruction =
                conditions[index];

            ImGui.PushID(
                instruction.InstanceId.ToString());

            bool remove =
                DrawInstructionCard(
                    instruction,
                    true,
                    index,
                    state);

            ImGui.PopID();

            if (remove)
            {
                RecordHistory(
                    "Delete Condition");

                conditions.RemoveAt(
                    index);

                _dirty =
                    true;

                index--;

                continue;
            }

            ImGui.Dummy(
                new Vector2(
                    0.0f,
                    8.0f));
        }

        if (ImGui.Button(
                "+ Add Condition",
                new Vector2(
                    -1.0f,
                    36.0f)))
        {
            ImGui.OpenPopup(
                "Add Condition");
        }

        DrawConditionPicker(
            conditions);
    }

    // ========================================================
    // ACTIONS
    // ========================================================

    private void DrawActionList(
        List<VisualInstruction> actions,
        EditorState? state)
    {
        for (int index = 0;
             index < actions.Count;
             index++)
        {
            VisualInstruction instruction =
                actions[index];

            ImGui.PushID(
                instruction.InstanceId.ToString());

            bool remove =
                DrawInstructionCard(
                    instruction,
                    false,
                    index,
                    state);

            ImGui.PopID();

            if (remove)
            {
                RecordHistory(
                    "Delete Action");

                actions.RemoveAt(
                    index);

                _dirty =
                    true;

                index--;

                continue;
            }

            ImGui.Dummy(
                new Vector2(
                    0.0f,
                    8.0f));
        }

        if (ImGui.Button(
                "+ Add Action",
                new Vector2(
                    -1.0f,
                    36.0f)))
        {
            ImGui.OpenPopup(
                "Add Action");
        }

        DrawActionPicker(
            actions);
    }

    // ========================================================
    // INSTRUCTION CARDS
    // ========================================================

    private bool DrawInstructionCard(
        VisualInstruction instruction,
        bool condition,
        int index,
        EditorState? state)
    {
        string displayName =
            instruction.Id;

        string category =
            "Unknown";

        if (condition)
        {
            if (_registry.TryGetCondition(
                    instruction.Id,
                    out VisualConditionDefinition? definition) &&
                definition != null)
            {
                displayName =
                    definition.DisplayName;

                category =
                    definition.Category;
            }
        }
        else
        {
            if (_registry.TryGetAction(
                    instruction.Id,
                    out VisualActionDefinition? definition) &&
                definition != null)
            {
                displayName =
                    definition.DisplayName;

                category =
                    definition.Category;
            }
        }

        ImGui.Separator();

        ImGui.TextDisabled(
            $"{index + 1:00}");

        ImGui.SameLine();

        ImGui.Text(
            displayName);

        /*
         * Keep the remove control directly beside the instruction
         * title instead of trying to right-align it inside the event
         * table cell. The previous right-edge calculation could place
         * the Actions-side X outside the visible column, making actions
         * appear impossible to delete.
         */
        ImGui.SameLine();

        string removeLabel =
            condition
                ? "Remove Condition"
                : "Remove Action";

        bool remove =
            ImGui.SmallButton(
                removeLabel);

        ImGui.TextDisabled(
            category);

        ImGui.Dummy(
            new Vector2(
                0.0f,
                5.0f));

        DrawInstructionArguments(
            instruction,
            state);

        return remove;
    }

    // ========================================================
    // ADD CONDITION / ACTION
    // ========================================================

    private void DrawConditionPicker(
        List<VisualInstruction> conditions)
    {
        if (!ImGui.BeginPopup(
                "Add Condition"))
        {
            return;
        }

        ImGui.TextDisabled(
            "ADD CONDITION");

        ImGui.Separator();

        var groups =
            _registry.Conditions
                .OrderBy(
                    definition =>
                        definition.Category)
                .ThenBy(
                    definition =>
                        definition.DisplayName)
                .GroupBy(
                    definition =>
                        definition.Category);

        foreach (var group
                 in groups)
        {
            if (!ImGui.BeginMenu(
                    group.Key))
            {
                continue;
            }

            foreach (VisualConditionDefinition definition
                     in group)
            {
                if (ImGui.MenuItem(
                        definition.DisplayName))
                {
                    RecordHistory(
                        "Add Condition");

                    conditions.Add(
                        CreateInstruction(
                            definition.Id));

                    _dirty =
                        true;

                    ImGui.CloseCurrentPopup();
                }
            }

            ImGui.EndMenu();
        }

        ImGui.EndPopup();
    }

    private void DrawActionPicker(
        List<VisualInstruction> actions)
    {
        if (!ImGui.BeginPopup(
                "Add Action"))
        {
            return;
        }

        ImGui.TextDisabled(
            "ADD ACTION");

        ImGui.Separator();

        var groups =
            _registry.Actions
                .OrderBy(
                    definition =>
                        definition.Category)
                .ThenBy(
                    definition =>
                        definition.DisplayName)
                .GroupBy(
                    definition =>
                        definition.Category);

        foreach (var group
                 in groups)
        {
            if (!ImGui.BeginMenu(
                    group.Key))
            {
                continue;
            }

            foreach (VisualActionDefinition definition
                     in group)
            {
                if (ImGui.MenuItem(
                        definition.DisplayName))
                {
                    RecordHistory(
                        "Add Action");

                    actions.Add(
                        CreateInstruction(
                            definition.Id));

                    _dirty =
                        true;

                    ImGui.CloseCurrentPopup();
                }
            }

            ImGui.EndMenu();
        }

        ImGui.EndPopup();
    }

    // ========================================================
    // INSTRUCTION DEFAULTS
    // ========================================================

    private static VisualInstruction CreateInstruction(
        string id)
    {
        var instruction =
            new VisualInstruction
            {
                Id = id
            };

        switch (id)
        {
            case "input.keyHeld":
            case "input.keyPressed":
            case "input.keyReleased":

                instruction.Arguments["key"] =
                    EventValue.String(
                        "W");

                break;

            case "character.moveForward":
            case "character.moveRight":

                instruction.Arguments["amount"] =
                    EventValue.Number(
                        1.0);

                break;

            case "character.setVelocity":

                instruction.Arguments["velocity"] =
                    EventValue.Vector3(
                        Vector3.Zero);

                break;

            case "character.addImpulse":

                instruction.Arguments["impulse"] =
                    EventValue.Vector3(
                        Vector3.Zero);

                break;

            case "transform.setPosition":

                instruction.Arguments["position"] =
                    EventValue.Vector3(
                        Vector3.Zero);

                break;

            case "transform.move":

                instruction.Arguments["amount"] =
                    EventValue.Vector3(
                        Vector3.Zero);

                break;

            case "transform.setX":
            case "transform.setY":
            case "transform.setZ":

                instruction.Arguments["value"] =
                    EventValue.Number(
                        0.0);

                break;

            case "variable.compare":

                instruction.Arguments["left"] =
                    EventValue.Number(
                        0.0);

                instruction.Arguments["operator"] =
                    EventValue.String(
                        "==");

                instruction.Arguments["right"] =
                    EventValue.Number(
                        0.0);

                break;

            case "variable.set":

                instruction.Arguments["value"] =
                    EventValue.Number(
                        0.0);

                break;

            case "variable.add":
            case "variable.subtract":

                instruction.Arguments["amount"] =
                    EventValue.Number(
                        1.0);

                break;
        }

        return instruction;
    }

    // ========================================================
    // ARGUMENT UI
    // ========================================================

    private void DrawInstructionArguments(
        VisualInstruction instruction,
        EditorState? state)
    {
        switch (instruction.Id)
        {
            case "input.keyHeld":
            case "input.keyPressed":
            case "input.keyReleased":

                DrawKeyArgument(
                    instruction,
                    "key",
                    "Key",
                    Key.W);

                break;

            case "character.moveForward":
            case "character.moveRight":

                DrawValueArgument(
                    instruction,
                    "amount",
                    "Amount",
                    VariableType.Number,
                    EventValue.Number(
                        1.0),
                    state,
                    false);

                break;

            case "character.setVelocity":

                DrawValueArgument(
                    instruction,
                    "velocity",
                    "Velocity",
                    VariableType.Vector3,
                    EventValue.Vector3(
                        Vector3.Zero),
                    state,
                    false);

                break;

            case "character.addImpulse":

                DrawValueArgument(
                    instruction,
                    "impulse",
                    "Impulse",
                    VariableType.Vector3,
                    EventValue.Vector3(
                        Vector3.Zero),
                    state,
                    false);

                break;

            case "transform.setPosition":

                DrawValueArgument(
                    instruction,
                    "position",
                    "Position",
                    VariableType.Vector3,
                    EventValue.Vector3(
                        Vector3.Zero),
                    state,
                    false);

                break;

            case "transform.move":

                DrawValueArgument(
                    instruction,
                    "amount",
                    "Amount",
                    VariableType.Vector3,
                    EventValue.Vector3(
                        Vector3.Zero),
                    state,
                    false);

                break;

            case "transform.setX":
            case "transform.setY":
            case "transform.setZ":

                DrawValueArgument(
                    instruction,
                    "value",
                    "Value",
                    VariableType.Number,
                    EventValue.Number(
                        0.0),
                    state,
                    false);

                break;

            // --------------------------------------------
            // VARIABLE / PROPERTY CONDITIONS
            // --------------------------------------------

            case "variable.compare":

                DrawValueArgument(
                    instruction,
                    "left",
                    "Left",
                    null,
                    EventValue.Number(
                        0.0),
                    state,
                    false);

                DrawComparisonOperator(
                    instruction);

                DrawValueArgument(
                    instruction,
                    "right",
                    "Right",
                    null,
                    EventValue.Number(
                        0.0),
                    state,
                    false);

                break;

            // --------------------------------------------
            // VARIABLE / PROPERTY ACTIONS
            // --------------------------------------------

            case "variable.set":

                DrawTargetReference(
                    instruction,
                    "target",
                    "Target",
                    null,
                    state);

                DrawValueArgument(
                    instruction,
                    "value",
                    "Value",
                    null,
                    EventValue.Number(
                        0.0),
                    state,
                    false);

                break;

            case "variable.add":

                DrawTargetReference(
                    instruction,
                    "target",
                    "Target",
                    VariableType.Number,
                    state);

                DrawValueArgument(
                    instruction,
                    "amount",
                    "Amount",
                    VariableType.Number,
                    EventValue.Number(
                        1.0),
                    state,
                    false);

                break;

            case "variable.subtract":

                DrawTargetReference(
                    instruction,
                    "target",
                    "Target",
                    VariableType.Number,
                    state);

                DrawValueArgument(
                    instruction,
                    "amount",
                    "Amount",
                    VariableType.Number,
                    EventValue.Number(
                        1.0),
                    state,
                    false);

                break;
        }
    }

    // ========================================================
    // GENERIC VALUE EDITOR
    // ========================================================

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
                out VariableReference? selected) &&
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