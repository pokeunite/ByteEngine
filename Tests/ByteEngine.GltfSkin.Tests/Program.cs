using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

string root=Path.Combine(Path.GetTempPath(),"ByteEngine-multiskin-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
try
{
 using var bytes=new MemoryStream();using var w=new BinaryWriter(bytes);
 void F(params float[] values){foreach(var v in values)w.Write(v);}
 F(0,0,0,1,0,0,0,1,0); // positions, 36 bytes
 for(int i=0;i<12;i++)w.Write((byte)0); // joints, 12
 F(1,0,0,0,1,0,0,0,1,0,0,0); // weights, 48
 F(1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1); // inverse bind, 64
 F(0,1); // times, 8
 F(0,0,0,0,1,0); // translations, 24
 File.WriteAllBytes(Path.Combine(root,"fixture.bin"),bytes.ToArray());
 var doc=new {asset=new{version="2.0"},scene=0,scenes=new[]{new{nodes=new[]{0,1,2,3}}},
  nodes=new object[]{new{name="Joint"},new{name="Joint"},new{name="Skin A",mesh=0,skin=0},new{name="Skin B",mesh=0,skin=1}},
  skins=new[]{new{joints=new[]{0},inverseBindMatrices=3},new{joints=new[]{1},inverseBindMatrices=3}},
  meshes=new[]{new{primitives=new[]{new{attributes=new{POSITION=0,JOINTS_0=1,WEIGHTS_0=2}}}}},
  buffers=new[]{new{uri="fixture.bin",byteLength=bytes.Length}},
  bufferViews=new object[]{new{buffer=0,byteOffset=0,byteLength=36},new{buffer=0,byteOffset=36,byteLength=12},new{buffer=0,byteOffset=48,byteLength=48},new{buffer=0,byteOffset=96,byteLength=64},new{buffer=0,byteOffset=160,byteLength=8},new{buffer=0,byteOffset=168,byteLength=24,byteStride=12}},
  accessors=new object[]{new{bufferView=0,componentType=5126,count=3,type="VEC3",min=new[]{0,0,0},max=new[]{1,1,0}},new{bufferView=1,componentType=5121,count=3,type="VEC4"},new{bufferView=2,componentType=5126,count=3,type="VEC4"},new{bufferView=3,componentType=5126,count=1,type="MAT4"},new{bufferView=4,componentType=5126,count=2,type="SCALAR",min=new[]{0},max=new[]{1}},new{bufferView=5,componentType=5126,count=2,type="VEC3"}},
  animations=new[]{new{name="Independent rigs",samplers=new[]{new{input=4,output=5}},channels=new[]{new{sampler=0,target=new{node=1,path="translation"}}}}}};
 string path=Path.Combine(root,"fixture.gltf");File.WriteAllText(path,JsonSerializer.Serialize(doc));
 var id=Guid.NewGuid();var source=new AssetRecord(id,AssetType.Model3D,"fixture.gltf",path,path+".meta",new(){Guid=id,Type=AssetType.Model3D});
 var imported=new GltfModelImporter().Import(source,new());
 void Check(bool pass,string label){if(!pass)throw new Exception(label);Console.WriteLine("PASS "+label);}
 Check(imported.Skeleton!.Bones.Count==2,"Both skins are retained");
 Check(imported.Skeleton.Bones.Select(b=>b.Name).Distinct().Count()==2,"Duplicate names identify distinct logical joints");
 Check(imported.Meshes.Count==2,"A mesh shared across skin palettes has separate import instances");
 Check(imported.Meshes[0].JointIndices.All(j=>j.X==0)&&imported.Meshes[1].JointIndices.All(j=>j.X==1),"Each mesh palette maps to its own skeleton segment");
 Check(imported.Animations.Single().Channels.Single().NodeName==imported.Skeleton.Bones[1].Name,"Animation resolves the second rig despite duplicate source names");
 Check(imported.Nodes.Where(n=>n.MeshKeys.Count>0).SelectMany(n=>n.MeshKeys).Distinct().Count()==2,"Scene nodes retain their correct mesh/skin association");
 Check(imported.Animations.Single().Channels.Single().Translation!.Keys.Last().Value.Y==1,"Redundant packed animation stride is repaired without changing keyframes");
 Check(new ModelImporterSettings{MaxTextureSize=1024}.Clone().MaxTextureSize==1024,"Texture limit survives native import-setting clone");
}
finally{Directory.Delete(root,true);}
