using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 GameObject? _highlight;int _highlighted=-1;
 public int HighlightedBlock=>_highlighted;
 void UpdateFeedback()
 {
  int id=_tuning>=0?_tuning:_hover>=0&&_tool is "Tune" or "Erase" or "Move"?_hover:_selected;
  if(!Building||id<0||!_visuals.ContainsKey(id)){if(_highlight!=null)_highlight.Active=false;_highlighted=-1;return;}
  if(_highlight==null){_highlight=GameObject.Scene!.CreateGameObject("Block selection outline");_highlight.SetParent(GameObject,false);for(int i=0;i<12;i++){var edge=GameObject.Scene!.CreateGameObject("Selection edge "+i);edge.SetParent(_highlight,false);edge.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Cube,CastShadows=false,Material=new(){Shading=MaterialShadingMode.Unlit,BaseColor=new(1,.78f,.22f,1)}});}}
  _highlight.Active=true;_highlighted=id;var block=_blocks.First(b=>b.Id==id);var def=_catalog[block.Type];_highlight.Transform.LocalPosition=block.P;_highlight.Transform.LocalRotation=block.Q;var low=def.Low-new Vector3(.014f);var high=def.High+new Vector3(.014f);var mid=(low+high)*.5f;var size=high-low;int index=0;
  for(int axis=0;axis<3;axis++)for(int a=0;a<2;a++)for(int b=0;b<2;b++){var edge=_highlight.Children[index++];var p=mid;var s=new Vector3(.012f);if(axis==0){p.Y=a==0?low.Y:high.Y;p.Z=b==0?low.Z:high.Z;s.X=size.X;}else if(axis==1){p.X=a==0?low.X:high.X;p.Z=b==0?low.Z:high.Z;s.Y=size.Y;}else{p.X=a==0?low.X:high.X;p.Y=b==0?low.Y:high.Y;s.Z=size.Z;}edge.Transform.LocalPosition=p;edge.Transform.LocalScale=s;edge.GetComponent<MeshRenderer>()!.Material.BaseColor=_tool=="Erase"?new(1,.2f,.15f,1):_tool=="Tune"?new(1,.78f,.22f,1):new(.6f,.85f,.4f,1);}
  if(_tool=="Tune"&&_hover>=0)Text("Placement instruction","TUNE / "+def.Label+" / Click the highlighted part");
 }
 void ApplyPresentation()
 {
  foreach(var (name,o) in _ui){if(name.EndsWith(" glyph",StringComparison.Ordinal)&&o.GetComponent<UiWidget>() is {} image&&_ui.TryGetValue(name[..^6],out var button)&&button.GetComponent<UiWidget>() is {} w){image.Size=new(22);image.Offset=w.Offset+(w.Size-image.Size)*.5f;image.Color=new(.9f,.93f,.88f,1);}if(o.GetComponent<UiText>() is {} text)text.ShadowColor=new(0,0,0,.65f);}
  SetRect("Part tray",new(0,606),new(1280,114));SetRect("Tray edge",new(0,606),new(1280,1));
  foreach(var o in _ui.Values.Where(o=>o.GetComponent<UiWidget>()?.Kind==UiWidgetKind.Panel)){var w=o.GetComponent<UiWidget>()!;if(w.Size.X>1200&&w.Offset.Y>600){w.Offset=new(0,606);w.Size=new(1280,114);}}
  for(int i=0;i<6;i++){float x=238+i*106;SetRect("Part selection "+i,new(x,612),new(100,100));SetRect($"Button {239+i*106},633",new(x+1,613),new(98,98));SetRect("Part "+i,new(x+18,619),new(64));if(_ui.TryGetValue("Part name "+i,out var label)&&label.GetComponent<UiText>() is {} t){t.Offset=new(x+5,687);t.FontSize=15;t.WrapWidth=92;t.Color=new(.91f,.94f,.88f,1);}}
  if(_ui.TryGetValue("Category 4",out var body)&&body.GetComponent<UiWidget>() is {} icon)icon.ImageReference=new("Assets/GarageUI/workshop/square.png");
  for(int i=0;i<5;i++){SetRect($"Button {8+i*42},639",new(8+i*42,629),new(38,73));SetRect("Category "+i,new(13+i*42,651),new(28));}
  if(_ui.TryGetValue("Selected block",out var title)&&title.GetComponent<UiText>() is {} selected){selected.Offset=new(936,619);selected.FontSize=20;}if(_ui.TryGetValue("Block function",out var details)&&details.GetComponent<UiText>() is {} detail){detail.Offset=new(936,650);detail.FontSize=16;detail.WrapWidth=310;detail.Color=new(.84f,.89f,.81f,1);}
  if(_ui.TryGetValue("Placement instruction",out var hint)&&hint.GetComponent<UiText>() is {} h){h.Offset=new(26,554);h.FontSize=15;h.Color=new(.97f,.97f,.93f,1);h.WrapWidth=950;}
  if(_ui.TryGetValue("Build controls",out var controls)&&controls.GetComponent<UiText>() is {} c){c.Offset=new(26,580);c.FontSize=15;c.Color=new(.78f,.86f,.76f,1);}
  if(_ui.TryGetValue("Placement status",out var status))status.Active=false;
  if(_ui.TryGetValue("Battle objective",out var objective)&&objective.GetComponent<UiText>() is {} objectiveText){objectiveText.Offset=new(26,73);objectiveText.FontSize=20;objectiveText.Color=new(.94f,.95f,.89f,1);}
  if(!_ui.ContainsKey("Contract background")){var panel=GameObject.Scene!.CreateGameObject("Contract background");panel.SetParent(_ui.Values.First(o=>o.GetComponent<UiCanvas>()!=null),false);panel.AddComponent(new UiWidget{Offset=new(14,64),Size=new(690,42),Color=new(.04f,.06f,.05f,.85f),Interactable=false,OrderInLayer=0});_ui[panel.Name]=panel;}
  if(!_ui.ContainsKey("Build guidance background")){var background=GameObject.Scene!.CreateGameObject("Build guidance background");background.SetParent(_ui.Values.First(o=>o.GetComponent<UiCanvas>()!=null),false);background.AddComponent(new UiWidget{Offset=new(14,544),Size=new(1010,55),Color=new(.04f,.06f,.05f,.9f),Interactable=false,OrderInLayer=0});_ui[background.Name]=background;}
 }
 void SetRect(string key,Vector2 position,Vector2 size){if(_ui.TryGetValue(key,out var obj)&&obj.GetComponent<UiWidget>() is {} w){w.Offset=position;w.Size=size;}}
}
