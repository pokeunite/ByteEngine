using System.Numerics;
using System.Text.Json.Nodes;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Core.Animation;

/// <summary>
/// Richer v0.11-C animation persistence. These codecs intentionally use the
/// existing component type names so older scenes remain compatible.
/// </summary>
public static class AnimationSerializationRegistrar
{
    public static void Register(ComponentSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);

        serializer.Register(new AnimationControllerCodec());
        serializer.Register(new SkeletalMeshRendererCodec());
        serializer.Register(new BoneSocket3DCodec());
    }

    private sealed class AnimationControllerCodec : IComponentCodec
    {
        public string TypeName => "AnimationController";
        public Type ComponentType => typeof(AnimationController);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            AnimationController controller =
                (AnimationController)component;

            return
                new ComponentData
                {
                    Type = TypeName,
                    Properties =
                        new JsonObject
                        {
                            ["animationProfileGuid"] =
                                controller.AnimationProfile.Guid.ToString(),
                            ["animationProfilePath"] =
                                controller.AnimationProfile.CachedProjectPath,
                            ["idle"] = controller.Idle,
                            ["walk"] = controller.Walk,
                            ["run"] = controller.Run,
                            ["jump"] = controller.Jump,
                            ["fall"] = controller.Fall,
                            ["land"] = controller.Land,
                            ["runThreshold"] = controller.RunThreshold,
                            ["driveLocomotion"] = controller.DriveLocomotion,
                            ["transitionDuration"] =
                                controller.TransitionDuration,
                            ["playbackSpeed"] =
                                controller.PlaybackSpeed,
                            ["rootMotionMode"] =
                                controller.RootMotionMode.ToString()
                        }
                };
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            AssetReference profile =
                ReadAssetReference(
                    data,
                    context,
                    "animationProfileGuid",
                    "animationProfilePath");

            return
                new AnimationController
                {
                    AnimationProfile = profile,
                    Idle = Text(data, "idle", "Idle"),
                    Walk = Text(data, "walk", "Walk"),
                    Run = Text(data, "run", "Run"),
                    Jump = Text(data, "jump", "Jump"),
                    Fall = Text(data, "fall", "Fall"),
                    Land = Text(data, "land", "Land"),
                    RunThreshold =
                        Float(data, "runThreshold", 4.0f),
                    DriveLocomotion =
                        data.Properties["driveLocomotion"]?
                            .GetValue<bool>() ??
                        true,
                    TransitionDuration =
                        Float(
                            data,
                            "transitionDuration",
                            0.15f),
                    PlaybackSpeed =
                        Float(
                            data,
                            "playbackSpeed",
                            1.0f),
                    RootMotionMode =
                        ReadRootMotionMode(data)
                };
        }
    }

    private sealed class SkeletalMeshRendererCodec : IComponentCodec
    {
        public string TypeName => "SkeletalMeshRenderer";
        public Type ComponentType => typeof(SkeletalMeshRenderer);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            SkeletalMeshRenderer renderer =
                (SkeletalMeshRenderer)component;

            return
                new ComponentData
                {
                    Type = TypeName,
                    Properties =
                        new JsonObject
                        {
                            ["modelGuid"] =
                                renderer.Model.Guid.ToString(),
                            ["modelPath"] =
                                renderer.Model.CachedProjectPath,
                            ["skeletonKey"] =
                                renderer.SkeletonKey,
                            ["materialKeys"] =
                                new JsonArray(
                                    renderer.MaterialKeys
                                        .Select(
                                            key =>
                                                (JsonNode?)JsonValue.Create(key))
                                        .ToArray()),
                            ["visible"] =
                                renderer.Visible,
                            ["defaultAnimation"] =
                                renderer.DefaultAnimation,
                            ["playOnStart"] =
                                renderer.PlayOnStart,
                            ["loop"] =
                                renderer.Loop,
                            ["speed"] =
                                renderer.Speed,
                            ["transitionDuration"] =
                                renderer.TransitionDuration
                        }
                };
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            Guid.TryParse(
                data.Properties["modelGuid"]?
                    .GetValue<string>(),
                out Guid modelGuid);

            string? modelPath =
                data.Properties["modelPath"]?
                    .GetValue<string>();

            AssetReference modelReference =
                modelGuid != Guid.Empty
                    ? new AssetReference(
                        modelGuid,
                        modelPath)
                    : !string.IsNullOrWhiteSpace(modelPath)
                        ? context.AssetDatabase.ResolveReference(modelPath)
                        : AssetReference.Empty;

            List<string> materials =
                data.Properties["materialKeys"] is JsonArray array
                    ? array
                        .Select(node => node?.GetValue<string>())
                        .Where(value => value != null)
                        .Cast<string>()
                        .ToList()
                    : new List<string>();

            return
                new SkeletalMeshRenderer
                {
                    Model = modelReference,
                    SkeletonKey =
                        data.Properties["skeletonKey"]?
                            .GetValue<string>(),
                    MaterialKeys = materials,
                    Visible =
                        data.Properties["visible"]?
                            .GetValue<bool>() ??
                        true,
                    DefaultAnimation =
                        Text(
                            data,
                            "defaultAnimation",
                            string.Empty),
                    PlayOnStart =
                        data.Properties["playOnStart"]?
                            .GetValue<bool>() ??
                        false,
                    Loop =
                        data.Properties["loop"]?
                            .GetValue<bool>() ??
                        true,
                    Speed =
                        Float(data, "speed", 1.0f),
                    TransitionDuration =
                        Float(
                            data,
                            "transitionDuration",
                            0.15f)
                };
        }
    }

    private sealed class BoneSocket3DCodec : IComponentCodec
    {
        public string TypeName => "BoneSocket3D";
        public Type ComponentType => typeof(BoneSocket3D);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            BoneSocket3D socket =
                (BoneSocket3D)component;

            return
                new ComponentData
                {
                    Type = TypeName,
                    Properties =
                        new JsonObject
                        {
                            ["boneName"] = socket.BoneName,
                            ["positionOffset"] = Vector3Node(socket.PositionOffset),
                            ["rotationOffsetDegrees"] = Vector3Node(socket.RotationOffsetDegrees),
                            ["scaleMultiplier"] = Vector3Node(socket.ScaleMultiplier),
                            ["inheritBoneScale"] = socket.InheritBoneScale
                        }
                };
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context) =>
            new BoneSocket3D
            {
                BoneName =
                    Text(data, "boneName", string.Empty),
                PositionOffset =
                    ReadVector3(
                        data.Properties["positionOffset"],
                        Vector3.Zero),
                RotationOffsetDegrees =
                    ReadVector3(
                        data.Properties["rotationOffsetDegrees"],
                        Vector3.Zero),
                ScaleMultiplier =
                    ReadVector3(
                        data.Properties["scaleMultiplier"],
                        Vector3.One),
                InheritBoneScale =
                    data.Properties["inheritBoneScale"]?
                        .GetValue<bool>() ??
                    false
            };
    }

    private static AssetReference ReadAssetReference(
        ComponentData data,
        ComponentSerializationContext context,
        string guidKey,
        string pathKey)
    {
        Guid.TryParse(
            data.Properties[guidKey]?
                .GetValue<string>(),
            out Guid guid);

        string? path =
            data.Properties[pathKey]?
                .GetValue<string>();

        if (guid != Guid.Empty)
        {
            return new AssetReference(
                guid,
                path);
        }

        if (!string.IsNullOrWhiteSpace(path))
        {
            return context.AssetDatabase.ResolveReference(
                path);
        }

        return AssetReference.Empty;
    }

    private static JsonArray Vector3Node(Vector3 value) =>
        new(
            value.X,
            value.Y,
            value.Z);

    private static Vector3 ReadVector3(
        JsonNode? node,
        Vector3 fallback)
    {
        if (node is not JsonArray array ||
            array.Count < 3)
        {
            return fallback;
        }

        return new Vector3(
            array[0]?.GetValue<float>() ?? fallback.X,
            array[1]?.GetValue<float>() ?? fallback.Y,
            array[2]?.GetValue<float>() ?? fallback.Z);
    }

    private static string Text(
        ComponentData data,
        string key,
        string fallback) =>
        data.Properties[key]?
            .GetValue<string>() ??
        fallback;

    private static float Float(
        ComponentData data,
        string key,
        float fallback) =>
        data.Properties[key]?
            .GetValue<float>() ??
        fallback;

    private static RootMotionMode ReadRootMotionMode(
        ComponentData data)
    {
        string? value =
            data.Properties["rootMotionMode"]?
                .GetValue<string>();

        return Enum.TryParse(
                value,
                ignoreCase: true,
                out RootMotionMode mode)
            ? mode
            : RootMotionMode.InPlace;
    }
}
