using System.Numerics;
using System.Text.Json.Nodes;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.VisualLogic;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class AssetsPanel
{
    private readonly EditorProjectContext _project;

    private readonly Action<AssetRecord> _openAsset;

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

        /*
         * Unity-style behavior:
         * open directly inside Assets.
         */
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
            $"Asset database contains {_project.AssetDatabase.Assets.Count} asset(s)."
        );
    }

    public void Draw(
        EditorState state,
        EditorLog log)
    {
        bool isOpen =
            IsOpen;

        ImGui.Begin(
            "Assets",
            ref isOpen
        );

        IsOpen =
            isOpen;

        EnsureValidDirectory();

        DrawToolbar(
            log
        );

        ImGui.Separator();

        /*
         * Unity-style Project browser:
         *
         * LEFT  = folders
         * RIGHT = folder contents/assets
         */
        float folderPaneWidth =
            Math.Clamp(
                ImGui.GetContentRegionAvail().X * 0.25f,
                180.0f,
                300.0f
            );

        ImGui.BeginChild(
            "ProjectFolderTree",
            new Vector2(
                folderPaneWidth,
                0.0f
            ),
            ImGuiChildFlags.None
        );

        DrawFolderTree();

        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild(
            "ProjectFolderContents",
            Vector2.Zero,
            ImGuiChildFlags.None
        );

        DrawCurrentFolder(
            state,
            log
        );

        ImGui.EndChild();

        DrawCreateBlueprintDialog(
            log
        );

        DrawCreateEventModuleDialog(
            log
        );

        ImGui.End();
    }

    // ========================================================
    // TOOLBAR
    // ========================================================

    private void DrawToolbar(
        EditorLog log)
    {
        if (ImGui.SmallButton(
                "Assets"))
        {
            SelectDirectory(
                GetAssetsRoot()
            );
        }

        ImGui.SameLine();

        if (ImGui.SmallButton(
                "Scenes"))
        {
            SelectDirectory(
                GetScenesRoot()
            );
        }

        ImGui.SameLine();

        ImGui.TextDisabled(
            Breadcrumb()
        );

        ImGui.SameLine();

        if (ImGui.SmallButton(
                "Refresh"))
        {
            Refresh(
                log
            );
        }
    }

    // ========================================================
    // FOLDER TREE
    // ========================================================

    private void DrawFolderTree()
    {
        ImGui.TextDisabled(
            "PROJECT"
        );

        ImGui.Separator();

        DrawFolderTreeNode(
            GetAssetsRoot(),
            "Assets",
            true
        );

        DrawFolderTreeNode(
            GetScenesRoot(),
            "Scenes",
            true
        );
    }

    private void DrawFolderTreeNode(
        string directory,
        string displayName,
        bool defaultOpen)
    {
        if (!Directory.Exists(
                directory))
        {
            return;
        }

        string[] children =
            GetDirectoriesSafe(
                directory
            );

        bool hasChildren =
            children.Length >
            0;

        bool selected =
            PathsEqual(
                directory,
                _currentDirectory
            );

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
                flags
            );

        if (ImGui.IsItemClicked(
                ImGuiMouseButton.Left))
        {
            SelectDirectory(
                directory
            );
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
                    child
                ),
                false
            );
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
                    directory
                )
                .OrderBy(
                    path =>
                        Path.GetFileName(
                            path
                        ),
                    StringComparer.OrdinalIgnoreCase
                )
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    // ========================================================
    // CURRENT FOLDER
    // ========================================================

    private void DrawCurrentFolder(
        EditorState state,
        EditorLog log)
    {
        UpdateListing();

        string folderName =
            GetDisplayFolderName(
                _currentDirectory
            );

        ImGui.Text(
            folderName
        );

        ImGui.SameLine();

        ImGui.TextDisabled(
            $"{_directories.Count} folder(s), {_files.Count} asset(s)"
        );

        ImGui.Separator();

        DrawDirectories();

        DrawFiles(
            state,
            log
        );

        DrawWindowContextMenu();
    }

    private void DrawDirectories()
    {
        foreach (string directory
                 in _directories)
        {
            string folderName =
                Path.GetFileName(
                    directory
                );

            bool clicked =
                ImGui.Selectable(
                    $"[DIR] {folderName}##content-folder:{directory}"
                );

            if (clicked &&
                ImGui.IsMouseDoubleClicked(
                    ImGuiMouseButton.Left))
            {
                SelectDirectory(
                    directory
                );
            }
        }
    }

    // ========================================================
    // ASSET FILES
    // ========================================================

    private void DrawFiles(
        EditorState state,
        EditorLog log)
    {
        foreach (string file
                 in _files)
        {
            string projectPath =
                Path.GetRelativePath(
                        _project.ProjectRoot,
                        file
                    )
                    .Replace(
                        '\\',
                        '/'
                    );

            _project.AssetDatabase.TryGetAsset(
                projectPath,
                out AssetRecord? asset
            );

            string icon =
                GetAssetIcon(
                    asset?.Type
                );

            bool selected =
                string.Equals(
                    state.SelectedAssetPath,
                    projectPath,
                    StringComparison.OrdinalIgnoreCase
                );

            bool clicked =
                ImGui.Selectable(
                    $"{icon} {Path.GetFileName(file)}##asset:{file}",
                    selected
                );

            if (clicked)
            {
                state.SelectedAssetId =
                    asset?.Guid;

                state.SelectedAssetPath =
                    projectPath;

                state.SelectedObject =
                    null;

                if (asset != null &&
                    ImGui.IsMouseDoubleClicked(
                        ImGuiMouseButton.Left))
                {
                    _openAsset(
                        asset
                    );
                }
            }

            DrawAssetContextMenu(
                state,
                log,
                asset,
                file
            );

            DrawAssetDragSource(
                asset,
                file
            );
        }
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
        if (asset == null)
        {
            return;
        }

        if (!ImGui.BeginPopupContextItem(
                $"AssetContext##{file}"))
        {
            return;
        }

        if (ImGui.MenuItem(
                "Open"))
        {
            _openAsset(
                asset
            );
        }

        if (asset.Type ==
            AssetType.EventModule)
        {
            ImGui.Separator();

            bool canAttach =
                state.Mode ==
                    EditorMode.Edit &&
                state.SelectedObject !=
                    null;

            ImGui.BeginDisabled(
                !canAttach
            );

            if (ImGui.MenuItem(
                    "Attach to Selected GameObject"))
            {
                AttachEventModule(
                    state,
                    log,
                    asset
                );
            }

            ImGui.EndDisabled();

            if (!canAttach)
            {
                ImGui.TextDisabled(
                    "Select a GameObject in the Hierarchy first."
                );
            }
        }

        ImGui.EndPopup();
    }

    private static void DrawAssetDragSource(
        AssetRecord? asset,
        string file)
    {
        if (asset == null)
        {
            return;
        }

        bool draggable =
            asset.Type is
                AssetType.Texture2D or
                AssetType.Model3D or
                AssetType.Blueprint or
                AssetType.EventModule;

        if (!draggable)
        {
            return;
        }

        if (!ImGui.BeginDragDropSource())
        {
            return;
        }

        AssetDragDrop.Set(
            asset.Guid
        );

        ImGui.Text(
            $"{asset.Type}: {Path.GetFileName(file)}"
        );

        ImGui.EndDragDropSource();
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

        if (target == null)
        {
            log.Warning(
                "Select a GameObject before attaching an Event Module."
            );

            return;
        }

        EventModuleComponent? existing =
            target.GetComponent<EventModuleComponent>();

        if (existing != null &&
            existing.Modules.Any(
                reference =>
                    reference.Guid ==
                    asset.Guid))
        {
            log.Warning(
                $"'{asset.ProjectPath}' is already attached to '{target.Name}'."
            );

            return;
        }

        EventModuleDefinition definition;

        try
        {
            definition =
                new EventModuleSerializer()
                    .Load(
                        asset.FullPath
                    );
        }
        catch (Exception exception)
        {
            log.Error(
                $"Could not load Event Module '{asset.ProjectPath}': {exception.Message}"
            );

            return;
        }

        var reference =
            new AssetReference(
                asset.Guid,
                asset.ProjectPath
            );

        void Attach()
        {
            EventModuleComponent component =
                target.GetComponent<EventModuleComponent>()
                ?? target.AddComponent(
                    new EventModuleComponent()
                );

            component.AddResolvedModule(
                reference,
                definition
            );
        }

        if (state.Undo != null)
        {
            state.Undo.Execute(
                state,
                "Attach Event Module",
                Attach
            );
        }
        else
        {
            Attach();

            state.MarkDirty();
        }

        log.Info(
            $"Attached Event Module '{definition.Name}' to '{target.Name}'."
        );
    }

    // ========================================================
    // EMPTY AREA CONTEXT MENU
    // ========================================================

    private void DrawWindowContextMenu()
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
                    "Byte Blueprint"))
            {
                _showCreateBlueprint =
                    true;
            }

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
                "Create Byte Blueprint"
            );

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
            128
        );

        ImGui.Combo(
            "Type",
            ref _blueprintType,
            "Generic Object\0Character\0"
        );

        bool valid =
            !string.IsNullOrWhiteSpace(
                _blueprintName
            );

        ImGui.BeginDisabled(
            !valid
        );

        if (ImGui.Button(
                "Create",
                new Vector2(
                    100.0f,
                    0.0f
                )))
        {
            try
            {
                CreateBlueprint(
                    log
                );

                ImGui.CloseCurrentPopup();
            }
            catch (Exception exception)
            {
                log.Error(
                    $"Could not create Blueprint: {exception.Message}"
                );
            }
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        if (ImGui.Button(
                "Cancel",
                new Vector2(
                    100.0f,
                    0.0f
                )))
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
                _blueprintName
            );

        string path =
            Path.Combine(
                directory,
                safeName +
                ".byteblueprint"
            );

        if (File.Exists(
                path))
        {
            throw new IOException(
                "A Blueprint with that name already exists."
            );
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
                }
            );

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
                }
            );

            root.Components.Add(
                new ComponentData
                {
                    Type =
                        "AnimationController"
                }
            );
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
                                            100
                                        )
                            },

                            new()
                            {
                                Name =
                                    "MoveSpeed",

                                Value =
                                    ByteEngine.Core.Variables
                                        .VariableValue
                                        .FromNumber(
                                            6
                                        )
                            },

                            new()
                            {
                                Name =
                                    "Team",

                                Value =
                                    ByteEngine.Core.Variables
                                        .VariableValue
                                        .FromString(
                                            "Player"
                                        )
                            }
                        }
                        : new List<VariableData>()
            };

        new BlueprintSerializer()
            .Save(
                blueprint,
                path
            );

        SelectDirectory(
            directory
        );

        _project.AssetDatabase.Scan();

        _listingDirty =
            true;

        log.Info(
            $"Created {type} Blueprint '{Path.GetFileName(path)}'."
        );
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
                "Create Event Module"
            );

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
            128
        );

        ImGui.Combo(
            "Template",
            ref _eventModuleTemplate,
            "Empty\0Character Movement\0"
        );

        if (_eventModuleTemplate ==
            1)
        {
            ImGui.TextDisabled(
                "Creates WASD + Space movement using CharacterController3D."
            );
        }

        bool valid =
            !string.IsNullOrWhiteSpace(
                _eventModuleName
            );

        ImGui.BeginDisabled(
            !valid
        );

        if (ImGui.Button(
                "Create",
                new Vector2(
                    100.0f,
                    0.0f
                )))
        {
            try
            {
                CreateEventModule(
                    log
                );

                ImGui.CloseCurrentPopup();
            }
            catch (Exception exception)
            {
                log.Error(
                    $"Could not create Event Module: {exception.Message}"
                );
            }
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        if (ImGui.Button(
                "Cancel",
                new Vector2(
                    100.0f,
                    0.0f
                )))
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
                _eventModuleName
            );

        string path =
            Path.Combine(
                directory,
                safeName +
                ".byteevents"
            );

        if (File.Exists(
                path))
        {
            throw new IOException(
                "An Event Module with that name already exists."
            );
        }

        EventModuleDefinition module =
            _eventModuleTemplate ==
            1
                ? EventModuleTemplates
                    .CreateCharacterMovement(
                        safeName
                    )
                : EventModuleTemplates
                    .CreateEmpty(
                        safeName
                    );

        new EventModuleSerializer()
            .Save(
                module,
                path
            );

        SelectDirectory(
            directory
        );

        _project.AssetDatabase.Scan();

        _listingDirty =
            true;

        log.Info(
            $"Created Event Module '{Path.GetFileName(path)}'."
        );

        if (_eventModuleTemplate ==
            1)
        {
            log.Info(
                "Character Movement template: W/S move forward/back, A/D strafe, Space jumps while grounded."
            );
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
                        _currentDirectory
                    )
                    .OrderBy(
                        path =>
                            Path.GetFileName(
                                path
                            ),
                        StringComparer.OrdinalIgnoreCase
                    )
            );

            _files.AddRange(
                Directory
                    .EnumerateFiles(
                        _currentDirectory
                    )
                    .Where(
                        path =>
                            !path.EndsWith(
                                ".meta",
                                StringComparison.OrdinalIgnoreCase
                            ) &&
                            !path.EndsWith(
                                ".tmp",
                                StringComparison.OrdinalIgnoreCase
                            )
                    )
                    .OrderBy(
                        path =>
                            Path.GetFileName(
                                path
                            ),
                        StringComparer.OrdinalIgnoreCase
                    )
            );
        }
        catch
        {
            /*
             * File system may change between frames.
             */
        }

        _listingDirty =
            false;
    }

    private void SelectDirectory(
        string directory)
    {
        string fullPath =
            Path.GetFullPath(
                directory
            );

        if (PathsEqual(
                fullPath,
                _currentDirectory))
        {
            _listingDirty =
                true;

            return;
        }

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
            _project.Project.AssetDirectory
        );
    }

    private string GetScenesRoot()
    {
        return _project.ResolveProjectPath(
            _project.Project.SceneDirectory
        );
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

    private static bool IsInsideDirectory(
        string path,
        string root)
    {
        string fullPath =
            Path.GetFullPath(
                path
            )
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            );

        string fullRoot =
            Path.GetFullPath(
                root
            )
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            );

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
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static bool PathsEqual(
        string left,
        string right)
    {
        return string.Equals(
            Path.GetFullPath(
                    left
                )
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                ),

            Path.GetFullPath(
                    right
                )
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                ),

            StringComparison.OrdinalIgnoreCase
        );
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
                    _currentDirectory
                );

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
                    _currentDirectory
                );

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
            directory
        );
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
                            : character
                )
            );

        return string.IsNullOrWhiteSpace(
            safeName)
                ? "New Asset"
                : safeName;
    }
}