using ByteEngine.Core.Graphics;

namespace ByteEngine.Core.Scene;

public sealed partial class Scene
{
    private bool _renderRegistryDirty = true;
    private bool _cameraRegistryDirty=true;
    private readonly List<Camera3D> _cameras=new();
    private readonly List<SkyEnvironment> _environments = new();
    private readonly List<DirectionalLight> _directionalLights = new();
    private readonly List<PointLight> _pointLights = new();
    private readonly List<ByteEngine.Core.Navigation.NavigationRegion3D> _navigationRegions=new();
    private bool _hasSimulationRates;
    internal bool HasSimulationRates {get{RefreshRenderRegistry();return _hasSimulationRates;}}
    internal ByteEngine.Core.Navigation.NavigationRegion3D? FindNavigationRegion(Guid id)
    {
        if(id!=Guid.Empty)return FindGameObject(id)?.GetComponent<ByteEngine.Core.Navigation.NavigationRegion3D>();
        RefreshRenderRegistry();
        foreach(var region in _navigationRegions)if(region.Enabled)return region;
        return null;
    }
    internal IReadOnlyList<SkyEnvironment> Environments { get { RefreshRenderRegistry(); return _environments; } }
    internal IReadOnlyList<DirectionalLight> DirectionalLights { get { RefreshRenderRegistry(); return _directionalLights; } }
    internal IReadOnlyList<PointLight> PointLights { get { RefreshRenderRegistry(); return _pointLights; } }
    internal void InvalidateComponentRegistries(Component? changed=null,bool physics=true,IReadOnlyList<Component>? membership=null)
    {
        InvalidateComponentSnapshots(changed,membership);
        if(physics&&(changed==null||AffectsPhysics(changed)))InvalidatePhysicsRegistry();
        else if(changed is ByteEngine.Core.Characters.CharacterController3D||membership?.Any(c=>c is ByteEngine.Core.Characters.CharacterController3D)==true||changed==null&&membership==null)AIRegistryRevision++;
        static bool RenderMember(Component c)=>c is SkyEnvironment or DirectionalLight or PointLight or ByteEngine.Core.Navigation.NavigationRegion3D or SimulationRate3D;
        bool all=changed==null&&membership==null;
        _renderRegistryDirty |= all||changed!=null&&RenderMember(changed)||membership?.Any(RenderMember)==true;
        _cameraRegistryDirty |= all||changed is Camera3D||membership?.Any(c=>c is Camera3D)==true;
    }
    private void RefreshRenderRegistry()
    {
        if (!_renderRegistryDirty) return;
        _environments.Clear(); _directionalLights.Clear(); _pointLights.Clear();_navigationRegions.Clear();
        _hasSimulationRates=false;
        foreach (var item in _gameObjects)
            foreach (var component in item.Components)
                if (component is SkyEnvironment sky) _environments.Add(sky);
                else if (component is DirectionalLight sun) _directionalLights.Add(sun);
                else if (component is PointLight point) _pointLights.Add(point);
                else if (component is ByteEngine.Core.Navigation.NavigationRegion3D region && ReferenceEquals(item.GetComponent<ByteEngine.Core.Navigation.NavigationRegion3D>(),region)) _navigationRegions.Add(region);
                else if (component is SimulationRate3D) _hasSimulationRates=true;
        _renderRegistryDirty = false;
    }
    private void RefreshCameraRegistry()
    {
        if(!_cameraRegistryDirty)return;_cameras.Clear();
        foreach(var item in _gameObjects)foreach(var component in item.Components)if(component is Camera3D camera)_cameras.Add(camera);
        _cameraRegistryDirty=false;
    }
}
