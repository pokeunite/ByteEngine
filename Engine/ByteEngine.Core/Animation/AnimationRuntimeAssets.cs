using ByteEngine.Core.Assets;

namespace ByteEngine.Core.Animation;

/// <summary>
/// Project-lifetime AssetManager bridge used by runtime animation components.
/// The editor configures it when a project opens; a standalone player can
/// configure the same bridge from its runtime bootstrap later.
/// </summary>
public static class AnimationRuntimeAssets
{
    private static readonly object Sync = new();

    private static Guid _registrationId = Guid.Empty;
    private static AssetManager? _assets;

    public static bool IsConfigured
    {
        get
        {
            lock (Sync)
            {
                return _assets != null;
            }
        }
    }

    public static Guid Configure(AssetManager assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        Guid registrationId = Guid.NewGuid();

        lock (Sync)
        {
            _registrationId = registrationId;
            _assets = assets;
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
            _assets = null;
        }
    }

    public static bool TryGet(out AssetManager? assets)
    {
        lock (Sync)
        {
            assets = _assets;
        }

        return assets != null;
    }
}
