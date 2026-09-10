namespace ByteEngine.Editor;

internal static class EditorPreferences
{
    private static readonly string DirectoryPath =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            ),
            "ByteEngine",
            "Editor"
        );

    public static string LayoutPath =>
        Path.Combine(
            DirectoryPath,
            "layout.ini"
        );

    private static string LastProjectPath =>
        Path.Combine(
            DirectoryPath,
            "last-project.txt"
        );

    public static string? ReadLastProject()
    {
        if (!File.Exists(
                LastProjectPath))
        {
            return null;
        }

        string path =
            File.ReadAllText(
                LastProjectPath
            )
            .Trim();

        return File.Exists(path)
            ? path
            : null;
    }

    public static void SaveLastProject(
        string projectFilePath)
    {
        Directory.CreateDirectory(
            DirectoryPath
        );

        File.WriteAllText(
            LastProjectPath,
            Path.GetFullPath(
                projectFilePath
            )
        );
    }
}
