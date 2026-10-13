using System.Numerics;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
namespace DuneCompany;

public sealed partial class DuneWorkshop3D
{
 float _cameraMotion=.35f,_cameraSensitivity=1,_cameraImpulse,_polishTimer,_hoverSoundCooldown;
 string? _driveBinding;bool _polishInputConsumed,_polishWasDriving;string _graphicsTier="Balanced",_worldResolution="Auto";UiWidget? _hoverSoundTarget;
 readonly Dictionary<string,Key> _driveKeys=[];
 static readonly (string action,Key key)[] DefaultDriveKeys=[("Forward",Key.W),("Reverse",Key.S),("Left",Key.A),("Right",Key.D),("Brake",Key.Space),("Drift",Key.LeftShift),("Recover",Key.R),("Test drive",Key.B),("Shift up",Key.X),("Shift down",Key.Z)];
 Key DriveKey(string action)=>_driveKeys.GetValueOrDefault(action,DefaultDriveKeys.First(k=>k.action==action).key);
 bool DriveDown(string action)=>Input.IsKeyDown(DriveKey(action));
 bool DrivePressed(string action)=>Input.IsKeyPressed(DriveKey(action));
 void LoadPolishPreferences(DuneMainMenu3D.Preferences p){
  _cameraMotion=float.IsFinite(p.CameraMotion)?Math.Clamp(p.CameraMotion,0,1):.35f;
  _cameraSensitivity=float.IsFinite(p.CameraSensitivity)?Math.Clamp(p.CameraSensitivity,.25f,2):1;
  _graphicsTier=DuneMainMenu3D.QualityName(p.Graphics);_worldResolution=DuneMainMenu3D.ResolutionName(p.WorldResolution);
  foreach(var (action,key) in DefaultDriveKeys)_driveKeys[action]=p.DriveBindings?.TryGetValue(action,out var saved)==true&&Enum.TryParse<Key>(saved,out var parsed)&&Enum.IsDefined(parsed)&&parsed is not (Key.Escape or Key.Enter or Key.Tab)?parsed:key;
 }
 void SavePolishPreferences(){var p=DuneMainMenu3D.ReadPreferences(ProjectRoot) with {CameraMotion=_cameraMotion,CameraSensitivity=_cameraSensitivity,DriveBindings=_driveKeys.ToDictionary(k=>k.Key,k=>k.Value.ToString())};AtomicSave(Path.Combine(SaveRoot,"menu-settings.json"),JsonSerializer.Serialize(p));}
 void InitializePolish(){
  if(!WorkshopV3)return;
  UiWidget Button(string name,GameObject parent,Vector2 position,Vector2 size,Action action,string label){
   var o=GameObject.Scene!.CreateGameObject(name);o.SetParent(parent,false);var w=o.AddComponent(new UiWidget{Kind=UiWidgetKind.Button,Offset=position,Size=size,Label=label,FontSize=15,FontReference=new("Assets/GarageUI/fonts/BarlowCondensed-SemiBold.ttf"),Color=new(.13f,.15f,.13f,1),HoverColor=new(.25f,.23f,.16f,1),PressedColor=new(.31f,.25f,.14f,1),OrderInLayer=75});_ui[name]=o;Bind(name,action);return w;
  }
  void Label(string name,GameObject parent,Vector2 position,int size,string text){var o=GameObject.Scene!.CreateGameObject(name);o.SetParent(parent,false);o.AddComponent(new UiText{Offset=position,Text=text,FontSize=size,WrapWidth=440,FontReference=new("Assets/GarageUI/fonts/BarlowCondensed-SemiBold.ttf"),Color=new(.88f,.87f,.81f,1),OrderInLayer=parent.Name=="Driving controls panel"?96:76});_ui[name]=o;}
  if(_ui.TryGetValue("Workshop settings panel",out var settings)){
   settings.GetComponent<UiWidget>()!.Size=new(540,568);
   foreach(var (bus,i) in new[]{"Master","Music","Sfx","Ui"}.Select((b,i)=>(b,i))){PlaceText("Settings "+bus+" value",UiAnchor.TopLeft,new(28,83+i*42),18);foreach(var (sign,x) in new[]{("-",354f),("+",421f)})PlaceWidget("Settings "+bus+" "+sign,UiAnchor.TopLeft,new(x,76+i*42),new(56,34));}
   Label("Settings motion value",settings,new(28,268),18,"");Label("Settings sensitivity value",settings,new(28,312),18,"");
   foreach(var (sign,x) in new[]{("-",354f),("+",421f)}){float delta=sign=="+"?.1f:-.1f;Button("Settings motion "+sign,settings,new(x,260),new(56,34),()=>{_cameraMotion=Math.Clamp(_cameraMotion+delta,0,1);SavePolishPreferences();},sign);Button("Settings sensitivity "+sign,settings,new(x,304),new(56,34),()=>{_cameraSensitivity=Math.Clamp(_cameraSensitivity+delta,.25f,2);SavePolishPreferences();},sign);}
   Button("Settings graphics",settings,new(28,352),new(449,36),()=>{var p=DuneMainMenu3D.ReadPreferences(ProjectRoot) with {Graphics=DuneMainMenu3D.NextQuality(_graphicsTier)};AtomicSave(Path.Combine(SaveRoot,"menu-settings.json"),JsonSerializer.Serialize(p));ApplyPolishQuality(p);},"GRAPHICS");
   Button("Settings resolution",settings,new(28,394),new(449,36),()=>{var p=DuneMainMenu3D.ReadPreferences(ProjectRoot) with {WorldResolution=DuneMainMenu3D.NextResolution(_worldResolution)};AtomicSave(Path.Combine(SaveRoot,"menu-settings.json"),JsonSerializer.Serialize(p));ApplyPolishQuality(p);},"WORLD RESOLUTION");
   Button("Settings controls",settings,new(28,436),new(449,36),()=>GarageActive("Driving controls panel",true),"DRIVING CONTROLS / REBIND KEYS");
   Label("Settings camera help",settings,new(28,481),15,"Text stays sharp at every world resolution.");
   PlaceWidget("Workshop settings close",UiAnchor.TopLeft,new(28,508),new(170,36));PlaceWidget("Workshop main menu",UiAnchor.TopLeft,new(264,508),new(213,36));
   var controls=GameObject.Scene!.CreateGameObject("Driving controls panel");controls.SetParent(settings,false);controls.AddComponent(new UiWidget{Offset=Vector2.Zero,Size=new(540,568),Color=new(.045f,.055f,.047f,1),Interactable=false,OrderInLayer=90});_ui[controls.Name]=controls;
   Label("Driving controls heading",controls,new(28,25),27,"DRIVING CONTROLS");Label("Driving controls guidance",controls,new(28,67),15,"Select an action, then press a key. Esc cancels.");
   foreach(var (entry,i) in DefaultDriveKeys.Select((k,i)=>(k,i))){string action=entry.action;var b=Button("Driving binding "+action,controls,new(28,108+i*35),new(449,30),()=>{_driveBinding=action;_ui["Driving binding "+action].GetComponent<UiWidget>()!.Label="PRESS A KEY / ESC TO CANCEL";},action);b.OrderInLayer=95;}
   Button("Driving bindings reset",controls,new(28,484),new(216,36),()=>{foreach(var (action,key) in DefaultDriveKeys)_driveKeys[action]=key;_driveBinding=null;SavePolishPreferences();},"RESET DEFAULTS").OrderInLayer=95;
   Button("Driving bindings close",controls,new(261,484),new(216,36),()=>{_driveBinding=null;GarageActive(controls.Name,false);},"DONE").OrderInLayer=95;controls.Active=false;
  }
  if(_ui.TryGetValue("Contract details",out var detail)){
   var image=GameObject.Scene!.CreateGameObject("Contract field image");image.SetParent(detail,false);image.AddComponent(new UiWidget{Kind=UiWidgetKind.Image,Offset=new(16,14),Size=new(488,126),Color=Vector4.One,Interactable=false,OrderInLayer=35});_ui[image.Name]=image;
  }
  if(_ui.TryGetValue("Garage driving UI",out var hud)){
   Label("Drive tool status",hud,new(36,142),16,"");
   var tension=GameObject.Scene!.CreateGameObject("Cable tension meter");tension.SetParent(hud,false);tension.AddComponent(new UiWidget{Kind=UiWidgetKind.ProgressBar,Offset=new(36,167),Size=new(200,5),Color=new(.15f,.17f,.14f,1),FillColor=new(.95f,.65f,.22f,1),Maximum=1,Interactable=false,OrderInLayer=32});_ui[tension.Name]=tension;
  }
  foreach(string name in new[]{"Recovery interaction","Placement instruction","Build controls","Driving controls guidance","Settings camera help"})
   if(_ui.TryGetValue(name,out var textObject)&&textObject.GetComponent<UiText>() is {} body){body.FontReference=new("Assets/GarageUI/fonts/Barlow-Regular.ttf");body.Color=new(.92f,.93f,.89f,1);}
 }
 void ApplyPolishQuality(DuneMainMenu3D.Preferences p){_graphicsTier=DuneMainMenu3D.QualityName(p.Graphics);_worldResolution=DuneMainMenu3D.ResolutionName(p.WorldResolution);DuneMainMenu3D.ApplyGraphicsPreferences(GameObject.Scene!,p);}
 bool CaptureDrivingBinding(){
  _polishInputConsumed=false;if(_driveBinding==null)return false;_polishInputConsumed=true;
  if(Input.IsKeyPressed(Key.Escape)){_driveBinding=null;return true;}
  foreach(var key in Enum.GetValues<Key>())if(Input.IsKeyPressed(key)){
   if(key is Key.Enter or Key.KeyPadEnter or Key.Tab||_driveKeys.Any(p=>p.Key!=_driveBinding&&p.Value==key)||_blocks.Any(b=>IsWinch(b.Type)&&WinchActions.Any(a=>BlockKey(b,a)==key))){Text("Driving controls guidance","Key already used. Choose another; Esc cancels.");return true;}
   _driveKeys[_driveBinding]=key;_driveBinding=null;SavePolishPreferences();Text("Driving controls guidance","Saved. Winch keys are set on each winch in Build.");return true;
  }return true;
 }
 void RefreshPolish(float dt){
  if(!WorkshopV3)return;_polishTimer+=dt;_hoverSoundCooldown=Math.Max(0,_hoverSoundCooldown-dt);_cameraImpulse*=MathF.Exp(-9*dt);
  bool driving=!Building;if(driving&&!_polishWasDriving&&_contractRun&&!SprintMission)JobNotice(ContractTitle(CurrentContract).ToUpperInvariant()+" / JOB ACTIVE");_polishWasDriving=driving;
  var hover=_buttons.Where(b=>b.button.GameObject.ActiveInHierarchy&&b.button.Interactable&&b.button.IsHovered).OrderByDescending(b=>b.button.OrderInLayer).FirstOrDefault().button;
  if(hover!=null&&hover!=_hoverSoundTarget&&_hoverSoundCooldown<=0){JobSound("menu-switch",.13f);_hoverSoundCooldown=.09f;}_hoverSoundTarget=hover;
  Text("Settings motion value",$"CAMERA MOTION  {_cameraMotion*100:0}%");Text("Settings sensitivity value",$"ORBIT SENSITIVITY  {_cameraSensitivity:0.00}x");
  if(_ui.TryGetValue("Settings graphics",out var graphics))graphics.GetComponent<UiWidget>()!.Label="GRAPHICS  /  "+_graphicsTier.ToUpperInvariant();
  if(_ui.TryGetValue("Settings resolution",out var resolution))resolution.GetComponent<UiWidget>()!.Label="WORLD RESOLUTION  /  "+_worldResolution.ToUpperInvariant();
  foreach(var (action,_) in DefaultDriveKeys)if(_ui.TryGetValue("Driving binding "+action,out var binding))binding.GetComponent<UiWidget>()!.Label=_driveBinding==action?"PRESS A KEY / ESC TO CANCEL":action.ToUpperInvariant()+"  /  "+DriveKey(action);
  if(_ui.TryGetValue("Recover machine",out var recover))recover.GetComponent<UiWidget>()!.Label="RECOVER ["+DriveKey("Recover")+"]";
  if(!Building&&_ui.TryGetValue("Recovery interaction",out var interaction)){var t=interaction.GetComponent<UiText>()!;t.Text=t.Text.Replace("R safe reset",DriveKey("Recover")+" safe reset").Replace("R reset both vehicles",DriveKey("Recover")+" reset both vehicles").Replace("R restart sprint",DriveKey("Recover")+" restart sprint").Replace("Space brake",DriveKey("Brake")+" brake").Replace("WASD drive",$"{DriveKey("Forward")}/{DriveKey("Reverse")} drive / {DriveKey("Left")}/{DriveKey("Right")} steer");}
  if(Building)Text("Build controls",$"RMB / MMB orbit / Scroll zoom / R yaw / F pitch / G roll / T face / {DriveKey("Test drive")} drive");
  if(!_ui.TryGetValue("Drive tool status",out var tool)||_physics==null){GarageActive("Drive tool status",false);GarageActive("Cable tension meter",false);return;}
  if(DriftMission){bool visible=!Building&&_contractRun&&!_contractComplete;tool.Active=visible;Text("Drive tool status",$"CHAIN +{_driftScore?.ChainScore??0:0}  /  x{_driftScore?.Multiplier??1} MULTIPLIER");GarageActive("Cable tension meter",visible);if(_ui.TryGetValue("Cable tension meter",out var fill))fill.GetComponent<UiWidget>()!.Value=_driftScore?.Fill??0;return;}
  bool cables=!Building&&_contractRun&&WinchMission&&!_contractComplete;tool.Active=cables;
  var winches=_blocks.Where(b=>IsWinch(b.Type)&&_physics.IsWinchConnected(b.Id)).ToArray();
  Text("Drive tool status",winches.Length==0?"WINCH / READY":$"{winches.Length} CABLE{(winches.Length==1?"":"S")} SECURED  /  {WinchTension/1000:0.0} kN");
  GarageActive("Cable tension meter",cables&&winches.Length>0);if(_ui.TryGetValue("Cable tension meter",out var meter)){var w=meter.GetComponent<UiWidget>()!;w.Value=Math.Clamp(WinchTension/20000,0,1);w.FillColor=w.Value>.85f?new(1,.36f,.17f,1):new(.95f,.65f,.22f,1);}
 }
 void LayoutPolishedContracts(float width,float height){
  float panelWidth=Math.Min(964,width-264),left=300,detailWidth=panelWidth-left-48;
  PlaceWidget("Contracts overlay",UiAnchor.TopLeft,new(240,64),new(panelWidth,Math.Min(600,height-80)));
  PlaceWidget("Close garage overlay",UiAnchor.TopLeft,new(240+panelWidth-48,77),new(32,32));
  PlaceWidget("Contract details",UiAnchor.TopLeft,new(left+24,92),new(detailWidth,486));
  PlaceText("Contract name",UiAnchor.TopLeft,new(16,153),24);PlaceText("Contract reward",UiAnchor.TopLeft,new(16,188),25);
  PlaceText("Contract history",UiAnchor.TopLeft,new(112,195),12);PlaceText("Contract brief",UiAnchor.TopLeft,new(16,229),16);PlaceText("Contract parts heading",UiAnchor.TopLeft,new(16,342),12);
  foreach(string name in new[]{"Contract brief","Contract history","Contract requirements"})if(_ui.TryGetValue(name,out var caption))caption.GetComponent<UiText>()!.WrapWidth=name=="Contract history"?detailWidth-140:detailWidth-32;
  for(int i=0;i<4;i++){PlaceWidget("Contract required "+i,UiAnchor.TopLeft,new(16+i*(detailWidth-32)/4,361),new(45,40));PlaceText("Contract required label "+i,UiAnchor.TopLeft,new(16+i*(detailWidth-32)/4,405),12);}
  PlaceText("Contract requirements",UiAnchor.TopLeft,new(16,427),12);PlaceWidget("Deploy contract",UiAnchor.TopLeft,new(16,454),new(detailWidth-32,36));
  PlaceWidget("Contract field image",UiAnchor.TopLeft,new(16,14),new(detailWidth-32,126));
  if(_ui.TryGetValue("Contract field image",out var image))image.GetComponent<UiWidget>()!.ImageReference=new($"Assets/Presentation/contract-{SelectedContract}.png");
  PlaceText("Contracts subtitle",UiAnchor.TopLeft,new(27,68),13);if(_ui.TryGetValue("Contracts subtitle",out var subtitle))subtitle.GetComponent<UiText>()!.WrapWidth=panelWidth-70;
 }
}
