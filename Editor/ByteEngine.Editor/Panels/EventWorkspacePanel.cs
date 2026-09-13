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

    private readonly GameObjectReferencePicker _objectPicker =
        new();

    private readonly EventModuleHistory _history =
        new();

    private readonly ByteGraphCanvas _graphCanvas =
        new();

    private readonly HashSet<Guid> _selectedGraphNodes =
        new();

    private enum WireDragKind
    {
        None,
        Condition,
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

    private readonly EventNamePopupState _eventNamePopup = new();
    private readonly ByteGraphViewSettingsState _viewSettings = new();

    private AssetRecord? _asset;

    private EditorProjectContext? _project;

    private EventModuleDefinition? _module;

    private bool _open;

    private bool _dirty;

    private bool _requestFocus;
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

        _open =
            open;

        if (!_open)
        {
            _eventNamePopup.Reset();
            _viewSettings.Reset();
        }

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
            _eventNamePopup.Reset();
            _viewSettings.Reset();
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

        DrawGraphCanvas(
            state);

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
    }

    // ========================================================
    // TOOLBAR
    // ========================================================

    private void DrawDocumentToolbar(
        EditorLog log)
    {
        ByteGraphToolbarLayout layout = ByteGraphToolbarLayout.ForWidth(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled("EVENT MODULE");
        ImGui.SameLine();
        ImGui.Text(_module!.Name);
        ImGui.SameLine();

        ImGui.BeginDisabled(!CanUndo);
        if (ImGui.Button("Undo")) Undo();
        if (ImGui.IsItemHovered() && UndoName != null) ImGui.SetTooltip($"Undo {UndoName}");
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(!CanRedo);
        if (ImGui.Button("Redo")) Redo();
        if (ImGui.IsItemHovered() && RedoName != null) ImGui.SetTooltip($"Redo {RedoName}");
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(!_dirty);
        if (ImGui.Button("Save")) Save(log);
        ImGui.EndDisabled();
        ImGui.SameLine();

        if (ImGui.Button("+ Event"))
            RequestAddEvent(GetDefaultRulePosition(_module.Rules.Count));
        ImGui.SameLine();
        ImGui.BeginDisabled(_selectedGraphNodes.Count == 0);
        if (ImGui.Button("+ Comment")) CreateGroupFromSelection();
        ImGui.EndDisabled();
        ImGui.SameLine();

        if (!layout.CollapseSecondary)
        {
            if (ImGui.Button("Arrange"))
            {
                RecordHistory("Auto Arrange Graph");
                AutoArrangeGraph();
                _requestFrameGraph = true;
                _dirty = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("Frame")) _requestFrameGraph = true;
            ImGui.SameLine();
        }

        if (ImGui.Button("-")) _graphCanvas.SetZoom(_graphCanvas.Zoom - .1f);
        ImGui.SameLine();
        ImGui.TextDisabled($"{_graphCanvas.Zoom * 100f:0}%");
        ImGui.SameLine();
        if (ImGui.Button("+")) _graphCanvas.SetZoom(_graphCanvas.Zoom + .1f);
        ImGui.SameLine();

        if (ImGui.Button("View##ByteGraphView")) _viewSettings.Begin(GetNodeScale());
        if (_viewSettings.ConsumeOpenRequest()) ImGui.OpenPopup("View##ByteGraphViewSettings");
        if (ImGui.BeginPopup("View##ByteGraphViewSettings"))
        {
            _viewSettings.MarkVisible();
            float nodeScalePercent = _viewSettings.NodeScale * 100f;
            ImGui.SetNextItemWidth(190f);
            if (ImGui.SliderFloat("Node Scale##ByteGraphView", ref nodeScalePercent,
                    ByteGraphViewSettingsState.MinimumNodeScale * 100f,
                    ByteGraphViewSettingsState.MaximumNodeScale * 100f, "%.0f%%",
                    ImGuiSliderFlags.None))
            {
                _viewSettings.SetNodeScale(nodeScalePercent / 100f);
                _module.EditorNodeScale = _viewSettings.NodeScale;
                _dirty = true;
            }
            if (ImGui.Button("Reset View##ByteGraphView")) _graphCanvas.ResetView();
            ImGui.EndPopup();
        }
        else
        {
            _viewSettings.RecoverWhenNotVisible();
        }
        ImGui.SameLine();

        if (ImGui.Button("...##ByteGraphMore")) ImGui.OpenPopup("More##ByteGraphToolbarMore");
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
            if (ImGui.MenuItem("Reset View")) _graphCanvas.ResetView();
            ImGui.BeginDisabled(_selectedGraphNodes.Count == 0);
            if (ImGui.MenuItem("Delete")) DeleteSelectedGraphNodes();
            ImGui.EndDisabled();
            ImGui.EndPopup();
        }

        if (layout.TraceOnSecondRow)
        {
            ImGui.NewLine();
        }
        else
        {
            ImGui.SameLine();
        }

        DrawTraceLegend();
    }

    private static void DrawTraceChip(string label, VisualLogicTraceState state)
    {
        ImGui.TextColored(GetTraceColor(state), $"● {label}");
    }

    private static void DrawTraceLegend()
    {
        bool liveTrace = VisualLogicDebugTrace.Enabled;
        if (ImGui.Checkbox("Live", ref liveTrace))
        {
            VisualLogicDebugTrace.Enabled = liveTrace;
            if (!liveTrace) VisualLogicDebugTrace.Clear();
        }
        ImGui.SameLine();
        DrawTraceChip("TRUE", VisualLogicTraceState.ConditionTrue);
        ImGui.SameLine();
        DrawTraceChip("FIRED", VisualLogicTraceState.EventTriggered);
        ImGui.SameLine();
        DrawTraceChip("FALSE", VisualLogicTraceState.ConditionFalse);
        ImGui.SameLine();
        DrawTraceChip("BLOCKED", VisualLogicTraceState.EventBlocked);
        ImGui.SameLine();
        DrawTraceChip("ACTION", VisualLogicTraceState.ActionExecuted);
        ImGui.SameLine();
        DrawTraceChip("SKIPPED", VisualLogicTraceState.ActionSkipped);
    }

    private bool CanAutoSave()
    {
        if (_wireDragKind !=
                WireDragKind.None ||
            _draggingGraphNodeId !=
                Guid.Empty ||
            _marqueeSelecting)
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

    private Vector2 GetEventGraphNodeSize()
    {
        return BaseEventGraphNodeSize *
               GetNodeScale();
    }

    private Vector2 GetScaledInstructionNodeSize(
        VisualInstruction instruction)
    {
        return GetInstructionNodeBaseSize(
                   instruction) *
               GetNodeScale();
    }

    private float GetNodeVisualScale()
    {
        return Math.Clamp(
            _graphCanvas.Zoom *
            GetNodeScale(),
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

        UpdateGraphPointerInteraction();
        HandleGraphShortcuts();

        DrawGraphGroupBackgrounds();
        DrawGraphWires();
        DrawWireDragPreview();

        if (_module.Rules.Count ==
            0)
        {
            ImGui.SetCursorScreenPos(
                _graphCanvas.ToScreen(
                    new Vector2(
                        80.0f,
                        80.0f)));

            ImGui.BeginChild(
                "##EmptyGraphHint",
                new Vector2(
                    430.0f,
                    125.0f),
                ImGuiChildFlags.Borders,
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse);

            ImGui.Text(
                "ByteGraph is empty.");

            ImGui.TextDisabled(
                "Right-click the canvas or use '+ Add Event'.");

            ImGui.TextDisabled(
                "Middle mouse pans. Mouse wheel zooms from 25% to 200%.");

            ImGui.TextDisabled(
                "Ctrl-click node headers to multi-select.");

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

                bool deleteRule =
                    DrawEventGraphNode(
                        rule,
                        ruleIndex);

                if (!rule.EditorCollapsed)
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
            GetEventGraphNodeSize();

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

        Vector4 defaultEventBackground =
            new(
                0.075f,
                0.095f,
                0.13f,
                0.98f);

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
                screenSize,
                ImGuiChildFlags.Borders,
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
                $"{rule.Conditions.Count} condition(s) | {rule.Actions.Count} action(s)");

            if (rule.Conditions.Count ==
                0)
            {
                ImGui.TextColored(
                    new Vector4(
                        1.0f,
                        0.72f,
                        0.24f,
                        1.0f),
                    "Every Frame");
            }
            else
            {
                ImGui.TextDisabled(
                    "ALL conditions must be true");
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
        EditorState? state)
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

        Vector4 defaultInstructionBackground =
            condition
                ? new Vector4(
                    0.065f,
                    0.11f,
                    0.14f,
                    0.98f)
                : new Vector4(
                    0.14f,
                    0.095f,
                    0.055f,
                    0.98f);

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

        bool visible =
            ImGui.BeginChild(
                $"InstructionGraphNode##{instruction.InstanceId}",
                _graphCanvas.ScaleSize(
                    logicalSize),
                ImGuiChildFlags.Borders,
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
                    ? $"CONDITION {index + 1:00}"
                    : "ACTION");

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

            ImGui.Selectable(
                $"{displayName}##NodeMoveHandle",
                false,
                ImGuiSelectableFlags.None,
                new Vector2(
                    -1.0f,
                    28.0f *
                    GetNodeVisualScale()));


            ImGui.TextDisabled(
                category);

            ImGui.Separator();

            DrawInstructionArguments(
                instruction,
                state);
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

        if (!condition ||
            logicGate)
        {
            _graphCanvas.DrawPin(
                input,
                pinColor,
                6.0f);
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

    private void DrawGraphWires()
    {
        if (_module ==
            null)
        {
            return;
        }

        foreach (EventRuleDefinition rule
                 in _module.Rules)
        {
            if (rule.EditorCollapsed)
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

                    _graphCanvas.DrawWire(
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
                _graphCanvas.DrawWire(
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

                    _graphCanvas.DrawWire(
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
                    _graphCanvas.DrawWire(
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
                            _graphCanvas.DrawWire(
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
                            _graphCanvas.DrawWire(
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

                _graphCanvas.DrawWire(
                    GetInstructionOutput(
                        action),
                    GetInstructionInput(
                        next),
                    ExecutionWireColor,
                    3.5f);
            }
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
                WireDragKind.Condition =>
                    ConditionWireColor,

                WireDragKind.ActionTrue =>
                    TrueExecutionWireColor,

                WireDragKind.ActionFalse =>
                    FalseExecutionWireColor,

                _ =>
                    ExecutionWireColor
            };

        if (_wireDragKind ==
                WireDragKind.Condition &&
            _wireDragSourceInstructionId ==
                Guid.Empty)
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
        if (_wireDragKind ==
                WireDragKind.Condition &&
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

        EventRuleDefinition? rule =
            FindRule(
                _pendingWireRuleId);

        if (rule ==
            null)
        {
            ImGui.TextDisabled(
                "The source event no longer exists.");

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

            if (rule.EditorCollapsed)
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

        return false;
    }

    private bool TryConnectConditionWireAtMouse()
    {
        EventRuleDefinition? rule =
            FindRule(
                _wireDragRuleId);

        if (rule ==
            null)
        {
            return false;
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

            if (rule.EditorCollapsed)
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

        return result;
    }

    private bool PointInsideNode(
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

            if (rule.EditorCollapsed)
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
    }

    private void TrySelectNodeFromRectangle(
        Guid nodeId,
        Vector2 selectionMinimum,
        Vector2 selectionMaximum)
    {
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

                MemberIds =
                    members
            });

        _dirty =
            true;
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

        return false;
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

        foreach (EventGraphGroupDefinition group
                 in _module.EditorGroups)
        {
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
                new Vector4(
                    0.12f,
                    0.14f,
                    0.18f,
                    0.30f),
                new Vector4(
                    0.48f,
                    0.52f,
                    0.62f,
                    0.70f));
        }
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
                        8.0f));

            Vector2 headerSize =
                new(
                    Math.Max(
                        245.0f *
                        _graphCanvas.Zoom,
                        120.0f),
                    Math.Max(
                        36.0f *
                        _graphCanvas.Zoom,
                        26.0f));

            ImGui.SetCursorScreenPos(
                headerPosition);

            ImGui.PushStyleColor(
                ImGuiCol.ChildBg,
                new Vector4(
                    0.10f,
                    0.12f,
                    0.16f,
                    0.96f));

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

                ImGui.SetNextItemWidth(
                    Math.Max(
                        headerSize.X -
                        52.0f,
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
            }

            ImGui.EndChild();

            ImGui.PopStyleColor();

            if (remove)
            {
                RecordHistory(
                    "Delete Comment Box");

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
            return false;
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
                "object.destroy" =>
                    190.0f,

                "object.setActive" =>
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
            300.0f,
            height);
    }

    private Vector2 GetEventConditionInput(
        EventRuleDefinition rule)
    {
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
    }

    private void FrameEntireGraph()
    {
        if (_module ==
                null ||
            _module.Rules.Count ==
                0)
        {
            _graphCanvas.ResetView();
            return;
        }

        Vector2 minimum =
            new(
                float.MaxValue,
                float.MaxValue);

        Vector2 maximum =
            new(
                float.MinValue,
                float.MinValue);

        foreach (EventRuleDefinition rule
                 in _module.Rules)
        {
            IncludeGraphRect(
                ref minimum,
                ref maximum,
                new Vector2(
                    rule.EditorX,
                    rule.EditorY),
                GetEventGraphNodeSize());

            if (rule.EditorCollapsed)
            {
                continue;
            }

            foreach (VisualInstruction instruction
                     in rule.Conditions.Concat(
                         rule.Actions))
            {
                IncludeGraphRect(
                    ref minimum,
                    ref maximum,
                    new Vector2(
                        instruction.EditorX,
                        instruction.EditorY),
                    GetScaledInstructionNodeSize(
                        instruction));
            }
        }

        foreach (EventGraphGroupDefinition group
                 in _module.EditorGroups)
        {
            if (!TryGetGroupBounds(
                    group,
                    out Vector2 groupMinimum,
                    out Vector2 groupMaximum))
            {
                continue;
            }

            IncludeGraphRect(
                ref minimum,
                ref maximum,
                groupMinimum,
                groupMaximum -
                groupMinimum);
        }

        _graphCanvas.FrameBounds(
            minimum,
            maximum);
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

            case "object.exists":
            case "object.isActive":
            case "object.destroy":
                instruction.Arguments["target"] =
                    EventValue.String(
                        "Self");
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

            case "object.exists":
            case "object.isActive":
            case "object.destroy":
                DrawObjectTargetArgument(
                    instruction,
                    "target",
                    "Target Object",
                    state);
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
                    EventValue.Number(
                        0.0),
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
    // BLUEPRINT ASSET
    // ========================================================

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
