using System.Text.Json;
using System.Text.Json.Serialization;

namespace ByteEngine.Core.Runtime;

/// <summary>Local user preference shared by the editor and Windows players; never stored inside a game package.</summary>
public sealed class VideoRecorderSettings
{
    public string OutputDirectory { get; set; } = DefaultOutputDirectory;
    public bool IncludeAudio { get; set; } = true;
    private VideoRecordingQuality _quality = VideoRecordingQuality.High;
    public VideoRecordingQuality Quality
    {
        get => _quality;
        set => _quality = Enum.IsDefined(value) ? value : VideoRecordingQuality.High;
    }
    [JsonIgnore] public VideoRecordingProfile Profile => VideoRecordingProfile.For(Quality);
    public static string DefaultOutputDirectory
    {
        get
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos, Environment.SpecialFolderOption.DoNotVerify);
            return string.IsNullOrEmpty(path) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Videos") : path;
        }
    }
    internal static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ByteEngine", "Recorder", "settings.json");

    public static VideoRecorderSettings Load() => Load(SettingsPath);

    internal static VideoRecorderSettings Load(string settingsPath)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<VideoRecorderSettings>(File.ReadAllText(settingsPath));
            if (settings != null && Path.IsPathFullyQualified(settings.OutputDirectory)) return settings;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { }
        return new();
    }

    public bool TrySave(string folder, out string error) => TrySave(folder, SettingsPath, out error);

    internal bool TrySave(string folder, string settingsPath, out string error)
    {
        try
        {
            string path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(folder));
            Directory.CreateDirectory(path);
            string probe = Path.Combine(path, ".byteengine-recorder-" + Guid.NewGuid().ToString("N"));
            using (File.Create(probe)) { }
            File.Delete(probe);
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            var next = new VideoRecorderSettings { OutputDirectory = path, IncludeAudio = IncludeAudio, Quality = Quality };
            string temporary = settingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(next));
            File.Move(temporary, settingsPath, overwrite: true);
            OutputDirectory = path;
            error = string.Empty;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            error = e.Message;
            return false;
        }
    }
}
