using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Assets;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 UiWidget? _speedDial;UiText? _speedNumber;Speedometer? _speedometer;
 void RefreshSpeedometer(){
  if(!GarageUx)return;
  if(_speedDial==null){var parent=GameObject.Scene!.FindGameObject("Garage driving UI");if(parent==null)return;
   var dial=GameObject.Scene.CreateGameObject("Driving speedometer");dial.SetParent(parent,false);_speedDial=dial.AddComponent(new UiWidget{Kind=UiWidgetKind.Image,Anchor=UiAnchor.BottomRight,Offset=new(-24,-90),Size=new(208,148),Color=Vector4.One,Interactable=false,OrderInLayer=30});
   var text=GameObject.Scene.CreateGameObject("Driving speed readout");text.SetParent(parent,false);_speedNumber=text.AddComponent(new UiText{Anchor=UiAnchor.BottomRight,Offset=new(-88,-104),FontSize=26,Color=new(.95f,.93f,.86f,1),OrderInLayer=31});
  }
  _speedometer??=_speedDial.GameObject.AddComponent(new Speedometer{ReadoutObject="Driving speed readout",DialPathPrefix="Assets/GritGarage/Speedometer/dial-",MaximumSpeed=120});
  _speedometer.SetSpeed(Speed);_speedometer.Refresh();
  if(_ui.TryGetValue("Machine status",out var old))old.Active=Building;
 }
}
