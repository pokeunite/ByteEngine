using ByteEngine.Core.Graphics;

namespace ByteEngine.Core.Scene;

public sealed partial class Scene
{
    private bool _renderRegistryDirty = true;
    private readonly List<SkyEnvironment> _environments = new();
    private readonly List<DirectionalLight> _directionalLights = new();
    private readonly List<PointLight> _pointLights = new();
    internal IReadOnlyList<SkyEnvironment> Environments { get { RefreshRenderRegistry(); return _environments; } }
    internal IReadOnlyList<DirectionalLight> DirectionalLights { get { RefreshRenderRegistry(); return _directionalLights; } }
    internal IReadOnlyList<PointLight> PointLights { get { RefreshRenderRegistry(); return _pointLights; } }
    internal void InvalidateComponentRegistries()
    {
        InvalidateObjectLookup();
        InvalidatePhysicsRegistry();
        _renderRegistryDirty = true;
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
}
