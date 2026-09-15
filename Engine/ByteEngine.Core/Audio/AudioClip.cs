using System.Runtime.InteropServices;
using System.Text;

using ByteEngine.Core.Diagnostics;

using OpenTK.Audio.OpenAL;

namespace ByteEngine.Core.Audio;

/// <summary>
/// Static PCM audio clip.
///
/// v0.11-A3 keeps WAV decoding fully managed. The native OpenAL buffer is
/// created lazily only when playback actually starts. This is important for
/// the editor: assigning an Audio Clip asset must never initialize a native
/// audio backend or terminate the editor process.
/// </summary>
public sealed class AudioClip
    : IDisposable
{
    private readonly byte[] _pcmData;

    private readonly ALFormat _format;

    private int _buffer;

    public string SourcePath { get; }

    public int Channels { get; }

    public int BitsPerSample { get; }

    public int SampleRate { get; }

    public float DurationSeconds { get; }

    internal int Buffer =>
        _buffer;

    internal bool IsUploaded =>
        _buffer !=
        0;

    private AudioClip(
        string sourcePath,
        ALFormat format,
        byte[] pcmData,
        int channels,
        int bitsPerSample,
        int sampleRate,
        float durationSeconds)
    {
        SourcePath =
            sourcePath;

        _format =
            format;

        _pcmData =
            pcmData;

        Channels =
            channels;

        BitsPerSample =
            bitsPerSample;

        SampleRate =
            sampleRate;

        DurationSeconds =
            durationSeconds;
    }

    /// <summary>
    /// Parses and validates a WAV file without touching OpenAL.
    /// Safe to call in Edit Mode and during scene/Blueprint deserialization.
    /// </summary>
    public static bool TryLoadWave(
        string path,
        out AudioClip? clip,
        Action<string>? warningSink = null)
    {
        clip =
            null;

        if (string.IsNullOrWhiteSpace(
                path) ||
            !File.Exists(
                path))
        {
            warningSink?.Invoke(
                $"Audio file was not found: {path}");

            return false;
        }

        if (!Path.GetExtension(
                path)
            .Equals(
                ".wav",
                StringComparison.OrdinalIgnoreCase))
        {
            warningSink?.Invoke(
                $"Audio v0.11-A supports PCM WAV files only: {path}");

            return false;
        }

        try
        {
            WaveData wave =
                ReadWave(
                    path);

            float bytesPerSecond =
                wave.SampleRate *
                wave.Channels *
                (
                    wave.BitsPerSample /
                    8.0f
                );

            float duration =
                bytesPerSecond >
                    0.0f
                    ? wave.Data.Length /
                      bytesPerSecond
                    : 0.0f;

            clip =
                new AudioClip(
                    Path.GetFullPath(
                        path),
                    wave.Format,
                    wave.Data,
                    wave.Channels,
                    wave.BitsPerSample,
                    wave.SampleRate,
                    duration);

            CrashDebugLog.Write(
                $"AudioClip: parsed WAV '{clip.SourcePath}' channels={clip.Channels} bits={clip.BitsPerSample} rate={clip.SampleRate} duration={clip.DurationSeconds:0.###}s without initializing OpenAL.");

            return true;
        }
        catch (Exception exception)
        {
            CrashDebugLog.WriteException(
                $"AudioClip.TryLoadWave failed for '{path}'",
                exception);

            warningSink?.Invoke(
                $"Could not load WAV '{path}': {exception.Message}");

            return false;
        }
    }

    /// <summary>
    /// Uploads the already-decoded PCM data to OpenAL.
    /// Called only when playback/runtime actually needs a native buffer.
    /// </summary>
    internal bool EnsureUploaded(
        Action<string>? warningSink = null)
    {
        if (_buffer !=
            0)
        {
            return true;
        }

        if (_pcmData.Length ==
            0)
        {
            warningSink?.Invoke(
                $"Audio WAV contains no PCM sample data: {SourcePath}");

            return false;
        }

        CrashDebugLog.Write(
            $"AudioClip: EnsureUploaded BEGIN '{SourcePath}'.");

        if (!AudioEngine.EnsureInitialized())
        {
            CrashDebugLog.Write(
                $"AudioClip: EnsureUploaded aborted because backend initialization failed. {AudioEngine.LastError}");

            warningSink?.Invoke(
                AudioEngine.LastError);

            return false;
        }

        int buffer =
            0;

        GCHandle pcmHandle =
            default;

        try
        {
            /*
             * OpenAL keeps one error flag. Clear any stale error before this
             * isolated upload sequence so every GetError below belongs to the
             * operation immediately before it.
             */
            _ =
                AL.GetError();

            CrashDebugLog.Write(
                "AudioClip: before AL.GenBuffer.");

            buffer =
                AL.GenBuffer();

            ALError generateError =
                AL.GetError();

            CrashDebugLog.Write(
                $"AudioClip: after AL.GenBuffer id={buffer} error={generateError}.");

            if (generateError !=
                    ALError.NoError ||
                buffer ==
                    0)
            {
                string message =
                    $"OpenAL could not create an audio buffer for '{SourcePath}'. Error={generateError}.";

                CrashDebugLog.Write(
                    $"AudioClip: {message}");

                warningSink?.Invoke(
                    message);

                return false;
            }

            /*
             * IMPORTANT:
             *
             * AL.BufferData(IntPtr, size) expects a pointer to at least
             * 'size' valid bytes. Passing `ref` to a copied first byte and
             * then claiming the whole PCM array length causes OpenAL to read
             * beyond that one-byte local variable and can terminate the
             * process with 0xC0000005.
             *
             * Pin the ACTUAL managed PCM byte[] for the duration of the
             * native copy, and pass its real address.
             */
            pcmHandle =
                GCHandle.Alloc(
                    _pcmData,
                    GCHandleType.Pinned);

            IntPtr pcmPointer =
                pcmHandle.AddrOfPinnedObject();

            if (pcmPointer ==
                IntPtr.Zero)
            {
                string message =
                    $"Could not pin PCM data for '{SourcePath}'.";

                CrashDebugLog.Write(
                    $"AudioClip: {message}");

                warningSink?.Invoke(
                    message);

                return false;
            }

            _ =
                AL.GetError();

            CrashDebugLog.Write(
                $"AudioClip: before AL.BufferData bytes={_pcmData.Length} format={_format} rate={SampleRate} ptr=0x{pcmPointer.ToInt64():X}.");

            AL.BufferData(
                buffer,
                _format,
                pcmPointer,
                _pcmData.Length,
                SampleRate);

            ALError uploadError =
                AL.GetError();

            CrashDebugLog.Write(
                $"AudioClip: after AL.BufferData error={uploadError}.");

            if (uploadError !=
                ALError.NoError)
            {
                string message =
                    $"OpenAL rejected WAV buffer data for '{SourcePath}'. Error={uploadError}.";

                CrashDebugLog.Write(
                    $"AudioClip: {message}");

                warningSink?.Invoke(
                    message);

                return false;
            }

            _buffer =
                buffer;

            /*
             * Ownership has transferred to AudioClip. Prevent finally from
             * deleting the successfully-created OpenAL buffer.
             */
            buffer =
                0;

            CrashDebugLog.Write(
                $"AudioClip: EnsureUploaded COMPLETE buffer={_buffer}.");

            return true;
        }
        catch (Exception exception)
        {
            CrashDebugLog.WriteException(
                $"AudioClip.EnsureUploaded managed exception for '{SourcePath}'",
                exception);

            warningSink?.Invoke(
                $"Could not upload WAV '{SourcePath}' to OpenAL: {exception.Message}");

            return false;
        }
        finally
        {
            if (pcmHandle.IsAllocated)
            {
                pcmHandle.Free();
            }

            /*
             * If generation succeeded but anything after it failed, destroy
             * the native buffer now so repeated Play attempts cannot leak
             * OpenAL resources.
             */
            if (buffer !=
                    0 &&
                AudioEngine.IsAvailable)
            {
                try
                {
                    _ =
                        AL.GetError();

                    AL.DeleteBuffer(
                        buffer);

                    ALError deleteError =
                        AL.GetError();

                    if (deleteError !=
                        ALError.NoError)
                    {
                        CrashDebugLog.Write(
                            $"AudioClip: cleanup AL.DeleteBuffer({buffer}) returned {deleteError}.");
                    }
                }
                catch (Exception cleanupException)
                {
                    CrashDebugLog.WriteException(
                        $"AudioClip cleanup failed for buffer {buffer}",
                        cleanupException);
                }
            }
        }
    }

    public void Dispose()
    {
        if (_buffer ==
            0)
        {
            return;
        }

        if (AudioEngine.IsAvailable)
        {
            try
            {
                AL.DeleteBuffer(
                    _buffer);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    $"Could not delete audio buffer for '{SourcePath}': {exception.Message}");
            }
        }

        _buffer =
            0;
    }

    private static WaveData ReadWave(
        string path)
    {
        using FileStream stream =
            File.OpenRead(
                path);

        using BinaryReader reader =
            new(
                stream,
                Encoding.ASCII,
                leaveOpen:
                    false);

        if (ReadFourCc(
                reader) !=
                "RIFF")
        {
            throw new InvalidDataException(
                "Missing RIFF header.");
        }

        _ =
            reader.ReadUInt32();

        if (ReadFourCc(
                reader) !=
                "WAVE")
        {
            throw new InvalidDataException(
                "RIFF file is not WAVE audio.");
        }

        ushort formatCode =
            0;

        ushort channels =
            0;

        int sampleRate =
            0;

        ushort bitsPerSample =
            0;

        byte[]? data =
            null;

        while (reader.BaseStream.Position +
               8 <=
               reader.BaseStream.Length)
        {
            string chunk =
                ReadFourCc(
                    reader);

            uint chunkSize =
                reader.ReadUInt32();

            long chunkStart =
                reader.BaseStream.Position;

            long chunkEnd =
                chunkStart +
                chunkSize;

            if (chunkEnd >
                reader.BaseStream.Length)
            {
                throw new InvalidDataException(
                    $"WAV chunk '{chunk}' extends beyond end of file.");
            }

            switch (chunk)
            {
                case "fmt ":
                {
                    if (chunkSize <
                        16)
                    {
                        throw new InvalidDataException(
                            "WAV fmt chunk is too small.");
                    }

                    formatCode =
                        reader.ReadUInt16();

                    channels =
                        reader.ReadUInt16();

                    sampleRate =
                        reader.ReadInt32();

                    _ =
                        reader.ReadInt32();

                    _ =
                        reader.ReadUInt16();

                    bitsPerSample =
                        reader.ReadUInt16();

                    break;
                }

                case "data":
                {
                    if (chunkSize >
                        int.MaxValue)
                    {
                        throw new InvalidDataException(
                            "WAV data chunk is too large.");
                    }

                    data =
                        reader.ReadBytes(
                            (int)chunkSize);

                    break;
                }
            }

            reader.BaseStream.Position =
                chunkEnd;

            if ((chunkSize &
                 1) !=
                0 &&
                reader.BaseStream.Position <
                reader.BaseStream.Length)
            {
                reader.BaseStream.Position++;
            }
        }

        if (formatCode !=
            1)
        {
            throw new NotSupportedException(
                $"WAV encoding {formatCode} is not PCM. v0.11-A supports PCM WAV only.");
        }

        if (channels is not
            1 and not
            2)
        {
            throw new NotSupportedException(
                $"WAV channel count {channels} is unsupported. Use mono or stereo.");
        }

        if (bitsPerSample is not
            8 and not
            16)
        {
            throw new NotSupportedException(
                $"WAV bit depth {bitsPerSample} is unsupported. Use 8-bit or 16-bit PCM.");
        }

        if (sampleRate <=
            0)
        {
            throw new InvalidDataException(
                "WAV sample rate is invalid.");
        }

        if (data ==
            null)
        {
            throw new InvalidDataException(
                "WAV has no data chunk.");
        }

        ALFormat format =
            (
                channels,
                bitsPerSample
            ) switch
            {
                (1, 8) =>
                    ALFormat.Mono8,

                (1, 16) =>
                    ALFormat.Mono16,

                (2, 8) =>
                    ALFormat.Stereo8,

                (2, 16) =>
                    ALFormat.Stereo16,

                _ =>
                    throw new NotSupportedException()
            };

        return
            new WaveData(
                format,
                channels,
                bitsPerSample,
                sampleRate,
                data);
    }

    private static string ReadFourCc(
        BinaryReader reader)
    {
        byte[] bytes =
            reader.ReadBytes(
                4);

        if (bytes.Length !=
            4)
        {
            throw new EndOfStreamException();
        }

        return
            Encoding.ASCII.GetString(
                bytes);
    }

    private readonly record struct WaveData(
        ALFormat Format,
        ushort Channels,
        ushort BitsPerSample,
        int SampleRate,
        byte[] Data);
}
