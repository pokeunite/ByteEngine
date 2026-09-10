using System.Numerics;
using System.Text.Json.Nodes;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Core.Serialization;

public sealed class ComponentSerializer
{
    private readonly Dictionary<Type, IComponentCodec> _byRuntimeType =
        new();

    private readonly Dictionary<string, IComponentCodec> _byTypeName =
        new(
            StringComparer.OrdinalIgnoreCase
        );

    private readonly ComponentSerializationContext _context;

    public ComponentSerializer(
        string projectRoot,
        AssetDatabase assetDatabase,
        AssetManager assets,
        Action<string>? warningSink = null)
    {
        _context =
            new ComponentSerializationContext(
                projectRoot,
                assetDatabase,
                assets,
                warningSink
            );

        Register(
            new Camera2DCodec()
        );

        Register(
            new SpriteRendererCodec()
        );

        Register(new Camera3DCodec());
        Register(new DirectionalLightCodec());
        Register(new MeshRendererCodec());
        Register(new GroundSurfaceCodec());
        Register(new BoxCollider3DCodec());
    }

    public void Register(
        IComponentCodec codec)
    {
        ArgumentNullException.ThrowIfNull(
            codec
        );

        _byRuntimeType[codec.ComponentType] =
            codec;

        _byTypeName[codec.TypeName] =
            codec;
    }

    public ComponentData? Serialize(
        Component component)
    {
        if (!_byRuntimeType.TryGetValue(
                component.GetType(),
                out IComponentCodec? codec))
        {
            _context.WarningSink?.Invoke(
                $"Component type '{component.GetType().FullName}' is not registered and was skipped."
            );

            return null;
        }

        ComponentData data =
            codec.Serialize(
                component,
                _context
            );

        data.Enabled =
            component.Enabled;

        return data;
    }

    public Component? Deserialize(
        ComponentData data)
    {
        if (!_byTypeName.TryGetValue(
                data.Type,
                out IComponentCodec? codec))
        {
            _context.WarningSink?.Invoke(
                $"Unknown component type '{data.Type}' was skipped."
            );

            return null;
        }

        try
        {
            Component component =
                codec.Deserialize(
                    data,
                    _context
                );

            component.Enabled =
                data.Enabled;

            return component;
        }
        catch (Exception exception)
        {
            _context.WarningSink?.Invoke(
                $"Could not deserialize component '{data.Type}': {exception.Message}. The component was skipped."
            );

            return null;
        }
    }

    private sealed class Camera2DCodec
        : IComponentCodec
    {
        public string TypeName =>
            "Camera2D";

        public Type ComponentType =>
            typeof(Camera2D);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            Camera2D camera =
                (Camera2D)component;

            return new ComponentData
            {
                Type = TypeName,
                Properties =
                    new JsonObject
                    {
                        ["zoom"] =
                            camera.Zoom
                    }
            };
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            float zoom =
                data.Properties["zoom"]?
                    .GetValue<float>() ??
                1.0f;

            return new Camera2D
            {
                Zoom = zoom
            };
        }
    }

    private sealed class SpriteRendererCodec
        : IComponentCodec
    {
        public string TypeName =>
            "SpriteRenderer";

        public Type ComponentType =>
            typeof(SpriteRenderer);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            SpriteRenderer sprite =
                (SpriteRenderer)component;

            AssetReference? reference =
                sprite.TextureReference ??
                CreateReferenceFromTexture(
                    sprite.Texture,
                    context
                );

            JsonObject properties =
                new()
                {
                    ["texture"] = CreateTextureNode(reference),
                    ["tint"] =
                        new JsonArray(
                            sprite.Tint.X,
                            sprite.Tint.Y,
                            sprite.Tint.Z,
                            sprite.Tint.W
                        ),
                    ["visible"] =
                        sprite.Visible,
                    ["orderInLayer"] = sprite.OrderInLayer
                    , ["size"] = new JsonArray(sprite.Size.X, sprite.Size.Y)
                };

            return new ComponentData
            {
                Type = TypeName,
                Properties = properties
            };
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            AssetReference reference = ReadTextureReference(data.Properties["texture"], context);

            Texture2D? texture = reference.IsEmpty
                ? null
                : context.Assets.LoadTexture(reference);

            Vector4 tint =
                ReadTint(
                    data.Properties["tint"]
                );

            bool visible =
                data.Properties["visible"]?
                    .GetValue<bool>() ??
                true;

            int orderInLayer = data.Properties["orderInLayer"]?.GetValue<int>() ?? 0;
            Vector2 size = ReadVector2(data.Properties["size"], new Vector2(64f, 64f));

            return new SpriteRenderer(
                texture,
                reference
            )
            {
                Tint = tint,
                Visible = visible,
                OrderInLayer = orderInLayer,
                Size = size
            };
        }

        private static JsonNode CreateTextureNode(AssetReference? reference)
        {
            var texture = new JsonObject();
            if (reference == null || reference.IsEmpty) return texture;
            if (reference.Guid != Guid.Empty) texture["guid"] = reference.Guid.ToString();
            if (reference.CachedProjectPath != null) texture["path"] = reference.CachedProjectPath;
            return texture;
        }

        private static AssetReference ReadTextureReference(JsonNode? node, ComponentSerializationContext context)
        {
            if (node is JsonValue legacy && legacy.TryGetValue(out string? path) && !string.IsNullOrWhiteSpace(path))
            {
                AssetReference migrated = context.AssetDatabase.ResolveReference(path);
                context.WarningSink?.Invoke(
                    migrated.Guid != Guid.Empty
                        ? $"Migrated legacy texture path '{path}' to asset GUID {migrated.Guid}."
                        : $"Legacy texture path '{path}' could not be resolved; the path reference was preserved.");
                return migrated;
            }

            if (node is JsonObject value)
            {
                string? guidText = value["guid"]?.GetValue<string>();
                string? cachedPath = value["path"]?.GetValue<string>();
                if (Guid.TryParse(guidText, out Guid guid)) return new AssetReference(guid, cachedPath);
                if (!string.IsNullOrWhiteSpace(cachedPath)) return context.AssetDatabase.ResolveReference(cachedPath);
            }

            return AssetReference.Empty;
        }

        private static AssetReference? CreateReferenceFromTexture(
            Texture2D? texture,
            ComponentSerializationContext context)
        {
            if (texture == null)
            {
                return null;
            }

            if (texture.FilePath == null)
            {
                context.WarningSink?.Invoke(
                    "A SpriteRenderer using a generated texture could not be assigned a persistent asset path."
                );

                return null;
            }

            string relative =
                Path.GetRelativePath(
                    context.ProjectRoot,
                    texture.FilePath
                );

            if (relative.StartsWith(
                    "..",
                    StringComparison.Ordinal))
            {
                context.WarningSink?.Invoke(
                    $"Texture '{texture.FilePath}' is outside the project and cannot be serialized as an asset reference."
                );

                return null;
            }

            if (context.AssetDatabase.TryGetAsset(relative, out AssetRecord? asset) && asset != null)
            {
                return new AssetReference(asset.Guid, asset.ProjectPath);
            }

            context.WarningSink?.Invoke($"Texture '{relative}' is not registered in the asset database.");
            return new AssetReference(relative);
        }

        private static Vector4 ReadTint(
            JsonNode? node)
        {
            if (node is not JsonArray array ||
                array.Count < 4)
            {
                return Vector4.One;
            }

            return new Vector4(
                array[0]?.GetValue<float>() ??
                1.0f,
                array[1]?.GetValue<float>() ??
                1.0f,
                array[2]?.GetValue<float>() ??
                1.0f,
                array[3]?.GetValue<float>() ??
                1.0f
            );
        }

        private static Vector2 ReadVector2(JsonNode? node, Vector2 fallback)
        {
            if (node is not JsonArray array || array.Count < 2) return fallback;
            return new Vector2(array[0]?.GetValue<float>() ?? fallback.X, array[1]?.GetValue<float>() ?? fallback.Y);
        }
    }

    private sealed class Camera3DCodec : IComponentCodec
    {
        public string TypeName=>"Camera3D"; public Type ComponentType=>typeof(Camera3D);
        public ComponentData Serialize(Component component,ComponentSerializationContext context){var c=(Camera3D)component;return Data(TypeName,new(){["fieldOfView"]=c.FieldOfView,["nearClip"]=c.NearClip,["farClip"]=c.FarClip});}
        public Component Deserialize(ComponentData data,ComponentSerializationContext context)=>new Camera3D{FieldOfView=Float(data,"fieldOfView",60),NearClip=Float(data,"nearClip",.1f),FarClip=Float(data,"farClip",1000)};
    }

    private sealed class DirectionalLightCodec : IComponentCodec
    {
        public string TypeName=>"DirectionalLight"; public Type ComponentType=>typeof(DirectionalLight);
        public ComponentData Serialize(Component component,ComponentSerializationContext context){var c=(DirectionalLight)component;return Data(TypeName,new(){["color"]=Array(c.Color),["intensity"]=c.Intensity});}
        public Component Deserialize(ComponentData data,ComponentSerializationContext context)=>new DirectionalLight{Color=Vector3(data.Properties["color"],System.Numerics.Vector3.One),Intensity=Float(data,"intensity",1)};
    }

    private sealed class MeshRendererCodec : IComponentCodec
    {
        public string TypeName=>"MeshRenderer"; public Type ComponentType=>typeof(MeshRenderer);
        public ComponentData Serialize(Component component,ComponentSerializationContext context){var c=(MeshRenderer)component;return Data(TypeName,new(){["primitive"]=c.Primitive.ToString(),["visible"]=c.Visible,["baseColor"]=new JsonArray(c.Material.BaseColor.X,c.Material.BaseColor.Y,c.Material.BaseColor.Z,c.Material.BaseColor.W)});}
        public Component Deserialize(ComponentData data,ComponentSerializationContext context){Enum.TryParse(data.Properties["primitive"]?.GetValue<string>(),true,out PrimitiveMeshType primitive);JsonArray? a=data.Properties["baseColor"] as JsonArray;var color=a?.Count>=4?new Vector4(a[0]!.GetValue<float>(),a[1]!.GetValue<float>(),a[2]!.GetValue<float>(),a[3]!.GetValue<float>()):Vector4.One;return new MeshRenderer{Primitive=primitive,Visible=data.Properties["visible"]?.GetValue<bool>()??true,Material=new Material{BaseColor=color}};}
    }

    private sealed class GroundSurfaceCodec : IComponentCodec
    {
        public string TypeName=>"GroundSurface"; public Type ComponentType=>typeof(GroundSurface);
        public ComponentData Serialize(Component component,ComponentSerializationContext context){var c=(GroundSurface)component;return Data(TypeName,new(){["walkable"]=c.Walkable,["surfaceType"]=c.SurfaceType,["friction"]=c.Friction});}
        public Component Deserialize(ComponentData data,ComponentSerializationContext context)=>new GroundSurface{Walkable=data.Properties["walkable"]?.GetValue<bool>()??true,SurfaceType=data.Properties["surfaceType"]?.GetValue<string>()??"Default",Friction=Float(data,"friction",1)};
    }

    private sealed class BoxCollider3DCodec : IComponentCodec
    {
        public string TypeName=>"BoxCollider3D"; public Type ComponentType=>typeof(BoxCollider3D);
        public ComponentData Serialize(Component component,ComponentSerializationContext context){var c=(BoxCollider3D)component;return Data(TypeName,new(){["size"]=Array(c.Size),["center"]=Array(c.Center),["isTrigger"]=c.IsTrigger});}
        public Component Deserialize(ComponentData data,ComponentSerializationContext context)=>new BoxCollider3D{Size=Vector3(data.Properties["size"],System.Numerics.Vector3.One),Center=Vector3(data.Properties["center"],System.Numerics.Vector3.Zero),IsTrigger=data.Properties["isTrigger"]?.GetValue<bool>()??false};
    }

    private static ComponentData Data(string type,JsonObject properties)=>new(){Type=type,Properties=properties};
    private static float Float(ComponentData data,string name,float fallback)=>data.Properties[name]?.GetValue<float>()??fallback;
    private static JsonArray Array(System.Numerics.Vector3 value)=>new(value.X,value.Y,value.Z);
    private static System.Numerics.Vector3 Vector3(JsonNode? node,System.Numerics.Vector3 fallback)=>node is JsonArray a&&a.Count>=3?new(a[0]?.GetValue<float>()??fallback.X,a[1]?.GetValue<float>()??fallback.Y,a[2]?.GetValue<float>()??fallback.Z):fallback;
}
