namespace ByteEngine.Core.Assets;

public abstract class AssetImporter<TResource>
{
    public abstract TResource Import(AssetRecord asset);
}
