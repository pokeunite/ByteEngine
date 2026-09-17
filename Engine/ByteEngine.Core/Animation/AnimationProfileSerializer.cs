using System.Text.Json;
using System.Text.Json.Serialization;

using ByteEngine.Core.Assets;

namespace ByteEngine.Core.Animation;

/// <summary>
/// JSON persistence for .byteanim Animation Profile assets.
/// </summary>
public static class AnimationProfileSerializer
{
    public const string FileExtension = ".byteanim";

    private static readonly JsonSerializerOptions Json =
        CreateOptions();

    public static AnimationProfile Load(
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            path);

        string json =
            File.ReadAllText(
                path);

        AnimationProfile? profile =
            JsonSerializer.Deserialize<AnimationProfile>(
                json,
                Json);

        if (profile == null)
        {
            throw new InvalidDataException(
                $"Animation profile '{path}' did not contain a valid profile.");
        }

        profile.Normalize();

        return profile;
    }

    public static void Save(
        string path,
        AnimationProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            path);

        ArgumentNullException.ThrowIfNull(
            profile);

        profile.Normalize();

        string? directory =
            Path.GetDirectoryName(
                Path.GetFullPath(path));

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(
                directory);
        }

        string temporary =
            path +
            ".tmp";

        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(
                profile,
                Json));

        File.Move(
            temporary,
            path,
            true);
    }

    public static AnimationProfile CreateDefault(
        string? name = null)
    {
        var profile =
            new AnimationProfile();

        if (!string.IsNullOrWhiteSpace(name))
        {
            profile.Name =
                name.Trim();
        }

        profile.Normalize();

        return profile;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options =
            new JsonSerializerOptions
            {
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive =
                    true,
                WriteIndented =
                    true,
                AllowTrailingCommas =
                    true,
                ReadCommentHandling =
                    JsonCommentHandling.Skip
            };

        options.Converters.Add(
            new JsonStringEnumConverter());

        options.Converters.Add(
            new AssetReferenceJsonConverter());

        return options;
    }

    /// <summary>
    /// AssetReference is intentionally a small immutable reference type and is
    /// not directly constructible by System.Text.Json. Keep .byteanim files
    /// human-readable while explicitly rebuilding the reference during load.
    /// </summary>
    private sealed class AssetReferenceJsonConverter
        : JsonConverter<AssetReference>
    {
        public override AssetReference Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return AssetReference.Empty;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(
                    "Animation Profile asset reference must be a JSON object.");
            }

            Guid guid =
                Guid.Empty;

            string? cachedProjectPath =
                null;

            while (reader.Read())
            {
                if (reader.TokenType ==
                    JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType !=
                    JsonTokenType.PropertyName)
                {
                    continue;
                }

                string? propertyName =
                    reader.GetString();

                if (!reader.Read())
                {
                    throw new JsonException(
                        "Unexpected end of Animation Profile asset reference.");
                }

                if (string.Equals(
                        propertyName,
                        "guid",
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType ==
                        JsonTokenType.String)
                    {
                        Guid.TryParse(
                            reader.GetString(),
                            out guid);
                    }

                    continue;
                }

                if (string.Equals(
                        propertyName,
                        "cachedProjectPath",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        propertyName,
                        "path",
                        StringComparison.OrdinalIgnoreCase))
                {
                    cachedProjectPath =
                        reader.TokenType ==
                            JsonTokenType.Null
                            ? null
                            : reader.GetString();

                    continue;
                }

                using JsonDocument ignored =
                    JsonDocument.ParseValue(
                        ref reader);
            }

            if (guid == Guid.Empty &&
                string.IsNullOrWhiteSpace(
                    cachedProjectPath))
            {
                return AssetReference.Empty;
            }

            return new AssetReference(
                guid,
                cachedProjectPath);
        }

        public override void Write(
            Utf8JsonWriter writer,
            AssetReference value,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString(
                "guid",
                value?.Guid ?? Guid.Empty);

            if (string.IsNullOrWhiteSpace(
                    value?.CachedProjectPath))
            {
                writer.WriteNull(
                    "cachedProjectPath");
            }
            else
            {
                writer.WriteString(
                    "cachedProjectPath",
                    value.CachedProjectPath);
            }

            writer.WriteEndObject();
        }
    }
}
