using System.Text.Json;
using System.Text.Json.Serialization;

using ByteEngine.Core.Assets;

namespace ByteEngine.Core.Animation;

/// <summary>
/// JSON persistence for .byteanim Animation Profile assets.
///
/// Important: the on-disk document deliberately does not deserialize
/// AssetReference directly. AssetReference is an immutable runtime type with
/// multiple constructors and has changed over ByteEngine's lifetime. The DTO
/// layer below keeps .byteanim files stable and reconstructs AssetReference
/// explicitly after JSON parsing.
/// </summary>
public static class AnimationProfileSerializer
{
    public const string FileExtension =
        ".byteanim";

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

        AnimationProfileDocument? document =
            JsonSerializer.Deserialize<AnimationProfileDocument>(
                json,
                Json);

        if (document == null)
        {
            throw new InvalidDataException(
                $"Animation profile '{path}' did not contain a valid profile.");
        }

        AnimationProfile profile =
            FromDocument(
                document);

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
                Path.GetFullPath(
                    path));

        if (!string.IsNullOrWhiteSpace(
                directory))
        {
            Directory.CreateDirectory(
                directory);
        }

        string temporary =
            path +
            ".tmp";

        AnimationProfileDocument document =
            ToDocument(
                profile);

        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(
                document,
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

        if (!string.IsNullOrWhiteSpace(
                name))
        {
            profile.Name =
                name.Trim();
        }

        profile.Normalize();

        return profile;
    }

    private static AnimationProfile FromDocument(
        AnimationProfileDocument document)
    {
        AnimationRigDocument rigDocument =
            document.Rig ??
            new AnimationRigDocument();

        AssetReference referenceModel =
            ToAssetReference(
                rigDocument.ReferenceModel);

        return
            new AnimationProfile
            {
                Version =
                    Math.Max(
                        document.Version,
                        1),

                Name =
                    document.Name ??
                    "Animation Profile",

                Rig =
                    new AnimationRigProfile
                    {
                        Type =
                            rigDocument.Type,

                        ReferenceModel =
                            referenceModel
                    },

                Locomotion =
                    document.Locomotion ??
                    new AnimationLocomotionProfile(),

                Actions =
                    document.Actions ??
                    new List<AnimationActionProfile>(),

                Procedural =
                    document.Procedural ??
                    new AnimationProceduralProfile()
            };
    }

    private static AnimationProfileDocument ToDocument(
        AnimationProfile profile)
    {
        return
            new AnimationProfileDocument
            {
                Version =
                    profile.Version,

                Name =
                    profile.Name,

                Rig =
                    new AnimationRigDocument
                    {
                        Type =
                            profile.Rig.Type,

                        ReferenceModel =
                            FromAssetReference(
                                profile.Rig.ReferenceModel)
                    },

                Locomotion =
                    profile.Locomotion,

                Actions =
                    profile.Actions,

                Procedural =
                    profile.Procedural
            };
    }

    private static AssetReference ToAssetReference(
        AssetReferenceDocument? document)
    {
        if (document == null)
        {
            return AssetReference.Empty;
        }

        string? path =
            FirstNonEmpty(
                document.CachedProjectPath,
                document.ProjectPath,
                document.Path);

        if (document.Guid ==
                Guid.Empty &&
            string.IsNullOrWhiteSpace(
                path))
        {
            return AssetReference.Empty;
        }

        return
            new AssetReference(
                document.Guid,
                path);
    }

    private static AssetReferenceDocument FromAssetReference(
        AssetReference? reference)
    {
        reference ??=
            AssetReference.Empty;

        return
            new AssetReferenceDocument
            {
                Guid =
                    reference.Guid,

                CachedProjectPath =
                    reference.CachedProjectPath
            };
    }

    private static string? FirstNonEmpty(
        params string?[] values)
    {
        foreach (string? value
                 in values)
        {
            if (!string.IsNullOrWhiteSpace(
                    value))
            {
                return value;
            }
        }

        return null;
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

        return options;
    }

    /// <summary>
    /// Stable on-disk representation of an Animation Profile.
    /// </summary>
    private sealed class AnimationProfileDocument
    {
        public int Version { get; set; } =
            AnimationProfile.CurrentVersion;

        public string? Name { get; set; } =
            "Animation Profile";

        public AnimationRigDocument? Rig { get; set; } =
            new();

        public AnimationLocomotionProfile? Locomotion { get; set; } =
            new();

        public List<AnimationActionProfile>? Actions { get; set; } =
            new();

        public AnimationProceduralProfile? Procedural { get; set; } =
            new();
    }

    /// <summary>
    /// Rig DTO exists specifically so JSON never tries to instantiate the
    /// runtime AssetReference type.
    /// </summary>
    private sealed class AnimationRigDocument
    {
        public AnimationRigType Type { get; set; } =
            AnimationRigType.Generic;

        public AssetReferenceDocument? ReferenceModel { get; set; } =
            new();
    }

    /// <summary>
    /// Accepts both the current "cachedProjectPath" spelling and older
    /// "projectPath"/"path" spellings when loading existing .byteanim files.
    /// </summary>
    private sealed class AssetReferenceDocument
    {
        public Guid Guid { get; set; }

        public string? CachedProjectPath { get; set; }

        public string? ProjectPath { get; set; }

        public string? Path { get; set; }
    }
}
