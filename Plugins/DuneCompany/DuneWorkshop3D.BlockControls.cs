using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 static readonly string[] WinchActions=["Attach","Reel in","Pay out"];
 string PartFunction(DunePart part)=>part.Id==33?"Independent cable winch. Attaches to vehicle sockets or steel eyes. Tune its pull, reel speed and key bindings below.":part.Function;
 string? _bindingAction;bool _bindingConsumed;
 bool UsesBlockKey(Key key)=>!Building&&_blocks.Any(b=>IsWinch(b.Type)&&WinchActions.Any(a=>BlockKey(b,a)==key));
 Key BlockKey(PlacedBlock b,string action){
  if(b.ControlKeys.TryGetValue(action,out var stored)&&Enum.TryParse<Key>(stored,out var key)&&Enum.IsDefined(key))return key;
  int slot=Array.IndexOf(_blocks.Where(p=>IsWinch(p.Type)).OrderBy(p=>p.Id).ToArray(),b);
  return (slot%3,action) switch{(0,"Attach")=>Key.H,(0,"Reel in")=>Key.E,(0,_)=>Key.Q,(1,"Attach")=>Key.J,(1,"Reel in")=>Key.U,(1,_)=>Key.O,(2,"Attach")=>Key.K,(2,"Reel in")=>Key.Y,_=>Key.P};
 }
 void BindBlockControls(){
  if(!_ui.TryGetValue("Block tuning panel",out var parent))return;
  foreach(var (action,index) in WinchActions.Select((a,i)=>(a,i))){
   var obj=GameObject.Scene!.CreateGameObject("Control binding "+action);obj.SetParent(parent,false);
   obj.AddComponent(new UiWidget{Kind=UiWidgetKind.Button,Offset=new(20,113+index*29),Size=new(240,27),Label=action,FontReference=new AssetReference("Assets/GarageUI/fonts/Barlow-Regular.ttf"),FontSize=16,Color=new(.16f,.17f,.15f,1),HoverColor=new(.32f,.25f,.15f,1),OrderInLayer=15});
   _ui[obj.Name]=obj;string selected=action;Bind(obj.Name,()=>{_typing=_drag=-1;_bindingAction=selected;RefreshBlockBindings();});obj.Active=false;
  }
 }
 void RefreshBlockBindings(){
  var b=_blocks.FirstOrDefault(b=>b.Id==_tuning);bool show=Building&&b!=null&&IsWinch(b.Type);
  foreach(var action in WinchActions)if(_ui.TryGetValue("Control binding "+action,out var obj)){
   obj.Active=show;if(!show)continue;var key=BlockKey(b!,action);
   bool shared=_blocks.Any(p=>IsWinch(p.Type)&&p.Id!=b!.Id&&WinchActions.Any(a=>BlockKey(p,a)==key));
   obj.GetComponent<UiWidget>()!.Label=_bindingAction==action?"PRESS KEY / ESC CANCEL":action.ToUpperInvariant()+"  ["+key+"]"+(shared?"  SHARED":"");
  }
 }
 bool CaptureBlockBinding(){
  _bindingConsumed=false;if(_bindingAction==null)return false;_bindingConsumed=true;
  if(Input.IsKeyPressed(Key.Escape)){_bindingAction=null;RefreshBlockBindings();return true;}
  var b=_blocks.FirstOrDefault(b=>b.Id==_tuning);if(b==null){_bindingAction=null;return true;}
  foreach(var key in Enum.GetValues<Key>())if(Input.IsKeyPressed(key)){
   if(_driveKeys.Values.Contains(key)||key is Key.Escape or Key.Tab or Key.Enter or Key.KeyPadEnter){_message="That key is used for driving or menus. Choose another key.";return true;}
   Record();b.ControlKeys[_bindingAction]=key.ToString();_bindingAction=null;BuildJson=Snapshot();RefreshBlockBindings();break;
  }
  return true;
 }
}
