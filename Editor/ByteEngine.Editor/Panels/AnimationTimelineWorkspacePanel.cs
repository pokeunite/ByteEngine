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
            $"Animation: {document.AnimationName}###AnimationTimelineWorkspace",
            ref open,
            ImGuiWindowFlags.MenuBar);

        if (visible)
        {
            DrawMenuBar(log);
            bool resolved = ResolveCurrent(log, refreshCleanDocument: true);
            DrawHeader(document, log);

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
                DrawSelectedInspector(document);
            }
        }

        ImGui.End();

        if (!open)
            RequestClose();
    }

    private void DrawMenuBar(EditorLog log)
    {
        if (!ImGui.BeginMenuBar()) return;

        ImGui.BeginDisabled(!HasUnsavedChanges);
        if (ImGui.MenuItem("Save", "Ctrl+S")) Save(log);
        ImGui.EndDisabled();

        if (ImGui.MenuItem("Revert")) DiscardUnsavedChanges(log);
        ImGui.Separator();
        if (ImGui.MenuItem("Close")) RequestClose();
        ImGui.EndMenuBar();
    }

    private void DrawHeader(AnimationTimelineDocument document, EditorLog log)
    {
        ImGui.TextColored(EditorTheme.AccentHover, document.AnimationName.ToUpperInvariant());
        ImGui.SameLine();
        ImGui.TextDisabled(document.IsDirty ? "Unsaved changes" : "Saved");
        ImGui.TextDisabled($"Model: {_resolvedAsset?.ProjectPath ?? document.ModelGuid.ToString()}");
        ImGui.TextDisabled($"Animation key: {document.AnimationKey}");

        if (!string.IsNullOrWhiteSpace(_error))
            ImGui.TextColored(new Vector4(1f, .45f, .32f, 1f), _error);

        ImGui.BeginDisabled(!document.IsDirty);
        if (ImGui.Button("Apply & Save")) Save(log);
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.TextDisabled($"{document.Events.Count} event(s)  |  {document.Windows.Count} window(s)");

        if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) &&
            ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.S))
        {
            Save(log);
        }
        ImGui.Separator();
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
        ImGui.SeparatorText("TIMELINE");

        if (ImGui.Button("+ Event"))
        {
            AnimationEventMarker marker = document.AddEvent(_preview.PlaybackTime);
            _selectedEventId = marker.Id;
            _selectedWindowId = Guid.Empty;
        }
        ImGui.SameLine();
        if (ImGui.Button("+ Window"))
        {
            AnimationWindow window = document.AddWindow(_preview.PlaybackTime);
            _selectedWindowId = window.Id;
            _selectedEventId = Guid.Empty;
        }
        ImGui.SameLine();
        ImGui.TextDisabled("Click the ruler to scrub; click markers or ranges to select.");

        float width = Math.Max(ImGui.GetContentRegionAvail().X, 240.0f);
        const float height = 122.0f;
        Vector2 origin = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##AnimationTimelineCanvas", new Vector2(width, height));
        Vector2 mouse = ImGui.GetMousePos();
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        uint background = ImGui.GetColorU32(new Vector4(.075f, .085f, .105f, 1f));
        uint grid = ImGui.GetColorU32(new Vector4(.28f, .31f, .36f, .75f));
        uint text = ImGui.GetColorU32(new Vector4(.72f, .75f, .8f, 1f));
        uint eventColor = ImGui.GetColorU32(EditorTheme.AccentHover);
        uint windowColor = ImGui.GetColorU32(new Vector4(.95f, .58f, .2f, .68f));
        uint selectedColor = ImGui.GetColorU32(new Vector4(1f, .9f, .35f, 1f));
        draw.AddRectFilled(origin, origin + new Vector2(width, height), background, 4f);

        float axisLeft = origin.X + 12f;
        float axisRight = origin.X + width - 12f;
        float axisWidth = Math.Max(axisRight - axisLeft, 1f);
        float rulerY = origin.Y + 24f;
        float eventY = origin.Y + 58f;
        float windowY = origin.Y + 91f;
        draw.AddText(origin + new Vector2(8f, 42f), text, "Events");
        draw.AddText(origin + new Vector2(8f, 76f), text, "Windows");

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

        foreach (AnimationEventMarker marker in document.Events)
        {
            float x = ToX(marker.Time);
            uint color = marker.Id == _selectedEventId ? selectedColor : eventColor;
            draw.AddLine(new Vector2(x, rulerY + 7f), new Vector2(x, eventY + 9f), color, 2f);
            draw.AddTriangleFilled(new Vector2(x, eventY - 7f), new Vector2(x - 6f, eventY + 3f),
                new Vector2(x + 6f, eventY + 3f), color);
            draw.AddText(new Vector2(x + 7f, eventY - 8f), color, marker.Name);
        }

        foreach (AnimationWindow window in document.Windows)
        {
            float left = ToX(window.StartTime);
            float right = Math.Max(ToX(window.EndTime), left + 2f);
            uint color = window.Id == _selectedWindowId ? selectedColor : windowColor;
            draw.AddRectFilled(new Vector2(left, windowY - 8f), new Vector2(right, windowY + 8f), color, 3f);
            draw.AddText(new Vector2(left + 4f, windowY - 7f),
                ImGui.GetColorU32(new Vector4(.08f, .08f, .08f, 1f)), window.Name);
        }

        float playheadX = ToX(_preview.PlaybackTime);
        draw.AddLine(new Vector2(playheadX, rulerY), new Vector2(playheadX, origin.Y + height - 6f),
            ImGui.GetColorU32(new Vector4(.3f, .85f, 1f, 1f)), 2f);

        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            bool selected = false;
            foreach (AnimationEventMarker marker in document.Events)
            {
                if (MathF.Abs(mouse.X - ToX(marker.Time)) <= 7f &&
                    mouse.Y >= eventY - 12f && mouse.Y <= eventY + 14f)
                {
                    _selectedEventId = marker.Id;
                    _selectedWindowId = Guid.Empty;
                    selected = true;
                    break;
                }
            }

            if (!selected)
            {
                foreach (AnimationWindow window in document.Windows)
                {
                    if (mouse.X >= ToX(window.StartTime) && mouse.X <= ToX(window.EndTime) &&
                        mouse.Y >= windowY - 12f && mouse.Y <= windowY + 12f)
                    {
                        _selectedWindowId = window.Id;
                        _selectedEventId = Guid.Empty;
                        selected = true;
                        break;
                    }
                }
            }

            if (!selected)
            {
                float time = document.Duration * Math.Clamp((mouse.X - axisLeft) / axisWidth, 0f, 1f);
                _preview.Seek(time);
            }
        }
    }

    private void DrawSelectedInspector(AnimationTimelineDocument document)
    {
        ImGui.SeparatorText("SELECTED EVENT / WINDOW");
        AnimationEventMarker? marker = document.Events.FirstOrDefault(item => item.Id == _selectedEventId);
        AnimationWindow? window = document.Windows.FirstOrDefault(item => item.Id == _selectedWindowId);

        if (marker == null && window == null)
        {
            ImGui.TextDisabled("Select an event marker or animation window.");
            return;
        }

        if (marker != null)
        {
            string name = marker.Name;
            string payload = marker.Payload;
            float time = marker.Time;
            bool changed = ImGui.InputText("Name", ref name, 128);
            changed |= ImGui.DragFloat("Time", ref time, .005f, 0f, document.Duration, "%.3f s");
            changed |= ImGui.InputText("Payload", ref payload, 256);
            if (changed)
            {
                marker.Name = name;
                marker.Time = document.ClampTime(time);
                marker.Payload = payload;
                document.MarkDirty();
            }
            if (ImGui.Button("Delete Event"))
            {
                document.DeleteEvent(marker.Id);
                _selectedEventId = Guid.Empty;
            }
            return;
        }

        string windowName = window!.Name;
        string windowPayload = window.Payload;
        float start = window.StartTime;
        float end = window.EndTime;
        bool windowChanged = ImGui.InputText("Name", ref windowName, 128);
        windowChanged |= ImGui.DragFloat("Start", ref start, .005f, 0f, document.Duration, "%.3f s");
        windowChanged |= ImGui.DragFloat("End", ref end, .005f, 0f, document.Duration, "%.3f s");
        windowChanged |= ImGui.InputText("Payload", ref windowPayload, 256);
        if (windowChanged)
        {
            window.Name = windowName;
            window.StartTime = start;
            window.EndTime = end;
            window.Payload = windowPayload;
            document.MarkDirty();
        }
        if (ImGui.Button("Delete Window"))
        {
            document.DeleteWindow(window.Id);
            _selectedWindowId = Guid.Empty;
        }
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
            _visible = false;
            _preview.Reset();
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

        ImGui.Text("The animation timeline has unsaved changes.");
        ImGui.Text("Save before continuing?");
        if (ImGui.Button("Save") && Save(log))
        {
            CompletePendingAction(log);
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Discard"))
        {
            DiscardUnsavedChanges(log);
            CompletePendingAction(log);
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
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
            _visible = false;
            _preview.Reset();
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

}
