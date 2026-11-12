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
    private void InitializeMissingGraphLayout()
    {
        if (_module ==
            null)
        {
            return;
        }

        if (_module.EditorNodeScale <
                0.55f ||
            _module.EditorNodeScale >
                1.15f)
        {
            _module.EditorNodeScale =
                0.72f;
        }

        _module.EditorGroups ??=
            new List<EventGraphGroupDefinition>();

        for (int ruleIndex = 0;
             ruleIndex < _module.Rules.Count;
             ruleIndex++)
        {
            EventRuleDefinition rule =
                _module.Rules[ruleIndex];

            if (!rule.EditorLayoutInitialized)
            {
                Vector2 position =
                    GetDefaultRulePosition(
                        ruleIndex);

                rule.EditorX =
                    position.X;

                rule.EditorY =
                    position.Y;

                rule.EditorLayoutInitialized =
                    true;
            }

            InitializeMissingInstructionLayout(
                rule);

            foreach (VisualInstruction condition
                     in rule.Conditions)
            {
                condition.ConditionInputIds ??=
                    new List<Guid>();
            }

            EnsureConditionFlowInitialized(
                rule);

            EnsureExecutionFlowInitialized(
                rule);
        }
    }

    private void InitializeMissingInstructionLayout(
        EventRuleDefinition rule)
    {
        float conditionY =
            rule.EditorY;

        for (int index = 0;
             index < rule.Conditions.Count;
             index++)
        {
            VisualInstruction instruction =
                rule.Conditions[index];

            Vector2 size =
                GetScaledInstructionNodeSize(
                    instruction);

            if (!instruction.EditorLayoutInitialized)
            {
                instruction.EditorX =
                    rule.EditorX -
                    size.X -
                    120.0f;

                instruction.EditorY =
                    conditionY;

                instruction.EditorLayoutInitialized =
                    true;
            }

            conditionY +=
                size.Y +
                36.0f;
        }

        float actionX =
            rule.EditorX +
            GetEventGraphNodeSize().X +
            120.0f;

        for (int index = 0;
             index < rule.Actions.Count;
             index++)
        {
            VisualInstruction instruction =
                rule.Actions[index];

            Vector2 size =
                GetScaledInstructionNodeSize(
                    instruction);

            if (!instruction.EditorLayoutInitialized)
            {
                instruction.EditorX =
                    actionX;

                instruction.EditorY =
                    rule.EditorY;

                instruction.EditorLayoutInitialized =
                    true;
            }

            actionX +=
                size.X +
                90.0f;
        }
    }

    private void AutoArrangeGraph()
    {
        if (_module ==
            null)
        {
            return;
        }

        if (ActiveGraphGroup is { } active)
        {
            int index = 0;
            foreach (Guid id in active.MemberIds.Where(id => !IsNodeHiddenByGroup(id)))
            {
                if (!TryGetNodeBounds(id, out Vector2 position, out _))
                    continue;
                var previousSelection = _selectedGraphNodes.ToArray();
                _selectedGraphNodes.Clear();
                _selectedGraphNodes.Add(id);
                MoveSelectedGraphNodes(id,
                    new Vector2(100 + index % 4 * 400, 100 + index / 4 * 250) - position);
                _selectedGraphNodes.Clear();
                _selectedGraphNodes.UnionWith(previousSelection);
                index++;
            }
            return;
        }

        float nextY =
            100.0f;

        for (int ruleIndex = 0;
             ruleIndex < _module.Rules.Count;
             ruleIndex++)
        {
            EventRuleDefinition rule =
                _module.Rules[ruleIndex];

            rule.EditorX =
                470.0f;

            rule.EditorY =
                nextY;

            rule.EditorLayoutInitialized =
                true;

            float conditionY =
                rule.EditorY;

            float conditionTotalHeight =
                0.0f;

            for (int index = 0;
                 index < rule.Conditions.Count;
                 index++)
            {
                VisualInstruction instruction =
                    rule.Conditions[index];

                Vector2 size =
                    GetScaledInstructionNodeSize(
                        instruction);

                instruction.EditorX =
                    rule.EditorX -
                    size.X -
                    120.0f;

                instruction.EditorY =
                    conditionY;

                instruction.EditorLayoutInitialized =
                    true;

                conditionY +=
                    size.Y +
                    36.0f;

                conditionTotalHeight +=
                    size.Y +
                    36.0f;
            }

            float actionX =
                rule.EditorX +
                GetEventGraphNodeSize().X +
                120.0f;

            foreach (VisualInstruction instruction
                     in rule.Actions)
            {
                Vector2 size =
                    GetScaledInstructionNodeSize(
                        instruction);

                instruction.EditorX =
                    actionX;

                instruction.EditorY =
                    rule.EditorY;

                instruction.EditorLayoutInitialized =
                    true;

                actionX +=
                    size.X +
                    90.0f;
            }

            nextY +=
                Math.Max(
                    470.0f *
                    GetNodeScale(),
                    conditionTotalHeight +
                    100.0f);
        }
    }

    private static Vector2 GetDefaultRulePosition(
        int index)
    {
        return new Vector2(
            470.0f,
            100.0f +
            index *
            560.0f);
    }

    private static Vector2 GetInstructionNodeBaseSize(
        VisualInstruction instruction)
    {
        float height =
            instruction.Id switch
            {
                "logic.and" or
                "logic.or" =>
                    155.0f,

                "flow.branch" =>
                    245.0f,

                "input.mouseHeld" or
                "input.mousePressed" or
                "input.mouseReleased" =>
                    170.0f,

                "system.always" or
                "system.triggerOnce" or
                "character.isGrounded" or
                "character.isFalling" or
                "character.isMoving" or
                "character.justLanded" or
                "character.jump" or
                "object.destroySelf" =>
                    125.0f,

                "input.keyHeld" or
                "input.keyPressed" or
                "input.keyReleased" =>
                    170.0f,

                "object.exists" or
                "object.isActive" or
                "object.destroy" or
                "audio.play" or
                "audio.pause" or
                "audio.stop" or
                "audio.isPlaying" or
                "animation.pause" or
                "animation.resume" or
                "animation.stop" or
                "animation.isPlaying" or
                "enemyAI.isIdle" or
                "enemyAI.isChasing" or
                "enemyAI.isAttacking" or
                "enemyAI.attackFired" =>
                    190.0f,

                "time.timerFinished" or
                "time.timerRunning" or
                "time.stopTimer" =>
                    190.0f,

                "time.startTimer" =>
                    300.0f,

                "audio.playClip" =>
                    300.0f,

                "animation.eventFired" or
                "animation.windowEntered" or
                "animation.windowExited" =>
                    610.0f,

                "animation.windowActive" =>
                    500.0f,

                "animation.currentClipIs" or
                "animation.currentStateIs" or
                "animation.setSpeed" or
                "animation.setTransitionDuration" =>
                    350.0f,

                "animation.enableLayer" or
                "animation.disableLayer" =>
                    430.0f,

                "animation.setLayerWeight" =>
                    540.0f,

                "animation.play" =>
                    500.0f,

                "animation.playAction" =>
                    610.0f,

                "physics.lineTrace" or
                "physics.lineTraceHitsAnything" =>
                    620.0f,

                "physics.castRayToCursor" or
                "physics.castRayInDirection" or
                "physics.castRay" or
                "physics.rayHitsAnything" =>
                    620.0f,

                "attachment.attachToSocket" =>
                    520.0f,

                "animation.triggerAction" =>
                    430.0f,

                "object.setActive" or
                "audio.setVolume" or
                "audio.setPitch" or
                "audio.setLoop" =>
                    285.0f,

                "object.spawnEmpty" =>
                    395.0f,

                "object.spawnBlueprint" =>
                    430.0f,

                "character.moveForward" or
                "character.moveRight" =>
                    205.0f,

                "character.setVelocity" or
                "character.addImpulse" =>
                    270.0f,

                "transform.setPosition" or
                "transform.move" or
                "transform.setRotation" or
                "transform.rotateBy" or
                "transform.setScale" =>
                    350.0f,

                "transform.setX" or
                "transform.setY" or
                "transform.setZ" =>
                    295.0f,

                "variable.compare" =>
                    510.0f,

                "variable.set" =>
                    390.0f,

                "variable.add" or
                "variable.subtract" =>
                    355.0f,

                "variable.toggle" =>
                    230.0f,

                _ =>
                    220.0f
            };

        return new Vector2(
            320.0f,
            height +
            72.0f);
    }

    private Vector2 GetEventConditionInput(
        EventRuleDefinition rule)
    {
        if (!_drawingGraphWires && IsNodeHiddenByGroup(rule.Id))
            return new Vector2(-1000000);
        Vector2 size =
            GetEventGraphNodeSize();

        return new Vector2(
            rule.EditorX,
            rule.EditorY +
            Math.Min(
                62.0f *
                GetNodeScale(),
                size.Y *
                0.34f));
    }

    private Vector2 GetEventExecutionOutput(
        EventRuleDefinition rule)
    {
        if (!_drawingGraphWires && IsNodeHiddenByGroup(rule.Id))
            return new Vector2(-1000000);
        Vector2 size =
            GetEventGraphNodeSize();

        return new Vector2(
            rule.EditorX +
            size.X,
            rule.EditorY +
            Math.Min(
                62.0f *
                GetNodeScale(),
                size.Y *
                0.34f));
    }

    private static bool IsBranchAction(
        VisualInstruction instruction)
    {
        return instruction.Id.Equals(
            "flow.branch",
            StringComparison.OrdinalIgnoreCase);
    }

    private Vector2 GetBranchTrueOutput(
        VisualInstruction instruction)
    {
        if (!_drawingGraphWires && IsNodeHiddenByGroup(instruction.InstanceId))
            return new Vector2(-1000000);
        Vector2 size =
            GetScaledInstructionNodeSize(
                instruction);

        return new Vector2(
            instruction.EditorX +
            size.X,
            instruction.EditorY +
            Math.Min(
                92.0f *
                GetNodeScale(),
                size.Y *
                0.42f));
    }

    private Vector2 GetBranchFalseOutput(
        VisualInstruction instruction)
    {
        if (!_drawingGraphWires && IsNodeHiddenByGroup(instruction.InstanceId))
            return new Vector2(-1000000);
        Vector2 size =
            GetScaledInstructionNodeSize(
                instruction);

        return new Vector2(
            instruction.EditorX +
            size.X,
            instruction.EditorY +
            Math.Min(
                150.0f *
                GetNodeScale(),
                size.Y *
                0.72f));
    }

    private Vector2 GetInstructionInput(
        VisualInstruction instruction)
    {
        if (!_drawingGraphWires && IsNodeHiddenByGroup(instruction.InstanceId))
            return new Vector2(-1000000);
        Vector2 size =
            GetScaledInstructionNodeSize(
                instruction);

        return new Vector2(
            instruction.EditorX,
            instruction.EditorY +
            Math.Min(
                52.0f *
                GetNodeScale(),
                size.Y *
                0.32f));
    }

    private Vector2 GetInstructionOutput(
        VisualInstruction instruction)
    {
        if (!_drawingGraphWires && IsNodeHiddenByGroup(instruction.InstanceId))
            return new Vector2(-1000000);
        Vector2 size =
            GetScaledInstructionNodeSize(
                instruction);

        return new Vector2(
            instruction.EditorX +
            size.X,
            instruction.EditorY +
            Math.Min(
                52.0f *
                GetNodeScale(),
                size.Y *
                0.32f));
    }

    private void BeginNodeDragHistory(
        Guid nodeId,
        string label)
    {
        if (_dragHistoryNodeId ==
            nodeId)
        {
            return;
        }

        RecordHistory(
            label);

        _dragHistoryNodeId =
            nodeId;
    }

    private void MoveSelectedGraphNodes(
        Guid anchorNodeId,
        Vector2 delta)
    {
        if (_module ==
            null)
        {
            return;
        }

        if (_selectedGraphNodes.Count ==
            0)
        {
            _selectedGraphNodes.Add(
                anchorNodeId);
        }

        /*
         * Every graph node moves independently.
         *
         * An Event owns Conditions/Actions logically at runtime, but their
         * ByteGraph editor positions are independent. Moving an Event must
         * therefore only move the Event card itself and let the wires stretch
         * to the existing Condition/Action positions.
         *
         * If the user actually wants several nodes to move together, they can
         * multi-select them (Ctrl/Shift or marquee) and drag the selection.
         */
        foreach (EventRuleDefinition rule
                 in _module.Rules)
        {
            if (_selectedGraphNodes.Contains(
                    rule.Id))
            {
                rule.EditorX +=
                    delta.X;

                rule.EditorY +=
                    delta.Y;
            }

            foreach (VisualInstruction instruction
                     in rule.Conditions)
            {
                if (!_selectedGraphNodes.Contains(
                        instruction.InstanceId))
                {
                    continue;
                }

                instruction.EditorX +=
                    delta.X;

                instruction.EditorY +=
                    delta.Y;
            }

            foreach (VisualInstruction instruction
                     in rule.Actions)
            {
                if (!_selectedGraphNodes.Contains(
                        instruction.InstanceId))
                {
                    continue;
                }

                instruction.EditorX +=
                    delta.X;

                instruction.EditorY +=
                    delta.Y;
            }
        }

        foreach (VisualInstruction instruction in _module.EditorLooseConditions
                     .Concat(_module.EditorLooseActions))
        {
            if (!_selectedGraphNodes.Contains(instruction.InstanceId))
                continue;
            instruction.EditorX += delta.X;
            instruction.EditorY += delta.Y;
        }
    }

    private void FrameEntireGraph()
    {
        if (_module == null)
            return;
        Vector2 minimum = new(float.MaxValue);
        Vector2 maximum = new(float.MinValue);
        IEnumerable<Guid> ids = _module.Rules.Select(rule => rule.Id)
            .Concat(_module.Rules.SelectMany(rule => rule.Conditions.Concat(rule.Actions))
                .Select(item => item.InstanceId))
            .Concat(_module.EditorLooseConditions.Concat(_module.EditorLooseActions)
                .Select(item => item.InstanceId));
        foreach (Guid id in ids)
            if (!IsNodeHiddenByGroup(id) &&
                TryGetNodeBounds(id, out Vector2 min, out Vector2 max))
                IncludeGraphRect(ref minimum, ref maximum, min, max - min);
        foreach (EventGraphGroupDefinition group in _module.EditorGroups)
            if (IsGroupVisible(group) &&
                TryGetGroupBounds(group, out Vector2 min, out Vector2 max))
                IncludeGraphRect(ref minimum, ref maximum, min, max - min);
        if (ActiveGraphGroup is { } active &&
            TryGetExpandedGroupBounds(active, out Vector2 entry, out Vector2 exit))
            IncludeGraphRect(ref minimum, ref maximum, entry - new Vector2(260, 0),
                exit - entry + new Vector2(520, 0));
        if (minimum.X == float.MaxValue)
            _graphCanvas.ResetView();
        else
            _graphCanvas.FrameBounds(minimum, maximum);
    }

    private static void IncludeGraphRect(
        ref Vector2 minimum,
        ref Vector2 maximum,
        Vector2 position,
        Vector2 size)
    {
        minimum.X =
            Math.Min(
                minimum.X,
                position.X);

        minimum.Y =
            Math.Min(
                minimum.Y,
                position.Y);

        maximum.X =
            Math.Max(
                maximum.X,
                position.X +
                size.X);

        maximum.Y =
            Math.Max(
                maximum.Y,
                position.Y +
                size.Y);
    }

    // ========================================================
    // ADD CONDITION / ACTION
    // ========================================================

    private void DrawConditionPicker(
        EventRuleDefinition rule,
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

        if (ImGui.BeginMenu(
                "Mouse"))
        {
            if (DrawMouseConditionPickerItems(
                    rule,
                    conditions))
            {
                ImGui.CloseCurrentPopup();
            }

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
                    CreateInstruction(
                        "logic.and");

                conditions.Add(
                    created);

                ConnectCondition(
                    rule,
                    created.InstanceId);

                _dirty =
                    true;

                ImGui.CloseCurrentPopup();
            }

            if (ImGui.MenuItem(
                    "OR"))
            {
                RecordHistory(
                    "Add OR");

                VisualInstruction created =
                    CreateInstruction(
                        "logic.or");

                conditions.Add(
                    created);

                ConnectCondition(
                    rule,
                    created.InstanceId);

                _dirty =
                    true;

                ImGui.CloseCurrentPopup();
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
                        CreateInstruction(
                            definition.Id);

                    conditions.Add(
                        created);

                    ConnectCondition(
                        rule,
                        created.InstanceId);

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
        EventRuleDefinition rule,
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

        if (ImGui.BeginMenu(
                "Flow"))
        {
            if (ImGui.MenuItem(
                    "Branch"))
            {
                RecordHistory(
                    "Add Branch");

                VisualInstruction created =
                    CreateInstruction(
                        "flow.branch");

                actions.Add(
                    created);

                AppendActionToExecutionFlow(
                    rule,
                    created);

                _dirty =
                    true;

                ImGui.CloseCurrentPopup();
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
                        CreateInstruction(
                            "object.spawnEmpty");

                    actions.Add(
                        created);

                    AppendActionToExecutionFlow(
                        rule,
                        created);

                    _dirty =
                        true;

                    ImGui.CloseCurrentPopup();
                }

                if (ImGui.MenuItem(
                        "Spawn Blueprint"))
                {
                    RecordHistory(
                        "Add Spawn Blueprint");

                    VisualInstruction created =
                        CreateInstruction(
                            "object.spawnBlueprint");

                    actions.Add(
                        created);

                    AppendActionToExecutionFlow(
                        rule,
                        created);

                    _dirty =
                        true;

                    ImGui.CloseCurrentPopup();
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
                        CreateInstruction(
                            definition.Id);

                    actions.Add(
                        created);

                    AppendActionToExecutionFlow(
                        rule,
                        created);

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

    private VisualInstruction CreateInstruction(
        string id)
    {
        var instruction =
            new VisualInstruction
            {
                Id = id
            };
        AddNodeToActiveGroup(instruction.InstanceId);

        switch (id)
        {
            case "time.startTimer":
                instruction.Arguments["name"] = EventValue.String("Timer");
                instruction.Arguments["duration"] = EventValue.Number(1.0);
                break;
            case "time.stopTimer":
            case "time.timerFinished":
            case "time.timerRunning":
                instruction.Arguments["name"] = EventValue.String("Timer");
                break;

            case "input.mouseHeld":
            case "input.mousePressed":
            case "input.mouseReleased":
                instruction.Arguments["button"] =
                    EventValue.String(
                        "Left");
                break;

            case "flow.branch":
                instruction.Arguments["condition"] =
                    EventValue.Boolean(
                        false);
                break;

            case "input.keyHeld":
            case "input.keyPressed":
            case "input.keyReleased":
                instruction.Arguments["key"] =
                    EventValue.String(
                        "W");
                break;

            case "input.actionHeld":
            case "input.actionPressed":
            case "input.actionReleased":
                instruction.Arguments["action"] = EventValue.String(DefaultActionToken("Jump"));
                break;

            case "input.axisGreater":
            case "input.vectorLengthGreater":
                instruction.Arguments["action"] = EventValue.String(DefaultActionToken("Move"));
                instruction.Arguments["value"] = EventValue.Number(.5);
                break;

            case "input.axisLess":
                instruction.Arguments["action"] = EventValue.String(DefaultActionToken("Move"));
                instruction.Arguments["value"] = EventValue.Number(-.5);
                break;

            case "physics.lineTrace":
            case "physics.lineTraceHitsAnything":
                // New traces default to a useful object + direction workflow.
                // start/end are kept too so Event Sheets created by the first
                // universal-trace pass remain loadable without migration.
                instruction.Arguments["startMode"] = EventValue.String("Object");
                instruction.Arguments["startObject"] = EventValue.String("Self");
                instruction.Arguments["start"] = EventValue.Vector3(Vector3.Zero);
                instruction.Arguments["endMode"] = EventValue.String("Direction");
                instruction.Arguments["endObject"] = EventValue.String("Self");
                instruction.Arguments["end"] = EventValue.Vector3(new Vector3(0f, 0f, -10f));
                instruction.Arguments["traceDirectionMode"] = EventValue.String("Forward");
                instruction.Arguments["traceDirection"] = EventValue.Vector3(new Vector3(0f, 0f, -1f));
                instruction.Arguments["distance"] = EventValue.Number(100);
                instruction.Arguments["layer"] = EventValue.Number(-1);
                instruction.Arguments["ignoreSelf"] = EventValue.Boolean(true);
                instruction.Arguments["includeTriggers"] = EventValue.Boolean(false);
                instruction.Arguments["drawDebug"] = EventValue.Boolean(false);
                instruction.Arguments["debugDuration"] = EventValue.Number(.25);
                break;

            case "physics.castRayToCursor":
            case "physics.castRayInDirection":
            case "physics.castRay":
            case "physics.rayHitsAnything":
                instruction.Arguments["source"] = EventValue.String("Self");
                instruction.Arguments["muzzlePath"] = EventValue.String(string.Empty);
                instruction.Arguments["aimMode"] = EventValue.String(
                    id == "physics.castRayToCursor" ? "TopDownCursor" : "MuzzleDirection");
                instruction.Arguments["topDownAimStyle"] = EventValue.String("Exact3D");
                instruction.Arguments["directionMode"] = EventValue.String("Forward");
                instruction.Arguments["originOffset"] = EventValue.Vector3(Vector3.Zero);

                /*
                 * Keep the legacy direction/worldSpace values serialized too.
                 * This preserves compatibility with existing Event Sheets and
                 * older runtime data while the editor presents presets.
                 */
                instruction.Arguments["direction"] =
                    EventValue.Vector3(
                        new Vector3(
                            0f,
                            0f,
                            -1f));

                instruction.Arguments["worldSpace"] =
                    EventValue.Boolean(
                        false);

                instruction.Arguments["distance"] = EventValue.Number(100);
                instruction.Arguments["layer"] = EventValue.Number(-1);
                instruction.Arguments["includeTriggers"] = EventValue.Boolean(false);
                instruction.Arguments["drawDebug"] = EventValue.Boolean(false);
                instruction.Arguments["debugDuration"] = EventValue.Number(.25);
                break;
            case "scene.load":
                instruction.Arguments["path"]=EventValue.String("Scenes/Workshop.bytescene");break;
            case "ui.setText":
            case "ui.setTextFromHealth":
            case "ui.setButtonLabel":
            case "ui.setBarValue":
            case "ui.setBarMaximum":
            case "ui.setBarFromHealth":
            case "ui.show":
            case "ui.hide":
            case "ui.setButtonEnabled":
            case "ui.buttonClicked":
            case "ui.buttonHovered":
            case "ui.isVisible":
            case "ui.barBelow":
            case "ui.barAbove":
            case "ui.setImage":
            case "ui.focus":
            case "ui.buttonFocused":
            case "ui.buttonEnabled":
            case "ui.setLanguage":
            case "ui.languageIs":
            case "ui.setTextKey":
            case "ui.setLabelKey":
            case "ui.playAnimation":
            case "ui.stopAnimation":
            case "ui.animationPlaying":
                instruction.Arguments["target"] = EventValue.String("Self");
                if (id is "ui.setText" or "ui.setButtonLabel")
                    instruction.Arguments["text"] = EventValue.String(string.Empty);
                if (id is "ui.setBarValue" or "ui.setBarMaximum" or "ui.barBelow" or "ui.barAbove")
                    instruction.Arguments["value"] = EventValue.Number(100);
                if (id == "ui.setImage")
                    instruction.Arguments["image"] = EventValue.String(string.Empty);
                if (id is "ui.setBarFromHealth" or "ui.setTextFromHealth")
                    instruction.Arguments["source"] = EventValue.String("Self");
                if (id == "ui.setTextFromHealth")
                {
                    instruction.Arguments["prefix"] = EventValue.String("HP ");
                    instruction.Arguments["prefixKey"] = EventValue.String(string.Empty);
                }
                if (id == "ui.setButtonEnabled")
                    instruction.Arguments["enabled"] = EventValue.Boolean(true);
                if (id is "ui.setLanguage" or "ui.languageIs")
                    instruction.Arguments["language"] = EventValue.String("en");
                if (id is "ui.setTextKey" or "ui.setLabelKey")
                    instruction.Arguments["key"] = EventValue.String("menu.play");
                break;
            case "material.setMaterial":
            case "material.setBaseColor":
            case "material.setMetallic":
            case "material.setRoughness":
            case "material.setEmissionColor":
            case "material.setEmissionIntensity":
            case "material.setTexture":
            case "material.resetOverrides":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["slot"] = EventValue.Number(0);
                if (id == "material.setMaterial")
                    instruction.Arguments["material"] = EventValue.String(string.Empty);
                if (id == "material.setTexture")
                {
                    instruction.Arguments["textureSlot"] = EventValue.String("BaseColor");
                    instruction.Arguments["texture"] = EventValue.String(string.Empty);
                }
                if (id is "material.setBaseColor" or "material.setEmissionColor")
                    instruction.Arguments["color"] = EventValue.Vector3(Vector3.One);
                if (id == "material.setBaseColor")
                    instruction.Arguments["alpha"] = EventValue.Number(1);
                if (id is "material.setMetallic" or "material.setRoughness" or
                    "material.setEmissionIntensity")
                    instruction.Arguments["value"] = EventValue.Number(1);
                break;
            case "combat.fireWeapon":
            case "combat.canFire":
            case "health.isDead":
            case "enemyAI.isIdle":
            case "waves.isIdle":
            case "waves.isRunning":
            case "waves.isSpawning":
            case "waves.isWaitingForClear":
            case "waves.isIntermission":
            case "waves.isCompleted":
            case "waves.isFailed":
            case "waves.waveStarted":
            case "waves.waveCleared":
            case "waves.completedThisFrame":
            case "waves.failedThisFrame":
            case "waves.noEnemiesRemain":
            case "waves.start":
            case "waves.stop":
            case "waves.restart":
            case "enemyAI.isChasing":
            case "enemyAI.isAttacking":
            case "enemyAI.attackFired":
            case "physics.lastRayHitObject":
            case "projectile.fire":
                instruction.Arguments["target"] = EventValue.String("Self");
                break;
            case "health.percentAtMost":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["value"] = EventValue.Number(.25);
                break;
            case "health.damage":
            case "health.heal":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["amount"] = EventValue.Number(20);
                break;
            case "combat.damageLastRayHit":
                instruction.Arguments["amount"] = EventValue.Number(20);
                break;
            case "physics.saveRayHit":
                instruction.Arguments["prefix"] = EventValue.String("Ray");
                break;

            case "attachment.attachToSocket":
                instruction.Arguments["object"] = EventValue.String("Self");
                instruction.Arguments["parent"] = EventValue.String("Self");
                instruction.Arguments["socket"] = EventValue.String(string.Empty);
                instruction.Arguments["locationRule"] = EventValue.String("SnapToTarget");
                instruction.Arguments["rotationRule"] = EventValue.String("SnapToTarget");
                instruction.Arguments["scaleRule"] = EventValue.String("KeepRelative");
                instruction.Arguments["positionOffset"] = EventValue.Vector3(Vector3.Zero);
                instruction.Arguments["rotationOffset"] = EventValue.Vector3(Vector3.Zero);
                instruction.Arguments["scaleMultiplier"] = EventValue.Vector3(Vector3.One);
                break;
            case "attachment.detach":
                instruction.Arguments["object"] = EventValue.String("Self");
                instruction.Arguments["keepWorld"] = EventValue.Boolean(true);
                break;
            case "attachment.isAttached":
                instruction.Arguments["object"] = EventValue.String("Self");
                break;
            case "attachment.isAttachedToSocket":
                instruction.Arguments["object"] = EventValue.String("Self");
                instruction.Arguments["parent"] = EventValue.String("Self");
                instruction.Arguments["socket"] = EventValue.String(string.Empty);
                break;
            case "animation.eventFired":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["event"] = EventValue.String(string.Empty);
                instruction.Arguments["clip"] = EventValue.String(string.Empty);
                instruction.Arguments["payload"] = EventValue.String(string.Empty);
                break;

            case "animation.windowEntered":
            case "animation.windowExited":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["window"] = EventValue.String(string.Empty);
                instruction.Arguments["clip"] = EventValue.String(string.Empty);
                instruction.Arguments["payload"] = EventValue.String(string.Empty);
                break;

            case "animation.windowActive":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["window"] = EventValue.String(string.Empty);
                instruction.Arguments["clip"] = EventValue.String(string.Empty);
                break;
            case "animation.pause":
            case "animation.resume":
            case "animation.stop":
            case "animation.isPlaying":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                break;

            case "animation.play":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["clip"] =
                    EventValue.String(
                        string.Empty);
                instruction.Arguments["loop"] =
                    EventValue.Boolean(
                        true);
                break;

            case "animation.playAction":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["clip"] =
                    EventValue.String(
                        string.Empty);
                instruction.Arguments["retrigger"] =
                    EventValue.Boolean(
                        false);
                instruction.Arguments["interruptCurrent"] =
                    EventValue.Boolean(
                        true);
                break;

            case "animation.playNamedAction":
                instruction.Arguments["target"]=EventValue.String("Self"); instruction.Arguments["action"]=EventValue.String(string.Empty);
                instruction.Arguments["retrigger"]=EventValue.Boolean(false); instruction.Arguments["queueIfBlocked"]=EventValue.Boolean(false); instruction.Arguments["forceInterrupt"]=EventValue.Boolean(false); break;
            case "animation.queueNamedAction":
            case "animation.currentActionIs":
                instruction.Arguments["target"]=EventValue.String("Self"); instruction.Arguments["action"]=EventValue.String(string.Empty); break;
            case "animation.queueCombo":
            case "animation.cancelAction":
            case "animation.actionPlaying":
                instruction.Arguments["target"]=EventValue.String("Self"); break;
            case "animation.triggerAction":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["action"] = EventValue.String(string.Empty);
                break;
            case "animation.setSpeed":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["speed"] =
                    EventValue.Number(
                        1.0);
                break;

            case "animation.setTransitionDuration":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["duration"] =
                    EventValue.Number(
                        0.15);
                break;

            case "animation.setLayerWeight":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["layer"] = EventValue.String(string.Empty);
                instruction.Arguments["weight"] = EventValue.Number(1.0);
                break;

            case "animation.enableLayer":
            case "animation.disableLayer":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["layer"] = EventValue.String(string.Empty);
                break;

            case "animation.currentClipIs":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["clip"] =
                    EventValue.String(
                        string.Empty);
                break;

            case "animation.currentStateIs":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["state"] =
                    EventValue.String(
                        "Idle");
                break;

            case "object.exists":
            case "object.isActive":
            case "object.destroy":
            case "audio.play":
            case "audio.pause":
            case "audio.stop":
            case "audio.isPlaying":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                break;

            case "audio.playClip":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["clipGuid"] =
                    EventValue.String(
                        string.Empty);
                instruction.Arguments["clipPath"] =
                    EventValue.String(
                        string.Empty);
                break;
            case "audio.setVolume":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["volume"] =
                    EventValue.Number(
                        1.0);
                break;

            case "audio.setPitch":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["pitch"] =
                    EventValue.Number(
                        1.0);
                break;

            case "audio.setLoop":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["loop"] =
                    EventValue.Boolean(
                        true);
                break;

            case "object.hasTag":
            case "object.doesNotHaveTag":
            case "object.addTag":
            case "object.removeTag":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["tag"] = EventValue.String(DefaultTagToken("Enemy"));
                break;

            case "object.withTagExists":
                instruction.Arguments["tag"] = EventValue.String(DefaultTagToken("Enemy"));
                break;

            case "object.isOnLayer":
            case "object.setLayer":
                instruction.Arguments["target"] = EventValue.String("Self");
                instruction.Arguments["layer"] = EventValue.Number(DefaultLayerIndex("Default"));
                break;

            case "object.setActive":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["active"] =
                    EventValue.Boolean(
                        true);
                break;

            case "object.spawnEmpty":
                instruction.Arguments["name"] =
                    EventValue.String(
                        "GameObject");

                instruction.Arguments["position"] =
                    EventValue.FromReference(
                        new VariableReference
                        {
                            Scope =
                                VariableScope.Component,

                            ComponentType =
                                "Transform",

                            MemberName =
                                "WorldPosition"
                        });
                break;

            case "object.spawnBlueprint":
                instruction.Arguments["blueprint"] =
                    EventValue.String(
                        string.Empty);

                instruction.Arguments["position"] =
                    EventValue.FromReference(
                        new VariableReference
                        {
                            Scope =
                                VariableScope.Component,

                            ComponentType =
                                "Transform",

                            MemberName =
                                "WorldPosition"
                        });
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
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["position"] =
                    EventValue.Vector3(
                        Vector3.Zero);
                break;

            case "transform.move":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["amount"] =
                    EventValue.Vector3(
                        Vector3.Zero);
                break;

            case "transform.setX":
            case "transform.setY":
            case "transform.setZ":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["value"] =
                    EventValue.Number(
                        0.0);
                break;

            case "transform.setRotation":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["rotation"] =
                    EventValue.Vector3(
                        Vector3.Zero);
                break;

            case "transform.rotateBy":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["amount"] =
                    EventValue.Vector3(
                        Vector3.Zero);
                break;

            case "transform.setScale":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
                instruction.Arguments["scale"] =
                    EventValue.Vector3(
                        Vector3.One);
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

        foreach (var argument in _registry.GetArguments(id))
            instruction.Arguments.TryAdd(argument.Name, argument.CreateDefault());
        return instruction;
    }

    // ========================================================
    // ARGUMENT UI
    // ========================================================

}
