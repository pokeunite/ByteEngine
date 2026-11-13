using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Authoring;
using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Mcp.Authoring;

public sealed partial class AuthoringSession
{
    private static readonly (string Id, string Name, ProjectTemplate Template, bool CoreSupported, string Note)[] Templates =
    [
        ("clean", "Clean Project", ProjectTemplate.Clean, true, "Empty scene with a project skeleton."),
        ("starter3d", "3D Starter", ProjectTemplate.Starter3D, true, "Camera, ground, cube and directional light."),
        ("interactive_sand", "Interactive Sand", ProjectTemplate.InteractiveSand, true, "Sand lab scene with inspection camera and HUD text."),
        ("bytearena", "ByteArena", ProjectTemplate.ByteArena, false, "Editor-only: also generates player/enemy Blueprints and combat Event Modules."),
        ("last_stand", "Last Stand", ProjectTemplate.LastStand, false, "Editor-only starter package with third-party assets."),
        ("karlson_demo", "Karlson Demo", ProjectTemplate.KarlsonDemo, false, "Editor-only starter package with third-party assets.")
    ];

    private static readonly string[] BundledFontFiles =
        ["TypeLightSans.ttf", "OSerif-Regular.ttf", "PublicPixel.ttf", "CC0-LICENSE.txt"];

    private static readonly string[] BundledCharacterFiles =
    [
        "Mannequin.glb", "Mannequin-FPS-Arms.glb", "Mannequin-Advanced-FPS.glb",
        "README.txt", "ANIMATION-CREDITS.txt", "LICENSE-CC0-1.0.txt", "animations.json"
    ];

    private static bool TryParseTemplate(string value, out (string Id, string Name, ProjectTemplate Template, bool CoreSupported, string Note) template)
    {
        string normalized = value.Trim().Replace("-", "").Replace("_", "").Replace(" ", "");
        template = Templates.FirstOrDefault(item =>
            string.Equals(item.Id.Replace("_", ""), normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Name.Replace(" ", ""), normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Template.ToString(), normalized, StringComparison.OrdinalIgnoreCase));
        return template.Id != null;
    }

    private object TemplatesTool() => new
    {
        ok = true,
        items = Templates.Select(item => new
        {
            id = item.Id,
            name = item.Name,
            available = item.CoreSupported,
            editor_required = !item.CoreSupported,
            note = item.Note
        }).ToArray()
    };

    private object CreateProjectTool(JsonElement? args)
    {
        string templateName = Request.String(args, "template") ?? "starter3d";
        if (!TryParseTemplate(templateName, out var template))
            throw new McpFault("INVALID_REQUEST", "Unknown template. Use be_project(op:\"templates\") for ids.");
        if (!template.CoreSupported)
            throw new McpFault("EDITOR_REQUIRED", $"{template.Name} needs editor-only asset generation. Create it once in the Project Browser, or use starter3d.");

        string requested = Request.Required(args, "path");
        if (!Path.IsPathFullyQualified(requested))
            throw new McpFault("INVALID_REQUEST", "Use an absolute folder or .byteproject path.");

        string projectFile;
        string root;
        string name;
        if (requested.EndsWith(".byteproject", StringComparison.OrdinalIgnoreCase))
        {
            projectFile = Path.GetFullPath(requested);
            root = Path.GetDirectoryName(projectFile)!;
            name = Path.GetFileNameWithoutExtension(projectFile);
        }
        else
        {
            string parent = Path.GetFullPath(requested);
            name = Request.String(args, "name") ?? new DirectoryInfo(parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).Name;
            root = string.Equals(Path.GetFileName(parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), name, StringComparison.OrdinalIgnoreCase)
                ? parent : Path.Combine(parent, name);
            projectFile = Path.Combine(root, name + ".byteproject");
        }

        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            throw new McpFault("ALREADY_EXISTS", $"The project folder is not empty: {root}");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        Directory.CreateDirectory(Path.Combine(root, "Scenes"));
        Directory.CreateDirectory(Path.Combine(root, "Scripts"));

        ProjectData project = new()
        {
            Name = name,
            ProjectId = Guid.NewGuid(),
            EngineVersion = ByteEngineInfo.Version,
            StartupScene = "Scenes/Main.bytescene",
            AssetDirectory = "Assets",
            SceneDirectory = "Scenes"
        };
        _projects.Save(project, projectFile);
        Attach(project, projectFile);

        int fonts = Request.Bool(args, "install_fonts", true)
            ? InstallBundledFiles("Fonts", "Fonts/ByteEngine", BundledFontFiles)
            : 0;
        int characters = Request.Bool(args, "install_characters", true)
            ? InstallBundledFiles("Characters/ByteEngine", "Characters/ByteEngine", BundledCharacterFiles)
            : 0;
        Database.Scan();

        string sceneFull = Inside(project.StartupScene, ".bytescene");
        var scene = ProjectTemplateFactory.Create(template.Template);
        Scenes.Save(scene, sceneFull);
        Database.Scan();
        _projects.Save(project, projectFile);
        MarkChanged(projectFile);
        MarkChanged(sceneFull);

        return new
        {
            ok = true,
            name = project.Name,
            path = _projectFile,
            root = _root,
            template = template.Id,
            startup_scene = project.StartupScene,
            rev = Rev(projectFile),
            scene_rev = Rev(sceneFull),
            objects = scene.GameObjectCount,
            fonts,
            characters
        };
    }

    private int InstallBundledFiles(string sourceFolder, string destinationFolder, string[] files)
    {
        string? repository = FindRepositoryRoot();
        string[] candidates = repository == null
            ? [Path.Combine(AppContext.BaseDirectory, "Resources", sourceFolder)]
            :
            [
                Path.Combine(repository, "Editor", "ByteEngine.Editor", "Resources", sourceFolder),
                Path.Combine(repository, "Dist", "ByteEngine", "Resources", sourceFolder),
                Path.Combine(AppContext.BaseDirectory, "Resources", sourceFolder)
            ];
        string? source = candidates.FirstOrDefault(Directory.Exists);
        if (source == null) return 0;
        string destination = Path.Combine(Root, Project.AssetDirectory, destinationFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(destination);
        int added = 0;
        foreach (string file in files)
        {
            string input = Path.Combine(source, file);
            if (!File.Exists(input)) continue;
            string target = Path.Combine(destination, file);
            if (File.Exists(target)) continue;
            File.Copy(input, target, overwrite: false);
            added++;
        }
        return added;
    }
}
