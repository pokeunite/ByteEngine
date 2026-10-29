using System.Text.Json.Serialization;

namespace ByteEngine.Core.Vfx;

// Typed setters avoid reflective SIMD argument invocation in browser AOT.
// Preserve VFX's existing PascalCase document format.
[JsonSourceGenerationOptions(IncludeFields=true,GenerationMode=JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(VfxEffect))]
internal partial class VfxJsonContext : JsonSerializerContext;
