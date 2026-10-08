using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
namespace DuneCompany;
public sealed partial class DuneMainMenu3D
{
 bool _newGameConfirmation;
 void EnsureNewGameConfirmation()
 {
  var scene=GameObject.Scene!;if(scene.FindGameObject("New Game confirmation")!=null)return;
  var canvas=scene.GameObjects.FirstOrDefault(o=>o.GetComponent<UiCanvas>()!=null)??scene.CreateGameObject("Menu confirmation canvas");
  if(canvas.GetComponent<UiCanvas>()==null)canvas.AddComponent(new UiCanvas());
  var font=new AssetReference("Assets/GarageUI/fonts/BarlowCondensed-SemiBold.ttf");
  GameObject Widget(string name,GameObject parent,Vector2 position,Vector2 size,Vector4 color,int order){var obj=scene.CreateGameObject(name);obj.SetParent(parent,false);obj.AddComponent(new UiWidget{Kind=UiWidgetKind.Panel,Offset=position,Size=size,Color=color,Interactable=false,OrderInLayer=order});return obj;}
  var overlay=Widget("New Game confirmation",canvas,Vector2.Zero,new(1280,720),new(0,0,0,.7f),100);overlay.GetComponent<UiWidget>()!.StretchHorizontal=overlay.GetComponent<UiWidget>()!.StretchVertical=true;
  var panel=Widget("New Game confirmation panel",overlay,new(340,220),new(600,270),new(.06f,.065f,.06f,1),101);
  void Text(string name,string text,Vector2 offset,int size,Vector4 color){var obj=scene.CreateGameObject(name);obj.SetParent(panel,false);obj.AddComponent(new UiText{Text=text,Offset=offset,FontSize=size,FontReference=font,Color=color,OrderInLayer=104});}
  Text("New Game confirmation title","START A NEW GAME?",new(28,22),30,new(.95f,.9f,.8f,1));
  Text("New Game confirmation explanation","Your vehicle, contract progress and money will reset.\nSaved blueprints and settings will be kept.",new(28,73),21,new(.75f,.75f,.7f,1));
  Text("New Game confirmation error","",new(28,136),16,new(1,.4f,.25f,1));
  foreach(var (name,label,x,gold) in new[]{("Cancel New Game button","CANCEL",28f,false),("Confirm New Game button","START NEW GAME",308f,true)}){var obj=Widget(name,panel,new(x,199),new(264,45),gold?new(.85f,.51f,.15f,1):new(.16f,.17f,.16f,1),103);var button=obj.GetComponent<UiWidget>()!;button.Kind=UiWidgetKind.Button;button.Label=label;button.FontReference=font;button.FontSize=20;button.HoverColor=new(.55f,.37f,.17f,1);button.Interactable=true;}
  overlay.Active=false;
 }
 void ShowNewGameConfirmation(){EnsureNewGameConfirmation();_newGameConfirmation=true;GameObject.Scene!.FindGameObject("New Game confirmation")!.Active=true;UiNavigation.Focus(GameObject.Scene.FindGameObject("Cancel New Game button")!.GetComponent<UiWidget>());}
 void CloseNewGameConfirmation(){_newGameConfirmation=false;if(GameObject.Scene?.FindGameObject("New Game confirmation") is {} panel)panel.Active=false;}
 void ConfirmNewGame()
 {
  if(!_newGameConfirmation)return;
  try{DuneCampaignSaves.Reset(SaveRoot);NewGamePending=true;ContinuePending=false;CloseNewGameConfirmation();GameObject.Scene!.RequestLoad("Scenes/Workshop.bytescene");}
  catch(Exception error)when(error is IOException or UnauthorizedAccessException){var label=GameObject.Scene!.FindGameObject("New Game confirmation error")!.GetComponent<UiText>()!;label.Text="Could not reset saves. "+error.Message;}
 }
}
