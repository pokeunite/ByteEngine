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
    private readonly EditorDocumentManager _documents;

    public EventWorkspacePanel(EditorDocumentManager documents)
    {
        _documents = documents;
    }
    private readonly EventModuleSerializer _serializer =
        new();

    private VisualLogicRegistry? _registryCache;
    private int _registryRevision = -1;
    private VisualLogicRegistry _registry
    {
        get
        {
            int revision = ByteEngine.Core.Plugins.ByteEnginePluginRegistry.Revision;
            if (_registryCache == null || revision != _registryRevision)
            { _registryCache = VisualLogicRegistry.CreateDefault(); _registryRevision = revision; }
            return _registryCache;
        }
    }

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

}
