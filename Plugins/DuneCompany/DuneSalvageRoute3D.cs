using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
namespace DuneCompany;
public sealed class DuneSalvageRoute3D:Component
{
 public Vector3 Garage{get;set;}=Vector3.Zero;public Vector3 SalvageYard{get;set;}=new(160,0,-360);public float ArrivalRadius{get;set;}=18;public float BoundaryMargin{get;set;}=18;
 public bool ActiveMission{get;private set;}public bool Arrived{get;private set;}public bool Completed{get;private set;}public bool Quit{get;private set;}
 DuneWorkshop3D? _vehicle;UiText? _objective;UiWidget? _quitButton,_returnButton;bool _lastBuilding=true;
 public override int UpdateOrder=>80;
 protected override void OnStart(){ActiveMission=Arrived=Completed=Quit=false;_vehicle=null;_objective=null;_lastBuilding=true;}
 void Bind(){var scene=GameObject.Scene!;_vehicle??=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneWorkshop3D>().FirstOrDefault();_objective??=scene.FindGameObject("Battle objective")?.GetComponent<UiText>();if(_quitButton!=null)return;var canvas=scene.GameObjects.FirstOrDefault(o=>o.GetComponent<UiCanvas>()!=null);if(canvas==null)return;
  UiWidget Button(string name,string label,float x){var obj=scene.CreateGameObject(name);obj.SetParent(canvas,false);return obj.AddComponent(new UiWidget{Kind=UiWidgetKind.Button,Offset=new(x,320),Size=new(124,34),Label=label,FontSize=15,Color=new(.12f,.15f,.12f,.96f),HoverColor=new(.24f,.3f,.18f,1),OrderInLayer=25});}
  _quitButton=Button("Quit mission","QUIT MISSION",996);_returnButton=Button("Return to base","RETURN TO BASE",1128);
 }
 public void BeginMission(){ActiveMission=true;Arrived=Completed=Quit=false;}
 public void QuitMission(){if(!ActiveMission)return;ActiveMission=false;Quit=true;Completed=false;}
 public void CompleteMission(){if(!ActiveMission||!Arrived)return;Completed=true;ActiveMission=false;}
 public bool ReturnToBase(){Bind();if(ActiveMission||_vehicle==null)return false;_vehicle.Command("Return to base");Arrived=false;_lastBuilding=true;return true;}
 protected override void OnUpdate(){Bind();if(_vehicle==null||_objective==null)return;if(_lastBuilding&&!_vehicle.Building)BeginMission();_lastBuilding=_vehicle.Building;var p=_vehicle.Transform.WorldPosition;float distance=Vector2.Distance(new(p.X,p.Z),new(SalvageYard.X,SalvageYard.Z));
  if(ActiveMission&&distance<Math.Clamp(ArrivalRadius,5,40))Arrived=true;
  if(ActiveMission&&Arrived&&distance<ArrivalRadius&&Math.Abs(_vehicle.Speed)<2&&Input.IsKeyPressed(Key.E))CompleteMission();
  if(_quitButton!=null){_quitButton.Visible=!_vehicle.Building&&ActiveMission;_returnButton!.Visible=!_vehicle.Building&&!ActiveMission;if(_quitButton.WasClicked)QuitMission();if(_returnButton.WasClicked)ReturnToBase();}
  var mode=GameObject.Scene!.FindGameObject("Run / build")?.GetComponent<UiWidget>();if(mode!=null)mode.Interactable=_vehicle.Building||(!ActiveMission&&Vector2.Distance(new(p.X,p.Z),new(Garage.X,Garage.Z))<22);
  _objective.Text=_vehicle.Building?"SALVAGE CONTRACT / Deploy your rover when ready":ActiveMission?(Arrived?"STOP AT THE YARD / Press E to collect salvage and finish":$"SALVAGE CONTRACT / {distance:0} m / Building locked during mission"):Completed?"CONTRACT COMPLETE / Return to base to build and redeploy":"MISSION QUIT / Return to base to build and redeploy";
 }
 protected override void OnStop(){ActiveMission=false;_vehicle=null;_objective=null;_quitButton=_returnButton=null;}
}
