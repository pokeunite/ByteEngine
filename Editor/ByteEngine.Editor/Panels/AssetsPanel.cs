using System.Numerics;
using System.Text.Json.Nodes;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Editor.Selection;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class AssetsPanel : IDisposable
{
    private readonly EditorProjectContext _project;

    private readonly EditorDocumentManager _documents;

    private readonly Action<AssetRecord> _openAsset;

    private readonly EditorDocumentWindowManager _documentWindows;

    private Renderer2D? _renderer;
    private Renderer3D? _renderer3D;

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

    private bool _showCreateAnimationProfile;

    private string _animationProfileName =
        "New Animation Profile";

    private bool _showCreateFolder;

    private string _newFolderName =
        "New Folder";

    private bool _showRename;

    private string? _renameTargetPath;

    private bool _renameTargetIsDirectory;

    private string _renameBuffer =
        string.Empty;

    private readonly AssetDeleteInteractionState _deleteInteraction = new();

    private readonly AssetSelectionModel _assetSelection = new();
    private readonly List<AssetSelectionBounds> _assetBounds = new();
    private bool _assetMarquee;
    private Vector2 _assetMarqueeStart;
    private Vector2 _assetMarqueeEnd;

    private bool _focusNextDraw =
        true;

    private bool _gridView =
        true;

    private string _assetSearch =
        string.Empty;

    private string? _expandedModelPath;

    private float _assetTileScale =
        EditorPreferences.AssetIconScale;

    private float BrowserTileScale =>
        Math.Clamp(
            _assetTileScale,
            0.50f,
            1.65f);

    private float BrowserIconSize =>
        48.0f *
        BrowserTileScale;

    private float BrowserTileWidth =>
        158.0f *
        BrowserTileScale;

    private float BrowserTileHeight =>
        BrowserIconSize +
        ImGui.GetTextLineHeight() *
        3.0f +
        44.0f *
        BrowserTileScale;

    public bool IsOpen { get; set; } =
        true;

    /// <summary>
    /// Folder currently displayed by the Asset Browser. External OS file drops
    /// import here when this path is inside the project's Assets directory.
    /// </summary>
    public string CurrentImportDirectory =>
        _currentDirectory;

    public AssetsPanel(
        EditorProjectContext project,
        Action<AssetRecord> openAsset,
        EditorDocumentManager documents,
        EditorDocumentWindowManager documentWindows)
    {
        _project = project;
        _openAsset = openAsset;
        _documents = documents;
        _documentWindows = documentWindows;

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
        EditorLog log,
        Renderer2D renderer,
        Renderer3D renderer3D,
        int windowWidth,
        int windowHeight)
    {
        _renderer = renderer;
        _renderer3D = renderer3D;
        bool isOpen =
            IsOpen;

        if (_focusNextDraw)
        {
            ImGui.SetNextWindowFocus();

            _focusNextDraw =
                false;
        }

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

            DrawCreateAnimationProfileDialog(
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
        else
        {
            _deleteInteraction.Reset();
            _assetMarquee = false;
        }

        ImGui.End();


    }

    public void DrawWorkspaces(EditorLog log, Renderer2D renderer, Renderer3D renderer3D, int windowWidth, int windowHeight) { }

    public bool DrawActiveDocumentInspector() => false;
    // ========================================================
    // TOOLBAR
    // ========================================================

    private void DrawToolbar(
        EditorState state,
        EditorLog log)
    {
        float commandBarWidth = ImGui.GetContentRegionAvail().X;
        bool compactCommandBar = commandBarWidth < 640.0f;
        EditorUi.BeginToolbar("##AssetCommandBar",
            compactCommandBar ? EditorTheme.StandardControlHeight * 2.0f + 10.0f : EditorTheme.ToolbarHeight);

        if (EditorUi.PrimaryButton("+ Create", size: new Vector2(0.0f, EditorTheme.StandardControlHeight)))
            ImGui.OpenPopup("AssetBrowserCreateMenu");

        if (ImGui.BeginPopup("AssetBrowserCreateMenu"))
        {
            if (ImGui.MenuItem("New Folder")) { _newFolderName = "New Folder"; _showCreateFolder = true; }
            if (ImGui.MenuItem("Byte Blueprint")) _showCreateBlueprint = true;
            if (ImGui.MenuItem("Event Module")) _showCreateEventModule = true;
            if (ImGui.MenuItem("Animation Profile"))
            {
                _animationProfileName = "New Animation Profile";
                _showCreateAnimationProfile = true;
            }

            bool canCreateFromSelection = state.Mode == EditorMode.Edit && state.SelectedObject != null &&
                IsInsideDirectory(_currentDirectory, GetAssetsRoot());
            ImGui.BeginDisabled(!canCreateFromSelection);
            if (ImGui.MenuItem("Blueprint From Selected")) CreateBlueprintFromSelectedObject(state, log);
            ImGui.EndDisabled();
            ImGui.EndPopup();
        }

        ImGui.SameLine();
        if (EditorUi.ToolbarButton("Refresh", "Refresh the asset database")) Refresh(log);
        EditorUi.ToolbarSeparator();

        const float trailingControlsWidth = 252.0f;
        float searchWidth = Math.Max(
            ImGui.GetContentRegionAvail().X - (compactCommandBar ? 0.0f : trailingControlsWidth),
            compactCommandBar ? 80.0f : 100.0f);
        EditorUi.SearchField("##AssetBrowserSearch", ref _assetSearch, 128, searchWidth);
        if (compactCommandBar)
            ImGui.NewLine();
        else
            ImGui.SameLine();
        if (EditorUi.ToolbarToggle("Grid", _gridView, "Grid view")) _gridView = true;
        ImGui.SameLine();
        if (EditorUi.ToolbarToggle("List", !_gridView, "List view")) _gridView = false;
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100.0f);
        if (ImGui.SliderFloat("##AssetIconSize", ref _assetTileScale, 0.50f, 1.65f, "%.2fx",
                ImGuiSliderFlags.AlwaysClamp))
        {
            EditorPreferences.AssetIconScale = _assetTileScale;
        }
        EditorUi.Tooltip("Asset thumbnail size");

        EditorUi.EndToolbar();

        ImGui.PushStyleColor(ImGuiCol.ChildBg, EditorTheme.BackgroundRaised);
        ImGui.BeginChild("##AssetBreadcrumb", new Vector2(0.0f, 28.0f), ImGuiChildFlags.Borders,
            ImGuiWindowFlags.NoScrollbar);
        ImGui.TextColored(EditorTheme.TextSecondary, Breadcrumb().Replace("/", "  >  "));
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    // ========================================================
    // FOLDER TREE
    // ========================================================

    private void DrawFolderTree(
        EditorLog log)
    {
        ImGui.TextColored(EditorTheme.TextMuted, "FOLDERS");
        ImGui.Spacing();

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

        ImGui.Text(folderName);
        ImGui.SameLine();

        int visibleFolderCount =
            _directories.Count(
                directory =>
                    MatchesSearch(
                        Path.GetFileName(directory)));

        int visibleFileCount =
            _files.Count(
                file =>
                    MatchesSearch(
                        Path.GetFileName(file)));

        ImGui.TextDisabled(
            string.IsNullOrWhiteSpace(_assetSearch)
                ? $"{_directories.Count} folder(s), {_files.Count} asset(s)"
                : $"{visibleFolderCount} folder(s), {visibleFileCount} asset(s) match");

        ImGui.Separator();

        if (visibleFolderCount == 0 && visibleFileCount == 0)
            EditorUi.EmptyState("This folder is empty.", "Drop assets here or use Create.");

        if (_gridView)
        {
            DrawAssetGrid(
                state,
                log);
        }
        else
        {
            DrawDirectories(log);
            DrawFiles(
                state,
                log);
        }

        if (AssetDeleteCommand.ShouldBegin(
                ImGui.IsWindowFocused(
                    ImGuiFocusedFlags.RootAndChildWindows),
                ImGui.IsKeyPressed(ImGuiKey.Delete),
                _assetSelection.Count))
        {
            BeginDeleteSelected();
        }

        DrawWindowContextMenu(
            state,
            log);
    }

    private void DrawAssetGrid(
        EditorState state,
        EditorLog log)
    {
        string[] orderedFiles =
            _files.ToArray();

        _assetSelection.Retain(
            orderedFiles);

        _assetBounds.Clear();

        float availableWidth =
            Math.Max(
                ImGui.GetContentRegionAvail().X,
                BrowserTileWidth);

        const float spacing = 9.0f;

        int columns =
            Math.Max(
                1,
                (int)(
                    (availableWidth + spacing) /
                    (BrowserTileWidth + spacing)));

        int slot = 0;

        foreach (string directory
                 in _directories.ToArray())
        {
            string folderName =
                Path.GetFileName(directory);

            if (!MatchesSearch(folderName))
            {
                continue;
            }

            BeginGridSlot(
                slot,
                columns);

            DrawDirectoryTile(
                directory,
                folderName,
                log);

            slot++;
        }

        for (int fileIndex = 0;
             fileIndex < orderedFiles.Length;
             fileIndex++)
        {
            string file =
                orderedFiles[fileIndex];

            string fileName =
                Path.GetFileName(file);

            if (!MatchesSearch(fileName))
            {
                continue;
            }

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

            BeginGridSlot(
                slot,
                columns);

            DrawAssetTile(
                state,
                log,
                orderedFiles,
                fileIndex,
                file,
                asset);

            slot++;

            if (asset?.Type != AssetType.Model3D ||
                !string.Equals(
                    _expandedModelPath,
                    file,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                ModelAsset model =
                    _project.Assets.LoadModel(
                        new AssetReference(
                            asset.Guid,
                            asset.ProjectPath));

                foreach (ImportedAnimation animation
                         in model.Animations)
                {
                    if (!MatchesSearch(animation.Name) &&                        !string.IsNullOrWhiteSpace(_assetSearch))
                    {
                        continue;
                    }

                    BeginGridSlot(
                        slot,
                        columns);

                    DrawAnimationTile(
                        state,
                        log,
                        asset,
                        animation);

                    slot++;
                }
            }
            catch (Exception exception)
            {
                log.Warning(
                    $"Could not list animation clips for '{asset.ProjectPath}': {exception.Message}");
            }
        }

        DrawGridSelectionMarquee(state);
    }

    private static void BeginGridSlot(
        int slot,
        int columns)
    {
        if (slot > 0 &&
            slot % columns != 0)
        {
            ImGui.SameLine();
        }
    }

    private void DrawDirectoryTile(
        string directory,
        string folderName,
        EditorLog log)
    {
        ImGui.PushID(
            $"folder-tile:{directory}");

        Vector2 minimum =
            ImGui.GetCursorScreenPos();

        ImGui.InvisibleButton(
            "##FolderTile",
            new Vector2(
                BrowserTileWidth,
                BrowserTileHeight));

        Vector2 maximum =
            ImGui.GetItemRectMax();

        bool hovered =
            ImGui.IsItemHovered();

        DrawBrowserTileVisual(
            minimum,
            maximum,
            EditorIconKind.Folder,
            folderName,
            "Folder",
            false,
            hovered);

        if (hovered &&
            ImGui.IsMouseDoubleClicked(
                ImGuiMouseButton.Left))
        {
            SelectDirectory(directory);
        }

        if (hovered)
        {
            ImGui.SetTooltip(folderName);
        }

        DrawAssetDropTarget(
            directory,
            log);

        if (ImGui.BeginPopupContextItem(
                "FolderTileContext"))
        {
            if (ImGui.MenuItem("Open"))
            {
                SelectDirectory(directory);
            }

            if (ImGui.MenuItem("New Folder Here"))
            {
                SelectDirectory(directory);
                _newFolderName = "New Folder";
                _showCreateFolder = true;
            }

            ImGui.Separator();

            if (ImGui.MenuItem("Rename"))
            {
                BeginRename(
                    directory,
                    true);
            }

            if (ImGui.MenuItem("Delete"))
            {
                BeginDelete(
                    directory,
                    true);
            }

            ImGui.EndPopup();
        }

        ImGui.PopID();
    }

    private void DrawAssetTile(
        EditorState state,
        EditorLog log,
        IReadOnlyList<string> orderedFiles,
        int fileIndex,
        string file,
        AssetRecord? asset)
    {
        ImGui.PushID(
            $"asset-tile:{file}");

        bool selected =
            _assetSelection.Contains(file) &&
            string.IsNullOrWhiteSpace(
                state.SelectedModelAnimationKey);

        Vector2 minimum =
            ImGui.GetCursorScreenPos();

        ImGui.InvisibleButton(
            "##AssetTile",
            new Vector2(
                BrowserTileWidth,
                BrowserTileHeight));

        Vector2 maximum =
            ImGui.GetItemRectMax();

        bool hovered =
            ImGui.IsItemHovered();

        bool modelExpanded =
            asset?.Type == AssetType.Model3D &&
            string.Equals(
                _expandedModelPath,
                file,
                StringComparison.OrdinalIgnoreCase);

        bool hasExpandableClips =
            asset?.Type ==
                AssetType.Model3D;

        DrawBrowserTileVisual(
            minimum,
            maximum,
            EditorIcons.ForAsset(asset?.Type),
            GetTileDisplayName(
                file,
                asset?.Type),
            EditorIcons.AssetTypeName(asset?.Type),
            selected,
            hovered,
            hasExpandableClips
                ? "Clips"
                : null,
            hasExpandableClips,
            modelExpanded);

        _assetBounds.Add(
            new AssetSelectionBounds(
                file,
                minimum,
                maximum));

        if (ImGui.IsItemClicked(
                ImGuiMouseButton.Left))
        {
            Vector2 mouse =
                ImGui.GetMousePos();

            bool clickedModelFooter =
                asset?.Type == AssetType.Model3D &&
                mouse.Y >=
                    maximum.Y -
                    28.0f *
                    BrowserTileScale;

            if (clickedModelFooter)
            {
                _expandedModelPath =
                    modelExpanded
                        ? null
                        : file;
            }
            else
            {
                ImGuiIOPtr io =
                    ImGui.GetIO();

                _assetSelection.Click(
                    orderedFiles,
                    fileIndex,
                    io.KeyCtrl,
                    io.KeyShift);

                SyncPrimaryAssetSelection(state);
            }
        }

        if (hovered &&
            ImGui.IsMouseDoubleClicked(
                ImGuiMouseButton.Left))
        {
            if (asset?.Type == AssetType.Model3D)
            {
                _expandedModelPath =
                    modelExpanded
                        ? null
                        : file;
            }
            else if (asset != null)
            {
                OpenAsset(
                    asset,
                    log);
            }
        }

        if (hovered)
        {
            ImGui.SetTooltip(
                $"{Path.GetFileName(file)}\n{EditorIcons.AssetTypeName(asset?.Type)}");
        }

        DrawAssetContextMenu(
            state,
            log,
            asset,
            file);

        DrawAssetDragSource(
            asset,
            file);

        ImGui.PopID();
    }

    private void DrawAnimationTile(
        EditorState state,
        EditorLog log,
        AssetRecord asset,
        ImportedAnimation animation)
    {
        ImGui.PushID(
            $"animation-tile:{asset.Guid}:{animation.Key}");

        bool selected =
            state.SelectedAssetId == asset.Guid &&
            string.Equals(
                state.SelectedModelAnimationKey,
                animation.Key,
                StringComparison.Ordinal);

        Vector2 minimum =
            ImGui.GetCursorScreenPos();

        ImGui.InvisibleButton(
            "##AnimationTile",
            new Vector2(
                BrowserTileWidth,
                BrowserTileHeight));

        Vector2 maximum =
            ImGui.GetItemRectMax();

        bool hovered =
            ImGui.IsItemHovered();

        DrawBrowserTileVisual(
            minimum,
            maximum,
            EditorIconKind.Animation,
            animation.Name,
            "Animation Clip",
            selected,
            hovered,
            $"{animation.Duration:0.00}s");

        if (ImGui.IsItemClicked(
                ImGuiMouseButton.Left))
        {
            state.SelectedAssetId =
                asset.Guid;

            state.SelectedAssetPath =
                asset.ProjectPath;

            state.SelectedModelAnimationKey =
                animation.Key;

            state.SelectedObject =
                null;
        }

        DrawAnimationOpenActions(asset, animation, log);

        if (hovered)
        {
            ImGui.SetTooltip(
                $"Animation Clip\n{animation.Name}\nDuration: {animation.Duration:0.000}s");
        }

        ImGui.PopID();
    }

    private void DrawBrowserTileVisual(
        Vector2 minimum,
        Vector2 maximum,
        EditorIconKind icon,
        string name,
        string typeName,
        bool selected,
        bool hovered,
        string? footer = null,
        bool showExpandArrow = false,
        bool expanded = false)
    {
        var drawList =
            ImGui.GetWindowDrawList();

        float scale =
            BrowserTileScale;

        float lineHeight =
            ImGui.GetTextLineHeight();

        Vector4 background =
            selected
                ? new Vector4(
                    EditorTheme.Accent.X,
                    EditorTheme.Accent.Y,
                    EditorTheme.Accent.Z,
                    0.28f)
                : hovered
                    ? EditorTheme.PanelHover
                    : EditorTheme.PanelRaised;

        drawList.AddRectFilled(
            minimum,
            maximum,
            ImGui.GetColorU32(
                background),
            EditorTheme.SmallCornerRadius);

        drawList.AddRect(
            minimum,
            maximum,
            ImGui.GetColorU32(
                selected
                    ? EditorTheme.Accent
                    : EditorTheme.Border),
            EditorTheme.SmallCornerRadius);

        float iconSize =
            BrowserIconSize;

        float iconTop =
            minimum.Y +
            10.0f *
            scale;

        EditorIcons.DrawTileIcon(
            icon,
            new Vector2(
                minimum.X +
                (
                    BrowserTileWidth -
                    iconSize
                ) *
                0.5f,
                iconTop),
            iconSize);

        float textWidth =
            BrowserTileWidth -
            18.0f *
            scale;

        float nameY =
            iconTop +            iconSize +
            9.0f *
            scale;

        string displayName =
            FitTileText(
                name,
                textWidth);

        Vector2 nameSize =
            ImGui.CalcTextSize(
                displayName);

        drawList.AddText(
            new Vector2(
                minimum.X +
                Math.Max(
                    (
                        BrowserTileWidth -
                        nameSize.X
                    ) *
                    0.5f,
                    5.0f),
                nameY),
            ImGui.GetColorU32(
                EditorTheme.Text),
            displayName);

        float typeY =
            nameY +
            lineHeight +
            4.0f *
            scale;

        string displayType =
            FitTileText(
                typeName,
                textWidth);

        Vector2 typeSize =
            ImGui.CalcTextSize(
                displayType);

        drawList.AddText(
            new Vector2(
                minimum.X +
                Math.Max(
                    (
                        BrowserTileWidth -
                        typeSize.X
                    ) *
                    0.5f,
                    5.0f),
                typeY),
            ImGui.GetColorU32(
                EditorTheme.TextMuted),
            displayType);

        if (string.IsNullOrWhiteSpace(
                footer))
        {
            return;
        }

        float footerTop =
            typeY +
            lineHeight +
            5.0f *
            scale;

        drawList.AddRectFilled(
            new Vector2(
                minimum.X +
                5.0f *
                scale,
                footerTop),
            new Vector2(
                maximum.X -
                5.0f *
                scale,
                maximum.Y -
                6.0f *
                scale),
            ImGui.GetColorU32(
                new Vector4(
                    0.08f,
                    0.095f,
                    0.12f,
                    0.72f)),
            3.0f);

        if (showExpandArrow)
        {
            float arrowSize =
                5.5f *
                scale;

            Vector2 arrowCenter =
                new(
                    minimum.X +
                    18.0f *
                    scale,
                    footerTop +
                    lineHeight *
                    0.55f);

            uint animationColor =
                ImGui.GetColorU32(
                    EditorIcons.Color(
                        EditorIconKind.Animation));

            if (expanded)
            {
                drawList.AddTriangleFilled(
                    new Vector2(
                        arrowCenter.X -
                        arrowSize,
                        arrowCenter.Y -
                        arrowSize *
                        0.55f),
                    new Vector2(
                        arrowCenter.X +
                        arrowSize,
                        arrowCenter.Y -
                        arrowSize *
                        0.55f),
                    new Vector2(
                        arrowCenter.X,
                        arrowCenter.Y +
                        arrowSize),
                    animationColor);
            }
            else
            {
                drawList.AddTriangleFilled(
                    new Vector2(
                        arrowCenter.X -
                        arrowSize *
                        0.55f,
                        arrowCenter.Y -
                        arrowSize),
                    new Vector2(
                        arrowCenter.X -
                        arrowSize *
                        0.55f,
                        arrowCenter.Y +
                        arrowSize),
                    new Vector2(
                        arrowCenter.X +
                        arrowSize,
                        arrowCenter.Y),
                    animationColor);
            }

            drawList.AddText(
                new Vector2(
                    minimum.X +
                    31.0f *
                    scale,
                    footerTop),
                animationColor,
                footer);

            return;
        }

        string displayFooter =
            FitTileText(
                footer,
                textWidth);

        Vector2 footerSize =
            ImGui.CalcTextSize(
                displayFooter);

        drawList.AddText(
            new Vector2(
                minimum.X +
                Math.Max(
                    (
                        BrowserTileWidth -
                        footerSize.X
                    ) *
                    0.5f,
                    5.0f),
                footerTop),
            ImGui.GetColorU32(
                EditorTheme.TextMuted),
            displayFooter);
    }

    private static string GetTileDisplayName(
        string file,
        AssetType? type)
    {
        if (type ==
                null ||
            type ==
                AssetType.Unknown)
        {
            return
                Path.GetFileName(
                    file);
        }

        return
            Path.GetFileNameWithoutExtension(
                file);
    }

    private static string FitTileText(        string text,
        float maximumWidth)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            ImGui.CalcTextSize(text).X <= maximumWidth)
        {
            return text;
        }

        string extension =
            Path.GetExtension(text);

        string stem =
            string.IsNullOrWhiteSpace(extension)
                ? text
                : Path.GetFileNameWithoutExtension(text);

        while (stem.Length > 3)
        {
            string candidate =
                stem +
                "..." +
                extension;

            if (ImGui.CalcTextSize(candidate).X <= maximumWidth)
            {
                return candidate;
            }

            stem =
                stem[..^1];
        }

        return
            stem +
            "..." +
            extension;
    }

    private bool MatchesSearch(
        string text)
    {
        return
            string.IsNullOrWhiteSpace(_assetSearch) ||
            text.Contains(
                _assetSearch.Trim(),
                StringComparison.OrdinalIgnoreCase);
    }

    private void DrawGridSelectionMarquee(
        EditorState state)
    {
        bool emptySpaceClicked =
            ImGui.IsWindowHovered() &&
            ImGui.IsMouseClicked(
                ImGuiMouseButton.Left) &&
            !ImGui.IsAnyItemHovered();

        if (emptySpaceClicked)
        {
            _assetMarquee = true;
            _assetMarqueeStart = ImGui.GetMousePos();
            _assetMarqueeEnd = _assetMarqueeStart;
        }

        if (!_assetMarquee)
        {
            return;
        }

        _assetMarqueeEnd =
            ImGui.GetMousePos();

        Vector2 minimum =
            Vector2.Min(
                _assetMarqueeStart,
                _assetMarqueeEnd);

        Vector2 maximum =
            Vector2.Max(
                _assetMarqueeStart,
                _assetMarqueeEnd);

        ImGui.GetWindowDrawList()
            .AddRectFilled(
                minimum,
                maximum,
                ImGui.GetColorU32(
                    new Vector4(
                        EditorTheme.Accent.X,
                        EditorTheme.Accent.Y,
                        EditorTheme.Accent.Z,
                        0.12f)));

        ImGui.GetWindowDrawList()
            .AddRect(
                minimum,
                maximum,
                ImGui.GetColorU32(EditorTheme.Accent));

        if (ImGui.IsMouseDown(
                ImGuiMouseButton.Left))
        {
            return;
        }

        _assetSelection.Marquee(
            _assetBounds,
            _assetMarqueeStart,
            _assetMarqueeEnd,
            ImGui.GetIO().KeyCtrl);

        SyncPrimaryAssetSelection(state);
        _assetMarquee = false;
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
        string[] orderedFiles = _files.ToArray();
        _assetSelection.Retain(orderedFiles);
        _assetBounds.Clear();

        for (int fileIndex = 0; fileIndex < orderedFiles.Length; fileIndex++)
        {
            string file = orderedFiles[fileIndex];

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

            if (asset?.Type ==
                AssetType.Model3D)
            {
                DrawModelAssetEntry(
                    state,
                    log,
                    orderedFiles,
                    fileIndex,
                    file,
                    asset);

                continue;
            }

            string icon =
                GetAssetIcon(
                    asset?.Type);

            bool selected =
                _assetSelection.Contains(
                    file);

            bool clicked =
                ImGui.Selectable(
                    $"{icon} {Path.GetFileName(file)}##asset:{file}",
                    selected);

            if (clicked)
            {
                ImGuiIOPtr io =
                    ImGui.GetIO();

                _assetSelection.Click(
                    orderedFiles,
                    fileIndex,
                    io.KeyCtrl,
                    io.KeyShift);

                SyncPrimaryAssetSelection(
                    state);
            }

            _assetBounds.Add(
                new AssetSelectionBounds(
                    file,
                    ImGui.GetItemRectMin(),
                    ImGui.GetItemRectMax()));

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

        bool emptySpaceClicked =
            ImGui.IsWindowHovered() &&
            ImGui.IsMouseClicked(
                ImGuiMouseButton.Left) &&
            !ImGui.IsAnyItemHovered();

        if (emptySpaceClicked)
        {
            _assetMarquee =
                true;

            _assetMarqueeStart =
                ImGui.GetMousePos();

            _assetMarqueeEnd =
                _assetMarqueeStart;
        }

        if (_assetMarquee)
        {
            _assetMarqueeEnd =
                ImGui.GetMousePos();

            Vector2 minimum =
                Vector2.Min(
                    _assetMarqueeStart,
                    _assetMarqueeEnd);

            Vector2 maximum =
                Vector2.Max(
                    _assetMarqueeStart,
                    _assetMarqueeEnd);

            ImGui.GetWindowDrawList()
                .AddRectFilled(
                    minimum,
                    maximum,
                    ImGui.GetColorU32(
                        new Vector4(
                            .2f,
                            .55f,
                            1f,
                            .12f)));

            ImGui.GetWindowDrawList()
                .AddRect(
                    minimum,
                    maximum,
                    ImGui.GetColorU32(
                        new Vector4(
                            .3f,
                            .7f,
                            1f,
                            .9f)));

            if (!ImGui.IsMouseDown(
                    ImGuiMouseButton.Left))
            {
                _assetSelection.Marquee(
                    _assetBounds,
                    _assetMarqueeStart,
                    _assetMarqueeEnd,
                    ImGui.GetIO().KeyCtrl);

                SyncPrimaryAssetSelection(
                    state);

                _assetMarquee =
                    false;
            }
        }
    }

    private void DrawModelAssetEntry(
        EditorState state,
        EditorLog log,
        IReadOnlyList<string> orderedFiles,
        int fileIndex,
        string file,
        AssetRecord asset)
    {
        bool selected =
            _assetSelection.Contains(
                file) &&
            string.IsNullOrWhiteSpace(
                state.SelectedModelAnimationKey);

        ImGuiTreeNodeFlags flags =
            ImGuiTreeNodeFlags.OpenOnArrow |
            ImGuiTreeNodeFlags.SpanAvailWidth;

        if (selected)
        {
            flags |=
                ImGuiTreeNodeFlags.Selected;
        }

        bool open =
            ImGui.TreeNodeEx(
                $"[3D] {Path.GetFileName(file)}##model-asset:{file}",
                flags);

        if (ImGui.IsItemClicked(
                ImGuiMouseButton.Left))
        {
            ImGuiIOPtr io =
                ImGui.GetIO();

            _assetSelection.Click(
                orderedFiles,
                fileIndex,
                io.KeyCtrl,
                io.KeyShift);

            state.SelectedModelAnimationKey =
                null;

            SyncPrimaryAssetSelection(
                state);
        }

        _assetBounds.Add(
            new AssetSelectionBounds(
                file,
                ImGui.GetItemRectMin(),
                ImGui.GetItemRectMax()));

        if (ImGui.IsItemHovered() &&
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

        if (!open)
        {
            return;
        }

        try
        {
            ModelAsset model =
                _project.Assets.LoadModel(
                    new AssetReference(
                        asset.Guid,
                        asset.ProjectPath));

            if (model.Animations.Count ==
                0)
            {
                ImGui.TextDisabled(
                    "  [ANIM] No animation clips");
            }
            else
            {
                foreach (var animation
                         in model.Animations)
                {
                    bool clipSelected =
                        state.SelectedAssetId ==
                            asset.Guid &&
                        string.Equals(
                            state.SelectedModelAnimationKey,
                            animation.Key,
                            StringComparison.Ordinal);

                    string label =
                        $"[ANIM] {animation.Name}  {animation.Duration:0.00}s##animation:{asset.Guid}:{animation.Key}";

                    if (ImGui.Selectable(
                            label,
                            clipSelected))
                    {
                        _assetSelection.Clear();

                        state.SelectedAssetId =
                            asset.Guid;

                        state.SelectedAssetPath =
                            asset.ProjectPath;

                        state.SelectedModelAnimationKey =
                            animation.Key;

                        state.SelectedObject =
                            null;
                    }

                    DrawAnimationOpenActions(asset, animation, log);

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(
                            $"Animation Clip\n{animation.Name}\nDuration: {animation.Duration:0.000}s");
                    }
                }
            }
        }
        catch (Exception exception)
        {
            ImGui.TextColored(
                new Vector4(
                    1.0f,
                    0.35f,
                    0.35f,
                    1.0f),
                "  Animation discovery failed");

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(
                    exception.Message);
            }
        }

        ImGui.TreePop();
    }
    private void DrawAnimationOpenActions(
        AssetRecord asset,
        ImportedAnimation animation,
        EditorLog log)
    {
        if (ImGui.IsItemHovered() &&
            ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            if (_renderer != null && _renderer3D != null)
                _documentWindows.OpenAnimation(asset, animation, _project, log, _renderer, _renderer3D);
        }

        if (ImGui.BeginPopupContextItem(
                $"AnimationContext##{asset.Guid}:{animation.Key}"))
        {
            if (ImGui.MenuItem("Open Animation Timeline"))
                if (_renderer != null && _renderer3D != null)
                _documentWindows.OpenAnimation(asset, animation, _project, log, _renderer, _renderer3D);

            ImGui.EndPopup();
        }
    }
    private void SyncPrimaryAssetSelection(EditorState state)
    {
        state.SelectedModelAnimationKey =
            null;

        string? primary =
            _assetSelection.PrimaryPath;

        if (primary ==
            null)
        {
            state.SelectedAssetId =
                null;

            state.SelectedAssetPath =
                null;

            return;
        }

        string projectPath =
            Path.GetRelativePath(
                    _project.ProjectRoot,
                    primary)
                .Replace(
                    Path.DirectorySeparatorChar,
                    '/');

        _project.AssetDatabase.TryGetAsset(
            projectPath,
            out AssetRecord? asset);

        state.SelectedAssetId =
            asset?.Guid;

        state.SelectedAssetPath =
            projectPath;

                AssetDragDrop.RememberObjectSelection(
            state.SelectedObject);
state.SelectedObject =
            null;
    }
    private void OpenAsset(
        AssetRecord asset,
        EditorLog log)
    {
        if (asset.Type ==
            AssetType.AnimationProfile)
        {
            _documentWindows.OpenProfile(asset, _project, log);

            return;
        }

        if (asset.Type ==
            AssetType.EventModule)
        {
            _documentWindows.OpenEvent(asset, log);

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

            AssetType.AudioClip =>
                "[AUD]",

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

            AssetType.AnimationProfile =>
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

            if (ImGui.MenuItem(
                    "Animation Profile"))
            {
                _animationProfileName =
                    "New Animation Profile";

                _showCreateAnimationProfile =
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

        try
        {
            Vector3 worldPosition = selected.Transform.WorldPosition;
            Quaternion worldRotation = selected.Transform.WorldRotation;
            Vector3 worldScale = selected.Transform.WorldScale;
            Guid objectId = selected.Id;
            BlueprintPromotionService.Promote(_project, selected, path);
            state.SelectedObject = selected;
            state.SelectedAssetId = null;
            state.SelectedAssetPath = null;
            state.MarkDirty();
            RefreshAfterFileOperation();
            log.Info(
                $"Created Blueprint '{Path.GetFileName(path)}' and converted '{selected.Name}' to its first instance. " +
                $"Transform preserved: {selected.Id == objectId && selected.Transform.WorldPosition == worldPosition && selected.Transform.WorldRotation == worldRotation && selected.Transform.WorldScale == worldScale}.");
        }
        catch (Exception exception)
        {
            log.Error($"Could not create Blueprint from selection: {exception.Message}");
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
    // ANIMATION PROFILE CREATION
    // ========================================================

    private void DrawCreateAnimationProfileDialog(
        EditorLog log)
    {
        if (_showCreateAnimationProfile)
        {
            ImGui.OpenPopup(
                "Create Animation Profile");

            _showCreateAnimationProfile =
                false;
        }

        if (!ImGui.BeginPopupModal(
                "Create Animation Profile",
                ImGuiWindowFlags.AlwaysAutoResize))
        {
            return;
        }

        ImGui.TextWrapped(
            "Creates a reusable .byteanim asset. Assign or drag it into an Animation Controller after setup.");

        ImGui.InputText(
            "Name",
            ref _animationProfileName,
            128);

        bool valid =
            !string.IsNullOrWhiteSpace(
                _animationProfileName);

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
                CreateAnimationProfile(
                    log);

                ImGui.CloseCurrentPopup();
            }
            catch (Exception exception)
            {
                log.Error(
                    $"Could not create Animation Profile: {exception.Message}");
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

    private void CreateAnimationProfile(
        EditorLog log)
    {
        string directory =
            GetAssetCreationDirectory();

        string safeName =
            MakeSafeFileName(
                _animationProfileName);

        string path =
            GetUniqueAssetPath(
                directory,
                safeName,
                AnimationProfileSerializer.FileExtension);

        AnimationProfile profile =
            AnimationProfileSerializer.CreateDefault(
                safeName);

        AnimationProfileSerializer.Save(
            path,
            profile);

        SelectDirectory(
            directory);

        RefreshAfterFileOperation();

        string projectPath =
            ToProjectPath(
                path);

        if (_project.AssetDatabase.TryGetAsset(
                projectPath,
                out AssetRecord? asset) &&
            asset?.Type == AssetType.AnimationProfile)
        {
            _documentWindows.OpenProfile(asset, _project, log);
        }

        log.Info(
            $"Created Animation Profile '{Path.GetFileName(path)}'.");
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

        IEnumerable<string> targets;
        if (!isDirectory && _assetSelection.Count > 1 && _assetSelection.Contains(path))
            targets = _assetSelection.Paths;
        else
            targets = new[] { path };
        _deleteInteraction.Begin(targets, isDirectory);
    }

    private void BeginDeleteSelected()
    {
        if (_assetSelection.Count == 0) return;
        _deleteInteraction.Begin(_assetSelection.Paths, false);
    }

    private void DrawDeleteDialog(
        EditorState state,
        EditorLog log)
    {
        if (_deleteInteraction.ConsumeOpenRequest())
        {
            ImGui.OpenPopup("Delete Asset##AssetDeleteConfirmation");
        }

        if (!ImGui.BeginPopupModal("Delete Asset##AssetDeleteConfirmation", ImGuiWindowFlags.AlwaysAutoResize))
        {
            _deleteInteraction.RecoverWhenNotVisible();
            return;
        }
        _deleteInteraction.MarkVisible();

        IReadOnlyList<string> targets = _deleteInteraction.Targets;
        string? target = targets.Count == 1 ? targets[0] : null;
        AssetRecord[] affectedBlueprints = ResolveAssetsForDeletion(targets, _deleteInteraction.IsDirectory)
            .Where(asset => asset.Type == AssetType.Blueprint)
            .DistinctBy(asset => asset.Guid)
            .ToArray();

        // C9.5 UX: clear deleted Animation Profile references from live scene
        // components at delete time instead of leaving dead GUID/path values.
        AssetRecord[] affectedAnimationProfiles = ResolveAssetsForDeletion(targets, _deleteInteraction.IsDirectory)
            .Where(asset => asset.Type == AssetType.AnimationProfile)
            .DistinctBy(asset => asset.Guid)
            .ToArray();

        int animationProfileReferences = CountAnimationProfileReferences(
            state.EditorScene,
            affectedAnimationProfiles);

        int blueprintInstances = affectedBlueprints
            .Sum(asset => BlueprintPromotionService.CountInstances(
                state.EditorScene,
                new AssetReference(asset.Guid, asset.ProjectPath)));

        if (targets.Count > 1)
        {
            ImGui.TextWrapped($"Delete {targets.Count} selected assets?");
        }
        else if (target != null)
        {
            ImGui.TextWrapped(
                _deleteInteraction.IsDirectory
                    ? $"Delete folder '{Path.GetFileName(target)}' and everything inside it?"
                    : $"Delete asset '{Path.GetFileName(target)}'?");
        }

        if (blueprintInstances > 0 ||
            animationProfileReferences > 0)
        {
            ImGui.Separator();

            if (blueprintInstances > 0)
            {
                ImGui.TextWrapped(
                    $"{blueprintInstances} scene object(s) use the selected Blueprint asset(s).");
                ImGui.TextWrapped("Deleting will unpack those instances and keep their current objects and components.");
            }

            if (animationProfileReferences > 0)
            {
                ImGui.TextWrapped(
                    $"{animationProfileReferences} Animation Controller(s) use the selected Animation Profile asset(s).");
                ImGui.TextWrapped(
                    "Deleting will automatically clear those Animation Profile component references.");
            }
        }
        else
        {
            ImGui.TextDisabled("Known live references: none.");
        }

        ImGui.TextColored(
            new Vector4(
                1.0f,
                0.45f,
                0.30f,
                1.0f),
            "This cannot be undone.");

        bool canDelete =
            targets.Count > 0;

        ImGui.BeginDisabled(
            !canDelete);

        if (ImGui.Button(
                blueprintInstances > 0 ? "Delete and Unpack Instances" : "Delete",
                new Vector2(
                    210.0f,
                    0.0f)))
        {
            if (targets.Count > 0)
            {
                try
                {
                    foreach (AssetRecord blueprint in affectedBlueprints)
                    {
                        int unpacked = BlueprintPromotionService.UnpackInstances(
                            state.EditorScene,
                            new AssetReference(blueprint.Guid, blueprint.ProjectPath));
                        if (unpacked > 0) state.MarkDirty();
                        if (state.RuntimeScene != null)
                            BlueprintPromotionService.UnpackInstances(state.RuntimeScene,
                                new AssetReference(blueprint.Guid, blueprint.ProjectPath));
                    }
                    int clearedProfiles = ClearAnimationProfileReferences(
                        state.EditorScene,
                        affectedAnimationProfiles);

                    if (clearedProfiles > 0)
                    {
                        state.MarkDirty();
                    }

                    if (state.RuntimeScene != null)
                    {
                        ClearAnimationProfileReferences(
                            state.RuntimeScene,
                            affectedAnimationProfiles);
                    }

                    foreach (string item in targets.ToArray())
                    {
                        DeletePath(item, _deleteInteraction.IsDirectory);
                    }
                    _assetSelection.Clear();
                    state.SelectedAssetId = null;
                    state.SelectedAssetPath = null;
                    log.Info(targets.Count == 1
                        ? $"Deleted '{Path.GetFileName(targets[0])}'."
                        : $"Deleted {targets.Count} selected assets.");
                }
                catch (Exception exception)
                {
                    log.Error(
                        $"Could not delete: {exception.Message}");
                }
            }

            _deleteInteraction.Reset();

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
            _deleteInteraction.Reset();

            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private static int CountAnimationProfileReferences(
        ByteEngine.Core.Scene.Scene scene,
        IReadOnlyList<AssetRecord> profiles)
    {
        if (profiles.Count == 0)
        {
            return 0;
        }

        return scene.GameObjects
            .SelectMany(gameObject => gameObject.Components.OfType<AnimationController>())
            .Count(controller => AnimationProfileReferenceMatchesAny(
                controller.AnimationProfile,
                profiles));
    }

    private static int ClearAnimationProfileReferences(
        ByteEngine.Core.Scene.Scene scene,
        IReadOnlyList<AssetRecord> profiles)
    {
        if (profiles.Count == 0)
        {
            return 0;
        }

        int cleared = 0;

        foreach (AnimationController controller in scene.GameObjects
                     .SelectMany(gameObject => gameObject.Components.OfType<AnimationController>()))
        {
            if (!AnimationProfileReferenceMatchesAny(
                    controller.AnimationProfile,
                    profiles))
            {
                continue;
            }

            controller.AnimationProfile =
                AssetReference.Empty;

            controller.ApplyAnimationProfile();
            cleared++;
        }

        return cleared;
    }

    private static bool AnimationProfileReferenceMatchesAny(
        AssetReference? reference,
        IReadOnlyList<AssetRecord> profiles)
    {
        if (reference == null ||
            reference.IsEmpty)
        {
            return false;
        }

        foreach (AssetRecord profile in profiles)
        {
            if (reference.Guid != Guid.Empty &&
                reference.Guid == profile.Guid)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(reference.CachedProjectPath) &&
                string.Equals(
                    reference.CachedProjectPath,
                    profile.ProjectPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private AssetRecord? TryGetAsset(string fullPath)
    {
        string projectPath = Path.GetRelativePath(_project.ProjectRoot, fullPath)
            .Replace(Path.DirectorySeparatorChar, '/');
        return _project.AssetDatabase.TryGetAsset(projectPath, out AssetRecord? asset) ? asset : null;
    }

    private IEnumerable<AssetRecord> ResolveAssetsForDeletion(
        IReadOnlyList<string> targets,
        bool isDirectory)
    {
        foreach (string target in targets)
        {
            if (!isDirectory)
            {
                AssetRecord? asset = TryGetAsset(target);
                if (asset != null) yield return asset;
                continue;
            }

            string prefix = Path.GetRelativePath(_project.ProjectRoot, target)
                .Replace(Path.DirectorySeparatorChar, '/')
                .TrimEnd('/') + "/";
            foreach (AssetRecord asset in _project.AssetDatabase.Assets)
                if (asset.ProjectPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    yield return asset;
        }
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

    public void Dispose()
    {
        _project.AssetDatabase.DatabaseChanged -= OnAssetDatabaseChanged;

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
