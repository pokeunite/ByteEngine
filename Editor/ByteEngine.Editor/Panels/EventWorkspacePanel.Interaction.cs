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
    private void DrawConditionCreateForGate(
        EventRuleDefinition? owner, VisualInstruction gate, Vector2 position)
    {
        foreach (IGrouping<string, VisualConditionDefinition> group in _registry.Conditions
                     .Where(definition => !IsLegacyRaycastCreationDefinition(definition.Id))
                     .OrderBy(definition => definition.Category)
                     .ThenBy(definition => definition.DisplayName)
                     .GroupBy(definition => definition.Category))
        {
            if (!ImGui.BeginMenu(group.Key))
                continue;
            foreach (VisualConditionDefinition definition in group)
            {
                if (!ImGui.MenuItem(definition.DisplayName))
                    continue;
                if (owner == null)
                {
                    CreateLooseInstruction(definition.Id, true, position);
                    Guid sourceId = _selectedGraphNodes.First();
                    if (!gate.ConditionInputIds.Contains(sourceId))
                        gate.ConditionInputIds.Add(sourceId);
                }
                else
                {
                    RecordHistory("Add Logic Input");
                    VisualInstruction created = AddInstructionAt(owner,
                        definition.Id, true, position, Guid.Empty);
                    ConnectConditionToGate(owner, gate, created.InstanceId);
                }
                _dirty = true;
            }
            ImGui.EndMenu();
        }
    }

    private void HandleGraphBackgroundContext()
    {
        /*
         * Context-menu opening is handled in
         * UpdateGraphPointerInteraction before node windows are drawn.
         * Keeping this method preserves DrawGraphCanvas ordering while
         * avoiding duplicate popup-open requests.
         */
    }

    private void DrawGraphContextPopup()
    {
        if (_module ==
                null ||
            !ImGui.BeginPopup(
                "ByteGraph Context"))
        {
            return;
        }

        if (ImGui.MenuItem(
                "Add Event"))
        {
            RequestAddEvent(
                _graphContextPosition);
        }

        if (ImGui.BeginMenu("Add Condition (Unconnected)"))
        {
            DrawLooseDefinitionCreateMenu(true, _graphContextPosition);
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Add Action (Unconnected)"))
        {
            DrawLooseDefinitionCreateMenu(false, _graphContextPosition);
            ImGui.EndMenu();
        }

        if (_module.Rules.Count >
            0)
        {
            if (ImGui.BeginMenu(
                    "Add Condition To"))
            {
                foreach ((EventRuleDefinition rule, string label)
                         in GetRuleMenuEntries())
                {
                    if (ImGui.BeginMenu(
                            label))
                    {
                        DrawDefinitionCreateMenu(
                            rule,
                            true,
                            _graphContextPosition,
                            Guid.Empty);

                        ImGui.EndMenu();
                    }
                }

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu(
                    "Add Action To"))
            {
                foreach ((EventRuleDefinition rule, string label)
                         in GetRuleMenuEntries())
                {
                    if (ImGui.BeginMenu(
                            label))
                    {
                        DrawDefinitionCreateMenu(
                            rule,
                            false,
                            _graphContextPosition,
                            Guid.Empty);

                        ImGui.EndMenu();
                    }
                }

                ImGui.EndMenu();
            }
        }

        ImGui.Separator();

        EventRuleDefinition? selectedEvent = _module.Rules
            .FirstOrDefault(rule => _selectedGraphNodes.Contains(rule.Id));
        if (selectedEvent != null && ImGui.MenuItem("Rename", "F2"))
            RequestRenameEvent(selectedEvent);

        ImGui.Separator();

        ImGui.BeginDisabled(
            _selectedGraphNodes.Count ==
            0);

        if (ImGui.MenuItem(
                "Add Comment"))
        {
            CreateGroupFromSelection();
        }
        ImGui.BeginDisabled(_selectedGraphNodes.Count < 2);
        if (ImGui.MenuItem("Collapse Selection"))
            CollapseSelectedGraphNodes();
        ImGui.EndDisabled();

        ImGui.Separator();
        if (ImGui.MenuItem(
                "Delete"))
        {
            DeleteSelectedGraphNodes();
        }

        ImGui.EndDisabled();

        if (_selectedGraphNodes.Count >
                0 &&
            ImGui.MenuItem(
                "Clear Selection"))
        {
            _selectedGraphNodes.Clear();
        }

        ImGui.Separator();

        if (ImGui.MenuItem(
                _selectedGraphNodes.Count > 0 ? "Arrange Selection" : "Arrange Graph"))
        {
            RecordHistory(
                "Auto Arrange Graph");

            AutoArrangeGraph();

            _requestFrameGraph =
                true;

            _dirty =
                true;
        }

        if (ImGui.MenuItem(
                _selectedGraphNodes.Count > 0 ? "Frame Selection" : "Frame Graph"))
        {
            _requestFrameGraph =
                true;
        }

        ImGui.EndPopup();
    }

    private void DrawLooseDefinitionCreateMenu(bool condition, Vector2 position,
        Guid sourceId = default, WireDragKind sourceKind = WireDragKind.None)
    {
        if (_module == null)
            return;

        if (condition)
        {
            foreach (IGrouping<string, VisualConditionDefinition> group in _registry.Conditions
                         .Where(definition => !IsLegacyRaycastCreationDefinition(definition.Id))
                         .Where(definition => sourceId == Guid.Empty ||
                             definition.Id is "logic.and" or "logic.or")
                         .OrderBy(definition => definition.Category)
                         .ThenBy(definition => definition.DisplayName)
                         .GroupBy(definition => definition.Category))
            {
                if (!ImGui.BeginMenu(group.Key))
                    continue;
                foreach (VisualConditionDefinition definition in group)
                    if (ImGui.MenuItem(definition.DisplayName))
                    {
                        VisualInstruction created =
                            CreateLooseInstruction(definition.Id, true, position);
                        if (sourceId != Guid.Empty)
                            created.ConditionInputIds.Add(sourceId);
                    }
                ImGui.EndMenu();
            }
        }
        else
        {
            foreach (IGrouping<string, VisualActionDefinition> group in _registry.Actions
                         .Where(definition => !IsLegacyRaycastCreationDefinition(definition.Id))
                         .OrderBy(definition => definition.Category)
                         .ThenBy(definition => definition.DisplayName)
                         .GroupBy(definition => definition.Category))
            {
                if (!ImGui.BeginMenu(group.Key))
                    continue;
                foreach (VisualActionDefinition definition in group)
                    if (ImGui.MenuItem(definition.DisplayName))
                    {
                        VisualInstruction created =
                            CreateLooseInstruction(definition.Id, false, position);
                        VisualInstruction? source = _module.EditorLooseActions
                            .FirstOrDefault(item => item.InstanceId == sourceId);
                        if (source != null)
                        {
                            if (sourceKind == WireDragKind.ActionTrue)
                                source.TrueActionId = created.InstanceId;
                            else if (sourceKind == WireDragKind.ActionFalse)
                                source.FalseActionId = created.InstanceId;
                            else
                                source.NextActionId = created.InstanceId;
                        }
                    }
                ImGui.EndMenu();
            }
        }
    }

    private VisualInstruction CreateLooseInstruction(string definitionId, bool condition, Vector2 position)
    {
        if (_module == null)
            throw new InvalidOperationException("An Event Sheet must be open.");

        RecordHistory(condition ? "Add Draft Condition" : "Add Draft Action");
        VisualInstruction instruction = CreateInstruction(definitionId);
        instruction.EditorX = position.X;
        instruction.EditorY = position.Y;
        instruction.EditorLayoutInitialized = true;
        (condition ? _module.EditorLooseConditions : _module.EditorLooseActions)
            .Add(instruction);
        _selectedGraphNodes.Clear();
        _selectedGraphNodes.Add(instruction.InstanceId);
        _dirty = true;
        return instruction;
    }

    private void DrawDefinitionCreateMenu(
        EventRuleDefinition rule,
        bool condition,
        Vector2 position,
        Guid insertAfterInstructionId,
        bool connectFromWire = false)
    {
        if (condition)
        {
            if (ImGui.BeginMenu(
                    "Mouse"))
            {
                DrawMouseConditionCreateItems(
                    rule,
                    position,
                    insertAfterInstructionId);

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu(
                    "Logic"))
            {
                if (ImGui.MenuItem(
                        "AND"))
                {
                    RecordHistory(
                        "Add AND");

                    VisualInstruction created =
                        AddInstructionAt(
                            rule,
                            "logic.and",
                            true,
                            position,
                            insertAfterInstructionId);

                    ConfigureCreatedLogicGate(
                        rule,
                        created,
                        insertAfterInstructionId,
                        connectFromWire);
                }

                if (ImGui.MenuItem(
                        "OR"))
                {
                    RecordHistory(
                        "Add OR");

                    VisualInstruction created =
                        AddInstructionAt(
                            rule,
                            "logic.or",
                            true,
                            position,
                            insertAfterInstructionId);

                    ConfigureCreatedLogicGate(
                        rule,
                        created,
                        insertAfterInstructionId,
                        connectFromWire);
                }

                ImGui.EndMenu();
            }

            var groups =
                _registry.Conditions
                    .Where(definition => !IsLegacyRaycastCreationDefinition(definition.Id))
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

                        VisualInstruction created =
                            AddInstructionAt(
                                rule,
                                definition.Id,
                                true,
                                position,
                                insertAfterInstructionId);

                        ConnectCondition(
                            rule,
                            created.InstanceId);
                    }
                }

                ImGui.EndMenu();
            }

            return;
        }

        if (ImGui.BeginMenu(
                "Flow"))
        {
            if (ImGui.MenuItem(
                    "Branch"))
            {
                RecordHistory(
                    "Add Branch");

                VisualInstruction created =
                    AddInstructionAt(
                        rule,
                        "flow.branch",
                        false,
                        position,
                        insertAfterInstructionId,
                        connectFromWire &&
                        insertAfterInstructionId ==
                            Guid.Empty);

                if (connectFromWire)
                {
                    ConnectNewActionAfterSource(
                        rule,
                        insertAfterInstructionId,
                        created,
                        _pendingWireCreateKind);
                }
                else
                {
                    AppendActionToExecutionFlow(
                        rule,
                        created);
                }
            }

            ImGui.EndMenu();
        }

        var actionGroups =
            _registry.Actions
                .Where(definition => !IsLegacyRaycastCreationDefinition(definition.Id))
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
                 in actionGroups)
        {
            if (!ImGui.BeginMenu(
                    group.Key))
            {
                continue;
            }

            if (group.Key.Equals(
                    "Object",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (ImGui.MenuItem(
                        "Spawn Empty Object"))
                {
                    RecordHistory(
                        "Add Spawn Empty Object");

                    VisualInstruction created =
                        AddInstructionAt(
                            rule,
                            "object.spawnEmpty",
                            false,
                            position,
                            insertAfterInstructionId,
                            connectFromWire &&
                            insertAfterInstructionId ==
                                Guid.Empty);

                    if (connectFromWire)
                    {
                        ConnectNewActionAfterSource(
                            rule,
                            insertAfterInstructionId,
                            created,
                            _pendingWireCreateKind);
                    }
                    else
                    {
                        AppendActionToExecutionFlow(
                            rule,
                            created);
                    }
                }

                if (ImGui.MenuItem(
                        "Spawn Blueprint"))
                {
                    RecordHistory(
                        "Add Spawn Blueprint");

                    VisualInstruction created =
                        AddInstructionAt(
                            rule,
                            "object.spawnBlueprint",
                            false,
                            position,
                            insertAfterInstructionId,
                            connectFromWire &&
                            insertAfterInstructionId ==
                                Guid.Empty);

                    if (connectFromWire)
                    {
                        ConnectNewActionAfterSource(
                            rule,
                            insertAfterInstructionId,
                            created,
                            _pendingWireCreateKind);
                    }
                    else
                    {
                        AppendActionToExecutionFlow(
                            rule,
                            created);
                    }
                }

                ImGui.Separator();
            }

            foreach (VisualActionDefinition definition
                     in group)
            {
                if (ImGui.MenuItem(
                        definition.DisplayName))
                {
                    RecordHistory(
                        "Add Action");

                    VisualInstruction created =
                        AddInstructionAt(
                            rule,
                            definition.Id,
                            false,
                            position,
                            insertAfterInstructionId,
                            connectFromWire &&
                            insertAfterInstructionId ==
                                Guid.Empty);

                    if (connectFromWire)
                    {
                        ConnectNewActionAfterSource(
                            rule,
                            insertAfterInstructionId,
                            created,
                            _pendingWireCreateKind);
                    }
                    else
                    {
                        AppendActionToExecutionFlow(
                            rule,
                            created);
                    }
                }
            }

            ImGui.EndMenu();
        }
    }

    private static bool IsLegacyRaycastCreationDefinition(
        string id)
    {
        // Keep these IDs loadable for existing Event Sheets, but do not offer
        // them for new nodes now that Line Trace covers object, direction,
        // cursor and raw world-position workflows in one universal primitive.
        return id.Equals(
                   "physics.castRayToCursor",
                   StringComparison.OrdinalIgnoreCase) ||
               id.Equals(
                   "physics.castRayInDirection",
                   StringComparison.OrdinalIgnoreCase) ||
               id.Equals(
                   "physics.castRay",
                   StringComparison.OrdinalIgnoreCase) ||
               id.Equals(
                   "physics.rayHitsAnything",
                   StringComparison.OrdinalIgnoreCase);
    }

    private VisualInstruction AddInstructionAt(
        EventRuleDefinition rule,
        string definitionId,
        bool condition,
        Vector2 position,
        Guid insertAfterInstructionId,
        bool insertAtBeginningWhenNoSource = false)
    {
        VisualInstruction instruction =
            CreateInstruction(
                definitionId);

        instruction.EditorX =
            position.X;

        instruction.EditorY =
            position.Y;

        instruction.EditorLayoutInitialized =
            true;

        List<VisualInstruction> list =
            condition
                ? rule.Conditions
                : rule.Actions;

        int insertIndex =
            insertAtBeginningWhenNoSource &&
            insertAfterInstructionId ==
                Guid.Empty
                ? 0
                : list.Count;

        if (insertAfterInstructionId !=
            Guid.Empty)
        {
            int sourceIndex =
                list.FindIndex(
                    item =>
                        item.InstanceId ==
                        insertAfterInstructionId);

            if (sourceIndex >=
                0)
            {
                insertIndex =
                    sourceIndex +
                    1;
            }
        }

        list.Insert(
            insertIndex,
            instruction);

        _selectedGraphNodes.Clear();

        _selectedGraphNodes.Add(
            instruction.InstanceId);

        _dirty =
            true;

        return instruction;
    }

    private void RequestAddEvent(Vector2 position)
    {
        _eventNamePopup.BeginCreate(position);
    }

    private void RequestRenameEvent(EventRuleDefinition rule)
    {
        _eventNamePopup.BeginRename(rule.Id, GetRuleDisplayName(rule));
    }

    private void DrawEventNamePopup()
    {
        const string popupName = "Event Name##EventNamePopup";
        if (_eventNamePopup.ConsumeOpenRequest())
        {
            ImGui.OpenPopup(popupName);
        }

        bool open = true;
        if (!ImGui.BeginPopupModal(popupName, ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            _eventNamePopup.RecoverWhenNotVisible();
            return;
        }
        _eventNamePopup.MarkVisible();

        if (_eventNamePopup.IsRename &&
            (_eventNamePopup.TargetEventId is not Guid targetId || FindRule(targetId) == null))
        {
            _eventNamePopup.Reset();
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            return;
        }

        ImGui.TextUnformatted(_eventNamePopup.IsRename ? "Rename Event" : "Create Event");
        ImGui.Separator();
        ImGui.TextUnformatted("Name:");
        ImGui.SetNextItemWidth(320f);
        if (_eventNamePopup.ConsumeFocusRequest()) ImGui.SetKeyboardFocusHere();
        string buffer = _eventNamePopup.Buffer;
        if (ImGui.InputText("##EventDisplayName", ref buffer, 128, ImGuiInputTextFlags.AutoSelectAll))
            _eventNamePopup.Buffer = buffer;

        bool confirm = ImGui.Button(_eventNamePopup.IsRename ? "Rename" : "Create") ||
            ImGui.IsKeyPressed(ImGuiKey.Enter);
        ImGui.SameLine();
        bool cancel = ImGui.Button("Cancel") || ImGui.IsKeyPressed(ImGuiKey.Escape) || !open;

        if (confirm && !string.IsNullOrWhiteSpace(_eventNamePopup.Buffer))
        {
            string displayName = _eventNamePopup.Buffer.Trim();
            if (!_eventNamePopup.IsRename)
            {
                RecordHistory("Add Event");
                AddEventAt(_eventNamePopup.CreatePosition, displayName);
                _requestFrameGraph = true;
            }
            else if (_eventNamePopup.TargetEventId is Guid eventId && FindRule(eventId) is { } rule)
            {
                RecordHistory("Rename Event");
                rule.DisplayName = displayName;
                rule.EditorTitle = displayName;
                _dirty = true;
            }
            _eventNamePopup.Reset();
            ImGui.CloseCurrentPopup();
        }
        else if (cancel)
        {
            _eventNamePopup.Reset();
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private void AddEventAt(
        Vector2 position,
        string displayName)
    {
        if (_module ==
            null)
        {
            return;
        }

        var rule =
            new EventRuleDefinition
            {
                EditorLayoutInitialized =
                    true,

                EditorX =
                    position.X,

                EditorY =
                    position.Y,

                DisplayName = displayName,
                EditorTitle = displayName
            };

        _module.Rules.Add(
            rule);
        AddNodeToActiveGroup(rule.Id);

        _selectedGraphNodes.Clear();

        _selectedGraphNodes.Add(
            rule.Id);

        _dirty =
            true;
    }

    private EventRuleDefinition? FindRule(
        Guid id)
    {
        return _module?
            .Rules
            .FirstOrDefault(
                rule =>
                    rule.Id ==
                    id);
    }

    private static string GetRuleDisplayName(
        EventRuleDefinition rule)
    {
        return !string.IsNullOrWhiteSpace(rule.DisplayName)
            ? rule.DisplayName
            : !string.IsNullOrWhiteSpace(rule.EditorTitle)
                ? rule.EditorTitle
                : "New Event";
    }

    private IEnumerable<(EventRuleDefinition Rule, string Label)> GetRuleMenuEntries()
    {
        if (_module == null) yield break;
        Dictionary<string, int> totals = _module.Rules
            .GroupBy(GetRuleDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (EventRuleDefinition rule in _module.Rules)
        {
            string name = GetRuleDisplayName(rule);
            seen[name] = seen.GetValueOrDefault(name) + 1;
            yield return (rule, totals[name] > 1 ? $"{name} ({seen[name]})" : name);
        }
    }

    private void UpdateGraphPointerInteraction()
    {
        // The reference picker is drawn later in the Details child window.
        // Geometry-only graph hit tests must not consume its click first:
        // clearing the selected node would stop drawing Details and close
        // the picker before ImGui can handle the selection.
        if (ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId))
        {
            _hoveredGraphNodeId = Guid.Empty;
            _anyGraphNodeHovered = false;
            _draggingGraphNodeId = Guid.Empty;
            _graphNodeDragStarted = false;
            _marqueeSelecting = false;
            return;
        }
        if (_groupPointerCaptured)
        {
            _anyGraphNodeHovered = true;
            _hoveredGraphNodeId = Guid.Empty;
            return;
        }
        Vector2 mouseGraphPosition =
            _graphCanvas.MouseGraphPosition;

        Vector2 mouseScreenPosition =
            ImGui.GetIO().MousePos;

        _hoveredGraphNodeId =
            HitTestGraphNode(
                mouseGraphPosition);

        _anyGraphNodeHovered =
            _hoveredGraphNodeId !=
            Guid.Empty;

        bool pointerOnGraphPin =
            IsPointerOnAnyGraphPin();

        /*
         * Reset the drag state as soon as the left button is released.
         */
        if (!ImGui.IsMouseDown(
                ImGuiMouseButton.Left))
        {
            _draggingGraphNodeId =
                Guid.Empty;

            _graphNodeDragStarted =
                false;

            _dragHistoryNodeId =
                Guid.Empty;
        }

        /*
         * Selection and drag arming are geometry based.
         *
         * A node is armed for dragging on the FIRST mouse-down when that
         * mouse-down occurs in its drag region. We then use our own tiny
         * 2-pixel threshold instead of Dear ImGui's normal drag threshold.
         * This makes dragging feel immediate instead of requiring several
         * click/drag attempts.
         */
        if (_wireDragKind ==
                WireDragKind.None &&
            !_marqueeSelecting &&
            _graphCanvas.IsMouseInsideCanvas &&
            ImGui.IsMouseClicked(
                ImGuiMouseButton.Left))
        {
            if (pointerOnGraphPin)
            {
                /*
                 * Pin gestures are handled later when the nodes draw.
                 * Do not arm node dragging or marquee selection here.
                 */
                _draggingGraphNodeId =
                    Guid.Empty;

                _graphNodeDragStarted =
                    false;
            }
            else if (_hoveredGraphNodeId !=
                     Guid.Empty)
            {
                SelectGraphNodeFromClick(
                    _hoveredGraphNodeId);

                if (PointInsideNodeDragArea(
                        mouseGraphPosition,
                        _hoveredGraphNodeId))
                {
                    _draggingGraphNodeId =
                        _hoveredGraphNodeId;

                    _graphNodeDragStarted =
                        false;

                    _graphNodeDragStartMouse =
                        mouseScreenPosition;

                    _graphNodeDragPreviousMouse =
                        mouseScreenPosition;
                }
                else
                {
                    _draggingGraphNodeId =
                        Guid.Empty;
                }
            }
            else
            {
                _draggingGraphNodeId =
                    Guid.Empty;

                _graphNodeDragStarted =
                    false;

                _marqueeSelecting =
                    true;

                _marqueeAdditive =
                    ImGui.IsKeyDown(
                        ImGuiKey.ModCtrl) ||
                    ImGui.IsKeyDown(
                        ImGuiKey.ModShift);

                _marqueeStart =
                    mouseGraphPosition;

                _marqueeCurrent =
                    _marqueeStart;
            }
        }

        if (_draggingGraphNodeId !=
                Guid.Empty &&
            ImGui.IsMouseDown(
                ImGuiMouseButton.Left))
        {
            /*
             * Use a deliberately small drag threshold.
             *
             * Dear ImGui's normal IsMouseDragging threshold is useful for
             * buttons, but it makes graph nodes feel sticky. Two pixels is
             * enough to distinguish a click from an intentional node move.
             */
            if (!_graphNodeDragStarted)
            {
                Vector2 dragFromStart =
                    mouseScreenPosition -
                    _graphNodeDragStartMouse;

                if (dragFromStart.LengthSquared() >=
                    4.0f)
                {
                    _graphNodeDragStarted =
                        true;

                    BeginNodeDragHistory(
                        _draggingGraphNodeId,
                        _selectedGraphNodes.Count >
                            1
                            ? "Move Selected Nodes"
                            : "Move Graph Node");

                    Vector2 initialDelta =
                        _graphCanvas.ScreenDeltaToGraph(
                            mouseScreenPosition -
                            _graphNodeDragPreviousMouse);

                    MoveSelectedGraphNodes(
                        _draggingGraphNodeId,
                        initialDelta);

                    _graphNodeDragPreviousMouse =
                        mouseScreenPosition;

                    _dirty =
                        true;
                }
            }
            else
            {
                Vector2 screenDelta =
                    mouseScreenPosition -
                    _graphNodeDragPreviousMouse;

                if (screenDelta.LengthSquared() >
                    0.0f)
                {
                    Vector2 delta =
                        _graphCanvas.ScreenDeltaToGraph(
                            screenDelta);

                    MoveSelectedGraphNodes(
                        _draggingGraphNodeId,
                        delta);

                    _graphNodeDragPreviousMouse =
                        mouseScreenPosition;

                    _dirty =
                        true;
                }
            }
        }

        /*
         * Open the graph context menu from raw canvas hit testing.
         * Do this before drawing node child windows so no later ImGui
         * item can swallow the right-click.
         */
        if (_graphCanvas.IsMouseInsideCanvas &&
            _hoveredGraphNodeId ==
                Guid.Empty &&
            !pointerOnGraphPin &&
            !_marqueeSelecting &&
            _wireDragKind ==
                WireDragKind.None &&
            ImGui.IsMouseClicked(
                ImGuiMouseButton.Right))
        {
            _graphContextPosition =
                mouseGraphPosition;

            ImGui.OpenPopup(
                "ByteGraph Context");
        }
    }

    private bool IsPointerOnAnyGraphPin()
    {
        if (_module ==
            null)
        {
            return false;
        }

        foreach (EventRuleDefinition rule
                 in _module.Rules)
        {
            if (_graphCanvas.IsPointHovered(
                    GetEventConditionInput(
                        rule),
                    16.0f) ||
                _graphCanvas.IsPointHovered(
                    GetEventExecutionOutput(
                        rule),
                    16.0f))
            {
                return true;
            }

            if (rule.EditorCollapsed && ActiveGraphGroup == null)
            {
                continue;
            }

            foreach (VisualInstruction condition
                     in rule.Conditions)
            {
                if (_graphCanvas.IsPointHovered(
                        GetInstructionOutput(
                            condition),
                        16.0f) ||
                    (
                        IsLogicGate(
                            condition) &&
                        _graphCanvas.IsPointHovered(
                            GetInstructionInput(
                                condition),
                            16.0f)
                    ))
                {
                    return true;
                }
            }

            foreach (VisualInstruction action
                     in rule.Actions)
            {
                if (_graphCanvas.IsPointHovered(
                        GetInstructionInput(
                            action),
                        16.0f))
                {
                    return true;
                }

                if (IsBranchAction(
                        action))
                {
                    if (_graphCanvas.IsPointHovered(
                            GetBranchTrueOutput(
                                action),
                            16.0f) ||
                        _graphCanvas.IsPointHovered(
                            GetBranchFalseOutput(
                                action),
                            16.0f))
                    {
                        return true;
                    }

                    continue;
                }

                if (_graphCanvas.IsPointHovered(
                        GetInstructionOutput(
                            action),
                        16.0f))
                {
                    return true;
                }
            }
        }

        foreach (VisualInstruction instruction in _module.EditorLooseConditions
                     .Concat(_module.EditorLooseActions))
        {
            bool condition = _module.EditorLooseConditions.Contains(instruction);
            if ((!IsBranchAction(instruction) &&
                 _graphCanvas.IsPointHovered(GetInstructionOutput(instruction), 16.0f)) ||
                ((!condition || IsLogicGate(instruction)) &&
                 _graphCanvas.IsPointHovered(GetInstructionInput(instruction), 16.0f)) ||
                (IsBranchAction(instruction) &&
                 (_graphCanvas.IsPointHovered(GetBranchTrueOutput(instruction), 16.0f) ||
                  _graphCanvas.IsPointHovered(GetBranchFalseOutput(instruction), 16.0f))))
                return true;
        }

        return false;
    }

    private bool TryConnectConditionWireAtMouse()
    {
        if (_wireDragKind == WireDragKind.ConditionInput)
            return TryConnectGateInputWireAtMouse();

        EventRuleDefinition? rule =
            FindRule(
                _wireDragRuleId);

        if (rule ==
            null)
        {
            return TryConnectLooseConditionOutputAtMouse();
        }

        /*
         * Dragging from a Condition/gate output.
         */
        if (_wireDragSourceInstructionId !=
            Guid.Empty)
        {
            VisualInstruction? source =
                FindCondition(
                    rule,
                    _wireDragSourceInstructionId);

            if (source ==
                null)
            {
                return false;
            }

            /*
             * Output -> Event input makes the source a root expression.
             */
            if (_graphCanvas.IsPointHovered(
                    GetEventConditionInput(
                        rule),
                    18.0f))
            {
                RecordHistory(
                    "Connect Condition Wire");

                ConnectCondition(
                    rule,
                    source.InstanceId);

                _dirty =
                    true;

                return true;
            }

            /*
             * Output -> AND/OR input adds the source to that gate.
             */
            foreach (VisualInstruction gate
                     in rule.Conditions.Where(
                         IsLogicGate))
            {
                if (!_graphCanvas.IsPointHovered(
                        GetInstructionInput(
                            gate),
                        18.0f))
                {
                    continue;
                }

                if (gate.InstanceId ==
                        source.InstanceId ||
                    WouldCreateConditionCycle(
                        rule,
                        source.InstanceId,
                        gate.InstanceId))
                {
                    return true;
                }

                RecordHistory(
                    "Connect Logic Wire");

                ConnectConditionToGate(
                    rule,
                    gate,
                    source.InstanceId);

                _dirty =
                    true;

                return true;
            }

            if (_module != null)
                foreach (VisualInstruction gate in _module.EditorLooseConditions.Where(IsLogicGate))
                {
                    if (!_graphCanvas.IsPointHovered(GetInstructionInput(gate), 18.0f))
                        continue;
                    RecordHistory("Connect Logic Wire");
                    AttachLooseConditionToRule(rule, gate);
                    ConnectConditionToGate(rule, gate, source.InstanceId);
                    ConnectCondition(rule, gate.InstanceId);
                    _dirty = true;
                    return true;
                }

            return false;
        }

        /*
         * Dragging backwards from the Event's blue input onto a Condition
         * output still creates a root connection.
         */
        foreach (VisualInstruction condition
                 in rule.Conditions)
        {
            if (!_graphCanvas.IsPointHovered(
                    GetInstructionOutput(
                        condition),
                    18.0f))
            {
                continue;
            }

            RecordHistory(
                "Connect Condition Wire");

            ConnectCondition(
                rule,
                condition.InstanceId);

            _dirty =
                true;

            return true;
        }

        if (_module != null)
            foreach (VisualInstruction condition in _module.EditorLooseConditions.ToArray())
            {
                if (!_graphCanvas.IsPointHovered(GetInstructionOutput(condition), 18.0f))
                    continue;
                RecordHistory("Connect Draft Condition");
                AttachLooseConditionToRule(rule, condition);
                ConnectCondition(rule, condition.InstanceId);
                _dirty = true;
                return true;
            }

        return false;
    }

    private void AttachLooseConditionToRule(
        EventRuleDefinition rule, VisualInstruction condition)
        => AttachLooseConditionToRule(rule, condition, new HashSet<Guid>());

    private void AttachLooseConditionToRule(
        EventRuleDefinition rule, VisualInstruction condition, HashSet<Guid> visited)
    {
        if (_module == null ||
            !visited.Add(condition.InstanceId) ||
            !_module.EditorLooseConditions.Remove(condition))
            return;

        rule.Conditions.Add(condition);
        foreach (Guid inputId in condition.ConditionInputIds.ToArray())
        {
            VisualInstruction? looseInput = _module.EditorLooseConditions
                .FirstOrDefault(item => item.InstanceId == inputId);
            if (looseInput != null)
                AttachLooseConditionToRule(rule, looseInput, visited);
        }
    }

    private bool TryConnectLooseConditionOutputAtMouse()
    {
        if (_module == null)
            return false;
        VisualInstruction? source = _module.EditorLooseConditions
            .FirstOrDefault(item => item.InstanceId == _wireDragSourceInstructionId);
        if (source == null)
            return false;

        foreach (EventRuleDefinition rule in _module.Rules)
        {
            if (_graphCanvas.IsPointHovered(GetEventConditionInput(rule), 18.0f))
            {
                RecordHistory("Connect Draft Condition");
                AttachLooseConditionToRule(rule, source);
                ConnectCondition(rule, source.InstanceId);
                _dirty = true;
                return true;
            }
            foreach (VisualInstruction gate in rule.Conditions.Where(IsLogicGate))
            {
                if (!_graphCanvas.IsPointHovered(GetInstructionInput(gate), 18.0f))
                    continue;
                RecordHistory("Connect Draft Logic Input");
                AttachLooseConditionToRule(rule, source);
                if (!WouldCreateConditionCycle(rule, source.InstanceId, gate.InstanceId))
                    ConnectConditionToGate(rule, gate, source.InstanceId);
                _dirty = true;
                return true;
            }
        }

        foreach (VisualInstruction gate in _module.EditorLooseConditions.Where(IsLogicGate))
        {
            if (gate.InstanceId == source.InstanceId ||
                !_graphCanvas.IsPointHovered(GetInstructionInput(gate), 18.0f))
                continue;
            if (WouldCreateDraftConditionCycle(source.InstanceId, gate.InstanceId))
                return true;
            RecordHistory("Connect Draft Logic Input");
            if (!gate.ConditionInputIds.Contains(source.InstanceId))
                gate.ConditionInputIds.Add(source.InstanceId);
            _dirty = true;
            return true;
        }
        return false;
    }

    private bool TryConnectGateInputWireAtMouse()
    {
        if (_module == null)
            return false;
        EventRuleDefinition? rule = FindRule(_wireDragRuleId);
        VisualInstruction? gate = rule == null
            ? _module.EditorLooseConditions.FirstOrDefault(item =>
                item.InstanceId == _wireDragSourceInstructionId)
            : FindCondition(rule, _wireDragSourceInstructionId);
        if (gate == null || !IsLogicGate(gate))
            return false;

        IEnumerable<VisualInstruction> sources = rule == null
            ? _module.EditorLooseConditions
            : rule.Conditions.Concat(_module.EditorLooseConditions);
        foreach (VisualInstruction source in sources.ToArray())
        {
            if (source.InstanceId == gate.InstanceId ||
                !_graphCanvas.IsPointHovered(GetInstructionOutput(source), 18.0f))
                continue;

            if (rule == null)
            {
                if (!_module.EditorLooseConditions.Contains(source))
                    continue;
                if (WouldCreateDraftConditionCycle(source.InstanceId, gate.InstanceId))
                    return true;
                RecordHistory("Connect Draft Logic Input");
                if (!gate.ConditionInputIds.Contains(source.InstanceId))
                    gate.ConditionInputIds.Add(source.InstanceId);
            }
            else
            {
                if (WouldCreateConditionCycle(rule, source.InstanceId, gate.InstanceId))
                    return true;
                RecordHistory("Connect Logic Input");
                if (_module.EditorLooseConditions.Contains(source))
                    AttachLooseConditionToRule(rule, source);
                ConnectConditionToGate(rule, gate, source.InstanceId);
            }
            _dirty = true;
            return true;
        }
        return false;
    }

    private bool WouldCreateDraftConditionCycle(Guid sourceId, Guid gateId)
    {
        if (_module == null)
            return false;
        var pending = new Stack<Guid>();
        var visited = new HashSet<Guid>();
        pending.Push(sourceId);
        while (pending.Count > 0)
        {
            Guid currentId = pending.Pop();
            if (currentId == gateId)
                return true;
            if (!visited.Add(currentId))
                continue;
            VisualInstruction? current = _module.EditorLooseConditions
                .FirstOrDefault(item => item.InstanceId == currentId);
            if (current != null)
                foreach (Guid inputId in current.ConditionInputIds)
                    pending.Push(inputId);
        }
        return false;
    }

    private void DrawMouseConditionCreateItems(
        EventRuleDefinition rule,
        Vector2 position,
        Guid insertAfterInstructionId)
    {
        if (ImGui.MenuItem(
                "Button Is Held"))
        {
            RecordHistory(
                "Add Mouse Condition");

            VisualInstruction created =
                AddInstructionAt(
                    rule,
                    "input.mouseHeld",
                    true,
                    position,
                    insertAfterInstructionId);

            ConnectCondition(
                rule,
                created.InstanceId);
        }

        if (ImGui.MenuItem(
                "Button Pressed"))
        {
            RecordHistory(
                "Add Mouse Condition");

            VisualInstruction created =
                AddInstructionAt(
                    rule,
                    "input.mousePressed",
                    true,
                    position,
                    insertAfterInstructionId);

            ConnectCondition(
                rule,
                created.InstanceId);
        }

        if (ImGui.MenuItem(
                "Button Released"))
        {
            RecordHistory(
                "Add Mouse Condition");

            VisualInstruction created =
                AddInstructionAt(
                    rule,
                    "input.mouseReleased",
                    true,
                    position,
                    insertAfterInstructionId);

            ConnectCondition(
                rule,
                created.InstanceId);
        }
    }

    private bool DrawMouseConditionPickerItems(
        EventRuleDefinition rule,
        List<VisualInstruction> conditions)
    {
        string? id =
            null;

        if (ImGui.MenuItem(
                "Button Is Held"))
        {
            id =
                "input.mouseHeld";
        }
        else if (ImGui.MenuItem(
                     "Button Pressed"))
        {
            id =
                "input.mousePressed";
        }
        else if (ImGui.MenuItem(
                     "Button Released"))
        {
            id =
                "input.mouseReleased";
        }

        if (id ==
            null)
        {
            return false;
        }

        RecordHistory(
            "Add Mouse Condition");

        VisualInstruction created =
            CreateInstruction(
                id);

        conditions.Add(
            created);

        ConnectCondition(
            rule,
            created.InstanceId);

        _dirty =
            true;

        return true;
    }

    private void HandleGraphShortcuts()
    {
        if (_module ==
                null ||
            _selectedGraphNodes.Count ==
                0 ||
            ImGui.GetIO().WantTextInput ||
            !ImGui.IsWindowFocused(
                ImGuiFocusedFlags.RootAndChildWindows))
        {
            return;
        }

        bool hasModifier =
            ImGui.IsKeyDown(
                ImGuiKey.ModCtrl) ||
            ImGui.IsKeyDown(
                ImGuiKey.ModShift) ||
            ImGui.IsKeyDown(
                ImGuiKey.ModAlt);

        if (!hasModifier &&
            ImGui.IsKeyPressed(
                ImGuiKey.C))
        {
            CreateGroupFromSelection();

            return;
        }

        if (!hasModifier &&
            ImGui.IsKeyPressed(
                ImGuiKey.Delete))
        {
            DeleteSelectedGraphNodes();
        }
    }

    private Guid HitTestGraphNode(
        Vector2 graphPoint)
    {
        if (_module ==
            null)
        {
            return Guid.Empty;
        }

        Guid result =
            Guid.Empty;

        /*
         * Iterate in the same broad order that nodes are drawn.
         * If nodes overlap, the later node wins, matching what the
         * user visually perceives as being on top.
         */
        foreach (EventRuleDefinition rule
                 in _module.Rules)
        {
            if (PointInsideNode(
                    graphPoint,
                    rule.Id))
            {
                result =
                    rule.Id;
            }

            if (rule.EditorCollapsed && ActiveGraphGroup == null)
            {
                continue;
            }

            foreach (VisualInstruction instruction
                     in rule.Conditions)
            {
                if (PointInsideNode(
                        graphPoint,
                        instruction.InstanceId))
                {
                    result =
                        instruction.InstanceId;
                }
            }

            foreach (VisualInstruction instruction
                     in rule.Actions)
            {
                if (PointInsideNode(
                        graphPoint,
                        instruction.InstanceId))
                {
                    result =
                        instruction.InstanceId;
                }
            }
        }

        foreach (VisualInstruction instruction in _module.EditorLooseConditions
                     .Concat(_module.EditorLooseActions))
            if (PointInsideNode(graphPoint, instruction.InstanceId))
                result = instruction.InstanceId;

        return result;
    }

    private bool PointInsideNode(
        Vector2 graphPoint,
        Guid nodeId)
    {
        if (IsNodeHiddenByGroup(nodeId))
            return false;
        if (!TryGetNodeBounds(
                nodeId,
                out Vector2 minimum,
                out Vector2 maximum))
        {
            return false;
        }

        return graphPoint.X >=
                   minimum.X &&
               graphPoint.Y >=
                   minimum.Y &&
               graphPoint.X <=
                   maximum.X &&
               graphPoint.Y <=
                   maximum.Y;
    }

    private bool PointInsideNodeDragArea(
        Vector2 graphPoint,
        Guid nodeId)
    {
        if (!TryGetNodeBounds(
                nodeId,
                out Vector2 minimum,
                out Vector2 maximum))
        {
            return false;
        }

        bool insideHorizontalBounds =
            graphPoint.X >=
                minimum.X &&
            graphPoint.X <=
                maximum.X;

        if (!insideHorizontalBounds)
        {
            return false;
        }

        float nodeHeight =
            Math.Max(
                maximum.Y -
                minimum.Y,
                1.0f);

        /*
         * Keep drag handles usable even when the graph or node scale is
         * small. The minimum is expressed in SCREEN pixels and converted
         * back to graph units, so zooming out does not make the draggable
         * area microscopic.
         */
        float minimumHeaderHeight =
            28.0f /
            Math.Max(
                _graphCanvas.Zoom,
                0.25f);

        bool isEventNode =
            _module?.Rules.Any(
                rule =>
                    rule.Id ==
                    nodeId) ==
            true;

        if (isEventNode)
        {
            /*
             * Event cards contain several interactive controls in the
             * middle, so keep those controls safe and make the clearly
             * non-parameter areas generous:
             *
             *   - a larger top EVENT strip
             *   - a larger bottom "Drag / Select Event" strip
             *
             * At low zoom the screen-space minimum keeps these regions
             * easy to grab.
             */
            float topDragHeight =
                Math.Min(
                    Math.Max(
                        50.0f *
                        GetNodeScale(),
                        minimumHeaderHeight),
                    nodeHeight *
                    0.34f);

            float bottomDragHeight =
                Math.Min(
                    Math.Max(
                        68.0f *
                        GetNodeScale(),
                        minimumHeaderHeight),
                    nodeHeight *
                    0.42f);

            bool insideTopDragArea =
                graphPoint.Y >=
                    minimum.Y &&
                graphPoint.Y <=
                    minimum.Y +
                    topDragHeight;

            bool insideBottomDragArea =
                graphPoint.Y >=
                    maximum.Y -
                    bottomDragHeight &&
                graphPoint.Y <=
                    maximum.Y;

            return insideTopDragArea ||
                   insideBottomDragArea;
        }

        /*
         * Condition / Action nodes have their title and category at the
         * top, with parameter editors below. Make roughly the upper third
         * draggable while keeping the parameter controls out of the drag
         * region.
         */
        float headerHeight =
            Math.Min(
                Math.Max(
                    82.0f *
                    GetNodeScale(),
                    minimumHeaderHeight),
                nodeHeight *
                0.42f);

        return graphPoint.Y >=
                   minimum.Y &&
               graphPoint.Y <=
                   minimum.Y +
                   headerHeight;
    }

    private void SelectGraphNodeFromClick(
        Guid nodeId)
    {
        bool multiSelect =
            ImGui.IsKeyDown(
                ImGuiKey.ModCtrl) ||
            ImGui.IsKeyDown(
                ImGuiKey.ModShift);

        if (!multiSelect)
        {
            /*
             * If the user clicks a node that is already part of a
             * multi-selection, keep the entire selection intact so the
             * selection can be dragged as one group.
             */
            if (_selectedGraphNodes.Contains(
                    nodeId))
            {
                return;
            }

            _selectedGraphNodes.Clear();

            _selectedGraphNodes.Add(
                nodeId);

            return;
        }

        if (!_selectedGraphNodes.Add(
                nodeId))
        {
            _selectedGraphNodes.Remove(
                nodeId);
        }
    }

    private void HandleGraphMarquee()
    {
        if (_module ==
                null ||
            !_marqueeSelecting)
        {
            return;
        }

        _marqueeCurrent =
            _graphCanvas.MouseGraphPosition;

        _graphCanvas.DrawSelectionRectangle(
            _marqueeStart,
            _marqueeCurrent);

        if (!ImGui.IsMouseReleased(
                ImGuiMouseButton.Left))
        {
            return;
        }

        Vector2 screenDelta =
            _graphCanvas.ToScreen(
                _marqueeCurrent) -
            _graphCanvas.ToScreen(
                _marqueeStart);

        bool dragged =
            screenDelta.LengthSquared() >=
            16.0f;

        if (!_marqueeAdditive)
        {
            _selectedGraphNodes.Clear();
        }

        if (dragged)
        {
            SelectNodesInRectangle(
                _marqueeStart,
                _marqueeCurrent);
        }

        _marqueeSelecting =
            false;
    }

    private void SelectNodesInRectangle(
        Vector2 a,
        Vector2 b)
    {
        if (_module ==
            null)
        {
            return;
        }

        Vector2 selectionMinimum =
            new(
                Math.Min(
                    a.X,
                    b.X),
                Math.Min(
                    a.Y,
                    b.Y));

        Vector2 selectionMaximum =
            new(
                Math.Max(
                    a.X,
                    b.X),
                Math.Max(
                    a.Y,
                    b.Y));

        foreach (EventRuleDefinition rule
                 in _module.Rules)
        {
            TrySelectNodeFromRectangle(
                rule.Id,
                selectionMinimum,
                selectionMaximum);

            if (rule.EditorCollapsed && ActiveGraphGroup == null)
            {
                continue;
            }

            foreach (VisualInstruction instruction
                     in rule.Conditions.Concat(
                         rule.Actions))
            {
                TrySelectNodeFromRectangle(
                    instruction.InstanceId,
                    selectionMinimum,
                    selectionMaximum);
            }
        }

        foreach (VisualInstruction instruction in _module.EditorLooseConditions
                     .Concat(_module.EditorLooseActions))
            TrySelectNodeFromRectangle(
                instruction.InstanceId, selectionMinimum, selectionMaximum);
    }

    private void TrySelectNodeFromRectangle(
        Guid nodeId,
        Vector2 selectionMinimum,
        Vector2 selectionMaximum)
    {
        if (IsNodeHiddenByGroup(nodeId))
            return;
        if (!TryGetNodeBounds(
                nodeId,
                out Vector2 nodeMinimum,
                out Vector2 nodeMaximum))
        {
            return;
        }

        bool intersects =
            nodeMaximum.X >=
                selectionMinimum.X &&
            nodeMinimum.X <=
                selectionMaximum.X &&
            nodeMaximum.Y >=
                selectionMinimum.Y &&
            nodeMinimum.Y <=
                selectionMaximum.Y;

        if (intersects)
        {
            _selectedGraphNodes.Add(
                nodeId);
        }
    }

    private void DeleteSelectedGraphNodes()
    {
        if (_module ==
                null ||
            _selectedGraphNodes.Count ==
                0)
        {
            return;
        }

        /*
         * Take a snapshot before mutating anything. Deleting several
         * selected nodes is one undoable editor operation.
         */
        RecordHistory(
            _selectedGraphNodes.Count >
                1
                ? "Delete Selected Nodes"
                : "Delete Graph Node");

        HashSet<Guid> selected =
            _selectedGraphNodes
                .ToHashSet();

        /*
         * Delete whole Events first. Their child Conditions/Actions belong
         * to the Event, so they must not also be processed independently.
         */
        for (int ruleIndex =
                 _module.Rules.Count -
                 1;
             ruleIndex >=
                 0;
             ruleIndex--)
        {
            EventRuleDefinition rule =
                _module.Rules[ruleIndex];

            if (!selected.Contains(
                    rule.Id))
            {
                continue;
            }

            RemoveNodeFromGroups(
                rule.Id);

            foreach (VisualInstruction instruction
                     in rule.Conditions.Concat(
                         rule.Actions))
            {
                RemoveNodeFromGroups(
                    instruction.InstanceId);

                selected.Remove(
                    instruction.InstanceId);
            }

            _module.Rules.RemoveAt(
                ruleIndex);

            selected.Remove(
                rule.Id);
        }

        /*
         * Delete selected Conditions and Actions from Events that remain.
         * Work backwards so list indices remain valid while removing.
         */
        foreach (EventRuleDefinition rule
                 in _module.Rules)
        {
            for (int conditionIndex =
                     rule.Conditions.Count -
                     1;
                 conditionIndex >=
                     0;
                 conditionIndex--)
            {
                VisualInstruction condition =
                    rule.Conditions[
                        conditionIndex];

                if (!selected.Contains(
                        condition.InstanceId))
                {
                    continue;
                }

                DisconnectConditionEverywhere(
                    rule,
                    condition.InstanceId);

                RemoveNodeFromGroups(
                    condition.InstanceId);

                rule.Conditions.RemoveAt(
                    conditionIndex);
            }

            for (int actionIndex =
                     rule.Actions.Count -
                     1;
                 actionIndex >=
                     0;
                 actionIndex--)
            {
                VisualInstruction action =
                    rule.Actions[
                        actionIndex];

                if (!selected.Contains(
                        action.InstanceId))
                {
                    continue;
                }

                /*
                 * Preserve the surrounding execution chain when possible.
                 *
                 * Example:
                 *     A -> B -> C
                 *
                 * Delete B:
                 *     A ------> C
                 */
                RemoveActionFromExecutionFlow(
                    rule,
                    action);

                RemoveNodeFromGroups(
                    action.InstanceId);

                rule.Actions.RemoveAt(
                    actionIndex);
            }
        }

        foreach (VisualInstruction instruction in _module.EditorLooseConditions
                     .Concat(_module.EditorLooseActions)
                     .Where(item => selected.Contains(item.InstanceId)))
        {
            RemoveNodeFromGroups(instruction.InstanceId);
            DisconnectLooseReferences(instruction.InstanceId);
        }
        _module.EditorLooseConditions.RemoveAll(item => selected.Contains(item.InstanceId));
        _module.EditorLooseActions.RemoveAll(item => selected.Contains(item.InstanceId));

        _selectedGraphNodes.Clear();

        _draggingGraphNodeId =
            Guid.Empty;

        _graphNodeDragStarted =
            false;

        _marqueeSelecting =
            false;

        CancelWireDrag();

        _dirty =
            true;
    }

    private void CreateGroupFromSelection()
    {
        if (_module ==
                null ||
            _selectedGraphNodes.Count ==
                0)
        {
            return;
        }

        List<Guid> members =
            _selectedGraphNodes
                .Where(
                    NodeExists)
                .ToList();

        if (members.Count ==
            0)
        {
            return;
        }

        RecordHistory(
            "Create Comment Box");

        _module.EditorGroups.Add(
            new EventGraphGroupDefinition
            {
                Title =
                    "Comment",
                ParentGroupId = ActiveGraphGroup?.Id,

                MemberIds =
                    members
            });

        _dirty =
            true;
    }

    private void CollapseSelectedGraphNodes()
    {
        if (_module == null)
            return;

        List<Guid> members = _selectedGraphNodes.Where(NodeExists)
            .Where(id => !IsNodeHiddenByGroup(id)).Distinct().ToList();
        if (members.Count < 2)
            return;

        RecordHistory("Collapse Graph Selection");
        _module.EditorGroups.Add(new EventGraphGroupDefinition
        {
            Title = "Group",
            Collapsed = true,
            ParentGroupId = ActiveGraphGroup?.Id,
            MemberIds = members
        });
        _selectedGraphNodes.Clear();
        _dirty = true;
    }

    private EventGraphGroupDefinition? ActiveGraphGroup =>
        _module?.EditorGroups.FirstOrDefault(group => group.Id == _activeGraphGroupId);

    private bool IsNodeHiddenByGroup(Guid id)
    {
        EventGraphGroupDefinition? active = ActiveGraphGroup;
        if (active != null && !active.MemberIds.Contains(id))
            return true;
        return _module?.EditorGroups.Any(group => group.Collapsed &&
            group.Id != _activeGraphGroupId && group.ParentGroupId == active?.Id &&
            group.MemberIds.Contains(id)) == true;
    }

    private bool IsGroupVisible(EventGraphGroupDefinition group) =>
        group.ParentGroupId == ActiveGraphGroup?.Id && group.Id != _activeGraphGroupId;

    private void AddNodeToActiveGroup(Guid id)
    {
        EventGraphGroupDefinition? group = ActiveGraphGroup;
        var visited = new HashSet<Guid>();
        while (group != null && visited.Add(group.Id))
        {
            if (!group.MemberIds.Contains(id))
                group.MemberIds.Add(id);
            Guid? parent = group.ParentGroupId;
            group = _module?.EditorGroups.FirstOrDefault(item => item.Id == parent);
        }
    }

    private void ActivateGraphTab(Guid id)
    {
        if (_activeGraphGroupId == id)
            return;
        _graphTabViews[_activeGraphGroupId] = (_graphCanvas.Pan, _graphCanvas.Zoom);
        _activeGraphGroupId = id;
        _selectedGraphNodes.Clear();
        _draggingGroupId = Guid.Empty;
        _resizingGroup = false;
        CancelWireDrag();
        if (_graphTabViews.TryGetValue(id, out var view))
            _graphCanvas.RestoreView(view.Pan, view.Zoom);
        else
            _requestFrameGraph = true;
    }

    private void OpenGroupGraph(EventGraphGroupDefinition group)
    {
        if (!_openGraphGroups.Contains(group.Id))
            _openGraphGroups.Add(group.Id);
        ActivateGraphTab(group.Id);
        _focusGraphTab = group.Id;
    }

    private void DrawGraphTabs()
    {
        if (_module == null)
            return;
        if (_graphTabModuleId != _module.Id)
        {
            _graphTabModuleId = _module.Id;
            _activeGraphGroupId = Guid.Empty;
            _openGraphGroups.Clear();
            _graphTabViews.Clear();
            _focusGraphTab = null;
        }
        _openGraphGroups.RemoveAll(id => !_module.EditorGroups.Any(group => group.Id == id));
        if (_activeGraphGroupId != Guid.Empty && ActiveGraphGroup == null)
            ActivateGraphTab(Guid.Empty);
        if (!ImGui.BeginTabBar("##EventGraphTabs", ImGuiTabBarFlags.Reorderable))
            return;
        if (ImGui.BeginTabItem("Event Graph"))
        {
            ActivateGraphTab(Guid.Empty);
            ImGui.EndTabItem();
        }
        for (int index = 0; index < _openGraphGroups.Count; index++)
        {
            Guid id = _openGraphGroups[index];
            EventGraphGroupDefinition group = _module.EditorGroups.First(item => item.Id == id);
            bool open = true;
            if (ImGui.BeginTabItem($"{group.Title}##{id}", ref open,
                    _focusGraphTab == id ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
            {
                ActivateGraphTab(id);
                ImGui.EndTabItem();
            }
            if (!open)
            {
                _openGraphGroups.RemoveAt(index--);
                if (_activeGraphGroupId == id)
                    ActivateGraphTab(Guid.Empty);
            }
        }
        ImGui.EndTabBar();
        _focusGraphTab = null;
    }

    private bool NodeExists(
        Guid id)
    {
        if (_module ==
            null)
        {
            return false;
        }

        foreach (EventRuleDefinition rule
                 in _module.Rules)
        {
            if (rule.Id ==
                id)
            {
                return true;
            }

            if (rule.Conditions.Any(
                    instruction =>
                        instruction.InstanceId ==
                        id) ||
                rule.Actions.Any(
                    instruction =>
                        instruction.InstanceId ==
                        id))
            {
                return true;
            }
        }

        return _module.EditorLooseConditions.Any(item => item.InstanceId == id) ||
               _module.EditorLooseActions.Any(item => item.InstanceId == id);
    }

    private void RemoveNodeFromGroups(
        Guid id)
    {
        if (_module ==
            null)
        {
            return;
        }

        foreach (EventGraphGroupDefinition group
                 in _module.EditorGroups)
        {
            group.MemberIds.RemoveAll(
                memberId =>
                    memberId ==
                    id);
        }

        _module.EditorGroups.RemoveAll(
            group =>
                group.MemberIds.Count ==
                0);
    }

    private void DrawGraphGroupBackgrounds()
    {
        if (_module ==
            null)
        {
            return;
        }

        if (ActiveGraphGroup is { } active &&
            TryGetExpandedGroupBounds(active, out Vector2 entry, out Vector2 exit))
        {
            DrawGraphBoundary("Inputs from parent graph", new Vector2(entry.X - 300, entry.Y + 25));
            DrawGraphBoundary("Outputs to parent graph", new Vector2(exit.X + 100, entry.Y + 25));
        }

        foreach (EventGraphGroupDefinition group
                 in _module.EditorGroups)
        {
            if (!IsGroupVisible(group))
                continue;
            if (!TryGetGroupBounds(
                    group,
                    out Vector2 minimum,
                    out Vector2 maximum))
            {
                continue;
            }

            _graphCanvas.DrawGroupBox(
                minimum,
                maximum,
                EditorTheme.PanelRaised with { W = 0.30f },
                EditorTheme.Border with { W = 0.75f });
            ImDrawListPtr draw = ImGui.GetWindowDrawList();
            Vector2 corner = _graphCanvas.ToScreen(maximum);
            if (group.Collapsed)
            {
                float middle = (minimum.Y + maximum.Y) * 0.5f;
                draw.AddCircleFilled(_graphCanvas.ToScreen(new Vector2(minimum.X, middle)), 5,
                    ImGui.GetColorU32(ConditionWireColor));
                draw.AddCircleFilled(_graphCanvas.ToScreen(new Vector2(maximum.X, middle)), 5,
                    ImGui.GetColorU32(ExecutionWireColor));
            }
            draw.AddText(_graphCanvas.ToScreen(minimum + new Vector2(12, 3)),
                ImGui.GetColorU32(EditorTheme.TextMuted), group.Collapsed ? "GRAPH - drag" : "COMMENT - drag");
            if (!group.Collapsed)
                draw.AddTriangleFilled(corner - new Vector2(16, 2),
                    corner - new Vector2(2, 16), corner - new Vector2(2),
                    ImGui.GetColorU32(EditorTheme.TextMuted));
        }
    }

    private void DrawGraphBoundary(string label, Vector2 position)
    {
        _graphCanvas.DrawGroupBox(position, position + new Vector2(220, 70),
            EditorTheme.PanelRaised, EditorTheme.Border);
        ImGui.GetWindowDrawList().AddText(_graphCanvas.ToScreen(position + new Vector2(10, 10)),
            ImGui.GetColorU32(EditorTheme.TextMuted), label);
    }

    private void DrawGraphGroupHeaders()
    {
        if (_module ==
            null)
        {
            return;
        }

        for (int index = 0;
             index < _module.EditorGroups.Count;
             index++)
        {
            EventGraphGroupDefinition group =
                _module.EditorGroups[index];
            if (!IsGroupVisible(group))
                continue;

            if (!TryGetGroupBounds(
                    group,
                    out Vector2 minimum,
                    out _))
            {
                continue;
            }

            Vector2 headerPosition =
                _graphCanvas.ToScreen(
                    minimum +
                    new Vector2(
                        12.0f,
                        30.0f));

            Vector2 headerSize =
                new(
                    Math.Max(
                        245.0f *
                        _graphCanvas.Zoom,
                        120.0f),
                    Math.Max(
                        (group.Collapsed ? 64.0f : 36.0f) *
                        _graphCanvas.Zoom,
                        26.0f));

            ImGui.SetCursorScreenPos(
                headerPosition);

            ImGui.PushStyleColor(
                ImGuiCol.ChildBg,
                EditorTheme.BackgroundRaised with { W = 0.96f });

            bool visible =
                ImGui.BeginChild(
                    $"GraphGroupHeader##{group.Id}",
                    headerSize,
                    ImGuiChildFlags.Borders,
                    ImGuiWindowFlags.NoScrollbar |
                    ImGuiWindowFlags.NoScrollWithMouse);

            _anyGraphNodeHovered |=
                ImGui.IsWindowHovered(
                    ImGuiHoveredFlags.RootAndChildWindows);

            bool remove =
                false;

            if (visible)
            {
                ImGui.SetWindowFontScale(
                    Math.Clamp(
                        _graphCanvas.Zoom,
                        0.35f,
                        1.5f));

                string title =
                    string.IsNullOrWhiteSpace(
                        group.Title)
                        ? "Comment"
                        : group.Title;

                if (ImGui.SmallButton(group.Collapsed ? "Open##OpenGroup" : "-##CollapseGroup"))
                {
                    if (group.Collapsed)
                        OpenGroupGraph(group);
                    else
                    {
                        RecordHistory("Collapse Graph Group");
                        group.Collapsed = true;
                        _selectedGraphNodes.Clear();
                        _dirty = true;
                    }
                }
                ImGui.SameLine();
                ImGui.SetNextItemWidth(
                    Math.Max(
                        headerSize.X -
                        112.0f,
                        60.0f));

                if (ImGui.InputText(
                        "##GroupTitle",
                        ref title,
                        96))
                {
                    group.Title =
                        title;

                    _dirty =
                        true;
                }

                ImGui.SameLine();

                if (ImGui.SmallButton(
                        "X##DeleteGroup"))
                {
                    remove =
                        true;
                }
                if (group.Collapsed)
                    ImGui.TextDisabled($"{group.MemberIds.Count} nodes - open graph");
            }

            ImGui.EndChild();

            ImGui.PopStyleColor();

            if (remove)
            {
                RecordHistory(
                    "Delete Comment Box");
                foreach (EventGraphGroupDefinition child in _module.EditorGroups)
                    if (child.ParentGroupId == group.Id)
                        child.ParentGroupId = group.ParentGroupId;

                _module.EditorGroups.RemoveAt(
                    index);

                _dirty =
                    true;

                index--;
            }
        }
    }

    private bool TryGetGroupBounds(
        EventGraphGroupDefinition group,
        out Vector2 minimum,
        out Vector2 maximum)
    {
        if (!group.EditorBoundsInitialized)
        {
            if (!TryGetExpandedGroupBounds(group, out minimum, out maximum))
                return false;
            group.EditorX = minimum.X;
            group.EditorY = minimum.Y;
            group.EditorWidth = Math.Max(310, maximum.X - minimum.X);
            group.EditorHeight = Math.Max(140, maximum.Y - minimum.Y);
            group.EditorBoundsInitialized = true;
        }
        minimum = new Vector2(group.EditorX, group.EditorY);
        maximum = minimum + (group.Collapsed ? new Vector2(310, 125) :
            new Vector2(Math.Max(310, group.EditorWidth), Math.Max(140, group.EditorHeight)));
        return true;
    }

    private void UpdateGroupPointerInteraction()
    {
        _groupPointerCaptured = false;
        if (_module == null ||
            (!_graphCanvas.IsMouseInsideCanvas && _draggingGroupId == Guid.Empty) ||
            ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId))
            return;
        Vector2 mouse = ImGui.GetIO().MousePos;
        foreach (EventGraphGroupDefinition group in _module.EditorGroups.AsEnumerable().Reverse())
        {
            if (!IsGroupVisible(group) ||
                !TryGetGroupBounds(group, out Vector2 minimum, out Vector2 maximum))
                continue;
            Vector2 min = _graphCanvas.ToScreen(minimum);
            Vector2 max = _graphCanvas.ToScreen(maximum);
            if (group.Collapsed && mouse.X >= min.X && mouse.X <= max.X &&
                mouse.Y >= min.Y && mouse.Y <= max.Y &&
                ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                OpenGroupGraph(group);
                _groupPointerCaptured = true;
                return;
            }
            bool drag = mouse.X >= min.X && mouse.X <= max.X &&
                mouse.Y >= min.Y && mouse.Y <= min.Y + Math.Max(22 * _graphCanvas.Zoom, 12);
            bool resize = !group.Collapsed && mouse.X >= max.X - 18 &&
                mouse.X <= max.X + 4 && mouse.Y >= max.Y - 18 && mouse.Y <= max.Y + 4;
            if (drag || resize || _draggingGroupId == group.Id)
            {
                _groupPointerCaptured = true;
                ImGui.SetMouseCursor(resize || _resizingGroup && _draggingGroupId == group.Id
                    ? ImGuiMouseCursor.ResizeNWSE : ImGuiMouseCursor.ResizeAll);
                if (_draggingGroupId == Guid.Empty && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    RecordHistory(resize ? "Resize Comment Box" : "Move Graph Group");
                    _draggingGroupId = group.Id;
                    _resizingGroup = resize;
                    _marqueeSelecting = false;
                    if (drag && group.Collapsed && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        OpenGroupGraph(group);
                        _draggingGroupId = Guid.Empty;
                        return;
                    }
                }
            }
            if (_draggingGroupId != group.Id)
                continue;
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                _draggingGroupId = Guid.Empty;
                _resizingGroup = false;
                continue;
            }
            Vector2 delta = ImGui.GetIO().MouseDelta / _graphCanvas.Zoom;
            if (delta == Vector2.Zero)
                continue;
            if (_resizingGroup)
            {
                group.EditorWidth = Math.Max(310, group.EditorWidth + delta.X);
                group.EditorHeight = Math.Max(140, group.EditorHeight + delta.Y);
            }
            else
            {
                group.EditorX += delta.X;
                group.EditorY += delta.Y;
                var selection = _selectedGraphNodes.ToArray();
                _selectedGraphNodes.Clear();
                _selectedGraphNodes.UnionWith(group.MemberIds);
                MoveSelectedGraphNodes(Guid.Empty, delta);
                _selectedGraphNodes.Clear();
                _selectedGraphNodes.UnionWith(selection);
                foreach (EventGraphGroupDefinition child in _module.EditorGroups)
                    if (child.ParentGroupId == group.Id)
                    {
                        child.EditorX += delta.X;
                        child.EditorY += delta.Y;
                    }
            }
            _dirty = true;
        }
    }

    private bool TryGetExpandedGroupBounds(
        EventGraphGroupDefinition group,
        out Vector2 minimum,
        out Vector2 maximum)
    {
        minimum =
            new Vector2(
                float.MaxValue,
                float.MaxValue);

        maximum =
            new Vector2(
                float.MinValue,
                float.MinValue);

        bool any =
            false;

        foreach (Guid id
                 in group.MemberIds)
        {
            if (!TryGetNodeBounds(
                    id,
                    out Vector2 nodeMinimum,
                    out Vector2 nodeMaximum))
            {
                continue;
            }

            minimum.X =
                Math.Min(
                    minimum.X,
                    nodeMinimum.X);

            minimum.Y =
                Math.Min(
                    minimum.Y,
                    nodeMinimum.Y);

            maximum.X =
                Math.Max(
                    maximum.X,
                    nodeMaximum.X);

            maximum.Y =
                Math.Max(
                    maximum.Y,
                    nodeMaximum.Y);

            any =
                true;
        }

        if (!any)
        {
            return false;
        }

        minimum -=
            new Vector2(
                42.0f,
                72.0f);

        maximum +=
            new Vector2(
                42.0f,
                42.0f);

        return true;
    }

    private bool TryGetNodeBounds(
        Guid id,
        out Vector2 minimum,
        out Vector2 maximum)
    {
        minimum =
            default;

        maximum =
            default;

        if (_module ==
            null)
        {
            return false;
        }

        foreach (EventRuleDefinition rule
                 in _module.Rules)
        {
            if (rule.Id ==
                id)
            {
                minimum =
                    new Vector2(
                        rule.EditorX,
                        rule.EditorY);

                maximum =
                    minimum +
                    GetEventGraphNodeSize();

                return true;
            }

            foreach (VisualInstruction instruction
                     in rule.Conditions.Concat(
                         rule.Actions))
            {
                if (instruction.InstanceId !=
                    id)
                {
                    continue;
                }

                minimum =
                    new Vector2(
                        instruction.EditorX,
                        instruction.EditorY);

                maximum =
                    minimum +
                    GetScaledInstructionNodeSize(
                        instruction);

                return true;
            }
        }

        VisualInstruction? loose = _module.EditorLooseConditions
            .Concat(_module.EditorLooseActions)
            .FirstOrDefault(item => item.InstanceId == id);
        if (loose != null)
        {
            minimum = new Vector2(loose.EditorX, loose.EditorY);
            maximum = minimum + GetScaledInstructionNodeSize(loose);
            return true;
        }

        return false;
    }

    private void GetInstructionPresentation(
        VisualInstruction instruction,
        bool condition,
        out string displayName,
        out string category)
    {
        displayName =
            instruction.Id;

        category =
            "Unknown";

        if (condition)
        {
            if (instruction.Id.Equals(
                    "input.mouseHeld",
                    StringComparison.OrdinalIgnoreCase))
            {
                displayName =
                    "Mouse Button Is Held";

                category =
                    "Input";

                return;
            }

            if (instruction.Id.Equals(
                    "input.mousePressed",
                    StringComparison.OrdinalIgnoreCase))
            {
                displayName =
                    "Mouse Button Pressed";

                category =
                    "Input";

                return;
            }

            if (instruction.Id.Equals(
                    "input.mouseReleased",
                    StringComparison.OrdinalIgnoreCase))
            {
                displayName =
                    "Mouse Button Released";

                category =
                    "Input";

                return;
            }

            if (instruction.Id.Equals(
                    "logic.and",
                    StringComparison.OrdinalIgnoreCase))
            {
                displayName =
                    "AND";

                category =
                    "Logic";

                return;
            }

            if (instruction.Id.Equals(
                    "logic.or",
                    StringComparison.OrdinalIgnoreCase))
            {
                displayName =
                    "OR";

                category =
                    "Logic";

                return;
            }

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

            return;
        }

        if (instruction.Id.Equals(
                "flow.branch",
                StringComparison.OrdinalIgnoreCase))
        {
            displayName =
                "Branch";

            category =
                "Flow";

            return;
        }

        if (instruction.Id.Equals(
                "object.spawnEmpty",
                StringComparison.OrdinalIgnoreCase))
        {
            displayName =
                "Spawn Empty Object";

            category =
                "Object";

            return;
        }

        if (instruction.Id.Equals(
                "object.spawnBlueprint",
                StringComparison.OrdinalIgnoreCase))
        {
            displayName =
                "Spawn Blueprint";

            category =
                "Object";

            return;
        }

        if (_registry.TryGetAction(
                instruction.Id,
                out VisualActionDefinition? actionDefinition) &&
            actionDefinition !=
            null)
        {
            displayName =
                actionDefinition.DisplayName;

            category =
                actionDefinition.Category;
        }
    }

    private static string GetInstructionCompactSummary(VisualInstruction instruction)
    {
        if (instruction.Arguments.Count == 0)
            return "Select to configure";

        IEnumerable<KeyValuePair<string, EventValue>> useful = instruction.Arguments
            .Where(pair => pair.Value != null)
            .OrderBy(pair => pair.Key.Equals("target", StringComparison.OrdinalIgnoreCase) ? 0
                : pair.Key.Equals("value", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
            .Take(2);
        string summary = string.Join("  |  ", useful.Select(pair =>
            $"{pair.Key}: {FormatCompactValue(pair.Value)}"));
        return summary.Length <= 56 ? summary : summary[..53] + "...";
    }

    private static string FormatCompactValue(EventValue value)
    {
        if (value.Kind == EventValueKind.Reference)
            return value.Reference == null ? "Choose..." : FormatReference(value.Reference);

        VariableValue constant = value.Constant;
        return constant.Type switch
        {
            VariableType.Boolean => constant.Boolean ? "true" : "false",
            VariableType.String => constant.String,
            VariableType.Number => constant.Number.ToString("0.###"),
            VariableType.Vector2 => constant.Vector2.ToString(),
            VariableType.Vector3 => constant.Vector3.ToString(),
            _ => "..."
        };
    }

    private static string GetInstructionDescription(
        VisualInstruction instruction,
        bool condition)
    {
        return instruction.Id switch
        {
            "system.always" =>
                "Always evaluates to TRUE, so the event can run every frame.",

            "system.triggerOnce" =>
                "Returns TRUE only on the first frame the condition becomes valid.",

            "time.timerFinished" =>
                "TRUE only during the update when the named timer reaches zero.",
            "time.timerRunning" =>
                "TRUE while the named timer is counting down.",
            "time.startTimer" =>
                "Starts or restarts a named timer using game time, in seconds.",
            "time.stopTimer" =>
                "Stops the named timer and clears its finish pulse.",

            "input.mouseHeld" =>
                "TRUE while the selected mouse button is being held down.",

            "input.mousePressed" =>
                "TRUE only on the frame the selected mouse button is pressed.",

            "input.mouseReleased" =>
                "TRUE only on the frame the selected mouse button is released.",

            "input.keyHeld" =>
                "TRUE while the selected keyboard key is being held down.",

            "input.keyPressed" =>
                "TRUE only on the frame the selected keyboard key is pressed.",

            "input.keyReleased" =>
                "TRUE only on the frame the selected keyboard key is released.",

            "input.actionHeld" =>
                "TRUE while the selected Input Action is active.",

            "input.actionPressed" =>
                "TRUE only when the selected Input Action is triggered this frame.",

            "input.actionReleased" =>
                "TRUE only when the selected Input Action is released this frame.",

            "input.axisGreater" =>
                "Checks whether the selected input axis is greater than the chosen value.",

            "input.axisLess" =>
                "Checks whether the selected input axis is less than the chosen value.",

            "input.vectorLengthGreater" =>
                "Checks whether the selected vector Input Action exceeds the chosen magnitude.",

            "physics.lineTrace" =>
                "Universal 3D trace. Choose a Start object or world position, then trace to an object, direction, mouse cursor, or world position.",

            "physics.lineTraceHitsAnything" =>
                "Runs the same universal Start-to-End trace and returns TRUE when the segment hits a collider.",

            "physics.castRayToCursor" =>
                "Shoots from the selected object or muzzle to the collider directly under the mouse cursor.",

            "physics.castRayInDirection" =>
                "Shoots from the selected object or muzzle in the chosen local or world direction.",

            "physics.castRay" =>
                "Casts a configurable 3D physics ray and stores the result for later raycast actions and conditions.",

            "physics.rayHitsAnything" =>
                "Casts a 3D physics ray and returns TRUE when it hits a collider.",

            "attachment.attachToSocket" =>
                "Attaches an object to a model-owned skeletal socket and follows the animated socket transform.",

            "logic.and" =>
                "TRUE only when every Condition connected to this gate is TRUE.",

            "logic.or" =>
                "TRUE when at least one Condition connected to this gate is TRUE.",

            "variable.compare" =>
                "Compares two values using the selected comparison operator.",

            "object.exists" =>
                "Checks whether the target object currently exists in the scene.",

            "object.isActive" =>
                "Checks whether the target object is currently active.",

            "object.hasTag" =>
                "Checks whether the target object has the selected tag.",

            "object.doesNotHaveTag" =>
                "Checks whether the target object does not have the selected tag.",

            "object.withTagExists" =>
                "Checks whether any object with the selected tag exists in the scene.",

            "object.isOnLayer" =>
                "Checks whether the target object is assigned to the selected layer.",

            "enemyAI.isIdle" =>
                "TRUE when the selected enemy has no target in detection range.",
            "enemyAI.isChasing" =>
                "TRUE while the selected enemy is pursuing a target outside attack range.",
            "enemyAI.isAttacking" =>
                "TRUE while the selected enemy has a target in attack range.",
            "enemyAI.attackFired" =>
                "TRUE only on the update in which the selected enemy actually damages its target.",
            "audio.isPlaying" =>
                "Checks whether the target AudioSource3D is currently playing.",

            "animation.isPlaying" =>
                "Checks whether the target AnimationController is currently playing.",

            "animation.currentClipIs" =>
                "Checks the currently playing animation clip.",

            "animation.currentStateIs" =>
                "Checks the current locomotion animation state.",

            "animation.eventFired" =>
                "TRUE only while a matching animation marker occurrence is being dispatched.",

            "animation.windowEntered" =>
                "TRUE only while a matching animation window-enter occurrence is being dispatched.",

            "animation.windowExited" =>
                "TRUE only while a matching animation window-exit occurrence is being dispatched.",

            "animation.windowActive" =>
                "Checks whether a named animation window is currently active.",

            "animation.play" =>
                "Plays the selected clip on the target AnimationController.",

            "animation.playAction" =>
                "Plays a non-looping action override, suspends locomotion, then returns to the current locomotion state when it finishes.",

            "animation.triggerAction" =>
                "Starts the selected named action, or queues its next combo action when that chain is already playing.",

            "animation.pause" =>
                "Pauses animation playback on the target AnimationController.",

            "animation.resume" =>
                "Resumes animation playback on the target AnimationController.",

            "animation.stop" =>
                "Stops animation playback on the target AnimationController.",

            "animation.setSpeed" =>
                "Changes animation playback speed on the target AnimationController.",

            "animation.setTransitionDuration" =>
                "Changes cross-fade transition duration on the target AnimationController.",

            "animation.setLayerWeight" =>
                "Sets a named Animation Profile layer's runtime weight from 0 to 1 while preserving its authored Blend In and Blend Out.",

            "animation.enableLayer" =>
                "Blends a named Animation Profile layer toward full runtime weight using the layer's authored Blend In.",

            "animation.disableLayer" =>
                "Blends a named Animation Profile layer toward zero runtime weight using the layer's authored Blend Out.",

            "character.isGrounded" =>
                "Checks whether the Character Controller is touching the ground.",

            "character.isFalling" =>
                "Checks whether the Character Controller is currently falling.",

            "character.isMoving" =>
                "Checks whether the Character Controller is currently moving.",

            "character.justLanded" =>
                "TRUE only on the frame the Character Controller lands.",

            "flow.branch" =>
                "Routes execution through the TRUE or FALSE output based on a Boolean value.",

            "object.destroySelf" =>
                "Destroys the object that owns this Event Module.",

            "object.destroy" =>
                "Destroys the selected target object.",

            "object.setActive" =>
                "Enables or disables the selected target object.",

            "object.spawnEmpty" =>
                "Creates a new empty GameObject at the selected position.",

            "object.spawnBlueprint" =>
                "Creates an instance of the selected Blueprint at the chosen position.",

            "object.addTag" =>
                "Adds the selected tag to the target object.",

            "object.removeTag" =>
                "Removes the selected tag from the target object.",

            "object.setLayer" =>
                "Moves the target object to the selected layer.",

            "audio.play" =>
                "Plays the Audio Clip already assigned to the target AudioSource3D.",

            "audio.playClip" =>
                "Loads the selected Audio Clip into the target AudioSource3D and plays it.",

            "audio.pause" =>
                "Pauses playback on the target AudioSource3D.",

            "audio.stop" =>
                "Stops playback on the target AudioSource3D.",

            "audio.setVolume" =>
                "Changes the target AudioSource3D volume.",

            "audio.setPitch" =>
                "Changes the target AudioSource3D playback pitch.",

            "audio.setLoop" =>
                "Enables or disables looping on the target AudioSource3D.",

            "character.moveForward" =>
                "Moves the Character Controller forward by the specified amount.",

            "character.moveRight" =>
                "Moves the Character Controller sideways by the specified amount.",

            "character.jump" =>
                "Requests a jump from the Character Controller.",

            "character.setVelocity" =>
                "Sets the Character Controller velocity directly.",

            "character.addImpulse" =>
                "Adds an instantaneous velocity impulse to the Character Controller.",

            "transform.setPosition" =>
                "Sets the target object's world position.",

            "transform.move" =>
                "Moves the target object by the specified offset.",

            "transform.setX" =>
                "Sets the target object's X position.",

            "transform.setY" =>
                "Sets the target object's Y position.",

            "transform.setZ" =>
                "Sets the target object's Z position.",

            "transform.setRotation" =>
                "Sets the target object's rotation.",

            "transform.rotateBy" =>
                "Rotates the target object by the specified amount.",

            "transform.setScale" =>
                "Sets the target object's scale.",

            "variable.set" =>
                "Assigns a new value to the selected variable or property.",

            "variable.add" =>
                "Adds the specified amount to the selected numeric variable.",

            "variable.subtract" =>
                "Subtracts the specified amount from the selected numeric variable.",

            "variable.toggle" =>
                "Flips the selected Boolean value between TRUE and FALSE.",

            _ =>
                condition
                    ? "Evaluates this condition and passes its TRUE/FALSE result into the event graph."
                    : "Executes this action when the event reaches this node."
        };
    }

    private static IReadOnlyList<VisualInstruction> GetConnectedConditions(
        EventRuleDefinition rule)
    {
        if (!rule.HasExplicitConditionFlow)
        {
            return rule.Conditions;
        }

        if (rule.ConnectedConditionIds.Count ==
            0)
        {
            return Array.Empty<VisualInstruction>();
        }

        HashSet<Guid> connected =
            rule.ConnectedConditionIds.ToHashSet();

        return rule.Conditions
            .Where(
                condition =>
                    connected.Contains(
                        condition.InstanceId))
            .ToList();
    }

    private static void EnsureConditionFlowInitialized(
        EventRuleDefinition rule)
    {
        if (rule.HasExplicitConditionFlow)
        {
            return;
        }

        rule.HasExplicitConditionFlow =
            true;

        rule.ConnectedConditionIds =
            rule.Conditions
                .Select(
                    condition =>
                        condition.InstanceId)
                .ToList();
    }

    private static void ConnectCondition(
        EventRuleDefinition rule,
        Guid conditionId)
    {
        rule.HasExplicitConditionFlow =
            true;

        if (!rule.ConnectedConditionIds.Contains(
                conditionId))
        {
            rule.ConnectedConditionIds.Add(
                conditionId);
        }
    }

    private static bool IsLogicGate(
        VisualInstruction instruction)
    {
        return instruction.Id.Equals(
                   "logic.and",
                   StringComparison.OrdinalIgnoreCase) ||
               instruction.Id.Equals(
                   "logic.or",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static VisualInstruction? FindCondition(
        EventRuleDefinition rule,
        Guid conditionId)
    {
        return rule.Conditions.FirstOrDefault(
            condition =>
                condition.InstanceId ==
                conditionId);
    }

    private static void DisconnectCondition(
        EventRuleDefinition rule,
        Guid conditionId)
    {
        rule.HasExplicitConditionFlow =
            true;

        rule.ConnectedConditionIds.RemoveAll(
            id =>
                id ==
                conditionId);
    }

    private static void DisconnectConditionEverywhere(
        EventRuleDefinition rule,
        Guid conditionId)
    {
        DisconnectCondition(
            rule,
            conditionId);

        foreach (VisualInstruction gate
                 in rule.Conditions.Where(
                     IsLogicGate))
        {
            gate.ConditionInputIds.RemoveAll(
                id =>
                    id ==
                    conditionId);
        }
    }

    private static void ConnectConditionToGate(
        EventRuleDefinition rule,
        VisualInstruction gate,
        Guid conditionId)
    {
        if (!IsLogicGate(
                gate))
        {
            return;
        }

        /*
         * Routing a Condition into a gate removes its direct Event-root
         * connection. This makes the visual graph match runtime meaning:
         *
         *     A ----\
         *            OR ----> Event
         *     B ----/
         *
         * rather than accidentally keeping A/B wired directly to Event too.
         * A Condition can still fan out to multiple gates.
         */
        DisconnectCondition(
            rule,
            conditionId);

        if (!gate.ConditionInputIds.Contains(
                conditionId))
        {
            gate.ConditionInputIds.Add(
                conditionId);
        }
    }

    private static bool WouldCreateConditionCycle(
        EventRuleDefinition rule,
        Guid sourceConditionId,
        Guid targetGateId)
    {
        VisualInstruction? source =
            FindCondition(
                rule,
                sourceConditionId);

        if (source ==
                null ||
            !IsLogicGate(
                source))
        {
            return false;
        }

        HashSet<Guid> visited =
            new();

        Stack<Guid> pending =
            new();

        pending.Push(
            sourceConditionId);

        while (pending.Count >
               0)
        {
            Guid currentId =
                pending.Pop();

            if (!visited.Add(
                    currentId))
            {
                continue;
            }

            if (currentId ==
                targetGateId)
            {
                return true;
            }

            VisualInstruction? current =
                FindCondition(
                    rule,
                    currentId);

            if (current ==
                    null ||
                !IsLogicGate(
                    current))
            {
                continue;
            }

            foreach (Guid inputId
                     in current.ConditionInputIds)
            {
                pending.Push(
                    inputId);
            }
        }

        return false;
    }

    private static void ConfigureCreatedLogicGate(
        EventRuleDefinition rule,
        VisualInstruction gate,
        Guid sourceConditionId,
        bool connectFromWire)
    {
        if (connectFromWire &&
            sourceConditionId !=
                Guid.Empty)
        {
            ConnectConditionToGate(
                rule,
                gate,
                sourceConditionId);
        }

        /*
         * A newly created gate becomes a root expression by default so its
         * output reaches the Event immediately.
         */
        ConnectCondition(
            rule,
            gate.InstanceId);
    }

    private static VisualInstruction? FindAction(
        EventRuleDefinition rule,
        Guid actionId)
    {
        return rule.Actions.FirstOrDefault(
            action =>
                action.InstanceId ==
                actionId);
    }

    private void EnsureExecutionFlowInitialized(
        EventRuleDefinition rule)
    {
        if (rule.HasExplicitExecutionFlow)
        {
            return;
        }

        rule.HasExplicitExecutionFlow =
            true;

        rule.FirstActionId =
            rule.Actions.Count >
                0
                ? rule.Actions[0].InstanceId
                : null;

        for (int index = 0;
             index < rule.Actions.Count;
             index++)
        {
            rule.Actions[index].NextActionId =
                index +
                1 <
                rule.Actions.Count
                    ? rule.Actions[index + 1].InstanceId
                    : null;
        }
    }

    private bool TryConnectExecutionWireAtMouse()
    {
        EventRuleDefinition? rule =
            FindRule(
                _wireDragRuleId);

        if (rule ==
            null)
        {
            return TryConnectLooseActionOutputAtMouse();
        }

        if (_module != null)
            foreach (VisualInstruction draft in _module.EditorLooseActions.ToArray())
            {
                if (!_graphCanvas.IsPointHovered(GetInstructionInput(draft), 16.0f))
                    continue;
                RecordHistory("Connect Draft Action");
                AttachLooseActionToRule(rule, draft);
                InsertDraftActionChainAfterSource(rule, _wireDragSourceInstructionId,
                    draft, _wireDragKind);
                _dirty = true;
                return true;
            }

        VisualInstruction? target =
            null;

        foreach (VisualInstruction action
                 in rule.Actions)
        {
            if (!_graphCanvas.IsPointHovered(
                    GetInstructionInput(
                        action),
                    16.0f))
            {
                continue;
            }

            target =
                action;

            break;
        }

        if (target ==
            null)
        {
            return false;
        }

        if (_wireDragSourceInstructionId ==
            target.InstanceId)
        {
            return true;
        }

        if (WouldCreateExecutionCycle(
                rule,
                _wireDragSourceInstructionId,
                target.InstanceId))
        {
            return true;
        }

        RecordHistory(
            "Connect Execution Wire");

        ConnectExecution(
            rule,
            _wireDragSourceInstructionId,
            target.InstanceId,
            _wireDragKind);

        _dirty =
            true;

        return true;
    }

    private void AttachLooseActionToRule(
        EventRuleDefinition rule, VisualInstruction action)
        => AttachLooseActionToRule(rule, action, new HashSet<Guid>());

    private void AttachLooseActionToRule(
        EventRuleDefinition rule, VisualInstruction action, HashSet<Guid> visited)
    {
        if (_module == null ||
            !visited.Add(action.InstanceId) ||
            !_module.EditorLooseActions.Remove(action))
            return;

        rule.Actions.Add(action);
        foreach (Guid? successorId in new[]
                 { action.NextActionId, action.TrueActionId, action.FalseActionId })
        {
            VisualInstruction? successor = _module.EditorLooseActions
                .FirstOrDefault(item => item.InstanceId == successorId);
            if (successor != null)
                AttachLooseActionToRule(rule, successor, visited);
        }
    }

    private bool TryConnectLooseActionOutputAtMouse()
    {
        if (_module == null)
            return false;
        VisualInstruction? source = _module.EditorLooseActions
            .FirstOrDefault(item => item.InstanceId == _wireDragSourceInstructionId);
        if (source == null)
            return false;

        foreach (EventRuleDefinition rule in _module.Rules)
        {
            if (_graphCanvas.IsPointHovered(GetEventExecutionOutput(rule), 18.0f))
            {
                RecordHistory("Connect Draft Action");
                AttachLooseActionToRule(rule, source);
                InsertDraftActionChainAfterSource(rule, Guid.Empty, source, _wireDragKind);
                _dirty = true;
                return true;
            }

            foreach (VisualInstruction target in rule.Actions)
            {
                if (!_graphCanvas.IsPointHovered(GetInstructionInput(target), 16.0f))
                    continue;
                RecordHistory("Connect Draft Action");
                AttachLooseActionToRule(rule, source);
                InsertActionBeforeTarget(rule, source, target, _wireDragKind);
                _dirty = true;
                return true;
            }
        }

        foreach (VisualInstruction target in _module.EditorLooseActions)
        {
            if (target.InstanceId == source.InstanceId ||
                !_graphCanvas.IsPointHovered(GetInstructionInput(target), 16.0f))
                continue;
            if (WouldCreateDraftActionCycle(source.InstanceId, target.InstanceId))
                return true;
            RecordHistory("Connect Draft Actions");
            if (_wireDragKind == WireDragKind.ActionTrue)
                source.TrueActionId = target.InstanceId;
            else if (_wireDragKind == WireDragKind.ActionFalse)
                source.FalseActionId = target.InstanceId;
            else
                source.NextActionId = target.InstanceId;
            _dirty = true;
            return true;
        }
        return false;
    }

    private bool WouldCreateDraftActionCycle(Guid sourceId, Guid targetId)
    {
        if (_module == null)
            return false;
        var pending = new Stack<Guid>();
        var visited = new HashSet<Guid>();
        pending.Push(targetId);
        while (pending.Count > 0)
        {
            Guid currentId = pending.Pop();
            if (currentId == sourceId)
                return true;
            if (!visited.Add(currentId))
                continue;
            VisualInstruction? action = _module.EditorLooseActions
                .FirstOrDefault(item => item.InstanceId == currentId);
            if (action == null)
                continue;
            foreach (Guid? next in new[]
                     { action.NextActionId, action.TrueActionId, action.FalseActionId })
                if (next.HasValue)
                    pending.Push(next.Value);
        }
        return false;
    }

    private static void InsertActionBeforeTarget(
        EventRuleDefinition rule, VisualInstruction source,
        VisualInstruction target, WireDragKind kind)
    {
        if (rule.FirstActionId == target.InstanceId)
            rule.FirstActionId = source.InstanceId;
        foreach (VisualInstruction candidate in rule.Actions)
        {
            if (candidate.InstanceId == source.InstanceId)
                continue;
            if (candidate.NextActionId == target.InstanceId)
                candidate.NextActionId = source.InstanceId;
            if (candidate.TrueActionId == target.InstanceId)
                candidate.TrueActionId = source.InstanceId;
            if (candidate.FalseActionId == target.InstanceId)
                candidate.FalseActionId = source.InstanceId;
        }
        VisualInstruction tail = FindDraftChainTail(rule, source);
        if (kind == WireDragKind.ActionTrue)
            source.TrueActionId = target.InstanceId;
        else if (kind == WireDragKind.ActionFalse)
            source.FalseActionId = target.InstanceId;
        else
            tail.NextActionId = target.InstanceId;
        rule.HasExplicitExecutionFlow = true;
    }

    private static VisualInstruction FindDraftChainTail(
        EventRuleDefinition rule, VisualInstruction first)
    {
        VisualInstruction current = first;
        var visited = new HashSet<Guid> { first.InstanceId };
        while (current.NextActionId.HasValue)
        {
            VisualInstruction? next = FindAction(rule, current.NextActionId.Value);
            if (next == null || !visited.Add(next.InstanceId))
                break;
            current = next;
        }
        return current;
    }

    private static void AppendDraftActionChain(
        EventRuleDefinition rule, VisualInstruction first)
    {
        rule.HasExplicitExecutionFlow = true;
        if (!rule.FirstActionId.HasValue)
        {
            rule.FirstActionId = first.InstanceId;
            return;
        }
        VisualInstruction? existingFirst = FindAction(rule, rule.FirstActionId.Value);
        if (existingFirst != null)
            FindDraftChainTail(rule, existingFirst).NextActionId = first.InstanceId;
    }

    private static void InsertDraftActionChainAfterSource(
        EventRuleDefinition rule, Guid sourceId,
        VisualInstruction first, WireDragKind kind)
    {
        VisualInstruction tail = FindDraftChainTail(rule, first);
        Guid? originalDraftNext = first.NextActionId;
        ConnectNewActionAfterSource(rule, sourceId, first, kind);
        if (originalDraftNext.HasValue && tail.InstanceId != first.InstanceId)
        {
            Guid? previousSuccessor = first.NextActionId;
            first.NextActionId = originalDraftNext;
            tail.NextActionId = previousSuccessor;
        }
    }

    private static bool WouldCreateExecutionCycle(
        EventRuleDefinition rule,
        Guid sourceActionId,
        Guid targetActionId)
    {
        if (sourceActionId ==
            Guid.Empty)
        {
            return false;
        }

        HashSet<Guid> visited =
            new();

        Stack<Guid> pending =
            new();

        pending.Push(
            targetActionId);

        while (pending.Count >
               0)
        {
            Guid currentId =
                pending.Pop();

            if (currentId ==
                sourceActionId)
            {
                return true;
            }

            if (!visited.Add(
                    currentId))
            {
                continue;
            }

            VisualInstruction? action =
                FindAction(
                    rule,
                    currentId);

            if (action ==
                null)
            {
                continue;
            }

            if (IsBranchAction(
                    action))
            {
                if (action.TrueActionId.HasValue)
                {
                    pending.Push(
                        action.TrueActionId.Value);
                }

                if (action.FalseActionId.HasValue)
                {
                    pending.Push(
                        action.FalseActionId.Value);
                }

                continue;
            }

            if (action.NextActionId.HasValue)
            {
                pending.Push(
                    action.NextActionId.Value);
            }
        }

        return false;
    }

    private static void ConnectExecution(
        EventRuleDefinition rule,
        Guid sourceActionId,
        Guid targetActionId,
        WireDragKind sourceKind)
    {
        rule.HasExplicitExecutionFlow =
            true;

        DisconnectIncomingExecution(
            rule,
            targetActionId);

        if (sourceActionId ==
            Guid.Empty)
        {
            rule.FirstActionId =
                targetActionId;

            return;
        }

        VisualInstruction? source =
            FindAction(
                rule,
                sourceActionId);

        if (source ==
            null)
        {
            return;
        }

        switch (sourceKind)
        {
            case WireDragKind.ActionTrue:
                source.TrueActionId =
                    targetActionId;
                break;

            case WireDragKind.ActionFalse:
                source.FalseActionId =
                    targetActionId;
                break;

            default:
                source.NextActionId =
                    targetActionId;
                break;
        }
    }

    private static void DisconnectIncomingExecution(
        EventRuleDefinition rule,
        Guid targetActionId)
    {
        if (rule.FirstActionId ==
            targetActionId)
        {
            rule.FirstActionId =
                null;
        }

        foreach (VisualInstruction action
                 in rule.Actions)
        {
            if (action.NextActionId ==
                targetActionId)
            {
                action.NextActionId =
                    null;
            }

            if (action.TrueActionId ==
                targetActionId)
            {
                action.TrueActionId =
                    null;
            }

            if (action.FalseActionId ==
                targetActionId)
            {
                action.FalseActionId =
                    null;
            }
        }
    }

    private static void ConnectNewActionAfterSource(
        EventRuleDefinition rule,
        Guid sourceActionId,
        VisualInstruction created,
        WireDragKind sourceKind)
    {
        rule.HasExplicitExecutionFlow =
            true;

        if (sourceActionId ==
            Guid.Empty)
        {
            Guid? previousFirst =
                rule.FirstActionId;

            rule.FirstActionId =
                created.InstanceId;

            created.NextActionId =
                previousFirst;

            return;
        }

        VisualInstruction? source =
            FindAction(
                rule,
                sourceActionId);

        if (source ==
            null)
        {
            AppendActionToExecutionFlow(
                rule,
                created);

            return;
        }

        if (sourceKind ==
            WireDragKind.ActionTrue)
        {
            Guid? previous =
                source.TrueActionId;

            source.TrueActionId =
                created.InstanceId;

            created.NextActionId =
                previous;

            return;
        }

        if (sourceKind ==
            WireDragKind.ActionFalse)
        {
            Guid? previous =
                source.FalseActionId;

            source.FalseActionId =
                created.InstanceId;

            created.NextActionId =
                previous;

            return;
        }

        Guid? previousNext =
            source.NextActionId;
        source.NextActionId =
            created.InstanceId;

        created.NextActionId =
            previousNext;
    }

    private static void AppendActionToExecutionFlow(
        EventRuleDefinition rule,
        VisualInstruction action)
    {
        rule.HasExplicitExecutionFlow =
            true;

        action.NextActionId =
            null;

        if (!rule.FirstActionId.HasValue)
        {
            rule.FirstActionId =
                action.InstanceId;

            return;
        }

        HashSet<Guid> visited =
            new();

        Guid currentId =
            rule.FirstActionId.Value;

        while (visited.Add(
                   currentId))
        {
            VisualInstruction? current =
                FindAction(
                    rule,
                    currentId);

            if (current ==
                null)
            {
                rule.FirstActionId =
                    action.InstanceId;

                return;
            }

            /*
             * Once a Branch is reached, there is no single unambiguous tail.
             * Leave newly-added Actions disconnected so the user explicitly
             * wires them to TRUE or FALSE.
             */
            if (IsBranchAction(
                    current))
            {
                return;
            }

            if (!current.NextActionId.HasValue)
            {
                current.NextActionId =
                    action.InstanceId;

                return;
            }

            currentId =
                current.NextActionId.Value;
        }
    }

    private static void RemoveActionFromExecutionFlow(
        EventRuleDefinition rule,
        VisualInstruction action)
    {
        if (!rule.HasExplicitExecutionFlow)
        {
            return;
        }

        Guid? successor =
            IsBranchAction(
                action)
                ? null
                : action.NextActionId;

        if (rule.FirstActionId ==
            action.InstanceId)
        {
            rule.FirstActionId =
                successor;
        }

        foreach (VisualInstruction candidate
                 in rule.Actions)
        {
            if (candidate.InstanceId ==
                action.InstanceId)
            {
                continue;
            }

            if (candidate.NextActionId ==
                action.InstanceId)
            {
                candidate.NextActionId =
                    successor;
            }

            if (candidate.TrueActionId ==
                action.InstanceId)
            {
                candidate.TrueActionId =
                    successor;
            }

            if (candidate.FalseActionId ==
                action.InstanceId)
            {
                candidate.FalseActionId =
                    successor;
            }
        }

        action.NextActionId =
            null;

        action.TrueActionId =
            null;

        action.FalseActionId =
            null;
    }

}
