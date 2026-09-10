using ByteEngine.Core.Assets;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class AssetsPanel
{
    private readonly EditorProjectContext _project;
    private string? _currentDirectory;
    private readonly List<string> _directories = new();
    private readonly List<string> _files = new();
    private bool _listingDirty = true;

    public bool IsOpen { get; set; } = true;

    public AssetsPanel(EditorProjectContext project)
    {
        _project = project;
        _project.AssetDatabase.DatabaseChanged += () => _listingDirty = true;
    }

    public void Refresh(EditorLog log)
    {
        _project.AssetDatabase.Scan();
        log.Info($"Asset database contains {_project.AssetDatabase.Assets.Count} asset(s).");
    }

    public void Draw(EditorState state, EditorLog log)
    {
        bool isOpen = IsOpen;
        ImGui.Begin("Assets", ref isOpen);
        IsOpen = isOpen;

        if (ImGui.SmallButton("Back") && _currentDirectory != null)
        {
            string? parent = Path.GetDirectoryName(_currentDirectory);
            _currentDirectory = IsContentRoot(_currentDirectory) ? null : parent;
            _listingDirty = true;
        }
        ImGui.SameLine();
        ImGui.TextDisabled(Breadcrumb());
        ImGui.SameLine();
        if (ImGui.SmallButton("Refresh")) Refresh(log);
        ImGui.Separator();

        if (_currentDirectory == null)
        {
            DrawRootFolder(_project.Project.AssetDirectory);
            DrawRootFolder(_project.Project.SceneDirectory);
            ImGui.End();
            return;
        }

        if (!Directory.Exists(_currentDirectory))
        {
            _currentDirectory = null;
            ImGui.TextDisabled("Folder no longer exists.");
            ImGui.End();
            return;
        }

        UpdateListing();

        foreach (string directory in _directories)
        {
            if (ImGui.Selectable($"[DIR] {Path.GetFileName(directory)}##{directory}") && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                _currentDirectory = directory;
                _listingDirty = true;
            }
        }

        foreach (string file in _files)
        {
            string projectPath = Path.GetRelativePath(_project.ProjectRoot, file).Replace('\\', '/');
            _project.AssetDatabase.TryGetAsset(projectPath, out AssetRecord? asset);
            string icon = asset?.Type switch
            {
                AssetType.Texture2D => "[IMG]",
                AssetType.Scene => "[SCN]",
                _ => "[FILE]"
            };
            bool selected = string.Equals(state.SelectedAssetPath, projectPath, StringComparison.OrdinalIgnoreCase);
            if (ImGui.Selectable($"{icon} {Path.GetFileName(file)}##{file}", selected))
            {
                state.SelectedAssetId = asset?.Guid;
                state.SelectedAssetPath = projectPath;
                state.SelectedObject = null;
            }

            if (asset?.Type == AssetType.Texture2D && ImGui.BeginDragDropSource())
            {
                AssetDragDrop.Set(asset.Guid);
                ImGui.Text($"Texture: {Path.GetFileName(file)}");
                ImGui.EndDragDropSource();
            }
        }

        ImGui.End();
    }

    private void DrawRootFolder(string relativePath)
    {
        string fullPath = _project.ResolveProjectPath(relativePath);
        if (ImGui.Selectable($"[DIR] {Path.GetFileName(fullPath)}##{fullPath}") && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            _currentDirectory = fullPath;
            _listingDirty = true;
        }
    }

    private void UpdateListing()
    {
        if (!_listingDirty || _currentDirectory == null) return;
        _directories.Clear();
        _files.Clear();
        _directories.AddRange(Directory.EnumerateDirectories(_currentDirectory).OrderBy(Path.GetFileName));
        _files.AddRange(Directory.EnumerateFiles(_currentDirectory)
            .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName));
        _listingDirty = false;
    }

    private string Breadcrumb() => _currentDirectory == null
        ? _project.Project.Name
        : $"{_project.Project.Name} / {Path.GetRelativePath(_project.ProjectRoot, _currentDirectory).Replace('\\', '/')}";

    private bool IsContentRoot(string directory) =>
        string.Equals(directory, _project.ResolveProjectPath(_project.Project.AssetDirectory), StringComparison.OrdinalIgnoreCase) ||
        string.Equals(directory, _project.ResolveProjectPath(_project.Project.SceneDirectory), StringComparison.OrdinalIgnoreCase);
}
