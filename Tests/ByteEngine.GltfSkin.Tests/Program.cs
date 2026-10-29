using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using System.Reflection;
using System.Text.Json.Nodes;

if (args.Length == 2 && args[0] == "--inspect-model")
{
 string modelPath=Path.GetFullPath(args[1]);var modelId=Guid.NewGuid();
 var record=new AssetRecord(modelId,AssetType.Model3D,Path.GetFileName(modelPath),modelPath,modelPath+".meta",new(){Guid=modelId,Type=AssetType.Model3D});
 var model=new GltfModelImporter().Import(record,new());
 if(model.Skeleton?.Bones.Count!=51 || model.Animations.Count!=76 || model.Meshes.Count==0)
  throw new Exception("Explorer skeleton, clips or mesh failed native import validation.");
 foreach(var clip in new[]{"SHRun","DHRun","SHStandShoot","DHStandReload","StandDeath"})
  if(!model.Animations.Any(a=>a.Name.EndsWith(clip,StringComparison.Ordinal)))throw new Exception("Missing clip: "+clip);
 Console.WriteLine($"PASS Explorer native import: {model.Skeleton.Bones.Count} bones, {model.Animations.Count} clips, {model.Meshes.Count} mesh parts.");
 return;
}
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
 var transform=Matrix4x4.CreateScale(2,3,4)*Matrix4x4.CreateRotationY(.7f)*Matrix4x4.CreateTranslation(5,6,7);
 var rotation=Quaternion.CreateFromYawPitchRoll(.3f,.4f,.5f);
 var portable=new ImportedModel{Guid=id,Name="Portable numeric regression",Nodes=[new(){Name="Root",LocalTransform=transform}],
  Skeleton=new(){Bones=[new(){Name="Root",BindPose=transform}]},
  Materials=[new(){Name="Sand",BaseColor=new(.9f,.4f,.1f,1)}],
  Animations=[new(){Name="Turn",Duration=1,Channels=[new(){NodeName="Root",Rotation=new(){Keys=[new(0,rotation,Quaternion.Identity,Quaternion.Identity)]},Translation=new(){Keys=[new(0,new(1,2,3),Vector3.Zero,Vector3.Zero)]}}]}]};
 CookedModelStore.Save(root,portable);var loaded=CookedModelStore.Load(root,id);
 Check(loaded.Nodes[0].LocalTransform==transform&&loaded.Skeleton!.Bones[0].BindPose==transform,"Portable node and inverse-bind matrix channels remain exact");
 Check(loaded.Animations[0].Channels[0].Rotation!.Keys[0].Value==rotation&&loaded.Animations[0].Channels[0].Translation!.Keys[0].Value==new Vector3(1,2,3),"Portable quaternion and translation tracks remain exact");
 var materialPath=Path.Combine(root,"sand.bmat");var material=new MaterialAsset{Standard=new(){BaseColor=new(.9f,.4f,.1f,1),UvTiling=new(50,-57.142857f)}};
 MaterialAssetSerializer.Save(materialPath,material);var sand=MaterialAssetSerializer.Load(materialPath);
 Check(sand.Standard.BaseColor==material.Standard.BaseColor&&sand.Standard.UvTiling==material.Standard.UvTiling,"Material colour and signed UV tiling survive typed deserialization");
 // Exercise the actual renderer, including transitions into and out of its CPU fallback.
 F(1,1,1,1,2,1);File.WriteAllBytes(Path.Combine(root,"fixture.bin"),bytes.ToArray());
 var json=JsonNode.Parse(File.ReadAllText(path))!;
 json["buffers"]![0]!["byteLength"]=bytes.Length;
 json["bufferViews"]!.AsArray().Add(JsonNode.Parse("{\"buffer\":0,\"byteOffset\":192,\"byteLength\":24}"));
 json["accessors"]!.AsArray().Add(JsonNode.Parse("{\"bufferView\":6,\"componentType\":5126,\"count\":2,\"type\":\"VEC3\"}"));
 json["animations"]!.AsArray().Add(JsonNode.Parse("{\"name\":\"Stretch\",\"samplers\":[{\"input\":4,\"output\":6}],\"channels\":[{\"sampler\":0,\"target\":{\"node\":1,\"path\":\"scale\"}}]}"));
 File.WriteAllText(path,json.ToJsonString());
 using var db=new AssetDatabase(root,["."]);using var assets=new AssetManager(db);
 var registration=AnimationRuntimeAssets.Configure(assets);
 try
 {
  var scene=new Scene("Rigid animation regression");var owner=scene.CreateGameObject("Skin");
  var renderer=owner.AddComponent(new SkeletalMeshRenderer{Model=new AssetReference(db.Assets.Single(a=>a.ProjectPath=="fixture.gltf").Guid)});
  Check(renderer.Play("Independent rigs",false,0)&&renderer.Seek(.5f),"Runtime translation clip loads");renderer.Pause();
  Vector3 Vertex(int mesh,int vertex)
  {
   var poses=renderer.CapturePoseMeshes();try{var data=poses[mesh].Mesh.VertexData.Span;int i=vertex*8;return Vector3.Transform(new(data[i],data[i+1],data[i+2]),poses[mesh].World);}
   finally{foreach(var pose in poses)pose.Mesh.Dispose();}
  }
  Check(Vector3.Distance(Vertex(0,0),Vector3.Zero)<.0001f&&Vector3.Distance(Vertex(1,0),new(0,.5f,0))<.0001f,"Rigid parts preserve independent world poses and pose snapshots");
  var runtime=(System.Collections.IEnumerable)typeof(SkeletalMeshRenderer).GetField("_runtimeMeshes",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(renderer)!;
  var meshes=runtime.Cast<object>().Select(r=>(Mesh)r.GetType().GetProperty("Mesh")!.GetValue(r)!).ToArray();
  var versions=meshes.Select(m=>m.GeometryVersion).ToArray();renderer.Seek(.7f);_ = Vertex(1,0);
  Check(meshes.Select(m=>m.GeometryVersion).SequenceEqual(versions),"Rigid animation changes matrices without uploading vertices");
  Check(renderer.Play("Stretch",false,0)&&renderer.Seek(.5f),"Nonuniform scale clip loads");
  Check(Vector3.Distance(Vertex(1,2),new(0,1.5f,0))<.0001f,"Nonuniform skin uses correct CPU fallback");
  renderer.Play("Independent rigs",false,0);renderer.Seek(.25f);
  Check(Vector3.Distance(Vertex(1,2),new(0,1.25f,0))<.0001f,"Returning to rigid animation restores source geometry");
 }
 finally{AnimationRuntimeAssets.Clear(registration);}
}
finally{Directory.Delete(root,true);}
