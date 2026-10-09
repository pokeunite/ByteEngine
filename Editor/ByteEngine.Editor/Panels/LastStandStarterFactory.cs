using ByteEngine.Core;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Serialization;

namespace ByteEngine.Editor.Panels;

internal static class LastStandStarterFactory
{
    public static string Install(string projectDirectory, string projectName, string? packageFile = null)
    {
        packageFile ??= Path.Combine(AppContext.BaseDirectory, "Resources", "Starters", "LastStand.bytepak");
        if (!File.Exists(packageFile)) throw new FileNotFoundException("Last Stand starter package is missing. Refresh the engine distribution.", packageFile);
        if (Directory.Exists(projectDirectory) && Directory.EnumerateFileSystemEntries(projectDirectory).Any())
            throw new IOException("Choose an empty folder for the Last Stand starter.");
        Directory.CreateDirectory(projectDirectory);
        ByteAssetPackage.Extract(packageFile, projectDirectory);
        string sourceProject = Path.Combine(projectDirectory, "Game.byteproject");
        var serializer = new ProjectSerializer();
        var project = serializer.Load(sourceProject);
        project.Name = projectName;
        project.ProjectId = Guid.NewGuid();
        project.EngineVersion = ByteEngineInfo.Version;
        BundledCharacterInstaller.Install(projectDirectory, project.AssetDirectory);
        string projectFile = Path.Combine(projectDirectory, projectName + ".byteproject");
        serializer.Save(project, projectFile);
        if (!string.Equals(sourceProject, projectFile, StringComparison.OrdinalIgnoreCase)) File.Delete(sourceProject);
        return projectFile;
    }
}
