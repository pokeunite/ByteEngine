using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 bool _garageEditing;int _tutorialStep=-1,_tutorialGuidedType=-1;bool _tutorialLoaded;
 bool WorkshopV3=>GameObject.Scene?.FindGameObject("Garage UX v3")!=null;
 void BindWorkshopV3(){
  if(!WorkshopV3)return;
  Bind("Workshop settings",()=>OpenGarageOverlay("Settings overlay"));Bind("Workshop settings close",CloseGarageOverlay);Bind("Workshop main menu",()=>GameObject.Scene!.RequestLoad("Scenes/MainMenu.bytescene"));
  foreach(string bus in new[]{"Master","Music","Sfx","Ui"})foreach(string sign in new[]{"-","+"}){string selected=bus;float delta=sign=="+"?.1f:-.1f;Bind("Settings "+bus+" "+sign,()=>ChangeWorkshopVolume(selected,delta));}
  Bind("Build vehicle",()=>SetGarageEditing(true));Bind("Build home",()=>SetGarageEditing(false));Bind("Build save",()=>Command("Save vehicle"));Bind("Build test",()=>{if(ReadyToDrive)ToggleDrive();else _message="Fit a seat, engine and two wheels before testing.";});
  Bind("Tutorial next",AdvanceWorkshopTutorial);Bind("Tutorial skip",FinishWorkshopTutorial);Bind("Tutorial replay",()=>{_tutorialStep=0;SetGarageEditing(false);});
  _garageEditing=false;_elevation=.28f;_zoom=6;_tutorialLoaded=true;_tutorialStep=File.Exists(Path.Combine(SaveRoot,"workshop-tutorial.json"))?-1:0;
 }
 void SetGarageEditing(bool editing){if(!WorkshopV3)return;_garageEditing=editing;_garageOverlay="";CloseTuning();_searching=false;_selected=-1;_highlighted=-1;if(_ghost!=null)_ghost.Active=false;UpdateGarageVisibility();}
 void RefreshWorkshopV3Visibility(){
  if(!WorkshopV3)return;bool edit=Building&&_garageEditing&&_garageOverlay.Length==0;
  GarageActive("Garage home navigation",Building&&!_garageEditing);
  foreach(string name in new[]{"Parts catalogue","Garage command strip","Garage instruction background","Build home","Build save","Build test","Build header"})GarageActive(name,edit);
  GarageActive("Garage inspector",edit&&_tutorialStep<0&&(_selected>=0||_tuning>=0));
  if(!edit||_tutorialStep<0||TutorialRequiredPart<0)GarageActive("Tutorial part guide",false);GarageActive("Build controls",edit);GarageActive("Placement instruction",edit);
  GarageActive("Workshop settings panel",Building&&_garageOverlay=="Settings overlay");GarageActive("Main menu button",false);
  GarageActive("Garage wallet card",Building);GarageActive("Garage wallet",Building);GarageActive("Garage wallet icon",Building);
  GarageActive("Home readiness",Building&&!_garageEditing&&_garageOverlay.Length==0);GarageActive("Home camera controls",Building&&!_garageEditing&&_garageOverlay.Length==0);
  GarageActive("Workshop tutorial",Building&&_tutorialStep>=0&&(_garageOverlay.Length==0||_tutorialStep==5));
  foreach(string name in new[]{"Garage top bar","Garage navigation background","Garage top navigation","Garage tab underline","Contracts action","Save machine","Garage active tab"})GarageActive(name,false);
 }
 void ResizeWorkshopV3(){
  var viewport=Input.GameViewSize;if(viewport.X<1||viewport.Y<1)return;float scale=Math.Min(viewport.X/1280,viewport.Y/720),height=viewport.Y/scale,width=viewport.X/scale;
  PlaceWidget("Garage command strip",UiAnchor.TopLeft,new(0,height-72),new(width,72));
  PlaceWidget("Parts catalogue",UiAnchor.TopLeft,new(0,48),new(248,height-120));
  PlaceWidget("Garage inspector",UiAnchor.TopRight,new(0,48),new(280,height-120));
  PlaceWidget("Garage instruction background",UiAnchor.TopLeft,new(248,height-112),new(width-528,40));
  PlaceText("Build controls",UiAnchor.TopLeft,new(264,height-109),12);
  PlaceText("Placement instruction",UiAnchor.TopLeft,new(264,height-90),14);
  PlaceWidget("Build home",UiAnchor.TopLeft,new(16,18),new(122,36));
  float x=158;foreach(var item in new[]{("Place blocks",64f),("Tune blocks",64f),("Move branch",64f),("Rotate mount",74f),("Change mount face",64f),("Copy part",84f),("Erase blocks",64f),("Undo",56f),("Redo",56f)}){PlaceWidget(item.Item1,UiAnchor.TopLeft,new(x,18),new(item.Item2,36));x+=item.Item2+6;}
  PlaceWidget("Build save",UiAnchor.TopRight,new(-16,18),new(146,36));
  PlaceWidget("Build test",UiAnchor.TopRight,new(-172,18),new(140,36));
  PlaceText("Garage save status",UiAnchor.TopLeft,new(470,23),14);
  foreach(var (name,tool) in new[]{("Place blocks","Place"),("Tune blocks","Tune"),("Move branch","Move"),("Erase blocks","Erase")})if(_ui.TryGetValue(name,out var item)){var w=item.GetComponent<UiWidget>()!;w.ThemeKey=_tool==tool?"primary":"secondary";foreach(var child in item.Children)if(child.GetComponent<UiText>() is {} caption)caption.Color=_tool==tool?new(.07f,.06f,.04f,1):new(.88f,.87f,.83f,1);}
  if(_ui.TryGetValue("Build save",out var save))save.GetComponent<UiWidget>()!.ThemeKey="primary";
  PlaceWidget("Garage wallet card",UiAnchor.TopRight,new(-296,9),new(154,30));
  PlaceText("Garage wallet",UiAnchor.TopRight,new(-348,12),21);
  PlaceWidget("Garage wallet icon",UiAnchor.TopRight,new(-309,14),new(18,18));
  ResizeMissionHud(width,height);
 }
 void PlaceWidget(string name,UiAnchor anchor,Vector2 offset,Vector2 size){if(_ui.TryGetValue(name,out var o)&&o.GetComponent<UiWidget>() is {} w){w.Anchor=anchor;w.Offset=offset;w.Size=size;}}
 void PlaceText(string name,UiAnchor anchor,Vector2 offset,int size){if(_ui.TryGetValue(name,out var o)&&o.GetComponent<UiText>() is {} t){t.Anchor=anchor;t.Offset=offset;t.FontSize=size;}}
 int TutorialFrameCount=>_blocks.Count(b=>_catalog[b.Type].Group=="Structure");
 int TutorialRequiredPart=>_tutorialStep switch {1=>_catalog.Values.First(p=>p.Group=="Structure").Id,2=>25,3=>!_blocks.Any(b=>b.Type is 23 or 24)?23:_blocks.Count(b=>_catalog[b.Type].Wheel)<2?13:-1,_=>-1};
 bool TutorialCanAdvance=>_tutorialStep switch{1=>TutorialFrameCount>=2,2=>_blocks.Any(b=>b.Type is 25 or 26),3=>ReadyToDrive,_=>true};
 void GuideTutorialPart(){
  int target=TutorialRequiredPart;
  if(target>=0&&target!=_tutorialGuidedType){_tutorialGuidedType=target;_category=Array.IndexOf(Groups,_catalog[target].Group);_search="";_page=Array.FindIndex(Palette(),p=>p.Id==target)/6;SelectType(target);}
  if(_ui.TryGetValue("Tutorial part guide",out var guide)){
   int slot=Array.FindIndex(Palette(),p=>p.Id==target)-_page*6;guide.Active=Building&&_garageEditing&&_tutorialStep>=0&&target>=0&&slot>=0&&slot<6;
   if(guide.Active){guide.GetComponent<UiWidget>()!.Offset=new(14+(slot%2)*116,177+(slot/2)*135);Text("Tutorial part caption","CHOOSE THIS PART");}
  }
  for(int slot=0;slot<6;slot++){int index=_page*6+slot;var parts=Palette();if(index<parts.Length&&parts[index].Id==target&&_ui.TryGetValue("Part selection "+slot,out var border))border.GetComponent<UiWidget>()!.Color=new(1,.75f,.25f,1);}
 }
 bool TutorialBlocksPointer(Vector2 mouse,Vector2 viewport){return _tutorialStep>=0&&_ui.TryGetValue("Workshop tutorial",out var bubble)&&bubble.ActiveInHierarchy&&bubble.GetComponent<UiWidget>()!.Contains(mouse,viewport);}
 readonly (string title,string body,string action)[] _tutorialLessons=[
  ("YOUR FIRST SHIFT","Build a recovery vehicle, choose a contract, then bring the target home for payment.","LET'S BUILD"),
  ("MAKE A SOLID FRAME","Add one rail to your starting frame. Choose the highlighted rail, then click a mounting face. RMB / MMB orbits; scroll zooms.","FRAME READY"),
  ("FIT THE DRIVER'S SEAT","Open Body and add a seat to your frame. The seat check turns green when one is fitted.","SEAT FITTED"),
  ("ADD ENGINE AND WHEELS","Fit an engine and at least two wheels. Steering needs a steering hinge. R / F / G rotate a preview; T changes its mounting face.","READY TO DRIVE"),
  ("MAKE IT YOUR MACHINE","Use Tune, then click a placed part. Drag a slider or type a value. Save changes updates your loaded blueprint.","OPEN CONTRACTS"),
  ("READ THE JOB FIRST","Expand an available contract. Check its parts and reward. Fit the recovery tool, then choose Accept & Deploy.","I'M READY")];
 void AdvanceWorkshopTutorial(){
  if(_tutorialStep==0)SetGarageEditing(true);
  if(_tutorialStep==1&&TutorialFrameCount<2){_message="Add one structural rail to the starting frame.";return;}
  if(_tutorialStep==2&&!_blocks.Any(b=>b.Type is 25 or 26)){_message="Fit a driver's seat to continue.";return;}
  if(_tutorialStep==3&&!ReadyToDrive){_message="Fit an engine and at least two wheels to continue.";return;}
  if(_tutorialStep==4){SetGarageEditing(false);OpenGarageOverlay("Contracts overlay");}
  _tutorialGuidedType=-1;if(++_tutorialStep>=_tutorialLessons.Length)FinishWorkshopTutorial();
 }
 void FinishWorkshopTutorial(){_tutorialStep=-1;_tutorialGuidedType=-1;RefreshPalette();GarageActive("Tutorial part guide",false);AtomicSave(Path.Combine(SaveRoot,"workshop-tutorial.json"),"{\"complete\":true}");GarageActive("Workshop tutorial",false);}
 void ChangeWorkshopVolume(string name,float delta){var bus=Enum.Parse<ByteEngine.Core.Audio.AudioBus>(name);ByteEngine.Core.Audio.AudioMixer.SetVolume(bus,ByteEngine.Core.Audio.AudioMixer.GetVolume(bus)+delta);var p=DuneMainMenu3D.ReadPreferences(ProjectRoot);p=name switch {"Master"=>p with {Volume=ByteEngine.Core.Audio.AudioMixer.GetVolume(bus)},"Music"=>p with {Music=ByteEngine.Core.Audio.AudioMixer.GetVolume(bus)},"Sfx"=>p with {Sfx=ByteEngine.Core.Audio.AudioMixer.GetVolume(bus)},_=>p with {Ui=ByteEngine.Core.Audio.AudioMixer.GetVolume(bus)}};AtomicSave(Path.Combine(SaveRoot,"menu-settings.json"),System.Text.Json.JsonSerializer.Serialize(p));}
 void UpdateWorkshopTutorial(){
  if(!WorkshopV3||!_tutorialLoaded)return;
  if(_tutorialStep>=0){if(_ui.TryGetValue("Workshop tutorial",out var panel)){var widget=panel.GetComponent<UiWidget>()!;widget.Anchor=UiAnchor.TopRight;widget.Offset=new Vector2(-8,60);widget.Size=new Vector2(264,300);}var lesson=_tutorialLessons[_tutorialStep];Text("Tutorial progress",$"FIRST SHIFT / {_tutorialStep+1} OF {_tutorialLessons.Length}");Text("Tutorial heading",lesson.title);string progress=_tutorialStep switch{1=>$"\nFRAME: {Math.Min(TutorialFrameCount,2)}/2 parts",2=>$"\nSEAT: {(_blocks.Any(b=>b.Type is 25 or 26)?"FITTED":"NEEDED")}",3=>$"\nENGINE: {(_blocks.Any(b=>b.Type is 23 or 24)?"OK":"NEEDED")} / WHEELS: {Math.Min(_blocks.Count(b=>_catalog[b.Type].Wheel),2)}/2",_=>""};Text("Tutorial body",lesson.body+progress);if(_ui.TryGetValue("Tutorial next",out var next)){var button=next.GetComponent<UiWidget>()!;button.Label=lesson.action;button.Interactable=TutorialCanAdvance;button.Color=TutorialCanAdvance?new(.95f,.65f,.22f,1):new(.32f,.29f,.21f,1);}GuideTutorialPart();}
  foreach(string name in new[]{"Master","Music","Sfx","Ui"})Text("Settings "+name+" value",$"{name.ToUpperInvariant()}  {ByteEngine.Core.Audio.AudioMixer.GetVolume(Enum.Parse<ByteEngine.Core.Audio.AudioBus>(name))*100:0}%");
  Text("Home ready status",ReadyToDrive?"READY TO DRIVE":"FINISH YOUR BUILD");
  foreach(var (name,ok) in new[]{("seat",_blocks.Any(b=>b.Type is 25 or 26)),("engine",_blocks.Any(b=>b.Type is 23 or 24)),("wheel",_blocks.Any(b=>_catalog[b.Type].Wheel))})
   if(_ui.TryGetValue("Home "+name+" check",out var icon)){var w=icon.GetComponent<UiWidget>()!;w.ImageReference=new("Assets/GarageUI/v3/"+(ok?"check":"missing")+".png");w.Color=ok?new(.56f,.77f,.25f,1):new(.9f,.3f,.23f,1);}
  foreach(var (name,y) in new[]{("Garage tab",74f),("Build vehicle",132f),("Blueprints tab",190f),("Contracts tab",248f),("Workshop settings",334f)}){
   bool selected=name=="Garage tab"&&_garageOverlay.Length==0||name=="Blueprints tab"&&_garageOverlay=="Blueprints overlay"||name=="Contracts tab"&&_garageOverlay=="Contracts overlay"||name=="Workshop settings"&&_garageOverlay=="Settings overlay";
   if(_ui.TryGetValue(name,out var button))button.GetComponent<UiWidget>()!.Color=selected?new(.14f,.115f,.07f,1):Vector4.Zero;
   string label=name=="Workshop settings"?"Workshop settings caption":name+" nav label";if(_ui.TryGetValue(label,out var caption))caption.GetComponent<UiText>()!.Color=selected?new(.95f,.65f,.22f,1):new(.86f,.85f,.81f,1);
   if(selected&&_ui.TryGetValue("Home active stripe",out var stripe)){stripe.GetComponent<UiWidget>()!.Offset=new(0,y);stripe.GetComponent<UiWidget>()!.OrderInLayer=26;}
  }
  RefreshWorkshopV3Visibility();
 }
}
