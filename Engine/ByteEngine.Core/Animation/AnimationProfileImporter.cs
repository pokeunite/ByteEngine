using ByteEngine.Core.Assets;

namespace ByteEngine.Core.Animation;

/// <summary>
/// Asset importer used by editor/runtime asset services once .byteanim is wired
/// into the AssetDatabase in the next C8 slice.
/// </summary>
public sealed class AnimationProfileImporter :
    AssetImporter<AnimationProfile>
{
    public override AnimationProfile Import(
        AssetRecord asset)
    {
        ArgumentNullException.ThrowIfNull(
            asset);

        return AnimationProfileSerializer.Load(
            asset.FullPath);
    }
}
