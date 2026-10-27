using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningCampLayout3D
{
 public bool SuppressWeaponFire=>IsEditing||Math.Abs(ByteEngine.Core.Time.TotalTime-committedAt)<.000001;
 BoundingBox3D WorldBounds(GameObject o,Vector3 position,float yaw)
 {
  if(!bounds.TryGetValue(o.Id,out var local))bounds[o.Id]=local=MiningCampGeometry.LocalBounds(o);
  Matrix4x4.Decompose(o.Transform.WorldMatrix,out var scale,out _,out _);
  return MiningCampGeometry.Transform(local,Matrix4x4.CreateScale(scale)*Matrix4x4.CreateRotationY(yaw*MathF.PI/180)*Matrix4x4.CreateTranslation(position));
 }
 static bool Contains(BoundingBox3D b,Vector3 p,float margin=0)=>p.X>=b.Minimum.X-margin&&p.X<=b.Maximum.X+margin&&p.Z>=b.Minimum.Z-margin&&p.Z<=b.Maximum.Z+margin;
 static bool Intersects(BoundingBox3D a,BoundingBox3D b,float margin)=>a.Minimum.X-margin<b.Maximum.X&&a.Maximum.X+margin>b.Minimum.X&&a.Minimum.Z-margin<b.Maximum.Z&&a.Maximum.Z+margin>b.Minimum.Z;
 bool Validate(GameObject root,Vector3 position,float yaw,out string reason)
 {
  reason="";if(!float.IsFinite(position.X)||!float.IsFinite(position.Z)||!float.IsFinite(yaw)){reason="Choose a position on the camp floor.";return false;}
  var box=WorldBounds(root,position,yaw);float clearance=Math.Clamp(root.GetComponent<MiningCampObject3D>()?.Clearance??.3f,0,2);
  if(box.Minimum.X<BuildMinimum.X||box.Maximum.X>BuildMaximum.X||box.Minimum.Z<BuildMinimum.Y||box.Maximum.Z>BuildMaximum.Y){reason="Keep the whole object inside the camp.";return false;}
  foreach(var other in Objects.Where(o=>o!=root))if(Intersects(box,WorldBounds(other,other.Transform.WorldPosition,Yaw(other)),clearance)){reason="Leave space between camp objects.";return false;}
  if(Contains(box,Transform.WorldPosition,.8f)||GameObject.Scene!.FindGameObject(SpawnClearanceObjectId) is {} spawn&&Contains(box,spawn.Transform.WorldPosition,Math.Max(.5f,SpawnClearance))){reason="Keep the player and arrival area clear.";return false;}
  var movable=Objects.ToArray();
  foreach(var o in GameObject.Scene!.GameObjects.Where(o=>o.ActiveInHierarchy&&o.Layer!=20&&!MiningCampGeometry.Under(o,GameObject)&&!movable.Any(m=>MiningCampGeometry.Under(o,m))))
   foreach(var collider in o.Components.OfType<Collider3D>().Where(c=>c.Enabled&&!c.IsTrigger))
   {var obstacle=MiningCampGeometry.ColliderBounds(collider);if(obstacle.Maximum.Y<=GroundHeight+.15f||obstacle.Minimum.Y>GroundHeight+3)continue;if(Intersects(box,obstacle,clearance)){reason="Leave space around "+o.Name+".";return false;}}
  return true;
 }
 public bool ConfirmMove()
 {
  if(!IsEditing||Selected is not {} root)return false;
  if(!Validate(root,PreviewPosition,PreviewYaw,out var reason)){Status=reason;return false;}
  var old=root.Transform.WorldPosition;var rotation=root.Transform.WorldRotation;
  root.Transform.WorldPosition=PreviewPosition;root.Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,PreviewYaw*MathF.PI/180);
  var region=GameObject.Scene!.FindGameObject(NavigationObjectId)?.GetComponent<NavigationRegion3D>();region?.Bake();
  bool reachable=region==null||Op is not {} op||GameObject.Scene.FindGameObject(op.DrillObjectId) is not {} drill||GameObject.Scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningSpawnPoint3D>().All(s=>region.FindPath(s.Transform.WorldPosition,drill.Transform.WorldPosition,20000).Count>0);
  bool saved=reachable&&(Planner?.Owns(root)==true?Planner.SaveRelocation(root):SaveLayout());
  if(!saved){root.Transform.WorldPosition=old;root.Transform.WorldRotation=rotation;region?.Bake();Status=reachable?"Could not save this move; the original position was restored.":"Keep an enemy route open to the drill.";PreviewValid=false;return false;}
  committedAt=ByteEngine.Core.Time.TotalTime;CancelMove();Status="Position saved. Choose another object or press Tab to finish.";return true;
 }
 string LayoutPath=>Path.Combine(GameSaveStorage.GetDirectory(GameObject.Scene!.ProjectRoot!),Path.GetFileName(SaveSlot));
 public bool SaveLayout()
 {
  if(!PersistProgress)return true;
  try{if(Path.GetFileName(SaveSlot)!=SaveSlot)throw new IOException("Camp save must be a filename.");Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath)!);File.WriteAllText(LayoutPath+".tmp",JsonSerializer.Serialize(new LayoutState{Objects=Objects.Where(o=>o.GetComponent<MiningCampObject3D>()!=null).Select(o=>new Pose{Id=o.Id,X=o.Transform.WorldPosition.X,Y=o.Transform.WorldPosition.Y,Z=o.Transform.WorldPosition.Z,Yaw=Yaw(o)}).ToArray()}));File.Move(LayoutPath+".tmp",LayoutPath,true);return true;}
  catch(Exception e)when(e is IOException or UnauthorizedAccessException){Status=e.Message;return false;}
 }
 public void LoadLayout()
 {
  try
  {
   if(Path.GetFileName(SaveSlot)!=SaveSlot||!File.Exists(LayoutPath))return;var state=JsonSerializer.Deserialize<LayoutState>(File.ReadAllText(LayoutPath));if(state?.Version!=1)return;
   // Apply the complete saved layout together: swapping two machines must not depend on load order.
   var originals=new List<(GameObject Root,Vector3 Position,Quaternion Rotation)>();
   foreach(var p in (state.Objects??[]).Take(64))if(GameObject.Scene!.FindGameObject(p.Id) is {} o&&o.GetComponent<MiningCampObject3D>() is {AllowRelocation:true}&&float.IsFinite(p.X)&&float.IsFinite(p.Z)&&float.IsFinite(p.Yaw))
   {originals.Add((o,o.Transform.WorldPosition,o.Transform.WorldRotation));o.Transform.WorldPosition=new(p.X,GroundHeight,p.Z);o.Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,NormalizeYaw(p.Yaw)*MathF.PI/180);}
   // Bounds/overlap checks ignore the current player during restore; the reserved arrival point is protected at placement time.
   bool valid=originals.All(p=>{var b=WorldBounds(p.Root,p.Root.Transform.WorldPosition,Yaw(p.Root));return b.Minimum.X>=BuildMinimum.X&&b.Maximum.X<=BuildMaximum.X&&b.Minimum.Z>=BuildMinimum.Y&&b.Maximum.Z<=BuildMaximum.Y;});
   if(!valid){foreach(var p in originals){p.Root.Transform.WorldPosition=p.Position;p.Root.Transform.WorldRotation=p.Rotation;}Status="Saved layout no longer fits this camp; using authored positions.";}
   GameObject.Scene!.FindGameObject(NavigationObjectId)?.GetComponent<NavigationRegion3D>()?.Bake();
  }catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException){Status="Could not restore camp layout: "+e.Message;}
 }
 public sealed class LayoutState{public int Version{get;set;}=1;public Pose[]? Objects{get;set;}}
 public sealed class Pose{public Guid Id{get;set;}public float X{get;set;}public float Y{get;set;}public float Z{get;set;}public float Yaw{get;set;}}
}
