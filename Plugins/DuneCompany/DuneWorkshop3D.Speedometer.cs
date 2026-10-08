using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Assets;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 UiWidget? _speedDial;UiText? _speedNumber;int _speedFrame=-1;
 void RefreshSpeedometer(){
  if(!GarageUx)return;
  if(_speedDial==null){var parent=GameObject.Scene!.FindGameObject("Garage driving UI");if(parent==null)return;
   var dial=GameObject.Scene.CreateGameObject("Driving speedometer");dial.SetParent(parent,false);_speedDial=dial.AddComponent(new UiWidget{Kind=UiWidgetKind.Image,Anchor=UiAnchor.BottomRight,Offset=new(-24,-90),Size=new(208,148),Color=Vector4.One,Interactable=false,OrderInLayer=30});
   var text=GameObject.Scene.CreateGameObject("Driving speed readout");text.SetParent(parent,false);_speedNumber=text.AddComponent(new UiText{Anchor=UiAnchor.BottomRight,Offset=new(-88,-104),FontSize=26,Color=new(.95f,.93f,.86f,1),OrderInLayer=31});
  }
  float kph=Math.Abs(Speed)*3.6f;int frame=(int)Math.Clamp(MathF.Round(kph/2),0,60);
  if(frame!=_speedFrame){_speedDial.ImageReference=new AssetReference($"Assets/GritGarage/Speedometer/dial-{frame:00}.png");_speedFrame=frame;}
  _speedNumber!.Text=$"{kph:0} km/h";
  if(_ui.TryGetValue("Machine status",out var old))old.Active=Building;
 }
}
