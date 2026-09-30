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

internal sealed class EventWorkspacePanel
{
    private readonly EditorDocumentManager _documents;

    public EventWorkspacePanel(EditorDocumentManager documents)
    {
        _documents = documents;
    }
    private readonly EventModuleSerializer _serializer =
        new();

    private readonly VisualLogicRegistry _registry =
        VisualLogicRegistry.CreateDefault();

    private readonly VariableReferencePicker _referencePicker =
        new();

    private readonly GameObjectReferencePicker _objectPicker =
        new();

    private string _audioClipSearch = string.Empty;

    private readonly Dictionary<Guid, AnimationSignalCacheEntry>
        _animationSignalCache = new();

    private sealed record AnimationSignalCacheEntry(
        string Key,
        AnimationSignalAuthoringResult Result);
    private readonly EventModuleHistory _history =
        new();

    private readonly ByteGraphCanvas _graphCanvas =
        new();

    private readonly HashSet<Guid> _selectedGraphNodes =
        new();

    private readonly Dictionary<Vector2, Guid> _graphPortOwners = new();
    private readonly List<Guid> _openGraphGroups = new();
    private readonly Dictionary<Guid, (Vector2 Pan, float Zoom)> _graphTabViews = new();
    private Guid _activeGraphGroupId;
    private Guid _graphTabModuleId;
    private Guid? _focusGraphTab;
    private Guid _draggingGroupId;
    private bool _resizingGroup;
    private bool _groupPointerCaptured;
    private bool _drawingGraphWires;

    private readonly EventRuleDefinition _looseNodeOwner = new() { Id = Guid.Empty };

    private readonly Dictionary<Guid, float> _measuredInstructionNodeLogicalHeights =
        new();

    private readonly Dictionary<Guid, float> _measuredEventNodeLogicalHeights =
        new();

    private enum WireDragKind
    {
        None,
        Condition,
        ConditionInput,
        Action,
        ActionTrue,
        ActionFalse
    }

    private WireDragKind _wireDragKind;

    private WireDragKind _pendingWireCreateKind;

    private Guid _wireDragRuleId =
        Guid.Empty;

    private Guid _wireDragSourceInstructionId =
        Guid.Empty;

    private Guid _pendingWireRuleId =
        Guid.Empty;

    private Guid _pendingWireSourceInstructionId =
        Guid.Empty;

    private Vector2 _wireDragStart;

    private Vector2 _pendingWireCreatePosition;

    private Vector2 _graphContextPosition;

    private bool _anyGraphNodeHovered;

    private Guid _hoveredGraphNodeId =
        Guid.Empty;

    private bool _marqueeSelecting;

    private bool _marqueeAdditive;

    private Vector2 _marqueeStart;

    private Vector2 _marqueeCurrent;

    private Guid _draggingGraphNodeId =
        Guid.Empty;

    private bool _graphNodeDragStarted;

    private Vector2 _graphNodeDragStartMouse;

    private Vector2 _graphNodeDragPreviousMouse;

    private Guid _dragHistoryNodeId =
        Guid.Empty;

    private bool _requestFrameGraph =
        true;

    private Guid _requestFrameNodeId = Guid.Empty;
    private string _graphOutlineSearch = string.Empty;

    private readonly EventNamePopupState _eventNamePopup = new();
    private readonly ByteGraphViewSettingsState _viewSettings = new();

    private AssetRecord? _asset;

    private EditorProjectContext? _project;

    private EventModuleDefinition? _module;

    private bool _open;

    private bool _dirty;

    private bool _requestFocus;
    private bool _openUnsavedPopup;
    private EditorDocumentId? _registeredDocumentId;
    private readonly EditorIdleDebounce _autoSaveIdle = new();

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

        _selectedGraphNodes.Clear();

        InitializeMissingGraphLayout();

        _requestFrameGraph =
            true;

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

        _selectedGraphNodes.Clear();

        InitializeMissingGraphLayout();

        _requestFrameGraph =
            true;

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

        _project =
            EditorProjectContext.Active;

        _eventNamePopup.Reset();
        _viewSettings.Reset();

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

            InitializeMissingGraphLayout();

            _graphCanvas.ResetView();

            _requestFrameGraph =
                true;

            _history.Reset(
                _module);

            _open =
                true;

            _dirty =
                false;

            _requestFocus =
                true;

            RegisterDocument(log);

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

        if (_requestFocus && !_eventNamePopup.IsOpen && !_viewSettings.IsOpen)
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

        bool graphPointerInteraction =
            _graphCanvas.IsMouseInsideCanvas &&
            (
                ImGui.IsMouseDown(
                    ImGuiMouseButton.Left) ||
                ImGui.IsMouseDown(
                    ImGuiMouseButton.Middle) ||
                ImGui.IsMouseDown(
                    ImGuiMouseButton.Right) ||
                _draggingGraphNodeId !=
                    Guid.Empty ||
                _wireDragKind !=
                    WireDragKind.None ||
                _marqueeSelecting
            );

        ImGuiWindowFlags workspaceFlags =
            graphPointerInteraction
                ? ImGuiWindowFlags.NoMove
                : ImGuiWindowFlags.None;

        bool visible =
            ImGui.Begin(
                $"{_module.Name}{dirtyMarker}###EventWorkspace:{_asset.Guid}",
                ref open,
                workspaceFlags);

        if (!open) RequestClose();

        if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) &&
            _registeredDocumentId.HasValue)
            _documents.Activate(_registeredDocumentId.Value);

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
            EventWorkspaceUndoRouter.ClearFocused(this);

        if (!visible)
        {
            _eventNamePopup.Reset();
            _viewSettings.Reset();
            ImGui.End();
            DrawUnsavedPopup(log);
            return;
        }

        ImGui.PushStyleVar(
            ImGuiStyleVar.ItemSpacing,
            new Vector2(EditorTheme.XS, EditorTheme.XS));

        ImGui.PushStyleVar(
            ImGuiStyleVar.FramePadding,
            new Vector2(EditorTheme.S, 4.0f));

        DrawDocumentToolbar(log);
        DrawModuleSettings();
        DrawTraceLegend();
        DrawGraphWorkspace(state);

        DrawEventNamePopup();

        if (!_eventNamePopup.IsOpen &&
            ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) &&
            ImGui.IsKeyPressed(ImGuiKey.F2))
        {
            EventRuleDefinition? selectedRule = _module.Rules
                .FirstOrDefault(rule => _selectedGraphNodes.Contains(rule.Id));
            if (selectedRule != null) RequestRenameEvent(selectedRule);
        }

        /*
         * Event Modules are runtime assets, not scene-local scratch data.
         *
         * Keep the on-disk .byteevents file synchronized with ByteGraph so
         * entering Play mode can never execute an older saved graph while
         * the editor shows a newer one.
         *
         * We wait until the user is no longer actively dragging/clicking an
         * editor control so node movement and sliders do not write every
         * frame.
         */
        if (_autoSaveIdle.Ready(ImGui.GetTime(), _dirty, !CanAutoSave()))
        {
            Save(
                log,
                false);
        }

        ImGui.PopStyleVar(
            2);

        ImGui.End();
        DrawUnsavedPopup(log);
    }

    // ========================================================
    // TOOLBAR
    // ========================================================

    private void DrawGraphWorkspace(EditorState? state)
    {
        DrawGraphTabs();
        float availableWidth = ImGui.GetContentRegionAvail().X;
        float detailsWidth = MathF.Min(380.0f, MathF.Max(300.0f, availableWidth * 0.30f));
        bool showDetails = availableWidth >= 720.0f;
        bool showOutline = availableWidth >= 1050.0f;
        float outlineWidth = showOutline ? 210.0f : 0.0f;

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

        DrawGraphCanvas(state);
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

    private void DrawGraphOutline(float width)
    {
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

        if (!layout.CollapseSecondary)
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

        ImGui.SameLine();
        if (EditorUi.ToolbarButton("...", "More graph actions")) ImGui.OpenPopup("More##ByteGraphToolbarMore");
        if (ImGui.BeginPopup("More##ByteGraphToolbarMore"))
        {
            if (layout.CollapseSecondary && ImGui.MenuItem("Arrange"))
            {
                RecordHistory("Auto Arrange Graph");
                AutoArrangeGraph();
                _requestFrameGraph = true;
                _dirty = true;
            }
            if (layout.CollapseSecondary && ImGui.MenuItem("Frame")) _requestFrameGraph = true;
            if (layout.CollapseSecondary) ImGui.Separator();
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
        ImGui.TextColored(GetTraceColor(state), $"● {label}");
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
        if (_wireDragKind !=
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
            case "flow.branch":
                DrawValueArgument(
                    instruction,
                    "condition",
                    "Condition",
                    VariableType.Boolean,
                    EventValue.Boolean(
                        false),
                    state,
                    false);

                ImGui.Separator();

                ImGui.TextColored(
                    TrueExecutionWireColor,
                    "TRUE output");

                ImGui.TextColored(
                    FalseExecutionWireColor,
                    "FALSE output");
                break;

            case "time.startTimer":
                DrawValueArgument(instruction, "name", "Timer Name", VariableType.String,
                    EventValue.String("Timer"), state, false);
                DrawValueArgument(instruction, "duration", "Duration (seconds)", VariableType.Number,
                    EventValue.Number(1.0), state, false);
                break;
            case "time.stopTimer":
            case "time.timerFinished":
            case "time.timerRunning":
                DrawValueArgument(instruction, "name", "Timer Name", VariableType.String,
                    EventValue.String("Timer"), state, false);
                break;

            case "input.mouseHeld":
            case "input.mousePressed":
            case "input.mouseReleased":
                DrawMouseButtonArgument(
                    instruction,
                    "button",
                    "Mouse Button",
                    MouseButton.Left);
                break;

            case "logic.and":
                ImGui.TextDisabled(
                    "TRUE when every connected input is TRUE.");

                ImGui.TextDisabled(
                    "Connect Condition outputs to the left pin.");
                break;

            case "logic.or":
                ImGui.TextDisabled(
                    "TRUE when any connected input is TRUE.");

                ImGui.TextDisabled(
                    "Connect Condition outputs to the left pin.");
                break;

            case "input.keyHeld":
            case "input.keyPressed":
            case "input.keyReleased":
                DrawKeyArgument(
                    instruction,
                    "key",
                    "Key",
                    Key.W);
                break;

            case "input.actionHeld":
            case "input.actionPressed":
            case "input.actionReleased":
                DrawInputActionArgument(instruction, "action", "Input Action", "Jump");
                break;

            case "input.axisGreater":
            case "input.axisLess":
            case "input.vectorLengthGreater":
                DrawInputActionArgument(instruction, "action", "Input Action", "Move");
                DrawValueArgument(instruction, "value", "Value", VariableType.Number,
                    EventValue.Number(instruction.Id == "input.axisLess" ? -.5 : .5), state, false);
                break;

            case "physics.lineTrace":
            case "physics.lineTraceHitsAnything":
                DrawLineTraceArguments(
                    instruction,
                    state);
                break;

            case "physics.castRayToCursor":
            case "physics.castRayInDirection":
            case "physics.castRay":
            case "physics.rayHitsAnything":
                DrawRaycastArguments(
                    instruction,
                    state);
                break;
            case "physics.lastRayHit":
            case "physics.lastRayMissed":
                ImGui.TextDisabled("Reads the latest raycast in this Event Sheet update.");
                break;
            case "physics.lastRayHitObject":
                DrawObjectTargetArgument(instruction, "target", "Object To Compare", state);
                break;
            case "physics.saveRayHit":
                DrawValueArgument(instruction, "prefix", "Self Variable Prefix", VariableType.String,
                    EventValue.String("Ray"), state, false);
                ImGui.TextDisabled("Writes Hit, ObjectId, Point, Normal, and Distance variables.");
                break;
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
                DrawUiArguments(instruction, state);
                break;
            case "material.setMaterial":
            case "material.setBaseColor":
            case "material.setMetallic":
            case "material.setRoughness":
            case "material.setEmissionColor":
            case "material.setEmissionIntensity":
            case "material.setTexture":
            case "material.resetOverrides":
                DrawMaterialActionArguments(instruction, state);
                break;
            case "combat.canFire":
            case "combat.fireWeapon":
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
            case "projectile.fire":
                DrawObjectTargetArgument(instruction, "target", "Object", state);
                break;
            case "health.percentAtMost":
                DrawObjectTargetArgument(instruction, "target", "Object", state);
                DrawValueArgument(instruction, "value", "Health Fraction (0-1)", VariableType.Number,
                    EventValue.Number(.25), state, false);
                break;
            case "health.damage":
            case "health.heal":
                DrawObjectTargetArgument(instruction, "target", "Object", state);
                DrawValueArgument(instruction, "amount", "Amount", VariableType.Number,
                    EventValue.Number(20), state, false);
                break;
            case "combat.damageLastRayHit":
                DrawValueArgument(instruction, "amount", "Damage", VariableType.Number,
                    EventValue.Number(20), state, false);
                break;

            case "attachment.attachToSocket":
                DrawObjectTargetArgument(instruction, "object", "Object", state);
                DrawObjectTargetArgument(instruction, "parent", "Parent", state);
                DrawAttachmentSocketArgument(instruction, state);
                DrawAttachmentRuleArgument(instruction, "locationRule", "Location Rule", "SnapToTarget");
                DrawAttachmentRuleArgument(instruction, "rotationRule", "Rotation Rule", "SnapToTarget");
                DrawAttachmentRuleArgument(instruction, "scaleRule", "Scale Rule", "KeepRelative");
                if (ImGui.TreeNode($"Offsets (optional)##{instruction.InstanceId}"))
                {
                    DrawValueArgument(instruction, "positionOffset", "Position Offset", VariableType.Vector3,
                        EventValue.Vector3(Vector3.Zero), state, false);
                    DrawValueArgument(instruction, "rotationOffset", "Rotation Offset", VariableType.Vector3,
                        EventValue.Vector3(Vector3.Zero), state, false);
                    DrawValueArgument(instruction, "scaleMultiplier", "Scale Multiplier", VariableType.Vector3,
                        EventValue.Vector3(Vector3.One), state, false);
                    ImGui.TreePop();
                }
                break;
            case "attachment.detach":
                DrawObjectTargetArgument(instruction, "object", "Object", state);
                DrawValueArgument(instruction, "keepWorld", "Keep World", VariableType.Boolean, EventValue.Boolean(true), state, false);
                break;
            case "attachment.isAttached":
                DrawObjectTargetArgument(instruction, "object", "Object", state);
                break;
            case "attachment.isAttachedToSocket":
                DrawObjectTargetArgument(instruction, "object", "Object", state);
                DrawObjectTargetArgument(instruction, "parent", "Parent", state);
                DrawAttachmentSocketArgument(instruction, state);
                break;
            case "animation.eventFired":
                DrawAnimationSignalCondition(
                    instruction,
                    state,
                    "event",
                    "Event",
                    includePayload: true);
                break;

            case "animation.windowEntered":
            case "animation.windowExited":
                DrawAnimationSignalCondition(
                    instruction,
                    state,
                    "window",
                    "Window",
                    includePayload: true);
                break;

            case "animation.windowActive":
                DrawAnimationSignalCondition(
                    instruction,
                    state,
                    "window",
                    "Window",
                    includePayload: false);
                break;
            case "animation.pause":
            case "animation.resume":
            case "animation.stop":
            case "animation.isPlaying":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);
                break;

            case "animation.play":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawAnimationClipArgument(
                    instruction,
                    "clip",
                    "Clip",
                    state);

                DrawValueArgument(
                    instruction,
                    "loop",
                    "Loop",
                    VariableType.Boolean,
                    EventValue.Boolean(
                        true),
                    state,
                    false);
                break;

            case "animation.playAction":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawAnimationClipArgument(
                    instruction,
                    "clip",
                    "Action Clip",
                    state);

                DrawValueArgument(
                    instruction,
                    "retrigger",
                    "Retrigger Same Action",
                    VariableType.Boolean,
                    EventValue.Boolean(
                        false),
                    state,
                    false);

                DrawValueArgument(
                    instruction,
                    "interruptCurrent",
                    "Interrupt Current Action",
                    VariableType.Boolean,
                    EventValue.Boolean(
                        true),
                    state,
                    false);
                break;

            case "animation.playNamedAction":
                DrawObjectTargetArgument(instruction,"target","Target Object",state); DrawAnimationActionArgument(instruction,state);
                DrawValueArgument(instruction,"retrigger","Retrigger",VariableType.Boolean,EventValue.Boolean(false),state,false);
                DrawValueArgument(instruction,"queueIfBlocked","Queue If Blocked",VariableType.Boolean,EventValue.Boolean(false),state,false);
                DrawValueArgument(instruction,"forceInterrupt","Force Interrupt",VariableType.Boolean,EventValue.Boolean(false),state,false); break;
            case "animation.queueNamedAction":
            case "animation.currentActionIs":
                DrawObjectTargetArgument(instruction,"target","Target Object",state); DrawAnimationActionArgument(instruction,state); break;
            case "animation.queueCombo":
            case "animation.cancelAction":
            case "animation.actionPlaying":
                DrawObjectTargetArgument(instruction,"target","Target Object",state); break;
            case "animation.triggerAction":
                DrawObjectTargetArgument(instruction, "target", "Target Object", state);
                DrawAnimationActionArgument(instruction, state, "Start Action");
                break;
            case "animation.setSpeed":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "speed",
                    "Speed",
                    VariableType.Number,
                    EventValue.Number(
                        1.0),
                    state,
                    false);
                break;

            case "animation.setTransitionDuration":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "duration",
                    "Transition Duration",
                    VariableType.Number,
                    EventValue.Number(
                        0.15),
                    state,
                    false);
                break;

            case "animation.setLayerWeight":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);
                DrawAnimationLayerArgument(
                    instruction,
                    state);
                DrawValueArgument(
                    instruction,
                    "weight",
                    "Weight (0 - 1)",
                    VariableType.Number,
                    EventValue.Number(1.0),
                    state,
                    false);
                break;

            case "animation.enableLayer":
            case "animation.disableLayer":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);
                DrawAnimationLayerArgument(
                    instruction,
                    state);
                break;

            case "animation.currentClipIs":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawAnimationClipArgument(
                    instruction,
                    "clip",
                    "Clip",
                    state);
                break;

            case "animation.currentStateIs":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "state",
                    "State",
                    VariableType.String,
                    EventValue.String(
                        "Idle"),
                    state,
                    false);
                break;

            case "object.exists":
            case "object.isActive":
            case "object.destroy":
            case "audio.play":
            case "audio.pause":
            case "audio.stop":
            case "audio.isPlaying":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);
                break;

            case "audio.playClip":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);
                DrawAudioClipArgument(
                    instruction);
                break;
            case "audio.setVolume":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "volume",
                    "Volume",
                    VariableType.Number,
                    EventValue.Number(
                        1.0),
                    state,
                    false);
                break;

            case "audio.setPitch":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "pitch",
                    "Pitch",
                    VariableType.Number,
                    EventValue.Number(
                        1.0),
                    state,
                    false);
                break;

            case "audio.setLoop":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "loop",
                    "Loop",
                    VariableType.Boolean,
                    EventValue.Boolean(
                        true),
                    state,
                    false);
                break;

            case "object.hasTag":
            case "object.doesNotHaveTag":
            case "object.addTag":
            case "object.removeTag":
                DrawObjectTargetArgument(instruction, "target", "Object", state);
                DrawTagArgument(instruction, "tag", "Tag", "Enemy");
                break;

            case "object.withTagExists":
                DrawTagArgument(instruction, "tag", "Tag", "Enemy");
                break;

            case "object.isOnLayer":
            case "object.setLayer":
                DrawObjectTargetArgument(instruction, "target", "Object", state);
                DrawLayerArgument(instruction, "layer", "Layer", "Default");
                break;

            case "object.setActive":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "active",
                    "Active",
                    VariableType.Boolean,
                    EventValue.Boolean(
                        true),
                    state,
                    false);
                break;

            case "object.spawnEmpty":
                DrawValueArgument(
                    instruction,
                    "name",
                    "Object Name",
                    VariableType.String,
                    EventValue.String(
                        "GameObject"),
                    state,
                    false);

                DrawValueArgument(
                    instruction,
                    "position",
                    "Spawn Position",
                    VariableType.Vector3,
                    EventValue.Vector3(
                        Vector3.Zero),
                    state,
                    false);
                break;

            case "object.spawnBlueprint":
                DrawBlueprintAssetArgument(
                    instruction,
                    "blueprint",
                    "Blueprint Asset");

                DrawValueArgument(
                    instruction,
                    "position",
                    "Spawn Position",
                    VariableType.Vector3,
                    EventValue.Vector3(
                        Vector3.Zero),
                    state,
                    false);
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
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

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
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

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
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "value",
                    "Value",
                    VariableType.Number,
                    EventValue.Number(0.0),
                    state,
                    false);
                break;

            case "transform.setRotation":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "rotation",
                    "Rotation (Degrees)",
                    VariableType.Vector3,
                    EventValue.Vector3(
                        Vector3.Zero),
                    state,
                    false);
                break;

            case "transform.rotateBy":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "amount",
                    "Degrees",
                    VariableType.Vector3,
                    EventValue.Vector3(
                        Vector3.Zero),
                    state,
                    false);
                break;

            case "transform.setScale":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);

                DrawValueArgument(
                    instruction,
                    "scale",
                    "Scale",
                    VariableType.Vector3,
                    EventValue.Vector3(
                        Vector3.One),
                    state,
                    false);
                break;

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

            case "variable.set":
                DrawTargetReference(
                    instruction,
                    "target",
                    "Target",
                    null,
                    state);

                VariableType? selectedType =
                    TryGetSelectedTargetVariableType(instruction, state);
                DrawValueArgument(
                    instruction,
                    "value",
                    "Value",
                    selectedType,
                    selectedType == VariableType.Boolean
                        ? EventValue.Boolean(false)
                        : EventValue.Number(0.0),
                    state,
                    false);
                break;

            case "variable.add":
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

            case "variable.toggle":
                DrawTargetReference(
                    instruction,
                    "target",
                    "Target",
                    VariableType.Boolean,
                    state);
                break;
        }
    }

    // ========================================================
    // RAYCAST AUTHORING
    // ========================================================

    private void DrawLineTraceArguments(
        VisualInstruction instruction,
        EditorState? state)
    {
        string startMode =
            DrawLineTraceModeArgument(
                instruction,
                "startMode",
                "Start From",
                "WorldPosition",
                new[]
                {
                    ("Object", "Object"),
                    ("WorldPosition", "World Position")
                });

        if (startMode.Equals(
                "Object",
                StringComparison.OrdinalIgnoreCase))
        {
            DrawObjectTargetArgument(
                instruction,
                "startObject",
                "Start Object",
                state);

            ImGui.TextDisabled(
                "Uses the object's first collider centre; falls back to its transform position. Select a muzzle Empty for an exact origin.");
        }
        else
        {
            DrawValueArgument(
                instruction,
                "start",
                "Start World Position",
                VariableType.Vector3,
                EventValue.Vector3(Vector3.Zero),
                state,
                false);
        }

        ImGui.Spacing();

        string endMode =
            DrawLineTraceModeArgument(
                instruction,
                "endMode",
                "End At",
                "WorldPosition",
                new[]
                {
                    ("Object", "Object"),
                    ("Direction", "Direction + Distance"),
                    ("Cursor", "Mouse Cursor"),
                    ("WorldPosition", "World Position")
                });

        if (endMode.Equals(
                "Object",
                StringComparison.OrdinalIgnoreCase))
        {
            DrawObjectTargetArgument(
                instruction,
                "endObject",
                "Target Object",
                state);

            ImGui.TextDisabled(
                "Aims at the target collider centre; falls back to its transform position.");
        }
        else if (endMode.Equals(
                     "Direction",
                     StringComparison.OrdinalIgnoreCase))
        {
            string directionMode =
                DrawLineTraceDirectionArgument(
                    instruction);

            if (directionMode.Equals(
                    "CustomLocal",
                    StringComparison.OrdinalIgnoreCase))
            {
                DrawValueArgument(
                    instruction,
                    "traceDirection",
                    "Custom Local Direction",
                    VariableType.Vector3,
                    EventValue.Vector3(new Vector3(0f, 0f, -1f)),
                    state,
                    false);
            }
            else if (directionMode.Equals(
                         "CustomWorld",
                         StringComparison.OrdinalIgnoreCase))
            {
                DrawValueArgument(
                    instruction,
                    "traceDirection",
                    "Custom World Direction",
                    VariableType.Vector3,
                    EventValue.Vector3(new Vector3(0f, 0f, -1f)),
                    state,
                    false);
            }

            DrawCompactRayValueArgument(
                instruction,
                "distance",
                "Trace Distance",
                VariableType.Number,
                EventValue.Number(100),
                state);
        }
        else if (endMode.Equals(
                     "Cursor",
                     StringComparison.OrdinalIgnoreCase))
        {
            DrawCompactRayValueArgument(
                instruction,
                "distance",
                "Maximum Distance",
                VariableType.Number,
                EventValue.Number(100),
                state);

            ImGui.TextDisabled(
                "The cursor chooses the aim point; Maximum Distance clamps the actual Start -> End trace.");
        }
        else
        {
            DrawValueArgument(
                instruction,
                "end",
                "End World Position",
                VariableType.Vector3,
                EventValue.Vector3(new Vector3(0f, 0f, -10f)),
                state,
                false);
        }

        DrawRayCollisionLayerArgument(
            instruction,
            state);

        DrawCompactRayValueArgument(
            instruction,
            "ignoreSelf",
            "Ignore Start Hierarchy",
            VariableType.Boolean,
            EventValue.Boolean(true),
            state);

        DrawCompactRayValueArgument(
            instruction,
            "includeTriggers",
            "Include Triggers",
            VariableType.Boolean,
            EventValue.Boolean(false),
            state);

        DrawCompactRayValueArgument(
            instruction,
            "drawDebug",
            "Draw Debug Trace",
            VariableType.Boolean,
            EventValue.Boolean(false),
            state);

        if (ImGui.TreeNode(
                $"Advanced Trace Settings##{instruction.InstanceId}"))
        {
            DrawValueArgument(
                instruction,
                "debugDuration",
                "Debug Duration (seconds)",
                VariableType.Number,
                EventValue.Number(.25),
                state,
                false);

            ImGui.TreePop();
        }

        string summary =
            endMode switch
            {
                "Object" => "Start -> target object",
                "Direction" => "Start -> direction x distance",
                "Cursor" => "Start -> collider under cursor (distance limited)",
                _ => "Start -> world position"
            };

        ImGui.TextDisabled(summary);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "ByteEngine resolves these simple inputs to one finite Start -> End segment, then performs one physics line trace. " +
                "The physics primitive stays universal; object, direction and cursor modes are only convenient ways to resolve End.");
        }
    }

    private string DrawLineTraceModeArgument(
        VisualInstruction instruction,
        string argumentName,
        string label,
        string legacyFallback,
        IReadOnlyList<(string Mode, string Label)> options)
    {
        string current =
            instruction.Arguments.TryGetValue(
                    argumentName,
                    out EventValue? value) &&
                value != null &&
                value.Kind == EventValueKind.Constant &&
                value.Constant.Type == VariableType.String &&
                !string.IsNullOrWhiteSpace(value.Constant.String)
                    ? value.Constant.String
                    : legacyFallback;

        string preview =
            options.FirstOrDefault(
                option =>
                    option.Mode.Equals(
                        current,
                        StringComparison.OrdinalIgnoreCase)).Label
            ?? current;

        ImGui.PushID(
            $"LineTraceMode:{argumentName}");

        ImGui.TextDisabled(label);
        ImGui.SetNextItemWidth(-1.0f);

        if (ImGui.BeginCombo(
                "##Mode",
                preview))
        {
            foreach ((string mode, string optionLabel) in options)
            {
                bool selected =
                    mode.Equals(
                        current,
                        StringComparison.OrdinalIgnoreCase);

                if (ImGui.Selectable(
                        optionLabel,
                        selected))
                {
                    RecordHistory(
                        $"Change Line Trace {label}");

                    instruction.Arguments[argumentName] =
                        EventValue.String(mode);

                    current =
                        mode;

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

        ImGui.PopID();

        return current;
    }

    private string DrawLineTraceDirectionArgument(
        VisualInstruction instruction)
    {
        string current =
            instruction.Arguments.TryGetValue(
                    "traceDirectionMode",
                    out EventValue? value) &&
                value != null &&
                value.Kind == EventValueKind.Constant &&
                value.Constant.Type == VariableType.String &&
                !string.IsNullOrWhiteSpace(value.Constant.String)
                    ? value.Constant.String
                    : "Forward";

        (string Mode, string Label)[] options =
        {
            ("Forward", "Forward"),
            ("Back", "Back"),
            ("Right", "Right"),
            ("Left", "Left"),
            ("Up", "Up"),
            ("Down", "Down"),
            ("CustomLocal", "Custom Local Direction"),
            ("CustomWorld", "Custom World Direction")
        };

        string preview =
            options.FirstOrDefault(
                option =>
                    option.Mode.Equals(
                        current,
                        StringComparison.OrdinalIgnoreCase)).Label
            ?? current;

        ImGui.PushID("LineTraceDirection");
        ImGui.TextDisabled("Direction");
        ImGui.SetNextItemWidth(-1.0f);

        if (ImGui.BeginCombo(
                "##Direction",
                preview))
        {
            foreach ((string mode, string optionLabel) in options)
            {
                bool selected =
                    mode.Equals(
                        current,
                        StringComparison.OrdinalIgnoreCase);

                if (ImGui.Selectable(
                        optionLabel,
                        selected))
                {
                    RecordHistory("Change Line Trace Direction");
                    instruction.Arguments["traceDirectionMode"] =
                        EventValue.String(mode);
                    current = mode;
                    _dirty = true;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        ImGui.PopID();
        return current;
    }

    private void DrawRaycastArguments(
        VisualInstruction instruction,
        EditorState? state)
    {
        DrawObjectTargetArgument(
            instruction,
            "source",
            "Ray Origin Object",
            state);

        DrawRayMuzzlePointArgument(
            instruction,
            state);

        bool cursorAction = instruction.Id.Equals(
            "physics.castRayToCursor",
            StringComparison.OrdinalIgnoreCase);
        bool directionAction = instruction.Id.Equals(
            "physics.castRayInDirection",
            StringComparison.OrdinalIgnoreCase);

        string aimMode;
        if (cursorAction)
        {
            aimMode = "TopDownCursor";
            ImGui.TextWrapped("Shoots from the origin object or muzzle to the collider directly under the mouse cursor.");
        }
        else if (directionAction)
        {
            aimMode = "MuzzleDirection";
            ImGui.TextWrapped("Shoots from the origin object or muzzle in the selected direction.");
        }
        else
        {
            aimMode = DrawRayAimModeArgument(instruction);
            if (aimMode == "TopDownCursor") DrawTopDownAimStyleArgument(instruction);
        }
        string directionMode = aimMode == "MuzzleDirection"
            ? DrawRayDirectionModeArgument(instruction) : string.Empty;

        if (string.Equals(
                directionMode,
                "CustomLocal",
                StringComparison.OrdinalIgnoreCase))
        {
            DrawValueArgument(
                instruction,
                "direction",
                "Custom Local Direction",
                VariableType.Vector3,
                EventValue.Vector3(
                    new Vector3(
                        0f,
                        0f,
                        -1f)),
                state,
                false);
        }
        else if (string.Equals(
                     directionMode,
                     "CustomWorld",
                     StringComparison.OrdinalIgnoreCase))
        {
            DrawValueArgument(
                instruction,
                "direction",
                "Custom World Direction",
                VariableType.Vector3,
                EventValue.Vector3(
                    new Vector3(
                        0f,
                        0f,
                        -1f)),
                state,
                false);
        }

        DrawCompactRayValueArgument(instruction, "distance", "Maximum Distance",
            VariableType.Number, EventValue.Number(100), state);
        DrawRayCollisionLayerArgument(instruction, state);
        DrawCompactRayValueArgument(instruction, "includeTriggers", "Include Triggers",
            VariableType.Boolean, EventValue.Boolean(false), state);
        DrawCompactRayValueArgument(instruction, "drawDebug", "Draw Debug Ray",
            VariableType.Boolean, EventValue.Boolean(false), state);

        if (ImGui.TreeNode($"Advanced Ray Settings##{instruction.InstanceId}"))
        {
            DrawValueArgument(instruction, "originOffset", "Muzzle Local Offset",
                VariableType.Vector3, EventValue.Vector3(Vector3.Zero), state, false);
            DrawValueArgument(instruction, "debugDuration", "Debug Duration (seconds)",
                VariableType.Number, EventValue.Number(.25), state, false);
            ImGui.TreePop();
        }

        ImGui.TextDisabled(cursorAction
            ? "Origin object / muzzle -> collider under cursor"
            : directionAction
                ? "Origin object / muzzle -> selected direction"
                : "Camera target -> muzzle hit");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(cursorAction
                ? "The visible cursor selects a collider. The ray starts at the chosen object or muzzle and ends at that exact hit point."
                : directionAction
                    ? "The ray starts at the chosen object or muzzle and follows the selected local or world direction."
                    : "Camera selects the aim point. The muzzle ray determines the real hit. Cyan = camera; red = hit; green = miss.");
    }

    private void DrawCompactRayValueArgument(VisualInstruction instruction, string argumentName,
        string label, VariableType type, EventValue fallback, EditorState? state)
    {
        if (!instruction.Arguments.TryGetValue(argumentName, out EventValue? value) || value == null)
            instruction.Arguments[argumentName] = value = fallback;
        if (value.Kind != EventValueKind.Constant || value.Constant.Type != type)
        {
            DrawValueArgument(instruction, argumentName, label, type, fallback, state, false);
            return;
        }

        ImGui.PushID(argumentName);
        if (type == VariableType.Boolean)
        {
            bool checkedValue = value.Constant.Boolean;
            if (ImGui.Checkbox(label, ref checkedValue))
            {
                instruction.Arguments[argumentName] = EventValue.Boolean(checkedValue);
                _dirty = true;
            }
            ImGui.SameLine();
        }
        else
        {
            ImGui.TextDisabled(label);
            ImGui.SameLine();
        }

        if (ImGui.SmallButton("Reference"))
        {
            RecordHistory("Use Ray Value Reference");
            instruction.Arguments[argumentName] = new EventValue { Kind = EventValueKind.Reference };
            _dirty = true;
        }

        if (type == VariableType.Number)
        {
            float number = (float)value.Constant.Number;
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.DragFloat("##Value", ref number, .1f, .01f, 100000f))
            {
                instruction.Arguments[argumentName] = EventValue.Number(MathF.Max(.01f, number));
                _dirty = true;
            }
        }
        ImGui.PopID();
    }

    private string DrawRayAimModeArgument(VisualInstruction instruction)
    {
        string current = instruction.Arguments.TryGetValue("aimMode", out EventValue? value) &&
            value.Kind == EventValueKind.Constant && value.Constant.Type == VariableType.String
                ? value.Constant.String : "MuzzleDirection";
        (string Mode, string Label)[] options =
        [
            ("TopDownCursor", "Top Down Cursor"),
            ("ThirdPersonCrosshair", "Third Person Crosshair"),
            ("FirstPersonCrosshair", "First Person Crosshair"),
            ("MuzzleDirection", "Muzzle Direction")
        ];
        string preview = options.FirstOrDefault(item => item.Mode == current).Label ?? "Muzzle Direction";
        ImGui.PushID("RayAimMode");
        ImGui.TextDisabled("Aim Mode");
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.BeginCombo("##AimMode", preview))
        {
            foreach ((string mode, string label) in options)
            {
                bool selected = current == mode;
                if (ImGui.Selectable(label, selected))
                {
                    RecordHistory("Change Ray Aim Mode");
                    instruction.Arguments["aimMode"] = EventValue.String(mode);
                    current = mode;
                    _dirty = true;
                }
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        ImGui.PopID();
        return current;
    }

    private void DrawTopDownAimStyleArgument(VisualInstruction instruction)
    {
        string current = instruction.Arguments.TryGetValue("topDownAimStyle", out EventValue? value) &&
            value.Kind == EventValueKind.Constant && value.Constant.Type == VariableType.String
                ? value.Constant.String : "Exact3D";
        ImGui.PushID("TopDownAimStyle");
        ImGui.TextDisabled("Top Down Aim Style");
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.BeginCombo("##AimStyle", current == "Planar" ? "Horizontal / Planar" : "Exact 3D Target"))
        {
            foreach ((string mode, string label) in new[]
                     { ("Exact3D", "Exact 3D Target"), ("Planar", "Horizontal / Planar") })
            {
                bool selected = current == mode;
                if (ImGui.Selectable(label, selected))
                {
                    RecordHistory("Change Top Down Aim Style");
                    instruction.Arguments["topDownAimStyle"] = EventValue.String(mode);
                    current = mode;
                    _dirty = true;
                }
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        ImGui.PopID();
    }


    private void DrawRayCollisionLayerArgument(VisualInstruction instruction, EditorState? state)
    {
        ClassificationSettings? settings = EditorProjectContext.Active?.Project.Classification;
        if (settings == null ||
            (instruction.Arguments.TryGetValue("layer", out EventValue? existing) &&
             (existing.Kind != EventValueKind.Constant || existing.Constant.Type != VariableType.Number)))
        {
            DrawValueArgument(instruction, "layer", "Collision Layer (-1 = All)",
                VariableType.Number, EventValue.Number(-1), state, false);
            return;
        }

        int current = instruction.Arguments.TryGetValue("layer", out EventValue? value)
            ? (int)value.Constant.Number : -1;
        string preview = current == -1 ? "All" : settings.FindLayer(current)?.Name ?? $"Layer {current}";
        ImGui.PushID("RayCollisionLayer");
        ImGui.TextDisabled("Collision Layer");
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.BeginCombo("##CollisionLayer", preview))
        {
            if (ImGui.Selectable("All", current == -1))
            {
                RecordHistory("Change Ray Collision Layer");
                instruction.Arguments["layer"] = EventValue.Number(-1);
                _dirty = true;
            }
            foreach (ObjectLayerDefinition layer in settings.Layers.OrderBy(item => item.Index))
            {
                if (ImGui.Selectable($"{layer.Index}  {layer.Name}##RayLayer{layer.Index}",
                        current == layer.Index))
                {
                    RecordHistory("Change Ray Collision Layer");
                    instruction.Arguments["layer"] = EventValue.Number(layer.Index);
                    _dirty = true;
                }
            }
            ImGui.EndCombo();
        }
        ImGui.PopID();
    }

    private void DrawRayMuzzlePointArgument(
        VisualInstruction instruction,
        EditorState? state)
    {
        string currentPath =
            instruction.Arguments.TryGetValue(
                "muzzlePath",
                out EventValue? existing) &&
            existing != null &&
            existing.Kind == EventValueKind.Constant &&
            existing.Constant.Type == VariableType.String
                ? existing.Constant.String
                : string.Empty;

        GameObject? owner =
            state != null
                ? ResolveEditorObjectArgument(
                    instruction,
                    "source",
                    state)
                : null;

        ImGui.PushID(
            "RayMuzzlePoint");

        ImGui.TextDisabled(
            "Muzzle Point");

        if (owner == null)
        {
            ImGui.TextDisabled(
                "Ray Owner is unresolved. Runtime will use the Ray Owner as the muzzle.");

            if (!string.IsNullOrWhiteSpace(
                    currentPath))
            {
                ImGui.TextDisabled(
                    $"Saved child path: {currentPath}");
            }

            ImGui.PopID();
            return;
        }

        GameObject? selectedObject =
            ResolveEditorRayMuzzlePath(
                owner,
                currentPath);

        string preview =
            string.IsNullOrWhiteSpace(
                currentPath)
                ? $"Use Ray Owner ({owner.Name})"
                : selectedObject != null
                    ? currentPath
                    : currentPath + " (Missing)";

        ImGui.SetNextItemWidth(
            -1.0f);

        if (ImGui.BeginCombo(
                "##MuzzlePoint",
                preview))
        {
            bool useOwner =
                string.IsNullOrWhiteSpace(
                    currentPath);

            if (ImGui.Selectable(
                    $"Use Ray Owner ({owner.Name})",
                    useOwner))
            {
                RecordHistory(
                    "Change Ray Muzzle Point");

                instruction.Arguments["muzzlePath"] =
                    EventValue.String(
                        string.Empty);

                currentPath =
                    string.Empty;

                selectedObject =
                    owner;

                _dirty =
                    true;
            }

            (GameObject Object, string Path)[] candidates =
                EnumerateRayMuzzleCandidates(
                    owner)
                .ToArray();

            if (candidates.Length > 0)
            {
                ImGui.Separator();
            }

            foreach ((GameObject Object, string Path) candidate
                     in candidates)
            {
                bool selected =
                    string.Equals(
                        currentPath,
                        candidate.Path,
                        StringComparison.OrdinalIgnoreCase);

                bool isEmpty =
                    candidate.Object.Components.Count == 0;

                string emptyHint =
                    isEmpty
                        ? "  [Empty]"
                        : string.Empty;

                if (ImGui.Selectable(
                        $"{candidate.Path}{emptyHint}##rayMuzzle:{candidate.Object.Id}",
                        selected))
                {
                    RecordHistory(
                        "Change Ray Muzzle Point");

                    instruction.Arguments["muzzlePath"] =
                        EventValue.String(
                            candidate.Path);

                    currentPath =
                        candidate.Path;

                    selectedObject =
                        candidate.Object;

                    _dirty =
                        true;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            if (candidates.Length == 0)
            {
                ImGui.TextDisabled(
                    "This Ray Owner has no child objects. Add an Empty under the gun first.");
            }

            ImGui.EndCombo();
        }

        if (!string.IsNullOrWhiteSpace(
                currentPath) &&
            selectedObject == null)
        {
            ImGui.TextColored(
                new Vector4(
                    1.0f,
                    .72f,
                    .28f,
                    1.0f),
                "The saved Muzzle Point no longer exists under the Ray Owner.");
        }
        else if (selectedObject != null &&
                 !ReferenceEquals(
                     selectedObject,
                     owner) &&
                 selectedObject.Components.Count > 0)
        {
            ImGui.TextDisabled(
                "Tip: a component-free Empty is recommended for a clean muzzle transform.");
        }

        ImGui.PopID();
    }

    private string DrawRayDirectionModeArgument(
        VisualInstruction instruction)
    {
        string current =
            InferRayDirectionMode(
                instruction);

        (string Mode, string Label)[] options =
        {
            ("Forward", "Muzzle Forward"),
            ("Back", "Muzzle Back"),
            ("Right", "Muzzle Right"),
            ("Left", "Muzzle Left"),
            ("Up", "Muzzle Up"),
            ("Down", "Muzzle Down"),
            ("CustomLocal", "Custom Local Direction"),
            ("CustomWorld", "Custom World Direction")
        };

        string preview =
            options
                .FirstOrDefault(
                    item =>
                        string.Equals(
                            item.Mode,
                            current,
                            StringComparison.OrdinalIgnoreCase))
                .Label
            ?? current;

        ImGui.PushID(
            "RayDirectionMode");

        ImGui.TextDisabled(
            "Direction");

        ImGui.SetNextItemWidth(
            -1.0f);

        if (ImGui.BeginCombo(
                "##DirectionMode",
                preview))
        {
            foreach ((string Mode, string Label) option
                     in options)
            {
                bool selected =
                    string.Equals(
                        current,
                        option.Mode,
                        StringComparison.OrdinalIgnoreCase);

                if (ImGui.Selectable(
                        option.Label,
                        selected))
                {
                    SetRayDirectionMode(
                        instruction,
                        option.Mode);

                    current =
                        option.Mode;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        string hint =
            current switch
            {
                "Forward" =>
                    "Uses the Muzzle Point's local Forward axis (-Z).",

                "Back" =>
                    "Uses the opposite of the Muzzle Point's Forward axis.",

                "Right" =>
                    "Uses the Muzzle Point's local Right axis (+X).",

                "Left" =>
                    "Uses the opposite of the Muzzle Point's Right axis.",

                "Up" =>
                    "Uses the Muzzle Point's local Up axis (+Y).",

                "Down" =>
                    "Uses the opposite of the Muzzle Point's Up axis.",

                "CustomWorld" =>
                    "Uses an absolute world-space vector. It does not rotate with the Muzzle Point.",

                _ =>
                    "Uses a custom vector in the Muzzle Point's local space."
            };

        ImGui.TextWrapped(
            hint);

        ImGui.PopID();

        return current;
    }

    private void SetRayDirectionMode(
        VisualInstruction instruction,
        string mode)
    {
        RecordHistory(
            "Change Ray Direction");

        instruction.Arguments["directionMode"] =
            EventValue.String(
                mode);

        switch (mode)
        {
            case "Forward":
                instruction.Arguments["direction"] =
                    EventValue.Vector3(
                        new Vector3(
                            0f,
                            0f,
                            -1f));

                instruction.Arguments["worldSpace"] =
                    EventValue.Boolean(
                        false);
                break;

            case "Back":
                instruction.Arguments["direction"] =
                    EventValue.Vector3(
                        new Vector3(
                            0f,
                            0f,
                            1f));

                instruction.Arguments["worldSpace"] =
                    EventValue.Boolean(
                        false);
                break;

            case "Right":
                instruction.Arguments["direction"] =
                    EventValue.Vector3(
                        new Vector3(
                            1f,
                            0f,
                            0f));

                instruction.Arguments["worldSpace"] =
                    EventValue.Boolean(
                        false);
                break;

            case "Left":
                instruction.Arguments["direction"] =
                    EventValue.Vector3(
                        new Vector3(
                            -1f,
                            0f,
                            0f));

                instruction.Arguments["worldSpace"] =
                    EventValue.Boolean(
                        false);
                break;

            case "Up":
                instruction.Arguments["direction"] =
                    EventValue.Vector3(
                        new Vector3(
                            0f,
                            1f,
                            0f));

                instruction.Arguments["worldSpace"] =
                    EventValue.Boolean(
                        false);
                break;

            case "Down":
                instruction.Arguments["direction"] =
                    EventValue.Vector3(
                        new Vector3(
                            0f,
                            -1f,
                            0f));

                instruction.Arguments["worldSpace"] =
                    EventValue.Boolean(
                        false);
                break;

            case "CustomWorld":
                instruction.Arguments["worldSpace"] =
                    EventValue.Boolean(
                        true);
                break;

            default:
                instruction.Arguments["worldSpace"] =
                    EventValue.Boolean(
                        false);
                break;
        }

        _dirty =
            true;
    }

    private static string InferRayDirectionMode(
        VisualInstruction instruction)
    {
        if (instruction.Arguments.TryGetValue(
                "directionMode",
                out EventValue? modeValue) &&
            modeValue != null &&
            modeValue.Kind == EventValueKind.Constant &&
            modeValue.Constant.Type == VariableType.String)
        {
            string normalized =
                NormalizeRayDirectionMode(
                    modeValue.Constant.String);

            if (!string.IsNullOrWhiteSpace(
                    normalized))
            {
                return normalized;
            }
        }

        bool legacyWorldSpace =
            instruction.Arguments.TryGetValue(
                "worldSpace",
                out EventValue? worldValue) &&
            worldValue != null &&
            worldValue.Kind == EventValueKind.Constant &&
            worldValue.Constant.Type == VariableType.Boolean &&
            worldValue.Constant.Boolean;

        if (legacyWorldSpace)
        {
            return "CustomWorld";
        }

        if (!instruction.Arguments.TryGetValue(
                "direction",
                out EventValue? directionValue) ||
            directionValue == null ||
            directionValue.Kind != EventValueKind.Constant ||
            directionValue.Constant.Type != VariableType.Vector3)
        {
            return "CustomLocal";
        }

        Vector3 direction =
            directionValue.Constant.Vector3;

        if (RayDirectionNearlyEquals(
                direction,
                new Vector3(
                    0f,
                    0f,
                    -1f)))
        {
            return "Forward";
        }

        if (RayDirectionNearlyEquals(
                direction,
                new Vector3(
                    0f,
                    0f,
                    1f)))
        {
            return "Back";
        }

        if (RayDirectionNearlyEquals(
                direction,
                new Vector3(
                    1f,
                    0f,
                    0f)))
        {
            return "Right";
        }

        if (RayDirectionNearlyEquals(
                direction,
                new Vector3(
                    -1f,
                    0f,
                    0f)))
        {
            return "Left";
        }

        if (RayDirectionNearlyEquals(
                direction,
                new Vector3(
                    0f,
                    1f,
                    0f)))
        {
            return "Up";
        }

        if (RayDirectionNearlyEquals(
                direction,
                new Vector3(
                    0f,
                    -1f,
                    0f)))
        {
            return "Down";
        }

        return "CustomLocal";
    }

    private static string NormalizeRayDirectionMode(
        string value)
    {
        return value
            .Trim()
            .Replace(
                " ",
                string.Empty)
            .ToLowerInvariant() switch
        {
            "forward" or
            "muzzleforward" =>
                "Forward",

            "back" or
            "muzzleback" =>
                "Back",

            "right" or
            "muzzleright" =>
                "Right",

            "left" or
            "muzzleleft" =>
                "Left",

            "up" or
            "muzzleup" =>
                "Up",

            "down" or
            "muzzledown" =>
                "Down",

            "customlocal" or
            "customlocaldirection" =>
                "CustomLocal",

            "customworld" or
            "customworlddirection" =>
                "CustomWorld",

            _ =>
                string.Empty
        };
    }

    private static bool RayDirectionNearlyEquals(
        Vector3 left,
        Vector3 right)
    {
        return Vector3.DistanceSquared(
                   left,
                   right) <=
               .000001f;
    }

    private static IEnumerable<(GameObject Object, string Path)>
        EnumerateRayMuzzleCandidates(
            GameObject owner)
    {
        return EnumerateRayMuzzleCandidates(
            owner,
            string.Empty);
    }

    private static IEnumerable<(GameObject Object, string Path)>
        EnumerateRayMuzzleCandidates(
            GameObject parent,
            string prefix)
    {
        foreach (GameObject child
                 in parent.Children
                     .OrderBy(
                         item =>
                             item.Name,
                         StringComparer.OrdinalIgnoreCase))
        {
            string path =
                string.IsNullOrWhiteSpace(
                    prefix)
                    ? child.Name
                    : $"{prefix}/{child.Name}";

            yield return
                (
                    child,
                    path
                );

            foreach ((GameObject Object, string Path) descendant
                     in EnumerateRayMuzzleCandidates(
                         child,
                         path))
            {
                yield return
                    descendant;
            }
        }
    }

    private static GameObject? ResolveEditorRayMuzzlePath(
        GameObject owner,
        string path)
    {
        if (string.IsNullOrWhiteSpace(
                path))
        {
            return owner;
        }

        GameObject current =
            owner;

        foreach (string segment
                 in path.Split(
                     '/',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            GameObject? next =
                current.Children.FirstOrDefault(
                    child =>
                        string.Equals(
                            child.Name,
                            segment,
                            StringComparison.OrdinalIgnoreCase));

            if (next == null)
            {
                return null;
            }

            current =
                next;
        }

        return current;
    }

    //
    // ========================================================
    // ANIMATION SIGNAL AUTHORING
    // ========================================================

    private void DrawAnimationSignalCondition(
        VisualInstruction instruction,
        EditorState? state,
        string signalArgument,
        string signalLabel,
        bool includePayload)
    {
        DrawObjectTargetArgument(instruction, "target", "Target Object", state);

        GameObject? target =
            state != null
                ? ResolveAnimationTargetForEditor(instruction, state)
                : null;

        if (state == null || _project == null || target == null)
        {
            DrawAnimationSignalManualFallback(
                instruction, signalArgument, signalLabel, includePayload, state,
                "Target cannot be resolved in the current authoring context.");
            return;
        }

        AnimationController? controller =
            AnimationController.FindForObject(target);

        if (controller == null)
        {
            DrawDisabledAnimationSignalFields(
                instruction, signalArgument, signalLabel, includePayload, state,
                "Target has no AnimationController.");
            return;
        }

        string cacheKey =
            AnimationSignalAuthoringResolver.CreateCacheKey(target, controller);

        if (!_animationSignalCache.TryGetValue(
                instruction.InstanceId,
                out AnimationSignalCacheEntry? cached) ||
            !string.Equals(cached.Key, cacheKey, StringComparison.Ordinal))
        {
            cached = new AnimationSignalCacheEntry(
                cacheKey,
                AnimationSignalAuthoringResolver.Resolve(target, _project));

            _animationSignalCache[instruction.InstanceId] = cached;
        }

        AnimationSignalAuthoringResult resolved = cached.Result;

        if (!resolved.HasSelectors || resolved.Model == null)
        {
            DrawAnimationSignalManualFallback(
                instruction, signalArgument, signalLabel, includePayload, state,
                resolved.Message);
            return;
        }

        string clipValue =
            DrawAnimationSignalClipPicker(instruction, resolved.Model);

        ImportedAnimation? selectedClip =
            string.IsNullOrWhiteSpace(clipValue)
                ? null
                : AnimationSignalAuthoringResolver.FindClip(
                    resolved.Model, clipValue);

        if (!string.IsNullOrWhiteSpace(clipValue) &&
            selectedClip == null)
        {
            DrawStaleAnimationValueWarning(
                "clip", clipValue, "selected animation model");
        }

        DrawAnimationSignalMetadataPicker(
            instruction, signalArgument, signalLabel, selectedClip);

        if (includePayload)
        {
            DrawValueArgument(
                instruction, "payload", "Payload (blank = Any)",
                VariableType.String, EventValue.String(string.Empty),
                state, false);
        }
    }

    private void DrawAnimationSignalManualFallback(
        VisualInstruction instruction,
        string signalArgument,
        string signalLabel,
        bool includePayload,
        EditorState? state,
        string message)
    {
        DrawValueArgument(
            instruction, "clip", "Animation Clip (manual)",
            VariableType.String, EventValue.String(string.Empty),
            state, false);
        DrawValueArgument(
            instruction, signalArgument, $"{signalLabel} (manual)",
            VariableType.String, EventValue.String(string.Empty),
            state, false);

        if (includePayload)
        {
            DrawValueArgument(
                instruction, "payload", "Payload (blank = Any)",
                VariableType.String, EventValue.String(string.Empty),
                state, false);
        }

        ImGui.TextWrapped(
            $"{message} Manual filters will still work at runtime.");
    }

    private void DrawDisabledAnimationSignalFields(
        VisualInstruction instruction,
        string signalArgument,
        string signalLabel,
        bool includePayload,
        EditorState? state,
        string message)
    {
        ImGui.BeginDisabled();
        try
        {
            DrawValueArgument(
                instruction, "clip", "Animation Clip",
                VariableType.String, EventValue.String(string.Empty),
                state, false);
            DrawValueArgument(
                instruction, signalArgument, signalLabel,
                VariableType.String, EventValue.String(string.Empty),
                state, false);
        }
        finally
        {
            ImGui.EndDisabled();
        }

        if (includePayload)
        {
            DrawValueArgument(
                instruction, "payload", "Payload (blank = Any)",
                VariableType.String, EventValue.String(string.Empty),
                state, false);
        }

        ImGui.TextColored(
            new Vector4(1.0f, 0.72f, 0.28f, 1.0f),
            message);
    }

    private string DrawAnimationSignalClipPicker(
        VisualInstruction instruction,
        ModelAsset model)
    {
        EventValue value = EnsureAnimationSignalString(instruction, "clip");

        if (value.Kind != EventValueKind.Constant)
        {
            DrawValueArgument(
                instruction, "clip", "Animation Clip",
                VariableType.String, EventValue.String(string.Empty),
                null, false);
            ImGui.TextDisabled(
                "Dynamic clip reference; metadata choices are unavailable.");
            return string.Empty;
        }

        string current = value.Constant.String;
        ImportedAnimation? selected =
            string.IsNullOrWhiteSpace(current)
                ? null
                : AnimationSignalAuthoringResolver.FindClip(model, current);
        string preview =
            string.IsNullOrWhiteSpace(current)
                ? "Any Clip"
                : selected?.Name ?? current;

        ImGui.TextDisabled("Animation Clip");
        ImGui.SetNextItemWidth(-1.0f);

        if (ImGui.BeginCombo("##AnimationSignalClip", preview))
        {
            if (ImGui.Selectable(
                    "Any Clip",
                    string.IsNullOrWhiteSpace(current)))
            {
                SetAnimationSignalString(
                    instruction, "clip", string.Empty,
                    "Change Animation Clip");
                current = string.Empty;
            }

            foreach (ImportedAnimation animation in model.Animations)
            {
                if (string.IsNullOrWhiteSpace(animation.Key) &&
                    string.IsNullOrWhiteSpace(animation.Name))
                {
                    continue;
                }

                string stored =
                    string.IsNullOrWhiteSpace(animation.Key)
                        ? animation.Name
                        : animation.Key;
                bool isSelected =
                    string.Equals(current, animation.Key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(current, animation.Name, StringComparison.OrdinalIgnoreCase);

                if (ImGui.Selectable(
                        $"{animation.Name}##clip:{animation.Key}",
                        isSelected))
                {
                    SetAnimationSignalString(
                        instruction, "clip", stored,
                        "Change Animation Clip");
                    current = stored;
                }

                DrawFullValueTooltip(animation.Name);
            }

            ImGui.EndCombo();
        }

        DrawFullValueTooltip(preview);
        return current;
    }

    private void DrawAnimationSignalMetadataPicker(
        VisualInstruction instruction,
        string argumentName,
        string label,
        ImportedAnimation? selectedClip)
    {
        EventValue value =
            EnsureAnimationSignalString(instruction, argumentName);

        if (value.Kind != EventValueKind.Constant)
        {
            DrawValueArgument(
                instruction, argumentName, label,
                VariableType.String, EventValue.String(string.Empty),
                null, false);
            return;
        }

        string current = value.Constant.String;
        bool isEvent =
            string.Equals(argumentName, "event", StringComparison.Ordinal);
        string anyLabel = isEvent ? "Any Event" : "Any Window";
        string preview =
            string.IsNullOrWhiteSpace(current) ? anyLabel : current;

        ImGui.TextDisabled(label);
        ImGui.BeginDisabled(selectedClip == null);
        try
        {
            ImGui.SetNextItemWidth(-1.0f);

            if (ImGui.BeginCombo(
                    $"##AnimationSignal:{argumentName}",
                    preview))
            {
                if (ImGui.Selectable(
                        anyLabel,
                        string.IsNullOrWhiteSpace(current)))
                {
                    SetAnimationSignalString(
                        instruction, argumentName, string.Empty,
                        $"Change Animation {label}");
                    current = string.Empty;
                }

                if (selectedClip != null && isEvent)
                {
                    foreach (AnimationEventMarker marker
                             in selectedClip.Events.OrderBy(item => item.Time))
                    {
                        string display =
                            $"{marker.Name}    {marker.Time:0.###}s";

                        if (ImGui.Selectable(
                                $"{display}##event:{marker.Id}",
                                string.Equals(
                                    current, marker.Name,
                                    StringComparison.OrdinalIgnoreCase)))
                        {
                            SetAnimationSignalString(
                                instruction, argumentName, marker.Name,
                                "Change Animation Event");
                            current = marker.Name;
                        }

                        DrawFullValueTooltip(display);
                    }
                }
                else if (selectedClip != null)
                {
                    foreach (AnimationWindow window
                             in selectedClip.Windows.OrderBy(item => item.StartTime))
                    {
                        string display =
                            $"{window.Name}    {window.StartTime:0.###} - {window.EndTime:0.###}s";

                        if (ImGui.Selectable(
                                $"{display}##window:{window.Id}",
                                string.Equals(
                                    current, window.Name,
                                    StringComparison.OrdinalIgnoreCase)))
                        {
                            SetAnimationSignalString(
                                instruction, argumentName, window.Name,
                                "Change Animation Window");
                            current = window.Name;
                        }

                        DrawFullValueTooltip(display);
                    }
                }

                ImGui.EndCombo();
            }
        }
        finally
        {
            ImGui.EndDisabled();
        }

        DrawFullValueTooltip(preview);

        if (selectedClip == null)
        {
            ImGui.TextDisabled(
                "Select a concrete Animation Clip to browse metadata.");
            return;
        }

        bool found =
            isEvent
                ? AnimationSignalAuthoringResolver.ContainsEvent(
                    selectedClip, current)
                : AnimationSignalAuthoringResolver.ContainsWindow(
                    selectedClip, current);

        if (!found)
        {
            DrawStaleAnimationValueWarning(
                label.ToLowerInvariant(), current, "selected clip");
        }
    }

    private EventValue EnsureAnimationSignalString(
        VisualInstruction instruction,
        string argumentName)
    {
        if (!instruction.Arguments.TryGetValue(
                argumentName,
                out EventValue? value) ||
            value == null)
        {
            value = EventValue.String(string.Empty);
            instruction.Arguments[argumentName] = value;
        }

        if (value.Kind == EventValueKind.Constant &&
            value.Constant.Type != VariableType.String)
        {
            value = EventValue.String(string.Empty);
            instruction.Arguments[argumentName] = value;
        }

        return value;
    }

    private void SetAnimationSignalString(
        VisualInstruction instruction,
        string argumentName,
        string value,
        string historyLabel)
    {
        RecordHistory(historyLabel);
        instruction.Arguments[argumentName] = EventValue.String(value);
        _dirty = true;
    }

    private static void DrawStaleAnimationValueWarning(
        string valueKind,
        string value,
        string owner)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            ImGui.TextColored(
                new Vector4(1.0f, 0.72f, 0.28f, 1.0f),
                $"{value} - Not found on {owner} ({valueKind}).");
        }
    }

    private static void DrawFullValueTooltip(string value)
    {
        if (ImGui.IsItemHovered() &&
            !string.IsNullOrWhiteSpace(value))
        {
            ImGui.SetTooltip(value);
        }
    }

    // ========================================================
    // ANIMATION CLIP / PROFILE PICKERS
    // ========================================================

    private void DrawAnimationLayerArgument(
        VisualInstruction instruction,
        EditorState? state)
    {
        const string label = "Layer";
        GameObject? target =
            state != null
                ? ResolveAnimationTargetForEditor(instruction, state)
                : null;
        AnimationController? controller =
            AnimationController.FindForObject(target);

        if (_project == null ||
            controller == null ||
            controller.AnimationProfile.IsEmpty)
        {
            DrawValueArgument(
                instruction,
                "layer",
                label + " (manual)",
                VariableType.String,
                EventValue.String(string.Empty),
                state,
                false);
            return;
        }

        AnimationProfile profile;
        try
        {
            profile = _project.Assets.LoadAnimationProfile(controller.AnimationProfile);
        }
        catch
        {
            DrawValueArgument(
                instruction,
                "layer",
                label + " (manual)",
                VariableType.String,
                EventValue.String(string.Empty),
                state,
                false);
            return;
        }

        EventValue value = EnsureAnimationSignalString(instruction, "layer");
        if (value.Kind != EventValueKind.Constant)
        {
            DrawValueArgument(
                instruction,
                "layer",
                label,
                VariableType.String,
                EventValue.String(string.Empty),
                state,
                false);
            return;
        }

        string current = value.Constant.String;
        AnimationLayerProfile[] layers =
            profile.Layers
                .Where(layer => !string.IsNullOrWhiteSpace(layer.Name))
                .ToArray();

        ImGui.TextDisabled(label);
        ImGui.SetNextItemWidth(-1.0f);

        string preview = string.IsNullOrWhiteSpace(current)
            ? "Select Layer"
            : current;

        if (ImGui.BeginCombo("##AnimationLayer", preview))
        {
            if (ImGui.Selectable("<None>", string.IsNullOrWhiteSpace(current)))
            {
                SetAnimationSignalString(
                    instruction,
                    "layer",
                    string.Empty,
                    "Change Animation Layer");
                current = string.Empty;
            }

            if (layers.Length > 0)
            {
                ImGui.Separator();
            }

            for (int i = 0; i < layers.Length; i++)
            {
                AnimationLayerProfile layer = layers[i];
                bool selected = string.Equals(
                    current,
                    layer.Name,
                    StringComparison.OrdinalIgnoreCase);
                string status = layer.Enabled ? string.Empty : "  (Default Off)";

                if (ImGui.Selectable(
                        $"{layer.Name}{status}##animationLayer:{i}",
                        selected))
                {
                    SetAnimationSignalString(
                        instruction,
                        "layer",
                        layer.Name,
                        "Change Animation Layer");
                    current = layer.Name;
                }

                if (ImGui.IsItemHovered())
                {
                    string clip = string.IsNullOrWhiteSpace(layer.Clip)
                        ? "<No Clip>"
                        : layer.Clip;
                    ImGui.SetTooltip(
                        $"Clip: {clip}\nMode: {layer.BlendMode}\nMask: {layer.Mask.Kind}\nBlend In: {layer.BlendIn:0.###}s\nBlend Out: {layer.BlendOut:0.###}s");
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            if (layers.Length == 0)
            {
                ImGui.TextDisabled("The target Animation Profile has no animation layers.");
            }

            ImGui.EndCombo();
        }

        if (!string.IsNullOrWhiteSpace(current) &&
            !layers.Any(layer => string.Equals(
                layer.Name,
                current,
                StringComparison.OrdinalIgnoreCase)))
        {
            DrawStaleAnimationValueWarning(
                "layer",
                current,
                "Animation Profile");
        }
    }

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
