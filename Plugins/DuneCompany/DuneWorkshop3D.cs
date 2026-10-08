using System.Numerics;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace DuneCompany;
/// <summary>Jam workshop with rigid-grid construction and a wheel-contact driving prototype.</summary>
public sealed partial class DuneWorkshop3D:Component
{
 string SaveRoot=>ByteEngine.Core.Runtime.GameSaveStorage.GetDirectory(ProjectRoot);
 internal AssetManager? Assets;internal string ProjectRoot="";
 public string BuildJson{get;set;}="";public float MaximumSpeed{get;set;}=32;
 public bool Building{get;private set;}=true;public float Speed{get;private set;}
 public IReadOnlyList<PlacedBlock> Blocks=>_blocks;public IReadOnlyDictionary<int,DunePart> Catalog=>_catalog;
 readonly Dictionary<int,DunePart> _catalog=[];List<PlacedBlock> _blocks=[];readonly Dictionary<int,GameObject> _visuals=[];
 readonly Stack<string> _undo=[],_redo=[];SandBridge? _sand;Camera3D? _camera;GameObject? _ghost;Vector3 _garage;
 int _type=1,_category,_page,_hover=-1,_selected=-1,_twist,_pitchSteps,_rollSteps,_face,_move=-1;string _tool="Place",_message="Choose a block, then click a surface.";
 float _yaw,_orbit=.65f,_elevation=.5f,_zoom=8;Vector3 _candidate;Quaternion _candidateQ=Quaternion.Identity;bool _valid,_candidateMoving,_manualFace;readonly Dictionary<int,Vector3> _contacts=[];
 static readonly Vector3[] Normals=[-Vector3.UnitY,Vector3.UnitY,Vector3.UnitX,-Vector3.UnitX,-Vector3.UnitZ,Vector3.UnitZ];
 static readonly string[] Groups=["Structure","Drive","Combat","Mechanics","Body"];
 public double LastDriveCpuMilliseconds{get;private set;}public double LastCameraCpuMilliseconds{get;private set;}public double LastUiCpuMilliseconds{get;private set;}public double LastFeedbackCpuMilliseconds{get;private set;}
 public override int UpdateOrder=>50;
 protected override void OnStart()
 {
  var scene=GameObject.Scene!;var items=JsonSerializer.Deserialize<List<DunePart>>(File.ReadAllText(Path.Combine(ProjectRoot,"Assets/DuneParts/catalog.json")),BuildStore.Json)!;foreach(var p in items)_catalog[p.Id]=p;
  _blocks=string.IsNullOrWhiteSpace(BuildJson)?[new(){Id=0,Type=1}]:BuildStore.Read(BuildJson,_catalog);
  var authored=GameObject.Children.Where(o=>o.GetComponent<DuneBlock3D>()!=null).ToArray();
  if(authored.Length>0){var list=new List<PlacedBlock>();var used=new HashSet<int>();int next=_blocks.Max(b=>b.Id)+1;foreach(var o in authored){var b=o.GetComponent<DuneBlock3D>()!;int id=used.Add(b.BlockId)?b.BlockId:next++;var p=_blocks.FirstOrDefault(p=>p.Id==b.BlockId)??new PlacedBlock();var clone=JsonSerializer.Deserialize<PlacedBlock>(JsonSerializer.Serialize(p,BuildStore.Json),BuildStore.Json)!;clone.Id=id;clone.Type=b.PartType;clone.Parent=b.ParentBlock;clone.Power=b.DriveMultiplier;clone.Steering=b.SteeringAngle;clone.Grip=b.TyreGrip;if(b.MechanicalTuningAuthored){clone.Paint=b.Paint;clone.Travel=b.Travel;clone.Angle=b.Angle;clone.Stroke=b.Stroke;clone.SpringRate=b.SpringRate;clone.Damping=b.Damping;clone.Preload=b.Preload;clone.SpeedLimit=b.SpeedLimit;clone.BrakeStrength=b.BrakeStrength;}clone.Pose(o.Transform.LocalPosition,o.Transform.LocalRotation);list.Add(clone);}list=list.OrderBy(b=>b.Id).ToList();_blocks=BuildStore.Read(JsonSerializer.Serialize(list,BuildStore.Json),_catalog);}
  if(DuneMainMenu3D.NewGamePending){_blocks=[new(){Id=0,Type=1}];DuneMainMenu3D.NewGamePending=false;}
  else if(DuneMainMenu3D.ContinuePending||ByteEngine.Core.Runtime.GameSaveStorage.IsRuntimeProject(ProjectRoot)){var save=Path.Combine(SaveRoot,"vehicle.json");if(File.Exists(save))_blocks=BuildStore.Read(File.ReadAllText(save),_catalog);DuneMainMenu3D.ContinuePending=false;}
  var preferences=DuneMainMenu3D.ReadPreferences(ProjectRoot);VehicleSfxVolume*=Math.Clamp(preferences.Volume,0,1);foreach(var sky in scene.GameObjects.SelectMany(o=>o.Components).OfType<SkyEnvironment>())sky.Quality=preferences.Balanced?GraphicsQuality.Balanced:GraphicsQuality.Fast;
  _camera=scene.ActiveCamera;var terrain=scene.GameObjects.SelectMany(g=>g.Components).FirstOrDefault(c=>c.GetType().FullName=="DesertTerrain.DesertTerrain3D");if(terrain!=null)_sand=new(terrain);
  _garage=Transform.WorldPosition;if(_sand!=null&&_sand.Sample(_garage,out var ground,out _,out _,out _)){_garage.Y=ground.Y+1.4f;Transform.WorldPosition=_garage;}
  Input.NotifyGameViewPointerAim();BuildVisuals();BindUi();RefreshPalette();UpdateCamera();StartGarageMusic();
 }
 public static GameObject Model(Scene scene,AssetManager assets,string path,GameObject parent,string name,bool ghost=false)
 {
  var reference=new AssetReference(path);var model=assets.LoadModel(reference);var root=scene.CreateGameObject(name);root.SetParent(parent,false);root.AddComponent(new ModelHierarchyInstance{Model=reference});
  var importRoot=root;if(int.TryParse(Path.GetFileName(path).AsSpan(0,2),out int partId)&&partId is 23 or 24 or 25 or 26 or 28 or 29 or 30 or 31){importRoot=scene.CreateGameObject("Forward alignment");importRoot.SetParent(root,false);importRoot.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI);}
  var nodes=model.Nodes.ToDictionary(n=>n.Key,n=>scene.CreateGameObject(n.Name));foreach(var n in model.Nodes){var o=nodes[n.Key];o.SetParent(n.ParentKey!=null&&nodes.TryGetValue(n.ParentKey,out var p)?p:importRoot,false);if(Matrix4x4.Decompose(n.LocalTransform,out var scale,out var q,out var pos)){o.Transform.LocalPosition=pos;o.Transform.LocalRotation=n.Name=="Import Space"?Quaternion.Identity:q;o.Transform.LocalScale=scale;}foreach(var key in n.MeshKeys){var m=model.Meshes.First(x=>x.Key==key);var child=scene.CreateGameObject(m.Name);child.SetParent(o,false);var renderer=new MeshRenderer{Mesh=assets.GetModelMesh(reference,key),MeshReference=new(reference,key),CastShadows=!ghost};if(ghost)renderer.Material=new(){BaseColor=new(.2f,1,.65f,.3f),BlendMode=BlendMode3D.AlphaBlend};else if(m.MaterialKey!=null){renderer.MaterialReference=new(reference,m.MaterialKey);renderer.Material=assets.GetModelMaterial(reference,m.MaterialKey);}child.AddComponent(renderer);}}return root;
 }
 void BuildVisuals()
 {
  ClearCombatVisuals();_highlight=null;_highlighted=-1;foreach(var o in GameObject.Children.ToArray())GameObject.Scene!.DestroyGameObject(o);_visuals.Clear();_ghost=null;_mountMarker=null;
  foreach(var p in _blocks){var o=Model(GameObject.Scene!,Assets!,_catalog[p.Type].File,GameObject,"Block "+p.Id+" - "+_catalog[p.Type].Label);o.Transform.LocalPosition=p.P;o.Transform.LocalRotation=p.Q;_visuals[p.Id]=o;o.AddComponent(new DuneBlock3D{BlockId=p.Id,PartType=p.Type,ParentBlock=p.Parent,DriveMultiplier=p.Power,SteeringAngle=p.Steering,TyreGrip=p.Grip,MechanicalTuningAuthored=true,Paint=p.Paint,Travel=p.Travel,Angle=p.Angle,Stroke=p.Stroke,SpringRate=p.SpringRate,Damping=p.Damping,Preload=p.Preload,SpeedLimit=p.SpeedLimit,BrakeStrength=p.BrakeStrength});}
  foreach(var head in _blocks.Where(b=>b.Type==28).Take(2)){var lamp=_visuals[head.Id].AddComponent(new PointLight{Color=new(1,.85f,.6f),Intensity=1.3f,Range=9,CastShadows=false});}foreach(var b in _blocks)ApplyPaint(b);CacheOutputs();CloseWheelMountGaps();BuildJson=JsonSerializer.Serialize(_blocks,BuildStore.Json);_contacts.Clear();RefreshGhost();RefreshHud();
 }
 void RefreshGhost(){if(_ghost!=null)GameObject.Scene!.DestroyGameObject(_ghost);_ghost=Model(GameObject.Scene!,Assets!,_catalog[_type].File,GameObject,"Placement preview",true);CaptureDefinition(new(){Id=-1,Type=_type},_ghost,Descendants(_ghost).FirstOrDefault(o=>o.Name==OutputName(_type)));_ghost.Active=false;}
 string Snapshot()=>JsonSerializer.Serialize(_blocks,BuildStore.Json);
 void Record(){_garageSaved=false;_undo.Push(Snapshot());_redo.Clear();}
 public bool AddBlock(int type,int parent,Vector3 position,Quaternion rotation,bool movingMount=false)
 {
  if(!Building||_blocks.Count>=256||!_catalog.ContainsKey(type)||!_blocks.Any(b=>b.Id==parent)||!float.IsFinite(position.LengthSquared())||position.Length()>100||!float.IsFinite(rotation.LengthSquared())||rotation.LengthSquared()<.01f)return false;
  Record();var block=new PlacedBlock{Id=_blocks.Max(b=>b.Id)+1,Type=type,Parent=parent,MovingMount=movingMount};block.Pose(position,Quaternion.Normalize(rotation));_blocks.Add(block);_selected=block.Id;BuildVisuals();return true;
 }
 public bool ReparentBlock(int id,int parent,bool moving){if(!Building||id==0||!_blocks.Any(b=>b.Id==parent)||!_blocks.Any(b=>b.Id==id))return false;var copy=BuildStore.Read(Snapshot(),_catalog);var b=copy.First(b=>b.Id==id);b.Parent=parent;b.MovingMount=moving;try{copy=BuildStore.Read(JsonSerializer.Serialize(copy,BuildStore.Json),_catalog);}catch(InvalidDataException){return false;}Record();_blocks=copy;BuildVisuals();return true;}
 public bool RemoveBlock(int id)
 {
  if(!Building||_blocks.Count<=1||!_blocks.Any(b=>b.Id==id))return false;Record();var removed=_blocks.First(b=>b.Id==id);
  if(id==0){var replacement=_blocks.First(b=>b.Parent==0);int next=replacement.Id;_blocks.Remove(removed);foreach(var b in _blocks){if(b.Id==next){b.Id=0;b.Parent=-1;b.MovingMount=false;}else{if(b.Parent==0){b.Parent=next;b.MovingMount=false;}if(b.Parent==next)b.Parent=0;}}}
  else{foreach(var b in _blocks.Where(b=>b.Parent==id)){b.Parent=removed.Parent;b.MovingMount=removed.MovingMount;}_blocks.Remove(removed);}
  _blocks=BuildStore.Read(Snapshot(),_catalog);_selected=-1;BuildVisuals();return true;
 }
 public void Command(string command)
 {
  try{switch(command){case "Select winch contract":SelectRecoveryContract(true);break;case "Select tow contract":SelectRecoveryContract(false);break;case "Open contracts":OpenGarageOverlay("Contracts overlay");break;case "Open blueprints":OpenGarageOverlay("Blueprints overlay");break;case "Close garage overlay":CloseGarageOverlay();break;case "Deploy contract":DeployGarageContract();break;case "Toggle drive":ToggleDrive();break;case "Save session":AtomicSave(Path.Combine(SaveRoot,"vehicle.json"),Snapshot());break;case "Save vehicle":SaveCurrentBlueprint();break;case "Load vehicle":if(!Building)break;var loaded=BuildStore.Read(File.ReadAllText(Path.Combine(SaveRoot,"vehicle.json")),_catalog);Record();_blocks=loaded;BuildVisuals();_message="Vehicle loaded.";_garageSaved=true;break;case "Undo":if(Building&&_undo.TryPop(out var undo)){_redo.Push(Snapshot());_blocks=BuildStore.Read(undo,_catalog);BuildVisuals();}break;case "Redo":if(Building&&_redo.TryPop(out var redo)){_undo.Push(Snapshot());_blocks=BuildStore.Read(redo,_catalog);BuildVisuals();}break;case "Delete selected":RemoveBlock(_selected);break;case "Recover vehicle":RecoverPhysics(!MissionActive);break;case "Quit mission":_contractRun=false;Mission?.QuitMission();break;case "Return to base":if(!MissionActive){if(!Building)ToggleDrive();else RecoverPhysics();}break;case "Tune selected":OpenTuning();break;case "Connect winch":ConnectWinch();break;case "Disconnect winch":DisconnectWinch();break;case "Connect recovery":ConnectRecovery();break;case "Disconnect recovery":DisconnectRecovery();break;}}
  catch(Exception ex) when(ex is IOException or JsonException or InvalidDataException){_message="Could not complete action: "+ex.Message;}
 }
 DuneSalvageRoute3D? _mission;public DuneSalvageRoute3D? Mission=>_mission??=GameObject.Scene?.GameObjects.SelectMany(o=>o.Components).OfType<DuneSalvageRoute3D>().FirstOrDefault();
 public bool MissionActive=>_contractRun&&!_contractComplete||Mission?.ActiveMission==true;
 void ToggleDrive()
 {
  if(!Building&&MissionActive){_message="Finish or quit the mission before returning to the workshop.";return;}
  if(!Building&&Mission!=null&&Vector3.Distance(Transform.WorldPosition,_garage)>22){_message="Return to base before building.";return;}
  if(Building&&!ReadyToDrive){_message="Add a seat, engine and at least two wheels to drive.";return;}
  Building=!Building;_cameraReady=false;_lookTimer=0;Speed=0;_contacts.Clear();if(_mountMarker!=null)_mountMarker.Active=false;if(_ghost!=null)_ghost.Active=false;CloseGarageOverlay();CloseTuning();if(Building){_contractRun=false;StopPhysics();Transform.WorldPosition=_garage;Transform.WorldRotation=Quaternion.Identity;_yaw=0;BuildVisuals();}else{StartPhysics();if(!_contractRun)Mission?.BeginMission();}UpdateFeedback();_message=Building?"Ready to build.":"WASD drive / Space brake / Shift drift / R recover / B return to workshop";UpdateVisibility();RefreshHud();
 }
 protected override void OnUpdate()
 {
  if(GameObject.Scene?.FindGameObject("Main menu button")?.GetComponent<UiWidget>() is {} menuButton)menuButton.Interactable=Building;
  Input.NotifyGameViewPointerAim();float dt=Math.Clamp((float)Time.DeltaTime,0,.1f);var phaseWatch=System.Diagnostics.Stopwatch.StartNew();UpdateUi();UpdateGarageMusic(dt);LastUiCpuMilliseconds=phaseWatch.Elapsed.TotalMilliseconds;if(Input.IsKeyPressed(Key.B)&&_typing<0&&!_searching&&!_namingBlueprint)Command("Toggle drive");
  if(Building){if(_typing<0&&!_searching&&!_namingBlueprint){if(Input.IsKeyPressed(Key.R))_twist=(_twist+1)%4;if(Input.IsKeyPressed(Key.F))_pitchSteps=(_pitchSteps+1)%4;if(Input.IsKeyPressed(Key.G))_rollSteps=(_rollSteps+1)%4;if(Input.IsKeyPressed(Key.T)){_manualFace=true;_face=(_face+1)%6;}if(Input.IsKeyPressed(Key.Delete))Command("Delete selected");if(Input.IsKeyDown(Key.LeftControl)&&Input.IsKeyPressed(Key.Z))Command("Undo");if(Input.IsKeyDown(Key.LeftControl)&&Input.IsKeyPressed(Key.Y))Command("Redo");}Pick();UpdateFeedback();}
  else{if(Input.IsKeyPressed(Key.R))Command("Recover vehicle");phaseWatch.Restart();StepDrive((Input.IsKeyDown(Key.W)?1:0)-(Input.IsKeyDown(Key.S)?1:0),(Input.IsKeyDown(Key.D)?1:0)-(Input.IsKeyDown(Key.A)?1:0),Input.IsKeyDown(Key.Space),dt,Input.IsKeyDown(Key.LeftShift));LastDriveCpuMilliseconds=phaseWatch.Elapsed.TotalMilliseconds;}
  bool looking=Input.IsGameViewHovered&&(Input.IsMouseButtonDown(MouseButton.Middle)||Input.IsMouseButtonDown(MouseButton.Right));if(looking){if(!Building)_lookTimer=2.5f;var d=Input.GameViewMouseDelta;if(d.LengthSquared()<.001f)d=Input.MouseDelta;_orbit-=d.X*.007f;_elevation=Math.Clamp(_elevation+d.Y*.005f,-1.35f,1.4f);}if(Input.IsGameViewHovered)_zoom=Math.Clamp(_zoom-Input.Snapshot.MouseWheel,2,45);
  if(_typing<0&&!_searching&&!_namingBlueprint&&Input.IsGameViewHovered){if(Input.IsKeyDown(Key.J))_orbit-=1.4f*dt;if(Input.IsKeyDown(Key.L))_orbit+=1.4f*dt;if(Input.IsKeyDown(Key.I))_elevation=Math.Clamp(_elevation+dt,-1.35f,1.4f);if(Input.IsKeyDown(Key.K))_elevation=Math.Clamp(_elevation-dt,-1.35f,1.4f);}phaseWatch.Restart();UpdateCamera();LastCameraCpuMilliseconds=phaseWatch.Elapsed.TotalMilliseconds;Text("Machine count",$"{_blocks.Count} BLOCKS / {_blocks.Sum(b=>_catalog[b.Type].Mass):0} kg");Text("Machine status",Building?"WORKSHOP":$"{Math.Abs(Speed)*3.6f:0} km/h");Text("Simulation mode",Building?"BUILD":"DRIVE");Text("Placement instruction",_message);RefreshReadiness();RefreshGarageState();phaseWatch.Restart();UpdateCombat(dt);UpdateRecovery();LastFeedbackCpuMilliseconds=phaseWatch.Elapsed.TotalMilliseconds;if(Building)UpdateFeedback();RefreshSpeedometer();RecordFrameDiagnostics(dt);
 }
 Vector2 _placementNudge;GameObject? _mountMarker;
 void Pick()
 {
  _hover=-1;
  if(_mountMarker!=null)_mountMarker.Active=false;if(_camera==null||!Input.IsGameViewHovered ){if(_ghost!=null)_ghost.Active=false;return;}var mouse=Input.GameViewMousePosition;var viewport=Input.GameViewSize;float y=mouse.Y/Math.Max(1,viewport.Y)*720;if(_ui.ContainsKey("Garage UX v2")?(GaragePointerBlocked(mouse,viewport)): (y<48||y>=606||(mouse.X/Math.Max(1,viewport.X)*1280>996&&y<360))){if(_ghost!=null)_ghost.Active=false;return;}
  var ray=_camera.ScreenPointToRay(Input.GameViewPointerNormalized,viewport.X/Math.Max(1,viewport.Y));float closest=float.MaxValue;_hover=-1;Vector3 hit=default,normal=default;PartBounds hitBounds=default;bool movingSurface=false;
  foreach(var p in _blocks){Matrix4x4.Invert(_visuals[p.Id].Transform.WorldMatrix,out var inverse);var origin=Vector3.Transform(ray.Origin,inverse);var direction=Vector3.TransformNormal(ray.Direction,inverse);var def=_catalog[p.Type];var boxes=PickingBounds(p.Type);foreach(var (box,moving) in boxes)if(HitBox(origin,direction,box.Low,box.High,out float t,out var n)&&t<closest){closest=t;_hover=p.Id;hit=origin+direction*t;normal=n;hitBounds=box;movingSurface=moving;}}
  _valid=false;if(_hover>=0){var target=_blocks.First(p=>p.Id==_hover);_candidateMoving=movingSurface&&!_catalog[target.Type].Wheel;var def=_catalog[target.Type];var n=normal;var snap=target.Type<=12?SnapFace(hit,n,hitBounds.Low,hitBounds.High):hitBounds.Center;
   if(target.Type>12){if(Math.Abs(n.X)>.5f)snap.X=n.X>0?hitBounds.High.X:hitBounds.Low.X;if(Math.Abs(n.Y)>.5f)snap.Y=n.Y>0?hitBounds.High.Y:hitBounds.Low.Y;if(Math.Abs(n.Z)>.5f)snap.Z=n.Z>0?hitBounds.High.Z:hitBounds.Low.Z;}
   var tangent=Math.Abs(n.Y)>.5f?Vector3.UnitX:Math.Abs(n.X)>.5f?Vector3.UnitZ:Vector3.UnitX;var across=Vector3.Cross(n,tangent);
   if(!Input.IsKeyDown(Key.LeftControl)){if(Input.IsKeyPressed(Key.Tab))_placementNudge=Vector2.Zero;if(Input.IsKeyPressed(Key.Z))_placementNudge.X-=.25f;if(Input.IsKeyPressed(Key.X))_placementNudge.X+=.25f;if(Input.IsKeyPressed(Key.C))_placementNudge.Y-=.25f;if(Input.IsKeyPressed(Key.V))_placementNudge.Y+=.25f;}
   snap+=tangent*_placementNudge.X+across*_placementNudge.Y;
   if(_mountMarker==null){_mountMarker=GameObject.Scene!.CreateGameObject("Mount alignment marker");_mountMarker.SetParent(GameObject,false);_mountMarker.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Cube,CastShadows=false,Material=new(){Shading=MaterialShadingMode.Unlit,BaseColor=new(1,.8f,.15f,1)}});_mountMarker.Transform.LocalScale=new(.065f);}
   _mountMarker.Active=_tool=="Place";_mountMarker.Transform.LocalPosition=target.P+Vector3.Transform(snap+n*.04f,target.Q);
   var selected=_catalog[_type];var rotation=Quaternion.CreateFromYawPitchRoll(_twist*MathF.PI*.5f,_pitchSteps*MathF.PI*.5f,_rollSteps*MathF.PI*.5f);Vector3 incoming;Quaternion q;
   if(selected.Id<=12&&!_manualFace){incoming=NearestAxis(Vector3.Transform(-n,Quaternion.Inverse(rotation)));q=Quaternion.Normalize(target.Q*rotation);}
   else{incoming=selected.Wheel?Vector3.UnitZ:!_manualFace&&selected.Id is 20 or 21 or 22 or 32?Vector3.UnitZ:selected.Id==18&&!_manualFace?-Vector3.UnitY:selected.Id is 16 or 17&&!_manualFace?Vector3.UnitY:Normals[_face];var align=FromTo(incoming,-n);if(selected.Id==18&&!_manualFace&&Math.Abs(n.Y)<.5f){var pin=Vector3.Transform(Vector3.UnitZ,align);var up=Vector3.Normalize(Vector3.UnitY-n*Vector3.Dot(Vector3.UnitY,n));float angle=MathF.Atan2(Vector3.Dot(n,Vector3.Cross(pin,up)),Vector3.Dot(pin,up));align=Quaternion.Normalize(Quaternion.CreateFromAxisAngle(n,angle)*align);}var local=Quaternion.Normalize(Quaternion.CreateFromAxisAngle(n,_twist*MathF.PI*.5f)*align*Quaternion.CreateFromYawPitchRoll(0,_pitchSteps*MathF.PI*.5f,_rollSteps*MathF.PI*.5f));q=Quaternion.Normalize(target.Q*FromTo(Vector3.Transform(incoming,local),-n)*local);}
   Vector3 own=MountPoint(_type,incoming);
   _candidate=target.P+Vector3.Transform(snap,target.Q)-Vector3.Transform(own,q);_candidateQ=q;_valid=!Overlap(_candidate,q,_type,_move);string placementError=_valid?"": "Part intersects another block";if(_valid&&!HasMountSupport(_type,_candidate,q,incoming,_hover)){_valid=false;placementError=(_catalog[target.Type].Wheel||target.Type is 23 or 24 or 25 or 26 or 30 or 31)?"Attach to a frame or mechanical mounting plate, not a tyre, engine or seat housing":$"Needs {selected.SupportWidth:0.##} � {selected.SupportLength:0.##} m of connected mounting surface";}
   _message=_tool=="Tune"?"Click the highlighted part to tune it":_valid?"Click to place":placementError+" / Add support or choose another face";
   if(_ghost!=null){_ghost.Transform.LocalPosition=_candidate;_ghost.Transform.LocalRotation=q;_ghost.Active=_tool=="Place";foreach(var renderer in Descendants(_ghost).SelectMany(o=>o.Components).OfType<MeshRenderer>())renderer.Material.BaseColor=_valid?new(.2f,1,.65f,.3f):new(1,.15f,.1f,.3f);}
   if(Input.IsMouseButtonPressed(MouseButton.Left)){_selected=_hover;if(_tool=="Erase")RemoveBlock(_hover);else if(_tool=="Tune")OpenTuning();else if(_tool=="Move"){if(_hover!=0){_move=_hover;_type=target.Type;_tool="Place";RefreshGhost();_message="Click a new mount to move the selected branch.";}}else if(_valid){if(_move>=0)MoveBranch();else AddBlock(_type,_hover,_candidate,_candidateQ,_candidateMoving);}else _message=placementError+" / Add support or choose another face";}
  }else if(_ghost!=null){_ghost.Active=false;if(_mountMarker!=null)_mountMarker.Active=false;}
 }
 void MoveBranch(){var root=_blocks.First(p=>p.Id==_move);var ids=new HashSet<int>{_move};foreach(var p in _blocks)if(ids.Contains(p.Parent))ids.Add(p.Id);if(ids.Contains(_hover)){_message="A branch cannot connect to itself.";return;}Record();var oldPosition=root.P;var delta=_candidateQ*Quaternion.Inverse(root.Q);foreach(var p in _blocks.Where(p=>ids.Contains(p.Id))){p.Pose(_candidate+Vector3.Transform(p.P-oldPosition,delta),Quaternion.Normalize(delta*p.Q));}root.Parent=_hover;root.MovingMount=_candidateMoving;_blocks=BuildStore.Read(Snapshot(),_catalog);_move=-1;BuildVisuals();}
 static Quaternion FromTo(Vector3 a,Vector3 b){float dot=Vector3.Dot(a,b);if(dot>.999f)return Quaternion.Identity;if(dot<-.999f)return Quaternion.CreateFromAxisAngle(Math.Abs(a.Y)<.9f?Vector3.Normalize(Vector3.Cross(a,Vector3.UnitY)):Vector3.UnitX,MathF.PI);return Quaternion.Normalize(new Quaternion(Vector3.Cross(a,b),1+dot));}
 static bool HitBox(Vector3 o,Vector3 d,Vector3 low,Vector3 high,out float t,out Vector3 n){t=0;n=Vector3.Zero;float far=float.MaxValue;for(int axis=0;axis<3;axis++){float p=axis==0?o.X:axis==1?o.Y:o.Z,v=axis==0?d.X:axis==1?d.Y:d.Z,a=axis==0?low.X:axis==1?low.Y:low.Z,b=axis==0?high.X:axis==1?high.Y:high.Z;if(Math.Abs(v)<.00001f){if(p<a||p>b)return false;continue;}float first=(a-p)/v,last=(b-p)/v;float sign=-1;if(first>last){(first,last)=(last,first);sign=1;}if(first>t){t=first;n=axis==0?new(sign,0,0):axis==1?new(0,sign,0):new(0,0,sign);}far=Math.Min(far,last);if(t>far)return false;}return t>0;}
 (Vector3 low,Vector3 high) Box(DunePart def,Vector3 p,Quaternion q){var low=new Vector3(float.MaxValue);var high=new Vector3(float.MinValue);for(int i=0;i<8;i++){var v=p+Vector3.Transform(new Vector3((i&1)==0?def.Low.X:def.High.X,(i&2)==0?def.Low.Y:def.High.Y,(i&4)==0?def.Low.Z:def.High.Z),q);low=Vector3.Min(low,v);high=Vector3.Max(high,v);}return(low,high);}
 (Vector3 low,Vector3 high) BoundsBox(PartBounds bounds,Vector3 p,Quaternion q){var low=new Vector3(float.MaxValue);var high=new Vector3(float.MinValue);for(int i=0;i<8;i++){var v=p+Vector3.Transform(new Vector3((i&1)==0?bounds.Low.X:bounds.High.X,(i&2)==0?bounds.Low.Y:bounds.High.Y,(i&4)==0?bounds.Low.Z:bounds.High.Z),q);low=Vector3.Min(low,v);high=Vector3.Max(high,v);}return(low,high);}
 PartBounds[] PlacementBounds(int type){if(_physicsDefinitions.TryGetValue(type,out var p)&&p.Articulated)return [p.Fixed,p.Moving];return [new(_catalog[type].Low,_catalog[type].High)];}
 bool Overlap(Vector3 p,Quaternion q,int type,int exclude){foreach(var candidate in PlacementBounds(type)){var a=BoundsBox(candidate,p,q);foreach(var b in _blocks.Where(b=>b.Id!=exclude))foreach(var existing in PlacementBounds(b.Type)){var x=BoundsBox(existing,b.P,b.Q);if(a.low.X<x.high.X-.04f&&a.high.X>x.low.X+.04f&&a.low.Y<x.high.Y-.04f&&a.high.Y>x.low.Y+.04f&&a.low.Z<x.high.Z-.04f&&a.high.Z>x.low.Z+.04f)return true;}}return false;}
 protected override void OnStop(){StopGarageMusic();RestoreGroundVisibility();ExportFrameDiagnostics();ClearVehicleFeedback();StopPhysics();_contacts.Clear();}
 protected override void OnDestroy(){StopGarageMusic();RestoreGroundVisibility();ClearVehicleFeedback();StopPhysics();}
}
