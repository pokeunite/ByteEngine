using ByteEngine.Core.Graphics;

namespace ByteEngine.Core.Scene;

public sealed partial class Scene
{
    private bool _renderRegistryDirty = true;
    private bool _cameraRegistryDirty = true;
    private int _componentRegistryVersion;
    private readonly List<SkyEnvironment> _environments = new();
    private readonly List<DirectionalLight> _directionalLights = new();
    private readonly List<PointLight> _pointLights = new();
    private readonly List<Camera3D> _cameras = new();
    internal IReadOnlyList<SkyEnvironment> Environments { get { RefreshRenderRegistry(); return _environments; } }
    internal IReadOnlyList<DirectionalLight> DirectionalLights { get { RefreshRenderRegistry(); return _directionalLights; } }
    internal IReadOnlyList<PointLight> PointLights { get { RefreshRenderRegistry(); return _pointLights; } }
    internal void InvalidateComponentRegistries()
    {
        unchecked { _componentRegistryVersion++; }
        InvalidateObjectLookup();
        InvalidatePhysicsRegistry();
        _renderRegistryDirty = true;
        _cameraRegistryDirty = true;
    }
    private void RefreshRenderRegistry()
    {
        if (!_renderRegistryDirty) return;
        _environments.Clear(); _directionalLights.Clear(); _pointLights.Clear();
        foreach (var item in _gameObjects)
            foreach (var component in item.Components)
                if (component is SkyEnvironment sky) _environments.Add(sky);
                else if (component is DirectionalLight sun) _directionalLights.Add(sun);
                else if (component is PointLight point) _pointLights.Add(point);
        _renderRegistryDirty = false;
    }
    internal void RefreshCameraRegistry()
    {
        if (!_cameraRegistryDirty) return;
        _cameras.Clear();
        foreach (var item in _gameObjects)
            foreach (var component in item.Components)
                if (component is Camera3D camera) _cameras.Add(camera);
        _cameraRegistryDirty = false;
    }
}
