using System.Numerics;
using System.Text.Json.Nodes;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.VisualLogic;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class AssetsPanel
{
    private readonly EditorProjectContext _project;

    private readonly Action<AssetRecord> _openAsset;

    private readonly EventWorkspaceCollection _eventWorkspaces =
        new();

    private readonly List<string> _directories =
        new();

    private readonly List<string> _files =
        new();

    private string _currentDirectory;

    private bool _listingDirty =
        true;

    private bool _showCreateBlueprint;

    private string _blueprintName =
        "New Blueprint";

    private int _blueprintType;

    private bool _showCreateEventModule;

    private string _eventModuleName =
        "New Event Module";

    private int _eventModuleTemplate;

    private bool _showCreateFolder;

    private string _newFolderName =
        "New Folder";

    private bool _showRename;

    private string? _renameTargetPath;

    private bool _renameTargetIsDirectory;

    private string _renameBuffer =
        string.Empty;

    private bool _showDeleteConfirm;

    private string? _deleteTargetPath;

    private bool _deleteTargetIsDirectory;

    public bool IsOpen { get; set; } =
        true;

    public AssetsPanel(
        EditorProjectContext project,
        Action<AssetRecord> openAsset)
    {
        _project =
            project;

        _openAsset =
            openAsset;

        _currentDirectory =
            GetAssetsRoot();

        _project.AssetDatabase.DatabaseChanged +=
            OnAssetDatabaseChanged;
    }

    private void OnAssetDatabaseChanged()
    {
        _listingDirty =
            true;
    }

    public void Refresh(
        EditorLog log)
    {
        _project.AssetDatabase.Scan();

        _listingDirty =
            true;

        log.Info(
            $"Asset database contains {_project.AssetDatabase.Assets.Count} asset(s).");
    }

    public void Draw(
        EditorState state,
        EditorLog log)
    {
        bool isOpen =
            IsOpen;

        bool visible =
            ImGui.Begin(
                "Assets",
                ref isOpen);

        IsOpen =
            isOpen;

        if (visible)
        {
            EnsureValidDirectory();

            DrawToolbar(
                state,
                log);

            ImGui.Separator();

            float folderPaneWidth =
                Math.Clamp(
                    ImGui.GetContentRegionAvail().X *
                    0.25f,
                    180.0f,
                    300.0f);

            ImGui.BeginChild(
                "ProjectFolderTree",
                new Vector2(
                    folderPaneWidth,
                    0.0f),
                ImGuiChildFlags.None);

            DrawFolderTree(
                log);

            ImGui.EndChild();

            ImGui.SameLine();

            ImGui.BeginChild(
                "ProjectFolderContents",
                Vector2.Zero,
                ImGuiChildFlags.None);

            DrawCurrentFolder(
                state,
                log);

            ImGui.EndChild();

            DrawCreateBlueprintDialog(
                log);

            DrawCreateEventModuleDialog(
                log);

            DrawCreateFolderDialog(
                log);

            DrawRenameDialog(
                state,
                log);

            DrawDeleteDialog(
                state,
                log);
        }

        ImGui.End();

        _eventWorkspaces.Draw(
            log);
    }

    // ========================================================
    // TOOLBAR
    // ========================================================

    private void DrawToolbar(
        EditorState state,
        EditorLog log)
    {
        if (ImGui.SmallButton(
                "Assets"))
        {
            SelectDirectory(
                GetAssetsRoot());
        }

        ImGui.SameLine();

        if (ImGui.SmallButton(
                "Scenes"))
        {
            SelectDirectory(
                GetScenesRoot());
        }

        ImGui.SameLine();

        if (ImGui.SmallButton(
                "+ Folder"))
        {
            _newFolderName =
                "New Folder";

            _showCreateFolder =
                true;
        }

        ImGui.SameLine();

        bool canCreateFromSelection =
            state.Mode ==
                EditorMode.Edit &&
            state.SelectedObject !=
                null &&
            IsInsideDirectory(
                _currentDirectory,
                GetAssetsRoot());

        ImGui.BeginDisabled(
            !canCreateFromSelection);

        if (ImGui.SmallButton(
                "Blueprint From Selected"))
        {
            CreateBlueprintFromSelectedObject(
                state,
                log);
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        ImGui.TextDisabled(
            Breadcrumb());

        ImGui.SameLine();

        if (ImGui.SmallButton(
                "Refresh"))
        {
            Refresh(
                log);
        }
    }

    // ========================================================
    // FOLDER TREE
    // ========================================================

    private void DrawFolderTree(
        EditorLog log)
    {
        ImGui.TextDisabled(
            "PROJECT");

        ImGui.Separator();

        DrawFolderTreeNode(
            GetAssetsRoot(),
            "Assets",
            true,
            log);

        DrawFolderTreeNode(
            GetScenesRoot(),
            "Scenes",
            true,
            log);
    }

    private void DrawFolderTreeNode(
        string directory,
        string displayName,
        bool defaultOpen,
        EditorLog log)
    {
        if (!Directory.Exists(
                directory))
        {
            return;
        }

        string[] children =
            GetDirectoriesSafe(
                directory);

        bool hasChildren =
            children.Length >
            0;

        bool selected =
            PathsEqual(
                directory,
                _currentDirectory);

        ImGuiTreeNodeFlags flags =
            ImGuiTreeNodeFlags.OpenOnArrow |
            ImGuiTreeNodeFlags.SpanAvailWidth;

        if (defaultOpen)
        {
            flags |=
                ImGuiTreeNodeFlags.DefaultOpen;
        }

        if (selected)
        {
            flags |=
                ImGuiTreeNodeFlags.Selected;
        }

        if (!hasChildren)
        {
            flags |=
                ImGuiTreeNodeFlags.Leaf |
                ImGuiTreeNodeFlags.NoTreePushOnOpen;
        }

        bool open =
            ImGui.TreeNodeEx(
                $"{displayName}##folder:{directory}",
                flags);

        if (ImGui.IsItemClicked(
                ImGuiMouseButton.Left))
        {
            SelectDirectory(
                directory);
        }

        DrawAssetDropTarget(
            directory,
            log);

        bool protectedRoot =
            IsProtectedRoot(
                directory);

        if (!protectedRoot &&
            ImGui.BeginPopupContextItem(
                $"FolderTreeContext##{directory}"))
        {
            if (ImGui.MenuItem(
                    "Open"))
            {
                SelectDirectory(
                    directory);
            }

            if (ImGui.MenuItem(
                    "New Folder Here"))
            {
                SelectDirectory(
                    directory);

                _newFolderName =
                    "New Folder";

                _showCreateFolder =
                    true;
            }

            ImGui.Separator();

            if (ImGui.MenuItem(
                    "Rename"))
            {
                BeginRename(
                    directory,
                    true);
            }

            if (ImGui.MenuItem(
                    "Delete"))
            {
                BeginDelete(
                    directory,
                    true);
            }

            ImGui.EndPopup();
        }

        if (!hasChildren ||
            !open)
        {
            return;
        }

        foreach (string child
                 in children)
        {
            DrawFolderTreeNode(
                child,
                Path.GetFileName(
                    child),
                false,
                log);
        }

        ImGui.TreePop();
    }

    private static string[] GetDirectoriesSafe(
        string directory)
    {
        try
        {
            return Directory
                .EnumerateDirectories(
                    directory)
                .OrderBy(
                    path =>
                        Path.GetFileName(
                            path),
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    // ========================================================
    // FOLDER CONTENTS
    // ========================================================

    private void DrawCurrentFolder(
        EditorState state,
        EditorLog log)
    {
        UpdateListing();

        string folderName =
            GetDisplayFolderName(
                _currentDirectory);

        ImGui.Text(
            folderName);

        ImGui.SameLine();

        ImGui.TextDisabled(
            $"{_directories.Count} folder(s), {_files.Count} asset(s)");

        ImGui.Separator();

        DrawDirectories(
            log);

        DrawFiles(
            state,
            log);

        DrawWindowContextMenu(
            state,
            log);
    }

    private void DrawDirectories(
        EditorLog log)
    {
        foreach (string directory
                 in _directories.ToArray())
        {
            string folderName =
                Path.GetFileName(
                    directory);

            ImGui.Selectable(
                $"[DIR] {folderName}##content-folder:{directory}");

            /*
             * Double-click detection must not depend on Selectable() returning
             * true on the second click. ImGui can consume the second click as
             * part of the double-click sequence, which made folders look dead.
             */
            if (ImGui.IsItemHovered() &&
                ImGui.IsMouseDoubleClicked(
                    ImGuiMouseButton.Left))
            {
                SelectDirectory(
                    directory);
            }

            DrawAssetDropTarget(
                directory,
                log);

            if (ImGui.BeginPopupContextItem(
                    $"FolderContext##{directory}"))
            {
                if (ImGui.MenuItem(
                        "Open"))
                {
                    SelectDirectory(
                        directory);
                }

                if (ImGui.MenuItem(
                        "New Folder Here"))
                {
                    SelectDirectory(
                        directory);

                    _newFolderName =
                        "New Folder";

                    _showCreateFolder =
                        true;
                }

                ImGui.Separator();

                if (ImGui.MenuItem(
                        "Rename"))
                {
                    BeginRename(
                        directory,
                        true);
                }

                if (ImGui.MenuItem(
                        "Delete"))
                {
                    BeginDelete(
                        directory,
                        true);
                }

                ImGui.EndPopup();
            }
        }
    }

    // ========================================================
    // FILES
    // ========================================================

    private void DrawFiles(
        EditorState state,
        EditorLog log)
    {
        foreach (string file
                 in _files.ToArray())
        {
            string projectPath =
                Path.GetRelativePath(
                        _project.ProjectRoot,
                        file)
                    .Replace(
                        '\\',
                        '/');

            _project.AssetDatabase.TryGetAsset(
                projectPath,
                out AssetRecord? asset);

            string icon =
                GetAssetIcon(
                    asset?.Type);

            bool selected =
                string.Equals(
                    state.SelectedAssetPath,
                    projectPath,
                    StringComparison.OrdinalIgnoreCase);

            bool clicked =
                ImGui.Selectable(
                    $"{icon} {Path.GetFileName(file)}##asset:{file}",
                    selected);

            if (clicked)
            {
                state.SelectedAssetId =
                    asset?.Guid;

                state.SelectedAssetPath =
                    projectPath;

                state.SelectedObject =
                    null;
            }

            if (asset !=
                    null &&
                ImGui.IsItemHovered() &&
                ImGui.IsMouseDoubleClicked(
                    ImGuiMouseButton.Left))
            {
                OpenAsset(
                    asset,
                    log);
            }

            DrawAssetContextMenu(
                state,
                log,
                asset,
                file);

            DrawAssetDragSource(
                asset,
                file);
        }
    }

    private void OpenAsset(
        AssetRecord asset,
        EditorLog log)
    {
        if (asset.Type ==
            AssetType.EventModule)
        {
            _eventWorkspaces.Open(
                asset,
                log);

            return;
        }

        _openAsset(
            asset);
    }

    private static string GetAssetIcon(
        AssetType? type)
    {
        return type switch
        {
            AssetType.Texture2D =>
                "[IMG]",

            AssetType.Scene =>
                "[SCN]",

            AssetType.Model3D =>
                "[3D]",

            AssetType.Blueprint =>
                "[BP]",

            AssetType.EventModule =>
                "[EVT]",

            AssetType.AnimationEvents =>
                "[ANIM]",

            _ =>
                "[FILE]"
        };
    }

    // ========================================================
    // ASSET CONTEXT MENU
    // ========================================================

    private void DrawAssetContextMenu(
        EditorState state,
        EditorLog log,
        AssetRecord? asset,
        string file)
    {
        if (!ImGui.BeginPopupContextItem(
                $"AssetContext##{file}"))
        {
            return;
        }

        if (asset !=
                null &&
            ImGui.MenuItem(
                "Open"))
        {
            OpenAsset(
                asset,
                log);
        }

        if (asset?.Type ==
            AssetType.EventModule)
        {
            ImGui.Separator();

            bool canAttach =
                state.Mode ==
                    EditorMode.Edit &&
                state.SelectedObject !=
                    null;

            ImGui.BeginDisabled(
                !canAttach);

            if (ImGui.MenuItem(
                    "Attach to Selected GameObject"))
            {
                AttachEventModule(
                    state,
                    log,
                    asset);
            }

            ImGui.EndDisabled();

            if (!canAttach)
            {
                ImGui.TextDisabled(
                    "Select a GameObject in the Hierarchy first.");
            }
        }

        ImGui.Separator();

        if (ImGui.MenuItem(
                "Rename"))
        {
            BeginRename(
                file,
                false);
        }

        if (ImGui.MenuItem(
                "Delete"))
        {
            BeginDelete(
                file,
                false);
        }

        ImGui.EndPopup();
    }

    private static void DrawAssetDragSource(
        AssetRecord? asset,
        string file)
    {
        if (asset ==
            null)
        {
            return;
        }

        if (!ImGui.BeginDragDropSource())
        {
            return;
        }

        AssetDragDrop.Set(
            asset.Guid);

        ImGui.Text(
            $"Move {Path.GetFileName(file)}");

        ImGui.EndDragDropSource();
    }

    private void DrawAssetDropTarget(
        string destinationDirectory,
        EditorLog log)
    {
        if (!ImGui.BeginDragDropTarget())
        {
            return;
        }

        Guid? assetGuid =
            AssetDragDrop.Accept();

        if (assetGuid.HasValue &&
            assetGuid.Value !=
                Guid.Empty &&
            _project.AssetDatabase.TryGetAsset(
                assetGuid.Value,
                out AssetRecord? asset) &&
            asset !=
                null)
        {
            MoveAssetToFolder(
                asset,
                destinationDirectory,
                log);
        }

        ImGui.EndDragDropTarget();
    }

    // ========================================================
    // EVENT MODULE ATTACHMENT
    // ========================================================

    private void AttachEventModule(
        EditorState state,
        EditorLog log,
        AssetRecord asset)
    {
        GameObject? target =
            state.SelectedObject;

        if (target ==
            null)
        {
            log.Warning(
                "Select a GameObject before attaching an Event Module.");

            return;
        }

        EventModuleComponent? existing =
            target.GetComponent<EventModuleComponent>();

        if (existing !=
                null &&
            existing.Modules.Any(
                reference =>
                    reference.Guid ==
                    asset.Guid))
        {
            log.Warning(
                $"'{asset.ProjectPath}' is already attached to '{target.Name}'.");

            return;
        }

        EventModuleDefinition definition;

        try
        {
            definition =
                new EventModuleSerializer()
                    .Load(
                        asset.FullPath);
        }
        catch (Exception exception)
        {
            log.Error(
                $"Could not load Event Module '{asset.ProjectPath}': {exception.Message}");

            return;
        }

        var reference =
            new AssetReference(
                asset.Guid,
                asset.ProjectPath);

        void Attach()
        {
            EventModuleComponent component =
                target.GetComponent<EventModuleComponent>()
                ?? target.AddComponent(
                    new EventModuleComponent());

            component.AddResolvedModule(
                reference,
                definition);
        }

        if (state.Undo !=
            null)
        {
            state.Undo.Execute(
                state,
                "Attach Event Module",
                Attach);
        }
        else
        {
            Attach();

            state.MarkDirty();
        }

        log.Info(
            $"Attached Event Module '{definition.Name}' to '{target.Name}'.");
    }

    // ========================================================
    // CONTENT CONTEXT MENU
    // ========================================================

    private void DrawWindowContextMenu(
        EditorState state,
        EditorLog log)
    {
        if (!ImGui.BeginPopupContextWindow(
                "ProjectContentContext",
                ImGuiPopupFlags.MouseButtonRight |
                ImGuiPopupFlags.NoOpenOverItems))
        {
            return;
        }

        if (ImGui.BeginMenu(
                "Create"))
        {
            if (ImGui.MenuItem(
                    "Folder"))
            {
                _newFolderName =
                    "New Folder";

                _showCreateFolder =
                    true;
            }

            if (ImGui.MenuItem(
                    "Byte Blueprint"))
            {
                _showCreateBlueprint =
                    true;
            }

            bool canCreateFromSelection =
                state.Mode ==
                    EditorMode.Edit &&
                state.SelectedObject !=
                    null &&
                IsInsideDirectory(
                    _currentDirectory,
                    GetAssetsRoot());

            ImGui.BeginDisabled(
                !canCreateFromSelection);

            if (ImGui.MenuItem(
                    "Blueprint From Selected Object"))
            {
                CreateBlueprintFromSelectedObject(
                    state,
                    log);
            }

            ImGui.EndDisabled();

            if (ImGui.MenuItem(
                    "Event Module"))
            {
                _showCreateEventModule =
                    true;
            }

            ImGui.EndMenu();
        }

        ImGui.EndPopup();
    }

    // ========================================================
    // BLUEPRINT CREATION
    // ========================================================

    private void DrawCreateBlueprintDialog(
        EditorLog log)
    {
        if (_showCreateBlueprint)
        {
            ImGui.OpenPopup(
                "Create Byte Blueprint");

            _showCreateBlueprint =
                false;
        }

        if (!ImGui.BeginPopupModal(
                "Create Byte Blueprint",
                ImGuiWindowFlags.AlwaysAutoResize))
        {
            return;
        }

        ImGui.InputText(
            "Name",
            ref _blueprintName,
            128);

        ImGui.Combo(
            "Type",
            ref _blueprintType,
            "Generic Object\0Character\0");

        bool valid =
            !string.IsNullOrWhiteSpace(
                _blueprintName);

        ImGui.BeginDisabled(
            !valid);

        if (ImGui.Button(
                "Create",
                new Vector2(
                    100.0f,
                    0.0f)))
        {
            try
            {
                CreateBlueprint(
                    log);

                ImGui.CloseCurrentPopup();
            }
            catch (Exception exception)
            {
                log.Error(
                    $"Could not create Blueprint: {exception.Message}");
            }
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        if (ImGui.Button(
                "Cancel",
                new Vector2(
                    100.0f,
                    0.0f)))
        {
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private void CreateBlueprint(
        EditorLog log)
    {
        string directory =
            GetAssetCreationDirectory();

        string safeName =
            MakeSafeFileName(
                _blueprintName);

        string path =
            Path.Combine(
                directory,
                safeName +
                ".byteblueprint");

        if (File.Exists(
                path))
        {
            throw new IOException(
                "A Blueprint with that name already exists.");
        }

        BlueprintType type =
            (BlueprintType)_blueprintType;

        var root =
            new GameObjectData
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    safeName
            };

        if (type ==
            BlueprintType.Character)
        {
            root.Components.Add(
                new ComponentData
                {
                    Type =
                        "CharacterController3D"
                });

            root.Components.Add(
                new ComponentData
                {
                    Type =
                        "CapsuleCollider3D",

                    Properties =
                        new JsonObject
                        {
                            ["radius"] =
                                0.5f,

                            ["height"] =
                                2.0f
                        }
                });

            root.Components.Add(
                new ComponentData
                {
                    Type =
                        "AnimationController"
                });
        }

        var blueprint =
            new BlueprintDefinition
            {
                Name =
                    safeName,

                Type =
                    type,

                Root =
                    root,

                Variables =
                    type ==
                    BlueprintType.Character
                        ? new List<VariableData>
                        {
                            new()
                            {
                                Name =
                                    "Health",

                                Value =
                                    ByteEngine.Core.Variables
                                        .VariableValue
                                        .FromNumber(
                                            100)
                            },

                            new()
                            {
                                Name =
                                    "MoveSpeed",

                                Value =
                                    ByteEngine.Core.Variables
                                        .VariableValue
                                        .FromNumber(
                                            6)
                            },

                            new()
                            {
                                Name =
                                    "Team",

                                Value =
                                    ByteEngine.Core.Variables
                                        .VariableValue
                                        .FromString(
                                            "Player")
                            }
                        }
                        : new List<VariableData>()
            };

        new BlueprintSerializer()
            .Save(
                blueprint,
                path);

        SelectDirectory(
            directory);

        RefreshAfterFileOperation();

        log.Info(
            $"Created {type} Blueprint '{Path.GetFileName(path)}'.");
    }

    private void CreateBlueprintFromSelectedObject(
        EditorState state,
        EditorLog log)
    {
        GameObject? selected =
            state.SelectedObject;

        if (state.Mode !=
                EditorMode.Edit ||
            selected ==
                null)
        {
            log.Warning(
                "Select a GameObject in Edit mode first.");

            return;
        }

        string directory =
            GetAssetCreationDirectory();

        string safeName =
            MakeSafeFileName(
                selected.Name);

        string path =
            GetUniqueAssetPath(
                directory,
                safeName,
                ".byteblueprint");

        SceneData serializedScene =
            _project.Scenes.Serialize(
                state.EditorScene);

        HashSet<Guid> hierarchyIds =
            new();

        CollectHierarchyIds(
            selected,
            hierarchyIds);

        GameObjectData? root =
            serializedScene.GameObjects
                .FirstOrDefault(
                    data =>
                        data.Id ==
                        selected.Id);

        if (root ==
            null)
        {
            log.Error(
                $"Could not serialize selected GameObject '{selected.Name}'.");

            return;
        }

        root.ParentId =
            null;

        List<GameObjectData> children =
            serializedScene.GameObjects
                .Where(
                    data =>
                        data.Id !=
                            selected.Id &&
                        hierarchyIds.Contains(
                            data.Id))
                .ToList();

        BlueprintType type =
            selected.GetComponent<CharacterController3D>() !=
                null
                ? BlueprintType.Character
                : BlueprintType.GenericObject;

        List<Guid> eventModules =
            selected
                .GetComponent<EventModuleComponent>()?
                .Modules
                .Where(
                    reference =>
                        reference.Guid !=
                        Guid.Empty)
                .Select(
                    reference =>
                        reference.Guid)
                .Distinct()
                .ToList()
            ?? new List<Guid>();

        var blueprint =
            new BlueprintDefinition
            {
                Name =
                    Path.GetFileNameWithoutExtension(
                        path),

                Type =
                    type,

                Root =
                    root,

                Children =
                    children,

                Variables =
                    root.Variables
                        .Select(
                            variable =>
                                new VariableData
                                {
                                    Name =
                                        variable.Name,

                                    Value =
                                        variable.Value.Clone()
                                })
                        .ToList(),

                EventModules =
                    eventModules
            };

        new BlueprintSerializer()
            .Save(
                blueprint,
                path);

        RefreshAfterFileOperation();

        log.Info(
            $"Created Blueprint '{Path.GetFileName(path)}' from '{selected.Name}' with {children.Count + 1} GameObject(s).");
    }

    private static void CollectHierarchyIds(
        GameObject gameObject,
        ISet<Guid> ids)
    {
        if (!ids.Add(
                gameObject.Id))
        {
            return;
        }

        foreach (GameObject child
                 in gameObject.Children)
        {
            CollectHierarchyIds(
                child,
                ids);
        }
    }

    // ========================================================
    // EVENT MODULE CREATION
    // ========================================================

    private void DrawCreateEventModuleDialog(
        EditorLog log)
    {
        if (_showCreateEventModule)
        {
            ImGui.OpenPopup(
                "Create Event Module");

            _showCreateEventModule =
                false;
        }

        if (!ImGui.BeginPopupModal(
                "Create Event Module",
                ImGuiWindowFlags.AlwaysAutoResize))
        {
            return;
        }

        ImGui.InputText(
            "Name",
            ref _eventModuleName,
            128);

        ImGui.Combo(
            "Template",
            ref _eventModuleTemplate,
            "Empty\0Character Movement\0");

        if (_eventModuleTemplate ==
            1)
        {
            ImGui.TextDisabled(
                "Creates WASD + Space movement using CharacterController3D.");
        }

        bool valid =
            !string.IsNullOrWhiteSpace(
                _eventModuleName);

        ImGui.BeginDisabled(
            !valid);

        if (ImGui.Button(
                "Create",
                new Vector2(
                    100.0f,
                    0.0f)))
        {
            try
            {
                CreateEventModule(
                    log);

                ImGui.CloseCurrentPopup();
            }
            catch (Exception exception)
            {
                log.Error(
                    $"Could not create Event Module: {exception.Message}");
            }
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        if (ImGui.Button(
                "Cancel",
                new Vector2(
                    100.0f,
                    0.0f)))
        {
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private void CreateEventModule(
        EditorLog log)
    {
        string directory =
            GetAssetCreationDirectory();

        string safeName =
            MakeSafeFileName(
                _eventModuleName);

        string path =
            Path.Combine(
                directory,
                safeName +
                ".byteevents");

        if (File.Exists(
                path))
        {
            throw new IOException(
                "An Event Module with that name already exists.");
        }

        EventModuleDefinition module =
            _eventModuleTemplate ==
            1
                ? EventModuleTemplates
                    .CreateCharacterMovement(
                        safeName)
                : EventModuleTemplates
                    .CreateEmpty(
                        safeName);

        new EventModuleSerializer()
            .Save(
                module,
                path);

        SelectDirectory(
            directory);

        RefreshAfterFileOperation();

        log.Info(
            $"Created Event Module '{Path.GetFileName(path)}'.");

        if (_eventModuleTemplate ==
            1)
        {
            log.Info(
                "Character Movement template: W/S move forward/back, A/D strafe, Space jumps while grounded.");
        }
    }

    // ========================================================
    // FOLDER CREATION
    // ========================================================

    private void DrawCreateFolderDialog(
        EditorLog log)
    {
        if (_showCreateFolder)
        {
            ImGui.OpenPopup(
                "Create Folder");

            _showCreateFolder =
                false;
        }

        if (!ImGui.BeginPopupModal(
                "Create Folder",
                ImGuiWindowFlags.AlwaysAutoResize))
        {
            return;
        }

        ImGui.InputText(
            "Name",
            ref _newFolderName,
            128);

        bool valid =
            !string.IsNullOrWhiteSpace(
                _newFolderName);

        ImGui.BeginDisabled(
            !valid);

        if (ImGui.Button(
                "Create",
                new Vector2(
                    100.0f,
                    0.0f)))
        {
            try
            {
                string safeName =
                    MakeSafeFileName(
                        _newFolderName);

                string path =
                    Path.Combine(
                        _currentDirectory,
                        safeName);

                if (Directory.Exists(
                        path) ||
                    File.Exists(
                        path))
                {
                    throw new IOException(
                        $"'{safeName}' already exists.");
                }

                Directory.CreateDirectory(
                    path);

                RefreshAfterFileOperation();

                log.Info(
                    $"Created folder '{safeName}'.");
            }
            catch (Exception exception)
            {
                log.Error(
                    $"Could not create folder: {exception.Message}");
            }

            ImGui.CloseCurrentPopup();
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        if (ImGui.Button(
                "Cancel",
                new Vector2(
                    100.0f,
                    0.0f)))
        {
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    // ========================================================
    // RENAME
    // ========================================================

    private void BeginRename(
        string path,
        bool isDirectory)
    {
        _renameTargetPath =
            path;

        _renameTargetIsDirectory =
            isDirectory;

        _renameBuffer =
            isDirectory
                ? Path.GetFileName(
                    path)
                : Path.GetFileNameWithoutExtension(
                    path);

        _showRename =
            true;
    }

    private void DrawRenameDialog(
        EditorState state,
        EditorLog log)
    {
        if (_showRename)
        {
            ImGui.OpenPopup(
                "Rename Asset");

            _showRename =
                false;
        }

        if (!ImGui.BeginPopupModal(
                "Rename Asset",
                ImGuiWindowFlags.AlwaysAutoResize))
        {
            return;
        }

        string? target =
            _renameTargetPath;

        ImGui.InputText(
            "Name",
            ref _renameBuffer,
            256);

        bool valid =
            target !=
                null &&
            !string.IsNullOrWhiteSpace(
                _renameBuffer);

        ImGui.BeginDisabled(
            !valid);

        if (ImGui.Button(
                "Rename",
                new Vector2(
                    100.0f,
                    0.0f)))
        {
            if (target !=
                null)
            {
                try
                {
                    string newPath =
                        RenamePath(
                            target,
                            _renameTargetIsDirectory,
                            _renameBuffer);

                    if (!_renameTargetIsDirectory &&
                        state.SelectedAssetPath !=
                            null &&
                        PathsEqualProjectPath(
                            state.SelectedAssetPath,
                            target))
                    {
                        state.SelectedAssetPath =
                            ToProjectPath(
                                newPath);
                    }

                    log.Info(
                        $"Renamed '{Path.GetFileName(target)}' to '{Path.GetFileName(newPath)}'.");
                }
                catch (Exception exception)
                {
                    log.Error(
                        $"Could not rename: {exception.Message}");
                }
            }

            _renameTargetPath =
                null;

            ImGui.CloseCurrentPopup();
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        if (ImGui.Button(
                "Cancel",
                new Vector2(
                    100.0f,
                    0.0f)))
        {
            _renameTargetPath =
                null;

            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private string RenamePath(
        string path,
        bool isDirectory,
        string requestedName)
    {
        if (!File.Exists(
                path) &&
            !Directory.Exists(
                path))
        {
            throw new FileNotFoundException(
                "The item no longer exists.",
                path);
        }

        if (isDirectory &&
            IsProtectedRoot(
                path))
        {
            throw new InvalidOperationException(
                "Project root content folders cannot be renamed.");
        }

        string safeName =
            MakeSafeFileName(
                requestedName);

        string parent =
            Path.GetDirectoryName(
                path)
            ?? throw new InvalidOperationException(
                "Item has no parent folder.");

        string destination;

        if (isDirectory)
        {
            destination =
                Path.Combine(
                    parent,
                    safeName);

            if (Directory.Exists(
                    destination) ||
                File.Exists(
                    destination))
            {
                throw new IOException(
                    $"'{safeName}' already exists.");
            }

            Directory.Move(
                path,
                destination);

            if (IsInsideDirectory(
                    _currentDirectory,
                    path))
            {
                string relative =
                    Path.GetRelativePath(
                        path,
                        _currentDirectory);

                _currentDirectory =
                    relative ==
                    "."
                        ? destination
                        : Path.Combine(
                            destination,
                            relative);
            }
        }
        else
        {
            string extension =
                Path.GetExtension(
                    path);

            destination =
                Path.Combine(
                    parent,
                    safeName +
                    extension);

            if (File.Exists(
                    destination) ||
                Directory.Exists(
                    destination))
            {
                throw new IOException(
                    $"'{Path.GetFileName(destination)}' already exists.");
            }

            MoveAssetFileAndMeta(
                path,
                destination);
        }

        RefreshAfterFileOperation();

        return destination;
    }

    // ========================================================
    // DELETE
    // ========================================================

    private void BeginDelete(
        string path,
        bool isDirectory)
    {
        if (isDirectory &&
            IsProtectedRoot(
                path))
        {
            return;
        }

        _deleteTargetPath =
            path;

        _deleteTargetIsDirectory =
            isDirectory;

        _showDeleteConfirm =
            true;
    }

    private void DrawDeleteDialog(
        EditorState state,
        EditorLog log)
    {
        if (_showDeleteConfirm)
        {
            ImGui.OpenPopup(
                "Delete Asset");

            _showDeleteConfirm =
                false;
        }

        if (!ImGui.BeginPopupModal(
                "Delete Asset",
                ImGuiWindowFlags.AlwaysAutoResize))
        {
            return;
        }

        string? target =
            _deleteTargetPath;

        if (target !=
            null)
        {
            ImGui.TextWrapped(
                _deleteTargetIsDirectory
                    ? $"Delete folder '{Path.GetFileName(target)}' and everything inside it?"
                    : $"Delete asset '{Path.GetFileName(target)}'?");
        }

        ImGui.TextColored(
            new Vector4(
                1.0f,
                0.45f,
                0.30f,
                1.0f),
            "This cannot be undone.");

        bool canDelete =
            target !=
                null;

        ImGui.BeginDisabled(
            !canDelete);

        if (ImGui.Button(
                "Delete",
                new Vector2(
                    100.0f,
                    0.0f)))
        {
            if (target !=
                null)
            {
                try
                {
                    DeletePath(
                        target,
                        _deleteTargetIsDirectory);

                    if (!_deleteTargetIsDirectory &&
                        state.SelectedAssetPath !=
                            null &&
                        PathsEqualProjectPath(
                            state.SelectedAssetPath,
                            target))
                    {
                        state.SelectedAssetId =
                            null;

                        state.SelectedAssetPath =
                            null;
                    }

                    log.Info(
                        $"Deleted '{Path.GetFileName(target)}'.");
                }
                catch (Exception exception)
                {
                    log.Error(
                        $"Could not delete: {exception.Message}");
                }
            }

            _deleteTargetPath =
                null;

            ImGui.CloseCurrentPopup();
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        if (ImGui.Button(
                "Cancel",
                new Vector2(
                    100.0f,
                    0.0f)))
        {
            _deleteTargetPath =
                null;

            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private void DeletePath(
        string path,
        bool isDirectory)
    {
        if (isDirectory)
        {
            if (IsProtectedRoot(
                    path))
            {
                throw new InvalidOperationException(
                    "Project root content folders cannot be deleted.");
            }

            if (IsInsideDirectory(
                    _currentDirectory,
                    path))
            {
                string? parent =
                    Path.GetDirectoryName(
                        path);

                _currentDirectory =
                    parent !=
                        null &&
                    (
                        IsInsideDirectory(
                            parent,
                            GetAssetsRoot()) ||
                        IsInsideDirectory(
                            parent,
                            GetScenesRoot())
                    )
                        ? parent
                        : GetAssetsRoot();
            }

            if (Directory.Exists(
                    path))
            {
                Directory.Delete(
                    path,
                    true);
            }
        }
        else
        {
            if (File.Exists(
                    path))
            {
                File.Delete(
                    path);
            }

            string meta =
                path +
                ".meta";

            if (File.Exists(
                    meta))
            {
                File.Delete(
                    meta);
            }
        }

        RefreshAfterFileOperation();
    }

    // ========================================================
    // MOVE ASSETS
    // ========================================================

    private void MoveAssetToFolder(
        AssetRecord asset,
        string destinationDirectory,
        EditorLog log)
    {
        try
        {
            if (!Directory.Exists(
                    destinationDirectory))
            {
                throw new DirectoryNotFoundException(
                    destinationDirectory);
            }

            string source =
                asset.FullPath;

            string destination =
                Path.Combine(
                    destinationDirectory,
                    Path.GetFileName(
                        source));

            if (PathsEqual(
                    source,
                    destination))
            {
                return;
            }

            if (File.Exists(
                    destination) ||
                Directory.Exists(
                    destination))
            {
                throw new IOException(
                    $"'{Path.GetFileName(destination)}' already exists in '{GetDisplayFolderName(destinationDirectory)}'.");
            }

            MoveAssetFileAndMeta(
                source,
                destination);

            RefreshAfterFileOperation();

            log.Info(
                $"Moved '{Path.GetFileName(source)}' to '{ToProjectPath(destinationDirectory)}'.");
        }
        catch (Exception exception)
        {
            log.Error(
                $"Could not move asset: {exception.Message}");
        }
    }

    private static void MoveAssetFileAndMeta(
        string source,
        string destination)
    {
        string sourceMeta =
            source +
            ".meta";

        string destinationMeta =
            destination +
            ".meta";

        File.Move(
            source,
            destination);

        if (File.Exists(
                sourceMeta) &&
            !File.Exists(
                destinationMeta))
        {
            File.Move(
                sourceMeta,
                destinationMeta);
        }
    }

    // ========================================================
    // DIRECTORY LISTING
    // ========================================================

    private void UpdateListing()
    {
        if (!_listingDirty)
        {
            return;
        }

        _directories.Clear();

        _files.Clear();

        if (!Directory.Exists(
                _currentDirectory))
        {
            return;
        }

        try
        {
            _directories.AddRange(
                Directory
                    .EnumerateDirectories(
                        _currentDirectory)
                    .OrderBy(
                        path =>
                            Path.GetFileName(
                                path),
                        StringComparer.OrdinalIgnoreCase));

            _files.AddRange(
                Directory
                    .EnumerateFiles(
                        _currentDirectory)
                    .Where(
                        path =>
                            !path.EndsWith(
                                ".meta",
                                StringComparison.OrdinalIgnoreCase) &&
                            !path.EndsWith(
                                ".tmp",
                                StringComparison.OrdinalIgnoreCase))
                    .OrderBy(
                        path =>
                            Path.GetFileName(
                                path),
                        StringComparer.OrdinalIgnoreCase));
        }
        catch
        {
        }

        _listingDirty =
            false;
    }

    private void RefreshAfterFileOperation()
    {
        _project.AssetDatabase.Scan();

        _listingDirty =
            true;
    }

    private void SelectDirectory(
        string directory)
    {
        string fullPath =
            Path.GetFullPath(
                directory);

        _currentDirectory =
            fullPath;

        _listingDirty =
            true;
    }

    private void EnsureValidDirectory()
    {
        if (Directory.Exists(
                _currentDirectory))
        {
            return;
        }

        _currentDirectory =
            GetAssetsRoot();

        _listingDirty =
            true;
    }

    // ========================================================
    // PATH HELPERS
    // ========================================================

    private string GetAssetsRoot()
    {
        return _project.ResolveProjectPath(
            _project.Project.AssetDirectory);
    }

    private string GetScenesRoot()
    {
        return _project.ResolveProjectPath(
            _project.Project.SceneDirectory);
    }

    private string GetAssetCreationDirectory()
    {
        string assetsRoot =
            GetAssetsRoot();

        if (IsInsideDirectory(
                _currentDirectory,
                assetsRoot))
        {
            return _currentDirectory;
        }

        return assetsRoot;
    }

    private bool IsProtectedRoot(
        string path)
    {
        return
            PathsEqual(
                path,
                GetAssetsRoot()) ||
            PathsEqual(
                path,
                GetScenesRoot());
    }

    private static bool IsInsideDirectory(
        string path,
        string root)
    {
        string fullPath =
            Path.GetFullPath(
                path)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        string fullRoot =
            Path.GetFullPath(
                root)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

        if (PathsEqual(
                fullPath,
                fullRoot))
        {
            return true;
        }

        string prefix =
            fullRoot +
            Path.DirectorySeparatorChar;

        return fullPath.StartsWith(
            prefix,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(
        string left,
        string right)
    {
        return string.Equals(
            Path.GetFullPath(
                    left)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),

            Path.GetFullPath(
                    right)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),

            StringComparison.OrdinalIgnoreCase);
    }

    private bool PathsEqualProjectPath(
        string projectPath,
        string fullPath)
    {
        return string.Equals(
            projectPath
                .Replace(
                    '\\',
                    '/')
                .TrimStart(
                    '/'),
            ToProjectPath(
                fullPath),
            StringComparison.OrdinalIgnoreCase);
    }

    private string ToProjectPath(
        string fullPath)
    {
        return Path
            .GetRelativePath(
                _project.ProjectRoot,
                fullPath)
            .Replace(
                '\\',
                '/');
    }

    private string Breadcrumb()
    {
        string assetsRoot =
            GetAssetsRoot();

        string scenesRoot =
            GetScenesRoot();

        if (IsInsideDirectory(
                _currentDirectory,
                assetsRoot))
        {
            string relative =
                Path.GetRelativePath(
                    assetsRoot,
                    _currentDirectory);

            return relative ==
                   "."
                ? "Assets"
                : $"Assets/{relative.Replace('\\', '/')}";
        }

        if (IsInsideDirectory(
                _currentDirectory,
                scenesRoot))
        {
            string relative =
                Path.GetRelativePath(
                    scenesRoot,
                    _currentDirectory);

            return relative ==
                   "."
                ? "Scenes"
                : $"Scenes/{relative.Replace('\\', '/')}";
        }

        return _currentDirectory;
    }

    private string GetDisplayFolderName(
        string directory)
    {
        if (PathsEqual(
                directory,
                GetAssetsRoot()))
        {
            return "Assets";
        }

        if (PathsEqual(
                directory,
                GetScenesRoot()))
        {
            return "Scenes";
        }

        return Path.GetFileName(
            directory);
    }

    private static string GetUniqueAssetPath(
        string directory,
        string baseName,
        string extension)
    {
        string path =
            Path.Combine(
                directory,
                baseName +
                extension);

        if (!File.Exists(
                path))
        {
            return path;
        }

        int suffix =
            2;

        while (true)
        {
            path =
                Path.Combine(
                    directory,
                    $"{baseName} {suffix}{extension}");

            if (!File.Exists(
                    path))
            {
                return path;
            }

            suffix++;
        }
    }

    private static string MakeSafeFileName(
        string name)
    {
        string trimmed =
            name.Trim();

        string safeName =
            string.Concat(
                trimmed.Select(
                    character =>
                        Path.GetInvalidFileNameChars()
                            .Contains(
                                character)
                            ? '_'
                            : character));

        return string.IsNullOrWhiteSpace(
            safeName)
                ? "New Asset"
                : safeName;
    }
}
