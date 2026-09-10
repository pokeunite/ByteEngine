using System.Numerics;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal enum ProjectTemplate
{
    Clean,
    Starter3D
}

internal sealed record NewProjectRequest(string Name, string ParentDirectory, ProjectTemplate Template);

internal sealed class ProjectBrowserPanel
{
    private string _projectName = "MyGame";
    private string _parentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private ProjectTemplate _template = ProjectTemplate.Starter3D;
    private bool _openCreatePopup;

    public void OpenCreateDialog()
    {
        _openCreatePopup = true;
    }

    public void DrawLanding(Action<NewProjectRequest> createProject, Action openProject, string? recentProject)
    {
        ImGuiViewportPtr viewport = ImGui.GetMainViewport();
        Vector2 size = new(760f, 470f);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        ImGui.SetNextWindowPos(viewport.WorkPos + (viewport.WorkSize - size) * .5f, ImGuiCond.Always);
        ImGui.Begin("ByteEngine Project Browser",
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDocking);

        ImGui.SetWindowFontScale(1.45f);
        ImGui.Text("ByteEngine");
        ImGui.SetWindowFontScale(1f);
        ImGui.TextDisabled("Create a project or open an existing .byteproject file.");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("New Project", new Vector2(220f, 58f))) OpenCreateDialog();
        ImGui.SameLine();
        if (ImGui.Button("Open Project", new Vector2(220f, 58f))) openProject();

        ImGui.Spacing();
        ImGui.SeparatorText("Recent Project");
        if (!string.IsNullOrWhiteSpace(recentProject) && File.Exists(recentProject))
        {
            ImGui.Text(Path.GetFileNameWithoutExtension(recentProject));
            ImGui.TextDisabled(recentProject);
            ImGui.TextDisabled("Use Open Project to browse to this or another project.");
        }
        else
        {
            ImGui.TextDisabled("No recent project. ByteEngine will not open one automatically.");
        }

        ImGui.End();
        DrawCreateDialog(createProject);
    }

    public void DrawCreateDialog(Action<NewProjectRequest> createProject)
    {
        if (_openCreatePopup)
        {
            ImGui.OpenPopup("Create New ByteEngine Project");
            _openCreatePopup = false;
        }

        ImGui.SetNextWindowSize(new Vector2(620f, 430f), ImGuiCond.Appearing);
        if (!ImGui.BeginPopupModal("Create New ByteEngine Project", ImGuiWindowFlags.NoResize)) return;

        ImGui.Text("Project name");
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputText("##ProjectName", ref _projectName, 96);

        ImGui.Spacing();
        ImGui.Text("Location");
        ImGui.SetNextItemWidth(-92f);
        ImGui.InputText("##ProjectDirectory", ref _parentDirectory, 1024);
        ImGui.SameLine();
        if (ImGui.Button("Browse...", new Vector2(82f, 0f)))
        {
            string? selected = EditorDialogs.ChooseProjectDirectory(_parentDirectory);
            if (selected != null) _parentDirectory = selected;
        }

        string safeName = _projectName.Trim();
        string preview = string.IsNullOrWhiteSpace(safeName) || string.IsNullOrWhiteSpace(_parentDirectory)
            ? "Select a name and location"
            : Path.Combine(_parentDirectory, safeName);
        ImGui.TextDisabled($"Project folder: {preview}");

        ImGui.Spacing();
        ImGui.SeparatorText("Template");
        int template = (int)_template;
        if (ImGui.RadioButton("Clean Project", ref template, (int)ProjectTemplate.Clean))
            _template = ProjectTemplate.Clean;
        ImGui.TextDisabled("    Empty Main scene. Start from a blank project.");
        if (ImGui.RadioButton("3D Starter", ref template, (int)ProjectTemplate.Starter3D))
            _template = ProjectTemplate.Starter3D;
        ImGui.TextDisabled("    Camera, lit cube, ground plane, collider and directional light.");

        bool valid = IsValidName(safeName) && Directory.Exists(_parentDirectory);
        if (!valid)
        {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(1f, .55f, .3f, 1f),
                Directory.Exists(_parentDirectory) ? "Enter a valid project name." : "Select an existing parent directory.");
        }

        ImGui.SetCursorPosY(ImGui.GetWindowHeight() - 48f);
        ImGui.BeginDisabled(!valid);
        if (ImGui.Button("Create Project", new Vector2(155f, 32f)))
        {
            createProject(new NewProjectRequest(safeName, Path.GetFullPath(_parentDirectory), _template));
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(100f, 32f))) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }

    private static bool IsValidName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && name is not "." and not "..";
}
