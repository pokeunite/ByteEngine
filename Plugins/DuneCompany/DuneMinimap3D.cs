using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
namespace DuneCompany;
/// <summary>North-up map of the whole drivable blockout, with a live vehicle marker.</summary>
public sealed class DuneMinimap3D:Component
{
 public float WorldSize{get;set;}=1024;public string ImagePath{get;set;}="Assets/MissionWorld/minimap.png";
 GameObject? _root;UiWidget? _player;Minimap? _map;UiText? _heading;DuneWorkshop3D? _vehicle;
 public override int UpdateOrder=>90;
 protected override void OnUpdate(){var scene=GameObject.Scene!;_vehicle??=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneWorkshop3D>().FirstOrDefault();if(_vehicle==null)return;if(_root==null){var canvas=scene.GameObjects.FirstOrDefault(o=>o.GetComponent<UiCanvas>()!=null);if(canvas==null)return;_root=scene.CreateGameObject("Mission minimap");_root.SetParent(canvas,false);_root.AddComponent(new UiWidget{Offset=new(996,64),Size=new(256,248),Color=new(.04f,.06f,.05f,.96f),Interactable=false,OrderInLayer=20});var image=scene.CreateGameObject("Mission map image");image.SetParent(canvas,false);image.AddComponent(new UiWidget{Kind=UiWidgetKind.Image,Offset=new(1004,88),Size=new(240,216),ImageReference=new AssetReference(ImagePath),Color=Vector4.One,Interactable=false,OrderInLayer=21});var marker=scene.CreateGameObject("Minimap player marker");marker.SetParent(canvas,false);_player=marker.AddComponent(new UiWidget{Kind=UiWidgetKind.Panel,Size=new(8),Color=new(1,.86f,.25f,1),Interactable=false,OrderInLayer=24});var title=scene.CreateGameObject("Minimap heading");title.SetParent(canvas,false);_heading=title.AddComponent(new UiText{Offset=new(1007,68),FontSize=14,Color=new(.91f,.94f,.84f,1),OrderInLayer=24});}
  var p=_vehicle.Transform.WorldPosition;_map??=_player!.GameObject.AddComponent(new Minimap{MapOffset=new(1004,88),MapSize=new(240,216)});_map.WorldSize=new(WorldSize);_map.SetPosition(p);float heading=MathF.Atan2(-_vehicle.Transform.Forward.X,-_vehicle.Transform.Forward.Z)*180/MathF.PI;_heading!.Text=$"N   DUNE COMPANY   {((heading+360)%360):0}°";
 }
 protected override void OnStop(){_root=null;_player=null;_map=null;_heading=null;_vehicle=null;}
}
