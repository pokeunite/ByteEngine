using System.Text.Json.Serialization;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Core.Serialization;

// Generated, typed property setters avoid WASM reflection invocation of SIMD
// arguments (colours, quaternions and matrices). Keep the desktop wire format.
[JsonSourceGenerationOptions(IncludeFields = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true, GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ImportedModel))]
[JsonSerializable(typeof(MaterialAssetSerializer.MaterialDocument))]
[JsonSerializable(typeof(MaterialParameters))]
internal partial class PortableAssetJsonContext : JsonSerializerContext;
