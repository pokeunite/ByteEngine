using System.Runtime.InteropServices;
using System.Text;

namespace ByteEngine.Core.Diagnostics;

/// <summary>
/// Persistent crash/breadcrumb logger. Each entry is appended synchronously so
/// useful diagnostics survive even if the process terminates abruptly.
/// </summary>
public static class CrashDebugLog
{
    private static readonly object Sync = new();
    private static bool _handlersInstalled;
    private static string? _logPath;

    public static string? LogPath
    {
        get
        {
            lock (Sync) return _logPath;
        }
    }

    public static void InstallGlobalHandlers()
    {
        lock (Sync)
        {
            if (_handlersInstalled) return;
            _handlersInstalled = true;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            try
            {
                if (eventArgs.ExceptionObject is Exception exception)
                {
                    WriteException(
                        eventArgs.IsTerminating
                            ? "UNHANDLED TERMINATING EXCEPTION"
                            : "UNHANDLED EXCEPTION",
                        exception);
                }
                else
                {
                    Write($"UNHANDLED NON-EXCEPTION OBJECT: {eventArgs.ExceptionObject}");
                }
            }
            catch
            {
            }
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            try
            {
                WriteException("UNOBSERVED TASK EXCEPTION", eventArgs.Exception);
            }
            catch
            {
            }
        };
    }

    public static void ConfigureProjectRoot(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot)) return;

        try
        {
            string logsDirectory =
                Path.Combine(Path.GetFullPath(projectRoot), "Logs");

            Directory.CreateDirectory(logsDirectory);

            string fileName =
                $"crash-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.txt";

            string path =
                Path.Combine(logsDirectory, fileName);

            lock (Sync)
            {
                _logPath = path;
            }

            WriteHeader();
            Write($"Crash log configured for project root: {projectRoot}");
        }
        catch
        {
        }
    }

    public static void Write(string message)
    {
        try
        {
            string? path;

            lock (Sync)
            {
                path = _logPath;
            }

            if (string.IsNullOrWhiteSpace(path)) return;

            string line =
                $"[{DateTime.Now:O}] [T{Environment.CurrentManagedThreadId}] {message}{Environment.NewLine}";

            lock (Sync)
            {
                File.AppendAllText(path, line, Encoding.UTF8);
            }
        }
        catch
        {
        }
    }

    public static void WriteException(string context, Exception exception)
    {
        try
        {
            Write($"{context}{Environment.NewLine}{exception}");
        }
        catch
        {
        }
    }

    private static void WriteHeader()
    {
        try
        {
            string? path;

            lock (Sync)
            {
                path = _logPath;
            }

            if (string.IsNullOrWhiteSpace(path)) return;

            var builder = new StringBuilder();

            builder.AppendLine("BYTEENGINE CRASH DEBUG LOG");
            builder.AppendLine($"Started: {DateTime.Now:O}");
            builder.AppendLine($"Process ID: {Environment.ProcessId}");
            builder.AppendLine($"64-bit Process: {Environment.Is64BitProcess}");
            builder.AppendLine($"OS: {RuntimeInformation.OSDescription}");
            builder.AppendLine($"Framework: {RuntimeInformation.FrameworkDescription}");
            builder.AppendLine($"Architecture: {RuntimeInformation.ProcessArchitecture}");
            builder.AppendLine($"Command Line: {Environment.CommandLine}");
            builder.AppendLine(new string('=', 80));

            lock (Sync)
            {
                File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
        }
    }
}
