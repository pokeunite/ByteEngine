using System.ComponentModel;
using System.Numerics;

using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Variables;
using ByteEngine.Core.Scene;
using ByteEngine.Editor.Panels;

using ImGuiNET;

using OpenTK.Windowing.Common;

namespace ByteEngine.Editor;

public sealed class EditorApplication
    : ByteEngineApplication
{
    private readonly string? _startupProjectFile;

    private readonly EditorLog _log =
        new();

    private readonly EditorLayout _layout =
        new();

    private readonly HierarchyPanel _hierarchy =
        new();

    private readonly InspectorPanel _inspector =
        new();

    private readonly SceneViewPanel _sceneView =
        new();

    private readonly GameViewPanel _gameView =
        new();

    private readonly ConsolePanel _console =
        new();

    private readonly PerformancePanel _performance =
        new();

    private readonly ProjectSettingsPanel _projectSettings = new();

    private readonly ProjectBrowserPanel _projectBrowser = new();

    private readonly EditorDocumentManager _documents = new();

    private readonly EditorDocumentWindowManager _documentWindows;

    private readonly EditorClipboard _clipboard = new();

    private ImGuiController? _imgui;

    private EditorProjectContext? _projectContext;

    private AssetsPanel? _assets;

    private EditorState? _state;

    private Action? _pendingUnsavedAction;

    private bool _openUnsavedPopup;

    private bool _allowClose;

    private EditorDocumentId? _sceneDocumentId;

    protected override bool CloseOnEscape =>
        false;

    protected override bool ShouldUpdateScene =>
        _state?.Mode ==
        EditorMode.Play;

    protected override bool GameplayInputActionsEnabled =>
        _state?.Mode == EditorMode.Play;

    protected override bool ShouldRenderSceneToWindow =>
        false;

    public EditorApplication(
        string? projectFile)
        : base(
            1600,
            900,
            $"ByteEngine Editor v{ByteEngineInfo.Version}"
        )
    {
        _documentWindows = new EditorDocumentWindowManager(this, _documents, () => _imgui?.MakeCurrent());

        _startupProjectFile =
            string.IsNullOrWhiteSpace(
                projectFile)
                ? null
                : Path.GetFullPath(
                    projectFile
                );
    }

    protected override void OnEngineStart()
    {
        _imgui =
            new ImGuiController(
                this,
                EditorPreferences.LayoutPath
            );

        RuntimeDiagnostics.OutputSink = message => _log.Info(message);

        if (EditorPreferences.EnsureLayoutVersion(
                4))
        {
            _layout.RequestReset();
        }

        if (_startupProjectFile != null)
        {
            OpenProject(
                _startupProjectFile
            );
        }
        else
        {
            _log.Info(
                "Choose New Project or Open Project to begin."
            );
        }
    }

    protected override void OnEngineRender()
    {
        if (_imgui == null)
        {
            return;
        }

        _imgui.Update(
            Math.Max(
                (float)Time.DeltaTime,
                1.0f / 1000.0f
            )
        );

        HandleShortcuts();
        DrawMainMenu();
        _layout.DrawDockSpace();

        if (_state == null)
        {
            _projectBrowser.DrawLanding(CreateProject, ShowOpenProjectDialog, EditorPreferences.ReadLastProject());
        }
        else
        {
            _projectContext?.AssetDatabase.Update();
            DrawPanels();
            _projectBrowser.DrawCreateDialog(CreateProject);
        }

        DrawUnsavedChangesPopup();
        UpdateWindowTitle();

        _imgui.Render();
        _documentWindows.PumpAndRender(Math.Max((float)Time.DeltaTime, 1.0f / 1000.0f));
        _imgui.MakeCurrent();
    }

    protected override void OnTextInput(
        TextInputEventArgs e)
    {
        base.OnTextInput(e);

        _imgui?.AddInputCharacter(
            (uint)e.Unicode
        );
    }

    protected override void OnFileDrop(FileDropEventArgs e)
    {
        base.OnFileDrop(e);
        if (_projectContext == null || _state == null)
        {
            _log.Warning("Open or create a project before dropping asset files into ByteEngine.");
            return;
        }

        string? destinationDirectory =
            _assets?.CurrentImportDirectory;

        IReadOnlyList<AssetRecord> imported =
            new ExternalAssetImporter(
                _projectContext,
                _log)
            .Import(
                e.FileNames,
                destinationDirectory);
        AssetRecord? selected =
            imported.FirstOrDefault();

        if (selected != null &&
            _state.SelectedObject == null)
        {
            _state.SelectedAssetId =
                selected.Guid;

            _state.SelectedAssetPath =
                selected.ProjectPath;
        }
    }

    protected override void OnClosing(
        CancelEventArgs e)
    {
        if (!_allowClose &&
            HasUnsavedChanges)
        {
            e.Cancel =
                true;
            RequestAfterUnsavedCheck(
                RequestClose
            );
        }

        base.OnClosing(e);
    }

    protected override void OnEngineShutdown()
    {
        RuntimeDiagnostics.OutputSink = null;
        _inspector.Dispose();
        _sceneView.Dispose();
        _gameView.Dispose();
        _documentWindows.Dispose();
        _assets?.Dispose();
        _assets = null;
        _projectContext?.Dispose();
        _projectContext = null;

        _imgui?.Dispose();
        _imgui = null;
    }

    private void DrawPanels()
    {
        if (_state == null)
        {
            return;
        }

        if (_hierarchy.IsOpen)
        {
            _hierarchy.Draw(
                _state,
                () => CreateGameObject(
                    "GameObject"
                ),
                DeleteSelectedObject,
                DuplicateSelectedObjects,
                CopySelectedObjects,
                PasteObjects,
                CreateChildObject,
                InstantiateHierarchyAsset
            );
        }

        if (_inspector.IsOpen)
        {
            _inspector.Draw(
                _state,
                _projectContext!,
                Renderer,
                Renderer3D,
                FramebufferSize.X,
                FramebufferSize.Y,
                reference =>
                {
                    AssetRecord? asset =
                        _projectContext!.AssetDatabase.Resolve(
                            reference);

                    if (asset != null)
                    {
                        _documentWindows.OpenBlueprint(asset, _projectContext, Renderer, Renderer3D);
                    }
                },
                () => _assets?.DrawActiveDocumentInspector() == true);
        }
        if (_sceneView.IsOpen)
        {
            _sceneView.Draw(
                _state,
                Renderer,
                Renderer3D,
                FramebufferSize.X,
                FramebufferSize.Y,
                _projectContext!,
                CreateSpriteFromAsset,
                CreateModelFromAsset,
                CreateBlueprintFromAsset,
                position => CreateObjectAtPosition("GameObject", position, null),
                position => CreateObjectAtPosition("Sprite", position, () => new SpriteRenderer()),
                position => CreateObjectAtPosition("Camera", position, () => new Camera2D()),
                PasteObjects
            );
            if (_sceneView.IsFocused && _sceneDocumentId.HasValue)
                _documents.Activate(_sceneDocumentId.Value);
        }

        if (_gameView.IsOpen)
        {
            _gameView.Draw(_state, Renderer, Renderer3D, FramebufferSize.X, FramebufferSize.Y,
                grabCursor => CaptureGameInput(grabCursor), ReleaseGameInput);
        }

        // Draw background bottom-workspace tabs first. Assets is drawn last
        // so its one-shot startup focus request is not immediately stolen by
        // Console or Performance in the same dock node.
        if (_performance.IsOpen)
        {
            _performance.Draw(
                _state,
                _sceneView.IsOpen,
                _gameView.IsOpen
            );
        }

        _projectSettings.Draw(_projectContext!);

        if (_console.IsOpen)
        {
            _console.Draw(
                _log
            );
        }

        if (_assets?.IsOpen == true)
        {
            _assets.Draw(
                _state,
                _log,
                Renderer,
                Renderer3D,
                FramebufferSize.X,
                FramebufferSize.Y
            );
        }
        AssetReference? requestedProfile = AnimationProfileWorkspaceRequest.Consume();
        if (requestedProfile != null &&
            _projectContext!.AssetDatabase.Resolve(requestedProfile) is AssetRecord requestedAsset &&
            requestedAsset.Type == AssetType.AnimationProfile)
        {
            _documentWindows.OpenProfile(requestedAsset, _projectContext, _log);
        }

    }

    private void DrawMainMenu()
    {
        ImGui.PushStyleVar(
            ImGuiStyleVar.FramePadding,
            new Vector2(
                9.0f,
                6.0f));

        if (!ImGui.BeginMainMenuBar())
        {
            ImGui.PopStyleVar();
            return;
        }

        ImGui.TextColored(
            EditorTheme.AccentHover,
            "BYTEENGINE");

        ImGui.SameLine();
        ImGui.TextDisabled("|");
        ImGui.SameLine();

        DrawFileMenu();

        if (_state != null)
        {
            DrawEditMenu();
        }

        DrawPreferencesMenu();

        if (_state != null)
        {
            DrawGameObjectMenu();
            DrawWindowMenu();
            DrawPlayControls();
            DrawEditorStatus();
        }

        ImGui.EndMainMenuBar();
        ImGui.PopStyleVar();
    }

    private void DrawFileMenu()
    {
        if (!ImGui.BeginMenu("File"))
        {
            return;
        }

        if (ImGui.MenuItem(
                "New Project"))
        {
            RequestAfterUnsavedCheck(_projectBrowser.OpenCreateDialog);
        }

        if (ImGui.MenuItem(
                "Open Project"))
        {
            RequestAfterUnsavedCheck(
                ShowOpenProjectDialog
            );
        }

        ImGui.Separator();

        bool hasProject =
            _projectContext != null;

        if (ImGui.MenuItem(
                "New Scene",
                "Ctrl+N",
                false,
                hasProject))
        {
            RequestAfterUnsavedCheck(
                CreateNewScene
            );
        }

        if (ImGui.MenuItem(
                "Open Scene",
                string.Empty,
                false,
                hasProject))
        {
            RequestAfterUnsavedCheck(
                ShowOpenSceneDialog
            );
        }

        if (ImGui.MenuItem(
                "Save Scene",
                "Ctrl+S",
                false,
                _state != null))
        {
            SaveScene();
        }

        if (ImGui.MenuItem(
                "Save Scene As",
                "Ctrl+Shift+S",
                false,
                _state != null))
        {
            SaveSceneAs();
        }

        ImGui.Separator();

        if (ImGui.MenuItem("Exit"))
        {
            RequestAfterUnsavedCheck(
                RequestClose
            );
        }

        ImGui.EndMenu();
    }

    private void DrawEditMenu()
    {
        if (!ImGui.BeginMenu("Edit"))
        {
            return;
        }

        bool eventFocused =
            EventWorkspaceUndoRouter.HasFocusedWorkspace;

        bool canUndo =
            eventFocused
                ? EventWorkspaceUndoRouter.CanUndo
                : _state?.Mode ==
                      EditorMode.Edit &&
                  _state.Undo?.CanUndo ==
                      true;

        bool canRedo =
            eventFocused
                ? EventWorkspaceUndoRouter.CanRedo
                : _state?.Mode ==
                      EditorMode.Edit &&
                  _state.Undo?.CanRedo ==
                      true;

        string undoLabel =
            eventFocused
                ? EventWorkspaceUndoRouter.UndoName is string eventUndo
                    ? $"Undo {eventUndo}"
                    : "Undo"
                : _state?.Undo?.UndoName is string sceneUndo
                    ? $"Undo {sceneUndo}"
                    : "Undo";

        string redoLabel =
            eventFocused
                ? EventWorkspaceUndoRouter.RedoName is string eventRedo
                    ? $"Redo {eventRedo}"
                    : "Redo"
                : _state?.Undo?.RedoName is string sceneRedo
                    ? $"Redo {sceneRedo}"
                    : "Redo";

        if (ImGui.MenuItem(
                undoLabel,
                "Ctrl+Z",
                false,
                canUndo))
        {
            if (eventFocused)
            {
                EventWorkspaceUndoRouter.TryUndo();
            }
            else
            {
                _state!.Undo!.Undo(
                    _state);
            }
        }

        if (ImGui.MenuItem(
                redoLabel,
                "Ctrl+Y",
                false,
                canRedo))
        {
            if (eventFocused)
            {
                EventWorkspaceUndoRouter.TryRedo();
            }
            else
            {
                _state!.Undo!.Redo(
                    _state);
            }
        }

        ImGui.EndMenu();
    }

    private void DrawPreferencesMenu()
    {
        if (!ImGui.BeginMenu(
                "Preferences"))
        {
            return;
        }

        bool enable2D =
            EditorPreferences.Enable2DEditor;

        if (ImGui.MenuItem(
                "Enable 2D Editor",
                string.Empty,
                enable2D))
        {
            EditorPreferences.Enable2DEditor =
                !enable2D;
        }

        bool showFps =
            EditorPreferences.ShowFpsCounter;

        if (ImGui.MenuItem(
                "Show FPS Counter",
                string.Empty,
                showFps))
        {
            EditorPreferences.ShowFpsCounter =
                !showFps;
        }

        ImGui.Separator();

        ImGui.TextDisabled(
            "3D editor is the default workspace.");

        ImGui.EndMenu();
    }

    private void DrawGameObjectMenu()
    {
        if (_state == null ||
            !ImGui.BeginMenu("GameObject"))
        {
            return;
        }

        bool canEdit =
            _state.Mode ==
            EditorMode.Edit;

        if (ImGui.MenuItem(
                "Create Empty",
                string.Empty,
                false,
                canEdit))
        {
            CreateGameObject(
                "GameObject"
            );
        }

        ImGui.Separator();

        if (EditorPreferences.Enable2DEditor &&
            ImGui.BeginMenu("2D", canEdit))
        {
            if (ImGui.MenuItem("Sprite")) CreateObjectWithComponent("Sprite", () => new SpriteRenderer());
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("3D Object", canEdit))
        {
            if (ImGui.MenuItem("Cube")) CreateMeshPrimitive("Cube", PrimitiveMeshType.Cube);
            if (ImGui.MenuItem("Sphere")) CreateMeshPrimitive("Sphere", PrimitiveMeshType.Sphere);
            if (ImGui.MenuItem("Plane")) CreateMeshPrimitive("Plane", PrimitiveMeshType.Plane);
            ImGui.EndMenu();
        }
        if (ImGui.BeginMenu("Gameplay", canEdit))
        {
            if (ImGui.MenuItem("Damage Target")) CreateDamageTarget();
            ImGui.EndMenu();
        }
        if (ImGui.BeginMenu("Camera", canEdit))
        {
            if (EditorPreferences.Enable2DEditor &&
                ImGui.MenuItem("Camera 2D"))
            {
                CreateObjectWithComponent(
                    "Camera 2D",
                    () => new Camera2D());
            }

            if (ImGui.MenuItem("Camera 3D"))
            {
                CreateObjectWithComponent(
                    "Camera 3D",
                    () => new Camera3D());
            }

            ImGui.EndMenu();
        }
        if (ImGui.BeginMenu("Light", canEdit))
        {
            if (ImGui.MenuItem("Directional Light"))
            {
                CreateObjectWithComponent(
                    "Directional Light",
                    () => new DirectionalLight());
            }

            if (ImGui.MenuItem("Point Light"))
            {
                CreateObjectWithComponent(
                    "Point Light",
                    () => new PointLight());
            }

            ImGui.EndMenu();
        }

        if (ImGui.MenuItem(
                "Sky Environment",
                string.Empty,
                false,
                canEdit))
        {
            CreateObjectWithComponent(
                "Sky Environment",
                () => new SkyEnvironment());
        }

        ImGui.Separator();

        if (ImGui.MenuItem("Duplicate Selected", "Ctrl+D", false, canEdit && _state.Selection.Count > 0)) DuplicateSelectedObjects();
        if (ImGui.MenuItem("Copy", "Ctrl+C", false, _state.Selection.Count > 0)) CopySelectedObjects();
        if (ImGui.MenuItem("Paste", "Ctrl+V", false, canEdit && _clipboard.HasData)) PasteObjects();

        ImGui.Separator();

        if (ImGui.MenuItem(
                "Delete Selected",
                "Delete",
                false,
                canEdit &&
                _state.SelectedObject !=
                null))
        {
            DeleteSelectedObject();
        }

        ImGui.EndMenu();
    }

    private void RegisterSceneDocument()
    {
        if (_state == null) return;
        if (_sceneDocumentId.HasValue) _documents.Unregister(_sceneDocumentId.Value);
        string key = _state.SceneFilePath ?? $"untitled:{_state.ProjectFilePath}";
        string title = Path.GetFileNameWithoutExtension(_state.SceneFilePath) ?? _state.EditorScene.Name;
        _sceneDocumentId = new EditorDocumentId(EditorDocumentType.Scene, key);
        _documents.RegisterOrFocus(new EditorDocument(
            _sceneDocumentId.Value,
            $"Scene: {title}",
            _sceneView.RequestFocus,
            () => { },
            SaveScene,
            isDirty: () => _state?.IsDirty == true));
    }

    private void DrawWindowMenu()
    {
        if (!ImGui.BeginMenu("Window"))
        {
            return;
        }

        DrawPanelToggle(
            "Hierarchy",
            _hierarchy
        );

        DrawPanelToggle(
            "Inspector",
            _inspector
        );

        DrawPanelToggle(
            "Scene View",
            _sceneView
        );

        DrawPanelToggle(
            "Game View",
            _gameView
        );

        if (_assets != null)
        {
            DrawPanelToggle(
                "Assets",
                _assets
            );
        }

        DrawPanelToggle(
            "Console",
            _console
        );

        DrawPanelToggle(
            "Performance",
            _performance
        );

        bool projectSettingsOpen = _projectSettings.IsOpen;
        if (ImGui.MenuItem("Project Settings", string.Empty, projectSettingsOpen))
            _projectSettings.IsOpen = !projectSettingsOpen;

        ImGui.Separator();

        if (ImGui.MenuItem("Restore Layout"))
        {
            SetAllPanelsOpen();
        }

        if (ImGui.MenuItem("Reset Layout"))
        {
            EditorPreferences.BottomWorkspaceCollapsed = false;
            SetAllPanelsOpen();
            _layout.RequestReset();
        }

        ImGui.EndMenu();
    }

    private void DrawPlayControls()
    {
        if (_state == null ||
            _projectContext == null)
        {
            return;
        }

        const float playWidth =
            78.0f;

        const float pauseWidth =
            78.0f;

        const float stopWidth =
            72.0f;

        const float spacing =
            6.0f;

        float totalWidth =
            playWidth +
            pauseWidth +
            stopWidth +
            spacing *
            2.0f;

        float centeredX =
            (
                ImGui.GetWindowWidth() -
                totalWidth
            ) *
            0.5f;

        ImGui.SetCursorPosX(
            Math.Max(
                ImGui.GetCursorPosX() +
                10.0f,
                centeredX));

        bool playActive =
            _state.Mode ==
            EditorMode.Play;

        bool paused =
            _state.Mode ==
            EditorMode.Paused;

        ImGui.PushStyleColor(
            ImGuiCol.Button,
            playActive
                ? new Vector4(
                    0.16f,
                    0.42f,
                    0.25f,
                    1.0f)
                : new Vector4(
                    0.13f,
                    0.22f,
                    0.18f,
                    1.0f));

        ImGui.PushStyleColor(
            ImGuiCol.ButtonHovered,
            new Vector4(
                0.20f,
                0.56f,
                0.32f,
                1.0f));

        ImGui.PushStyleColor(
            ImGuiCol.ButtonActive,
            new Vector4(
                0.24f,
                0.68f,
                0.39f,
                1.0f));

        ImGui.BeginDisabled(
            _state.Mode ==
            EditorMode.Play);

        if (ImGui.Button(
                paused
                    ? "> Resume"
                    : "> Play",
                new Vector2(
                    playWidth,
                    0.0f)))
        {
            if (paused)
            {
                ResumePlayMode();
            }
            else
            {
                EnterPlayMode();
            }
        }

        ImGui.EndDisabled();
        ImGui.PopStyleColor(3);

        ImGui.SameLine(
            0.0f,
            spacing);

        ImGui.PushStyleColor(
            ImGuiCol.Button,
            paused
                ? new Vector4(
                    0.48f,
                    0.34f,
                    0.12f,
                    1.0f)
                : new Vector4(
                    0.24f,
                    0.20f,
                    0.12f,
                    1.0f));

        ImGui.PushStyleColor(
            ImGuiCol.ButtonHovered,
            new Vector4(
                0.64f,
                0.45f,
                0.15f,
                1.0f));

        ImGui.PushStyleColor(
            ImGuiCol.ButtonActive,
            new Vector4(
                0.78f,
                0.55f,
                0.18f,
                1.0f));

        ImGui.BeginDisabled(
            _state.Mode !=
            EditorMode.Play);

        if (ImGui.Button(
                "|| Pause",
                new Vector2(
                    pauseWidth,
                    0.0f)))
        {
            PausePlayMode();
        }

        ImGui.EndDisabled();
        ImGui.PopStyleColor(3);

        ImGui.SameLine(
            0.0f,
            spacing);

        ImGui.PushStyleColor(
            ImGuiCol.Button,
            _state.Mode !=
                EditorMode.Edit
                ? new Vector4(
                    0.46f,
                    0.17f,
                    0.18f,
                    1.0f)
                : new Vector4(
                    0.23f,
                    0.13f,
                    0.14f,
                    1.0f));

        ImGui.PushStyleColor(
            ImGuiCol.ButtonHovered,
            new Vector4(
                0.62f,
                0.22f,
                0.24f,
                1.0f));

        ImGui.PushStyleColor(
            ImGuiCol.ButtonActive,
            new Vector4(
                0.74f,
                0.28f,
                0.30f,
                1.0f));

        ImGui.BeginDisabled(
            _state.Mode ==
            EditorMode.Edit);

        if (ImGui.Button(
                "[] Stop",
                new Vector2(
                    stopWidth,
                    0.0f)))
        {
            StopPlayMode();
        }

        ImGui.EndDisabled();
        ImGui.PopStyleColor(3);
    }

    private void DrawEditorStatus()
    {
        if (_state == null)
        {
            return;
        }

        string sceneName =
            _state.SceneFilePath ==
                null
                ? "Untitled.bytescene"
                : Path.GetFileName(
                    _state.SceneFilePath);

        string modeText =
            _state.Mode switch
            {
                EditorMode.Play =>
                    "PLAY",
                EditorMode.Paused =>
                    "PAUSED",
                _ =>
                    "EDIT"
            };

        Vector4 modeColor =
            _state.Mode switch
            {
                EditorMode.Play =>
                    new Vector4(
                        0.38f,
                        0.82f,
                        0.49f,
                        1.0f),
                EditorMode.Paused =>
                    new Vector4(
                        0.95f,
                        0.70f,
                        0.28f,
                        1.0f),
                _ =>
                    EditorTheme.AccentHover
            };

        string statusText =
            $"{modeText}  |  {sceneName}";

        float statusWidth =
            ImGui.CalcTextSize(
                statusText).X +
            18.0f;

        float targetX =
            ImGui.GetWindowWidth() -
            statusWidth;

        if (targetX >
            ImGui.GetCursorPosX() +
            12.0f)
        {
            ImGui.SetCursorPosX(
                targetX);
        }
        else
        {
            ImGui.SameLine();
        }

        ImGui.TextColored(
            modeColor,
            modeText);

        ImGui.SameLine();
        ImGui.TextDisabled("|");

        ImGui.SameLine();
        ImGui.TextDisabled(
            sceneName);
    }

    private void HandleShortcuts()
    {
        if (_state == null ||
            ImGui.GetIO().WantTextInput)
        {
            return;
        }

        bool control =
            ImGui.IsKeyDown(
                ImGuiKey.ModCtrl
            );

        bool shift =
            ImGui.IsKeyDown(
                ImGuiKey.ModShift
            );

        if (control &&
            ImGui.IsKeyPressed(
                ImGuiKey.N))
        {
            RequestAfterUnsavedCheck(
                CreateNewScene
            );
        }

        if (control &&
            ImGui.IsKeyPressed(
                ImGuiKey.S))
        {
            if (shift && _documents.ActiveDocumentType == EditorDocumentType.Scene)
            {
                SaveSceneAs();
            }
            else if (!_documents.SaveActive())
            {
                SaveScene();
            }
        }

        if (control &&
            ImGui.IsKeyPressed(
                ImGuiKey.Z))
        {
            if (EventWorkspaceUndoRouter.HasFocusedWorkspace)
            {
                if (shift)
                {
                    EventWorkspaceUndoRouter.TryRedo();
                }
                else
                {
                    EventWorkspaceUndoRouter.TryUndo();
                }
            }
            else if (shift)
            {
                _state.Undo?.Redo(
                    _state);
            }
            else
            {
                _state.Undo?.Undo(
                    _state);
            }
        }

        if (control &&
            ImGui.IsKeyPressed(
                ImGuiKey.Y))
        {
            if (EventWorkspaceUndoRouter.HasFocusedWorkspace)
            {
                EventWorkspaceUndoRouter.TryRedo();
            }
            else
            {
                _state.Undo?.Redo(
                    _state);
            }
        }

        if (control && ImGui.IsKeyPressed(ImGuiKey.C)) CopySelectedObjects();
        if (control && ImGui.IsKeyPressed(ImGuiKey.V)) PasteObjects();
        if (control && ImGui.IsKeyPressed(ImGuiKey.D)) DuplicateSelectedObjects();

        if (_state.Mode ==
                EditorMode.Edit &&
            _state.SelectedObject != null &&
            ImGui.IsKeyPressed(
                ImGuiKey.Delete))
        {
            DeleteSelectedObject();
        }

        if (_state.SelectedObject != null &&
            ImGui.IsKeyPressed(
                ImGuiKey.F))
        {
            _sceneView.FrameSelected(
                _state
            );
        }
    }

    private bool HasUnsavedChanges =>
        _documents.HasDirtyDocuments ||
        _state?.IsDirty == true;
    private void RequestAfterUnsavedCheck(
        Action action)
    {
        if (!HasUnsavedChanges)
        {
            action();
            return;
        }

        _pendingUnsavedAction =
            action;

        _openUnsavedPopup =
            true;
    }

    private void DrawUnsavedChangesPopup()
    {
        if (_openUnsavedPopup)
        {
            ImGui.OpenPopup(
                "Unsaved Changes"
            );

            _openUnsavedPopup =
                false;
        }

        bool open =
            true;

        if (!ImGui.BeginPopupModal(
                "Unsaved Changes",
                ref open,
                ImGuiWindowFlags.AlwaysAutoResize))
        {
            return;
        }

        ImGui.Text(
            "The project has unsaved document changes."
        );

        ImGui.Text(
            "Save before continuing?"
        );

        if (ImGui.Button("Save"))
        {
            bool documentsSaved = _documents.SaveAllDirty();

            if (documentsSaved)
            {
                CompletePendingAction();
                ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();

        if (ImGui.Button("Discard"))
        {
            _documents.DiscardAllDirty();
            CompletePendingAction();
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();

        if (ImGui.Button("Cancel"))
        {
            _pendingUnsavedAction =
                null;

            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private void CompletePendingAction()
    {
        Action? action =
            _pendingUnsavedAction;

        _pendingUnsavedAction =
            null;

        action?.Invoke();
    }

    private void RequestClose()
    {
        _allowClose =
            true;

        Close();
    }

    private void CreateProject(NewProjectRequest request)
    {
        try
        {
            string projectDirectory = Path.Combine(request.ParentDirectory, request.Name);
            if (Directory.Exists(projectDirectory) && Directory.EnumerateFileSystemEntries(projectDirectory).Any())
                throw new IOException($"The project folder already exists and is not empty: {projectDirectory}");

            string projectFile = Path.Combine(request.ParentDirectory, request.Name + ".byteproject");
            EditorProjectContext context =
                EditorProjectContext.Create(
                    projectFile,
                    warning => _log.Warning(
                        warning
                    )
                );

            Scene scene = ProjectTemplateFactory.Create(request.Template);

            if (request.Template == ProjectTemplate.ByteArena)
            {
                ByteArenaAssetFactory.Generate(context, scene);
            }

            string scenePath =
                context.ResolveProjectPath(
                    context.Project.StartupScene
                );

            context.Scenes.Save(
                scene,
                scenePath
            );

            SwitchProject(
                context,
                scene,
                scenePath
            );

            context.SaveProject();

            _log.Info(
                $"Created {request.Template} project '{context.Project.Name}' in '{context.ProjectRoot}'."
            );
        }
        catch (Exception exception)
        {
            ReportError(
                "Could not create project",
                exception
            );
        }
    }

    private void ShowOpenProjectDialog()
    {
        string? projectFile =
            EditorDialogs.ChooseProject();

        if (projectFile != null)
        {
            OpenProject(
                projectFile
            );
        }
    }

    private void OpenProject(
        string projectFile)
    {
        EditorProjectContext? context =
            null;

        try
        {
            context =
                EditorProjectContext.Open(
                    projectFile,
                    warning => _log.Warning(
                        warning
                    )
                );

            string scenePath =
                context.ResolveProjectPath(
                    context.Project.StartupScene
                );

            if (!File.Exists(scenePath))
            {
                throw new FileNotFoundException(
                    $"Startup scene '{context.Project.StartupScene}' was not found.",
                    scenePath
                );
            }

            Scene scene =
                context.Scenes.Load(
                    scenePath
                );

            SwitchProject(
                context,
                scene,
                scenePath
            );

            context = null;

            _log.Info(
                $"Opened project '{_projectContext!.Project.Name}'."
            );
        }
        catch (Exception exception)
        {
            context?.Dispose();

            ReportError(
                "Could not open project",
                exception
            );
        }
    }

    private void SwitchProject(
        EditorProjectContext context,
        Scene scene,
        string sceneFilePath)
    {
        if (_state?.Mode !=
            EditorMode.Edit)
        {
            StopPlayMode();
        }

        EditorProjectContext? previous =
            _projectContext;

        int refreshedBlueprints =
            BlueprintInstanceSynchronizer.RefreshOutdated(
                context,
                scene);

        Scenes.SetEditorScene(
            scene
        );

        _projectContext =
            context;

        _documentWindows.CloseAllImmediately();
        _assets?.Dispose();
        _documents.Clear();
        _sceneDocumentId = null;
        _assets = new AssetsPanel(context, OpenAsset, _documents, _documentWindows);
        SetAllPanelsOpen();

        _assets.Refresh(
            _log
        );

        _state =

            new EditorState
            {
                EditorScene = scene,
                Project = context.Project,
                ProjectFilePath =
                    context.ProjectFilePath,
                SceneFilePath =
                    sceneFilePath,
                SelectedObject =
                    scene.GameObjects.FirstOrDefault()
            };

        if (refreshedBlueprints > 0)
        {
            _state.MarkDirty();
            _log.Info(
                $"Updated {refreshedBlueprints} stale Blueprint instance(s) from their saved assets. Save the scene to keep the refreshed hierarchy.");
        }
        else
        {
            _state.ClearDirty();
        }
        InitializeUndo(_state, context, true);
        RegisterSceneDocument();

        EditorPreferences.SaveLastProject(
            context.ProjectFilePath
        );

        previous?.Dispose();
    }

    private void CreateNewScene()
    {
        if (_projectContext == null)
        {
            return;
        }

        if (_state?.Mode !=
            EditorMode.Edit)
        {
            StopPlayMode();
        }

        Scene scene = new("Untitled Scene");

        Scenes.SetEditorScene(
            scene
        );

        _state =
            new EditorState
            {
                EditorScene = scene,
                Project =
                    _projectContext.Project,
                ProjectFilePath =
                    _projectContext.ProjectFilePath,
                SceneFilePath = null,
                SelectedObject =
                    scene.GameObjects.FirstOrDefault()
            };

        _state.MarkDirty();
        InitializeUndo(_state, _projectContext, false);
        RegisterSceneDocument();

        _log.Info(
            "Created a new unsaved scene in Edit mode."
        );
    }

    private void ShowOpenSceneDialog()
    {
        if (_projectContext == null)
        {
            return;
        }

        string sceneDirectory =
            _projectContext.ResolveProjectPath(
                _projectContext.Project.SceneDirectory
            );

        string? scenePath =
            EditorDialogs.ChooseScene(
                sceneDirectory
            );

        if (scenePath == null)
        {
            return;
        }

        try
        {
            EnsureInsideProject(
                scenePath
            );

            Scene scene =
                _projectContext.Scenes.Load(
                    scenePath
                );

            int refreshedBlueprints =
                BlueprintInstanceSynchronizer.RefreshOutdated(
                    _projectContext,
                    scene);

            Scenes.SetEditorScene(
                scene
            );

            _state =
                new EditorState
                {
                    EditorScene = scene,
                    Project =
                        _projectContext.Project,
                    ProjectFilePath =
                        _projectContext.ProjectFilePath,
                    SceneFilePath =
                        scenePath,
                    SelectedObject =
                        scene.GameObjects.FirstOrDefault()
                };

            if (refreshedBlueprints > 0)
            {
                _state.MarkDirty();
                _log.Info(
                    $"Updated {refreshedBlueprints} stale Blueprint instance(s) from their saved assets. Save the scene to keep the refreshed hierarchy.");
            }
            else
            {
                _state.ClearDirty();
            }
            InitializeUndo(_state, _projectContext, true);
            RegisterSceneDocument();
            _log.Info(
                $"Opened scene '{Path.GetFileName(scenePath)}'."
            );
        }
        catch (Exception exception)
        {
            ReportError(
                "Could not open scene",
                exception
            );
        }
    }

    private void OpenAsset(AssetRecord asset)
    {
        if (_projectContext == null) return;
        try
        {
            if (asset.Type == AssetType.Blueprint)
            {
                _documentWindows.OpenBlueprint(asset, _projectContext, Renderer, Renderer3D);
                _log.Info($"Opened Blueprint '{asset.ProjectPath}'.");
            }
            else if (asset.Type == AssetType.Scene)
            {
                RequestAfterUnsavedCheck(() => OpenSceneAsset(asset));
            }
            else if (asset.Type == AssetType.Model3D && _state != null)
            {
                _state.SelectedObject = null;
                _state.SelectedAssetId = asset.Guid;
                _state.SelectedAssetPath = asset.ProjectPath;
            }
        }
        catch (Exception exception)
        {
            ReportError($"Could not open '{asset.ProjectPath}'", exception);
        }
    }

    private void OpenSceneAsset(AssetRecord asset)
    {
        if (_projectContext == null) return;
        Scene scene = _projectContext.Scenes.Load(asset.FullPath);
        int refreshedBlueprints =
            BlueprintInstanceSynchronizer.RefreshOutdated(
                _projectContext,
                scene);
        Scenes.SetEditorScene(scene);
        _state = new EditorState
        {
            EditorScene = scene,
            Project = _projectContext.Project,
            ProjectFilePath = _projectContext.ProjectFilePath,
            SceneFilePath = asset.FullPath,
            SelectedObject = scene.GameObjects.FirstOrDefault()
        };
        if (refreshedBlueprints > 0)
        {
            _state.MarkDirty();
            _log.Info(
                $"Updated {refreshedBlueprints} stale Blueprint instance(s) from their saved assets. Save the scene to keep the refreshed hierarchy.");
        }
        else
        {
            _state.ClearDirty();
        }
        InitializeUndo(_state, _projectContext, true);
        RegisterSceneDocument();
        _log.Info($"Opened scene '{asset.ProjectPath}'.");
    }

    private bool SaveScene()
    {
        if (_state == null ||
            _projectContext == null)
        {
            return false;
        }

        if (_state.SceneFilePath == null)
        {
            return SaveSceneAs();
        }

        return SaveSceneTo(
            _state.SceneFilePath
        );
    }

    private bool SaveSceneAs()
    {
        if (_state == null ||
            _projectContext == null)
        {
            return false;
        }

        string sceneDirectory =
            _projectContext.ResolveProjectPath(
                _projectContext.Project.SceneDirectory
            );

        Directory.CreateDirectory(
            sceneDirectory
        );

        string suggestedName =
            string.IsNullOrWhiteSpace(
                _state.EditorScene.Name)
                ? "Main.bytescene"
                : $"{_state.EditorScene.Name}.bytescene";

        string? scenePath =
            EditorDialogs.ChooseSceneSavePath(
                sceneDirectory,
                suggestedName
            );

        if (scenePath == null)
        {
            return false;
        }

        try
        {
            EnsureInsideProject(
                scenePath
            );
        }
        catch (Exception exception)
        {
            ReportError(
                "Invalid scene location",
                exception
            );

            return false;
        }

        return SaveSceneTo(
            scenePath
        );
    }

    private bool SaveSceneTo(
        string scenePath)
    {
        if (_state == null ||
            _projectContext == null)
        {
            return false;
        }

        try
        {
            _projectContext.Scenes.Save(
                _state.EditorScene,
                scenePath
            );
            _projectContext.SaveProject();

            _state.SceneFilePath =
                scenePath;

            if (_state.Undo != null) _state.Undo.MarkSaved(_state);
            else _state.ClearDirty();

            _log.Info(
                $"Saved scene '{Path.GetFileName(scenePath)}'."
            );

            return true;
        }
        catch (Exception exception)
        {
            ReportError(
                "Could not save scene",
                exception
            );

            return false;
        }
    }

    private void EnsureInsideProject(
        string path)
    {
        if (_projectContext == null)
        {
            throw new InvalidOperationException(
                "No project is open."
            );
        }

        string fullPath =
            Path.GetFullPath(
                path
            );

        string root =
            _projectContext.ProjectRoot
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                ) +
                Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(
                root,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Scene files must be saved inside the current project."
            );
        }
    }

    private void EnterPlayMode()
    {
        if (_state == null ||
            _projectContext == null ||
            _state.Mode !=
            EditorMode.Edit)
        {
            return;
        }

        Guid? selectedId =
            _state.SelectedObject?.Id;

        Scene runtimeScene =
            _projectContext.Scenes
                .CloneForRuntime(
                    _state.EditorScene
                );

        _state.RuntimeScene =
            runtimeScene;

        _state.RuntimeGlobals = new VariableStore();
        foreach (var definition in _state.Project.GlobalVariables)
            _state.RuntimeGlobals.Set(definition.Name, definition.Value.Clone());
        Scenes.GlobalVariables.Clear();
        foreach (var variable in _state.RuntimeGlobals)
            Scenes.GlobalVariables.Set(variable.Key, variable.Value);

        _state.Mode =
            EditorMode.Play;

        _gameView.RequestFocus();

        _state.SelectedObject =
            selectedId.HasValue
                ? runtimeScene.FindGameObject(
                    selectedId.Value
                )
                : null;

        Scenes.LoadScene(
            runtimeScene
        );

        _log.Info(
            "Play mode started from a serialization-isolated runtime scene."
        );
    }

    private void PausePlayMode()
    {
        if (_state?.Mode !=
            EditorMode.Play)
        {
            return;
        }

        _state.Mode =
            EditorMode.Paused;

        ReleaseGameInput();

        _log.Info(
            "Play mode paused."
        );
    }

    private void ResumePlayMode()
    {
        if (_state?.Mode !=
            EditorMode.Paused)
        {
            return;
        }

        _state.Mode =
            EditorMode.Play;

        _log.Info(
            "Play mode resumed."
        );
    }

    private void StopPlayMode()
    {
        if (_state == null ||
            _state.Mode ==
            EditorMode.Edit)
        {
            return;
        }

        ReleaseGameInput();

        Guid? selectedId =
            _state.SelectedObject?.Id;

        Scenes.SetEditorScene(
            _state.EditorScene
        );

        _state.RuntimeScene =
            null;
        _state.RuntimeGlobals = null;
        Scenes.GlobalVariables.Clear();

        _state.Mode =
            EditorMode.Edit;

        _sceneView.RequestFocus();

        _state.SelectedObject =
            selectedId.HasValue
                ? _state.EditorScene.FindGameObject(
                    selectedId.Value
                )
                : null;

        _log.Info(
            "Play mode stopped. The runtime scene was discarded."
        );
    }

    private GameObject CreateGameObject(
        string baseName)
    {
        if (_state == null) throw new InvalidOperationException("No scene is open.");
        GameObject? created = null;
        if (_state.Undo != null)
            _state.Undo.Execute(_state, $"Create {baseName}", () => created = EditorSceneCommands.CreateGameObject(_state, baseName, _log));
        else
            created = EditorSceneCommands.CreateGameObject(_state, baseName, _log);
        return created!;
    }

    private void DeleteSelectedObject()
    {
        if (_state == null) return;
        if (_state.Undo != null) _state.Undo.Execute(_state, "Delete Objects", () => EditorSceneCommands.DeleteSelected(_state, _log));
        else EditorSceneCommands.DeleteSelected(_state, _log);
    }

    private void CreateSpriteFromAsset(AssetRecord asset, Vector2 worldPosition)
    {
        if (_state == null || _projectContext == null) return;
        if (_state.Undo != null)
            _state.Undo.Execute(_state, "Create Sprite", () => EditorSceneCommands.CreateSprite(_state, _projectContext, asset, worldPosition, _log));
        else EditorSceneCommands.CreateSprite(_state, _projectContext, asset, worldPosition, _log);
    }

    private void CreateModelFromAsset(AssetRecord asset, Vector3 worldPosition)
    {
        if (_state == null || _projectContext == null) return;
        try
        {
            if (_state.Undo != null)
                _state.Undo.Execute(
                    _state,
                    "Instantiate Model",
                    () => EditorSceneCommands.CreateModel(
                        _state, _projectContext, asset, worldPosition, _log));
            else
                EditorSceneCommands.CreateModel(
                    _state, _projectContext, asset, worldPosition, _log);
        }
        catch (Exception exception)
        {
            _log.Error($"Could not instantiate model '{asset.ProjectPath}': {exception.Message}");
        }
    }

    private void InstantiateHierarchyAsset(Guid assetId, GameObject? parent)
    {
        if (_state == null || _projectContext == null ||
            !_projectContext.AssetDatabase.TryGetAsset(assetId, out AssetRecord? asset) || asset == null)
        {
            return;
        }

        if (asset.Type != AssetType.Model3D)
        {
            _log.Warning("Only 3D model assets can be dropped into the Hierarchy.");
            return;
        }

        try
        {
            Action instantiate = () =>
            {
                GameObject model = EditorSceneCommands.CreateModel(
                    _state, _projectContext, asset, Vector3.Zero, _log);

                if (parent != null)
                {
                    model.SetParent(parent, false);
                }
            };

            if (_state.Undo != null)
            {
                _state.Undo.Execute(_state, "Instantiate Model", instantiate);
            }
            else
            {
                instantiate();
            }
        }
        catch (Exception exception)
        {
            _log.Error($"Could not instantiate model '{asset.ProjectPath}': {exception.Message}");
        }
    }

    private void CreateBlueprintFromAsset(AssetRecord asset, Vector3 worldPosition)
    {
        if (_state == null || _projectContext == null) return;
        try
        {
            Action create = () =>
            {
                EditorSceneCommands.CreateBlueprintInstance(
                    _state, _projectContext, asset, worldPosition, _log);
                _sceneView.FrameSelected(_state);
            };
            if (_state.Undo != null) _state.Undo.Execute(_state, "Instantiate Blueprint", create);
            else create();
        }
        catch (Exception exception)
        {
            _log.Error($"Could not instantiate Blueprint '{asset.ProjectPath}': {exception.Message}");
        }
    }

    private void CopySelectedObjects()
    {
        if (_state == null || _projectContext == null || _state.Selection.Count == 0) return;
        _clipboard.Copy(_state, _projectContext.Scenes);
        _log.Info($"Copied {_state.Selection.Count} GameObject(s).");
    }

    private void PasteObjects()
    {
        if (_state == null || _projectContext == null || !_clipboard.HasData) return;
        _state.Undo?.Execute(_state, "Paste Objects", () => _clipboard.Paste(_state, _projectContext.Scenes));
    }

    private void DuplicateSelectedObjects()
    {
        if (_state == null || _projectContext == null || _state.Selection.Count == 0 || _state.Mode != EditorMode.Edit) return;
        _clipboard.Copy(_state, _projectContext.Scenes);
        _state.Undo?.Execute(_state, "Duplicate Objects", () => _clipboard.Paste(_state, _projectContext.Scenes));
    }

    private void CreateChildObject(GameObject parent)
    {
        if (_state == null) return;
        _state.Undo?.Execute(_state, "Create Child", () =>
        {
            GameObject child = EditorSceneCommands.CreateGameObject(_state, "GameObject", _log);
            child.SetParent(parent);
        });
    }

    private void CreateObjectWithComponent(string name, Func<ByteEngine.Core.Scene.Component> componentFactory)
    {
        if (_state == null) return;
        _state.Undo?.Execute(_state, $"Create {name}", () =>
        {
            GameObject gameObject = EditorSceneCommands.CreateGameObject(_state, name, _log);
            gameObject.AddComponent(componentFactory());
        });
    }

    private void CreateDamageTarget()
    {
        if (_state == null || _state.Mode != EditorMode.Edit) return;
        GameObject? selected = _state.SelectedObject;
        Vector3 position = new(0f, 1f, -7f);
        if (selected != null)
        {
            Vector3 forward = selected.Transform.Forward;
            forward.Y = 0f;
            if (forward.LengthSquared() < .000001f) forward = -Vector3.UnitZ;
            position = selected.Transform.WorldPosition + Vector3.Normalize(forward) * 7f;
            position.Y = 1f;
        }
        Action create = () =>
        {
            GameObject target = EditorSceneCommands.CreateGameObject(_state, "Damage Target", _log);
            target.Transform.WorldPosition = position;
            target.AddComponent(new MeshRenderer
            {
                Primitive = PrimitiveMeshType.Cube,
                Material = new Material { BaseColor = new Vector4(.9f, .2f, .18f, 1f) }
            });
            target.AddComponent(new BoxCollider3D { Size = Vector3.One });
            target.AddComponent(new HealthComponent
            {
                MaxHealth = 100f, CurrentHealth = 100f, DestroyOnDeath = true
            });
        };
        if (_state.Undo != null) _state.Undo.Execute(_state, "Create Damage Target", create);
        else create();
    }

    private void CreateMeshPrimitive(string name, PrimitiveMeshType primitive)
    {
        if (_state == null) return;
        _state.Undo?.Execute(_state, $"Create {name}", () =>
        {
            GameObject gameObject = EditorSceneCommands.CreateGameObject(_state, name, _log);
            gameObject.AddComponent(new MeshRenderer { Primitive = primitive, UsePrimitive = true });
            if (primitive == PrimitiveMeshType.Plane)
                gameObject.AddComponent(new BoxCollider3D { Size = new Vector3(1f, .05f, 1f) });
        });
    }

    private void CreateObjectAtPosition(string name, Vector3 position, Func<ByteEngine.Core.Scene.Component>? componentFactory)
    {
        if (_state == null) return;
        _state.Undo?.Execute(_state, $"Create {name}", () =>
        {
            GameObject gameObject = EditorSceneCommands.CreateGameObject(_state, name, _log);
            gameObject.Transform.WorldPosition = position;
            if (componentFactory != null) gameObject.AddComponent(componentFactory());
        });
    }

    private void InitializeUndo(EditorState state, EditorProjectContext context, bool isSaved)
    {
        state.Undo = new Commands.UndoManager(context.Scenes, scene => Scenes.SetEditorScene(scene));
        state.Undo.Reset(state, isSaved);
    }

    private void UpdateWindowTitle()
    {
        string title;

        if (_state == null ||
            _projectContext == null)
        {
            title =
                $"ByteEngine Editor v{ByteEngineInfo.Version} - Select a Project";
        }
        else
        {
            string sceneName =
                _state.SceneFilePath == null
                    ? "Untitled.bytescene"
                    : Path.GetFileName(
                        _state.SceneFilePath
                    );

            string dirtyMarker =
                HasUnsavedChanges
                    ? " *"
                    : string.Empty;

            title =
                $"ByteEngine Editor - {sceneName}{dirtyMarker}";
        }

        if (Title != title)
        {
            Title = title;
        }
    }

    private void ReportError(
        string title,
        Exception exception)
    {
        _log.Error(
            $"{title}: {exception.Message}"
        );

        EditorDialogs.ShowError(
            title,
            exception.Message
        );
    }

    private static void DrawPanelToggle(
        string label,
        object panel)
    {
        bool isOpen =
            panel switch
            {
                HierarchyPanel value =>
                    value.IsOpen,
                InspectorPanel value =>
                    value.IsOpen,
                SceneViewPanel value =>
                    value.IsOpen,
                GameViewPanel value =>
                    value.IsOpen,
                AssetsPanel value =>
                    value.IsOpen,
                ConsolePanel value =>
                    value.IsOpen,
                PerformancePanel value =>
                    value.IsOpen,
                _ => false
            };

        if (!ImGui.MenuItem(
                label,
                string.Empty,
                isOpen))
        {
            return;
        }

        switch (panel)
        {
            case HierarchyPanel value:
                value.IsOpen = !isOpen;
                break;
            case InspectorPanel value:
                value.IsOpen = !isOpen;
                break;
            case SceneViewPanel value:
                value.IsOpen = !isOpen;
                break;
            case GameViewPanel value:
                value.IsOpen = !isOpen;
                break;
            case AssetsPanel value:
                value.IsOpen = !isOpen;
                break;
            case ConsolePanel value:
                value.IsOpen = !isOpen;
                break;
            case PerformancePanel value:
                value.IsOpen = !isOpen;
                break;
        }
    }

    private void SetAllPanelsOpen()
    {
        _hierarchy.IsOpen = true;
        _inspector.IsOpen = true;
        _sceneView.IsOpen = true;
        _gameView.IsOpen = true;
        _console.IsOpen = true;
        _performance.IsOpen = true;

        if (_assets != null)
        {
            _assets.IsOpen = true;
        }
    }

}
