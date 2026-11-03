using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
namespace ByteEngine.Tests;
internal static class DispatchPerformanceTests
{
 public static void Run()
 {
  var buffers=new ByteEngine.Core.Runtime.FrameTransferBuffers(1024,pinned:true);
  buffers.Allocate(64,out var bytes);bytes[0]=123;var address=buffers.GetPinnedAddress(0);
  GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
  if(address!=buffers.GetPinnedAddress(0)||System.Runtime.InteropServices.Marshal.ReadByte(address)!=123)throw new Exception("Pinned frame storage moved or changed under GC");
  buffers.BeginFrame();buffers.Allocate(64,out var reused);
  if(!ReferenceEquals(bytes,reused)||address!=buffers.GetPinnedAddress(0))throw new Exception("Pinned frame pool did not reuse stable storage");
  bool rejected=false;try{new ByteEngine.Core.Runtime.FrameTransferBuffers().GetPinnedAddress(0);}catch(InvalidOperationException){rejected=true;}
  if(!rejected)throw new Exception("Unpinned frame storage exposed an address");
  var bounded=new ByteEngine.Core.Runtime.FrameTransferBuffers(16,pinned:true);
  bounded.Allocate(8,out _);bounded.Allocate(8,out _);bounded.BeginFrame();bounded.Allocate(8,out _);bounded.Allocate(12,out var recent);bounded.BeginFrame();
  long created=bounded.CreatedBytes;bounded.Allocate(12,out var recentAgain);
  if(!ReferenceEquals(recent,recentAgain)||bounded.CreatedBytes!=created||bounded.RetainedBytes>16)throw new Exception("Stale frame sizes evicted a recently used size or exceeded the retention cap");
  Console.WriteLine("PASS transfers: pinned storage survives GC, reuses bounded pool and rejects unpinned pointers");
  Contacts();Ordering();RenderTransforms();MatrixPackets();Snapshots();NavigationQueue();QueryOnly();RenderOrdersAndClocks();
  var scene=new Scene("Dispatch checks");var obj=scene.CreateGameObject("Host");obj.AddComponent(new Empty());obj.AddComponent(new Empty());
  var weak=Probe(obj);
  for(int i=0;i<8&&weak.IsAlive;i++){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();}
  if(weak.IsAlive)throw new Exception("Weak component lookup cache retained a collectible plugin type");GC.KeepAlive(obj);
  var snapshotType=SnapshotMiss(scene);
  for(int i=0;i<8&&snapshotType.IsAlive;i++){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();}
  if(snapshotType.IsAlive)throw new Exception("Component snapshot miss retained a collectible plugin type");GC.KeepAlive(scene);
  if(new Empty().HasRenderCallback||!new Rendered().HasRenderCallback||!new InheritedRendered().HasRenderCallback)throw new Exception("Render callback detection changed inheritance behavior");
  Console.WriteLine("PASS dispatch: collectible hit/miss invalidation, weak-key unload and inherited/renderless callback detection");
 }
 static void RenderOrdersAndClocks()
 {
  var scene=new Scene("Render order and clock lookup");var parent=scene.CreateGameObject("Root");var child=scene.CreateGameObject("Child");child.SetParent(parent,false);child.AddComponent(new Empty());
  if(child.RenderOrder!=0||SimulationRate3D.Resolve(child)!=1)throw new Exception("Default order/clock changed");
  var first=child.AddComponent(new DynamicOrder());var later=child.AddComponent(new DynamicOrder{Value=4});if(child.RenderOrder!=4)throw new Exception("Null order override hid a later provider");
  first.Value=-2;if(child.RenderOrder!=-2)throw new Exception("Live render order override became stale");child.RemoveComponent(first);if(child.RenderOrder!=4)throw new Exception("Removed render order provider remained cached");
  var clock=parent.AddComponent(new SimulationRate3D{Rate=.3f});if(SimulationRate3D.Resolve(child)!=.3f)throw new Exception("Added parent clock did not invalidate fast clock path");
  clock.Rate=.7f;if(SimulationRate3D.Resolve(child)!=.7f)throw new Exception("Live parent clock rate became stale");clock.Enabled=false;if(SimulationRate3D.Resolve(child)!=1)throw new Exception("Disabled clock still applied");
  clock.Enabled=true;var own=child.AddComponent(new SimulationRate3D{Rate=.5f,SourceObjectId=parent.Id});if(SimulationRate3D.Resolve(child)!=.7f)throw new Exception("Shared clock reference changed");
  child.RemoveComponent(own);parent.RemoveComponent(clock);if(SimulationRate3D.Resolve(child)!=1)throw new Exception("Removed clock did not restore fast default");
  Console.WriteLine("PASS render-order providers and clock registry: null/live overrides, membership changes, parent rates, enabled state and shared sources");
 }
 [MethodImpl(MethodImplOptions.NoInlining)]static WeakReference SnapshotMiss(Scene scene)
 {
  var assembly=AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("SnapshotMiss"+Guid.NewGuid().ToString("N")),AssemblyBuilderAccess.RunAndCollect);
  var builder=assembly.DefineDynamicModule("Probe").DefineType("MissingPluginComponent",TypeAttributes.Public,typeof(Component));builder.DefineDefaultConstructor(MethodAttributes.Public);var type=builder.CreateType()!;
  typeof(Scene).GetMethod(nameof(Scene.GetComponentsSnapshot))!.MakeGenericMethod(type).Invoke(scene,null);return new WeakReference(type);
 }
 static void NavigationQueue()
 {
  var actual=new ByteEngine.Core.Navigation.NavigationOpenQueue();var expected=new PriorityQueue<int,(float,int)>();var random=new Random(813);
  for(int pass=0;pass<5;pass++)
  {
   actual.Clear();expected.Clear();
   for(int i=0;i<4000;i++){float score=i%173==0?float.NaN:i%179==0?float.PositiveInfinity:random.Next(-100,100)*.25f;actual.Enqueue(i,(score,i));expected.Enqueue(i,(score,i));if(i%3==0&&actual.Dequeue()!=expected.Dequeue())throw new Exception("Concrete navigation heap changed priority/tie order");}
   while(expected.Count>0)if(actual.Dequeue()!=expected.Dequeue())throw new Exception("Navigation heap changed a reused search result");
   if(actual.Count!=0)throw new Exception("Navigation heap retained consumed entries");
  }
  Console.WriteLine("PASS navigation queue: 20,000 mixed enqueue/dequeue entries match priority queue including ties, NaNs and infinities");
 }
 static void QueryOnly()
 {
  var scene=new Scene("Query-only damage hitbox");var target=scene.CreateGameObject("Hitbox");var collider=target.AddComponent(new ByteEngine.Core.Characters.BoxCollider3D{IsTrigger=true,QueryOnly=true});var probe=target.AddComponent(new ContactProbe());target.AddComponent(new ByteEngine.Core.Gameplay.DamageRegion3D());
  var other=scene.CreateGameObject("Overlapping solid");other.AddComponent(new ByteEngine.Core.Characters.BoxCollider3D());
  scene.Physics.Step(scene,1f/60);
  if(scene.Physics.Contacts.Count!=0||scene.Physics.LastColliderCount!=1||probe.Hits!=0)throw new Exception("Query-only damage hitbox entered the physical solver");
  if(!ByteEngine.Core.Physics.GameplayQuery3D.RaycastWithDamageHits(scene,new(0,0,4),-System.Numerics.Vector3.UnitZ,out var hit,10,ignore:other,includeTriggers:false,includeDamageTriggers:true)||hit.GameObject!=target)throw new Exception("Query-only damage hitbox stopped receiving weapon traces");
  if(!ByteEngine.Core.Physics.GameplayQuery3D.SphereCast(scene,new(0,0,4),-System.Numerics.Vector3.UnitZ,.1f,out hit,10,ignore:other))throw new Exception("Query-only shape cast missed");
  collider.QueryOnly=false;scene.Physics.Step(scene,1f/60);if(scene.Physics.Contacts.Count!=1)throw new Exception("Re-enabled contact generation did not enter");
  collider.QueryOnly=true;scene.Physics.Step(scene,1f/60);if(scene.Physics.Contacts.Count!=0)throw new Exception("Query-only toggle retained stale contacts");
  foreach(string name in new[]{"BoxCollider3DCodec","CapsuleCollider3DCodec","MeshCollider3DCodec"})
  {
   var type=typeof(ByteEngine.Core.Serialization.ComponentSerializer).GetNestedType(name,BindingFlags.NonPublic)!;var codec=(ByteEngine.Core.Serialization.IComponentCodec)Activator.CreateInstance(type,true)!;
   var sample=(ByteEngine.Core.Characters.Collider3D)Activator.CreateInstance(codec.ComponentType)!;sample.QueryOnly=true;
   var data=codec.Serialize(sample,null!);if(((ByteEngine.Core.Characters.Collider3D)codec.Deserialize(data,null!)).QueryOnly!=true)throw new Exception("QueryOnly lost in "+name);
   data.Properties.Remove("queryOnly");if(((ByteEngine.Core.Characters.Collider3D)codec.Deserialize(data,null!)).QueryOnly)throw new Exception("Legacy collider became query-only in "+name);
  }
  Console.WriteLine("PASS query-only hitboxes: weapon/shape queries preserved, solver exclusion/toggle and collider serialization backward compatibility");
 }
 static void Snapshots()
 {
  var scene=new Scene("Component snapshots");var first=scene.CreateGameObject("First");var second=scene.CreateGameObject("Second");
  var a=first.AddComponent(new Rendered());var b=second.AddComponent(new InheritedRendered());
  var initial=scene.GetComponentsSnapshot<Rendered>();
  if(initial.Count!=2||initial[0]!=a||initial[1]!=b||!ReferenceEquals(initial,scene.GetComponentsSnapshot<Rendered>()))throw new Exception("Component snapshot lost inheritance, ordering or storage reuse");
  var all=scene.GetComponentsSnapshot<Component>();var ui=first.AddComponent(new UiWidget());
  if(!ReferenceEquals(initial,scene.GetComponentsSnapshot<Rendered>())||ReferenceEquals(all,scene.GetComponentsSnapshot<Component>()))throw new Exception("Unrelated component invalidation lost a typed snapshot or retained a stale base snapshot");
  first.RemoveComponent(ui);var empty=scene.CreateGameObject("Empty visual");empty.SetParent(first,false);var popup=empty.AddComponent(new UiWidget());empty.RemoveComponent(popup);scene.DestroyGameObject(empty);
  if(!ReferenceEquals(initial,scene.GetComponentsSnapshot<Rendered>()))throw new Exception("Unrelated visual object lifetime invalidated render component membership");
  var branch=scene.CreateGameObject("Model branch");branch.SetParent(first,false);long unaffected=second.RenderHierarchyVersion,before=branch.RenderHierarchyVersion;
  first.AddComponent(new ByteEngine.Core.Assets.ModelHierarchyInstance());
  if(branch.RenderHierarchyVersion<=before||second.RenderHierarchyVersion!=unaffected)throw new Exception("Hierarchy invalidation did not stay within the affected subtree");
  a.Enabled=false;first.Active=false;if(!ReferenceEquals(initial,scene.GetComponentsSnapshot<Rendered>())||initial[0].Enabled||initial[0].GameObject.Active)throw new Exception("Snapshot cached stale enabled/active values");
  second.MoveBefore(first);var ordered=scene.GetComponentsSnapshot<Rendered>();if(ordered[0]!=b||initial[0]!=a)throw new Exception("Snapshot mutation changed an old snapshot or lost reorder invalidation");
  second.RemoveComponent(b);if(scene.GetComponentsSnapshot<Rendered>().Count!=1||initial.Count!=2)throw new Exception("Removed component remained in current snapshot");
  scene.DestroyGameObject(first);if(scene.GetComponentsSnapshot<Rendered>().Count!=0)throw new Exception("Destroyed object remained in component snapshot");
  Console.WriteLine("PASS component snapshots: inheritance, immutable membership, live state, order, removal and destruction");
  var duplicateId=Guid.NewGuid();var duplicateA=new GameObject(duplicateId,"Duplicate A");var duplicateB=new GameObject(duplicateId,"Duplicate B");scene.AddGameObject(duplicateA);scene.AddGameObject(duplicateB);
  if(scene.FindGameObject(duplicateId)!=duplicateA)throw new Exception("Duplicate identity did not resolve first scene member");duplicateB.MoveBefore(duplicateA);
  if(scene.FindGameObject(duplicateId)!=duplicateB)throw new Exception("Duplicate identity order did not update");scene.DestroyGameObject(duplicateB);
  if(scene.FindGameObject(duplicateId)!=duplicateA)throw new Exception("Removed duplicate did not reveal next scene member");scene.DestroyGameObject(duplicateA);
  if(scene.FindGameObject(duplicateId)!=null)throw new Exception("Removed identity remained in live object lookup");
 }
 static void MatrixPackets()
 {
  using var mesh=new ByteEngine.Core.Graphics.ThreeD.Mesh([0,0,0,0,1,0,0,0,1,0,0,0,1,0,0,0,0,0,1,0,1,0,0,0],[0,1,2]);
  var material=new ByteEngine.Core.Graphics.ThreeD.Material{WetContact0=new(1,2,3,4),WetContact1=new(5,6,7,8),WetContact2=new(9,10,11,12),WetLevels=new(13,14,15,16)};
  var items=new List<ByteEngine.Core.Graphics.ThreeD.RenderSubmission>();
  for(int i=0;i<3;i++){var matrix=System.Numerics.Matrix4x4.CreateRotationY(i*.7f)*System.Numerics.Matrix4x4.CreateTranslation(i,-i,2*i);items.Add(new(mesh,material,matrix,mesh.LocalBounds,ByteEngine.Core.Graphics.ThreeD.RenderQueue3D.Opaque,true,i!=1,true,0,i));}
  var bytes=new byte[BrowserMatrixPacking.PacketLength(items)];BrowserMatrixPacking.WritePacket(bytes,items);
  float[] Fields(System.Numerics.Matrix4x4 m)=>[m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];
  var expected=new List<float>();expected.AddRange(Fields(items[0].ModelMatrix));expected.AddRange(Enumerable.Range(1,16).Select(i=>(float)i));foreach(var item in items)expected.AddRange(Fields(item.ModelMatrix));foreach(var item in items)if(item.CastShadows)expected.AddRange(Fields(item.ModelMatrix));
  if(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes)!=3||System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4))!=2||!bytes.AsSpan(8).SequenceEqual(System.Runtime.InteropServices.MemoryMarshal.AsBytes(expected.ToArray().AsSpan())))throw new Exception("Packed matrices changed the established frame bytes");
  for(int i=0;i<50;i++){BrowserMatrixPacking.PacketLength(items);BrowserMatrixPacking.WritePacket(bytes,items);}
  long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++){BrowserMatrixPacking.PacketLength(items);BrowserMatrixPacking.WritePacket(bytes,items);}long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
  if(allocated>256)throw new Exception("Matrix packets still allocate boxed enumerators: "+allocated);
  Console.WriteLine("PASS frame packets: identical headers/matrices/wet vectors/shadow instances; "+allocated+" bytes allocated in 1,000 packet writes");
 }
 static void RenderTransforms()
 {
  var scene=new Scene("Render transform cache");var parent=scene.CreateGameObject("Root");var middle=scene.CreateGameObject("Middle");var child=scene.CreateGameObject("Mesh");middle.SetParent(parent,false);child.SetParent(middle,false);
  parent.Transform.LocalPosition=new(2,3,4);parent.Transform.LocalScale=new(2,1,3);middle.Transform.LocalRotation=System.Numerics.Quaternion.CreateFromYawPitchRoll(.4f,.2f,.1f);child.Transform.LocalPosition=new(-1,2,.5f);
  for(int i=0;i<1000;i++)if(child.Transform.RenderMatrix!=child.Transform.WorldMatrix)throw new Exception("Non-interpolated render matrix differs from cached world matrix");
  parent.Transform.LocalPosition+=new System.Numerics.Vector3(1,0,0);if(child.Transform.RenderMatrix!=child.Transform.WorldMatrix)throw new Exception("Render cache did not observe parent movement");
  child.SetParent(parent,true);if(child.Transform.RenderMatrix!=child.Transform.WorldMatrix)throw new Exception("Render cache did not observe reparenting");
  var chain=new List<GameObject>{parent};
  for(int i=0;i<12;i++){var node=scene.CreateGameObject("Cached branch "+i);node.SetParent(chain[^1],false);node.Transform.LocalPosition=new(.2f,i*.1f,-.3f);chain.Add(node);}
  System.Numerics.Matrix4x4 Expected(GameObject node){var t=node.Transform;var local=System.Numerics.Matrix4x4.CreateScale(t.LocalScale)*System.Numerics.Matrix4x4.CreateFromQuaternion(t.LocalRotation)*System.Numerics.Matrix4x4.CreateTranslation(t.LocalPosition);return node.Parent is {} p?local*Expected(p):local;}
  void Check(){foreach(var node in chain){var actual=node.Transform.WorldMatrix;var expected=Expected(node);if(actual!=expected)throw new Exception("Dirty transform propagation lost an ancestor change: "+node.Name);}}
  Check();var random=new Random(731);
  for(int i=0;i<400;i++){int index=random.Next(chain.Count);var t=chain[index].Transform;t.LocalPosition=new(random.NextSingle(),random.NextSingle(),random.NextSingle());if(i%3==0)t.LocalRotation=System.Numerics.Quaternion.CreateFromYawPitchRoll(random.NextSingle(),random.NextSingle(),random.NextSingle());if(i%7==0)t.LocalScale=new(1+random.NextSingle(),.5f+random.NextSingle(),1+random.NextSingle());if(i%2==0)_=chain[^1].Transform.WorldPosition;Check();}
  chain[^1].SetParent(middle,false);Check();chain[^1].SetParent(parent,true);Check();
  Console.WriteLine("PASS render transforms: cached deep hierarchy, non-uniform scales, parent movement and reparenting");
 }
 static void Contacts()
 {
  var scene=new Scene("Contact dispatch");var a=scene.CreateGameObject("A");var b=scene.CreateGameObject("B");a.AddComponent(new Empty());var collider=a.AddComponent(new ByteEngine.Core.Characters.BoxCollider3D());var other=b.AddComponent(new ByteEngine.Core.Characters.BoxCollider3D());
  var probe=a.AddComponent(new ContactProbe());var inherited=a.AddComponent(new InheritedContactProbe());
  var contact=new ByteEngine.Core.Physics.PhysicsContact3D(a,collider,b,other,default,default,0,false);
  a.DispatchCollisionEnter(contact);a.DispatchTriggerEnter(contact);
  if(probe.Hits!=1||inherited.Hits!=1||new Empty().HasContactCallbacks||!inherited.HasContactCallbacks)throw new Exception("Contact dispatch lost overrides or inherited handlers");
  probe.Enabled=false;a.DispatchCollisionEnter(contact);if(probe.Hits!=1)throw new Exception("Disabled contact handler fired");
  probe.Enabled=true;a.DispatchCollisionEnter(contact);if(probe.Hits!=2)throw new Exception("Re-enabled contact handler did not fire");
  a.RemoveComponent(probe);a.DispatchCollisionEnter(contact);if(probe.Hits!=2)throw new Exception("Removed handler remained in the contact schedule");
  Console.WriteLine("PASS contacts: inherited handlers, enabled state and schedule invalidation; empty components omitted");
 }
 static void Ordering()
 {
  using var mesh=new ByteEngine.Core.Graphics.ThreeD.Mesh([0,0,0,0,1,0,0,0,1,0,0,0,1,0,0,0,0,0,1,0,1,0,0,0],[0,1,2]);
  var material=new ByteEngine.Core.Graphics.ThreeD.Material();var water=new ByteEngine.Core.Graphics.ThreeD.Material{WaterSurface=true};
  var input=new List<ByteEngine.Core.Graphics.ThreeD.RenderSubmission>();
  void Add(int i,ByteEngine.Core.Graphics.ThreeD.RenderQueue3D queue,float distance=0,bool wet=false)=>input.Add(new(mesh,wet?water:material,System.Numerics.Matrix4x4.Identity,mesh.LocalBounds,queue,true,true,true,distance,i));
  var opaque=ByteEngine.Core.Graphics.ThreeD.RenderQueue3D.Opaque;var transparent=ByteEngine.Core.Graphics.ThreeD.RenderQueue3D.Transparent;
  Add(0,opaque);Add(1,transparent,1);Add(2,opaque);Add(3,transparent,9);Add(4,opaque);Add(5,opaque,0,true);Add(6,ByteEngine.Core.Graphics.ThreeD.RenderQueue3D.Overlay);
  var ordered=input.Where(ByteEngine.Core.Graphics.ThreeD.PortableRenderOrdering.IsOpaque).ToList();var tail=input.Where(s=>!ByteEngine.Core.Graphics.ThreeD.PortableRenderOrdering.IsOpaque(s)).ToList();ByteEngine.Core.Graphics.ThreeD.PortableRenderOrdering.Complete(ordered,tail);
  if(!ordered.Select(s=>s.SubmissionIndex).SequenceEqual(new[]{0,2,4,3,1,5,6}))throw new Exception("Portable ordering changed opaque, transparency, water or overlay order");
  var batches=ByteEngine.Core.Graphics.ThreeD.RenderBatcher.Build(ordered);
  if(batches.Count!=5||!batches[0].Instances.Select(s=>s.SubmissionIndex).SequenceEqual(new[]{0,2,4})||!batches.Select(b=>b.First.SubmissionIndex).SequenceEqual(new[]{0,3,1,5,6}))throw new Exception("One-pass batching changed first occurrence or instance order");
  input.Reverse();Add(7,(ByteEngine.Core.Graphics.ThreeD.RenderQueue3D)1000);
  var reference=input.OrderBy(s=>s,Comparer<ByteEngine.Core.Graphics.ThreeD.RenderSubmission>.Create(ByteEngine.Core.Graphics.ThreeD.PortableRenderOrdering.Compare)).ToArray();ordered=input.Where(ByteEngine.Core.Graphics.ThreeD.PortableRenderOrdering.IsOpaque).ToList();tail=input.Where(s=>!ByteEngine.Core.Graphics.ThreeD.PortableRenderOrdering.IsOpaque(s)).ToList();ByteEngine.Core.Graphics.ThreeD.PortableRenderOrdering.Complete(ordered,tail);
  if(!ordered.SequenceEqual(reference))throw new Exception("Custom queues/unordered input lost full-sort fallback");
  var variants=new List<ByteEngine.Core.Graphics.ThreeD.RenderSubmission>();
  for(int i=0;i<72;i++){var m=new ByteEngine.Core.Graphics.ThreeD.Material{BaseColor=new(i%12*.05f,.3f,.5f,1)};variants.Add(new(mesh,m,System.Numerics.Matrix4x4.Identity,mesh.LocalBounds,opaque,true,true,i%5==0,0,i));}
  batches=ByteEngine.Core.Graphics.ThreeD.RenderBatcher.Build(variants);
  var expectedGroups=variants.GroupBy(s=>(s.Material.BaseColor,s.ReceiveShadows)).ToArray();
  if(batches.Count!=expectedGroups.Length)throw new Exception("Large material-variant fallback lost a batch");
  for(int i=0;i<batches.Count;i++)if(!batches[i].Instances.Select(s=>s.SubmissionIndex).SequenceEqual(expectedGroups[i].Select(s=>s.SubmissionIndex)))throw new Exception("Material variant fallback changed first occurrence or membership");
  Console.WriteLine("PASS rendering: one-pass batch membership/order, stable opaque order, transparency sorting, water/overlay and custom-queue fallback");
 }
 [MethodImpl(MethodImplOptions.NoInlining)]static WeakReference Probe(GameObject obj)
 {
  var assembly=AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("LookupProbe"+Guid.NewGuid().ToString("N")),AssemblyBuilderAccess.RunAndCollect);
  var builder=assembly.DefineDynamicModule("Probe").DefineType("PluginMarker",TypeAttributes.Public,typeof(Component));builder.DefineDefaultConstructor(MethodAttributes.Public);var type=builder.CreateType()!;
  var lookup=typeof(GameObject).GetMethod(nameof(GameObject.GetComponent))!.MakeGenericMethod(type);
  if(lookup.Invoke(obj,null)!=null||lookup.Invoke(obj,null)!=null)throw new Exception("Collectible miss lookup changed");
  var instance=(Component)Activator.CreateInstance(type)!;obj.AddComponent(instance);
  if(!ReferenceEquals(lookup.Invoke(obj,null),instance))throw new Exception("Collectible lookup not invalidated on add");
  obj.RemoveComponent(instance);if(lookup.Invoke(obj,null)!=null)throw new Exception("Collectible lookup not invalidated on removal");
  return new WeakReference(type);
 }
 private sealed class Empty:Component{}
 private sealed class DynamicOrder:Component{internal int? Value;public override int? RenderOrder=>Value;}
 private class ContactProbe:Component{internal int Hits;protected override void OnCollisionEnter(ByteEngine.Core.Physics.PhysicsContact3D contact){Hits++;}}
 private class ContactBase:Component{internal int Hits;protected override void OnTriggerEnter(ByteEngine.Core.Physics.PhysicsContact3D contact){Hits++;}}
 private sealed class InheritedContactProbe:ContactBase{}
 private class Rendered:Component{protected override void OnRender(RenderContext context){}}
 private sealed class InheritedRendered:Rendered{}
}
