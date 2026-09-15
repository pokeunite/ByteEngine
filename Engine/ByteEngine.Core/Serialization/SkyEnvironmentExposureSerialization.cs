using System.Numerics;
using System.Text.Json.Nodes;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Core.Serialization;

/// <summary>
/// v0.9-L2 SkyEnvironment persistence.
///
/// RendererSerializationRegistrar installs the renderer codecs first.
/// Registering this codec afterward intentionally replaces only the
/// SkyEnvironment codec so Exposure participates in scene/Blueprint cloning
/// and persistence without disturbing the rest of the renderer serializer.
/// </summary>
public static class SkyEnvironmentExposureSerialization
{
    public static void Register(
        ComponentSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(
            serializer);

        serializer.Register(
            new SkyEnvironmentExposureCodec());
    }

    private sealed class SkyEnvironmentExposureCodec
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

                            ["skyMode"] =
                                (int)environment.SkyMode,

                            ["environmentMapGuid"] =
                                environment.EnvironmentMapReference.Guid.ToString(),

                            ["environmentMapPath"] =
                                environment.EnvironmentMapReference.CachedProjectPath,

                            ["environmentIntensity"] =
                                environment.EnvironmentIntensity,

                            ["environmentRotationDegrees"] =
                                environment.EnvironmentRotationDegrees,

                            ["environmentLightingEnabled"] =
                                environment.EnvironmentLightingEnabled,

                            ["exposure"] =
                                environment.Exposure,

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
                                environment.AmbientIntensity,

                            ["fogEnabled"] =
                                environment.FogEnabled,

                            ["fogMode"] =
                                (int)environment.FogMode,

                            ["fogColor"] =
                                Vector3Node(
                                    environment.FogColor),

                            ["fogStartDistance"] =
                                environment.FogStartDistance,

                            ["fogEndDistance"] =
                                environment.FogEndDistance,

                            ["fogDensity"] =
                                environment.FogDensity,

                            ["fogMaxOpacity"] =
                                environment.FogMaxOpacity
                        }
                };
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            Guid.TryParse(
                data.Properties["environmentMapGuid"]?
                    .GetValue<string>(),
                out Guid environmentMapGuid);

            string? environmentMapPath =
                data.Properties["environmentMapPath"]?
                    .GetValue<string>();

            AssetReference environmentMapReference =
                environmentMapGuid ==
                        Guid.Empty &&
                    string.IsNullOrWhiteSpace(
                        environmentMapPath)
                    ? AssetReference.Empty
                    : new AssetReference(
                        environmentMapGuid,
                        environmentMapPath);

            SkyEnvironment environment =
                new()
                {
                    DrawSky =
                        data.Properties["drawSky"]?
                            .GetValue<bool>() ??
                        true,

                    SkyMode =
                        (SkyMode3D)Math.Clamp(
                            ReadInt(
                                data,
                                "skyMode",
                                (int)SkyMode3D.Procedural),
                            (int)SkyMode3D.Procedural,
                            (int)SkyMode3D.EnvironmentMap),

                    EnvironmentMapReference =
                        environmentMapReference,

                    EnvironmentIntensity =
                        ReadFloat(
                            data,
                            "environmentIntensity",
                            1.0f),

                    EnvironmentRotationDegrees =
                        ReadFloat(
                            data,
                            "environmentRotationDegrees",
                            0.0f),

                    EnvironmentLightingEnabled =
                        data.Properties["environmentLightingEnabled"]?
                            .GetValue<bool>() ??
                        true,

                    Exposure =
                        ReadFloat(
                            data,
                            "exposure",
                            1.0f),

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
                            0.20f),

                    FogEnabled =
                        data.Properties["fogEnabled"]?
                            .GetValue<bool>() ??
                        false,

                    FogMode =
                        (FogMode3D)Math.Clamp(
                            ReadInt(
                                data,
                                "fogMode",
                                (int)FogMode3D.Linear),
                            (int)FogMode3D.Linear,
                            (int)FogMode3D.Exponential),

                    FogColor =
                        ReadVector3(
                            data.Properties["fogColor"],
                            new Vector3(
                                0.58f,
                                0.72f,
                                0.95f)),

                    FogStartDistance =
                        ReadFloat(
                            data,
                            "fogStartDistance",
                            20.0f),

                    FogEndDistance =
                        ReadFloat(
                            data,
                            "fogEndDistance",
                            100.0f),

                    FogDensity =
                        ReadFloat(
                            data,
                            "fogDensity",
                            0.025f),

                    FogMaxOpacity =
                        ReadFloat(
                            data,
                            "fogMaxOpacity",
                            1.0f)
                };

            if (!environmentMapReference.IsEmpty)
            {
                environment.SetEnvironmentMapTexture(
                    context.Assets.LoadTexture(
                        environmentMapReference));
            }

            return environment;
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
    }
}
