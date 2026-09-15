using ByteEngine.Core.Diagnostics;
using ByteEngine.Editor;

internal static class Program
{
    [STAThread]
    private static void Main(
        string[] args)
    {
        CrashDebugLog.InstallGlobalHandlers();

        try
        {
            string? projectFile =
                args.FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(projectFile))
            {
                string fullProjectPath =
                    Path.GetFullPath(projectFile);

                string? projectRoot =
                    Path.GetDirectoryName(fullProjectPath);

                if (!string.IsNullOrWhiteSpace(projectRoot))
                {
                    CrashDebugLog.ConfigureProjectRoot(projectRoot);
                }
            }

            CrashDebugLog.Write(
                "Program.Main: creating EditorApplication.");

            using var editor =
                new EditorApplication(projectFile);

            CrashDebugLog.Write(
                "Program.Main: entering editor.Run().");

            editor.Run();

            CrashDebugLog.Write(
                "Program.Main: editor.Run() returned normally.");
        }
        catch (Exception exception)
        {
            CrashDebugLog.WriteException(
                "TOP-LEVEL EDITOR EXCEPTION",
                exception);

            throw;
        }
    }
}
