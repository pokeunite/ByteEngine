using System.Text.Json;
using ByteEngine.Core.Serialization;

namespace ByteEngine.Core.Blueprints;

public sealed class BlueprintSerializer
{
    public void Save(BlueprintDefinition blueprint, string path)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        if (blueprint.Id == Guid.Empty) blueprint.Id = Guid.NewGuid();
        JsonSerialization.WriteAtomic(path, blueprint);
    }

    public BlueprintDefinition Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Blueprint asset was not found.", path);
        try
        {
            BlueprintDefinition blueprint = JsonSerializer.Deserialize<BlueprintDefinition>(
                File.ReadAllText(path), JsonSerialization.Options)
                ?? throw new InvalidDataException("Blueprint contained no data.");
            if (blueprint.Id == Guid.Empty) blueprint.Id = Guid.NewGuid();
            return blueprint;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Invalid Byte Blueprint '{path}': {exception.Message}", exception);
        }
    }
}
