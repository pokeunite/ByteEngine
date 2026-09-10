using ByteEngine.Editor;

internal static class Program
{
    [STAThread]
    private static void Main(
        string[] args)
    {
        string? projectFile =
            args.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(
                projectFile))
        {
            projectFile =
                EditorPreferences.ReadLastProject();
        }

        using var editor =
            new EditorApplication(
                projectFile
            );

        editor.Run();
    }
}
