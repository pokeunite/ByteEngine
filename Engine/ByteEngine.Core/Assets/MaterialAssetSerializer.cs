using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ByteEngine.Core.Assets;

/// <summary>Human-readable, versioned .bmat persistence and single-parent resolution.</summary>
public static class MaterialAssetSerializer
{
    public const string FileExtension = ".bmat";
    private static readonly JsonSerializerOptions Json = CreateOptions();
    public static JsonElement SerializeOverrideValue<T>(T value) =>
        JsonSerializer.SerializeToElement(value, Json);


    public static MaterialAsset Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        MaterialDocument document = JsonSerializer.Deserialize<MaterialDocument>(
            File.ReadAllText(path), Json) ??
            throw new InvalidDataException($"Material '{path}' is empty.");
        MaterialAsset asset = new()
        {
            Version = document.Version,
            Kind = document.Kind,
            Name = document.Name,
            ParentMaterial = document.ParentMaterial ?? AssetReference.Empty,
            Standard = document.Standard ?? new MaterialParameters(),
            Overrides = document.Overrides ?? new Dictionary<string, JsonElement>()
        };
        if (asset.Version != MaterialAsset.CurrentVersion)
            throw new InvalidDataException(
                $"Unsupported material version {asset.Version} in '{path}'.");
        asset.Standard ??= new MaterialParameters();
        asset.Overrides ??= new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        asset.Overrides = new Dictionary<string, JsonElement>(
            asset.Overrides, StringComparer.OrdinalIgnoreCase);
        asset.ParentMaterial ??= AssetReference.Empty;
        if (asset.Kind == MaterialAssetKind.Instance && asset.ParentMaterial.IsEmpty)
            throw new InvalidDataException($"Material instance '{path}' has no parent.");
        foreach (string key in asset.Overrides.Keys)
            if (!MaterialParameters.KnownProperties.Contains(key))
                throw new InvalidDataException($"Unknown material override '{key}' in '{path}'.");
        asset.Standard.Normalize();
        return asset;
    }

    public static void Save(string path, MaterialAsset asset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(asset);
        if (asset.Kind == MaterialAssetKind.Instance && asset.ParentMaterial.IsEmpty)
            throw new InvalidDataException("A material instance needs a parent material.");
        asset.Version = MaterialAsset.CurrentVersion;
        asset.Standard.Normalize();
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporaryPath = fullPath + ".tmp";
        MaterialDocument document = new()
        {
            Version = asset.Version,
            Kind = asset.Kind,
            Name = asset.Name,
            ParentMaterial = asset.Kind == MaterialAssetKind.Instance
                ? asset.ParentMaterial : null,
            Standard = asset.Kind == MaterialAssetKind.Standard
                ? asset.Standard : null,
            Overrides = asset.Kind == MaterialAssetKind.Instance
                ? asset.Overrides : null
        };
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(document, Json));
        File.Move(temporaryPath, fullPath, true);
    }

    public static MaterialParameters Resolve(
        AssetReference reference,
        Func<AssetReference, MaterialAsset> load)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(load);
        return ResolveCore(reference, load, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private static MaterialParameters ResolveCore(
        AssetReference reference,
        Func<AssetReference, MaterialAsset> load,
        HashSet<string> visited)
    {
        string identity = reference.Guid != Guid.Empty
            ? reference.Guid.ToString("N")
            : reference.CachedProjectPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(identity))
            throw new InvalidDataException("Empty material reference.");
        if (!visited.Add(identity))
            throw new InvalidDataException($"Material inheritance cycle at '{identity}'.");
        try
        {
            MaterialAsset asset = load(reference);
            if (asset.Kind == MaterialAssetKind.Standard)
                return asset.Standard.Clone();

            MaterialParameters inherited = ResolveCore(asset.ParentMaterial, load, visited);
            JsonObject values = JsonSerializer.SerializeToNode(inherited, Json)!.AsObject();
            foreach ((string key, JsonElement value) in asset.Overrides)
            {
                if (!MaterialParameters.KnownProperties.Contains(key))
                    throw new InvalidDataException($"Unknown material override '{key}'.");
                values[JsonNamingPolicy.CamelCase.ConvertName(key)] =
                    JsonNode.Parse(value.GetRawText());
            }
            MaterialParameters resolved = values.Deserialize<MaterialParameters>(Json) ??
                throw new InvalidDataException("Material instance could not be resolved.");
            resolved.Normalize();
            return resolved;
        }
        finally
        {
            visited.Remove(identity);
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            IncludeFields = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new MaterialAssetReferenceConverter());
        return options;
    }

    private sealed class MaterialDocument
    {
        public int Version { get; set; } = MaterialAsset.CurrentVersion;
        public MaterialAssetKind Kind { get; set; }
        public string Name { get; set; } = "New Material";
        public AssetReference? ParentMaterial { get; set; }
        public MaterialParameters? Standard { get; set; }
        public Dictionary<string, JsonElement>? Overrides { get; set; }
    }

    private sealed class MaterialAssetReferenceConverter : JsonConverter<AssetReference>
    {
        public override AssetReference Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Null) return AssetReference.Empty;
            Guid guid = root.TryGetProperty("guid", out JsonElement id) &&
                id.ValueKind == JsonValueKind.String && Guid.TryParse(id.GetString(), out Guid parsed)
                    ? parsed : Guid.Empty;
            string? path = root.TryGetProperty("path", out JsonElement pathValue) &&
                pathValue.ValueKind == JsonValueKind.String ? pathValue.GetString() : null;
            return new AssetReference(guid, path);
        }

        public override void Write(Utf8JsonWriter writer, AssetReference value,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            if (value.Guid != Guid.Empty) writer.WriteString("guid", value.Guid);
            if (!string.IsNullOrWhiteSpace(value.CachedProjectPath))
                writer.WriteString("path", value.CachedProjectPath);
            writer.WriteEndObject();
        }
    }
}
