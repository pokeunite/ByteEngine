using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Runtime;
using ByteEngine.Player;
using System.Runtime.InteropServices;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ByteEngine", "Games", "Logs");
        try { Directory.CreateDirectory(logDirectory); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            logDirectory = Path.Combine(Path.GetTempPath(), "ByteEngineGameLogs");
            Directory.CreateDirectory(logDirectory);
        }
        string log = Path.Combine(logDirectory, $"game-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.txt");
        CrashDebugLog.ConfigureStartupLog(log);
        CrashDebugLog.InstallGlobalHandlers();
        using var writer = new DiagnosticWriter();
        Console.SetOut(writer);
        Console.SetError(writer);
        bool smokeTest=args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase);
        bool validate = args.Contains("--validate", StringComparer.OrdinalIgnoreCase);
        try
        {
            if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
                throw new PlatformNotSupportedException("This game is built for Windows x64.");
            foreach (string library in new[] { "glfw3.dll", "assimp.dll", "openal32.dll" })
            {
                try
                {
                    nint handle = NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, library));
                    NativeLibrary.Free(handle);
                }
                catch (Exception error) when (error is DllNotFoundException or BadImageFormatException)
                {
                    throw new InvalidOperationException(
                        $"Native game dependency could not load: {library}. Keep the entire game folder together. " +
                        "Install or repair the Microsoft Visual C++ x64 Redistributable using the link supplied with the game, then retry.", error);
                }
            }
            if (validate) GamePackageExporter.ValidatePackage(AppContext.BaseDirectory);
            using var content = new GameContentSession(AppContext.BaseDirectory);
            using var project = new GameProjectRuntime(content.ProjectFile, CrashDebugLog.Write);
            using var game = new StandaloneGame(project,smokeTest,validate);
            game.Run();
            return 0;
        }
        catch (Exception error)
        {
            CrashDebugLog.WriteException("GAME FAILURE", error);
            if (!validate && !smokeTest)
                MessageBox.Show($"The game could not continue.\n\n{error.Message}\n\nDiagnostic log:\n{log}",
                    "Game Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
    private sealed class DiagnosticWriter : TextWriter
    {
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
        private readonly System.Text.StringBuilder _line = new();
        public override void WriteLine(string? value) => CrashDebugLog.Write(value ?? string.Empty);
        public override void Write(char value)
        {
            lock (_line)
            {
                if (value == '\n') { CrashDebugLog.Write(_line.ToString()); _line.Clear(); }
                else if (value != '\r') _line.Append(value);
            }
        }
    }
}
