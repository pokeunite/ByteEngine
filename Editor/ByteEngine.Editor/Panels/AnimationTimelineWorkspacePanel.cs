using System.Numerics;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class AnimationTimelineWorkspacePanel : IDisposable
{
    private readonly EditorDocumentManager _documents;

    public AnimationTimelineWorkspacePanel(EditorDocumentManager documents)
    {
        _documents = documents;
    }

    private readonly AnimationClipPreview _preview = new();

    private EditorProjectContext? _project;
    private AnimationTimelineDocument? _document;
    private ImportedAnimation? _resolvedAnimation;
    private AssetRecord? _resolvedAsset;
    private ModelAsset? _resolvedModel;
    private PendingOpen? _pendingOpen;
    private bool _pendingClose;
    private bool _openUnsavedPopup;
    private bool _visible;
    private bool _focusNextDraw;
    private string? _error;
    private Guid _selectedEventId;
    private Guid _selectedWindowId;
    private TimelineDragKind _dragCandidate;
    private Guid _dragItemId;
    private float _dragStartMouseX;
    private float _dragOriginalStart;
    private float _dragOriginalEnd;
    private bool _dragActive;
    private EditorDocumentId? _registeredDocumentId;

    public bool HasUnsavedChanges => _document?.IsDirty == true;

    public void Open(
        AssetRecord asset,
        ImportedAnimation animation,
        EditorProjectContext project,
        EditorLog log)
    {
        if (asset.Type != AssetType.Model3D || string.IsNullOrWhiteSpace(animation.Key))
            return;

        if (_document != null &&
            _document.ModelGuid == asset.Guid &&
            string.Equals(_document.AnimationKey, animation.Key, StringComparison.Ordinal))
        {
            _visible = true;
            _focusNextDraw = true;
            return;
        }

        var request = new PendingOpen(asset.Guid, asset.ProjectPath, animation.Key);
        if (HasUnsavedChanges)
        {
            _pendingOpen = request;
            _pendingClose = false;
            _openUnsavedPopup = true;
            return;
        }

        OpenNow(request, project, log);
    }

    public void Draw(
        EditorLog log,
        Renderer2D renderer,
        Renderer3D renderer3D,
        int windowWidth,
        int windowHeight)
    {
        if (_visible && _document != null && _project != null)
            DrawWorkspace(log, renderer, renderer3D, windowWidth, windowHeight);

        DrawUnsavedPopup(log);
    }

    public bool Save(EditorLog log)
    {
        if (_project == null || _document == null ||
            !ResolveCurrent(log, refreshCleanDocument: false) ||
            _resolvedAnimation == null)
        {
            return false;
        }

        try
        {
            _document.ApplyTo(_resolvedAnimation);
            ModelAnimationMetadataStore.Save(
                _project.ProjectRoot,
                _document.ModelGuid,
                _resolvedAnimation);
            _document.MarkSaved();
            _error = null;
            log.Info($"Saved animation timeline '{_resolvedAnimation.Name}'.");
            return true;
        }
        catch (Exception exception)
        {
            _error = exception.Message;
            log.Error($"Could not save animation timeline: {exception.Message}");
            return false;
        }
    }

    public void DiscardUnsavedChanges(EditorLog log)
    {
        if (_document == null || _project == null) return;
        ResolveCurrent(log, refreshCleanDocument: false);
        if (_resolvedAnimation != null)
        {
            _document.Reload(_resolvedAnimation);
            ClearSelection();
        }
    }

    public void Dispose()
    {
        if (_registeredDocumentId.HasValue)
            _documents.Unregister(_registeredDocumentId.Value);
        _preview.Dispose();
    }

    private void OpenNow(
        PendingOpen request,
        EditorProjectContext project,
        EditorLog log)
    {
        _project = project;
        _visible = true;
        _focusNextDraw = true;
        _error = null;
        _resolvedAnimation = null;
        _resolvedAsset = null;
        _resolvedModel = null;
        _preview.Reset();
        ClearSelection();

        if (!project.AssetDatabase.TryGetAsset(request.ModelGuid, out AssetRecord? asset) ||
            asset?.Type != AssetType.Model3D)
        {
            _document = null;
            _error = "The owner model asset is no longer available.";
            return;
        }

        try
        {
            ModelAsset model = project.Assets.LoadModel(
                new AssetReference(asset.Guid, asset.ProjectPath));
            ImportedAnimation? animation =
                AnimationTimelineDocument.ResolveAnimation(model, request.AnimationKey);
            if (animation == null)
            {
                _document = null;
                _error = "The animation no longer exists on the model.";
                return;
            }

            _document = new AnimationTimelineDocument(asset.Guid, animation);
            _resolvedAsset = asset;
            _resolvedModel = model;
            _resolvedAnimation = animation;
            RegisterDocument(log);
        }
        catch (Exception exception)
        {
            _document = null;
            _error = exception.Message;
            log.Error($"Could not open animation timeline: {exception.Message}");
        }
    }

    private void DrawWorkspace(
        EditorLog log,
        Renderer2D renderer,
        Renderer3D renderer3D,
        int windowWidth,
        int windowHeight)
    {
        AnimationTimelineDocument document = _document!;
        if (_focusNextDraw)
        {
            ImGui.SetNextWindowFocus();
            _focusNextDraw = false;
        }

        bool open = true;
        bool visible = ImGui.Begin(
            $"Animation: {document.AnimationName}{(document.IsDirty ? " *" : string.Empty)}###AnimationTimelineWorkspace",
            ref open,
            ImGuiWindowFlags.MenuBar);
        if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) &&
            _registeredDocumentId.HasValue)
            _documents.Activate(_registeredDocumentId.Value);

        if (visible)
        {
            DrawMenuBar(log);
            bool resolved = ResolveCurrent(log, refreshCleanDocument: true);
            DrawCommandBar(document, log);

            if (!resolved || _resolvedAsset == null || _resolvedModel == null || _resolvedAnimation == null)
            {
                ImGui.TextColored(new Vector4(1f, .38f, .32f, 1f),
                    _error ?? "Animation is unavailable.");
            }
            else
            {
                _preview.Draw(
                    _project!,
                    _resolvedAsset,
                    _resolvedModel,
                    _resolvedAnimation,
                    renderer,
                    renderer3D,
                    windowWidth,
                    windowHeight);

                DrawScrubber(document);
                DrawTimeline(document);
            }
        }

        ImGui.End();

        if (!open)
            RequestClose();
    }

    private void DrawMenuBar(EditorLog log)
    {
        if (!ImGui.BeginMenuBar()) return;

        if (ImGui.BeginMenu("File"))
        {
            ImGui.BeginDisabled(!HasUnsavedChanges);
            if (ImGui.MenuItem("Save", "Ctrl+S")) Save(log);
            if (ImGui.MenuItem("Revert")) DiscardUnsavedChanges(log);
            ImGui.EndDisabled();
            ImGui.Separator();
            if (ImGui.MenuItem("Close")) RequestClose();
            if (_registeredDocumentId.HasValue)
            {
                if (ImGui.MenuItem("Close Others"))
                    _documents.RequestCloseOthers(_registeredDocumentId.Value);
                if (ImGui.MenuItem("Close All")) _documents.RequestCloseAll();
            }
            ImGui.EndMenu();
        }
        ImGui.EndMenuBar();
    }

    private void DrawCommandBar(AnimationTimelineDocument document, EditorLog log)
    {
        EditorUi.BeginToolbar("##AnimationCommandBar");

        ImGui.BeginDisabled(!document.IsDirty);
        if (document.IsDirty)
            EditorUi.PrimaryButton("Save", () => Save(log));
        else
            EditorUi.ToolbarButton("Save", "Save animation metadata (Ctrl+S)");
        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.BeginDisabled(!document.IsDirty);
        if (EditorUi.ToolbarButton("Revert", "Discard unsaved animation metadata changes"))
            DiscardUnsavedChanges(log);
        ImGui.EndDisabled();

        EditorUi.ToolbarSeparator();
        _preview.DrawPlaybackControls();

        if (document.IsDirty)
        {
            ImGui.SameLine();
            EditorUi.StatusBadge("UNSAVED", EditorStatusKind.Warning);
        }

        EditorUi.EndToolbar();

        if (!string.IsNullOrWhiteSpace(_error))
            ImGui.TextColored(EditorTheme.Error, _error);

        if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) &&
            ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.S))
        {
            Save(log);
        }
    }

    private void DrawScrubber(AnimationTimelineDocument document)
    {
        float playhead = Math.Clamp(_preview.PlaybackTime, 0.0f, document.Duration);
        ImGui.SetNextItemWidth(-1.0f);
        if (ImGui.SliderFloat("##AnimationPlayhead", ref playhead, 0.0f,
                Math.Max(document.Duration, 0.0001f), $"{playhead:0.000} / {document.Duration:0.000} s"))
        {
            _preview.Seek(document.ClampTime(playhead));
        }
    }

    private void DrawTimeline(AnimationTimelineDocument document)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, EditorTheme.BackgroundRaised);
        ImGui.BeginChild("##AnimationTimelineRegion", new Vector2(0.0f, 184.0f), ImGuiChildFlags.Borders);

        ImGui.TextColored(EditorTheme.TextSecondary, "TIMELINE");

        ImGui.SameLine();
        if (EditorUi.SecondaryButton("+ Event"))
        {
            AnimationEventMarker marker = document.AddEvent(_preview.PlaybackTime);
            _selectedEventId = marker.Id;
            _selectedWindowId = Guid.Empty;
        }
        ImGui.SameLine();
        if (EditorUi.SecondaryButton("+ Window"))
        {
            AnimationWindow window = document.AddWindow(_preview.PlaybackTime);
            _selectedWindowId = window.Id;
            _selectedEventId = Guid.Empty;
        }
        EditorUi.Tooltip("Create an animation window at the current playhead");

        float width = Math.Max(ImGui.GetContentRegionAvail().X, 1.0f);
        const float height = 132.0f;
        Vector2 origin = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##AnimationTimelineCanvas", new Vector2(width, height));
        bool canvasHovered = ImGui.IsItemHovered();
        Vector2 mouse = ImGui.GetMousePos();
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        uint background = ImGui.GetColorU32(EditorTheme.Background);
        uint grid = ImGui.GetColorU32(EditorTheme.Border);
        uint text = ImGui.GetColorU32(EditorTheme.TextMuted);
        uint eventColor = ImGui.GetColorU32(EditorTheme.AccentHover);
        uint windowColor = ImGui.GetColorU32(EditorTheme.Warning with { W = 0.68f });
        uint selectedColor = ImGui.GetColorU32(EditorTheme.SelectionHover);
        uint hoverColor = ImGui.GetColorU32(EditorTheme.AccentHover);
        draw.AddRectFilled(origin, origin + new Vector2(width, height), background, 4f);

        const float trackGutter = 70.0f;
        float axisLeft = origin.X + trackGutter;
        float axisRight = origin.X + width - 12f;
        float axisWidth = Math.Max(axisRight - axisLeft, 1f);
        float rulerY = origin.Y + 25f;
        float eventY = origin.Y + 62f;
        float windowY = origin.Y + 101f;
        draw.AddLine(new Vector2(axisLeft - 8.0f, origin.Y),
            new Vector2(axisLeft - 8.0f, origin.Y + height), grid);
        draw.AddText(origin + new Vector2(8f, 52f), text, "Events");
        draw.AddText(origin + new Vector2(8f, 91f), text, "Windows");

        for (int tick = 0; tick <= 10; tick++)
        {
            float x = axisLeft + axisWidth * tick / 10f;
            draw.AddLine(new Vector2(x, rulerY - 5f), new Vector2(x, rulerY + 5f), grid);
            if (tick % 2 == 0)
                draw.AddText(new Vector2(x - 8f, origin.Y + 4f), text,
                    (document.Duration * tick / 10f).ToString("0.00"));
        }
        draw.AddLine(new Vector2(axisLeft, rulerY), new Vector2(axisRight, rulerY), grid, 2f);

        float ToX(float time) => axisLeft + axisWidth *
            (document.Duration > 0.00001f ? document.ClampTime(time) / document.Duration : 0f);

        Guid hoveredId = Guid.Empty;
        TimelineDragKind hoveredKind = TimelineDragKind.None;
        foreach (AnimationEventMarker marker in document.Events)
        {
            float x = ToX(marker.Time);
            bool hovered = canvasHovered && MathF.Abs(mouse.X - x) <= 8f &&
                mouse.Y >= eventY - 14f && mouse.Y <= eventY + 15f;
            if (hovered) { hoveredId = marker.Id; hoveredKind = TimelineDragKind.Event; }
            uint color = marker.Id == _selectedEventId ? selectedColor : hovered ? hoverColor : eventColor;
            draw.AddLine(new Vector2(x, rulerY + 7f), new Vector2(x, eventY + 10f), color, 2f);
            draw.AddTriangleFilled(new Vector2(x, eventY - 8f), new Vector2(x - 7f, eventY + 4f),
                new Vector2(x + 7f, eventY + 4f), color);
            if (x + 10f < axisRight)
                draw.AddText(new Vector2(x + 8f, eventY - 9f), color, TimelineLabel(marker.Name, 18));
        }

        foreach (AnimationWindow window in document.Windows)
        {
            float left = ToX(window.StartTime);
            float right = Math.Max(ToX(window.EndTime), left + 2f);
            bool inLane = canvasHovered && mouse.Y >= windowY - 14f && mouse.Y <= windowY + 14f;
            bool leftHovered = inLane && MathF.Abs(mouse.X - left) <= 7f;
            bool rightHovered = inLane && MathF.Abs(mouse.X - right) <= 7f;
            bool bodyHovered = inLane && mouse.X >= left && mouse.X <= right;
            if (leftHovered) { hoveredId = window.Id; hoveredKind = TimelineDragKind.WindowStart; }
            else if (rightHovered) { hoveredId = window.Id; hoveredKind = TimelineDragKind.WindowEnd; }
            else if (bodyHovered) { hoveredId = window.Id; hoveredKind = TimelineDragKind.WindowBody; }
            bool hovered = hoveredId == window.Id && hoveredKind != TimelineDragKind.Event;
            uint color = window.Id == _selectedWindowId ? selectedColor : hovered ? hoverColor : windowColor;
            draw.AddRectFilled(new Vector2(left, windowY - 9f), new Vector2(right, windowY + 9f), color, 3f);
            draw.AddLine(new Vector2(left, windowY - 12f), new Vector2(left, windowY + 12f), color, 3f);
            draw.AddLine(new Vector2(right, windowY - 12f), new Vector2(right, windowY + 12f), color, 3f);
            if (right - left > 34f)
                draw.AddText(new Vector2(left + 5f, windowY - 8f),
                    ImGui.GetColorU32(EditorTheme.Background),
                    TimelineLabel(window.Name, Math.Max((int)((right - left - 10f) / 7f), 4)));
        }

        float playheadX = ToX(_preview.PlaybackTime);
        draw.AddLine(new Vector2(playheadX, rulerY), new Vector2(playheadX, origin.Y + height - 5f),
            ImGui.GetColorU32(EditorTheme.Accent), 2f);
        draw.AddTriangleFilled(new Vector2(playheadX, rulerY), new Vector2(playheadX - 5f, rulerY - 7f),
            new Vector2(playheadX + 5f, rulerY - 7f), ImGui.GetColorU32(EditorTheme.Accent));

        if (canvasHovered)
        {
            float hoverTime = document.Duration * Math.Clamp((mouse.X - axisLeft) / axisWidth, 0f, 1f);
            draw.AddLine(new Vector2(mouse.X, rulerY), new Vector2(mouse.X, origin.Y + height - 5f),
                ImGui.GetColorU32(EditorTheme.TextMuted with { W = 0.18f }));
            if (!_dragActive)
            {
                if (hoveredKind == TimelineDragKind.Event)
                {
                    AnimationEventMarker? hoveredEvent = document.Events.FirstOrDefault(item => item.Id == hoveredId);
                    if (hoveredEvent != null) ImGui.SetTooltip($"{hoveredEvent.Name}\n{hoveredEvent.Time:0.000} s");
                }
                else if (hoveredKind != TimelineDragKind.None)
                {
                    AnimationWindow? hoveredWindow = document.Windows.FirstOrDefault(item => item.Id == hoveredId);
                    if (hoveredWindow != null)
                        ImGui.SetTooltip($"{hoveredWindow.Name}\n{hoveredWindow.StartTime:0.000}–{hoveredWindow.EndTime:0.000} s");
                }
                else
                {
                    ImGui.SetTooltip($"{hoverTime:0.000} s");
                }
            }
            if (hoveredKind is TimelineDragKind.WindowStart or TimelineDragKind.WindowEnd)
                ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
            else if (hoveredKind != TimelineDragKind.None)
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            _dragCandidate = hoveredKind;
            _dragItemId = hoveredId;
            _dragStartMouseX = mouse.X;
            _dragActive = false;
            if (hoveredKind == TimelineDragKind.Event)
            {
                AnimationEventMarker marker = document.Events.First(item => item.Id == hoveredId);
                _selectedEventId = marker.Id;
                _selectedWindowId = Guid.Empty;
                _dragOriginalStart = marker.Time;
                _dragOriginalEnd = marker.Time;
            }
            else if (hoveredKind != TimelineDragKind.None)
            {
                AnimationWindow window = document.Windows.First(item => item.Id == hoveredId);
                _selectedWindowId = window.Id;
                _selectedEventId = Guid.Empty;
                _dragOriginalStart = window.StartTime;
                _dragOriginalEnd = window.EndTime;
            }
            else
            {
                _preview.Seek(document.Duration * Math.Clamp((mouse.X - axisLeft) / axisWidth, 0f, 1f));
            }
        }

        if (_dragCandidate != TimelineDragKind.None && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            float delta = mouse.X - _dragStartMouseX;
            _dragActive |= MathF.Abs(delta) >= AnimationTimelineInteraction.DragThreshold;
            if (_dragActive)
            {
                if (_dragCandidate == TimelineDragKind.Event)
                {
                    AnimationEventMarker? marker = document.Events.FirstOrDefault(item => item.Id == _dragItemId);
                    if (marker != null)
                    {
                        marker.Time = AnimationTimelineInteraction.MoveEvent(
                            _dragOriginalStart, delta, axisWidth, document.Duration);
                        document.MarkDirty();
                    }
                }
                else
                {
                    AnimationWindow? window = document.Windows.FirstOrDefault(item => item.Id == _dragItemId);
                    if (window != null)
                    {
                        (float start, float end) = _dragCandidate switch
                        {
                            TimelineDragKind.WindowStart => AnimationTimelineInteraction.ResizeWindowStart(
                                _dragOriginalStart, _dragOriginalEnd, delta, axisWidth, document.Duration),
                            TimelineDragKind.WindowEnd => AnimationTimelineInteraction.ResizeWindowEnd(
                                _dragOriginalStart, _dragOriginalEnd, delta, axisWidth, document.Duration),
                            _ => AnimationTimelineInteraction.MoveWindow(
                                _dragOriginalStart, _dragOriginalEnd, delta, axisWidth, document.Duration)
                        };
                        window.StartTime = start;
                        window.EndTime = end;
                        document.MarkDirty();
                    }
                }
            }
        }
        else if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            _dragCandidate = TimelineDragKind.None;
            _dragItemId = Guid.Empty;
            _dragActive = false;
        }

        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    public bool DrawContextInspector()
    {
        if (!_visible || _document == null || _resolvedAnimation == null || _resolvedModel == null)
            return false;

        AnimationTimelineDocument document = _document;
        AnimationEventMarker? marker = document.Events.FirstOrDefault(item => item.Id == _selectedEventId);
        AnimationWindow? window = document.Windows.FirstOrDefault(item => item.Id == _selectedWindowId);

        if (marker != null)
        {
            if (EditorUi.SectionHeader("Animation Event"))
            {
                string name = marker.Name;
                string payload = marker.Payload;
                float time = marker.Time;
                bool changed = false;
                EditorUi.PropertyRow("Name", () => changed |= ImGui.InputText("##AnimationEventName", ref name, 128));
                EditorUi.PropertyRow("Time", () => changed |= ImGui.DragFloat("##AnimationEventTime", ref time,
                    .005f, 0f, document.Duration, "%.3f s"));
                EditorUi.PropertyRow("Payload", () => changed |= ImGui.InputText("##AnimationEventPayload", ref payload, 256));
                if (changed)
                {
                    marker.Name = name;
                    marker.Time = document.ClampTime(time);
                    marker.Payload = payload;
                    document.MarkDirty();
                }

                ImGui.Spacing();
                if (EditorUi.DestructiveButton("Delete Event"))
                {
                    document.DeleteEvent(marker.Id);
                    _selectedEventId = Guid.Empty;
                }
            }
            return true;
        }

        if (window != null)
        {
            if (EditorUi.SectionHeader("Animation Window"))
            {
                string name = window.Name;
                string payload = window.Payload;
                float start = window.StartTime;
                float end = window.EndTime;
                bool changed = false;
                EditorUi.PropertyRow("Name", () => changed |= ImGui.InputText("##AnimationWindowName", ref name, 128));
                EditorUi.PropertyRow("Start", () => changed |= ImGui.DragFloat("##AnimationWindowStart", ref start,
                    .005f, 0f, document.Duration, "%.3f s"));
                EditorUi.PropertyRow("End", () => changed |= ImGui.DragFloat("##AnimationWindowEnd", ref end,
                    .005f, 0f, document.Duration, "%.3f s"));
                EditorUi.LabelValue("Duration", $"{Math.Max(end - start, 0f):0.000} s");
                EditorUi.PropertyRow("Payload", () => changed |= ImGui.InputText("##AnimationWindowPayload", ref payload, 256));
                if (changed)
                {
                    window.Name = name;
                    window.StartTime = start;
                    window.EndTime = end;
                    window.Payload = payload;
                    document.MarkDirty();
                }

                ImGui.Spacing();
                if (EditorUi.DestructiveButton("Delete Window"))
                {
                    document.DeleteWindow(window.Id);
                    _selectedWindowId = Guid.Empty;
                }
            }
            return true;
        }

        if (EditorUi.SectionHeader("Animation Clip"))
        {
            EditorUi.LabelValue("Name", document.AnimationName);
            EditorUi.LabelValue("Source Model", Path.GetFileName(_resolvedAsset?.ProjectPath ?? string.Empty));
            EditorUi.LabelValue("Duration", $"{document.Duration:0.000} s");
            EditorUi.LabelValue("Channels", _resolvedAnimation.Channels.Count.ToString());
            int keyframes = _resolvedAnimation.Channels.Sum(channel =>
                (channel.Translation?.Keys.Count ?? 0) +
                (channel.Rotation?.Keys.Count ?? 0) +
                (channel.Scale?.Keys.Count ?? 0));
            EditorUi.LabelValue("Keyframes", keyframes.ToString());
            EditorUi.LabelValue("Skeleton Bones", (_resolvedModel.Skeleton?.Bones.Count ?? 0).ToString());
        }

        if (EditorUi.SectionHeader("Advanced", defaultOpen: false))
        {
            EditorUi.LabelValue("Stable Key", document.AnimationKey);
            EditorUi.LabelValue("Model GUID", document.ModelGuid.ToString());
            EditorUi.LabelValue("Source", _resolvedAsset?.ProjectPath ?? "Unavailable");
        }
        return true;
    }

    private bool ResolveCurrent(EditorLog log, bool refreshCleanDocument)
    {
        if (_project == null || _document == null) return false;

        try
        {
            if (!_project.AssetDatabase.TryGetAsset(_document.ModelGuid, out AssetRecord? asset) ||
                asset?.Type != AssetType.Model3D)
            {
                _error = "The owner model asset is no longer available.";
                return false;
            }

            ModelAsset model = _project.Assets.LoadModel(new AssetReference(asset.Guid, asset.ProjectPath));
            ImportedAnimation? animation =
                AnimationTimelineDocument.ResolveAnimation(model, _document.AnimationKey);
            if (animation == null)
            {
                _error = "The animation no longer exists after model refresh/reimport.";
                return false;
            }

            if (!ReferenceEquals(animation, _resolvedAnimation))
            {
                _preview.Reset();
                if (refreshCleanDocument && !_document.IsDirty)
                    _document.Reload(animation);
            }

            _resolvedAsset = asset;
            _resolvedModel = model;
            _resolvedAnimation = animation;
            _error = null;
            return true;
        }
        catch (Exception exception)
        {
            _error = exception.Message;
            log.Warning($"Animation timeline refresh failed: {exception.Message}");
            return false;
        }
    }

    private void RegisterDocument(EditorLog log)
    {
        if (_document == null) return;
        EditorDocumentId id = new(
            EditorDocumentType.Animation,
            $"{_document.ModelGuid:N}:{_document.AnimationKey}");
        if (_registeredDocumentId.HasValue && _registeredDocumentId.Value != id)
            _documents.Unregister(_registeredDocumentId.Value);
        _registeredDocumentId = id;
        _documents.RegisterOrFocus(new EditorDocument(
            id,
            $"Animation: {_document.AnimationName}",
            () => { _visible = true; _focusNextDraw = true; },
            RequestClose,
            () => Save(log),
            () => DiscardUnsavedChanges(log),
            () => HasUnsavedChanges));
    }

    private void CloseNow()
    {
        _visible = false;
        _preview.Reset();
        if (_registeredDocumentId.HasValue)
        {
            _documents.Unregister(_registeredDocumentId.Value);
            _registeredDocumentId = null;
        }
    }

    private void RequestClose()
    {
        if (HasUnsavedChanges)
        {
            _pendingClose = true;
            _pendingOpen = null;
            _openUnsavedPopup = true;
        }
        else
        {
            CloseNow();
        }
    }

    private void DrawUnsavedPopup(EditorLog log)
    {
        if (_openUnsavedPopup)
        {
            ImGui.OpenPopup("Unsaved Animation Timeline");
            _openUnsavedPopup = false;
        }

        bool open = true;
        if (!ImGui.BeginPopupModal("Unsaved Animation Timeline", ref open,
                ImGuiWindowFlags.AlwaysAutoResize)) return;

        ImGui.TextColored(EditorTheme.Text, "The animation timeline has unsaved changes.");
        ImGui.TextColored(EditorTheme.TextMuted, "Save before continuing?");
        ImGui.Spacing();
        if (EditorUi.PrimaryButton("Save") && Save(log))
        {
            CompletePendingAction(log);
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (EditorUi.DestructiveButton("Discard"))
        {
            DiscardUnsavedChanges(log);
            CompletePendingAction(log);
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (EditorUi.SecondaryButton("Cancel"))
        {
            _pendingOpen = null;
            _pendingClose = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private void CompletePendingAction(EditorLog log)
    {
        PendingOpen? open = _pendingOpen;
        bool close = _pendingClose;
        _pendingOpen = null;
        _pendingClose = false;

        if (open.HasValue && _project != null)
            OpenNow(open.Value, _project, log);
        else if (close)
        {
            CloseNow();
        }
    }

    private void ClearSelection()
    {
        _selectedEventId = Guid.Empty;
        _selectedWindowId = Guid.Empty;
    }

    private readonly record struct PendingOpen(
        Guid ModelGuid,
        string ProjectPath,
        string AnimationKey);
    private static string TimelineLabel(string value, int maximumCharacters)
    {
        maximumCharacters = Math.Max(maximumCharacters, 4);
        if (string.IsNullOrEmpty(value) || value.Length <= maximumCharacters) return value;
        return value[..(maximumCharacters - 3)] + "...";
    }
    private enum TimelineDragKind
    {
        None,
        Event,
        WindowBody,
        WindowStart,
        WindowEnd
    }

}
