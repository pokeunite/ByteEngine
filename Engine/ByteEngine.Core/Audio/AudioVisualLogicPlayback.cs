using ByteEngine.Core.Assets;

namespace ByteEngine.Core.Audio;

internal static class AudioVisualLogicPlayback
{
    public static bool PlayClip(
        AudioSource3D source,
        AssetReference reference,
        Action<string>? warningSink = null) =>
        Execute(
            source,
            reference,
            warningSink,
            AudioRuntimeAssets.TryLoadClip,
            () => source.IsPlaying,
            source.Stop,
            source.Play,
            () => source.BackendAvailable);

    internal static bool Execute(
        AudioSource3D source,
        AssetReference reference,
        Action<string>? warningSink,
        Func<AudioSource3D, AssetReference, Action<string>?, bool> loadClip,
        Func<bool> isPlaying,
        Action stop,
        Action play,
        Func<bool> backendAvailable)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(loadClip);
        ArgumentNullException.ThrowIfNull(isPlaying);
        ArgumentNullException.ThrowIfNull(stop);
        ArgumentNullException.ThrowIfNull(play);
        ArgumentNullException.ThrowIfNull(backendAvailable);

        if (reference.IsEmpty)
        {
            warningSink?.Invoke(
                "Play Audio Clip requires an Audio Clip asset.");
            return false;
        }

        bool alreadyLoaded =
            ReferencesSame(source.ClipReference, reference) &&
            source.ClipLoaded;

        if (!alreadyLoaded &&
            !loadClip(source, reference, warningSink))
        {
            return false;
        }

        if (!source.ClipLoaded)
        {
            warningSink?.Invoke(
                $"Audio clip '{reference}' did not produce a loaded clip.");
            return false;
        }

        if (!backendAvailable())
        {
            warningSink?.Invoke(
                $"Audio backend unavailable: {source.BackendStatus}");
            return false;
        }

        if (isPlaying())
        {
            stop();
        }

        play();
        return true;
    }

    internal static bool ReferencesSame(
        AssetReference left,
        AssetReference right)
    {
        if (left.Guid != Guid.Empty &&
            right.Guid != Guid.Empty)
        {
            return left.Guid == right.Guid;
        }

        return string.Equals(
            left.CachedProjectPath,
            right.CachedProjectPath,
            StringComparison.OrdinalIgnoreCase);
    }
}
