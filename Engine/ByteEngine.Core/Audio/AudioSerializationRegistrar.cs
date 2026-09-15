using System.Text.Json.Nodes;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Core.Audio;

/// <summary>
/// Scene/Blueprint persistence for ByteEngine audio components.
/// </summary>
public static class AudioSerializationRegistrar
{
    public static void Register(
        ComponentSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(
            serializer);

        serializer.Register(
            new AudioSource3DCodec());

        serializer.Register(
            new AudioListener3DCodec());
    }

    private sealed class AudioSource3DCodec
        : IComponentCodec
    {
        public string TypeName =>
            "AudioSource3D";

        public Type ComponentType =>
            typeof(AudioSource3D);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            AudioSource3D source =
                (AudioSource3D)component;

            return
                new ComponentData
                {
                    Type =
                        TypeName,

                    Properties =
                        new JsonObject
                        {
                            ["clip"] =
                                WriteReference(
                                    source.ClipReference),

                            ["playOnStart"] =
                                source.PlayOnStart,

                            ["loop"] =
                                source.Loop,

                            ["spatial"] =
                                source.Spatial,

                            ["volume"] =
                                source.Volume,

                            ["pitch"] =
                                source.Pitch,

                            ["minDistance"] =
                                source.MinDistance,

                            ["maxDistance"] =
                                source.MaxDistance,

                            ["rolloffFactor"] =
                                source.RolloffFactor
                        }
                };
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            AssetReference reference =
                ReadReference(
                    data.Properties["clip"],
                    context.AssetDatabase);

            var source =
                new AudioSource3D
                {
                    ClipReference =
                        reference,

                    PlayOnStart =
                        data.Properties["playOnStart"]?
                            .GetValue<bool>() ??
                        false,

                    Loop =
                        data.Properties["loop"]?
                            .GetValue<bool>() ??
                        false,

                    Spatial =
                        data.Properties["spatial"]?
                            .GetValue<bool>() ??
                        true,

                    Volume =
                        ReadFloat(
                            data,
                            "volume",
                            1.0f),

                    Pitch =
                        ReadFloat(
                            data,
                            "pitch",
                            1.0f),

                    MinDistance =
                        ReadFloat(
                            data,
                            "minDistance",
                            1.0f),

                    MaxDistance =
                        ReadFloat(
                            data,
                            "maxDistance",
                            100.0f),

                    RolloffFactor =
                        ReadFloat(
                            data,
                            "rolloffFactor",
                            1.0f)
                };

            LoadClip(
                source,
                reference,
                context);

            return source;
        }
    }

    private sealed class AudioListener3DCodec
        : IComponentCodec
    {
        public string TypeName =>
            "AudioListener3D";

        public Type ComponentType =>
            typeof(AudioListener3D);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            AudioListener3D listener =
                (AudioListener3D)component;

            return
                new ComponentData
                {
                    Type =
                        TypeName,

                    Properties =
                        new JsonObject
                        {
                            ["volume"] =
                                listener.Volume
                        }
                };
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            return
                new AudioListener3D
                {
                    Volume =
                        ReadFloat(
                            data,
                            "volume",
                            1.0f)
                };
        }
    }

    public static bool TryLoadClip(
        AudioSource3D source,
        AssetReference reference,
        AssetDatabase database,
        Action<string>? warningSink = null)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        ArgumentNullException.ThrowIfNull(
            reference);

        ArgumentNullException.ThrowIfNull(
            database);

        source.ClipReference =
            reference;

        if (reference.IsEmpty)
        {
            source.SetClip(
                null);

            return true;
        }

        AssetRecord? asset =
            database.Resolve(
                reference);

        if (asset ==
            null)
        {
            source.SetClip(
                null);

            warningSink?.Invoke(
                $"Audio clip '{reference}' could not be resolved.");

            return false;
        }

        if (!Path.GetExtension(
                asset.FullPath)
            .Equals(
                ".wav",
                StringComparison.OrdinalIgnoreCase))
        {
            source.SetClip(
                null);

            warningSink?.Invoke(
                $"Audio v0.11-A supports PCM WAV only: {asset.ProjectPath}");

            return false;
        }

        if (!AudioClip.TryLoadWave(
                asset.FullPath,
                out AudioClip? clip,
                warningSink))
        {
            source.SetClip(
                null);

            return false;
        }

        source.SetClip(
            clip);

        return true;
    }

    private static void LoadClip(
        AudioSource3D source,
        AssetReference reference,
        ComponentSerializationContext context)
    {
        TryLoadClip(
            source,
            reference,
            context.AssetDatabase,
            context.WarningSink);
    }

    private static JsonObject WriteReference(
        AssetReference reference)
    {
        var node =
            new JsonObject();

        if (reference.Guid !=
            Guid.Empty)
        {
            node["guid"] =
                reference.Guid.ToString();
        }

        if (!string.IsNullOrWhiteSpace(
                reference.CachedProjectPath))
        {
            node["path"] =
                reference.CachedProjectPath;
        }

        return node;
    }

    private static AssetReference ReadReference(
        JsonNode? node,
        AssetDatabase database)
    {
        if (node is
            JsonObject value)
        {
            string? guidText =
                value["guid"]?
                    .GetValue<string>();

            string? path =
                value["path"]?
                    .GetValue<string>();

            if (Guid.TryParse(
                    guidText,
                    out Guid guid))
            {
                return
                    new AssetReference(
                        guid,
                        path);
            }

            if (!string.IsNullOrWhiteSpace(
                    path))
            {
                return
                    database.ResolveReference(
                        path);
            }
        }

        /*
         * Accept a legacy/simple string path if one is hand-authored.
         */
        if (node is
                JsonValue simple &&
            simple.TryGetValue(
                out string? simplePath) &&
            !string.IsNullOrWhiteSpace(
                simplePath))
        {
            return
                database.ResolveReference(
                    simplePath);
        }

        return
            AssetReference.Empty;
    }

    private static float ReadFloat(
        ComponentData data,
        string key,
        float fallback)
    {
        return
            data.Properties[key]?
                .GetValue<float>() ??
            fallback;
    }
}
