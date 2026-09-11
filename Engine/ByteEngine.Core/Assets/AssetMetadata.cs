using ByteEngine.Core.Graphics;

namespace ByteEngine.Core.Assets;

public sealed class AssetMetadata
{
    public Guid Guid { get; set; }

    public AssetType Type { get; set; }

    public TextureImporterSettings Importer { get; set; } = new();

    public Importers.ModelImporterSettings ModelImporter { get; set; } = new();
}

public sealed class TextureImporterSettings
{
    public TextureFilter Filter { get; set; } = TextureFilter.Nearest;
}

public sealed record AssetRecord(
    Guid Guid,
    AssetType Type,
    string ProjectPath,
    string FullPath,
    string MetaPath,
    AssetMetadata Metadata);
