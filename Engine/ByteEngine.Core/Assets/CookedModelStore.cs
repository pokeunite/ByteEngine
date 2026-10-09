using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Serialization;
namespace ByteEngine.Core.Assets;
/// <summary>Binary portable mesh data plus small metadata; shared encoded textures are stored once.</summary>
public static class CookedModelStore
{
 public static string PathFor(string root,Guid id)=>Path.Combine(root,".byteengine","WebModels",id.ToString("N")+".bytemodel");
 public static string TexturePath(string root,string hash){if(hash.Length!=64||hash.Any(c=>!char.IsAsciiHexDigit(c)))throw new InvalidDataException("Invalid cooked texture hash.");return Path.Combine(root,".byteengine","WebTextures",hash.ToLowerInvariant()+".bin");}
 public static void Save(string root,ImportedModel model)
 {
  if(model.Guid==Guid.Empty)throw new InvalidDataException("Cooked model has no GUID.");
  ImportedTexture? CookTexture(ImportedTexture? t){if(t==null)return null;if(t.EncodedData.Length==0)return t;string hash=Convert.ToHexString(SHA256.HashData(t.EncodedData)).ToLowerInvariant();string file=TexturePath(root,hash);Directory.CreateDirectory(Path.GetDirectoryName(file)!);if(!File.Exists(file)){string temporaryTexture=file+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllBytes(temporaryTexture,t.EncodedData);File.Move(temporaryTexture,file,true);}finally{if(File.Exists(temporaryTexture))File.Delete(temporaryTexture);}}return new(){Key=t.Key,Name=t.Name,SourcePath=t.SourcePath,CookedContentHash=hash};}
  var metadata=new ImportedModel{Guid=model.Guid,SourceAssetGuid=model.SourceAssetGuid,Name=model.Name,Nodes=model.Nodes,Skeleton=model.Skeleton,Animations=model.Animations,Meshes=model.Meshes.Select(m=>new ImportedMesh{Key=m.Key,Name=m.Name,MaterialKey=m.MaterialKey}).ToList(),Materials=model.Materials.Select(m=>new ImportedMaterial{
   Key=m.Key,Name=m.Name,BaseColor=m.BaseColor,Metallic=m.Metallic,Roughness=m.Roughness,SurfaceType=m.SurfaceType,AlphaCutoff=m.AlphaCutoff,DoubleSided=m.DoubleSided,Unlit=m.Unlit,AmbientOcclusionStrength=m.AmbientOcclusionStrength,EmissionColor=m.EmissionColor,
   BaseColorTexture=CookTexture(m.BaseColorTexture),NormalTexture=CookTexture(m.NormalTexture),MetallicRoughnessTexture=CookTexture(m.MetallicRoughnessTexture),MetallicTexture=CookTexture(m.MetallicTexture),RoughnessTexture=CookTexture(m.RoughnessTexture),AmbientOcclusionTexture=CookTexture(m.AmbientOcclusionTexture),EmissionTexture=CookTexture(m.EmissionTexture)}).ToList()};
  string path=PathFor(root,model.Guid);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
  string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try { using(var file=File.Create(temporary)) { using var writer=new BinaryWriter(file);writer.Write("BYTMOD03"u8);var json=JsonSerializer.SerializeToUtf8Bytes(metadata,JsonSerialization.Options);writer.Write(json.Length);writer.Write(json);
  foreach(var mesh in model.Meshes){Write(writer,mesh.Vertices);Write(writer,mesh.Indices);Write(writer,mesh.JointIndices);Write(writer,mesh.JointWeights);}
  } File.Move(temporary,path,true); } finally { if(File.Exists(temporary))File.Delete(temporary); }
 }
 static void Write<T>(BinaryWriter writer,T[] values)where T:unmanaged{if(!BitConverter.IsLittleEndian)throw new PlatformNotSupportedException("Portable model cooking requires little-endian floats.");writer.Write(values.Length);writer.Write(MemoryMarshal.AsBytes(values.AsSpan()));}
 static T[] Read<T>(BinaryReader reader)where T:unmanaged{int count=reader.ReadInt32();long size=(long)count*System.Runtime.CompilerServices.Unsafe.SizeOf<T>();if(count<0||size>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException("Invalid cooked mesh array length.");var values=new T[count];reader.BaseStream.ReadExactly(MemoryMarshal.AsBytes(values.AsSpan()));return values;}
 static ImportedModel ReadMetadata(byte[] json,string? hydrateRoot=null)
 {
  ImportedTexture? Texture(ImportedTexture? texture)
  {
   if(hydrateRoot==null||texture?.CookedContentHash is not {} hash)return texture;
   byte[] encoded=File.ReadAllBytes(TexturePath(hydrateRoot,hash));
   if(!string.Equals(Convert.ToHexString(SHA256.HashData(encoded)),hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Cached texture integrity mismatch.");
   return new ImportedTexture{Key=texture.Key,Name=texture.Name,SourcePath=texture.SourcePath,EncodedData=encoded};
  }
  var model=JsonSerializer.Deserialize<ImportedModel>(json,JsonSerialization.Options)??throw new InvalidDataException("Invalid cooked model metadata.");
  // WASM reflection can corrupt the SIMD Vector4 argument to an init setter.
  // Reconstruct material tints through direct typed assignments, preserving authored channels.
  using var document=JsonDocument.Parse(json);if(document.RootElement.TryGetProperty("materials",out var materials)){
   int index=0;foreach(var item in materials.EnumerateArray()){
    if(index>=model.Materials.Count)throw new InvalidDataException("Cooked material count mismatch.");var m=model.Materials[index];var color=Vector4.One;
    if(item.TryGetProperty("baseColor",out var tint)){float Component(string name,float fallback){foreach(var property in tint.EnumerateObject())if(string.Equals(property.Name,name,StringComparison.OrdinalIgnoreCase))return property.Value.GetSingle();return fallback;}color=new Vector4(Component("x",0),Component("y",0),Component("z",0),Component("w",0));}
    model.Materials[index++]=new ImportedMaterial{Key=m.Key,Name=m.Name,BaseColor=color,Metallic=m.Metallic,Roughness=m.Roughness,SurfaceType=m.SurfaceType,AlphaCutoff=m.AlphaCutoff,DoubleSided=m.DoubleSided,Unlit=m.Unlit,AmbientOcclusionStrength=m.AmbientOcclusionStrength,EmissionColor=m.EmissionColor,BaseColorTexture=Texture(m.BaseColorTexture),NormalTexture=Texture(m.NormalTexture),MetallicRoughnessTexture=Texture(m.MetallicRoughnessTexture),MetallicTexture=Texture(m.MetallicTexture),RoughnessTexture=Texture(m.RoughnessTexture),AmbientOcclusionTexture=Texture(m.AmbientOcclusionTexture),EmissionTexture=Texture(m.EmissionTexture)};
   }
  }
  return model;
 }
 public static ImportedModel Load(string root,Guid id,bool hydrateTextures=false)
 {
  string path=PathFor(root,id);if(!File.Exists(path)){var legacy=ReadMetadata(File.ReadAllBytes(Path.ChangeExtension(path,"json")),hydrateTextures?root:null);if(legacy.Guid!=id)throw new InvalidDataException("Cooked model GUID mismatch.");return legacy;}
  using var file=File.OpenRead(path);using var reader=new BinaryReader(file);
  if(!reader.ReadBytes(8).AsSpan().SequenceEqual("BYTMOD03"u8))throw new InvalidDataException("Invalid cooked model header.");
  int length=reader.ReadInt32();if(length<0||length>64*1024*1024||length>file.Length-file.Position)throw new InvalidDataException("Invalid cooked model metadata length.");
  var metadata=ReadMetadata(reader.ReadBytes(length),hydrateTextures?root:null);if(metadata.Guid!=id)throw new InvalidDataException("Cooked model GUID mismatch.");
  var meshes=new List<ImportedMesh>();foreach(var m in metadata.Meshes){var vertices=Read<float>(reader);var indices=Read<uint>(reader);var joints=Read<Vector4>(reader);var weights=Read<Vector4>(reader);if(vertices.Length%8!=0||indices.Any(i=>i>=vertices.Length/8))throw new InvalidDataException("Invalid cooked mesh geometry.");meshes.Add(new(){Key=m.Key,Name=m.Name,MaterialKey=m.MaterialKey,Vertices=vertices,Indices=indices,JointIndices=joints,JointWeights=weights});}
  if(file.Position!=file.Length)throw new InvalidDataException("Unexpected cooked model trailing bytes.");
  return new(){Guid=metadata.Guid,SourceAssetGuid=metadata.SourceAssetGuid,Name=metadata.Name,Nodes=metadata.Nodes,Materials=metadata.Materials,Skeleton=metadata.Skeleton,Animations=metadata.Animations,Meshes=meshes};
 }
}
