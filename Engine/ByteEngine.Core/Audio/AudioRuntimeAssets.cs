using ByteEngine.Core.Assets;

namespace ByteEngine.Core.Audio;

/// <summary>
/// Project-lifetime bridge used by runtime audio actions. The editor configures
/// it when a project opens; standalone runtimes can configure it during their
/// own project bootstrap.
/// </summary>
public static class AudioRuntimeAssets
{
    private static readonly object Sync = new();

    private static Guid _registrationId = Guid.Empty;
    private static Func<
        AudioSource3D,
        AssetReference,
        Action<string>?,
        bool>? _clipLoader;

    public static bool IsConfigured
    {
        get
        {
            lock (Sync)
            {
                return _clipLoader != null;
            }
        }
    }

    public static Guid Configure(
        AssetDatabase database,
        Action<string>? warningSink = null)
    {
        ArgumentNullException.ThrowIfNull(database);

        return Configure(
            (source, reference, actionWarningSink) =>
            {
                Action<string>? warnings =
                    actionWarningSink ?? warningSink;
                AssetRecord? asset =
                    database.Resolve(reference);

                if (asset == null)
                {
                    warnings?.Invoke(
                        $"Audio clip '{reference}' could not be resolved.");
                    return false;
                }

                if (asset.Type != AssetType.AudioClip)
                {
                    warnings?.Invoke(
                        $"Asset '{asset.ProjectPath}' is not an Audio Clip.");
                    return false;
                }

                return AudioSerializationRegistrar.TryLoadClip(
                    source,
                    reference,
                    database,
                    warnings);
            });
    }

    internal static Guid Configure(
        Func<AudioSource3D, AssetReference, Action<string>?, bool> clipLoader)
    {
        ArgumentNullException.ThrowIfNull(clipLoader);

        Guid registrationId = Guid.NewGuid();

        lock (Sync)
        {
            _registrationId = registrationId;
            _clipLoader = clipLoader;
        }

        return registrationId;
    }

    public static void Clear(Guid registrationId)
    {
        if (registrationId == Guid.Empty)
        {
            return;
        }

        lock (Sync)
        {
            if (_registrationId != registrationId)
            {
                return;
            }

            _registrationId = Guid.Empty;
            _clipLoader = null;
        }
    }

    public static bool TryLoadClip(
        AudioSource3D source,
        AssetReference reference,
        Action<string>? warningSink = null)
    {
        Func<AudioSource3D, AssetReference, Action<string>?, bool>? loader;

        lock (Sync)
        {
            loader = _clipLoader;
        }

        if (loader == null)
        {
            warningSink?.Invoke(
                "Audio runtime assets are unavailable because no project is open.");
            return false;
        }

        return loader(source, reference, warningSink);
    }
}
