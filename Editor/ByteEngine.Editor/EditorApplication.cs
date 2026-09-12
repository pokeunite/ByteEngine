using System.ComponentModel;
using System.Numerics;

using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Characters;
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

    private readonly ProjectBrowserPanel _projectBrowser = new();

    private readonly BlueprintWorkspacePanel _blueprintWorkspace = new();

    private readonly EditorClipboard _clipboard = new();

    private ImGuiController? _imgui;

    private EditorProjectContext? _projectContext;

    private AssetsPanel? _assets;

    private EditorState? _state;

    private Action? _pendingUnsavedAction;

    private bool _openUnsavedPopup;

    private bool _allowClose;

    protected override bool CloseOnEscape =>
        false;

    protected override bool ShouldUpdateScene =>
        _state?.Mode ==
        EditorMode.Play;

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

        if (EditorPreferences.EnsureLayoutVersion(
                3))
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

        IReadOnlyList<AssetRecord> imported = new ExternalAssetImporter(_projectContext, _log).Import(e.FileNames);
        AssetRecord? selected = imported.FirstOrDefault();
        if (selected != null)
        {
            _state.SelectedObject = null;
            _state.SelectedAssetId = selected.Guid;
            _state.SelectedAssetPath = selected.ProjectPath;
        }
    }

    protected override void OnClosing(
        CancelEventArgs e)
    {
        if (!_allowClose &&
            _state?.IsDirty == true)
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
        _sceneView.Dispose();
        _gameView.Dispose();
        _blueprintWorkspace.Dispose();
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
                CreateChildObject
            );
        }

        if (_inspector.IsOpen)
        {
            _inspector.Draw(_state, _projectContext!);
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
        }

        if (_gameView.IsOpen)
        {
            _gameView.Draw(_state, Renderer, Renderer3D, FramebufferSize.X, FramebufferSize.Y);
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
                _log
            );
        }

        _blueprintWorkspace.Draw(
            Renderer, Renderer3D, FramebufferSize.X, FramebufferSize.Y);
    }

    private void DrawMainMenu()
    {
        if (!ImGui.BeginMainMenuBar())
        {
            return;
        }

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
        }

        ImGui.EndMainMenuBar();
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
            if (ImGui.MenuItem("Directional Light")) CreateObjectWithComponent("Directional Light", () => new DirectionalLight());
            ImGui.EndMenu();
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

        ImGui.Separator();

        if (ImGui.MenuItem(
                "Reset Layout"))
        {
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

        ImGui.SetCursorPosX(
            Math.Max(
                ImGui.GetCursorPosX(),
                (
                    ImGui.GetWindowWidth() -
                    190.0f
                ) /
                2.0f
            )
        );

        ImGui.BeginDisabled(
            _state.Mode ==
            EditorMode.Play
        );

        if (ImGui.Button(
                _state.Mode ==
                EditorMode.Paused
                    ? "Resume"
                    : "Play"))
        {
            if (_state.Mode ==
                EditorMode.Paused)
            {
                ResumePlayMode();
            }
            else
            {
                EnterPlayMode();
            }
        }

        ImGui.EndDisabled();
        ImGui.SameLine();

        ImGui.BeginDisabled(
            _state.Mode !=
            EditorMode.Play
        );

        if (ImGui.Button("Pause"))
        {
            PausePlayMode();
        }

        ImGui.EndDisabled();
        ImGui.SameLine();

        ImGui.BeginDisabled(
            _state.Mode ==
            EditorMode.Edit
        );

        if (ImGui.Button("Stop"))
        {
            StopPlayMode();
        }

        ImGui.EndDisabled();
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
            if (shift)
            {
                SaveSceneAs();
            }
            else
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

    private void RequestAfterUnsavedCheck(
        Action action)
    {
        if (_state?.IsDirty != true)
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
            "The current scene has unsaved changes."
        );

        ImGui.Text(
            "Save before continuing?"
        );

        if (ImGui.Button("Save"))
        {
            if (SaveScene())
            {
                CompletePendingAction();
                ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();

        if (ImGui.Button("Discard"))
        {
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

        Scenes.SetEditorScene(
            scene
        );

        _projectContext =
            context;

        _assets = new AssetsPanel(context, OpenAsset);

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

        _state.ClearDirty();
        InitializeUndo(_state, context, true);

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

            _state.ClearDirty();
            InitializeUndo(_state, _projectContext, true);

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
                _blueprintWorkspace.Open(asset, _projectContext);
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
        Scenes.SetEditorScene(scene);
        _state = new EditorState
        {
            EditorScene = scene,
            Project = _projectContext.Project,
            ProjectFilePath = _projectContext.ProjectFilePath,
            SceneFilePath = asset.FullPath,
            SelectedObject = scene.GameObjects.FirstOrDefault()
        };
        InitializeUndo(_state, _projectContext, true);
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

    private void CreateBlueprintFromAsset(AssetRecord asset, Vector3 worldPosition)
    {
        if (_state == null || _projectContext == null) return;
        try
        {
            Action create = () => EditorSceneCommands.CreateBlueprintInstance(
                _state, _projectContext, asset, worldPosition, _log);
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

    private void CreateMeshPrimitive(string name, PrimitiveMeshType primitive)
    {
        if (_state == null) return;
        _state.Undo?.Execute(_state, $"Create {name}", () =>
        {
            GameObject gameObject = EditorSceneCommands.CreateGameObject(_state, name, _log);
            gameObject.AddComponent(new MeshRenderer { Primitive = primitive });
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
                _state.IsDirty
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
