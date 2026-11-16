namespace ByteEngine.Core.Runtime;

public enum VideoRecordingQuality
{
    Low,
    Standard,
    High,
    Ultra
}

public readonly record struct VideoRecordingProfile(string Label, int MaximumWidth, int MaximumHeight, int Bitrate)
{
    public (int Width, int Height) Fit(int width, int height)
    {
        double scale = Math.Min(1, Math.Min((double)MaximumWidth / Math.Max(1, width), (double)MaximumHeight / Math.Max(1, height)));
        return (Math.Max(2, (int)(width * scale) & ~1), Math.Max(2, (int)(height * scale) & ~1));
    }

    public static VideoRecordingProfile For(VideoRecordingQuality quality) => quality switch
    {
        VideoRecordingQuality.Low => new("Low - up to 720p / 4 Mbps", 1280, 720, 4_000_000),
        VideoRecordingQuality.Standard => new("Standard - up to 1080p / 12 Mbps", 1920, 1080, 12_000_000),
        VideoRecordingQuality.Ultra => new("Ultra - up to 1080p / 50 Mbps", 1920, 1080, 50_000_000),
        _ => new("High - up to 1080p / 25 Mbps", 1920, 1080, 25_000_000)
    };
}
