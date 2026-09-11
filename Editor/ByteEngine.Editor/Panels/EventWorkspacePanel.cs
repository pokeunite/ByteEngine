using System.Numerics;

using ByteEngine.Core;
using ByteEngine.Core.Assets;
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

    public void Open(
        AssetRecord asset,
        EditorLog log)
    {
        ArgumentNullException.ThrowIfNull(
            asset
        );

        /*
         * If this document is already loaded, just reopen/focus it.
         *
         * This preserves unsaved changes if the user closes the
         * tab and immediately opens it again.
         */
        if (_asset?.Guid ==
                asset.Guid &&
            _module !=
                null)
        {
            _open =
                true;

            _requestFocus =
                true;

            return;
        }

        try
        {
            _module =
                _serializer.Load(
                    asset.FullPath
                );

            _asset =
                asset;

            _open =
                true;

            _dirty =
                false;

            _requestFocus =
                true;

            log.Info(
                $"Opened Event Module '{asset.ProjectPath}'."
            );
        }
        catch (Exception exception)
        {
            log.Error(
                $"Could not open Event Module '{asset.ProjectPath}': {exception.Message}"
            );
        }
    }

    public void Draw(
        EditorLog log)
    {
        if (!_open ||
            _module ==
                null ||
            _asset ==
                null)
        {
            return;
        }

        uint sceneDockId =
            EditorWorkspaceDocking.SceneDocumentDockId;

        /*
         * First time an Event Module is shown, dock it into the
         * same document node as Scene View.
         *
         * FirstUseEver is important:
         * after the user drags this tab elsewhere, ImGui remembers
         * their layout and we do NOT force it back.
         */
        if (sceneDockId !=
            0)
        {
            ImGui.SetNextWindowDockID(
                sceneDockId,
                ImGuiCond.FirstUseEver
            );
        }

        ImGui.SetNextWindowSize(
            new Vector2(
                1100.0f,
                720.0f
            ),
            ImGuiCond.FirstUseEver
        );

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

        /*
         * Visible title:
         * CharacterMovement *
         *
         * Internal ID includes GUID so many Event Modules can
         * be open as individual dock tabs.
         */
        bool visible =
            ImGui.Begin(
                $"{_module.Name}{dirtyMarker}##EventWorkspace:{_asset.Guid}",
                ref open
            );

        _open =
            open;

        if (!visible)
        {
            ImGui.End();

            return;
        }

        ImGui.PushStyleVar(
            ImGuiStyleVar.ItemSpacing,
            new Vector2(
                10.0f,
                9.0f
            )
        );

        ImGui.PushStyleVar(
            ImGuiStyleVar.FramePadding,
            new Vector2(
                8.0f,
                6.0f
            )
        );

        DrawDocumentToolbar(
            log
        );

        ImGui.Dummy(
            new Vector2(
                0.0f,
                4.0f
            )
        );

        DrawModuleSettings();

        ImGui.Dummy(
            new Vector2(
                0.0f,
                8.0f
            )
        );

        DrawRules();

        ImGui.PopStyleVar(
            2
        );

        ImGui.End();
    }

    private void DrawDocumentToolbar(
        EditorLog log)
    {
        ImGui.TextDisabled(
            "EVENT MODULE"
        );

        ImGui.SameLine();

        ImGui.Text(
            _module!.Name
        );

        ImGui.SameLine();

        ImGui.TextDisabled(
            "   "
        );

        ImGui.SameLine();

        ImGui.BeginDisabled(
            !_dirty
        );

        if (ImGui.Button(
                "Save"))
        {
            Save(
                log
            );
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        if (ImGui.Button(
                "+ Add Event"))
        {
            _module.Rules.Add(
                new EventRuleDefinition()
            );

            _dirty =
                true;
        }

        ImGui.SameLine();

        ImGui.TextDisabled(
            _asset!.ProjectPath
        );
    }

    private void Save(
        EditorLog log)
    {
        if (_module ==
                null ||
            _asset ==
                null)
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
                        _asset.FullPath
                    );
            }

            _serializer.Save(
                _module,
                _asset.FullPath
            );

            _dirty =
                false;

            log.Info(
                $"Saved Event Module '{_asset.ProjectPath}'."
            );
        }
        catch (Exception exception)
        {
            log.Error(
                $"Could not save Event Module '{_asset.ProjectPath}': {exception.Message}"
            );
        }
    }

    // ========================================================
    // MODULE SETTINGS
    // ========================================================

    private void DrawModuleSettings()
    {
        if (_module ==
            null)
        {
            return;
        }

        /*
         * Keep metadata collapsed by default.
         *
         * Most of the screen should belong to gameplay logic,
         * not technical asset information.
         */
        if (!ImGui.CollapsingHeader(
                "Module Settings"))
        {
            return;
        }

        ImGui.Indent(
            12.0f
        );

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
            $"Asset ID: {_module.Id}"
        );

        ImGui.TextDisabled(
            $"Format Version: {_module.Version}"
        );

        if (_module.TargetBlueprintGuid.HasValue)
        {
            ImGui.Text(
                $"Target Blueprint: {_module.TargetBlueprintGuid.Value}"
            );
        }
        else
        {
            ImGui.TextDisabled(
                "Target Blueprint: Generic"
            );
        }

        ImGui.SeparatorText(
            "Required Components"
        );

        if (_module.RequiredComponents.Count ==
            0)
        {
            ImGui.TextDisabled(
                "None"
            );
        }
        else
        {
            foreach (string component
                     in _module.RequiredComponents)
            {
                ImGui.BulletText(
                    component
                );
            }
        }

        ImGui.Unindent(
            12.0f
        );
    }

    // ========================================================
    // EVENTS
    // ========================================================

    private void DrawRules()
    {
        if (_module ==
            null)
        {
            return;
        }

        if (_module.Rules.Count ==
            0)
        {
            ImGui.Dummy(
                new Vector2(
                    0.0f,
                    30.0f
                )
            );

            ImGui.TextDisabled(
                "This module has no events."
            );

            ImGui.TextDisabled(
                "Click '+ Add Event' above to begin."
            );

            return;
        }

        for (int index = 0;
             index < _module.Rules.Count;
             index++)
        {
            EventRuleDefinition rule =
                _module.Rules[index];

            ImGui.PushID(
                rule.Id.ToString()
            );

            bool remove =
                DrawRule(
                    rule,
                    index
                );

            ImGui.PopID();

            if (remove)
            {
                _module.Rules.RemoveAt(
                    index
                );

                _dirty =
                    true;

                index--;
            }

            ImGui.Dummy(
                new Vector2(
                    0.0f,
                    12.0f
                )
            );
        }
    }

    private bool DrawRule(
        EventRuleDefinition rule,
        int index)
    {
        /*
         * Large event separator/header.
         */
        ImGui.SeparatorText(
            $"EVENT {index + 1}"
        );

        ImGui.Dummy(
            new Vector2(
                0.0f,
                3.0f
            )
        );

        bool enabled =
            rule.Enabled;

        if (ImGui.Checkbox(
                "Enabled",
                ref enabled))
        {
            rule.Enabled =
                enabled;

            _dirty =
                true;
        }

        ImGui.SameLine();

        ImGui.TextDisabled(
            $"Event ID: {rule.Id.ToString()[..8]}"
        );

        float deleteWidth =
            110.0f;

        float available =
            ImGui.GetContentRegionAvail().X;

        if (available >
            deleteWidth)
        {
            ImGui.SameLine(
                ImGui.GetCursorPosX() +
                available -
                deleteWidth
            );
        }

        bool removeRule =
            ImGui.Button(
                "Delete Event"
            );

        ImGui.Dummy(
            new Vector2(
                0.0f,
                5.0f
            )
        );

        if (rule.Conditions.Count ==
            0)
        {
            ImGui.TextColored(
                new Vector4(
                    1.0f,
                    0.75f,
                    0.25f,
                    1.0f
                ),
                "No conditions - this event executes every frame."
            );

            ImGui.Dummy(
                new Vector2(
                    0.0f,
                    4.0f
                )
            );
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
                ImGuiTableColumnFlags.WidthStretch,
                1.0f
            );

            ImGui.TableSetupColumn(
                "Actions",
                ImGuiTableColumnFlags.WidthStretch,
                1.0f
            );

            ImGui.TableNextRow();

            // CONDITIONS
            ImGui.TableNextColumn();

            DrawColumnHeader(
                "CONDITIONS",
                "When all conditions are true..."
            );

            DrawConditionList(
                rule.Conditions
            );

            // ACTIONS
            ImGui.TableNextColumn();

            DrawColumnHeader(
                "ACTIONS",
                "...run these actions in order."
            );

            DrawActionList(
                rule.Actions
            );

            ImGui.EndTable();
        }

        return removeRule;
    }

    private static void DrawColumnHeader(
        string title,
        string description)
    {
        ImGui.Text(
            title
        );

        ImGui.TextDisabled(
            description
        );

        ImGui.Dummy(
            new Vector2(
                0.0f,
                5.0f
            )
        );
    }

    // ========================================================
    // CONDITIONS
    // ========================================================

    private void DrawConditionList(
        List<VisualInstruction> conditions)
    {
        for (int index = 0;
             index < conditions.Count;
             index++)
        {
            VisualInstruction instruction =
                conditions[index];

            ImGui.PushID(
                instruction.InstanceId.ToString()
            );

            bool remove =
                DrawInstructionCard(
                    instruction,
                    true,
                    index
                );

            ImGui.PopID();

            if (remove)
            {
                conditions.RemoveAt(
                    index
                );

                _dirty =
                    true;

                index--;

                continue;
            }

            ImGui.Dummy(
                new Vector2(
                    0.0f,
                    7.0f
                )
            );
        }

        ImGui.Dummy(
            new Vector2(
                0.0f,
                3.0f
            )
        );

        if (ImGui.Button(
                "+ Add Condition",
                new Vector2(
                    -1.0f,
                    34.0f
                )))
        {
            ImGui.OpenPopup(
                "Add Condition"
            );
        }

        DrawConditionPicker(
            conditions
        );
    }

    // ========================================================
    // ACTIONS
    // ========================================================

    private void DrawActionList(
        List<VisualInstruction> actions)
    {
        for (int index = 0;
             index < actions.Count;
             index++)
        {
            VisualInstruction instruction =
                actions[index];

            ImGui.PushID(
                instruction.InstanceId.ToString()
            );

            bool remove =
                DrawInstructionCard(
                    instruction,
                    false,
                    index
                );

            ImGui.PopID();

            if (remove)
            {
                actions.RemoveAt(
                    index
                );

                _dirty =
                    true;

                index--;

                continue;
            }

            ImGui.Dummy(
                new Vector2(
                    0.0f,
                    7.0f
                )
            );
        }

        ImGui.Dummy(
            new Vector2(
                0.0f,
                3.0f
            )
        );

        if (ImGui.Button(
                "+ Add Action",
                new Vector2(
                    -1.0f,
                    34.0f
                )))
        {
            ImGui.OpenPopup(
                "Add Action"
            );
        }

        DrawActionPicker(
            actions
        );
    }

    // ========================================================
    // INSTRUCTION CARD
    // ========================================================

    private bool DrawInstructionCard(
        VisualInstruction instruction,
        bool condition,
        int index)
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
                definition !=
                null)
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
                definition !=
                null)
            {
                displayName =
                    definition.DisplayName;

                category =
                    definition.Category;
            }
        }

        ImGui.Separator();

        ImGui.TextDisabled(
            $"{index + 1:00}"
        );

        ImGui.SameLine();

        ImGui.Text(
            displayName
        );

        float available =
            ImGui.GetContentRegionAvail().X;

        if (available >
            35.0f)
        {
            ImGui.SameLine(
                ImGui.GetCursorPosX() +
                available -
                30.0f
            );
        }

        bool remove =
            ImGui.SmallButton(
                "X"
            );

        ImGui.TextDisabled(
            category
        );

        ImGui.Dummy(
            new Vector2(
                0.0f,
                2.0f
            )
        );

        DrawInstructionArguments(
            instruction
        );

        return remove;
    }

    // ========================================================
    // PICKERS
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
            "ADD CONDITION"
        );

        ImGui.Separator();

        IEnumerable<IGrouping<
            string,
            VisualConditionDefinition>> groups =
                _registry.Conditions
                    .Where(
                        definition =>
                            CanAuthorCondition(
                                definition.Id
                            )
                    )
                    .OrderBy(
                        definition =>
                            definition.Category
                    )
                    .ThenBy(
                        definition =>
                            definition.DisplayName
                    )
                    .GroupBy(
                        definition =>
                            definition.Category
                    );

        foreach (IGrouping<
                     string,
                     VisualConditionDefinition> group
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
                    conditions.Add(
                        CreateInstruction(
                            definition.Id
                        )
                    );

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
            "ADD ACTION"
        );

        ImGui.Separator();

        IEnumerable<IGrouping<
            string,
            VisualActionDefinition>> groups =
                _registry.Actions
                    .Where(
                        definition =>
                            CanAuthorAction(
                                definition.Id
                            )
                    )
                    .OrderBy(
                        definition =>
                            definition.Category
                    )
                    .ThenBy(
                        definition =>
                            definition.DisplayName
                    )
                    .GroupBy(
                        definition =>
                            definition.Category
                    );

        foreach (IGrouping<
                     string,
                     VisualActionDefinition> group
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
                    actions.Add(
                        CreateInstruction(
                            definition.Id
                        )
                    );

                    _dirty =
                        true;

                    ImGui.CloseCurrentPopup();
                }
            }

            ImGui.EndMenu();
        }

        ImGui.EndPopup();
    }

    private static bool CanAuthorCondition(
        string id)
    {
        return !id.StartsWith(
            "variable.",
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static bool CanAuthorAction(
        string id)
    {
        return !id.StartsWith(
            "variable.",
            StringComparison.OrdinalIgnoreCase
        );
    }

    // ========================================================
    // DEFAULT INSTRUCTION CREATION
    // ========================================================

    private static VisualInstruction CreateInstruction(
        string id)
    {
        var instruction =
            new VisualInstruction
            {
                Id =
                    id
            };

        switch (id)
        {
            case "input.keyHeld":
            case "input.keyPressed":
            case "input.keyReleased":

                instruction.Arguments["key"] =
                    EventValue.String(
                        "W"
                    );

                break;

            case "character.moveForward":
            case "character.moveRight":

                instruction.Arguments["amount"] =
                    EventValue.Number(
                        1.0
                    );

                break;

            case "character.setVelocity":

                instruction.Arguments["velocity"] =
                    EventValue.Vector3(
                        Vector3.Zero
                    );

                break;

            case "character.addImpulse":

                instruction.Arguments["impulse"] =
                    EventValue.Vector3(
                        Vector3.Zero
                    );

                break;

            case "transform.setPosition":

                instruction.Arguments["position"] =
                    EventValue.Vector3(
                        Vector3.Zero
                    );

                break;

            case "transform.move":

                instruction.Arguments["amount"] =
                    EventValue.Vector3(
                        Vector3.Zero
                    );

                break;

            case "transform.setX":
            case "transform.setY":
            case "transform.setZ":

                instruction.Arguments["value"] =
                    EventValue.Number(
                        0.0
                    );

                break;
        }

        return instruction;
    }

    // ========================================================
    // ARGUMENT UI
    // ========================================================

    private void DrawInstructionArguments(
        VisualInstruction instruction)
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
                    Key.W
                );

                break;

            case "character.moveForward":
            case "character.moveRight":

                DrawNumberArgument(
                    instruction,
                    "amount",
                    "Amount",
                    1.0f
                );

                break;

            case "character.setVelocity":

                DrawVector3Argument(
                    instruction,
                    "velocity",
                    "Velocity",
                    Vector3.Zero
                );

                break;

            case "character.addImpulse":

                DrawVector3Argument(
                    instruction,
                    "impulse",
                    "Impulse",
                    Vector3.Zero
                );

                break;

            case "transform.setPosition":

                DrawVector3Argument(
                    instruction,
                    "position",
                    "Position",
                    Vector3.Zero
                );

                break;

            case "transform.move":

                DrawVector3Argument(
                    instruction,
                    "amount",
                    "Amount",
                    Vector3.Zero
                );

                break;

            case "transform.setX":
            case "transform.setY":
            case "transform.setZ":

                DrawNumberArgument(
                    instruction,
                    "value",
                    "Value",
                    0.0f
                );

                break;
        }
    }

    private void DrawKeyArgument(
        VisualInstruction instruction,
        string argumentName,
        string label,
        Key fallback)
    {
        EventValue value =
            GetOrCreateStringArgument(
                instruction,
                argumentName,
                fallback.ToString()
            );

        if (value.Kind ==
            EventValueKind.Reference)
        {
            DrawReferencePlaceholder(
                instruction,
                argumentName,
                value,
                EventValue.String(
                    fallback.ToString()
                )
            );

            return;
        }

        string current =
            value.Constant.Type ==
            VariableType.String
                ? value.Constant.String
                : fallback.ToString();

        ImGui.SetNextItemWidth(
            -1.0f
        );

        if (!ImGui.BeginCombo(
                label,
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
                    StringComparison.OrdinalIgnoreCase
                );

            if (ImGui.Selectable(
                    name,
                    selected))
            {
                instruction.Arguments[argumentName] =
                    EventValue.String(
                        name
                    );

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

    private void DrawNumberArgument(
        VisualInstruction instruction,
        string argumentName,
        string label,
        float fallback)
    {
        EventValue value =
            GetOrCreateNumberArgument(
                instruction,
                argumentName,
                fallback
            );

        if (value.Kind ==
            EventValueKind.Reference)
        {
            DrawReferencePlaceholder(
                instruction,
                argumentName,
                value,
                EventValue.Number(
                    fallback
                )
            );

            return;
        }

        float current =
            value.Constant.Type ==
            VariableType.Number
                ? (float)value.Constant.Number
                : fallback;

        ImGui.SetNextItemWidth(
            -1.0f
        );

        if (ImGui.DragFloat(
                label,
                ref current,
                0.05f))
        {
            instruction.Arguments[argumentName] =
                EventValue.Number(
                    current
                );

            _dirty =
                true;
        }
    }

    private void DrawVector3Argument(
        VisualInstruction instruction,
        string argumentName,
        string label,
        Vector3 fallback)
    {
        EventValue value =
            GetOrCreateVector3Argument(
                instruction,
                argumentName,
                fallback
            );

        if (value.Kind ==
            EventValueKind.Reference)
        {
            DrawReferencePlaceholder(
                instruction,
                argumentName,
                value,
                EventValue.Vector3(
                    fallback
                )
            );

            return;
        }

        Vector3 current =
            value.Constant.Type ==
            VariableType.Vector3
                ? value.Constant.Vector3
                : fallback;

        ImGui.TextDisabled(
            label
        );

        ImGui.PushID(
            argumentName
        );

        float x =
            current.X;

        float y =
            current.Y;

        float z =
            current.Z;

        ImGui.SetNextItemWidth(
            -1.0f
        );

        bool changed =
            ImGui.DragFloat(
                "X",
                ref x,
                0.05f
            );

        ImGui.SetNextItemWidth(
            -1.0f
        );

        changed |=
            ImGui.DragFloat(
                "Y",
                ref y,
                0.05f
            );

        ImGui.SetNextItemWidth(
            -1.0f
        );

        changed |=
            ImGui.DragFloat(
                "Z",
                ref z,
                0.05f
            );

        ImGui.PopID();

        if (changed)
        {
            instruction.Arguments[argumentName] =
                EventValue.Vector3(
                    new Vector3(
                        x,
                        y,
                        z
                    )
                );

            _dirty =
                true;
        }
    }

    private void DrawReferencePlaceholder(
        VisualInstruction instruction,
        string argumentName,
        EventValue value,
        EventValue constantFallback)
    {
        string referenceText =
            value.Reference ==
            null
                ? "Invalid Reference"
                : FormatReference(
                    value.Reference
                );

        ImGui.TextDisabled(
            $"Reference: {referenceText}"
        );

        if (ImGui.Button(
                "Use Constant"))
        {
            instruction.Arguments[argumentName] =
                constantFallback;

            _dirty =
                true;
        }
    }

    // ========================================================
    // VALUE HELPERS
    // ========================================================

    private static string FormatReference(
        VariableReference reference)
    {
        string prefix =
            reference.Scope.ToString();

        if (!string.IsNullOrWhiteSpace(
                reference.ObjectName))
        {
            prefix +=
                $".{reference.ObjectName}";
        }

        if (!string.IsNullOrWhiteSpace(
                reference.ComponentType))
        {
            prefix +=
                $".{reference.ComponentType}";
        }

        if (!string.IsNullOrWhiteSpace(
                reference.MemberName))
        {
            prefix +=
                $".{reference.MemberName}";
        }

        return prefix;
    }

    private static EventValue GetOrCreateStringArgument(
        VisualInstruction instruction,
        string name,
        string fallback)
    {
        if (instruction.Arguments.TryGetValue(
                name,
                out EventValue? value))
        {
            return value;
        }

        value =
            EventValue.String(
                fallback
            );

        instruction.Arguments[name] =
            value;

        return value;
    }

    private static EventValue GetOrCreateNumberArgument(
        VisualInstruction instruction,
        string name,
        double fallback)
    {
        if (instruction.Arguments.TryGetValue(
                name,
                out EventValue? value))
        {
            return value;
        }

        value =
            EventValue.Number(
                fallback
            );

        instruction.Arguments[name] =
            value;

        return value;
    }

    private static EventValue GetOrCreateVector3Argument(
        VisualInstruction instruction,
        string name,
        Vector3 fallback)
    {
        if (instruction.Arguments.TryGetValue(
                name,
                out EventValue? value))
        {
            return value;
        }

        value =
            EventValue.Vector3(
                fallback
            );

        instruction.Arguments[name] =
            value;

        return value;
    }
}