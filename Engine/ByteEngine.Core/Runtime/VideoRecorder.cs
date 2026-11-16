using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ByteEngine.Core.Runtime;

public enum VideoRecorderState { Idle, WaitingForFrame, Recording, Saving, Error }

/// <summary>Bounded, asynchronous MP4 recording. The render thread supplies BGRA frames; only the encoder thread touches Media Foundation.</summary>
public sealed class VideoRecorder : IDisposable
{
    internal sealed record Frame(byte[] Pixels, long Time);
    private sealed class Session
    {
        internal readonly BlockingCollection<Frame> Frames = new(3);
        internal readonly Stopwatch Clock = Stopwatch.StartNew();
        internal required string Path;
        internal required int Width;
        internal required int Height;
        internal Task Work = Task.CompletedTask;
        internal long LastCapture = -1;
        internal long StopTime;
        internal readonly long QpcStart = (long)(Stopwatch.GetTimestamp() * (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency);
        internal bool Audio;
        internal VideoRecordingProfile Profile;
    }

    public const int FramesPerSecond = 30;
    private Session? _session;
    private readonly object _stateLock = new();
    private int _state;
    private string _message = "F9: start/stop video recording. Shift+F9: settings.";
    public VideoRecorderSettings Settings { get; } = VideoRecorderSettings.Load();
    public VideoRecorderState State => (VideoRecorderState)Volatile.Read(ref _state);
    public bool IsActive => State is VideoRecorderState.WaitingForFrame or VideoRecorderState.Recording;
    public string Message => Volatile.Read(ref _message);
    public string? LastFile { get; private set; }
    public int Width => _session?.Width ?? 0;
    public int Height => _session?.Height ?? 0;
    public long DroppedFrames { get; private set; }
    public TimeSpan Elapsed => _session?.Clock.Elapsed ?? TimeSpan.Zero;
    public VideoRecordingProfile RecordingProfile => IsActive && State != VideoRecorderState.WaitingForFrame && _session != null ? _session.Profile : Settings.Profile;

    public void Toggle()
    {
        if (IsActive) { _ = StopAsync(); return; }
        if (State == VideoRecorderState.Saving) return;
        if (!OperatingSystem.IsWindows()) { Fail("Video recording is available in Windows runtimes."); return; }
        Volatile.Write(ref _state, (int)VideoRecorderState.WaitingForFrame);
        Volatile.Write(ref _message, "Waiting for a rendered game frame. F9 cancels.");
    }

    internal bool PrepareFrame(int sourceWidth, int sourceHeight, out byte[]? pixels)
    {
        pixels = null;
        if (!IsActive || sourceWidth < 2 || sourceHeight < 2) return false;
        if (State == VideoRecorderState.WaitingForFrame)
        {
            var profile = Settings.Profile;
            var size = profile.Fit(sourceWidth, sourceHeight);
            try { Start(size.Width, size.Height); }
            catch (Exception e) { Fail("Cannot start recording: " + e.Message); return false; }
        }
        var session = _session;
        if (session == null || State != VideoRecorderState.Recording) return false;
        long now = session.Clock.Elapsed.Ticks;
        if (session.LastCapture >= 0 && now - session.LastCapture < TimeSpan.TicksPerSecond / FramesPerSecond) return false;
        session.LastCapture = now;
        if (session.Frames.Count >= 3) { DroppedFrames++; return false; }
        pixels = ArrayPool<byte>.Shared.Rent(checked(session.Width * session.Height * 4));
        return true;
    }

    internal void Start(int width, int height)
    {
        if (width < 2 || height < 2 || width > 1920 || height > 1080 || (width & 1) != 0 || (height & 1) != 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (_session?.Work.IsCompleted == false) throw new InvalidOperationException("A recording is still being saved.");
        _session?.Frames.Dispose();
        string path = Path.Combine(Settings.OutputDirectory, $"ByteEngine-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid().ToString("N")[..6]}.mp4");
        var session = new Session { Path = path, Width = width, Height = height, Audio = Settings.IncludeAudio, Profile = Settings.Profile };
        _session = session;
        LastFile = path;
        DroppedFrames = 0;
        Volatile.Write(ref _state, (int)VideoRecorderState.Recording);
        Volatile.Write(ref _message, "Recording video — F9 to stop.");
        session.Work = Task.Factory.StartNew(() => Encode(session), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    // Ownership of the pooled frame always transfers here, including a failed queue write.
    internal void Submit(byte[] pixels, long? timestamp = null)
    {
        var session = _session;
        bool accepted = false;
        try
        {
            if (session != null && State == VideoRecorderState.Recording)
                accepted = session.Frames.TryAdd(new(pixels, timestamp ?? session.Clock.Elapsed.Ticks));
        }
        catch (InvalidOperationException) { }
        finally
        {
            if (!accepted) { ArrayPool<byte>.Shared.Return(pixels); DroppedFrames++; }
        }
    }

    public Task StopAsync()
    {
        lock (_stateLock)
        {
            if (State == VideoRecorderState.WaitingForFrame)
            {
                Volatile.Write(ref _state, (int)VideoRecorderState.Idle);
                Volatile.Write(ref _message, "Recording cancelled.");
                return Task.CompletedTask;
            }
            var session = _session;
            if (State == VideoRecorderState.Recording && session != null)
            {
                session.StopTime = session.Clock.Elapsed.Ticks;
                session.Clock.Stop();
                Volatile.Write(ref _state, (int)VideoRecorderState.Saving);
                Volatile.Write(ref _message, "Saving video…");
                session.Frames.CompleteAdding();
            }
            return session?.Work ?? Task.CompletedTask;
        }
    }

    internal void Fail(string message)
    {
        lock (_stateLock)
        {
            _session?.Frames.CompleteAdding();
            Volatile.Write(ref _state, (int)VideoRecorderState.Error);
            Volatile.Write(ref _message, message);
        }
    }

    private void Encode(Session session)
    {
        Frame? pending = null;
        long origin = -1;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(session.Path)!);
            using var audio = session.Audio ? new WindowsPlaybackCapture() : null;
            using var writer = new WindowsMp4Writer(session.Path, session.Width, session.Height, FramesPerSecond, session.Audio, session.Profile.Bitrate);
            void DrainAudio()
            {
                if (origin < 0 || audio == null) return;
                audio.Drain((bytes, length, time) => writer.Audio(bytes, length, time - session.QpcStart - origin,
                    (session.StopTime > 0 ? session.StopTime : session.Clock.Elapsed.Ticks) - origin));
                // Endpoints may return no packets while silent. Keep both sink streams progressing,
                // allowing one loopback buffer of latency before treating missing playback as silence.
                writer.PadAudioTo(Math.Max(0, session.Clock.Elapsed.Ticks - origin - TimeSpan.TicksPerSecond / 2));
            }
            while (!session.Frames.IsCompleted)
            {
                if (session.Frames.TryTake(out var frame, 10))
                {
                    if (origin < 0) origin = frame.Time;
                    if (pending != null && frame.Time <= pending.Time) frame = frame with { Time = pending.Time + 1 };
                    var previous = pending;
                    pending = frame;
                    if (previous != null)
                    {
                        try { writer.Write(previous.Pixels, previous.Time - origin, Math.Max(1, frame.Time - previous.Time)); }
                        finally { ArrayPool<byte>.Shared.Return(previous.Pixels); }
                    }
                    FlipRows(frame.Pixels, session.Width, session.Height);
                }
                else if (pending != null && session.Clock.Elapsed.Ticks - pending.Time >= TimeSpan.TicksPerSecond / 5)
                {
                    // A modal dialog/minimized window can pause rendering while playback continues.
                    // Advance the held image so the two sink streams cannot deadlock waiting on each other.
                    long now = session.Clock.Elapsed.Ticks;
                    writer.Write(pending.Pixels, pending.Time - origin, now - pending.Time);
                    pending = pending with { Time = now };
                }
                DrainAudio();
            }
            DrainAudio();
            if (pending != null)
                writer.Write(pending.Pixels, pending.Time - origin, Math.Max(TimeSpan.TicksPerSecond / FramesPerSecond, session.StopTime - pending.Time));
            if (audio != null && origin >= 0) writer.PadAudioTo(Math.Max(TimeSpan.TicksPerSecond / FramesPerSecond, session.StopTime - origin));
            writer.Complete();
            lock (_stateLock)
            {
                if (State != VideoRecorderState.Error)
                {
                    Volatile.Write(ref _message, "Video saved: " + session.Path);
                    Volatile.Write(ref _state, (int)VideoRecorderState.Idle);
                }
            }
        }
        catch (Exception e)
        {
            Fail("Recording failed: " + e.Message + " Check the output folder and playback device. Windows N may need the Media Feature Pack.");
            try { File.Delete(session.Path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        finally
        {
            session.Clock.Stop();
            if (pending != null) ArrayPool<byte>.Shared.Return(pending.Pixels);
            while (session.Frames.TryTake(out var frame)) ArrayPool<byte>.Shared.Return(frame.Pixels);
        }
    }

    internal static void FlipRows(byte[] pixels, int width, int height)
    {
        int stride = checked(width * 4);
        byte[] row = ArrayPool<byte>.Shared.Rent(stride);
        try
        {
            for (int y = 0; y < height / 2; y++)
            {
                var top = pixels.AsSpan(y * stride, stride);
                var bottom = pixels.AsSpan((height - y - 1) * stride, stride);
                top.CopyTo(row);
                bottom.CopyTo(top);
                row.AsSpan(0, stride).CopyTo(bottom);
            }
        }
        finally { ArrayPool<byte>.Shared.Return(row); }
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _session?.Frames.Dispose();
        _session = null;
    }
}
