using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Runtime;
using ByteEngine.Player;
using System.Runtime.InteropServices;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        /*
         * Packaging identity probe.
         *
         * Tools/PublishPlayer.ps1 and Rebuild-ByteEngine-And-Refresh-Dist.ps1
         * launch the published player with --write-runtime-identity <path> to
         * prove the bundled and loose builds ship the same ByteEngine.Core.
         * GamePackageExporter validates that same MVID before running an
         * exported game, so the probe must answer before any window, project
         * or native dependency is required.
         */
        int identityIndex = Array.FindIndex(
            args,
            argument => string.Equals(argument, "--write-runtime-identity", StringComparison.OrdinalIgnoreCase));
        if (identityIndex >= 0 && identityIndex + 1 < args.Length)
        {
            string identityPath = Path.GetFullPath(args[identityIndex + 1].Trim('"'));
            string? identityDirectory = Path.GetDirectoryName(identityPath);
            if (!string.IsNullOrWhiteSpace(identityDirectory))
            {
                Directory.CreateDirectory(identityDirectory);
            }
            File.WriteAllText(
                identityPath,
                typeof(ByteEngine.Core.ByteEngineApplication).Assembly.ManifestModule.ModuleVersionId.ToString());
            return 0;
        }

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
        ByteEngine.Core.VisualLogic.VisualLogicDebugTrace.Enabled=args.Contains("--trace-events",StringComparer.OrdinalIgnoreCase);
        bool smokeTest=args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase);
        bool validate = args.Contains("--validate", StringComparer.OrdinalIgnoreCase);
        using var splash = smokeTest || validate ? null : new StartupSplash();
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
            string gameDirectory=AppContext.BaseDirectory;
            if(string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(gameDirectory)),"Runtime",StringComparison.OrdinalIgnoreCase))
                gameDirectory=Directory.GetParent(Path.TrimEndingDirectorySeparator(gameDirectory))!.FullName;
            if (validate) GamePackageExporter.ValidatePackage(gameDirectory);
            using var content = new GameContentSession(gameDirectory);
            using var project = new GameProjectRuntime(content.ProjectFile, CrashDebugLog.Write);
            using var game = new StandaloneGame(project,smokeTest,validate, () => splash?.Dispose());
            game.Run();
            return 0;
        }
        catch (Exception error)
        {
            CrashDebugLog.WriteException("GAME FAILURE", error);
            splash?.Dispose();
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

