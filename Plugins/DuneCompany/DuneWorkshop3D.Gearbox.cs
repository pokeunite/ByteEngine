using System.Numerics;
using ByteEngine.Core.Graphics;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 public bool HasGearbox=>_blocks.Any(b=>b.Type==39&&GearboxMounts.Connected(b,_blocks,_catalog));
 bool TryGearboxMount(int parent,out Vector3 position,out Quaternion rotation){var engine=_blocks.FirstOrDefault(b=>b.Id==parent);position=default;rotation=Quaternion.Identity;if(engine==null||!GearboxMounts.IsEngine(engine.Type))return false;position=GearboxMounts.Position(engine,_catalog[engine.Type]);rotation=engine.Q;return true;}
 public bool ShiftPlayerGear(int direction)
 {
  if(Building||_physics?.Transmission==null||(_race!=null&&_race.State!=ByteEngine.Core.AI.RaceState.Racing))return false;
  bool shifted=_physics.ShiftGear(direction);if(shifted){foreach(var b in _blocks.Where(b=>b.Type==39))_visuals[b.Id].GetComponent<DuneGearboxAnimation3D>()?.Shift(direction);JobNotice(_physics.Transmission.LastShift);}else if(_physics.Transmission.LastShift.StartsWith("DOWNSHIFT BLOCKED"))JobNotice(_physics.Transmission.LastShift);return shifted;
 }
 UiText? _gearReadout;UiWidget? _rpmBar;UiText? _shiftCue;
 void RefreshGearboxHud()
 {
  var transmission=_physics?.Transmission;bool show=!Building&&transmission!=null;
  if(_gearReadout==null&&show&&GameObject.Scene!.FindGameObject("Garage driving UI") is {} parent){
   var gear=GameObject.Scene.CreateGameObject("Transmission gear and RPM");gear.SetParent(parent,false);_gearReadout=gear.AddComponent(new UiText{Anchor=UiAnchor.BottomRight,Offset=new(-244,-302),FontSize=24,FontReference=new("Assets/GarageUI/fonts/BarlowCondensed-SemiBold.ttf"),Color=new(.95f,.93f,.86f,1),OrderInLayer=34});
   var bar=GameObject.Scene.CreateGameObject("Transmission RPM gauge");bar.SetParent(parent,false);_rpmBar=bar.AddComponent(new UiWidget{Kind=UiWidgetKind.ProgressBar,Anchor=UiAnchor.BottomRight,Offset=new(-244,-266),Size=new(220,10),Maximum=7000,Interactable=false,Color=new(.08f,.1f,.08f,1),FillColor=new(.9f,.65f,.22f,1),OrderInLayer=34});
   var cue=GameObject.Scene.CreateGameObject("Transmission shift cue");cue.SetParent(parent,false);_shiftCue=cue.AddComponent(new UiText{Anchor=UiAnchor.BottomRight,Offset=new(-244,-244),FontSize=18,FontReference=new("Assets/GarageUI/fonts/BarlowCondensed-SemiBold.ttf"),OrderInLayer=34});
  }
  if(_gearReadout==null)return;_gearReadout.GameObject.Active=_rpmBar!.GameObject.Active=_shiftCue!.GameObject.Active=show;if(!show)return;
  _gearReadout.Text=$"GEAR {transmission!.Gear}   {transmission.Rpm:0} RPM";_rpmBar.Value=transmission.Rpm;
  var color=transmission.Limiter?new Vector4(1,.2f,.12f,1):transmission.ShiftReady?new Vector4(.4f,1,.45f,1):new Vector4(.92f,.68f,.28f,1);_rpmBar.FillColor=color;_shiftCue.Color=color;
  _shiftCue.Text=transmission.ShiftRemaining>0?"SHIFTING":transmission.Limiter?"REV LIMIT / SHIFT UP":transmission.ShiftReady?"SHIFT UP NOW":"SHIFT WINDOW 5900–6800 RPM";
  _shiftCue.Text+=$"\n{DriveKey("Shift up")} UP / {DriveKey("Shift down")} DOWN";
 }
}
