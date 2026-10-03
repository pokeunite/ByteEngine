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
 public void RotatePreview(float degrees){if(Building&&float.IsFinite(degrees)){_twist=(int)((_twist+degrees)%360+360)%360;UpdateFreePreview();}}
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
