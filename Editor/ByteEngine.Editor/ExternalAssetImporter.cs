using ByteEngine.Core.Assets;

namespace ByteEngine.Editor;

internal sealed class ExternalAssetImporter
{
    private readonly EditorProjectContext _project;
    private readonly EditorLog _log;

    public ExternalAssetImporter(EditorProjectContext project, EditorLog log)
    {
        _project = project;
        _log = log;
    }

    public IReadOnlyList<AssetRecord> Import(IEnumerable<string> paths)
    {
        string assetRoot = _project.ResolveProjectPath(_project.Project.AssetDirectory);
        Directory.CreateDirectory(assetRoot);
        var importedPaths = new List<string>();

        foreach (string path in paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    ImportFile(path, assetRoot, importedPaths);
                }
                else if (Directory.Exists(path))
                {
                    string folder = CreateUniqueDirectory(assetRoot, Path.GetFileName(Path.TrimEndingDirectorySeparator(path)));
                    foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                        string relative = Path.GetRelativePath(path, file);
                        string destination = Path.Combine(folder, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                        File.Copy(file, destination, false);
                        importedPaths.Add(destination);
                    }
                }
                else
                {
                    _log.Warning($"Dropped path no longer exists: {path}");
                }
            }
            catch (Exception exception)
            {
                _log.Error($"Could not import '{path}': {exception.Message}");
            }
        }

        _project.AssetDatabase.Scan();
        var records = new List<AssetRecord>();
        foreach (string importedPath in importedPaths)
        {
            string projectPath = Path.GetRelativePath(_project.ProjectRoot, importedPath).Replace('\\', '/');
            if (_project.AssetDatabase.TryGetAsset(projectPath, out AssetRecord? asset) && asset != null)
            {
                records.Add(asset);
                string note = asset.Type == AssetType.Model3D
                    ? " Model rendering/import settings will arrive with the model pipeline."
                    : string.Empty;
                _log.Info($"Imported asset '{asset.ProjectPath}'.{note}");
            }
        }

        return records;
    }

    private static void ImportFile(string source, string assetRoot, List<string> importedPaths)
    {
        if (source.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) return;
        string fullSource = Path.GetFullPath(source);
        string rootPrefix = Path.GetFullPath(assetRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (fullSource.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            importedPaths.Add(fullSource);
            return;
        }

        string destination = CreateUniqueFilePath(assetRoot, Path.GetFileName(source));
        File.Copy(fullSource, destination, false);
        importedPaths.Add(destination);
    }

    private static string CreateUniqueFilePath(string directory, string fileName)
    {
        string candidate = Path.Combine(directory, fileName);
        if (!File.Exists(candidate)) return candidate;
        string stem = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        int suffix = 2;
        do candidate = Path.Combine(directory, $"{stem} {suffix++}{extension}"); while (File.Exists(candidate));
        return candidate;
    }

    private static string CreateUniqueDirectory(string parent, string name)
    {
        string candidate = Path.Combine(parent, name);
        if (!Directory.Exists(candidate)) { Directory.CreateDirectory(candidate); return candidate; }
        int suffix = 2;
        do candidate = Path.Combine(parent, $"{name} {suffix++}"); while (Directory.Exists(candidate));
        Directory.CreateDirectory(candidate);
        return candidate;
    }
}
