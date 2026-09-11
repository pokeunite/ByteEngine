using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Serialization.SerializationModels;
using System.Text.Json.Nodes;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class AssetsPanel
{
    private readonly EditorProjectContext _project;
    private string? _currentDirectory;
    private readonly List<string> _directories = new();
    private readonly List<string> _files = new();
    private bool _listingDirty = true;
    private readonly Action<AssetRecord> _openAsset;
    private bool _showCreateBlueprint;
    private string _blueprintName = "New Blueprint";
    private int _blueprintType;

    public bool IsOpen { get; set; } = true;

    public AssetsPanel(EditorProjectContext project, Action<AssetRecord> openAsset)
    {
        _project = project;
        _openAsset = openAsset;
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
                AssetType.Model3D => "[3D]",
                AssetType.Blueprint => "[BP]",
                AssetType.EventModule => "[EVT]",
                AssetType.AnimationEvents => "[ANIM]",
                _ => "[FILE]"
            };
            bool selected = string.Equals(state.SelectedAssetPath, projectPath, StringComparison.OrdinalIgnoreCase);
            if (ImGui.Selectable($"{icon} {Path.GetFileName(file)}##{file}", selected))
            {
                state.SelectedAssetId = asset?.Guid;
                state.SelectedAssetPath = projectPath;
                state.SelectedObject = null;
                if (asset != null && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    _openAsset(asset);
            }

            if (asset != null &&
                asset.Type is AssetType.Texture2D or AssetType.Model3D or AssetType.Blueprint &&
                ImGui.BeginDragDropSource())
            {
                AssetDragDrop.Set(asset.Guid);
                ImGui.Text($"{asset.Type}: {Path.GetFileName(file)}");
                ImGui.EndDragDropSource();
            }
        }

        if (ImGui.BeginPopupContextWindow("Assets Context", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
        {
            if (ImGui.BeginMenu("Create"))
            {
                if (ImGui.MenuItem("Byte Blueprint")) _showCreateBlueprint = true;
                ImGui.EndMenu();
            }
            ImGui.EndPopup();
        }

        DrawCreateBlueprintDialog(log);

        ImGui.End();
    }

    private void DrawCreateBlueprintDialog(EditorLog log)
    {
        if (_showCreateBlueprint)
        {
            ImGui.OpenPopup("Create Byte Blueprint");
            _showCreateBlueprint = false;
        }
        if (!ImGui.BeginPopupModal("Create Byte Blueprint", ImGuiWindowFlags.AlwaysAutoResize)) return;

        ImGui.InputText("Name", ref _blueprintName, 128);
        ImGui.Combo("Type", ref _blueprintType, "Generic Object\0Character\0");
        bool valid = !string.IsNullOrWhiteSpace(_blueprintName);
        ImGui.BeginDisabled(!valid);
        if (ImGui.Button("Create", new System.Numerics.Vector2(100f, 0f)))
        {
            try
            {
                string directory = _currentDirectory ?? _project.ResolveProjectPath(_project.Project.AssetDirectory);
                string safeName = string.Concat(_blueprintName.Trim().Select(character =>
                    Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
                string path = Path.Combine(directory, safeName + ".byteblueprint");
                if (File.Exists(path)) throw new IOException("A Blueprint with that name already exists.");
                BlueprintType type = (BlueprintType)_blueprintType;
                var root = new GameObjectData { Id = Guid.NewGuid(), Name = safeName };
                if (type == BlueprintType.Character)
                {
                    root.Components.Add(new ComponentData { Type = "CharacterController3D" });
                    root.Components.Add(new ComponentData
                    {
                        Type = "CapsuleCollider3D",
                        Properties = new JsonObject { ["radius"] = .5f, ["height"] = 2f }
                    });
                    root.Components.Add(new ComponentData { Type = "AnimationController" });
                }
                var blueprint = new BlueprintDefinition
                {
                    Name = safeName,
                    Type = type,
                    Root = root,
                    Variables = type == BlueprintType.Character
                        ? new List<VariableData>
                        {
                            new() { Name = "Health", Value = ByteEngine.Core.Variables.VariableValue.FromNumber(100) },
                            new() { Name = "MoveSpeed", Value = ByteEngine.Core.Variables.VariableValue.FromNumber(6) },
                            new() { Name = "Team", Value = ByteEngine.Core.Variables.VariableValue.FromString("Player") }
                        }
                        : new List<VariableData>()
                };
                new BlueprintSerializer().Save(blueprint, path);
                _project.AssetDatabase.Scan();
                log.Info($"Created {type} Blueprint '{Path.GetFileName(path)}'.");
                ImGui.CloseCurrentPopup();
            }
            catch (Exception exception)
            {
                log.Error($"Could not create Blueprint: {exception.Message}");
            }
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new System.Numerics.Vector2(100f, 0f))) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
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
