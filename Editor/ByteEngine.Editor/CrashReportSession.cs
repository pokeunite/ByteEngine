using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ByteEngine.Core.Diagnostics;

namespace ByteEngine.Editor;

/// <summary>Local-only reports; a locked marker distinguishes running from interrupted sessions.</summary>
internal sealed class CrashReportSession : IDisposable
{
    private readonly string _log;
    private readonly string _marker;
    private readonly FileStream _lease;
    private static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ByteEngine", "Logs");

    private CrashReportSession(string log, string marker, FileStream lease)
    {
        _log = log;
        _marker = marker;
        _lease = lease;
    }

    public static CrashReportSession? Start()
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            foreach (string marker in Directory.EnumerateFiles(LogDirectory, "*.pending"))
            {
                try
                {
                    // Other live processes hold this file exclusively.
                    using (new FileStream(marker, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                    string previousLog = marker[..^".pending".Length];
                    Offer(previousLog, "The previous ByteEngine session ended unexpectedly. This can also happen after a forced shutdown.");
                    File.Delete(marker);
                }
                catch (IOException) { }
            }

            string log = Path.Combine(LogDirectory,
                $"startup-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.txt");
            string pending = log + ".pending";
            var lease = new FileStream(pending, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            try
            {
                CrashDebugLog.ConfigureStartupLog(log);
                CrashDebugLog.Write($"Engine version: {ByteEngine.Core.ByteEngineInfo.Version}; OS architecture: {RuntimeInformation.OSArchitecture}; Logical CPUs: {Environment.ProcessorCount}");
                return new CrashReportSession(log, pending, lease);
            }
            catch { lease.Dispose(); throw; }
        }
        catch (Exception error)
        {
            MessageBox.Show($"Startup diagnostics could not be initialized: {error.Message}\nThe editor will still attempt to start.",
                "ByteEngine Diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }
    }

    public void Complete()
    {
        _lease.Dispose();
        try { File.Delete(_marker); } catch (IOException) { }
    }

    public void ReportCrash()
    {
        if (Offer(_log, "ByteEngine encountered an error. You can save a diagnostic ZIP and upload it to Discord."))
            Complete();
    }

    private static bool Offer(string log, string message)
    {
        if (!File.Exists(log)) return false;
        DialogResult choice = MessageBox.Show(message +
            "\n\nSave Diagnostic Report.?\nNo files are uploaded automatically. Reports contain diagnostic text only; review it before sharing.\n\nLogs folder: " + LogDirectory,
            "ByteEngine Diagnostic Report", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (choice != DialogResult.Yes) return true;
        using SaveFileDialog dialog = new()
        {
            Title = "Save Diagnostic Report",
            Filter = "Diagnostic ZIP (*.zip)|*.zip",
            DefaultExt = "zip",
            AddExtension = true,
            FileName = $"ByteEngine-Diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip"
        };
        if (dialog.ShowDialog() != DialogResult.OK) return false;
        try
        {
            SaveReport(log, dialog.FileName);
            MessageBox.Show("Report saved. You can upload this ZIP to Discord.",
                "ByteEngine Diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }
        catch (Exception error)
        {
            MessageBox.Show($"Could not save report: {error.Message}\nOriginal diagnostics remain in:\n{LogDirectory}",
                "ByteEngine Diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    internal static void SaveReport(string sourceLog, string destination)
    {
        // Build in memory first: never include assets, scripts, arbitrary folders or raw command lines.
        string text = File.ReadAllText(sourceLog);
        text = Regex.Replace(text, @"(?im)^.*Command Line:.*$", "Command Line: [omitted]");
        text = Regex.Replace(text, @"(?i)[A-Z]:[\\/][^\r\n""<>]*", "[local path]");
        text = Regex.Replace(text, @"\\\\[^\r\n""<>]+", "[network path]");
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(archive.CreateEntry("diagnostics.txt").Open()))
                writer.Write(text);
            using (var writer = new StreamWriter(archive.CreateEntry("README.txt").Open()))
                writer.Write("ByteEngine diagnostic report. Nothing was uploaded automatically. Review diagnostics.txt before sharing. No project assets or scripts are included. Paths and command lines are redacted; free-form log messages may still contain personal information.");
        }
        using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        buffer.Position = 0;
        buffer.CopyTo(output);
    }

    public void Dispose() => _lease.Dispose();
}
