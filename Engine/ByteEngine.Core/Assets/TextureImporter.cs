using ByteEngine.Core.Graphics;

namespace ByteEngine.Core.Assets;

public sealed class TextureImporter : AssetImporter<Texture2D>
{
    public override Texture2D Import(AssetRecord asset)
    {
        if (asset.Type != AssetType.Texture2D)
        {
            throw new InvalidOperationException($"Asset '{asset.ProjectPath}' is not a texture.");
        }

        return new Texture2D(asset.FullPath, asset.Metadata.Importer.Filter);
    }
}
