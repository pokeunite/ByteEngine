using ByteEngine.Editor;

internal static class Program
{
    [STAThread]
    private static void Main(
        string[] args)
    {
        string? projectFile =
            args.FirstOrDefault();

        using var editor =
            new EditorApplication(
                projectFile
            );

        editor.Run();
    }
}
