using System.Text.Json;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Serialization;

namespace ByteEngine.Core.Assets;

/// <summary>Portable imported-model data. Cooking happens on the desktop, never in WASM.</summary>
public static class CookedModelStore
{
    public static string PathFor(string root, Guid id) =>
        Path.Combine(root, ".byteengine", "WebModels", id.ToString("N") + ".json");
    public static void Save(string root, ImportedModel model)
    {
        if (model.Guid == Guid.Empty) throw new InvalidDataException("Cooked model has no GUID.");
        JsonSerialization.WriteAtomic(PathFor(root, model.Guid), model);
    }
    public static ImportedModel Load(string root, Guid id)
    {
        var model = JsonSerializer.Deserialize<ImportedModel>(File.ReadAllText(PathFor(root, id)), JsonSerialization.Options)
            ?? throw new InvalidDataException("Invalid cooked model.");
        if (model.Guid != id) throw new InvalidDataException("Cooked model GUID mismatch.");
        return model;
    }
}
