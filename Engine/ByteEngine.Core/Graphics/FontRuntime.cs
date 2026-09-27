using ByteEngine.Core.Assets;

namespace ByteEngine.Core.Graphics;

internal static class FontRuntime
{
    private static WeakReference<AssetDatabase>? _database;

    internal static void Configure(AssetDatabase database)
    {
        _database = new WeakReference<AssetDatabase>(database);
    }

    internal static string? ResolvePath(AssetReference? reference)
    {
        if (reference == null || reference.IsEmpty) return null;
        if (_database?.TryGetTarget(out AssetDatabase? database) == true)
        {
            AssetRecord? asset = database.Resolve(reference);
            if (asset?.Type == AssetType.Font) return asset.FullPath;
        }
        return reference.CachedProjectPath;
    }
}
