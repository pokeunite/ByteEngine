using System.Text.Json;
using System.Text.Json.Nodes;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Serialization;

namespace ByteEngine.Core.Blueprints;

public sealed class BlueprintSerializer
{
    private readonly AssetDatabase? _database;
    public BlueprintSerializer(AssetDatabase? database = null) => _database = database;
    public BlueprintDefinition CreateVariant(AssetReference source, string name)
    {
        var record = _database?.Resolve(source) ?? throw new InvalidOperationException("A variant requires a resolved base asset.");
        var parent = Load(record.FullPath);
        string baseline = JsonSerializer.Serialize(parent, JsonSerialization.Options);
        var variant = JsonSerializer.Deserialize<BlueprintDefinition>(baseline, JsonSerialization.Options)!;
        variant.Id = Guid.NewGuid(); variant.Name = name; variant.BaseBlueprint = source; variant.BaseSnapshot = baseline;
        return variant;
    }

    public void Save(BlueprintDefinition blueprint, string path)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        if (blueprint.Id == Guid.Empty) blueprint.Id = Guid.NewGuid();
        JsonSerialization.WriteAtomic(path, blueprint);
    }

    public BlueprintDefinition Load(string path) => LoadInherited(Path.GetFullPath(path), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    private BlueprintDefinition LoadInherited(string path, HashSet<string> visited)
    {
        if (visited.Count >= 32 || !visited.Add(path)) throw new InvalidDataException("Blueprint inheritance contains a cycle or exceeds 32 levels.");
        if (!File.Exists(path)) throw new FileNotFoundException("Blueprint asset was not found.", path);
        try
        {
            BlueprintDefinition blueprint = JsonSerializer.Deserialize<BlueprintDefinition>(
                File.ReadAllText(path), JsonSerialization.Options)
                ?? throw new InvalidDataException("Blueprint contained no data.");
            if (blueprint.Id == Guid.Empty) blueprint.Id = Guid.NewGuid();
            if (!blueprint.BaseBlueprint.IsEmpty)
            {
                var record = _database?.Resolve(blueprint.BaseBlueprint) ?? throw new InvalidDataException("Blueprint base asset cannot be resolved. Load variants with the project AssetDatabase.");
                var parent = LoadInherited(record.FullPath, visited);
                if (string.IsNullOrWhiteSpace(blueprint.BaseSnapshot)) throw new InvalidDataException("Blueprint variant has no base snapshot.");
                var baseline = JsonNode.Parse(blueprint.BaseSnapshot);
                var local = JsonSerializer.SerializeToNode(blueprint, JsonSerialization.Options);
                var latest = JsonSerializer.SerializeToNode(parent, JsonSerialization.Options);
                var conflicts = new List<string>();
                var merged = Merge(baseline, local, latest, "", conflicts)!;
                var resolved = merged.Deserialize<BlueprintDefinition>(JsonSerialization.Options)!;
                resolved.Id = blueprint.Id; resolved.Name = blueprint.Name; resolved.BaseBlueprint = blueprint.BaseBlueprint;
                resolved.BaseSnapshot = JsonSerializer.Serialize(parent, JsonSerialization.Options);
                resolved.InheritanceConflicts.AddRange(conflicts.Where(p => !p.StartsWith("/id") && !p.StartsWith("/name") && !p.StartsWith("/base")));
                blueprint = resolved;
                var own = _database?.Assets.FirstOrDefault(a => string.Equals(a.FullPath,path,StringComparison.OrdinalIgnoreCase));
                if(own != null) _database!.Dependencies.SetDependencies(own.Guid,[record.Guid]);
            }
            return blueprint;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Invalid Byte Blueprint '{path}': {exception.Message}", exception);
        }
    }
    private static JsonNode? Merge(JsonNode? baseline, JsonNode? local, JsonNode? latest, string path, List<string> conflicts)
    {
        if (JsonNode.DeepEquals(baseline, local)) return latest?.DeepClone();
        if (JsonNode.DeepEquals(baseline, latest) || JsonNode.DeepEquals(local, latest)) return local?.DeepClone();
        if (baseline is JsonObject b && local is JsonObject l && latest is JsonObject r)
        {
            var result=new JsonObject();
            foreach(string key in b.Select(p=>p.Key).Union(l.Select(p=>p.Key)).Union(r.Select(p=>p.Key)))
            {
                bool hasB=b.ContainsKey(key),hasL=l.ContainsKey(key),hasR=r.ContainsKey(key);
                var value=Merge(b[key],l[key],r[key],path+"/"+key,conflicts);
                if(value!=null || hasL && (!hasB || JsonNode.DeepEquals(b[key],r[key])) || !hasL && !hasB && hasR) result[key]=value;
            }
            return result;
        }
        if(baseline is JsonArray ba && local is JsonArray la && latest is JsonArray ra)
        {
            Dictionary<string,JsonNode?>? Map(JsonArray array)
            {
                var map=new Dictionary<string,JsonNode?>();var counts=new Dictionary<string,int>();
                foreach(var item in array)
                {
                    if(item is not JsonObject obj)return null;
                    string? key=obj["id"]?.ToString() ?? obj["type"]?.ToString() ?? obj["name"]?.ToString();
                    if(key==null)return null;
                    int occurrence=counts.GetValueOrDefault(key);counts[key]=occurrence+1;
                    map[key+":"+occurrence]=item;
                }
                return map;
            }
            var bm=Map(ba);var lm=Map(la);var rm=Map(ra);
            if(bm!=null&&lm!=null&&rm!=null)
            {
                var result=new JsonArray();
                foreach(var key in rm.Keys.Union(lm.Keys))
                {
                    var value=Merge(bm.GetValueOrDefault(key),lm.GetValueOrDefault(key),rm.GetValueOrDefault(key),path+"/"+key,conflicts);
                    if(value!=null)result.Add(value);
                }
                return result;
            }
        }
        conflicts.Add(path);return local?.DeepClone();
    }

}
