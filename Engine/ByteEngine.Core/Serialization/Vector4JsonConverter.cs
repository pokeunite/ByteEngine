using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace ByteEngine.Core.Serialization;
// Explicit reads avoid reflective SIMD struct field writes in the WASM AOT runtime.
internal sealed class Vector4JsonConverter : JsonConverter<Vector4>
{
 public override Vector4 Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options){
  if(reader.TokenType!=JsonTokenType.StartObject)throw new JsonException("Expected four-component vector object.");
  float x=0,y=0,z=0,w=0;
  while(reader.Read()){
   if(reader.TokenType==JsonTokenType.EndObject)return new Vector4(x,y,z,w);
   if(reader.TokenType!=JsonTokenType.PropertyName)throw new JsonException();
   string name=reader.GetString()!;if(!reader.Read())throw new JsonException();
   switch(name.ToLowerInvariant()){case "x":x=reader.GetSingle();break;case "y":y=reader.GetSingle();break;case "z":z=reader.GetSingle();break;case "w":w=reader.GetSingle();break;default:reader.Skip();break;}
  }
  throw new JsonException("Truncated vector.");
 }
 public override void Write(Utf8JsonWriter writer,Vector4 value,JsonSerializerOptions options){writer.WriteStartObject();writer.WriteNumber("x",value.X);writer.WriteNumber("y",value.Y);writer.WriteNumber("z",value.Z);writer.WriteNumber("w",value.W);writer.WriteEndObject();}
}
