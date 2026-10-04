using ByteEngine.Core;
using ByteEngine.Core.InputSystem;
using System.Numerics;
namespace GoblinScrapper.Construction;
public sealed partial class VehicleBuilder3D
{
 public bool UseBuiltInControls {get;set;}=true;
 public bool UseBuiltInPointerControls {get;set;}=true;
 public bool AutomaticCamera {get;set;}=true;
 public bool ShowWorkshopHud {get;set;}=true;
 private float _eventThrottle,_eventSteering;private bool _eventBrake,_eventDrift;
 public int SelectedBlockId=>_selectedBlock;
 public int HoveredBlockId=>_hoveredBlock;
 public int LastPlacedPartId {get;private set;}=-1;
 public bool CanSimulate=>FreeBuilding?Assembly?.CanDrive==true:Layout.CanDrive;
 public bool PlacementReady=>Building&&FreeBuilding&&string.IsNullOrEmpty(_placementIssue);
 private bool ControlKeyPressed(Key key)=>UseBuiltInControls&&Input.IsKeyPressed(key);
 private bool ControlKeyDown(Key key)=>UseBuiltInControls&&Input.IsKeyDown(key);
 public void ResetEventInput(){_eventThrottle=0;_eventSteering=0;_eventBrake=false;_eventDrift=false;}
 public void SetEventInput(float throttle,float steering,bool brake,bool drift){_eventThrottle=FiniteAxis(throttle);_eventSteering=FiniteAxis(steering);_eventBrake=brake;_eventDrift=drift;}
 private static float FiniteAxis(float value)=>float.IsFinite(value)?Math.Clamp(value,-1,1):0;
 public void AddEventThrottle(float amount)=>_eventThrottle=FiniteAxis(_eventThrottle+amount);
 public void AddEventSteering(float amount)=>_eventSteering=FiniteAxis(_eventSteering+amount);
 public void SetEventBrake(bool value)=>_eventBrake=value;
 public void SetEventDrift(bool value)=>_eventDrift=value;
 public void ReturnToBuild(){if(!Building)ToggleDrive();ResetEventInput();}
 public void ToggleSimulation(){ToggleDrive();ResetEventInput();}
 public bool SelectBuildPart(string file){int index=Array.IndexOf(BuilderPartFiles,Path.GetFileNameWithoutExtension(file));if(!Building||index<0)return false;SelectPart(index);return true;}
 public void PlacePreview(){if(Building)PlaceFreePreview();}
 public void RotatePreview(float degrees){if(Building&&float.IsFinite(degrees)){
  if(_candidateVisible&&_catalog!=null&&_catalog[SelectedPart].Kind=="beam") {
   var sockets=_catalog[SelectedPart].Sockets;var normal=-Vector3.Transform(sockets[_ownSocket].Normal,_candidateRotation);
   if(Math.Abs(degrees%90)<.001f){var next=RotateBeamMount(sockets,_ownSocket,_candidateRotation,normal,degrees);_ownSocket=next.Socket;_twist=next.Twist;}else _twist=(int)((_twist+degrees)%360+360)%360;
  } else _twist=(int)((_twist+degrees)%360+360)%360;
  UpdateFreePreview();
 }}
 internal static (int Socket,int Twist) RotateBeamMount(FreePartSocket[] sockets,int current,Quaternion rotation,Vector3 targetNormal,float degrees) {
  var shaft=Vector3.Transform(Vector3.UnitZ,rotation);var axis=Math.Abs(Vector3.Dot(shaft,Vector3.UnitY))>.9f?Vector3.UnitX:Vector3.UnitY;
  var desired=Quaternion.Normalize(Quaternion.CreateFromAxisAngle(axis,degrees*MathF.PI/180)*rotation);
  var wanted=Vector3.Transform(-targetNormal,Quaternion.Inverse(desired));
  int selected=current;float best=float.MaxValue;
  for(int i=0;i<sockets.Length;i++)if(Vector3.Dot(sockets[i].Normal,wanted)>.999f) {float score=sockets[i].Position.LengthSquared();if(score<best){best=score;selected=i;}}
  if(best==float.MaxValue)return(current,0);
  var aligned=AlignNormals(sockets[selected].Normal,-targetNormal);var basis=Math.Abs(sockets[selected].Normal.X)>.9f?Vector3.UnitY:Vector3.UnitX;
  var from=Vector3.Transform(basis,aligned);var to=Vector3.Transform(basis,desired);
  float angle=MathF.Atan2(Vector3.Dot(targetNormal,Vector3.Cross(from,to)),Vector3.Dot(from,to))*180/MathF.PI;
  return(selected,((int)MathF.Round(angle)%360+360)%360);
 }
 public void CyclePreviewMountFace(){if(!Building||_catalog==null)return;var sockets=_catalog[SelectedPart].Sockets;
  if(_catalog[SelectedPart].Kind!="beam"){NextPreviewConnector();return;}
  var faces=sockets.Select((s,i)=>(s,i)).Where(x=>Math.Abs(x.s.Normal.Z)>.9f||Math.Abs(x.s.Position.Z)<.001f).Select(x=>x.i).ToArray();
  if(faces.Length>0){int index=Array.IndexOf(faces,_ownSocket);_ownSocket=faces[(index+1)%faces.Length];_twist=0;UpdateFreePreview();}
 }
 public void OrbitBuildCamera(float yaw,float elevation){if(Building&&float.IsFinite(yaw)&&float.IsFinite(elevation)){_orbit+=yaw;_elevation=Math.Clamp(_elevation+elevation,-1.35f,1.35f);}}
 public void NextPreviewConnector(){if(Building&&_catalog!=null){_ownSocket=(_ownSocket+1)%Math.Max(1,_catalog[SelectedPart].Sockets.Length);UpdateFreePreview();}}
 public void DeleteSelectedBlock(){if(Building)RemoveAssemblyPart(_selectedBlock);}
 public void DeleteHoveredBlock(){if(Building)RemoveAssemblyPart(_hoveredBlock);}
 public void CancelBuildOperation(){if(Building){CancelMove();SetEraseTool(false);}}
 public void CopyHoveredPart(){if(Building&&Assembly?.Parts.TryGetValue(_hoveredBlock,out var part)==true)SelectBuildPart(part.File);}
 public void MoveHoveredBranch()
 {
  if(!Building||Assembly==null||_hoveredBlock<=0)return;
  int id=_hoveredBlock;CancelMove();SelectBuildPart(Assembly.Parts[id].File);_movingBlock=id;
  foreach(int child in Assembly.Descendants(id)){_assemblyVisuals[child].Active=false;if(child!=id)_moveGhosts[child]=CreateModelVisual(GameObject.Scene!,Assets!,PartsDirectory+"/"+Assembly.Parts[child].File+".glb",GameObject,"Moving branch preview",true);}
 }
 public int PlacePartAtConnector(string file,int parent,string connector,string ownConnector="",int twist=0)
 {
  LastPlacedPartId=-1;if(!Building||Assembly==null||_catalog==null||!Assembly.Parts.ContainsKey(parent))return -1;
  file=Path.GetFileNameWithoutExtension(file);if(!_catalog.Parts.TryGetValue(file,out var def))return -1;
  ownConnector=string.IsNullOrEmpty(ownConnector)?def.Sockets.FirstOrDefault(s=>s.Bone=="Root")?.Name??"":ownConnector;
  try{var pose=Assembly.Snap(parent,connector,file,ownConnector,twist);LastPlacedPartId=AddAssemblyPart(file,parent,pose.Position,pose.Rotation,Assembly.Catalog[Assembly.Parts[parent].File].Sockets.First(s=>s.Name==connector).Bone,connector,ownConnector);return LastPlacedPartId;}
  catch(Exception e)when(e is ArgumentException or InvalidOperationException or KeyNotFoundException){_message=e.Message;return -1;}
 }
}
