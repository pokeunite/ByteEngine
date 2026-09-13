using System.Text.Json;

using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Core.Serialization;

public sealed class ProjectSerializer
{
    public ProjectData Load(
        string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"ByteEngine project file was not found: {filePath}",
                filePath
            );
        }

        try
        {
            string json =
                File.ReadAllText(
                    filePath
                );

            ProjectData project =
                JsonSerializer.Deserialize<ProjectData>(
                    json,
                    JsonSerialization.Options
                ) ??
                throw new InvalidDataException(
                    "The project file did not contain project data."
                );

            Validate(project);

            return project;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Invalid ByteEngine project JSON in '{filePath}': {exception.Message}",
                exception
            );
        }
    }

    public void Save(
        ProjectData project,
        string filePath)
    {
        ArgumentNullException.ThrowIfNull(
            project
        );

        Validate(project);

        JsonSerialization.WriteAtomic(
            filePath,
            project
        );
    }

    private static void Validate(
        ProjectData project)
    {
        project.Classification ??= ByteEngine.Core.Classification.ClassificationSettings.CreateDefault();
        project.Classification.EnsureValid();
        if (string.IsNullOrWhiteSpace(
                project.Name))
        {
            throw new InvalidDataException(
                "Project name cannot be empty."
            );
        }

        if (project.ProjectId ==
            Guid.Empty)
        {
            throw new InvalidDataException(
                "Project ID cannot be empty."
            );
        }

        ValidateRelativePath(
            project.StartupScene,
            "startup scene"
        );

        ValidateRelativePath(
            project.AssetDirectory,
            "asset directory"
        );

        ValidateRelativePath(
            project.SceneDirectory,
            "scene directory"
        );

        if (project.Window.Width <= 0 ||
            project.Window.Height <= 0)
        {
            throw new InvalidDataException(
                "Project window dimensions must be positive."
            );
        }
    }

    private static void ValidateRelativePath(
        string path,
        string label)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            Path.IsPathRooted(path) ||
            path.Split(
                    '/',
                    '\\'
                )
                .Contains(".."))
        {
            throw new InvalidDataException(
                $"The project {label} must be a safe project-relative path."
            );
        }
    }
}
