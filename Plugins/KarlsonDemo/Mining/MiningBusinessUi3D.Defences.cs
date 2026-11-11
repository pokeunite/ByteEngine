using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 public List<Guid> DefenceButtonObjectIds{get;set;}=[];
 public string DefenceButtonBindings{get=>string.Join(";",DefenceButtonObjectIds);set=>DefenceButtonObjectIds=(value??"").Split(';',StringSplitOptions.RemoveEmptyEntries).Select(s=>Guid.TryParse(s,out var id)?id:Guid.Empty).Where(id=>id!=Guid.Empty).ToList();}
 readonly object defencePointerOwner=new();
 readonly Dictionary<MiningDefenseKind,int> lastReady=[];readonly Dictionary<MiningDefenseKind,double> readyGlow=[];
 MiningDefensePlanner3D? DefencePlanner=>GameObject.Scene?.FindGameObject(Op?.PlayerObjectId??Guid.Empty)?.GetComponent<MiningDefensePlanner3D>();
 public void SelectDefence(int index)
 {
  if(CampEditing||Screen!=MiningScreen.None||!Enum.IsDefined((MiningDefenseKind)index)||DefencePlanner is not {} planner)return;
  if(planner.SelectBuild((MiningDefenseKind)index)){foreach(var id in DefenceButtonObjectIds)if(W(id) is {} button)button.Interactable=false;Input.RequestUiPointer(defencePointerOwner,false);MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.Confirm);}
 }
 void UpdateDefenceHud()
 {
  Input.RequestUiPointer(defencePointerOwner,false);
  foreach(var id in DefenceButtonObjectIds)if(W(id) is {} widget){widget.Visible=false;widget.Interactable=false;}
  if(DefencePlanner is not {} planner)return;
  foreach(var kind in Enum.GetValues<MiningDefenseKind>()){int available=planner.Available(kind);if(available>lastReady.GetValueOrDefault(kind))readyGlow[kind]=Time.TotalTime+3;lastReady[kind]=available;}
 }
 public int ReadyDefenceHudCount=>DefencePlanner is {} p?Enum.GetValues<MiningDefenseKind>().Count(k=>p.Available(k)>0):0;
 void DrawDefenceHud(RenderContext c,float width,float height,float scale)
 {
  if(DefencePlanner is not {} planner||Op?.Phase==MiningPhase.Failed)return;
  if(!planner.BuildMode){if(!CombatHud){FieldHint(c,$"[{MiningPauseMenu3D.KeyLabel(planner.BuildKey)}] Build",width-214,height-118,194,scale);}return;}
  DrawSupportBuildTray(c,width,height,scale,planner);
 }
}
