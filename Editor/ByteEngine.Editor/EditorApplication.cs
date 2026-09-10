using System.ComponentModel;
using System.Numerics;

using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
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
            DrawProjectSelection();
        }
        else
        {
            _projectContext?.AssetDatabase.Update();
            DrawPanels();
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
                DeleteSelectedObject
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
                FramebufferSize.X,
                FramebufferSize.Y,
                _projectContext!,
                CreateSpriteFromAsset
            );
        }

        if (_gameView.IsOpen)
        {
            _gameView.Draw(_state, Renderer, FramebufferSize.X, FramebufferSize.Y);
        }

        if (_assets?.IsOpen == true)
        {
            _assets.Draw(
                _state,
                _log
            );
        }

        if (_console.IsOpen)
        {
            _console.Draw(
                _log
            );
        }
    }

    private void DrawProjectSelection()
    {
        ImGui.SetNextWindowSize(
            new Vector2(
                520.0f,
                240.0f
            ),
            ImGuiCond.FirstUseEver
        );

        ImGui.Begin(
            "Project Selection",
            ImGuiWindowFlags.NoCollapse
        );

        ImGui.Text(
            "Welcome to ByteEngine"
        );

        ImGui.TextDisabled(
            "Create a new project or open an existing .byteproject file."
        );

        ImGui.Spacing();

        if (ImGui.Button(
                "New Project",
                new Vector2(
                    180.0f,
                    44.0f
                )))
        {
            ShowNewProjectDialog();
        }

        ImGui.SameLine();

        if (ImGui.Button(
                "Open Project",
                new Vector2(
                    180.0f,
                    44.0f
                )))
        {
            ShowOpenProjectDialog();
        }

        ImGui.Separator();

        foreach (EditorLogEntry entry
                 in _log.Entries.TakeLast(3))
        {
            ImGui.TextWrapped(
                $"[{entry.Level}] {entry.Message}"
            );
        }

        ImGui.End();
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
            RequestAfterUnsavedCheck(
                ShowNewProjectDialog
            );
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

        ImGui.MenuItem(
            "Undo",
            "Ctrl+Z",
            false,
            false
        );

        ImGui.MenuItem(
            "Redo",
            "Ctrl+Y",
            false,
            false
        );

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

        if (ImGui.MenuItem(
                "Create Sprite",
                string.Empty,
                false,
                canEdit))
        {
            GameObject sprite = CreateGameObject("Sprite");
            sprite.AddComponent(new SpriteRenderer());
        }

        if (ImGui.MenuItem(
                "Create Camera",
                string.Empty,
                false,
                canEdit))
        {
            GameObject camera =
                CreateGameObject(
                    "Camera"
                );

            camera.AddComponent(
                new Camera2D()
            );
        }

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

    private void ShowNewProjectDialog()
    {
        string? projectFile =
            EditorDialogs.ChooseNewProject();

        if (projectFile == null)
        {
            return;
        }

        try
        {
            EditorProjectContext context =
                EditorProjectContext.Create(
                    projectFile,
                    warning => _log.Warning(
                        warning
                    )
                );

            Scene scene =
                CreateDefaultScene(
                    context
                );

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
                $"Created project '{context.Project.Name}'."
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

        _assets = new AssetsPanel(context);

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

        Scene scene =
            CreateDefaultScene(
                _projectContext
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
                SceneFilePath = null,
                SelectedObject =
                    scene.GameObjects.FirstOrDefault()
            };

        _state.MarkDirty();

        _log.Info(
            "Created a new unsaved scene in Edit mode."
        );
    }

    private Scene CreateDefaultScene(
        EditorProjectContext context)
    {
        Scene scene =
            new(
                "Main"
            );

        GameObject camera =
            scene.CreateGameObject(
                "Main Camera"
            );

        camera.AddComponent(
            new Camera2D()
        );

        return scene;
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

            _state.SceneFilePath =
                scenePath;

            _state.ClearDirty();

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
        return EditorSceneCommands.CreateGameObject(_state, baseName, _log);
    }

    private void DeleteSelectedObject()
    {
        if (_state != null) EditorSceneCommands.DeleteSelected(_state, _log);
    }

    private void CreateSpriteFromAsset(AssetRecord asset, Vector2 worldPosition)
    {
        if (_state != null && _projectContext != null)
            EditorSceneCommands.CreateSprite(_state, _projectContext, asset, worldPosition, _log);
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
        }
    }

    private void SetAllPanelsOpen()
    {
        _hierarchy.IsOpen = true;
        _inspector.IsOpen = true;
        _sceneView.IsOpen = true;
        _gameView.IsOpen = true;
        _console.IsOpen = true;

        if (_assets != null)
        {
            _assets.IsOpen = true;
        }
    }

}
