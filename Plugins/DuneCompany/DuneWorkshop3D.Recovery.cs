using System.Numerics;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 DuneRecoveryContract3D? _recoveryJob;bool _recoveryJobResolved;
 DuneRecoveryContract3D? RecoveryJob{get{if(!_recoveryJobResolved){_recoveryJobResolved=true;_recoveryJob=GameObject.Scene?.GameObjects.SelectMany(o=>o.Components).OfType<DuneRecoveryContract3D>().FirstOrDefault();}return _recoveryJob;}}
 Vector3 RecoverySpawn=>RecoveryJob?.Transform.WorldPosition??_garage+new Vector3(0,0,24);
 Vector3 DeliveryCenter {get {var p=_garage;if(_sand?.Sample(p,out var ground,out _,out _,out _)==true)p.Y=ground.Y+.035f;return p;}}
 bool DeliveryReady(Vector3 center){float half=RecoveryJob?.DeliveryHalfWidth??4.5f;var a=RecoveryPosition-center;var b=Transform.WorldPosition-center;return Math.Abs(a.X)<half&&Math.Abs(a.Z)<half&&Math.Abs(a.Y)<2&&Math.Abs(b.X)<half&&Math.Abs(b.Z)<half&&Math.Abs(b.Y)<3&&_physics!.RecoveryVelocity.Length()<.8f&&_physics.Velocity.Length()<.8f;}
 int JobPayment=>Math.Clamp(RecoveryJob?.Payment??250,1,100000);
 readonly List<PlacedBlock> _recoveryBlocks=[];readonly Dictionary<int,(GameObject root,GameObject? output,Matrix4x4 rest)> _recoveryVisuals=[];
 GameObject? _recoveryRoot,_hitchMarker,_truckHitchMarker,_deliveryMarker,_towLink;int _candidateHinge=-1;int _credits;float _deliveryHold;
 public bool RecoveryConnected=>_physics?.RecoveryConnected==true;
 public Vector3 RecoveryPosition=>_physics?.HasRecovery==true?_physics.RecoveryFrame().Position:_garage+new Vector3(0,0,24);
 public Vector3 RecoveryTruckHitch=>_candidateHinge>=0&&_physics!=null?_physics.TrailerHitch(_candidateHinge):Vector3.Zero;
 public int RecoveryPayment=>_credits;
 public string RecoveryObjective {get;private set;}="";
 public Vector3 RecoveryHitchPosition=>_physics?.HasRecovery==true?_physics.TargetHitch:Vector3.Zero;
 public string RecoveryConnectionHint {get;private set;}="";
 void PrepareRecoveryVisuals()
 {
  if(!_contractRun)return;var scene=GameObject.Scene!;foreach(var sand in scene.GameObjects.SelectMany(o=>o.Components).OfType<ByteEngine.Core.Gameplay.InteractiveSand3D>()){if(Math.Abs(sand.AuthoredDepressionDepth-Math.Clamp(RecoveryJob?.DepressionDepth??.24f,0,WinchMission?2.5f:.3f))>.001f||Vector2.Distance(sand.AuthoredDepressionCenter,new(RecoverySpawn.X,RecoverySpawn.Z))>.01f){sand.AuthoredDepressionCenter=new(RecoverySpawn.X,RecoverySpawn.Z);sand.AuthoredDepressionDepth=Math.Clamp(RecoveryJob?.DepressionDepth??.24f,0,WinchMission?2.5f:.3f);sand.AuthoredDepressionRadius=Math.Clamp(RecoveryJob?.DepressionRadius??5,2,12);sand.ResetTracks();}}_recoveryRoot=scene.CreateGameObject("Stranded recovery buggy");_recoveryBlocks.Clear();
  void Add(int type,Vector3 position,Quaternion rotation){var b=new PlacedBlock{Id=_recoveryBlocks.Count,Type=type,Parent=_recoveryBlocks.Count==0?-1:0,Power=0};b.Pose(position,rotation);_recoveryBlocks.Add(b);var root=Model(scene,Assets!,_catalog[type].File,_recoveryRoot,"Buggy "+b.Id+" - "+_catalog[type].Label);root.Transform.LocalPosition=position;root.Transform.LocalRotation=rotation;var output=Descendants(root).FirstOrDefault(o=>o.Name==OutputName(type));Matrix4x4.Invert(root.Transform.WorldMatrix,out var inverse);var rest=output==null?Matrix4x4.Identity:output.Transform.WorldMatrix*inverse;CaptureDefinition(new(){Id=-1,Type=type},root,output);_recoveryVisuals[b.Id]=(root,output,rest);}
  Add(3,Vector3.Zero,Quaternion.Identity);Add(25,new(0,.5f,-.45f),Quaternion.Identity);Add(1,new(0,.5f,.5f),Quaternion.Identity);
  foreach(int z in new[]{-1,1})foreach(int x in new[]{-1,1})Add(13,new(x*.58f,.04f,z*.75f),Quaternion.CreateFromAxisAngle(Vector3.UnitY,-x*MathF.PI*.5f));
  _hitchMarker=RecoveryPrimitive("Buggy compatible hitch",new(0,.22f,-1.4f),new(.19f,.19f,.19f),new(.2f,1,.7f,1),_recoveryRoot);
  RecoveryPrimitive("Buggy drawbar",new(0,.22f,-1.15f),new(.10f,.10f,.5f),new(.35f,.32f,.25f,1),_recoveryRoot);
  _truckHitchMarker=RecoveryPrimitive("Trailer hinge interaction point",Vector3.Zero,new(.145f),new(1,.7f,.2f,1));
  _deliveryMarker=RecoveryPrimitive("Delivery zone",DeliveryCenter,new((RecoveryJob?.DeliveryHalfWidth??4.5f)*2,.035f,(RecoveryJob?.DeliveryHalfWidth??4.5f)*2),new(.2f,.7f,.45f,.22f));
  _towLink=RecoveryPrimitive("Recovery coupling link",Vector3.Zero,new(.045f,.045f,.1f),new(.8f,.6f,.2f,1));_towLink.Active=false;
  if(WinchMission){_deliveryMarker!.Transform.WorldPosition=WinchDelivery;_deliveryMarker.Transform.LocalScale=new(8,.035f,8);PrepareWinchArena();}
  _deliveryMarker!.Active=false; // Delivery remains a logical zone, without the coloured ground slab.
  _deliveryHold=0;_winchExtracted=false;_winchWorkedAtPit=false;var file=Path.Combine(SaveRoot,"recovery-payment.json");if(File.Exists(file)){try{_credits=JsonSerializer.Deserialize<int>(File.ReadAllText(file));}catch(JsonException){_credits=0;}}
 }
 GameObject RecoveryPrimitive(string name,Vector3 position,Vector3 size,Vector4 color,GameObject? parent=null){var o=GameObject.Scene!.CreateGameObject(name);if(parent!=null)o.SetParent(parent,false);o.Transform.LocalPosition=position;o.Transform.LocalScale=size;o.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Cube,CastShadows=false,Material=new(){BaseColor=color,Shading=MaterialShadingMode.Unlit,BlendMode=color.W<1?BlendMode3D.AlphaBlend:BlendMode3D.Opaque}});return o;}
 void StartRecoveryPhysics(){if(!_contractRun||_physics==null)return;var spawn=RecoverySpawn;if(_sand!=null&&_sand.Sample(spawn,out var ground,out _,out _,out _))spawn.Y=ground.Y+.56f;_physics.AddRecoveryVehicle(_recoveryBlocks,_physicsDefinitions,spawn,WinchMission?Quaternion.CreateFromAxisAngle(Vector3.UnitX,.3f):Quaternion.Identity,new(0,.22f,-1.4f));}
 void ClearRecoveryVisuals(){ClearWinchArena();foreach(var o in new[]{_recoveryRoot,_truckHitchMarker,_deliveryMarker,_towLink})if(o?.Scene!=null)o.Scene.DestroyGameObject(o);_recoveryRoot=_hitchMarker=_truckHitchMarker=_deliveryMarker=_towLink=null;_recoveryVisuals.Clear();}
 public bool ConnectRecovery(){return !Building&&_contractRun&&!_contractComplete&&_candidateHinge>=0&&_physics?.ConnectRecovery(_candidateHinge)==true;}
 public void DisconnectRecovery()=>_physics?.DisconnectRecovery();
 void ResetRecoveryPair(){if(_physics==null)return;_physics.DisconnectWinch();_physics.DisconnectRecovery();var truck=Transform.WorldPosition;var buggy=RecoveryPosition;if(Vector3.Distance(truck,buggy)<5)buggy=truck+new Vector3(4,0,4);if(WinchMission){truck=RecoverySpawn+new Vector3(0,0,-10);buggy=RecoverySpawn;}
 Vector3 Lift(Vector3 p){if(_sand!=null){var bounds=_sand.Heightfield.WorldTerrainBounds;p.X=Math.Clamp(p.X,bounds.Minimum.X+8,bounds.Maximum.X-8);p.Z=Math.Clamp(p.Z,bounds.Minimum.Z+8,bounds.Maximum.Z-8);}if(_sand!=null&&_sand.Sample(p,out var g,out _,out _,out _))p.Y=g.Y+1.2f;else p.Y=1.2f;return p;}_physics.ResetTruck(Lift(truck),Quaternion.Identity);_physics.ResetRecovery(Lift(buggy),WinchMission?Quaternion.CreateFromAxisAngle(Vector3.UnitX,.3f):Quaternion.Identity);_cameraReady=false;_message="Vehicles righted. Hitch disconnected; reposition and reconnect.";}
 void UpdateRecovery()
 {
  if(Building||!_contractRun||_physics?.HasRecovery!=true){GarageActive("Recovery marker label",false);GarageActive("Recovery interaction",false);return;}
  foreach(var b in _recoveryBlocks){var visual=_recoveryVisuals[b.Id];var pose=_physics.RenderPartPose(b.Id+LandVehiclePhysics.RecoveryRootId);SetRenderedWorldPose(visual.root,Pose(pose.Position,pose.Rotation));if(visual.output!=null)SetRenderedWorldPose(visual.output,visual.rest*_physics.RenderOutputDeformation(b.Id+LandVehiclePhysics.RecoveryRootId)*visual.root.Transform.WorldMatrix);}
  var frame=_physics.RecoveryFrame(true);SetRenderedWorldPose(_hitchMarker!,Matrix4x4.CreateScale(.16f)*Pose(frame.Position+Vector3.Transform(new Vector3(0,.22f,-1.4f),frame.Rotation),frame.Rotation));
  if(WinchMission){UpdateWinchMission();return;}
  _candidateHinge=_blocks.Where(b=>b.Type==20).OrderBy(b=>Vector3.DistanceSquared(_physics.TrailerHitch(b.Id),_physics.TargetHitch)).Select(b=>b.Id).DefaultIfEmpty(-1).First();
  if(_truckHitchMarker!=null){_truckHitchMarker.Active=_candidateHinge>=0;if(_candidateHinge>=0)_truckHitchMarker.Transform.WorldPosition=_physics.TrailerHitch(_candidateHinge);}
  string available=_candidateHinge<0?"Fit a trailer hinge in the garage":_physics.HitchAvailability(_candidateHinge);RecoveryConnectionHint=RecoveryConnected?"CONNECTED  /  H disconnect":available=="Ready"?"H connect hitch":available;
  if(_towLink!=null){_towLink.Active=RecoveryConnected;if(RecoveryConnected){var a=_physics.TrailerHitch(_candidateHinge);var b=_physics.TargetHitch;var delta=b-a;_towLink.Transform.WorldPosition=(a+b)*.5f;_towLink.Transform.WorldRotation=FromTo(Vector3.UnitZ,delta.LengthSquared()>.00001f?Vector3.Normalize(delta):Vector3.UnitZ);_towLink.Transform.LocalScale=new(.045f,.045f,Math.Max(.045f,delta.Length()));}}
  if(!_contractComplete&&Input.IsKeyPressed(Key.H)){if(RecoveryConnected)DisconnectRecovery();else ConnectRecovery();}
  if(!_contractComplete){bool delivered=DeliveryReady(DeliveryCenter)&&RecoveryConnected;_deliveryHold=delivered?_deliveryHold+(float)Time.DeltaTime:0;if(_deliveryHold>.75f){_contractComplete=true;RecordContractCompletion();_credits+=JobPayment;Directory.CreateDirectory(SaveRoot);var file=Path.Combine(SaveRoot,"recovery-payment.json");File.WriteAllText(file+".tmp",JsonSerializer.Serialize(_credits));File.Move(file+".tmp",file,true);DisconnectRecovery();}}
  RecoveryObjective=_contractComplete?$"RECOVERY COMPLETE  +${JobPayment}  /  Balance ${_credits}":RecoveryConnected?"Tow the buggy into the green delivery area":"Recover the stranded buggy / align the amber hinge with its green hitch";
  Text("Battle objective",RecoveryObjective);Text("Recovery interaction",_contractComplete?"Job paid. Return to garage to restart.":RecoveryConnectionHint+"  /  R reset both vehicles");GarageActive("Recovery interaction",true);
  Vector3 marker=RecoveryConnected?_garage:RecoveryHitchPosition;ProjectRecoveryMarker(marker+(RecoveryConnected?Vector3.UnitY*.5f:Vector3.UnitY*1.2f),RecoveryConnected?"DELIVERY":"STRANDED BUGGY");
 }
 void ProjectRecoveryMarker(Vector3 point,string label){if(_camera==null||!_ui.TryGetValue("Recovery marker label",out var obj))return;var clip=Vector4.Transform(Vector4.Transform(new Vector4(point,1),_camera.GetViewMatrix()),_camera.GetProjectionMatrix(Input.GameViewSize.X/Math.Max(1,Input.GameViewSize.Y)));var text=obj.GetComponent<UiText>()!;var normalized=clip.W>0?new Vector2((clip.X/clip.W+1)*.5f,(1-clip.Y/clip.W)*.5f):new Vector2(.5f,.15f);float scale=Math.Min(Input.GameViewSize.X/1280,Input.GameViewSize.Y/720);text.Offset=new Vector2(Math.Clamp(normalized.X,.2f,.8f),Math.Clamp(normalized.Y,.13f,.8f))*Input.GameViewSize/Math.Max(.1f,scale);text.Text=$"{label}{(clip.W<=0?" / BEHIND YOU":"")}  {Vector3.Distance(Transform.WorldPosition,point):0} m";obj.Active=!_contractComplete;}
}
