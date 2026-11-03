using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
namespace ByteEngine.Tests;
internal static class AnimationOptimizationTests
{
 public static void Run()
 {
  Sockets();Skinning();SharedBindings();CrowdMerge();CrowdColors();GpuBounds();Sampling();
 }
 static void SharedBindings()
 {
  const int count=10000;var vertices=new float[count*8];var indices=new Vector4[count];var weights=new Vector4[count];
  for(int i=0;i<count;i++){vertices[i*8]=1;vertices[i*8+4]=1;indices[i]=new(0,1,0,0);weights[i]=new(.4f,.6f,0,0);}
  var source=new ImportedMesh{Vertices=vertices,JointIndices=indices,JointWeights=weights};
  using var mesh=new Mesh(vertices.ToArray(),[0,1,2],true);var material=new Material();
  var type=typeof(SkeletalMeshRenderer).GetNestedType("RuntimeSkinnedMesh",BindingFlags.NonPublic)!;
  object New(float[] output)=>Activator.CreateInstance(type,source,0,mesh,material,output)!;
  object Prop(object runtime,string name)=>type.GetProperty(name)!.GetValue(runtime)!;
  var a=New(vertices.ToArray());var b=New(vertices.ToArray());
  foreach(string name in new[]{"Influences","SingleBoneIndices","GroupInfluences","VertexGroups","UsedBones"})
   Check(ReferenceEquals(Prop(a,name),Prop(b,name)),"actors share immutable "+name);
  Check(!ReferenceEquals(Prop(a,"GroupMatrices"),Prop(b,"GroupMatrices"))&&!ReferenceEquals(Prop(a,"DeformedVertices"),Prop(b,"DeformedVertices")),"actors retain independent pose and deformation buffers");
  var skin=typeof(SkeletalMeshRenderer).GetMethod("SkinMesh",BindingFlags.Static|BindingFlags.NonPublic)!;
  skin.Invoke(null,[a,new[]{Matrix4x4.CreateTranslation(1,0,0),Matrix4x4.CreateTranslation(1,0,0)}]);
  skin.Invoke(null,[b,new[]{Matrix4x4.CreateTranslation(4,0,0),Matrix4x4.CreateTranslation(4,0,0)}]);
  Check(Math.Abs(((float[])Prop(a,"DeformedVertices"))[0]-2)<.00001f&&Math.Abs(((float[])Prop(b,"DeformedVertices"))[0]-5)<.00001f,"shared binding does not cross-contaminate different animated poses");
  var createGpu=type.GetMethod("CreateGpuData",BindingFlags.NonPublic|BindingFlags.Instance)!;
  var gpuA=(GpuSkinningData)createGpu.Invoke(a,[2])!;var gpuB=(GpuSkinningData)createGpu.Invoke(b,[2])!;
  Check(ReferenceEquals(gpuA.Influences,gpuB.Influences),"GPU actors share immutable packed weights");
  var limited=(GpuSkinningData)createGpu.Invoke(a,[1])!;
  Check(limited.Influences[5]==0&&gpuA.Influences[5]==.6f,"GPU weight validity remains specific to skeleton bone count");
  for(int i=0;i<5;i++)New(vertices);
  long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<100;i++)New(vertices);
  long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
  Check(allocated<256000,"100 warm 10,000-vertex binding instances allocate under 256 KB ("+allocated+")");
  weights[0]=new(1,0,0,0);var edited=New(vertices);
  Check(!ReferenceEquals(Prop(a,"Influences"),Prop(edited,"Influences"))&&ReferenceEquals(Prop(a,"Influences"),Prop(b,"Influences")),"in-place imported weight edits create a new binding without mutating existing actors");
  var editedGpu=(GpuSkinningData)createGpu.Invoke(edited,[2])!;
  Check(editedGpu.Influences[4]==1&&editedGpu.Influences[5]==0&&gpuA.Influences[4]==.4f,"updated GPU weights preserve prior actors' binding snapshots");
  Check(!BindingSourceWeakReference().IsAlive,"shared bindings do not pin unloaded imported meshes");
 }
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 static WeakReference BindingSourceWeakReference()
 {
  var weak=CreateBindingSource();for(int i=0;i<3;i++){GC.Collect();GC.WaitForPendingFinalizers();}return weak;
 }
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 static WeakReference CreateBindingSource()
 {
  var source=new ImportedMesh();var type=typeof(SkeletalMeshRenderer).GetNestedType("SharedSkinDefinition",BindingFlags.NonPublic)!;
  type.GetMethod("Get")!.Invoke(null,[source]);return new WeakReference(source);
 }
 static void Sampling()
 {
  var vectors=new ImportedVectorTrack{Keys=new(){new(0,new(1,2,3),Vector3.Zero,Vector3.Zero),new(1,new(4,6,8),Vector3.Zero,Vector3.Zero),new(2,new(-3,2,7),Vector3.Zero,Vector3.Zero)}};
  var rotations=new ImportedQuaternionTrack{Keys=new(){new(0,Quaternion.Identity,default,default),new(1,Quaternion.CreateFromYawPitchRoll(.8f,.2f,-.3f),default,default),new(2,Quaternion.CreateFromYawPitchRoll(-.3f,.7f,.1f),default,default)}};
  foreach(var interpolation in Enum.GetValues<ImportedAnimationInterpolation>())
  {
   var v=new ImportedVectorTrack{Keys=vectors.Keys,Interpolation=interpolation};var q=new ImportedQuaternionTrack{Keys=rotations.Keys,Interpolation=interpolation};
   for(int i=-20;i<=220;i++)
   {
    float time=i*.01f;int a=time<=1?0:1;float t=Math.Clamp(time-a,0,1);float smooth=t*t*(3-2*t);
    Vector3 expected=time<=0?v.Keys[0].Value:time>=2?v.Keys[2].Value:interpolation==ImportedAnimationInterpolation.Step?v.Keys[a].Value:Vector3.Lerp(v.Keys[a].Value,v.Keys[a+1].Value,interpolation==ImportedAnimationInterpolation.CubicSpline?smooth:t);
    if(Vector3.Distance(AnimationPoseSampler.Sample(v,time,Vector3.Zero),expected)>.00001f)throw new Exception("Span animation sampling changed vector interpolation");
    Quaternion expectedQ;
    if(time<=0)expectedQ=q.Keys[0].Value;else if(time>=2)expectedQ=q.Keys[2].Value;else if(interpolation==ImportedAnimationInterpolation.Step)expectedQ=q.Keys[a].Value;else if(interpolation==ImportedAnimationInterpolation.Linear)expectedQ=Quaternion.Slerp(q.Keys[a].Value,q.Keys[a+1].Value,t);else{var x=q.Keys[a].Value;var y=q.Keys[a+1].Value;var blended=Vector4.Lerp(new(x.X,x.Y,x.Z,x.W),new(y.X,y.Y,y.Z,y.W),smooth);expectedQ=Quaternion.Normalize(new(blended.X,blended.Y,blended.Z,blended.W));}
    if(Math.Abs(Quaternion.Dot(AnimationPoseSampler.Sample(q,time,Quaternion.Identity),expectedQ))<.99999f)throw new Exception("Span animation sampling changed quaternion interpolation");
   }
  }
  vectors.Keys.Add(new(3,new(9,8,7),Vector3.Zero,Vector3.Zero));if(AnimationPoseSampler.Sample(vectors,3,Vector3.Zero)!=new Vector3(9,8,7))throw new Exception("Animation sampling cached stale imported keys");
  Check(true,"span sampling preserves step/linear/cubic vector and quaternion interpolation, boundaries and live key edits");
 }
 static void GpuBounds()
 {
  float[] vertices=[-1,-1,0,0,0,1,0,0, 1,-1,0,0,0,1,0,0, 0,1,0,0,0,1,0,0];
  float[] influences=[0,1,0,0,.2f,.3f,0,0, 0,1,0,0,.5f,.5f,0,0, 0,1,0,0,0,0,0,0];
  var gpu=new GpuSkinningData(vertices,influences);
  for(int step=0;step<60;step++){
   gpu.Bones=[Matrix4x4.CreateScale(.7f,1.2f,1)*Matrix4x4.CreateRotationZ(step*.06f)*Matrix4x4.CreateTranslation(2,0,0),Matrix4x4.CreateRotationX(step*.04f)*Matrix4x4.CreateTranslation(-1,2,0)];
   var bounds=gpu.Bounds();
   for(int v=0;v<3;v++){
    var point=new Vector3(vertices[v*8],vertices[v*8+1],vertices[v*8+2]);float total=0;var deformed=Vector3.Zero;
    for(int j=0;j<4;j++){float w=influences[v*8+4+j];if(w<=0)continue;deformed+=Vector3.Transform(point,gpu.Bones[(int)influences[v*8+j]])*w;total+=w;}
    if(total>0)point=deformed/total;
    if(point.X<bounds.Minimum.X-.00001f||point.Y<bounds.Minimum.Y-.00001f||point.Z<bounds.Minimum.Z-.00001f||point.X>bounds.Maximum.X+.00001f||point.Y>bounds.Maximum.Y+.00001f||point.Z>bounds.Maximum.Z+.00001f)throw new Exception("GPU skinning bounds excluded a deformed vertex");
   }
  }
  Check(true,"GPU bounds contain weighted/unweighted vertices through 60 scaled and rotated poses");
 }
 static void CrowdColors()
 {
  var renderer=new SkeletalMeshRenderer();var runtimeType=typeof(SkeletalMeshRenderer).GetNestedType("RuntimeSkinnedMesh",BindingFlags.NonPublic)!;
  var list=(System.Collections.IList)typeof(SkeletalMeshRenderer).GetField("_runtimeMeshes",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(renderer)!;
  float[] data=[0,0,0,0,1,0,.2f,.3f,1,0,0,0,1,0,.2f,.3f,0,0,1,0,1,0,.2f,.3f];
  using var left=new Mesh(data,[0,1,2],true);using var right=new Mesh(data,[0,1,2],true);
  Material a=new(){BaseColor=new(.1f,.2f,.3f,1)},b=new(){BaseColor=new(.8f,.5f,.2f,1)};
  foreach(var tuple in new[]{(left,a),(right,b)})list.Add(Activator.CreateInstance(runtimeType,new object[]{new ImportedMesh{Vertices=data,Indices=[0,1,2]},0,tuple.Item1,tuple.Item2,data.ToArray()}));
  var build=typeof(SkeletalMeshRenderer).GetMethod("BuildCrowdDraws",BindingFlags.Instance|BindingFlags.NonPublic)!;
  Mesh[] meshes=[left,right];Matrix4x4[] placements=[Matrix4x4.Identity,Matrix4x4.CreateTranslation(2,0,0)];
  var draws=(Array)build.Invoke(renderer,[meshes,placements])!;
  Check(draws.Length==1,"compatible flat crowd colours merge into one draw");
  var draw=draws.GetValue(0)!;var material=(Material)draw.GetType().GetProperty("PaletteMaterial")!.GetValue(draw)!;
  Check(material.SolidColorPalette!.SequenceEqual(new[]{a.BaseColor,b.BaseColor}),"generated palette preserves exact linear RGBA colours");
  using var mesh=(Mesh)draw.GetType().GetProperty("Mesh")!.GetValue(draw)!;var v=mesh.VertexData.Span;
  Check(v[6]==0&&v[3*8+6]==1&&v[3*8]==2,"per-surface palette slots and transformed geometry remain distinct");
  b.Roughness=.25f;draws=(Array)build.Invoke(renderer,[meshes,placements])!;
  Check(draws.Length==2,"different roughness remains in separate draws");
 }
 static void CrowdMerge()
 {
  var renderer=new SkeletalMeshRenderer();const int vertices=10000;var data=new float[vertices*8];
  for(int i=0;i<vertices;i++){data[i*8]=i*.01f;data[i*8+4]=1;data[i*8+6]=.25f;data[i*8+7]=.75f;}
  using var source=new Mesh(data,[0,1,2],true);
  var method=typeof(SkeletalMeshRenderer).GetMethod("MergeCrowdGeometry",BindingFlags.Instance|BindingFlags.NonPublic)!;
  var merge=method.CreateDelegate<Func<SkeletalMeshRenderer,Mesh[],Matrix4x4[],IReadOnlyList<int>,Mesh>>();
  Mesh[] meshes=[source];Matrix4x4[] poses=[Matrix4x4.CreateTranslation(2,3,4)];int[] group=[0];
  using(var warm=merge(renderer,meshes,poses,group)){}
  long before=GC.GetAllocatedBytesForCurrentThread();using var result=merge(renderer,meshes,poses,group);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
  Check(bytes<400000,"10,000-vertex crowd merge allocates under 400 KB ("+bytes+")");
  var output=result.VertexData.Span;Check(output[0]==2&&output[1]==3&&output[2]==4&&output[4]==1&&output[6]==.25f&&output[7]==.75f,"preallocated merge preserves transformed positions, normals and UVs");
  Check(result.IndexData.Span.SequenceEqual(new uint[]{0,1,2}),"preallocated merge preserves triangle indices");
 }
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS animation optimization: "+message);}
 static void Set(object o,string name,object value)=>o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(o,value);
 static void Sockets()
 {
  var socket=new SkeletalSocketDefinition{Name="Grip",BoneName="Hand",PositionOffset=new(.1f,0,0)};
  var imported=new ImportedModel{Guid=Guid.NewGuid(),Skeleton=new SkeletonAsset{Bones=new(){new Bone{Name="Hand",BindPose=Matrix4x4.Identity}}},Nodes=new(){new ImportedNode{Key="Hand",Name="Hand"}}};
  var model=new ModelAsset(imported);model.ReplaceSockets([socket]);socket=model.FindSocket("Grip")!;
  var scene=new Scene("Socket cache tests");var root=scene.CreateGameObject("Owner");var renderer=root.AddComponent(new SkeletalMeshRenderer());
  Set(renderer,"_model",model);Set(renderer,"_skeleton",model.Skeleton!);Set(renderer,"_nodes",model.Nodes.ToArray());Set(renderer,"_boneNodeIndices",new[]{0});Set(renderer,"_currentPoseGlobals",new[]{Matrix4x4.Identity});Set(renderer,"_resolved",true);Set(renderer,"_poseDirty",false);
  bool Resolve(out SkeletalSocketTransform transform)=>SkeletalSocketResolver.TryGetSocketModelTransform(renderer,socket,out transform);
  Resolve(out var original);long hits=SkeletalSocketResolver.DiagnosticPoseCacheHits;
  long allocated=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
  for(int i=0;i<10000;i++)Resolve(out _);
  watch.Stop();allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
  Check(SkeletalSocketResolver.DiagnosticPoseCacheHits-hits==10000,"unchanged poses reuse socket results");
  Check(allocated<1024,"10,000 warm socket queries allocate under 1 KiB ("+allocated+" bytes)");
  Console.WriteLine("SOCKET BENCHMARK "+watch.Elapsed.TotalMilliseconds+" ms / 10000 queries");
  Set(renderer,"_currentPoseGlobals",new[]{Matrix4x4.CreateTranslation(2,3,4)});Resolve(out var moved);
  Check(Vector3.Distance(moved.Position,original.Position+new Vector3(2,3,4))<.0001f,"new bone pose invalidates cache");
  socket.PositionOffset+=Vector3.UnitX;Resolve(out var edited);Check(edited.Position==moved.Position+Vector3.UnitX,"live socket edits invalidate cache");
  root.Transform.WorldPosition=new(12,0,0);SkeletalSocketResolver.TryGetSocketWorldTransform(renderer,"Grip",out var world);Check(world.Position==edited.Position+root.Transform.WorldPosition,"world movement remains live with cached model pose");
  socket.BoneName="missing";Check(!Resolve(out _),"missing bones never return a cached success");socket.BoneName="HAND";Check(Resolve(out _),"case-insensitive bone lookup preserved");
 }
 static void Skinning()
 {
  const int count=96000;var vertices=new float[count*8];var indices=new Vector4[count];var weights=new Vector4[count];
  for(int i=0;i<count;i++){int at=i*8;vertices[at]=i%107*.01f;vertices[at+1]=i%71*.02f;vertices[at+2]=i%53*.03f;vertices[at+3]=.3f;vertices[at+4]=.9f;vertices[at+5]=.2f;vertices[at+6]=.75f;indices[i]=new(0,1,2,99);weights[i]=i%4==0?Vector4.Zero:new(.2f,.3f,.1f,.4f);}
  var source=new ImportedMesh{Vertices=vertices,JointIndices=indices,JointWeights=weights};
  using var mesh=new Mesh(vertices.ToArray(),[0,1,2],true);var output=vertices.ToArray();
  var runtimeType=typeof(SkeletalMeshRenderer).GetNestedType("RuntimeSkinnedMesh",BindingFlags.NonPublic)!;
  var runtime=Activator.CreateInstance(runtimeType,source,0,mesh,new Material(),output)!;
  var method=typeof(SkeletalMeshRenderer).GetMethod("SkinMesh",BindingFlags.Static|BindingFlags.NonPublic)!;
  var runtimeParameter=System.Linq.Expressions.Expression.Parameter(typeof(object));var paletteParameter=System.Linq.Expressions.Expression.Parameter(typeof(Matrix4x4[]));
  var skin=System.Linq.Expressions.Expression.Lambda<Action<object,Matrix4x4[]>>(System.Linq.Expressions.Expression.Call(method,System.Linq.Expressions.Expression.Convert(runtimeParameter,runtimeType),paletteParameter),runtimeParameter,paletteParameter).Compile();
  Matrix4x4[] palette=[Matrix4x4.CreateRotationY(.4f)*Matrix4x4.CreateTranslation(1,2,3),Matrix4x4.CreateScale(1.1f,.8f,1.2f),Matrix4x4.CreateRotationX(.8f)];
  bool prior=JobRunner.ParallelExecutionEnabled,priorSkinning=JobRunner.ParallelSkinningEnabled;JobRunner.ParallelSkinningEnabled=true;
  try{
   JobRunner.ParallelExecutionEnabled=false;skin(runtime,palette);var serial=output.ToArray();var bounds=mesh.LocalBounds;
   float maxError=0;
   for(int i=0;i<count;i++){
    int at=i*8;var p=new Vector3(vertices[at],vertices[at+1],vertices[at+2]);var n=new Vector3(vertices[at+3],vertices[at+4],vertices[at+5]);
    if(i%4!=0){p=(Vector3.Transform(p,palette[0])*.2f+Vector3.Transform(p,palette[1])*.3f+Vector3.Transform(p,palette[2])*.1f)/.6f;n=(Vector3.TransformNormal(n,palette[0])*.2f+Vector3.TransformNormal(n,palette[1])*.3f+Vector3.TransformNormal(n,palette[2])*.1f)/.6f;}
    n=Vector3.Normalize(n);maxError=Math.Max(maxError,Vector3.Distance(p,new(output[at],output[at+1],output[at+2])));maxError=Math.Max(maxError,Vector3.Distance(n,new(output[at+3],output[at+4],output[at+5])));
   }
   Check(maxError<.00001f,"grouped palette matches original four-influence reference within 0.00001 ("+maxError+")");
   JobRunner.ParallelExecutionEnabled=true;skin(runtime,palette);
   Check(serial.SequenceEqual(output)&&bounds==mesh.LocalBounds,"serial/worker skinning produces identical vertices, normals, UVs and bounds");
   Check(output[6]==.75f&&output[0]==vertices[0],"UVs and unweighted vertices preserved");
   for(int i=0;i<4;i++)skin(runtime,palette);
   var timer=Stopwatch.StartNew();for(int i=0;i<20;i++)skin(runtime,palette);timer.Stop();Console.WriteLine("SKIN BENCHMARK worker "+timer.Elapsed.TotalMilliseconds/20+" ms / 96000 vertices");
   JobRunner.ParallelExecutionEnabled=false;timer.Restart();for(int i=0;i<20;i++)skin(runtime,palette);timer.Stop();Console.WriteLine("SKIN BENCHMARK serial "+timer.Elapsed.TotalMilliseconds/20+" ms / 96000 vertices");
  }finally{JobRunner.ParallelExecutionEnabled=prior;JobRunner.ParallelSkinningEnabled=priorSkinning;}
 }
}
