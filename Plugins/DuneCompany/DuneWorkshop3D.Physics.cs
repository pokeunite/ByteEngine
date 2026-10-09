using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 LandVehiclePhysics? _physics;readonly Dictionary<int,VehiclePartPhysics> _physicsDefinitions=[];readonly Dictionary<int,Matrix4x4> _outputRest=[];readonly Dictionary<int,(GameObject Node,DuneBountyTarget3D Target,Vector3 Center)> _physicalTargets=[];
 public float SteeringJointAngle(int id){if(_physics==null||!_physicsDefinitions.TryGetValue(18,out var definition))return 0;Matrix4x4.Decompose(_physics.OutputDeformation(id),out _,out var q,out _);return 2*MathF.Atan2(Vector3.Dot(new(q.X,q.Y,q.Z),definition.Axis),q.W)*180/MathF.PI;}
 public float WheelAxisError(int id)=>_physics?.WheelAxisError(id)??0;
 float _traceTime;int _traceGeneration=-1;
 public string PhysicsDebug=>_physics?.DebugSnapshot()??"Build mode: physics inactive";
 public VehiclePartPhysics PhysicsDefinition(int type)=>_physicsDefinitions[type];
 public Vector3 PhysicalVelocity=>_physics?.Velocity??Vector3.Zero;
 void CaptureDefinition(PlacedBlock b,GameObject root,GameObject? output)
 {
  Matrix4x4.Invert(root.Transform.WorldMatrix,out var inverse);
  if(output!=null&&b.Id>=0)_outputRest[b.Id]=output.Transform.WorldMatrix*inverse;if(_physicsDefinitions.ContainsKey(b.Type))return;
  PartBounds? Bounds(GameObject obj){Vector3 low=new(float.MaxValue),high=new(float.MinValue);bool found=false;foreach(var node in Descendants(obj).Prepend(obj))foreach(var renderer in node.Components.OfType<MeshRenderer>()){if(renderer.Mesh==null)continue;var matrix=node.Transform.WorldMatrix*inverse;var vertices=renderer.Mesh.VertexData.ToArray();for(int i=0;i<vertices.Length;i+=8){var point=Vector3.Transform(new Vector3(vertices[i],vertices[i+1],vertices[i+2]),matrix);low=Vector3.Min(low,point);high=Vector3.Max(high,point);found=true;}}return found?new(low,high):null;}
  var def=_catalog[b.Type];var fixedNode=Descendants(root).FirstOrDefault(o=>o.Name=="Fixed_Body");var fixedBounds=fixedNode==null?new PartBounds(def.Low,def.High):Bounds(fixedNode)??new PartBounds(new(-.06f,-.06f,.01f),new(.06f,.06f,.075f));
  var moving=output==null?default:Bounds(output)??new PartBounds(def.Low,def.High);Vector3 pivot=output==null?Vector3.Zero:Vector3.Transform(output.Transform.WorldMatrix.Translation,inverse);Vector3 axis=b.Type is >=13 and <=15 or 21 or 22 or 18?Vector3.UnitZ:Vector3.UnitY;if(b.Type is 16 or 17 or 21 or 32)axis=-axis;
  _physicsDefinitions[b.Type]=new(fixedBounds,moving,pivot,axis,output!=null);if(output!=null&&b.Id>=0)_outputRest[b.Id]=output.Transform.WorldMatrix*inverse;
 }
 // Convert world matrices to local matrices explicitly. The engine's WorldRotation
 // accessor composes parent quaternions in a different order from WorldMatrix.
 // Using it on a tilted chassis conjugates the wheel axis and makes the mesh tumble.
 static void SetRenderedWorldPose(GameObject node,Matrix4x4 world)
 {
  var local=world;if(node.Parent!=null){if(!Matrix4x4.Invert(node.Parent.Transform.WorldMatrix,out var inverse))return;local=world*inverse;}
  if(Matrix4x4.Decompose(local,out var scale,out var rotation,out var position)){node.Transform.LocalPosition=position;node.Transform.LocalRotation=rotation;node.Transform.LocalScale=scale;}
 }
 public float RenderedWheelAxisError(int id)
 {
  if(_physics==null||!_outputs.TryGetValue(id,out var output)||!_outputRest.TryGetValue(id,out var rest))return 0;
  Matrix4x4.Invert(rest,out var inverse);var rendered=Vector3.Normalize(Vector3.TransformNormal(_physicsDefinitions[_blocks.Single(b=>b.Id==id).Type].Axis,inverse*output.node.Transform.WorldMatrix));
  var pose=_physics.RenderPartPose(id,true);var expected=Vector3.Normalize(Vector3.Transform(_physicsDefinitions[_blocks.Single(b=>b.Id==id).Type].Axis,pose.Rotation));return MathF.Acos(Math.Clamp(Vector3.Dot(rendered,expected),-1,1))*180/MathF.PI;
 }
 void StartPhysics()
 {
  StopPhysics();PrepareRecoveryVisuals();_physics=new(_blocks,_catalog,_physicsDefinitions,Transform.WorldPosition,Transform.WorldRotation,_sand?.Heightfield);_physics.SetSandPatch(_sand?.EnablePatch(Transform.WorldPosition));StartRecoveryPhysics();_physicalTargets.Clear();_traceTime=0;ConstructionDiagnostics.Record("DUNE PHYSICS SETUP",_physics.DebugSnapshot());
  foreach(var obstacle in GameObject.Scene!.GameObjects.Where(o=>o.ActiveInHierarchy).SelectMany(o=>o.Components).OfType<DuneWorldObstacle3D>().Where(c=>c.Enabled)){
   Matrix4x4.Decompose(obstacle.Transform.WorldMatrix,out var scale,out var rotation,out _);
   if(!float.IsFinite(obstacle.Size.LengthSquared())||obstacle.Size.X<=0||obstacle.Size.Y<=0||obstacle.Size.Z<=0)continue;
   _physics.AddObstacle(obstacle.GameObject.Id.GetHashCode(),Vector3.Transform(obstacle.Center,obstacle.Transform.WorldMatrix),Vector3.Abs(scale)*obstacle.Size,rotation);
  }
  foreach(var target in GameObject.Scene!.GameObjects.SelectMany(o=>o.Components).OfType<DuneBountyTarget3D>()){
   if(target.Defeated||!target.GameObject.ActiveInHierarchy)continue;foreach(var obj in target.GameObject.Children){var id=obj.Id.GetHashCode();Matrix4x4.Invert(obj.Transform.WorldMatrix,out var inverse);Vector3 low=new(float.MaxValue),high=new(float.MinValue);bool found=false;foreach(var node in Descendants(obj).Prepend(obj))foreach(var r in node.Components.OfType<MeshRenderer>()){if(r.Mesh==null)continue;var matrix=node.Transform.WorldMatrix*inverse;var vertices=r.Mesh.VertexData.Span;for(int i=0;i<vertices.Length;i+=8){var point=Vector3.Transform(new Vector3(vertices[i],vertices[i+1],vertices[i+2]),matrix);low=Vector3.Min(low,point);high=Vector3.Max(high,point);found=true;}}var center=found?(low+high)*.5f:Vector3.Zero;var size=found?high-low:Vector3.One;Matrix4x4.Decompose(obj.Transform.WorldMatrix,out var scale,out var rotation,out _);_physics.AddObstacle(id,Vector3.Transform(center,obj.Transform.WorldMatrix),Vector3.Abs(scale)*size,rotation);_physicalTargets[id]=(obj,target,center);}
  }
 }
 void StopPhysics(){ClearRecoveryVisuals();_physics?.Dispose();_physics=null;}
 void RecoverPhysics(bool atBase=true){if(_contractRun&&_physics?.HasRecovery==true&&!atBase){ResetRecoveryPair();return;}var recover=atBase?_garage:Transform.WorldPosition;if(!atBase&&_sand!=null){var bounds=_sand.Heightfield.WorldTerrainBounds;float marginX=Math.Min(20,(bounds.Maximum.X-bounds.Minimum.X)*.08f),marginZ=Math.Min(20,(bounds.Maximum.Z-bounds.Minimum.Z)*.08f);recover.X=Math.Clamp(recover.X,bounds.Minimum.X+marginX,bounds.Maximum.X-marginX);recover.Z=Math.Clamp(recover.Z,bounds.Minimum.Z+marginZ,bounds.Maximum.Z-marginZ);if(_sand.Sample(recover,out var ground,out _,out _,out _))recover.Y=ground.Y+1.4f;}StopPhysics();Transform.WorldPosition=recover;Transform.WorldRotation=Quaternion.Identity;_yaw=Speed=0;BuildVisuals();if(!Building)StartPhysics();_cameraReady=false;}
 float _sandStampTimer;
 public void StepDrive(float throttle,float steer,bool brake,float dt,bool handbrake=false)
 {
  if(Building||_physics==null)return;_sand?.ResetSampleMetrics();_sand?.FocusPatch(Transform.WorldPosition);_feedbackThrottle=throttle;_feedbackBrake=brake||handbrake;foreach(var entry in _physicalTargets.ToArray()){if(entry.Value.Target.Defeated){_physics.RemoveObstacle(entry.Key);_physicalTargets.Remove(entry.Key);}else{var node=entry.Value.Node;Matrix4x4.Decompose(node.Transform.WorldMatrix,out _,out var rotation,out _);_physics.MoveObstacle(entry.Key,Vector3.Transform(entry.Value.Center,node.Transform.WorldMatrix),rotation);}}
  float Resistance(Vector3 pos)=>_sand!=null&&_sand.Sample(pos,out _,out _,out _,out float resistance)?resistance:0;
  float Grip(Vector3 pos)=>_sand!=null&&_sand.Sample(pos,out _,out _,out float grip,out _)?grip:.65f;
  _physics.Step(dt,throttle,steer,brake,MaximumSpeed,(Input.IsKeyDown(Key.E)?1:0)-(Input.IsKeyDown(Key.Q)?1:0),Input.IsKeyDown(Key.E)?1:0,Grip,handbrake,Resistance);
  _sandStampTimer+=dt;float stampDt=_sandStampTimer>=.05f?Math.Min(_sandStampTimer,.1f):0;if(stampDt>0)_sandStampTimer=0;
  var frame=_physics.Frame;if(_sand!=null){var bounds=_sand.Heightfield.WorldTerrainBounds;float marginX=Math.Min(20,(bounds.Maximum.X-bounds.Minimum.X)*.08f),marginZ=Math.Min(20,(bounds.Maximum.Z-bounds.Minimum.Z)*.08f);if(frame.Position.X<bounds.Minimum.X+marginX*.9f||frame.Position.X>bounds.Maximum.X-marginX*.9f||frame.Position.Z<bounds.Minimum.Z+marginZ*.9f||frame.Position.Z>bounds.Maximum.Z-marginZ*.9f){Transform.WorldPosition=frame.Position;RecoverPhysics(false);return;}}var renderFrame=_physics.RenderFrame;SetRenderedWorldPose(GameObject,Pose(renderFrame.Position,renderFrame.Rotation));_yaw=MathF.Atan2(-Transform.Forward.X,-Transform.Forward.Z);Speed=Vector3.Dot(_physics.Velocity,Transform.Forward);
  foreach(var b in _blocks){var pose=_physics.RenderPartPose(b.Id);var visual=_visuals[b.Id];SetRenderedWorldPose(visual,Pose(pose.Position,pose.Rotation));_poses[b.Id]=Pose(visual.Transform.LocalPosition,visual.Transform.LocalRotation);
   if(_outputs.TryGetValue(b.Id,out var output)&&_outputRest.TryGetValue(b.Id,out var rest)){var matrix=rest*_physics.RenderOutputDeformation(b.Id)*visual.Transform.WorldMatrix;SetRenderedWorldPose(output.node,matrix);}
   UpdateSuspensionVisual(b,visual);
   if(stampDt>0&&_catalog[b.Type].Wheel&&_sand!=null){var feedback=_physics.WheelFeedback(b.Id);if(feedback.Grounded&&_sand.Sample(feedback.Contact,out var ground,out _,out _,out _)){if(_contacts.TryGetValue(b.Id,out var prior)){float load=_blocks.Sum(part=>_catalog[part.Type].Mass)*9.81f/Math.Max(1,_blocks.Count(part=>_catalog[part.Type].Wheel));if(_sand.Wheel(prior,ground,Math.Clamp(_physicsDefinitions[b.Type].Moving.Size.Z,.25f,.8f),load,feedback.Slip,stampDt))_physics.MarkTerrainChanged(ground);}_contacts[b.Id]=ground;}else _contacts.Remove(b.Id);}
  }
  if(ConstructionDiagnostics.Enabled){_traceTime+=dt;if(_traceGeneration!=ConstructionDiagnostics.Generation||_traceTime>=.25f){_traceGeneration=ConstructionDiagnostics.Generation;_traceTime=0;ConstructionDiagnostics.Record("DUNE LAND PHYSICS",$"throttle={throttle} steering={steer} brake={brake}\n"+_physics.DebugSnapshot(),true);}}
 }
}
