using System.Text.Json.Serialization;

namespace ByteEngine.Core.Assets;

public sealed record AssetReference
{
    public static AssetReference Empty { get; } =
        new(Guid.Empty);

    public Guid Guid { get; }

    public string? CachedProjectPath { get; }

    // Kept for source compatibility with v0.2. It is no longer the identity.
    public string ProjectPath =>
        CachedProjectPath ??
        string.Empty;

    public bool IsEmpty =>
        Guid == Guid.Empty &&
        string.IsNullOrWhiteSpace(
            CachedProjectPath);

    /// <summary>
    /// Canonical serialized constructor.
    ///
    /// AssetReference has two public constructors, so System.Text.Json cannot
    /// choose one automatically. Marking the GUID/path constructor explicitly
    /// makes AssetReference safe inside AnimationProfile and any other JSON
    /// payload without requiring every serializer to install a custom converter.
    /// </summary>
    [JsonConstructor]
    public AssetReference(
        Guid guid,
        string? cachedProjectPath = null)
    {
        Guid =
            guid;

        CachedProjectPath =
            Normalize(
                cachedProjectPath);
    }

    public AssetReference(
        string legacyProjectPath)
    {
        if (string.IsNullOrWhiteSpace(
                legacyProjectPath) ||
            Path.IsPathRooted(
                legacyProjectPath))
        {
            throw new ArgumentException(
                "Asset paths must be non-empty and project-relative.",
                nameof(legacyProjectPath));
        }

        Guid =
            Guid.Empty;

        CachedProjectPath =
            Normalize(
                legacyProjectPath);
    }

    public override string ToString() =>
        Guid != Guid.Empty
            ? Guid.ToString()
            : CachedProjectPath ??
              "(empty)";

    private static string? Normalize(
        string? path) =>
        string.IsNullOrWhiteSpace(
            path)
            ? null
            : path
                .Replace(
                    '\\',
                    '/')
                .TrimStart(
                    '/');
}
