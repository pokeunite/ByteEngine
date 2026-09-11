using System.Numerics;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;

using RuntimeScene =
    ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.VisualLogic;

/// <summary>
/// Runtime bridge used by visual logic to instantiate asset-backed objects.
///
/// The editor configures this service when a project is opened. A standalone
/// player/exporter can configure the same service from its own project runtime
/// bootstrap later without changing EventModuleRuntime.
/// </summary>
public static class RuntimeSpawnService
{
    public delegate GameObject? BlueprintSpawner(
        RuntimeScene scene,
        AssetReference blueprint,
        Vector3 worldPosition);

    private static readonly object Sync =
        new();

    private static Guid _registrationId =
        Guid.Empty;

    private static BlueprintSpawner? _blueprintSpawner;

    public static bool IsBlueprintSpawnerConfigured
    {
        get
        {
            lock (Sync)
            {
                return _blueprintSpawner !=
                       null;
            }
        }
    }

    public static Guid ConfigureBlueprintSpawner(
        BlueprintSpawner spawner)
    {
        ArgumentNullException.ThrowIfNull(
            spawner);

        Guid registrationId =
            Guid.NewGuid();

        lock (Sync)
        {
            _registrationId =
                registrationId;

            _blueprintSpawner =
                spawner;
        }

        return registrationId;
    }

    public static void ClearBlueprintSpawner(
        Guid registrationId)
    {
        if (registrationId ==
            Guid.Empty)
        {
            return;
        }

        lock (Sync)
        {
            if (_registrationId !=
                registrationId)
            {
                return;
            }

            _registrationId =
                Guid.Empty;

            _blueprintSpawner =
                null;
        }
    }

    public static GameObject? SpawnBlueprint(
        RuntimeScene scene,
        AssetReference blueprint,
        Vector3 worldPosition)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        ArgumentNullException.ThrowIfNull(
            blueprint);

        BlueprintSpawner? spawner;

        lock (Sync)
        {
            spawner =
                _blueprintSpawner;
        }

        return spawner?.Invoke(
            scene,
            blueprint,
            worldPosition);
    }
}
