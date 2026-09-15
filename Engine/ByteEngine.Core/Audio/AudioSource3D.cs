using System.Numerics;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Scene;

using OpenTK.Audio.OpenAL;

namespace ByteEngine.Core.Audio;

/// <summary>
/// Plays one static AudioClip as either positional 3D sound or listener-relative
/// non-spatial audio.
///
/// Use mono WAV files for positional 3D sounds. Stereo WAV is best suited to
/// non-spatial music/UI because OpenAL does not spatialize normal stereo buffers.
/// </summary>
public sealed class AudioSource3D
    : Component
{
    private AudioClip? _clip;

    private int _source;

    private Vector3 _previousPosition;

    private bool _hasPreviousPosition;

    private float _volume =
        1.0f;

    private float _pitch =
        1.0f;

    private float _minDistance =
        1.0f;

    private float _maxDistance =
        100.0f;

    private float _rolloffFactor =
        1.0f;

    public AssetReference ClipReference { get; set; } =
        AssetReference.Empty;

    public bool PlayOnStart { get; set; }

    public bool Loop { get; set; }

    public bool Spatial { get; set; } =
        true;

    public float Volume
    {
        get =>
            _volume;

        set =>
            _volume =
                Math.Clamp(
                    Finite(
                        value,
                        1.0f),
                    0.0f,
                    4.0f);
    }

    public float Pitch
    {
        get =>
            _pitch;

        set =>
            _pitch =
                Math.Clamp(
                    Finite(
                        value,
                        1.0f),
                    0.25f,
                    4.0f);
    }

    public float MinDistance
    {
        get =>
            _minDistance;

        set =>
            _minDistance =
                Math.Max(
                    Finite(
                        value,
                        1.0f),
                    0.001f);
    }

    public float MaxDistance
    {
        get =>
            _maxDistance;

        set =>
            _maxDistance =
                Math.Max(
                    Finite(
                        value,
                        100.0f),
                    0.001f);
    }

    public float RolloffFactor
    {
        get =>
            _rolloffFactor;

        set =>
            _rolloffFactor =
                Math.Max(
                    Finite(
                        value,
                        1.0f),
                    0.0f);
    }

    public bool IsPlaying =>
        _source !=
            0 &&
        AudioEngine.IsAvailable &&
        (ALSourceState)AL.GetSource(
            _source,
            ALGetSourcei.SourceState) ==
        ALSourceState.Playing;

    public float DurationSeconds =>
        _clip?.DurationSeconds ??
        0.0f;

    /// <summary>
    /// True when the Audio Clip reference has been resolved and decoded into
    /// an OpenAL buffer.
    /// </summary>
    public bool ClipLoaded =>
        _clip !=
        null;

    /// <summary>
    /// True when ByteEngine has a live OpenAL device/context.
    /// </summary>
    public bool BackendAvailable =>
        AudioEngine.IsAvailable;

    /// <summary>
    /// Human-readable backend state surfaced directly in the Inspector so a
    /// missing Windows OpenAL runtime cannot fail silently.
    /// </summary>
    public string BackendStatus =>
        AudioEngine.IsAvailable
            ? $"Ready — {AudioEngine.DeviceName}"
            : string.IsNullOrWhiteSpace(
                AudioEngine.LastError)
                ? "Not initialized"
                : AudioEngine.LastError;

    public void SetClip(
        AudioClip? clip)
    {
        if (ReferenceEquals(
                _clip,
                clip))
        {
            return;
        }

        if (_source !=
            0 &&
            AudioEngine.IsAvailable)
        {
            AL.SourceStop(
                _source);

            AL.Source(
                _source,
                ALSourcei.Buffer,
                0);
        }

        _clip?.Dispose();

        _clip =
            clip;

        if (_source !=
                0 &&
            _clip !=
                null &&
            AudioEngine.IsAvailable &&
            _clip.EnsureUploaded())
        {
            AL.Source(
                _source,
                ALSourcei.Buffer,
                _clip.Buffer);
        }
    }

    public void Play()
    {
        if (_clip ==
            null)
        {
            return;
        }

        if (!EnsureSource())
        {
            return;
        }

        ApplySettings();

        CrashDebugLog.Write(
            $"AudioSource3D.Play before AL.SourcePlay source={_source}.");

        AL.SourcePlay(
            _source);

        CrashDebugLog.Write(
            "AudioSource3D.Play after AL.SourcePlay.");
    }

    public void Pause()
    {
        if (_source ==
                0 ||
            !AudioEngine.IsAvailable)
        {
            return;
        }

        AL.SourcePause(
            _source);
    }

    public void Stop()
    {
        if (_source ==
                0 ||
            !AudioEngine.IsAvailable)
        {
            return;
        }

        AL.SourceStop(
            _source);
    }

    protected override void OnStart()
    {
        _previousPosition =
            Transform.WorldPosition;

        _hasPreviousPosition =
            true;

        EnsureSource();

        if (PlayOnStart)
        {
            Play();
        }
    }

    protected override void OnUpdate()
    {
        if (_source ==
                0 ||
            !AudioEngine.IsAvailable)
        {
            return;
        }

        ApplySettings();

        UpdateSpatialState();
    }

    protected override void OnStop()
    {
        DestroySource();

        _hasPreviousPosition =
            false;
    }

    protected override void OnDestroy()
    {
        DestroySource();

        _clip?.Dispose();

        _clip =
            null;
    }

    private bool EnsureSource()
    {
        if (_source !=
            0)
        {
            return true;
        }

        CrashDebugLog.Write(
            $"AudioSource3D.EnsureSource BEGIN object='{AttachedGameObject?.Name ?? "<detached>"}' clipLoaded={_clip != null}.");

        if (!AudioEngine.EnsureInitialized())
        {
            CrashDebugLog.Write(
                $"AudioSource3D.EnsureSource backend unavailable: {AudioEngine.LastError}");

            return false;
        }

        if (_clip !=
                null &&
            !_clip.EnsureUploaded(
                message =>
                    CrashDebugLog.Write(
                        $"AudioSource3D clip upload warning: {message}")))
        {
            CrashDebugLog.Write(
                "AudioSource3D.EnsureSource failed because clip upload failed.");

            return false;
        }

        CrashDebugLog.Write(
            "AudioSource3D.EnsureSource before AL.GenSource.");

        _source =
            AL.GenSource();

        CrashDebugLog.Write(
            $"AudioSource3D.EnsureSource after AL.GenSource id={_source}.");

        if (_clip !=
            null)
        {
            CrashDebugLog.Write(
                $"AudioSource3D.EnsureSource before binding buffer id={_clip.Buffer}.");

            AL.Source(
                _source,
                ALSourcei.Buffer,
                _clip.Buffer);

            CrashDebugLog.Write(
                "AudioSource3D.EnsureSource after buffer bind.");
        }

        ApplySettings();

        UpdateSpatialState();

        CrashDebugLog.Write(
            "AudioSource3D.EnsureSource COMPLETE.");

        return true;
    }

    private void ApplySettings()
    {
        if (_source ==
            0)
        {
            return;
        }

        AL.Source(
            _source,
            ALSourcef.Gain,
            Volume);

        AL.Source(
            _source,
            ALSourcef.Pitch,
            Pitch);

        AL.Source(
            _source,
            ALSourceb.Looping,
            Loop);

        AL.Source(
            _source,
            ALSourceb.SourceRelative,
            !Spatial);

        float referenceDistance =
            Math.Max(
                MinDistance,
                0.001f);

        float maximumDistance =
            Math.Max(
                MaxDistance,
                referenceDistance);

        AL.Source(
            _source,
            ALSourcef.ReferenceDistance,
            referenceDistance);

        AL.Source(
            _source,
            ALSourcef.MaxDistance,
            maximumDistance);

        AL.Source(
            _source,
            ALSourcef.RolloffFactor,
            Spatial
                ? RolloffFactor
                : 0.0f);
    }

    private void UpdateSpatialState()
    {
        if (_source ==
            0)
        {
            return;
        }

        if (!Spatial)
        {
            AL.Source(
                _source,
                ALSource3f.Position,
                0.0f,
                0.0f,
                0.0f);

            AL.Source(
                _source,
                ALSource3f.Velocity,
                0.0f,
                0.0f,
                0.0f);

            return;
        }

        Vector3 position =
            Transform.WorldPosition;

        Vector3 velocity =
            Vector3.Zero;

        float deltaTime =
            (float)Time.DeltaTime;

        if (_hasPreviousPosition &&
            deltaTime >
                0.000001f)
        {
            velocity =
                (
                    position -
                    _previousPosition
                ) /
                deltaTime;
        }

        _previousPosition =
            position;

        _hasPreviousPosition =
            true;

        AL.Source(
            _source,
            ALSource3f.Position,
            position.X,
            position.Y,
            position.Z);

        AL.Source(
            _source,
            ALSource3f.Velocity,
            velocity.X,
            velocity.Y,
            velocity.Z);
    }

    private void DestroySource()
    {
        if (_source ==
            0)
        {
            return;
        }

        if (AudioEngine.IsAvailable)
        {
            try
            {
                AL.SourceStop(
                    _source);

                AL.Source(
                    _source,
                    ALSourcei.Buffer,
                    0);

                AL.DeleteSource(
                    _source);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    $"Could not destroy audio source on '{AttachedGameObject?.Name ?? "<detached>"}': {exception.Message}");
            }
        }

        _source =
            0;
    }

    private static float Finite(
        float value,
        float fallback)
    {
        return
            float.IsFinite(
                value)
                ? value
                : fallback;
    }
}
