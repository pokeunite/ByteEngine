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
    private void DrawGraphWorkspace(EditorState? state)
    {
        DrawViewSwitch();
        if (_showEventGraph) DrawGraphTabs();

        float availableWidth = ImGui.GetContentRegionAvail().X;

        /*
         * EVENTS list visibility: width driven by default (760 px), with the
         * persisted "Events" toolbar control able to force it on or off.
         */
        bool outlineModeAllows = EditorPreferences.EventOutlineMode switch
        {
            1 => true,
            2 => false,
            _ => availableWidth >= 760.0f
        };
        bool showOutline = _showSheetOutline && outlineModeAllows && availableWidth >= 640.0f;
        float outlineWidth = showOutline
            ? Math.Clamp(availableWidth * 0.22f, 160.0f, 210.0f)
            : 0.0f;

        // The centre view keeps at least 320 px; the details pane yields first.
        bool detailsAllowed = _showEventGraph || _showSheetDetails;
        float detailsWidth = MathF.Min(380.0f, MathF.Max(300.0f, availableWidth * 0.30f));
        bool showDetails = detailsAllowed &&
                           availableWidth >= 720.0f &&
                           availableWidth - outlineWidth - detailsWidth - (showOutline ? 16.0f : 8.0f) >= 320.0f;

        if (showOutline)
        {
            DrawGraphOutline(outlineWidth);
            ImGui.SameLine();
        }

        if (showDetails)
        {
            ImGui.BeginChild("##EventGraphArea",
                new Vector2(MathF.Max(320.0f, availableWidth - detailsWidth - outlineWidth -
                    (showOutline ? 16.0f : 8.0f)), 0.0f),
                ImGuiChildFlags.None,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        }
        else
        {
            ImGui.BeginChild("##EventGraphArea",
                new Vector2(0.0f, MathF.Max(220.0f, ImGui.GetContentRegionAvail().Y * 0.60f)),
                ImGuiChildFlags.None,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        }

        if (_showEventGraph)
        {
            DrawGraphCanvas(state);
        }
        else
        {
            DrawEventSheet(state);
        }

        ImGui.EndChild();

        if (showDetails)
        {
            ImGui.SameLine();
            DrawSelectionDetails(state, detailsWidth);
        }
        else
        {
            DrawSelectionDetails(state, availableWidth);
        }
    }

    /// <summary>Event Sheet / Graph switch plus the per-view options popup.</summary>
    private void DrawViewSwitch()
    {
        Vector4 active = new(.16f, .30f, .42f, 1);
        Vector4 idle = new(.10f, .12f, .14f, 1);

        ImGui.PushStyleColor(ImGuiCol.Button, !_showEventGraph ? active : idle);
        if (ImGui.Button("Event sheet"))
        {
            _showEventGraph = false;
        }

        ImGui.PopStyleColor();
        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Button, _showEventGraph ? active : idle);
        if (ImGui.Button("Graph"))
        {
            _showEventGraph = true;
        }

        ImGui.PopStyleColor();
        ImGui.SameLine();

        if (ImGui.Button("View options"))
        {
            ImGui.OpenPopup("SheetViewOptions");
        }

        if (ImGui.BeginPopup("SheetViewOptions"))
        {
            bool outline = EditorPreferences.EventOutlineMode != 2;
            if (ImGui.Checkbox("Events list", ref outline))
            {
                EditorPreferences.EventOutlineMode = outline ? 0 : 2;
            }

            ImGui.Checkbox("Details", ref _showSheetDetails);
            ImGui.Checkbox("Module settings", ref _sheetShowModuleSettings);

            bool live = VisualLogicDebugTrace.Enabled;
            if (ImGui.Checkbox("Live feedback during play", ref live))
            {
                VisualLogicDebugTrace.Enabled = live;
                if (!live)
                {
                    VisualLogicDebugTrace.Clear();
                }
            }

            ImGui.EndPopup();
        }

        ImGui.Spacing();
    }

    private void DrawGraphOutline(float width)
    {
        if (!_showEventGraph)
        {
            DrawSheetOutline(width);
            return;
        }

        ImGui.BeginChild("##EventGraphOutline", new Vector2(width, 0.0f),
            ImGuiChildFlags.Borders);
        ImGui.SeparatorText("EVENTS");
        ImGui.SetNextItemWidth(-1.0f);
        ImGui.InputTextWithHint("##OutlineSearch", "Find node...", ref _graphOutlineSearch, 96);

        if (_module != null)
        {
            foreach (EventGraphGroupDefinition group in _module.EditorGroups
                .Where(group => group.Collapsed && IsGroupVisible(group)).ToArray())
            {
                ImGui.PushID(group.Id.ToString());
                if (ImGui.Selectable("Graph: " + group.Title))
                    OpenGroupGraph(group);
                ImGui.PopID();
            }
            foreach (EventRuleDefinition rule in _module.Rules)
            {
                if (IsNodeHiddenByGroup(rule.Id) && !rule.Conditions.Concat(rule.Actions)
                    .Any(item => !IsNodeHiddenByGroup(item.InstanceId)))
                    continue;
                string name = GetRuleDisplayName(rule);
                bool eventMatches = MatchesOutlineSearch(name);
                bool childMatches = rule.Conditions.Concat(rule.Actions)
                    .Any(item => MatchesOutlineSearch(GetOutlineInstructionName(item)));
                if (!eventMatches && !childMatches)
                    continue;

                ImGui.PushID(rule.Id.ToString());
                if (ImGui.SmallButton(rule.EditorOutlineCollapsed ? ">" : "v"))
                {
                    rule.EditorOutlineCollapsed = !rule.EditorOutlineCollapsed;
                    _dirty = true;
                }
                ImGui.SameLine();
                if (!IsNodeHiddenByGroup(rule.Id))
                    DrawOutlineEntry(rule.Id, name);
                else
                    ImGui.TextDisabled(name);
                ImGui.PopID();
                if (rule.EditorOutlineCollapsed && string.IsNullOrWhiteSpace(_graphOutlineSearch))
                    continue;
                ImGui.Indent(12.0f);
                foreach (VisualInstruction condition in rule.Conditions)
                    if (!IsNodeHiddenByGroup(condition.InstanceId) &&
                        MatchesOutlineSearch(GetOutlineInstructionName(condition)))
                        DrawOutlineEntry(condition.InstanceId,
                            "? " + GetOutlineInstructionName(condition));
                foreach (VisualInstruction action in rule.Actions)
                    if (!IsNodeHiddenByGroup(action.InstanceId) &&
                        MatchesOutlineSearch(GetOutlineInstructionName(action)))
                        DrawOutlineEntry(action.InstanceId,
                            "> " + GetOutlineInstructionName(action));
                ImGui.Unindent(12.0f);
            }

            if (_module.EditorLooseConditions.Count + _module.EditorLooseActions.Count > 0)
            {
                ImGui.SeparatorText("UNCONNECTED");
                foreach (VisualInstruction item in _module.EditorLooseConditions)
                    if (!IsNodeHiddenByGroup(item.InstanceId) && MatchesOutlineSearch(GetOutlineInstructionName(item)))
                        DrawOutlineEntry(item.InstanceId,
                            "? " + GetOutlineInstructionName(item));
                foreach (VisualInstruction item in _module.EditorLooseActions)
                    if (!IsNodeHiddenByGroup(item.InstanceId) && MatchesOutlineSearch(GetOutlineInstructionName(item)))
                        DrawOutlineEntry(item.InstanceId,
                            "> " + GetOutlineInstructionName(item));
            }
        }
        ImGui.EndChild();
    }

    private string GetOutlineInstructionName(VisualInstruction instruction)
    {
        bool condition = _registry.TryGetCondition(instruction.Id, out _);
        GetInstructionPresentation(instruction, condition, out string name, out _);
        return name;
    }

    private bool MatchesOutlineSearch(string text) =>
        string.IsNullOrWhiteSpace(_graphOutlineSearch) ||
        text.Contains(_graphOutlineSearch.Trim(), StringComparison.OrdinalIgnoreCase);

    private void DrawOutlineEntry(Guid nodeId, string label)
    {
        ImGui.PushID(nodeId.ToString());
        if (ImGui.Selectable(label, _selectedGraphNodes.Contains(nodeId)))
        {
            EventRuleDefinition? owner = _module?.Rules.FirstOrDefault(rule =>
                rule.EditorCollapsed &&
                (rule.Conditions.Any(item => item.InstanceId == nodeId) ||
                 rule.Actions.Any(item => item.InstanceId == nodeId)));
            if (owner != null)
            {
                RecordHistory("Expand Event");
                owner.EditorCollapsed = false;
                _dirty = true;
            }
            _selectedGraphNodes.Clear();
            _selectedGraphNodes.Add(nodeId);
            _requestFrameNodeId = nodeId;
        }
        ImGui.PopID();
    }

    private void DrawSelectionDetails(EditorState? state, float width)
    {
        ImGui.BeginChild("##EventSelectionDetails",
            new Vector2(width, 0.0f),
            ImGuiChildFlags.Borders);
        ImGui.SeparatorText("DETAILS");

        if (_module == null || _selectedGraphNodes.Count != 1)
        {
            EditorUi.EmptyState("Select a node", "Click a condition or action to edit its values.");
            ImGui.EndChild();
            return;
        }

        Guid selectedId = _selectedGraphNodes.First();
        foreach (EventRuleDefinition rule in _module.Rules)
        {
            if (rule.Id == selectedId)
            {
                ImGui.TextWrapped(GetRuleDisplayName(rule));
                ImGui.TextDisabled($"{rule.Conditions.Count} conditions | {rule.Actions.Count} actions");
                ImGui.EndChild();
                return;
            }

            VisualInstruction? instruction = rule.Conditions
                .Concat(rule.Actions)
                .FirstOrDefault(item => item.InstanceId == selectedId);
            if (instruction == null)
                continue;

            bool condition = rule.Conditions.Contains(instruction);
            GetInstructionPresentation(instruction, condition, out string name, out string category);
            ImGui.TextWrapped(name);
            ImGui.TextDisabled($"{(condition ? "Condition" : "Action")} / {category}");
            ImGui.Spacing();
            ImGui.TextWrapped(GetInstructionDescription(instruction, condition));
            ImGui.Separator();
            ImGui.PushID(instruction.InstanceId.ToString());
            DrawInstructionArguments(instruction, state);
            ImGui.PopID();
            ImGui.EndChild();
            return;
        }

        VisualInstruction? looseCondition = _module.EditorLooseConditions
            .FirstOrDefault(item => item.InstanceId == selectedId);
        VisualInstruction? looseAction = _module.EditorLooseActions
            .FirstOrDefault(item => item.InstanceId == selectedId);
        VisualInstruction? loose = looseCondition ?? looseAction;
        if (loose != null)
        {
            bool condition = looseCondition != null;
            GetInstructionPresentation(loose, condition, out string name, out string category);
            ImGui.TextWrapped(name);
            ImGui.TextDisabled($"Unconnected {(condition ? "Condition" : "Action")} / {category}");
            ImGui.TextWrapped("This draft is saved but will not execute until added to an event.");
            if (_module.Rules.Count > 0 && ImGui.BeginCombo(
                    "Add to Event", "Choose event..."))
            {
                foreach ((EventRuleDefinition rule, string label) in GetRuleMenuEntries())
                {
                    if (!ImGui.Selectable(label))
                        continue;

                    RecordHistory("Connect Draft Node To Event");
                    if (condition)
                    {
                        AttachLooseConditionToRule(rule, loose);
                        ConnectCondition(rule, loose.InstanceId);
                    }
                    else
                    {
                        AttachLooseActionToRule(rule, loose);
                        AppendDraftActionChain(rule, loose);
                    }
                    _dirty = true;
                    break;
                }
                ImGui.EndCombo();
            }
            ImGui.Separator();
            ImGui.PushID(loose.InstanceId.ToString());
            DrawInstructionArguments(loose, state);
            ImGui.PopID();
            ImGui.EndChild();
            return;
        }

        ImGui.TextDisabled("The selected node is no longer available.");
        ImGui.EndChild();
    }

    private static string EventsOutlineLabel() => EditorPreferences.EventOutlineMode switch
    {
        1 => "Events On",
        2 => "Events Off",
        _ => "Events Auto"
    };

    private static void CycleEventsOutlineMode() =>
        EditorPreferences.EventOutlineMode = (EditorPreferences.EventOutlineMode + 1) % 3;

    private void DrawDocumentToolbar(
        EditorLog log)
    {
        ByteGraphToolbarLayout layout = ByteGraphToolbarLayout.ForWidth(ImGui.GetContentRegionAvail().X);
        EditorUi.BeginToolbar("##EventDocumentToolbar");

        ImGui.BeginDisabled(!_dirty);
        if (_dirty)
            EditorUi.PrimaryButton("Save", () => Save(log));
        else
            EditorUi.ToolbarButton("Save", "Save Event Sheet");
        ImGui.EndDisabled();
        EditorUi.ToolbarSeparator();
        ImGui.BeginDisabled(!CanUndo);
        if (EditorUi.ToolbarButton("Undo", UndoName == null ? "Undo" : $"Undo {UndoName}")) Undo();
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(!CanRedo);
        if (EditorUi.ToolbarButton("Redo", RedoName == null ? "Redo" : $"Redo {RedoName}")) Redo();
        ImGui.EndDisabled();

        EditorUi.ToolbarSeparator();
        if (EditorUi.PrimaryButton("+ Event"))
            RequestAddEvent(GetDefaultRulePosition(_module!.Rules.Count));
        ImGui.SameLine();
        ImGui.BeginDisabled(_selectedGraphNodes.Count == 0);
        if (EditorUi.ToolbarButton("+ Comment", "Create a comment around the selection"))
            CreateGroupFromSelection();
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(_selectedGraphNodes.Count < 2);
        if (EditorUi.ToolbarButton("Collapse Selection", "Fold selected nodes into an expandable graph box"))
            CollapseSelectedGraphNodes();
        ImGui.EndDisabled();

        if (!layout.CollapseSecondary && _showEventGraph)
        {
            EditorUi.ToolbarSeparator();
            if (EditorUi.ToolbarButton("Arrange", "Auto-arrange graph"))
            {
                RecordHistory("Auto Arrange Graph");
                AutoArrangeGraph();
                _requestFrameGraph = true;
                _dirty = true;
            }
            ImGui.SameLine();
            if (EditorUi.ToolbarButton("Frame", "Frame graph")) _requestFrameGraph = true;
        }

        if (!layout.CollapseSecondary)
        {
            ImGui.SameLine();
            if (EditorUi.ToolbarButton(EventsOutlineLabel(),
                    "EVENTS list: auto (760 px) / always (needs 640 px) / never. Persisted per user."))
                CycleEventsOutlineMode();
        }

        if (_showEventGraph)
        {
            EditorUi.ToolbarSeparator();
            if (EditorUi.ToolbarButton("-", "Zoom out")) _graphCanvas.SetZoom(_graphCanvas.Zoom - .1f);
            ImGui.SameLine();
            ImGui.TextColored(EditorTheme.TextSecondary, $"{_graphCanvas.Zoom * 100f:0}%");
            ImGui.SameLine();
            if (EditorUi.ToolbarButton("+", "Zoom in")) _graphCanvas.SetZoom(_graphCanvas.Zoom + .1f);

            ImGui.SameLine();
            if (EditorUi.ToolbarButton("View", "Graph view settings")) _viewSettings.Begin(GetNodeScale());
            if (_viewSettings.ConsumeOpenRequest()) ImGui.OpenPopup("View##ByteGraphViewSettings");
            if (ImGui.BeginPopup("View##ByteGraphViewSettings"))
            {
                _viewSettings.MarkVisible();
                float nodeScalePercent = _viewSettings.NodeScale * 100f;
                ImGui.SetNextItemWidth(190f);
                if (ImGui.SliderFloat("Node Scale##ByteGraphView", ref nodeScalePercent,
                        ByteGraphViewSettingsState.MinimumNodeScale * 100f,
                        ByteGraphViewSettingsState.MaximumNodeScale * 100f, "%.0f%%"))
                {
                    _viewSettings.SetNodeScale(nodeScalePercent / 100f);
                    _module!.EditorNodeScale = _viewSettings.NodeScale;
                    _dirty = true;
                }
                if (EditorUi.SecondaryButton("Reset View")) _graphCanvas.ResetView();
                ImGui.EndPopup();
            }
            else
            {
                _viewSettings.RecoverWhenNotVisible();
            }
        }

        ImGui.SameLine();
        if (EditorUi.ToolbarButton("...", "More graph actions")) ImGui.OpenPopup("More##ByteGraphToolbarMore");
        if (ImGui.BeginPopup("More##ByteGraphToolbarMore"))
        {
            if (layout.CollapseSecondary && _showEventGraph && ImGui.MenuItem("Arrange"))
            {
                RecordHistory("Auto Arrange Graph");
                AutoArrangeGraph();
                _requestFrameGraph = true;
                _dirty = true;
            }
            if (layout.CollapseSecondary && _showEventGraph && ImGui.MenuItem("Frame")) _requestFrameGraph = true;
            if (layout.CollapseSecondary) ImGui.Separator();
            if (ImGui.MenuItem("Events: Auto", "", EditorPreferences.EventOutlineMode == 0))
                EditorPreferences.EventOutlineMode = 0;
            if (ImGui.MenuItem("Events: Always", "", EditorPreferences.EventOutlineMode == 1))
                EditorPreferences.EventOutlineMode = 1;
            if (ImGui.MenuItem("Events: Never", "", EditorPreferences.EventOutlineMode == 2))
                EditorPreferences.EventOutlineMode = 2;
            ImGui.Separator();
            if (ImGui.MenuItem("Reset View")) _graphCanvas.ResetView();
            ImGui.BeginDisabled(_selectedGraphNodes.Count == 0);
            if (ImGui.MenuItem("Delete")) DeleteSelectedGraphNodes();
            ImGui.EndDisabled();
            ImGui.EndPopup();
        }

        if (_dirty)
        {
            ImGui.SameLine();
            EditorUi.StatusBadge("UNSAVED", EditorStatusKind.Warning);
        }

        EditorUi.EndToolbar();
    }

    private static void DrawTraceChip(string label, VisualLogicTraceState state)
    {
        ImGui.TextColored(GetTraceColor(state), $"â— {label}");
    }

    private static void DrawTraceLegend()
    {
        bool compact = ImGui.GetContentRegionAvail().X < 760.0f;
        EditorUi.BeginToolbar("##EventTraceToolbar", compact ? 60.0f : EditorTheme.ToolbarHeight);
        ImGui.TextColored(EditorTheme.TextSecondary, "LIVE TRACE");
        ImGui.SameLine();
        bool liveTrace = VisualLogicDebugTrace.Enabled;
        if (ImGui.Checkbox("##ByteGraphLiveTrace", ref liveTrace))
        {
            VisualLogicDebugTrace.Enabled = liveTrace;
            if (!liveTrace) VisualLogicDebugTrace.Clear();
        }
        EditorUi.Tooltip("Show live Visual Logic execution states");
        ImGui.SameLine();
        DrawTraceChip("TRUE", VisualLogicTraceState.ConditionTrue);
        ImGui.SameLine();
        DrawTraceChip("FIRED", VisualLogicTraceState.EventTriggered);
        if (compact) ImGui.NewLine();
        else ImGui.SameLine();
        DrawTraceChip("FALSE", VisualLogicTraceState.ConditionFalse);
        ImGui.SameLine();
        DrawTraceChip("BLOCKED", VisualLogicTraceState.EventBlocked);
        ImGui.SameLine();
        DrawTraceChip("ACTION", VisualLogicTraceState.ActionExecuted);
        ImGui.SameLine();
        DrawTraceChip("SKIPPED", VisualLogicTraceState.ActionSkipped);
        EditorUi.EndToolbar();
    }

    private bool CanAutoSave()
    {
        if (_wireDragKind !=
                WireDragKind.None ||
            _draggingGraphNodeId !=
                Guid.Empty ||
            _marqueeSelecting || _draggingGroupId != Guid.Empty)
        {
            return false;
        }

        if (_eventNamePopup.IsOpen || _viewSettings.IsOpen)
        {
            return false;
        }

        if (ImGui.IsMouseDown(
                ImGuiMouseButton.Left) ||
            ImGui.IsMouseDown(
                ImGuiMouseButton.Middle) ||
            ImGui.IsMouseDown(
                ImGuiMouseButton.Right))
        {
            return false;
        }

        if (ImGui.IsAnyItemActive())
        {
            return false;
        }

        return true;
    }

    private void RegisterDocument(EditorLog log)
    {
        if (_asset == null || _module == null) return;
        EditorDocumentId id = new(EditorDocumentType.EventSheet, _asset.Guid.ToString("N"));
        _registeredDocumentId = id;
        _documents.RegisterOrFocus(new EditorDocument(
            id,
            $"Event Sheet: {_module.Name}",
            () => { _open = true; _requestFocus = true; },
            RequestClose,
            () => { Save(log, true); return !_dirty; },
            DiscardChanges,
            () => _dirty));
    }

    private void RequestClose()
    {
        if (_dirty) _openUnsavedPopup = true;
        else CloseNow();
    }

    private void CloseNow()
    {
        _open = false;
        _eventNamePopup.Reset();
        _viewSettings.Reset();
        EventWorkspaceUndoRouter.ClearFocused(this);
        if (_registeredDocumentId.HasValue)
        {
            _documents.Unregister(_registeredDocumentId.Value);
            _registeredDocumentId = null;
        }
    }

    private void DiscardChanges()
    {
        if (_asset == null) return;
        _module = _serializer.Load(_asset.FullPath);
        InitializeMissingGraphLayout();
        _history.Reset(_module);
        _dirty = false;
    }

    private void DrawUnsavedPopup(EditorLog log)
    {
        if (_openUnsavedPopup)
        {
            ImGui.OpenPopup("Unsaved Event Sheet");
            _openUnsavedPopup = false;
        }
        bool open = true;
        if (!ImGui.BeginPopupModal("Unsaved Event Sheet", ref open,
                ImGuiWindowFlags.AlwaysAutoResize)) return;
        ImGui.TextColored(EditorTheme.Text, "The Event Sheet has unsaved changes.");
        EditorUi.MutedText("Save before closing?");
        ImGui.Spacing();
        if (EditorUi.PrimaryButton("Save"))
        {
            Save(log, true);
            if (!_dirty) { CloseNow(); ImGui.CloseCurrentPopup(); }
        }
        ImGui.SameLine();
        if (EditorUi.DestructiveButton("Discard"))
        {
            DiscardChanges();
            CloseNow();
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (EditorUi.SecondaryButton("Cancel")) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }

    private void Save(
        EditorLog log,
        bool logSuccess = true)
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

            int hotReloaded =
                EventModuleLiveReload.Apply(
                    _asset,
                    log);

            if (logSuccess)
            {
                string runtimeSuffix =
                    hotReloaded >
                    0
                        ? $" | hot reloaded {hotReloaded} runtime instance(s)"
                        : string.Empty;

                log.Info(
                    $"Saved Event Module '{_asset.ProjectPath}'{runtimeSuffix}.");
            }
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
        if (_module == null || !EditorUi.SectionHeader("Module Settings", defaultOpen: false))
            return;

        string name = _module.Name;
        EditorUi.PropertyRow("Name", () =>
        {
            if (ImGui.InputText("##EventModuleName", ref name, 128))
            {
                _module.Name = name;
                _dirty = true;
            }
        });
        EditorUi.LabelValue("Target Blueprint",
            _module.TargetBlueprintGuid?.ToString() ?? "Generic");
        EditorUi.LabelValue("Required Components",
            _module.RequiredComponents.Count == 0
                ? "None"
                : string.Join(", ", _module.RequiredComponents));

        if (EditorUi.SectionHeader("Module Advanced", defaultOpen: false))
        {
            EditorUi.LabelValue("Asset ID", _module.Id.ToString());
            EditorUi.LabelValue("Format Version", _module.Version.ToString());
        }
    }


    // ========================================================
    // BYTEGRAPH
    // ========================================================

    private static readonly Vector2 BaseEventGraphNodeSize =
        new(
            300.0f,
            205.0f);

    private static readonly Vector4 ConditionWireColor =
        new(
            0.25f,
            0.72f,
            1.0f,
            1.0f);

    private static readonly Vector4 ExecutionWireColor =
        new(
            1.0f,
            0.66f,
            0.22f,
            1.0f);

    private static readonly Vector4 TrueExecutionWireColor =
        new(
            0.30f,
            0.95f,
            0.46f,
            1.0f);

    private static readonly Vector4 FalseExecutionWireColor =
        new(
            0.95f,
            0.30f,
            0.30f,
            1.0f);

    private static readonly Vector4 SelectionColor =
        new(
            1.0f,
            0.86f,
            0.28f,
            1.0f);

    private float GetNodeScale()
    {
        if (_module ==
            null)
        {
            return 0.72f;
        }

        if (_module.EditorNodeScale <
                0.55f ||
            _module.EditorNodeScale >
                1.15f)
        {
            _module.EditorNodeScale =
                0.72f;
        }

        return _module.EditorNodeScale;
    }

    private Vector2 GetEventGraphNodeSize(
        Guid? ruleId = null)
    {
        float layoutScale =
            GetNodeLayoutScale();

        Vector2 logicalSize =
            BaseEventGraphNodeSize *
            layoutScale;

        if (ruleId.HasValue &&
            _measuredEventNodeLogicalHeights.TryGetValue(
                ruleId.Value,
                out float measuredHeight))
        {
            // Measured content height is authoritative. The old code only
            // allowed nodes to grow beyond hard-coded estimates, which meant
            // stale/undersized estimates could still clip dynamic content.
            logicalSize.Y =
                MathF.Max(
                    measuredHeight,
                    120.0f *
                    layoutScale);
        }

        return logicalSize;
    }

    private Vector2 GetScaledInstructionNodeSize(
        VisualInstruction instruction)
    {
        float layoutScale =
            GetNodeLayoutScale();

        Vector2 logicalSize =
            new Vector2(320.0f, 145.0f) * layoutScale;

        if (_measuredInstructionNodeLogicalHeights.TryGetValue(
                instruction.InstanceId,
                out float measuredHeight))
        {
            // Once rendered, the node's real content height replaces the
            // per-id estimate. This keeps every current and future action /
            // condition node aligned with what it actually draws.
            logicalSize.Y =
                MathF.Max(
                    measuredHeight,
                    96.0f *
                    layoutScale);
        }

        return logicalSize;
    }

    private float GetNodeLayoutScale()
    {
        float zoom =
            MathF.Max(
                _graphCanvas.Zoom,
                0.01f);

        return GetNodeVisualScale() /
               zoom;
    }

    private float GetNodeVisualScale()
    {
        float scale = _graphCanvas.Zoom * GetNodeScale();
        // Keep node copy readable at the normal graph view without enlarging
        // node geometry or changing saved positions and wire endpoints.
        if (_graphCanvas.Zoom >= 0.85f)
            scale = MathF.Max(scale, 0.88f);
        return Math.Clamp(
            scale,
            0.25f,
            1.65f);
    }

    private VisualLogicTraceState GetLiveTraceState(
        Guid nodeId)
    {
        if (_module ==
                null ||
            !VisualLogicDebugTrace.Enabled)
        {
            return VisualLogicTraceState.None;
        }

        EditorState? state =
            EditorState.Active;

        if (state ==
                null ||
            state.Mode ==
                EditorMode.Edit)
        {
            return VisualLogicTraceState.None;
        }

        /*
         * During Play mode, states pulse briefly.
         * When paused, keep the last observed state visible so the graph can
         * be inspected without the trace disappearing immediately.
         */
        double windowSeconds =
            state.Mode ==
                EditorMode.Paused
                ? 60.0
                : 0.22;

        return VisualLogicDebugTrace.TryGetRecentState(
                _module.Id,
                nodeId,
                out VisualLogicTraceState traceState,
                windowSeconds)
            ? traceState
            : VisualLogicTraceState.None;
    }

    private static Vector4 GetTraceColor(
        VisualLogicTraceState state)
    {
        return state switch
        {
            VisualLogicTraceState.ConditionTrue or
            VisualLogicTraceState.EventTriggered =>
                new Vector4(
                    0.28f,
                    1.0f,
                    0.48f,
                    1.0f),

            VisualLogicTraceState.ConditionFalse or
            VisualLogicTraceState.EventBlocked or
            VisualLogicTraceState.ActionFailed =>
                new Vector4(
                    1.0f,
                    0.28f,
                    0.28f,
                    1.0f),

            VisualLogicTraceState.ActionExecuted =>
                new Vector4(
                    1.0f,
                    0.70f,
                    0.20f,
                    1.0f),

            VisualLogicTraceState.ActionSkipped =>
                new Vector4(
                    0.58f,
                    0.62f,
                    0.68f,
                    1.0f),

            _ =>
                new Vector4(
                    0.65f,
                    0.65f,
                    0.65f,
                    1.0f)
        };
    }

    private static Vector4 GetTraceBackgroundColor(
        VisualLogicTraceState state,
        Vector4 fallback)
    {
        return state switch
        {
            VisualLogicTraceState.ConditionTrue or
            VisualLogicTraceState.EventTriggered =>
                new Vector4(
                    0.08f,
                    0.24f,
                    0.13f,
                    0.99f),

            VisualLogicTraceState.ConditionFalse or
            VisualLogicTraceState.EventBlocked or
            VisualLogicTraceState.ActionFailed =>
                new Vector4(
                    0.26f,
                    0.075f,
                    0.075f,
                    0.99f),

            VisualLogicTraceState.ActionExecuted =>
                new Vector4(
                    0.27f,
                    0.17f,
                    0.055f,
                    0.99f),

            VisualLogicTraceState.ActionSkipped =>
                new Vector4(
                    0.12f,
                    0.13f,
                    0.15f,
                    0.99f),

            _ =>
                fallback
        };
    }

    private static string GetTraceLabel(
        VisualLogicTraceState state)
    {
        return state switch
        {
            VisualLogicTraceState.ConditionTrue =>
                "TRUE",

            VisualLogicTraceState.ConditionFalse =>
                "FALSE",

            VisualLogicTraceState.EventTriggered =>
                "FIRED",

            VisualLogicTraceState.EventBlocked =>
                "BLOCKED",

            VisualLogicTraceState.ActionExecuted =>
                "EXECUTED",

            VisualLogicTraceState.ActionSkipped =>
                "SKIPPED",

            VisualLogicTraceState.ActionFailed =>
                "FAILED",

            _ =>
                string.Empty
        };
    }

    private void PushGraphNodeStyle()
    {
        float scale =
            GetNodeVisualScale();

        ImGui.PushStyleVar(
            ImGuiStyleVar.ItemSpacing,
            new Vector2(
                8.0f,
                7.0f) *
            scale);

        ImGui.PushStyleVar(
            ImGuiStyleVar.FramePadding,
            new Vector2(
                6.0f,
                4.0f) *
            scale);
    }

    private static void PopGraphNodeStyle()
    {
        ImGui.PopStyleVar(
            2);
    }

    private void DrawGraphCanvas(
        EditorState? state)
    {
        if (_module ==
            null)
        {
            return;
        }

        InitializeMissingGraphLayout();

        _anyGraphNodeHovered =
            false;

        if (!ImGui.IsMouseDown(
                ImGuiMouseButton.Left))
        {
            _dragHistoryNodeId =
                Guid.Empty;
        }

        bool visible =
            _graphCanvas.Begin(
                "##ByteGraphCanvas");

        if (!visible)
        {
            _graphCanvas.End();
            return;
        }

        if (_requestFrameGraph)
        {
            FrameEntireGraph();

            _requestFrameGraph =
                false;
        }
        if (_requestFrameNodeId != Guid.Empty)
        {
            if (TryGetNodeBounds(_requestFrameNodeId, out Vector2 minimum,
                    out Vector2 maximum))
                _graphCanvas.FrameBounds(minimum - new Vector2(80.0f),
                    maximum + new Vector2(80.0f));
            _requestFrameNodeId = Guid.Empty;
        }

        UpdateGroupPointerInteraction();
        UpdateGraphPointerInteraction();
        HandleGraphShortcuts();

        DrawGraphGroupBackgrounds();
        DrawGraphWires();
        DrawWireDragPreview();

        if (_module.Rules.Count == 0 &&
            _module.EditorLooseConditions.Count == 0 &&
            _module.EditorLooseActions.Count == 0)
        {
            ImGui.SetCursorScreenPos(_graphCanvas.ToScreen(new Vector2(80.0f, 80.0f)));
            ImGui.BeginChild("##EmptyGraphHint", new Vector2(360.0f, 112.0f),
                ImGuiChildFlags.Borders, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            EditorUi.EmptyState("No events yet", "Add an Event to begin.");
            if (EditorUi.PrimaryButton("+ Event"))
                RequestAddEvent(GetDefaultRulePosition(0));
            ImGui.EndChild();
        }
        else
        {
            for (int ruleIndex = 0;
                 ruleIndex < _module.Rules.Count;
                 ruleIndex++)
            {
                EventRuleDefinition rule =
                    _module.Rules[ruleIndex];

                ImGui.PushID(
                    rule.Id.ToString());

                bool deleteRule = false;
                if (!IsNodeHiddenByGroup(rule.Id))
                    deleteRule = DrawEventGraphNode(rule, ruleIndex);

                if (!rule.EditorCollapsed || ActiveGraphGroup != null)
                {
                    DrawConditionGraphNodes(
                        rule,
                        state);

                    DrawActionGraphNodes(
                        rule,
                        state);
                }

                ImGui.PopID();

                if (deleteRule)
                {
                    RecordHistory(
                        "Delete Event");

                    RemoveNodeFromGroups(
                        rule.Id);

                    foreach (VisualInstruction instruction
                             in rule.Conditions.Concat(
                                 rule.Actions))
                    {
                        RemoveNodeFromGroups(
                            instruction.InstanceId);
                    }

                    _selectedGraphNodes.Remove(
                        rule.Id);

                    _module.Rules.RemoveAt(
                        ruleIndex);

                    _dirty =
                        true;

                    ruleIndex--;
                }
            }
        }

        DrawLooseGraphNodes(state);

        DrawGraphGroupHeaders();

        HandleGraphMarquee();
        FinishWireDrag();
        HandleGraphBackgroundContext();
        DrawWireCreatePopup();
        DrawGraphContextPopup();

        _graphCanvas.End();
    }

    private bool DrawEventGraphNode(
        EventRuleDefinition rule,
        int ruleIndex)
    {
        Vector2 logicalPosition =
            new(
                rule.EditorX,
                rule.EditorY);

        Vector2 screenPosition =
            _graphCanvas.ToScreen(
                logicalPosition);

        Vector2 logicalSize =
            GetEventGraphNodeSize(
                rule.Id);

        Vector2 screenSize =
            _graphCanvas.ScaleSize(
                logicalSize);

        ImGui.SetCursorScreenPos(
            screenPosition);

        bool selected =
            _selectedGraphNodes.Contains(
                rule.Id);

        VisualLogicTraceState liveState =
            GetLiveTraceState(
                rule.Id);

        Vector4 defaultEventBackground = EditorTheme.BackgroundRaised;

        ImGui.PushStyleColor(
            ImGuiCol.ChildBg,
            GetTraceBackgroundColor(
                liveState,
                defaultEventBackground));

        ImGui.PushStyleColor(
            ImGuiCol.Border,
            selected
                ? SelectionColor
                : liveState !=
                    VisualLogicTraceState.None
                    ? GetTraceColor(
                        liveState)
                    : rule.Enabled
                        ? new Vector4(
                            0.23f,
                            0.52f,
                            0.88f,
                            1.0f)
                        : new Vector4(
                            0.33f,
                            0.35f,
                            0.39f,
                            1.0f));

        PushGraphNodeStyle();

        bool visible =
            ImGui.BeginChild(
                $"EventGraphNode##{rule.Id}",
                new Vector2(
                    screenSize.X,
                    0.0f),
                ImGuiChildFlags.Borders |
                ImGuiChildFlags.AutoResizeY,
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse);

        bool nodeHovered =
            ImGui.IsWindowHovered(
                ImGuiHoveredFlags.RootAndChildWindows);

        _anyGraphNodeHovered |=
            nodeHovered;

        bool remove =
            false;

        if (visible)
        {
            ImGui.SetWindowFontScale(
                GetNodeVisualScale());

            ImGui.TextColored(
                new Vector4(
                    0.45f,
                    0.72f,
                    1.0f,
                    1.0f),
                $"EVENT {ruleIndex + 1:00}");

            if (liveState !=
                VisualLogicTraceState.None)
            {
                ImGui.SameLine();

                ImGui.TextColored(
                    GetTraceColor(
                        liveState),
                    GetTraceLabel(
                        liveState));
            }

            ImGui.SameLine();

            bool enabled =
                rule.Enabled;

            if (ImGui.Checkbox(
                    "##Enabled",
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

            if (ImGui.SmallButton(
                    rule.EditorCollapsed
                        ? "Expand"
                        : "Collapse"))
            {
                RecordHistory(
                    rule.EditorCollapsed
                        ? "Expand Event"
                        : "Collapse Event");

                rule.EditorCollapsed =
                    !rule.EditorCollapsed;

                _dirty =
                    true;
            }

            ImGui.SameLine();

            if (ImGui.SmallButton(
                    "X##DeleteEvent"))
            {
                remove =
                    true;
            }

            string title = GetRuleDisplayName(rule);

            ImGui.SetNextItemWidth(
                -1.0f);

            if (ImGui.InputText(
                    "##EventTitle",
                    ref title,
                    96))
            {
                rule.DisplayName = string.IsNullOrWhiteSpace(title)
                    ? $"Event {ruleIndex + 1}"
                    : title;
                rule.EditorTitle = rule.DisplayName;

                _dirty =
                    true;
            }

            ImGui.TextDisabled(
                $"{rule.Conditions.Count} condition(s)  |  {rule.Actions.Count} action(s)");

            if (rule.Conditions.Count ==
                0)
            {
                ImGui.TextColored(
                    new Vector4(
                        1.0f,
                        0.72f,
                        0.24f,
                        1.0f),
                    "Runs every frame because no conditions are attached.");
            }
            else
            {
                ImGui.TextDisabled(
                    "Runs when ALL connected conditions are TRUE.");
            }

            if (ImGui.Button(
                    "+ Condition"))
            {
                ImGui.OpenPopup(
                    "Add Condition");
            }

            ImGui.SameLine();

            if (ImGui.Button(
                    "+ Action"))
            {
                ImGui.OpenPopup(
                    "Add Action");
            }

            DrawConditionPicker(
                rule,
                rule.Conditions);

            DrawActionPicker(
                rule,
                rule.Actions);

            ImGui.Separator();

            ImGui.Selectable(
                "Drag / Select Event##MoveHandle",
                false,
                ImGuiSelectableFlags.None,
                new Vector2(
                    -1.0f,
                    24.0f *
                    GetNodeVisualScale()));

            float measuredLogicalHeight =
                (ImGui.GetCursorPosY() +
                 ImGui.GetStyle().WindowPadding.Y) /
                MathF.Max(
                    _graphCanvas.Zoom,
                    0.01f);

            if (!_measuredEventNodeLogicalHeights.TryGetValue(
                    rule.Id,
                    out float cachedHeight) ||
                MathF.Abs(
                    cachedHeight -
                    measuredLogicalHeight) >
                1.0f)
            {
                _measuredEventNodeLogicalHeights[rule.Id] =
                    measuredLogicalHeight;
            }
        }

        ImGui.EndChild();

        PopGraphNodeStyle();

        ImGui.PopStyleColor(
            2);

        Vector2 conditionPin =
            GetEventConditionInput(
                rule);

        Vector2 executionPin =
            GetEventExecutionOutput(
                rule);

        _graphCanvas.DrawPin(
            conditionPin,
            ConditionWireColor,
            7.0f);

        _graphCanvas.DrawPin(
            executionPin,
            ExecutionWireColor,
            7.0f);

        if (ImGui.IsMouseClicked(
                ImGuiMouseButton.Right) &&
            _graphCanvas.IsPointHovered(
                conditionPin,
                13.0f))
        {
            RecordHistory(
                "Disconnect Conditions");

            rule.HasExplicitConditionFlow =
                true;

            rule.ConnectedConditionIds.Clear();

            _dirty =
                true;
        }
        else if (ImGui.IsMouseClicked(
                ImGuiMouseButton.Right) &&
            _graphCanvas.IsPointHovered(
                executionPin,
                13.0f))
        {
            RecordHistory(
                "Disconnect Execution Wire");

            rule.HasExplicitExecutionFlow =
                true;

            rule.FirstActionId =
                null;

            _dirty =
                true;
        }

        TryStartWireDrag(
            rule,
            WireDragKind.Condition,
            Guid.Empty,
            conditionPin);

        TryStartWireDrag(
            rule,
            WireDragKind.Action,
            Guid.Empty,
            executionPin);

        return remove;
    }

    private void DrawConditionGraphNodes(
        EventRuleDefinition rule,
        EditorState? state)
    {
        for (int index = 0;
             index < rule.Conditions.Count;
             index++)
        {
            VisualInstruction instruction =
                rule.Conditions[index];
            if (IsNodeHiddenByGroup(instruction.InstanceId))
                continue;

            ImGui.PushID(
                instruction.InstanceId.ToString());
            bool remove =
                DrawInstructionGraphNode(
                    rule,
                    instruction,
                    true,
                    index,
                    state);

            ImGui.PopID();

            if (remove)
            {
                RecordHistory(
                    "Delete Condition");

                RemoveNodeFromGroups(
                    instruction.InstanceId);

                _selectedGraphNodes.Remove(
                    instruction.InstanceId);

                DisconnectConditionEverywhere(
                    rule,
                    instruction.InstanceId);

                rule.Conditions.RemoveAt(
                    index);

                _dirty =
                    true;

                index--;
            }
        }
    }

    private void DrawActionGraphNodes(
        EventRuleDefinition rule,
        EditorState? state)
    {
        for (int index = 0;
             index < rule.Actions.Count;
             index++)
        {
            VisualInstruction instruction =
                rule.Actions[index];
            if (IsNodeHiddenByGroup(instruction.InstanceId))
                continue;

            ImGui.PushID(
                instruction.InstanceId.ToString());

            bool remove =
                DrawInstructionGraphNode(
                    rule,
                    instruction,
                    false,
                    index,
                    state);

            ImGui.PopID();

            if (remove)
            {
                RecordHistory(
                    "Delete Action");

                RemoveNodeFromGroups(
                    instruction.InstanceId);

                _selectedGraphNodes.Remove(
                    instruction.InstanceId);

                RemoveActionFromExecutionFlow(
                    rule,
                    instruction);

                rule.Actions.RemoveAt(
                    index);

                _dirty =
                    true;

                index--;
            }
        }
    }

    private bool DrawInstructionGraphNode(
        EventRuleDefinition rule,
        VisualInstruction instruction,
        bool condition,
        int index,
        EditorState? state,
        bool loose = false)
    {
        GetInstructionPresentation(
            instruction,
            condition,
            out string displayName,
            out string category);

        Vector2 logicalSize =
            GetScaledInstructionNodeSize(
                instruction);

        Vector2 logicalPosition =
            new(
                instruction.EditorX,
                instruction.EditorY);

        ImGui.SetCursorScreenPos(
            _graphCanvas.ToScreen(
                logicalPosition));

        bool selected =
            _selectedGraphNodes.Contains(
                instruction.InstanceId);

        VisualLogicTraceState liveState =
            GetLiveTraceState(
                instruction.InstanceId);

        Vector4 defaultInstructionBackground = condition
            ? EditorTheme.PanelRaised
            : EditorTheme.BackgroundRaised;

        ImGui.PushStyleColor(
            ImGuiCol.ChildBg,
            GetTraceBackgroundColor(
                liveState,
                defaultInstructionBackground));

        ImGui.PushStyleColor(
            ImGuiCol.Border,
            selected
                ? SelectionColor
                : liveState !=
                    VisualLogicTraceState.None
                    ? GetTraceColor(
                        liveState)
                    : condition
                        ? new Vector4(
                            0.20f,
                            0.62f,
                            0.88f,
                            1.0f)
                        : new Vector4(
                            0.95f,
                            0.57f,
                            0.16f,
                            1.0f));

        PushGraphNodeStyle();

        Vector2 requestedScreenSize =
            _graphCanvas.ScaleSize(
                logicalSize);

        bool visible =
            ImGui.BeginChild(
                $"InstructionGraphNode##{instruction.InstanceId}",
                new Vector2(
                    requestedScreenSize.X,
                    0.0f),
                ImGuiChildFlags.Borders |
                ImGuiChildFlags.AutoResizeY,
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse);

        bool nodeHovered =
            ImGui.IsWindowHovered(
                ImGuiHoveredFlags.RootAndChildWindows);

        _anyGraphNodeHovered |=
            nodeHovered;

        bool remove =
            false;

        if (visible)
        {
            // The instruction's name is the node title, not a detail hidden
            // below the generic Condition/Action label. Selection and dragging
            // use node bounds, so wrapped text remains a usable drag region.
            float bodyScale = GetNodeVisualScale();
            ImGui.SetWindowFontScale(MathF.Max(bodyScale, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.94f, 0.96f, 1.0f, 1.0f));
            ImGui.TextWrapped(displayName);
            ImGui.PopStyleColor();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{(condition ? "Condition" : "Action")}: {displayName}");

            ImGui.SetWindowFontScale(
                GetNodeVisualScale());

            ImGui.TextColored(
                condition
                    ? new Vector4(
                        0.35f,
                        0.78f,
                        1.0f,
                        1.0f)
                    : new Vector4(
                        1.0f,
                        0.68f,
                        0.25f,
                        1.0f),
                condition
                    ? loose ? "UNCONNECTED CONDITION" : $"CONDITION {index + 1:00}"
                    : loose ? "UNCONNECTED ACTION" : "ACTION");

            if (liveState !=
                VisualLogicTraceState.None)
            {
                ImGui.SameLine();

                ImGui.TextColored(
                    GetTraceColor(
                        liveState),
                    GetTraceLabel(
                        liveState));
            }

            ImGui.SameLine();

            if (ImGui.SmallButton(
                    "X##RemoveInstruction"))
            {
                remove =
                    true;
            }

            ImGui.PushStyleColor(
                ImGuiCol.Button,
                condition
                    ? new Vector4(
                        0.10f,
                        0.24f,
                        0.34f,
                        1.0f)
                    : new Vector4(
                        0.34f,
                        0.20f,
                        0.08f,
                        1.0f));

            ImGui.PushStyleColor(
                ImGuiCol.ButtonHovered,
                condition
                    ? new Vector4(
                        0.13f,
                        0.31f,
                        0.43f,
                        1.0f)
                    : new Vector4(
                        0.43f,
                        0.26f,
                        0.10f,
                        1.0f));

            ImGui.SmallButton(
                $"{category}##InstructionCategory");

            ImGui.PopStyleColor(2);

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(
                    condition
                        ? $"Condition category: {category}"
                        : $"Action category: {category}");
            }

            string summary = GetInstructionCompactSummary(instruction);
            ImGui.TextDisabled(summary);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Select this node to edit its values in Details.");
            float measuredLogicalHeight =
                (ImGui.GetCursorPosY() +
                 ImGui.GetStyle().WindowPadding.Y) /
                MathF.Max(
                    _graphCanvas.Zoom,
                    0.01f);

            if (!_measuredInstructionNodeLogicalHeights.TryGetValue(
                    instruction.InstanceId,
                    out float cachedHeight) ||
                MathF.Abs(
                    cachedHeight -
                    measuredLogicalHeight) >
                1.0f)
            {
                _measuredInstructionNodeLogicalHeights[instruction.InstanceId] =
                    measuredLogicalHeight;
            }
        }

        ImGui.EndChild();

        PopGraphNodeStyle();

        ImGui.PopStyleColor(
            2);

        Vector4 pinColor =
            condition
                ? ConditionWireColor
                : ExecutionWireColor;

        Vector2 input =
            GetInstructionInput(
                instruction);

        Vector2 output =
            GetInstructionOutput(
                instruction);

        bool logicGate =
            condition &&
            IsLogicGate(
                instruction);

        bool branchAction =
            !condition &&
            IsBranchAction(
                instruction);

        if (loose)
        {
            if (!condition || logicGate)
            {
                _graphCanvas.DrawPin(input, pinColor, 6.0f);
                if (logicGate)
                    TryStartWireDrag(rule, WireDragKind.ConditionInput,
                        instruction.InstanceId, input);
            }
            if (branchAction)
            {
                Vector2 truePin = GetBranchTrueOutput(instruction);
                Vector2 falsePin = GetBranchFalseOutput(instruction);
                _graphCanvas.DrawPin(truePin, TrueExecutionWireColor, 6.5f);
                _graphCanvas.DrawPin(falsePin, FalseExecutionWireColor, 6.5f);
                TryStartWireDrag(rule, WireDragKind.ActionTrue,
                    instruction.InstanceId, truePin);
                TryStartWireDrag(rule, WireDragKind.ActionFalse,
                    instruction.InstanceId, falsePin);
            }
            else
            {
                _graphCanvas.DrawPin(output, pinColor, 6.0f);
                TryStartWireDrag(rule,
                    condition ? WireDragKind.Condition : WireDragKind.Action,
                    instruction.InstanceId, output);
            }
            return remove;
        }

        if (!condition ||
            logicGate)
        {
            _graphCanvas.DrawPin(
                input,
                pinColor,
                6.0f);
            if (logicGate)
                TryStartWireDrag(rule, WireDragKind.ConditionInput,
                    instruction.InstanceId, input);
        }

        if (branchAction)
        {
            Vector2 trueOutput =
                GetBranchTrueOutput(
                    instruction);

            Vector2 falseOutput =
                GetBranchFalseOutput(
                    instruction);

            _graphCanvas.DrawPin(
                trueOutput,
                TrueExecutionWireColor,
                6.5f);

            _graphCanvas.DrawPin(
                falseOutput,
                FalseExecutionWireColor,
                6.5f);

            if (ImGui.IsMouseClicked(
                    ImGuiMouseButton.Right))
            {
                if (_graphCanvas.IsPointHovered(
                        input,
                        13.0f))
                {
                    RecordHistory(
                        "Disconnect Execution Wire");

                    DisconnectIncomingExecution(
                        rule,
                        instruction.InstanceId);

                    _dirty =
                        true;
                }
                else if (_graphCanvas.IsPointHovered(
                             trueOutput,
                             13.0f))
                {
                    RecordHistory(
                        "Disconnect True Wire");

                    instruction.TrueActionId =
                        null;

                    _dirty =
                        true;
                }
                else if (_graphCanvas.IsPointHovered(
                             falseOutput,
                             13.0f))
                {
                    RecordHistory(
                        "Disconnect False Wire");

                    instruction.FalseActionId =
                        null;

                    _dirty =
                        true;
                }
            }

            TryStartWireDrag(
                rule,
                WireDragKind.ActionTrue,
                instruction.InstanceId,
                trueOutput);

            TryStartWireDrag(
                rule,
                WireDragKind.ActionFalse,
                instruction.InstanceId,
                falseOutput);

            return remove;
        }

        _graphCanvas.DrawPin(
            output,
            pinColor,
            6.0f);

        if (ImGui.IsMouseClicked(
                ImGuiMouseButton.Right))
        {
            if (condition &&
                logicGate &&
                _graphCanvas.IsPointHovered(
                    input,
                    13.0f))
            {
                RecordHistory(
                    "Disconnect Logic Inputs");

                instruction.ConditionInputIds.Clear();

                _dirty =
                    true;
            }
            else if (condition &&
                     _graphCanvas.IsPointHovered(
                         output,
                         13.0f))
            {
                RecordHistory(
                    "Disconnect Condition Wire");

                DisconnectConditionEverywhere(
                    rule,
                    instruction.InstanceId);

                _dirty =
                    true;
            }
            else if (!condition &&
                     _graphCanvas.IsPointHovered(
                         input,
                         13.0f))
            {
                RecordHistory(
                    "Disconnect Execution Wire");

                DisconnectIncomingExecution(
                    rule,
                    instruction.InstanceId);

                _dirty =
                    true;
            }
            else if (!condition &&
                     _graphCanvas.IsPointHovered(
                         output,
                         13.0f))
            {
                RecordHistory(
                    "Disconnect Execution Wire");

                instruction.NextActionId =
                    null;

                _dirty =
                    true;
            }
        }

        TryStartWireDrag(
            rule,
            condition
                ? WireDragKind.Condition
                : WireDragKind.Action,
            instruction.InstanceId,
            output);

        return remove;
    }

    private void DrawLooseGraphNodes(EditorState? state)
    {
        if (_module == null)
            return;

        DrawLooseGraphNodeList(_module.EditorLooseConditions, true, state);
        DrawLooseGraphNodeList(_module.EditorLooseActions, false, state);
    }

    private void DrawLooseGraphNodeList(
        List<VisualInstruction> nodes, bool condition, EditorState? state)
    {
        for (int index = 0; index < nodes.Count; index++)
        {
            VisualInstruction instruction = nodes[index];
            if (IsNodeHiddenByGroup(instruction.InstanceId))
                continue;
            ImGui.PushID(instruction.InstanceId.ToString());
            bool remove = DrawInstructionGraphNode(
                _looseNodeOwner, instruction, condition, index, state, true);
            ImGui.PopID();
            if (!remove)
                continue;

            RecordHistory(condition ? "Delete Draft Condition" : "Delete Draft Action");
            RemoveNodeFromGroups(instruction.InstanceId);
            DisconnectLooseReferences(instruction.InstanceId);
            _selectedGraphNodes.Remove(instruction.InstanceId);
            nodes.RemoveAt(index--);
            _dirty = true;
        }
    }

    private void DrawGraphWires()
    {
        _drawingGraphWires = true;
        _graphPortOwners.Clear();
        if (_module != null)
        {
            foreach (EventRuleDefinition rule in _module.Rules)
            {
                _graphPortOwners[GetEventConditionInput(rule)] = rule.Id;
                _graphPortOwners[GetEventExecutionOutput(rule)] = rule.Id;
            }
            foreach (VisualInstruction instruction in _module.Rules
                .SelectMany(rule => rule.Conditions.Concat(rule.Actions))
                .Concat(_module.EditorLooseConditions).Concat(_module.EditorLooseActions))
            {
                _graphPortOwners[GetInstructionInput(instruction)] = instruction.InstanceId;
                _graphPortOwners[GetInstructionOutput(instruction)] = instruction.InstanceId;
                if (IsBranchAction(instruction))
                {
                    _graphPortOwners[GetBranchTrueOutput(instruction)] = instruction.InstanceId;
                    _graphPortOwners[GetBranchFalseOutput(instruction)] = instruction.InstanceId;
                }
            }
        }
        if (_module ==
            null)
        {
            _drawingGraphWires = false;
            return;
        }

        foreach (EventRuleDefinition rule
                 in _module.Rules)
        {
            if (rule.EditorCollapsed && ActiveGraphGroup == null)
            {
                continue;
            }

            /*
             * Advanced Condition graph.
             *
             * Ordinary Condition outputs may feed AND/OR gate inputs.
             * Root Conditions (ordinary or gate outputs) feed the Event.
             */
            foreach (VisualInstruction gate
                     in rule.Conditions.Where(
                         IsLogicGate))
            {
                foreach (Guid inputId
                         in gate.ConditionInputIds)
                {
                    VisualInstruction? source =
                        FindCondition(
                            rule,
                            inputId);

                    if (source ==
                        null)
                    {
                        continue;
                    }

                    DrawCollapsibleWire(
                        GetInstructionOutput(
                            source),
                        GetInstructionInput(
                            gate),
                        ConditionWireColor,
                        3.0f);
                }
            }

            Vector2 eventConditionInput =
                GetEventConditionInput(
                    rule);

            foreach (VisualInstruction condition
                     in GetConnectedConditions(
                         rule))
            {
                DrawCollapsibleWire(
                    GetInstructionOutput(
                        condition),
                    eventConditionInput,
                    ConditionWireColor,
                    3.0f);
            }

            /*
             * Orange wires are now real execution-flow links rather than
             * being inferred from Actions list order.
             */
            if (!rule.HasExplicitExecutionFlow)
            {
                Vector2 previousOutput =
                    GetEventExecutionOutput(
                        rule);

                foreach (VisualInstruction action
                         in rule.Actions)
                {
                    Vector2 actionInput =
                        GetInstructionInput(
                            action);

                    DrawCollapsibleWire(
                        previousOutput,
                        actionInput,
                        ExecutionWireColor,
                        3.5f);

                    previousOutput =
                        GetInstructionOutput(
                            action);
                }

                continue;
            }

            if (rule.FirstActionId.HasValue)
            {
                VisualInstruction? firstAction =
                    FindAction(
                        rule,
                        rule.FirstActionId.Value);

                if (firstAction !=
                    null)
                {
                    DrawCollapsibleWire(
                        GetEventExecutionOutput(
                            rule),
                        GetInstructionInput(
                            firstAction),
                        ExecutionWireColor,
                        3.5f);
                }
            }

            foreach (VisualInstruction action
                     in rule.Actions)
            {
                if (IsBranchAction(
                        action))
                {
                    if (action.TrueActionId.HasValue)
                    {
                        VisualInstruction? trueAction =
                            FindAction(
                                rule,
                                action.TrueActionId.Value);

                        if (trueAction !=
                            null)
                        {
                            DrawCollapsibleWire(
                                GetBranchTrueOutput(
                                    action),
                                GetInstructionInput(
                                    trueAction),
                                TrueExecutionWireColor,
                                3.5f);
                        }
                    }

                    if (action.FalseActionId.HasValue)
                    {
                        VisualInstruction? falseAction =
                            FindAction(
                                rule,
                                action.FalseActionId.Value);

                        if (falseAction !=
                            null)
                        {
                            DrawCollapsibleWire(
                                GetBranchFalseOutput(
                                    action),
                                GetInstructionInput(
                                    falseAction),
                                FalseExecutionWireColor,
                                3.5f);
                        }
                    }

                    continue;
                }

                if (!action.NextActionId.HasValue)
                {
                    continue;
                }

                VisualInstruction? next =
                    FindAction(
                        rule,
                        action.NextActionId.Value);

                if (next ==
                    null)
                {
                    continue;
                }

                DrawCollapsibleWire(
                    GetInstructionOutput(
                        action),
                    GetInstructionInput(
                        next),
                    ExecutionWireColor,
                    3.5f);
            }
        }

        foreach (VisualInstruction gate in _module.EditorLooseConditions.Where(IsLogicGate))
            foreach (Guid inputId in gate.ConditionInputIds)
            {
                VisualInstruction? source = _module.EditorLooseConditions
                    .FirstOrDefault(item => item.InstanceId == inputId);
                if (source != null)
                    DrawCollapsibleWire(GetInstructionOutput(source),
                        GetInstructionInput(gate), ConditionWireColor, 3.0f);
            }

        foreach (VisualInstruction action in _module.EditorLooseActions)
        {
            void DrawDraftActionWire(Guid? targetId, Vector2 output, Vector4 color)
            {
                VisualInstruction? target = _module.EditorLooseActions
                    .FirstOrDefault(item => item.InstanceId == targetId);
                if (target != null)
                    DrawCollapsibleWire(output, GetInstructionInput(target), color, 3.5f);
            }

            if (IsBranchAction(action))
            {
                DrawDraftActionWire(action.TrueActionId,
                    GetBranchTrueOutput(action), TrueExecutionWireColor);
                DrawDraftActionWire(action.FalseActionId,
                    GetBranchFalseOutput(action), FalseExecutionWireColor);
            }
            else
                DrawDraftActionWire(action.NextActionId,
                    GetInstructionOutput(action), ExecutionWireColor);
        }
        _drawingGraphWires = false;
    }

    private void DrawCollapsibleWire(Vector2 source, Vector2 target, Vector4 color, float thickness)
    {
        EventGraphGroupDefinition? active = ActiveGraphGroup;
        if (active != null)
        {
            bool sourceInside = _graphPortOwners.TryGetValue(source, out Guid sourceId) &&
                active.MemberIds.Contains(sourceId);
            bool targetInside = _graphPortOwners.TryGetValue(target, out Guid targetId) &&
                active.MemberIds.Contains(targetId);
            if (!sourceInside && !targetInside)
                return;
            if (TryGetExpandedGroupBounds(active, out Vector2 min, out Vector2 max))
            {
                if (!sourceInside)
                    source = new Vector2(min.X - 80, min.Y + 60);
                if (!targetInside)
                    target = new Vector2(max.X + 100, min.Y + 60);
            }
        }
        EventGraphGroupDefinition? sourceGroup = FindCollapsedGroupAtPort(source);
        EventGraphGroupDefinition? targetGroup = FindCollapsedGroupAtPort(target);
        if (sourceGroup != null && sourceGroup == targetGroup)
            return;

        if (sourceGroup != null &&
            TryGetGroupBounds(sourceGroup, out Vector2 sourceMin, out Vector2 sourceMax))
            source = new Vector2(sourceMax.X, (sourceMin.Y + sourceMax.Y) * 0.5f);
        if (targetGroup != null &&
            TryGetGroupBounds(targetGroup, out Vector2 targetMin, out Vector2 targetMax))
            target = new Vector2(targetMin.X, (targetMin.Y + targetMax.Y) * 0.5f);

        _graphCanvas.DrawWire(source, target, color, thickness);
    }

    private EventGraphGroupDefinition? FindCollapsedGroupAtPort(Vector2 point)
    {
        if (_module == null || !_graphPortOwners.TryGetValue(point, out Guid owner))
            return null;
        return _module.EditorGroups.FirstOrDefault(group => group.Collapsed &&
            IsGroupVisible(group) && group.MemberIds.Contains(owner));
    }

    private void DisconnectLooseReferences(Guid deletedId)
    {
        if (_module == null)
            return;
        foreach (VisualInstruction gate in _module.EditorLooseConditions)
            gate.ConditionInputIds.RemoveAll(id => id == deletedId);
        foreach (VisualInstruction action in _module.EditorLooseActions)
        {
            if (action.NextActionId == deletedId) action.NextActionId = null;
            if (action.TrueActionId == deletedId) action.TrueActionId = null;
            if (action.FalseActionId == deletedId) action.FalseActionId = null;
        }
    }

    private void DrawWireDragPreview()
    {
        if (_wireDragKind ==
            WireDragKind.None)
        {
            return;
        }

        Vector2 mouse =
            _graphCanvas.MouseGraphPosition;

        Vector4 color =
            _wireDragKind switch
            {
                WireDragKind.Condition or WireDragKind.ConditionInput =>
                    ConditionWireColor,

                WireDragKind.ActionTrue =>
                    TrueExecutionWireColor,

                WireDragKind.ActionFalse =>
                    FalseExecutionWireColor,

                _ =>
                    ExecutionWireColor
            };

        if (_wireDragKind == WireDragKind.ConditionInput ||
            (_wireDragKind == WireDragKind.Condition &&
             _wireDragSourceInstructionId == Guid.Empty))
        {
            _graphCanvas.DrawWire(
                mouse,
                _wireDragStart,
                color,
                3.0f);
        }
        else
        {
            _graphCanvas.DrawWire(
                _wireDragStart,
                mouse,
                color,
                3.0f);
        }
    }

    private void TryStartWireDrag(
        EventRuleDefinition rule,
        WireDragKind kind,
        Guid sourceInstructionId,
        Vector2 pin)
    {
        if (ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId) ||
            _wireDragKind !=
                WireDragKind.None ||
            !_graphCanvas.IsPointHovered(
                pin,
                13.0f) ||
            !ImGui.IsMouseClicked(
                ImGuiMouseButton.Left))
        {
            return;
        }

        _draggingGraphNodeId =
            Guid.Empty;

        _graphNodeDragStarted =
            false;

        _wireDragKind =
            kind;

        _wireDragRuleId =
            rule.Id;

        _wireDragSourceInstructionId =
            sourceInstructionId;

        _wireDragStart =
            pin;
    }

    private void FinishWireDrag()
    {
        if (_wireDragKind ==
            WireDragKind.None)
        {
            return;
        }

        if (ImGui.IsMouseClicked(
                ImGuiMouseButton.Right))
        {
            CancelWireDrag();
            return;
        }

        if (!ImGui.IsMouseReleased(
                ImGuiMouseButton.Left))
        {
            return;
        }

        /*
         * Drop directly onto a compatible pin to connect immediately.
         * Releasing in empty space keeps the existing create-from-wire flow.
         */
        if ((_wireDragKind == WireDragKind.Condition ||
             _wireDragKind == WireDragKind.ConditionInput) &&
            TryConnectConditionWireAtMouse())
        {
            CancelWireDrag();
            return;
        }

        if (
            (
                _wireDragKind ==
                    WireDragKind.Action ||
                _wireDragKind ==
                    WireDragKind.ActionTrue ||
                _wireDragKind ==
                    WireDragKind.ActionFalse
            ) &&
            TryConnectExecutionWireAtMouse())
        {
            CancelWireDrag();
            return;
        }

        _pendingWireCreateKind =
            _wireDragKind;

        _pendingWireRuleId =
            _wireDragRuleId;

        _pendingWireSourceInstructionId =
            _wireDragSourceInstructionId;

        _pendingWireCreatePosition =
            _graphCanvas.MouseGraphPosition;

        CancelWireDrag();

        ImGui.OpenPopup(
            "Create From Wire");
    }

    private void CancelWireDrag()
    {
        _wireDragKind =
            WireDragKind.None;

        _wireDragRuleId =
            Guid.Empty;

        _wireDragSourceInstructionId =
            Guid.Empty;
    }

    private void DrawWireCreatePopup()
    {
        if (!ImGui.BeginPopup(
                "Create From Wire"))
        {
            return;
        }

        if (_pendingWireCreateKind == WireDragKind.ConditionInput)
        {
            EventRuleDefinition? owner = FindRule(_pendingWireRuleId);
            VisualInstruction? gate = owner == null
                ? _module?.EditorLooseConditions.FirstOrDefault(item =>
                    item.InstanceId == _pendingWireSourceInstructionId)
                : FindCondition(owner, _pendingWireSourceInstructionId);
            if (gate != null && IsLogicGate(gate))
            {
                ImGui.TextDisabled("CREATE CONDITION FOR LOGIC INPUT");
                ImGui.Separator();
                DrawConditionCreateForGate(owner, gate, _pendingWireCreatePosition);
            }
            ImGui.EndPopup();
            return;
        }

        EventRuleDefinition? rule =
            FindRule(
                _pendingWireRuleId);

        if (rule ==
            null)
        {
            if (_module != null &&
                _pendingWireCreateKind == WireDragKind.Condition &&
                _module.EditorLooseConditions.Any(item =>
                    item.InstanceId == _pendingWireSourceInstructionId))
            {
                ImGui.TextDisabled("CREATE LOGIC GATE FROM DRAFT CONDITION");
                DrawLooseDefinitionCreateMenu(true, _pendingWireCreatePosition,
                    _pendingWireSourceInstructionId, _pendingWireCreateKind);
            }
            else if (_module != null &&
                     _module.EditorLooseActions.Any(item =>
                         item.InstanceId == _pendingWireSourceInstructionId))
            {
                ImGui.TextDisabled("CREATE ACTION FROM DRAFT WIRE");
                DrawLooseDefinitionCreateMenu(false, _pendingWireCreatePosition,
                    _pendingWireSourceInstructionId, _pendingWireCreateKind);
            }
            else
                ImGui.TextDisabled("The source node no longer exists.");

            ImGui.EndPopup();
            return;
        }

        if (_pendingWireCreateKind ==
            WireDragKind.Condition)
        {
            ImGui.TextDisabled(
                "CREATE CONDITION FROM WIRE");

            ImGui.Separator();

            DrawDefinitionCreateMenu(
                rule,
                true,
                _pendingWireCreatePosition,
                _pendingWireSourceInstructionId,
                true);
        }
        else if (
            _pendingWireCreateKind ==
                WireDragKind.Action ||
            _pendingWireCreateKind ==
                WireDragKind.ActionTrue ||
            _pendingWireCreateKind ==
                WireDragKind.ActionFalse)
        {
            ImGui.TextDisabled(
                _pendingWireCreateKind ==
                    WireDragKind.ActionTrue
                    ? "CREATE TRUE ACTION FROM WIRE"
                    : _pendingWireCreateKind ==
                        WireDragKind.ActionFalse
                        ? "CREATE FALSE ACTION FROM WIRE"
                        : "CREATE ACTION FROM WIRE");

            ImGui.Separator();

            DrawDefinitionCreateMenu(
                rule,
                false,
                _pendingWireCreatePosition,
                _pendingWireSourceInstructionId,
                true);
        }

        ImGui.Separator();

        if (ImGui.MenuItem(
                "New Event Here (Independent)"))
        {
            RequestAddEvent(
                _pendingWireCreatePosition);
        }

        ImGui.EndPopup();
    }

}
