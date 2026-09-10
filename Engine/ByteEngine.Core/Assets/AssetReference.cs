namespace ByteEngine.Core.Assets;

public sealed record AssetReference
{
    public static AssetReference Empty { get; } = new(Guid.Empty);

    public Guid Guid { get; }

    public string? CachedProjectPath { get; }

    // Kept for source compatibility with v0.2. It is no longer the identity.
    public string ProjectPath => CachedProjectPath ?? string.Empty;

    public bool IsEmpty => Guid == Guid.Empty && string.IsNullOrWhiteSpace(CachedProjectPath);

    public AssetReference(Guid guid, string? cachedProjectPath = null)
    {
        Guid = guid;
        CachedProjectPath = Normalize(cachedProjectPath);
    }

    public AssetReference(string legacyProjectPath)
    {
        if (string.IsNullOrWhiteSpace(legacyProjectPath) || Path.IsPathRooted(legacyProjectPath))
        {
            throw new ArgumentException("Asset paths must be non-empty and project-relative.", nameof(legacyProjectPath));
        }

        Guid = Guid.Empty;
        CachedProjectPath = Normalize(legacyProjectPath);
    }

    public override string ToString() =>
        Guid != Guid.Empty ? Guid.ToString() : CachedProjectPath ?? "(empty)";

    private static string? Normalize(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : path.Replace('\\', '/').TrimStart('/');
}
