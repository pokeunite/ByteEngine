using System.Reflection;
using System.Text.Json.Nodes;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Core.Runtime;

/// <summary>Prepares referenced spawn assets without instantiating enemies or
/// running gameplay callbacks. Packaged projects are immutable; editor loads
/// continue to observe live files. Work is done before the scene enters Play.</summary>
internal static class RuntimeAssetPrewarmer
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Type,PropertyInfo[]> Properties=new();
    internal static IReadOnlyList<Texture2D> Prepare(Scene.Scene scene,AssetDatabase database,AssetManager assets,Action<string>? warning)
    {
        var seen=new HashSet<Guid>();var textures=new HashSet<Texture2D>();
        void Material(Material m){foreach(var t in new[]{m.MainTexture,m.NormalTexture,m.MetallicTexture,m.RoughnessTexture,m.AmbientOcclusionTexture,m.PackedPbrTexture,m.EmissionTexture})if(t!=null)textures.Add(t);}
        void Walk(JsonNode? node)
        {
            if(node is JsonObject obj)foreach(var pair in obj)Walk(pair.Value);
            else if(node is JsonArray array)foreach(var value in array)Walk(value);
            else if(node is JsonValue value&&value.TryGetValue<string>(out var text)&&Guid.TryParse(text,out var guid)&&database.TryGetAsset(guid,out var asset)&&asset!=null)Warm(asset);
        }
        void Warm(AssetRecord asset)
        {
            if(seen.Count>=256||!seen.Add(asset.Guid))return;
            try{
                var reference=new AssetReference(asset.Guid,asset.ProjectPath);
                switch(asset.Type){
                    case AssetType.Blueprint:
                        var template=BlueprintRuntimeFactory.Prepare(asset,database);
                        foreach(var item in new[]{template.Root}.Concat(template.Children))foreach(var component in item.Components)Walk(component.Properties);
                        foreach(var id in template.EventModules)if(database.TryGetAsset(id,out var module)&&module!=null)Warm(module);
                        break;
                    case AssetType.Model3D:
                        var model=assets.LoadModel(reference);
                        foreach(var material in model.Materials)Material(assets.GetModelMaterial(reference,material.Key));
                        break;
                    case AssetType.Texture2D:textures.Add(assets.LoadTexture(reference));break;
                    case AssetType.Material:Material(assets.LoadMaterial(reference));break;
                    case AssetType.AnimationProfile:assets.LoadAnimationProfile(reference);Walk(JsonNode.Parse(File.ReadAllText(asset.FullPath)));break;
                    case AssetType.VfxEffect:assets.LoadVfxEffect(reference);Walk(JsonNode.Parse(File.ReadAllText(asset.FullPath)));break;
                    case AssetType.EventModule:Walk(JsonNode.Parse(File.ReadAllText(asset.FullPath)));break;
                }
            }catch(Exception e){warning?.Invoke($"Preparing runtime asset '{asset.ProjectPath}': {e.Message}");}
        }
        foreach(var component in scene.GameObjects.SelectMany(o=>o.Components)){
            var properties=Properties.GetValue(component.GetType(),static type=>type.GetProperties(BindingFlags.Public|BindingFlags.Instance)
                .Where(p=>p.CanRead&&p.GetIndexParameters().Length==0&&(p.PropertyType==typeof(AssetReference)||typeof(IEnumerable<AssetReference>).IsAssignableFrom(p.PropertyType))).ToArray());
            foreach(var property in properties)try{
                var value=property.GetValue(component);
                var references=value is AssetReference single?new[]{single}:value as IEnumerable<AssetReference>??Array.Empty<AssetReference>();
                foreach(var reference in references){var asset=database.Resolve(reference);if(asset?.Type is AssetType.Blueprint or AssetType.EventModule)Warm(asset);}
            }catch(Exception e){warning?.Invoke($"Preparing spawn references on {component.GetType().Name}: {e.Message}");}
        }
        return textures.ToArray();
    }
}
