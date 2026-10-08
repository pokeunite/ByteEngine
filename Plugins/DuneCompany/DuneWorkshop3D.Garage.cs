using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 string _search="";bool _searching,_contractRun,_garageTextConsumed,_garageSaved=true;string _garageOverlay="";
 readonly Dictionary<string,Vector2> _garageLayoutOffsets=[];Vector2 _garageLayoutSize;
 bool GarageUx=>_ui.ContainsKey("Garage UX v2");
 void BindGarageUi()
 {
  if(!GarageUx)return;
  Bind("Search parts",()=>{_searching=true;_typing=-1;});
  Bind("Clear search",()=>{_search="";_searching=false;_page=0;RefreshPalette();});
  Bind("Garage tab",CloseGarageOverlay);Bind("Contracts tab",()=>OpenGarageOverlay("Contracts overlay"));Bind("Contracts action",()=>OpenGarageOverlay("Contracts overlay"));
  Bind("Blueprints tab",()=>OpenGarageOverlay("Blueprints overlay"));Bind("Close garage overlay",CloseGarageOverlay);
  Bind("Deploy contract",DeployGarageContract);BindBlueprintUi();BindContractBrowser();BindWinchMissionSwitch();
  Bind("Return workshop",()=>Command("Return to base"));Bind("Quit contract",()=>{Command("Quit mission");Command("Return to base");});
  Bind("Tune selection",()=>{_tool="Tune";_message="Click a placed component to tune it.";OpenTuning();});
  ResizeGarageLayout();RefreshGarageState();
 }
 void DeployGarageContract(){if(!Building)return;if(ContractCompletions(WinchMission)>0){_message="This job is completed. Choose an available contract.";return;}if(!ReadyToDrive){_message="Add a seat, engine and two wheels before deploying.";return;}if(!_blocks.Any(b=>b.Type==(WinchMission?33:20))){_message="Fit the required recovery tool from Mechanical.";Text("Contract requirements",_message);return;}_contractRun=true;_contractComplete=false;CloseGarageOverlay();ToggleDrive();}
 void OpenGarageOverlay(string name){if(!Building)return;CloseTuning();_searching=false;_garageOverlay=name;if(name=="Blueprints overlay")RefreshBlueprints();if(name=="Contracts overlay")RefreshContractBrowser();UpdateGarageVisibility();}
 void CloseGarageOverlay(){_garageOverlay="";_searching=false;_namingBlueprint=false;GarageActive("Blueprint naming",false);if(GarageUx)UpdateGarageVisibility();}
 void GarageActive(string name,bool active){if(_ui.TryGetValue(name,out var obj))obj.Active=active;else if(GameObject.Scene?.FindGameObject(name) is {} root)root.Active=active;}
 void UpdateGarageVisibility()
 {
  GarageActive("Garage build UI",Building);GarageActive("Garage driving UI",!Building);GarageActive("Garage environment",Building);GarageActive("Proving ground caches",false);
  GarageActive("Contracts overlay",Building&&_garageOverlay=="Contracts overlay");GarageActive("Blueprints overlay",Building&&_garageOverlay=="Blueprints overlay");GarageActive("Overlay backdrop",Building&&_garageOverlay.Length>0);GarageActive("Close garage overlay",Building&&_garageOverlay.Length>0);
  GarageActive("Context tooltip",false);GarageActive("Quit contract",!Building&&MissionActive);GarageActive("Return workshop",!Building&&!MissionActive);
  RefreshPalette();
 }
 bool GaragePointerBlocked(Vector2 mouse,Vector2 viewport)
 {
  if(_searching||_garageOverlay.Length>0)return true;
  float scale=Math.Min(viewport.X/1280,viewport.Y/720);float x=mouse.X/scale,y=mouse.Y/scale;
  return x<248||x>=viewport.X/scale-280||y<48||y>=viewport.Y/scale-56;
 }
 void RefreshGarageSelection()
 {
  if(!GarageUx||_catalog.Count==0)return;var def=_catalog[_type];
  Text("Selected block",def.Label.ToUpperInvariant());Text("Block function",def.Function);
  if(_ui.TryGetValue("Inspector part preview",out var preview)&&preview.GetComponent<UiWidget>() is {} picture)picture.ImageReference=new(def.Preview);
 }
 void RefreshGarageState()
 {
  if(!GarageUx)return;Text("Contract name",(RecoveryJob?.Title??"Stranded in the sand").ToUpperInvariant());Text("Contract reward",$"${JobPayment}");Text("Contract requirements",_blocks.Any(b=>b.Type==(WinchMission?33:20))?(WinchMission?"Powered winch fitted / Seat, engine and wheels required":"Trailer hinge fitted / Seat, engine and wheels required"):(WinchMission?"Fit a powered winch before accepting this rescue":"Fit a trailer hinge before accepting this recovery job"));RefreshRequirementIcons();ResizeGarageLayout();Text("Garage readiness",ReadyToDrive?"READY TO DRIVE":"BUILD REQUIREMENTS");if(_ui.TryGetValue("Garage readiness",out var readiness)&&readiness.GetComponent<UiText>() is {} status)status.Color=ReadyToDrive?new(.7f,.8f,.59f,1):new(.94f,.66f,.29f,1);Text("Seat readiness",_blocks.Any(b=>b.Type is 25 or 26)?"Seat OK":"Seat needed");Text("Engine readiness",_blocks.Any(b=>b.Type is 23 or 24)?"Engine OK":"Engine needed");Text("Wheel readiness",_blocks.Any(b=>_catalog[b.Type].Wheel)?"Wheel OK":"Wheel needed");
  Text("Garage mass",$"{_blocks.Sum(b=>_catalog[b.Type].Mass):0} kg");Text("Garage wheels",_blocks.Count(b=>_catalog[b.Type].Wheel).ToString());Text("Garage engines",_blocks.Count(b=>b.Type is 23 or 24).ToString());
  Text("Garage save status",_garageSaved?"Saved":"Unsaved");GarageActive("Tune selection",_tuning<0);
  if(_ui.TryGetValue("Search parts",out var search)&&search.GetComponent<UiWidget>() is {} field)field.Label=(_search.Length==0?"Search parts...":_search)+(_searching?" |":"");
  for(int i=0;i<5;i++)if(_ui.TryGetValue($"Button {8+i*42},639",out var category)&&category.GetComponent<UiWidget>() is {} tab)tab.Color=_search.Length==0&&i==_category?new(.27f,.22f,.14f,1):new(.095f,.10f,.10f,1);
  Text("Catalogue page",$"{_page+1} / {Math.Max(1,(Palette().Length+5)/6)}");Text("Search empty",Palette().Length==0?"No matching parts":"");
  GarageActive("Quit contract",!Building&&MissionActive);GarageActive("Return workshop",!Building&&!MissionActive);
 }
 void ResizeGarageLayout()
 {
  var viewport=Input.GameViewSize;if(viewport.X<1||viewport.Y<1||viewport==_garageLayoutSize)return;_garageLayoutSize=viewport;float scale=Math.Min(viewport.X/1280,viewport.Y/720);float height=viewport.Y/scale,width=viewport.X/scale,dy=height-720;
  if(_ui.TryGetValue("Parts catalogue",out var left))left.GetComponent<UiWidget>()!.Size=new(248,height-104);
  if(_ui.TryGetValue("Garage inspector",out var right))right.GetComponent<UiWidget>()!.Size=new(280,height-48);
  if(_ui.TryGetValue("Garage command strip",out var bottom)){var w=bottom.GetComponent<UiWidget>()!;w.Offset=new(0,height-56);w.Size=new(width-280,56);}
  if(_ui.TryGetValue("Garage instruction background",out var background)){var w=background.GetComponent<UiWidget>()!;w.Offset=new(248,height-121);w.Size=new(width-528,65);}
  foreach(var name in new[]{"Summary divider","Summary title","Mass caption","Garage mass","Wheels caption","Garage wheels","Engine caption","Garage engines","Readiness divider","Garage readiness","Seat readiness","Engine readiness","Wheel readiness","Requirement seat","Requirement engine","Requirement wheel","Garage drive hint","Run / build","Contracts action","Placement instruction","Build controls"})if(_ui.TryGetValue(name,out var o)){var widget=o.GetComponent<UiWidget>();var text=o.GetComponent<UiText>();var pos=widget?.Offset??text!.Offset;if(!_garageLayoutOffsets.TryGetValue(name,out var original)){original=pos;_garageLayoutOffsets[name]=pos;}if(widget!=null)widget.Offset=original+new Vector2(0,dy);else text!.Offset=original+new Vector2(0,dy);}
 }
 void UpdateGarageInput()
 {
  _garageTextConsumed=_searching&&(Input.IsKeyPressed(Key.Enter)||Input.IsKeyPressed(Key.Escape));if(!GarageUx||!_searching)return;string addition="";
  for(char ch='A';ch<='Z';ch++)if(Enum.TryParse<Key>(ch.ToString(),out var key)&&Input.IsKeyPressed(key))addition+=char.ToLowerInvariant(ch);
  for(int i=0;i<10;i++)if(Input.IsKeyPressed((Key)((int)Key.D0+i)))addition+=i;
  if(Input.IsKeyPressed(Key.Space))addition+=" ";if(_search.Length<32)_search+=addition;
  if(Input.IsKeyPressed(Key.Backspace)&&_search.Length>0)_search=_search[..^1];
  if(Input.IsKeyPressed(Key.Enter)||Input.IsKeyPressed(Key.Escape))_searching=false;
  if(addition.Length>0||Input.IsKeyPressed(Key.Backspace)){_page=0;RefreshPalette();}RefreshGarageState();
 }
}
