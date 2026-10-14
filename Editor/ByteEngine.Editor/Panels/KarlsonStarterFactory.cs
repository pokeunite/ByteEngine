using ByteEngine.Core;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Serialization;

namespace ByteEngine.Editor.Panels;

internal static class KarlsonStarterFactory
{
    public static string Install(string projectDirectory, string projectName, string? packageFile = null)
    {
        if (string.IsNullOrWhiteSpace(projectName) || projectName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            projectName is "." or "..") throw new ArgumentException("Enter a valid project name.", nameof(projectName));
        packageFile ??= Path.Combine(AppContext.BaseDirectory, "Resources", "Starters", "KarlsonDemo.bytepak");
        if (!File.Exists(packageFile)) throw new FileNotFoundException("Karlson Demo template is missing. Refresh the engine distribution.", packageFile);
        if (Directory.Exists(projectDirectory) && Directory.EnumerateFileSystemEntries(projectDirectory).Any())
            throw new IOException("Choose an empty folder for the Karlson Demo template.");
        Directory.CreateDirectory(projectDirectory);
        ByteAssetPackage.Extract(packageFile, projectDirectory);
        string sourceProject = Path.Combine(projectDirectory, "Game.byteproject");
        var serializer = new ProjectSerializer();
        var project = serializer.Load(sourceProject);
        project.Name = projectName;
        project.ProjectId = Guid.NewGuid();
        project.RuntimeSaveId = null;
        project.EngineVersion = ByteEngineInfo.Version;
        string projectFile = Path.Combine(projectDirectory, projectName + ".byteproject");
        serializer.Save(project, projectFile);
        if (!string.Equals(sourceProject, projectFile, StringComparison.OrdinalIgnoreCase)) File.Delete(sourceProject);
        return projectFile;
    }
}
