using System.Numerics;
using System.Text.Json.Nodes;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Core.Serialization;

public static class RendererSerializationRegistrar
{
    public static void Register(
        ComponentSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(
            serializer);

        serializer.Register(
            new MeshRendererV09Codec());

        serializer.Register(
            new PointLightCodec());

        serializer.Register(
            new SkyEnvironmentCodec());

        /*
         * Overrides the original DirectionalLight codec so shadow settings
         * participate in scene/Blueprint persistence without changing the
         * central ComponentSerializer.
         */
        serializer.Register(
            new DirectionalLightV09Codec());
    }

    private sealed class SkyEnvironmentCodec
        : IComponentCodec
    {
        public string TypeName =>
            "SkyEnvironment";

        public Type ComponentType =>
            typeof(SkyEnvironment);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            SkyEnvironment environment =
                (SkyEnvironment)component;

            return
                new ComponentData
                {
                    Type =
                        TypeName,

                    Properties =
                        new JsonObject
                        {
                            ["drawSky"] =
                                environment.DrawSky,

                            ["zenithColor"] =
                                Vector3Node(
                                    environment.ZenithColor),

                            ["horizonColor"] =
                                Vector3Node(
                                    environment.HorizonColor),

                            ["groundColor"] =
                                Vector3Node(
                                    environment.GroundColor),

                            ["skyIntensity"] =
                                environment.SkyIntensity,

                            ["horizonSharpness"] =
                                environment.HorizonSharpness,

                            ["overrideAmbient"] =
                                environment.OverrideAmbient,

                            ["ambientIntensity"] =
                                environment.AmbientIntensity
                        }
                };
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            return
                new SkyEnvironment
                {
                    DrawSky =
                        data.Properties["drawSky"]?
                            .GetValue<bool>() ??
                        true,

                    ZenithColor =
                        ReadVector3(
                            data.Properties["zenithColor"],
                            new Vector3(
                                0.08f,
                                0.20f,
                                0.48f)),

                    HorizonColor =
                        ReadVector3(
                            data.Properties["horizonColor"],
                            new Vector3(
                                0.58f,
                                0.72f,
                                0.95f)),

                    GroundColor =
                        ReadVector3(
                            data.Properties["groundColor"],
                            new Vector3(
                                0.08f,
                                0.075f,
                                0.07f)),

                    SkyIntensity =
                        ReadFloat(
                            data,
                            "skyIntensity",
                            1.0f),

                    HorizonSharpness =
                        ReadFloat(
                            data,
                            "horizonSharpness",
                            1.25f),

                    OverrideAmbient =
                        data.Properties["overrideAmbient"]?
                            .GetValue<bool>() ??
                        true,

                    AmbientIntensity =
                        ReadFloat(
                            data,
                            "ambientIntensity",
                            0.20f)
                };
        }
    }

    private sealed class DirectionalLightV09Codec
        : IComponentCodec
    {
        public string TypeName =>
            "DirectionalLight";

        public Type ComponentType =>
            typeof(DirectionalLight);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            DirectionalLight light =
                (DirectionalLight)component;

            return
                new ComponentData
                {
                    Type =
                        TypeName,

                    Properties =
                        new JsonObject
                        {
                            ["color"] =
                                Vector3Node(
                                    light.Color),

                            ["intensity"] =
                                light.Intensity,

                            ["ambientIntensity"] =
                                light.AmbientIntensity,

                            ["castShadows"] =
                                light.CastShadows,

                            ["shadowResolution"] =
                                light.ShadowResolution,

                            ["shadowDistance"] =
                                light.ShadowDistance,

                            ["shadowBias"] =
                                light.ShadowBias,

                            ["shadowStrength"] =
                                light.ShadowStrength,

                            ["shadowSoftness"] =
                                light.ShadowSoftness
                        }
                };
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            return
                new DirectionalLight
                {
                    Color =
                        ReadVector3(
                            data.Properties["color"],
                            Vector3.One),

                    Intensity =
                        ReadFloat(
                            data,
                            "intensity",
                            1.0f),

                    AmbientIntensity =
                        ReadFloat(
                            data,
                            "ambientIntensity",
                            0.25f),

                    CastShadows =
                        data.Properties["castShadows"]?
                            .GetValue<bool>() ??
                        false,

                    ShadowResolution =
                        ReadInt(
                            data,
                            "shadowResolution",
                            2048),

                    ShadowDistance =
                        ReadFloat(
                            data,
                            "shadowDistance",
                            50.0f),

                    ShadowBias =
                        ReadFloat(
                            data,
                            "shadowBias",
                            0.0015f),

                    ShadowStrength =
                        ReadFloat(
                            data,
                            "shadowStrength",
                            1.0f),

                    ShadowSoftness =
                        ReadFloat(
                            data,
                            "shadowSoftness",
                            1.0f)
                };
        }
    }

    private sealed class PointLightCodec
        : IComponentCodec
    {
        public string TypeName =>
            "PointLight";

        public Type ComponentType =>
            typeof(PointLight);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            PointLight light =
                (PointLight)component;

            return
                new ComponentData
                {
                    Type =
                        TypeName,

                    Properties =
                        new JsonObject
                        {
                            ["color"] =
                                Vector3Node(
                                    light.Color),

                            ["intensity"] =
                                light.Intensity,

                            ["range"] =
                                light.Range,

                            ["castShadows"] =
                                light.CastShadows,

                            ["shadowResolution"] =
                                light.ShadowResolution,

                            ["shadowBias"] =
                                light.ShadowBias,

                            ["shadowStrength"] =
                                light.ShadowStrength,

                            ["shadowSoftness"] =
                                light.ShadowSoftness
                        }
                };
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            return
                new PointLight
                {
                    Color =
                        ReadVector3(
                            data.Properties["color"],
                            Vector3.One),

                    Intensity =
                        ReadFloat(
                            data,
                            "intensity",
                            2.0f),

                    Range =
                        ReadFloat(
                            data,
                            "range",
                            10.0f),

                    CastShadows =
                        data.Properties["castShadows"]?
                            .GetValue<bool>() ??
                        false,

                    ShadowResolution =
                        ReadInt(
                            data,
                            "shadowResolution",
                            512),

                    ShadowBias =
                        ReadFloat(
                            data,
                            "shadowBias",
                            0.05f),

                    ShadowStrength =
                        ReadFloat(
                            data,
                            "shadowStrength",
                            1.0f),

                    ShadowSoftness =
                        ReadFloat(
                            data,
                            "shadowSoftness",
                            0.06f)
                };
        }
    }

    private sealed class MeshRendererV09Codec
        : IComponentCodec
    {
        public string TypeName =>
            "MeshRenderer";

        public Type ComponentType =>
            typeof(MeshRenderer);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            MeshRenderer renderer =
                (MeshRenderer)component;

            JsonObject properties =
                new()
                {
                    ["primitive"] =
                        renderer.Primitive.ToString(),

                    ["usePrimitive"] =
                        renderer.UsePrimitive,

                    ["visible"] =
                        renderer.Visible,

                    ["frustumCulling"] =
                        renderer.FrustumCulling,

                    ["castShadows"] =
                        renderer.CastShadows,

                    ["receiveShadows"] =
                        renderer.ReceiveShadows,

                    ["automaticRenderQueue"] =
                        renderer.AutomaticRenderQueue,

                    ["renderQueue"] =
                        renderer.RenderQueue.ToString(),

                    ["metallic"] =
                        renderer.Material.Metallic,

                    ["roughness"] =
                        renderer.Material.Roughness,

                    ["blendMode"] =
                        renderer.Material.BlendMode.ToString(),

                    ["alphaCutoff"] =
                        renderer.Material.AlphaCutoff,

                    ["depthTest"] =
                        renderer.Material.DepthTest,

                    ["depthWriteMode"] =
                        renderer.Material.DepthWriteMode.ToString(),

                    ["cullMode"] =
                        renderer.Material.CullMode.ToString(),

                    ["frontFace"] =
                        renderer.Material.FrontFace.ToString(),

                    ["polygonMode"] =
                        renderer.Material.PolygonMode.ToString(),

                    ["baseColor"] =
                        new JsonArray(
                            renderer.Material.BaseColor.X,
                            renderer.Material.BaseColor.Y,
                            renderer.Material.BaseColor.Z,
                            renderer.Material.BaseColor.W)
                };

            if (renderer.MeshReference !=
                null)
            {
                properties["modelGuid"] =
                    renderer.MeshReference
                        .Model
                        .Guid
                        .ToString();

                properties["modelPath"] =
                    renderer.MeshReference
                        .Model
                        .CachedProjectPath;

                properties["meshKey"] =
                    renderer.MeshReference
                        .SubAssetKey;
            }

            if (renderer.MaterialReference !=
                null)
            {
                properties["materialKey"] =
                    renderer.MaterialReference
                        .SubAssetKey;
            }

            return
                new ComponentData
                {
                    Type =
                        TypeName,

                    Properties =
                        properties
                };
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            if (!Enum.TryParse(
                    data.Properties["primitive"]?
                        .GetValue<string>(),
                    true,
                    out PrimitiveMeshType primitive))
            {
                primitive =
                    PrimitiveMeshType.Cube;
            }

            if (!Enum.TryParse(
                    data.Properties["renderQueue"]?
                        .GetValue<string>(),
                    true,
                    out RenderQueue3D renderQueue))
            {
                renderQueue =
                    RenderQueue3D.Opaque;
            }

            if (!Enum.TryParse(
                    data.Properties["blendMode"]?
                        .GetValue<string>(),
                    true,
                    out BlendMode3D blendMode))
            {
                blendMode =
                    BlendMode3D.Opaque;
            }

            if (!Enum.TryParse(
                    data.Properties["depthWriteMode"]?
                        .GetValue<string>(),
                    true,
                    out DepthWriteMode3D depthWriteMode))
            {
                depthWriteMode =
                    DepthWriteMode3D.Automatic;
            }

            if (!Enum.TryParse(
                    data.Properties["cullMode"]?
                        .GetValue<string>(),
                    true,
                    out CullMode3D cullMode))
            {
                cullMode =
                    CullMode3D.None;
            }

            if (!Enum.TryParse(
                    data.Properties["frontFace"]?
                        .GetValue<string>(),
                    true,
                    out FrontFaceWinding3D frontFace))
            {
                frontFace =
                    FrontFaceWinding3D.CounterClockwise;
            }

            if (!Enum.TryParse(
                    data.Properties["polygonMode"]?
                        .GetValue<string>(),
                    true,
                    out PolygonMode3D polygonMode))
            {
                polygonMode =
                    PolygonMode3D.Fill;
            }

            Vector4 baseColor =
                ReadVector4(
                    data.Properties["baseColor"],
                    Vector4.One);

            Material serializedMaterial =
                new()
                {
                    BaseColor =
                        baseColor,

                    Metallic =
                        ReadFloat(
                            data,
                            "metallic",
                            0.0f),

                    Roughness =
                        ReadFloat(
                            data,
                            "roughness",
                            1.0f),

                    BlendMode =
                        blendMode,

                    AlphaCutoff =
                        ReadFloat(
                            data,
                            "alphaCutoff",
                            0.5f),

                    DepthTest =
                        data.Properties["depthTest"]?
                            .GetValue<bool>() ??
                        true,

                    DepthWriteMode =
                        depthWriteMode,

                    CullMode =
                        cullMode,

                    FrontFace =
                        frontFace,

                    PolygonMode =
                        polygonMode
                };

            MeshRenderer renderer =
                new()
                {
                    Primitive =
                        primitive,

                    /*
                     * Backward compatibility:
                     * older primitive MeshRenderers did not store an explicit
                     * UsePrimitive flag. If no imported mesh key exists,
                     * preserve their previous primitive behavior.
                     */
                    UsePrimitive =
                        data.Properties["usePrimitive"]?
                            .GetValue<bool>() ??
                        !data.Properties.ContainsKey(
                            "meshKey"),

                    Visible =
                        data.Properties["visible"]?
                            .GetValue<bool>() ??
                        true,

                    FrustumCulling =
                        data.Properties["frustumCulling"]?
                            .GetValue<bool>() ??
                        true,

                    CastShadows =
                        data.Properties["castShadows"]?
                            .GetValue<bool>() ??
                        true,

                    ReceiveShadows =
                        data.Properties["receiveShadows"]?
                            .GetValue<bool>() ??
                        true,

                    AutomaticRenderQueue =
                        data.Properties["automaticRenderQueue"]?
                            .GetValue<bool>() ??
                        true,

                    RenderQueue =
                        renderQueue,

                    Material =
                        serializedMaterial
                };

            string? meshKey =
                data.Properties["meshKey"]?
                    .GetValue<string>();

            if (string.IsNullOrWhiteSpace(
                    meshKey))
            {
                return renderer;
            }

            Guid.TryParse(
                data.Properties["modelGuid"]?
                    .GetValue<string>(),
                out Guid modelGuid);

            string? modelPath =
                data.Properties["modelPath"]?
                    .GetValue<string>();

            AssetReference modelReference =
                new(
                    modelGuid,
                    modelPath);

            renderer.MeshReference =
                new ModelMeshReference(
                    modelReference,
                    meshKey);

            string? materialKey =
                data.Properties["materialKey"]?
                    .GetValue<string>();

            if (!string.IsNullOrWhiteSpace(
                    materialKey))
            {
                renderer.MaterialReference =
                    new ModelMaterialReference(
                        modelReference,
                        materialKey);
            }

            try
            {
                renderer.Mesh =
                    context.Assets.GetModelMesh(
                        modelReference,
                        meshKey);

                if (renderer.MaterialReference !=
                    null)
                {
                    Material imported =
                        context.Assets.GetModelMaterial(
                            modelReference,
                            renderer.MaterialReference
                                .SubAssetKey)
                        .Clone();

                    imported.BaseColor =
                        serializedMaterial.BaseColor;

                    imported.Metallic =
                        serializedMaterial.Metallic;

                    imported.Roughness =
                        serializedMaterial.Roughness;

                    imported.BlendMode =
                        serializedMaterial.BlendMode;

                    imported.AlphaCutoff =
                        serializedMaterial.AlphaCutoff;

                    imported.DepthTest =
                        serializedMaterial.DepthTest;

                    imported.DepthWriteMode =
                        serializedMaterial.DepthWriteMode;

                    imported.CullMode =
                        serializedMaterial.CullMode;

                    imported.FrontFace =
                        serializedMaterial.FrontFace;

                    imported.PolygonMode =
                        serializedMaterial.PolygonMode;

                    renderer.Material =
                        imported;
                }
            }
            catch (Exception exception)
            {
                context.WarningSink?.Invoke(
                    $"Could not resolve imported mesh '{meshKey}': {exception.Message}");
            }

            return renderer;
        }
    }

    private static int ReadInt(
        ComponentData data,
        string name,
        int fallback)
    {
        return
            data.Properties[name]?
                .GetValue<int>() ??
            fallback;
    }

    private static float ReadFloat(
        ComponentData data,
        string name,
        float fallback)
    {
        return
            data.Properties[name]?
                .GetValue<float>() ??
            fallback;
    }

    private static JsonArray Vector3Node(
        Vector3 value)
    {
        return
            new JsonArray(
                value.X,
                value.Y,
                value.Z);
    }

    private static Vector3 ReadVector3(
        JsonNode? node,
        Vector3 fallback)
    {
        if (node is not
                JsonArray array ||
            array.Count <
                3)
        {
            return fallback;
        }

        return
            new Vector3(
                array[0]?
                    .GetValue<float>() ??
                fallback.X,
                array[1]?
                    .GetValue<float>() ??
                fallback.Y,
                array[2]?
                    .GetValue<float>() ??
                fallback.Z);
    }

    private static Vector4 ReadVector4(
        JsonNode? node,
        Vector4 fallback)
    {
        if (node is not
                JsonArray array ||
            array.Count <
                4)
        {
            return fallback;
        }

        return
            new Vector4(
                array[0]?
                    .GetValue<float>() ??
                fallback.X,
                array[1]?
                    .GetValue<float>() ??
                fallback.Y,
                array[2]?
                    .GetValue<float>() ??
                fallback.Z,
                array[3]?
                    .GetValue<float>() ??
                fallback.W);
    }
}
