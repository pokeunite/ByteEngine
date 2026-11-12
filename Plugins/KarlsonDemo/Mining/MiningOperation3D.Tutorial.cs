using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;
namespace KarlsonDemo.Plugin;
public enum CampTutorialStep{AmmoPress,StartDrill,WinRound,Refinery,WaitForRefining,CompanyHub,WaitForSale,Complete}
public sealed partial class MiningOperation3D
{
 [Category("New game tutorial")]public bool TutorialEnabled{get;set;}=true;
 [Category("New game tutorial")]public bool ShowTutorialRoute{get;set;}=true;
 [Category("New game tutorial")]public int TutorialStartingReserve{get;set;}=12;
 [Browsable(false)]public CampTutorialStep TutorialStep{get;private set;}
 [Browsable(false)]public string TutorialPopup{get;private set;}="";
 [Browsable(false)]public bool TutorialActive=>TutorialEnabled&&TutorialStep!=CampTutorialStep.Complete;
 bool tutorialAmmoPrepared;float tutorialPopupClock,tutorialPathClock;const long tutorialCollection=0;
 IReadOnlyList<Vector3> tutorialPath=[];
 readonly Material tutorialLineMaterial=new(){Shading=MaterialShadingMode.Unlit,BaseColor=new(1,.76f,.12f,1),EmissionEnabled=true,EmissionColor=new(1,.64f,.05f),EmissionIntensity=.7f};
 MiningBusinessUi3D? TutorialUi=>GameObject.Scene?.GameObjects.SelectMany(o=>o.Components.OfType<MiningBusinessUi3D>()).FirstOrDefault();
 public string TutorialInstruction=>TutorialStep switch{
 CampTutorialStep.AmmoPress=>"Collect ammo from the ammo press bucket",
 CampTutorialStep.StartDrill=>ExpeditionActive?$"Prepare your camp / first wave in {PreparationRemaining:0}s":"Approach the drill and start your first operation",
 CampTutorialStep.WinRound=>"Defend the drill / eliminate the incoming spiders",
 CampTutorialStep.Refinery=>"Take your raw ore to the refinery",
 CampTutorialStep.WaitForRefining=>"The refinery is turning raw ore into refined ore",
 CampTutorialStep.CompanyHub=>"Use the company tent: MARKET / sell refined ore",
 CampTutorialStep.WaitForSale=>"Your ore is shipping / payout arrives automatically",_=>"Camp basics complete"};
 public void SkipCampTutorial(){TutorialStep=CampTutorialStep.Complete;DisposeTutorialRoute();tutorialPath=[];SaveProgress();}
 void TutorialAdvance(CampTutorialStep next,string popup="")
 {TutorialStep=next;DisposeTutorialRoute();tutorialPath=[];tutorialPathClock=0;TutorialPopup=popup;tutorialPopupClock=5;SaveProgress();}
 public void TickCampTutorial(float delta)
 {
  if(!float.IsFinite(delta)||delta<=0||GameObject.Scene is not {} scene)return;
  tutorialPopupClock-=delta;if(tutorialPopupClock<=0)TutorialPopup="";
  if(!TutorialActive)return;
  var ui=TutorialUi;var bucket=scene.FindGameObject(ui?.AmmoMachineObjectId??Guid.Empty)?.GetComponent<MiningAmmoBucket3D>();
  if(TutorialStep==CampTutorialStep.AmmoPress)
  {
   if((bucket?.CollectionSequence??0)<=tutorialCollection&&Player?.GetComponent<FpsGunLab3D>()?.SelectedWeapon is {} weapon&&(!tutorialAmmoPrepared||weapon.Definition is {} definition&&weapon.ReserveAmmo>=definition.ReserveCapacity)){weapon.SetAmmo(weapon.CurrentAmmo,Math.Min(weapon.ReserveAmmo,Math.Max(0,TutorialStartingReserve)));tutorialAmmoPrepared=true;SaveProgress();}
   if(bucket!=null&&bucket.CollectionSequence>tutorialCollection)TutorialAdvance(CampTutorialStep.StartDrill,"Ammo collected. The press produces reserve ammo.");
   // If the player starts a round first, don't strand them in an irrelevant step.
   else if(Phase==MiningPhase.Drilling)TutorialAdvance(CampTutorialStep.WinRound);
  }
  if(TutorialStep==CampTutorialStep.StartDrill&&Phase==MiningPhase.Drilling)TutorialAdvance(CampTutorialStep.WinRound);
  if(TutorialStep==CampTutorialStep.WinRound&&CompletedRounds>0&&Phase==MiningPhase.Preparation)TutorialAdvance(CampTutorialStep.Refinery,"Wave cleared. Your raw ore is ready to refine.");
  if(TutorialStep==CampTutorialStep.WinRound&&CompletedRounds==0&&Phase==MiningPhase.Preparation&&!ExpeditionActive)TutorialAdvance(CampTutorialStep.StartDrill,"Try again. Return to the drill when ready.");
  if(TutorialStep==CampTutorialStep.Refinery){if(RefiningAmount>0)TutorialAdvance(CampTutorialStep.WaitForRefining);else if(RefinedOre>0)TutorialAdvance(CampTutorialStep.CompanyHub);}
  if(TutorialStep==CampTutorialStep.WaitForRefining&&RefinedOre>0)TutorialAdvance(CampTutorialStep.CompanyHub,"Refined ore ready. Sell it at the company terminal.");
  if(TutorialStep==CampTutorialStep.CompanyHub&&PendingMarketSale is {Refined:true})TutorialAdvance(CampTutorialStep.WaitForSale);
  if(TutorialStep is CampTutorialStep.CompanyHub or CampTutorialStep.WaitForSale&&PendingMarketSale==null&&LastMarketSale is {Refined:true})TutorialAdvance(CampTutorialStep.Complete,"First sale complete. Reinvest in your camp.");
  tutorialPathClock-=delta;
  if(tutorialPathClock<=0){tutorialPathClock=Math.Clamp(TutorialRouteRefreshSeconds,.1f,2);if(Player is {} moving&&TutorialDestination(ui) is {} goal&&(!TutorialRouteReady||tutorialRibbon==null||Vector3.DistanceSquared(moving.Transform.WorldPosition,tutorialLastStart)>.16f||Vector3.DistanceSquared(goal.Transform.WorldPosition,tutorialLastTarget)>.16f)){tutorialLastStart=moving.Transform.WorldPosition;tutorialLastTarget=goal.Transform.WorldPosition;UpdateTutorialPath(ui);BuildTutorialRoute();}}
 }
 GameObject? TutorialDestination(MiningBusinessUi3D? ui)=>GameObject.Scene?.FindGameObject(TutorialStep switch{CampTutorialStep.AmmoPress=>ui?.AmmoMachineObjectId??Guid.Empty,CampTutorialStep.StartDrill=>DrillObjectId,CampTutorialStep.Refinery=>ui?.RefineryMachineObjectId??Guid.Empty,CampTutorialStep.CompanyHub=>ui?.TerminalObjectId??Guid.Empty,_=>Guid.Empty});
 void UpdateTutorialPath(MiningBusinessUi3D? ui)
 {
  TutorialRouteReady=false;tutorialPath=[];if(Player is not {} player||TutorialDestination(ui) is not {} target)return;
  Vector3 start=player.Transform.WorldPosition;
  var bounds=MiningCampGeometry.Transform(MiningCampGeometry.LocalBounds(target),target.Transform.WorldMatrix);
  var centre=(bounds.Minimum+bounds.Maximum)/2;centre.Y=start.Y;
  // End outside the machine footprint, on the nearest reachable side.
  var candidates=new[]{new Vector3(bounds.Minimum.X-1.4f,start.Y,Math.Clamp(start.Z,bounds.Minimum.Z,bounds.Maximum.Z)),new Vector3(bounds.Maximum.X+1.4f,start.Y,Math.Clamp(start.Z,bounds.Minimum.Z,bounds.Maximum.Z)),new Vector3(Math.Clamp(start.X,bounds.Minimum.X,bounds.Maximum.X),start.Y,bounds.Minimum.Z-1.4f),new Vector3(Math.Clamp(start.X,bounds.Minimum.X,bounds.Maximum.X),start.Y,bounds.Maximum.Z+1.4f)};
  var region=GameObject.Scene!.GameObjects.SelectMany(o=>o.Components.OfType<NavigationRegion3D>()).FirstOrDefault(r=>r.Enabled&&r.Ready);
  foreach(var end in candidates.OrderBy(p=>Vector3.DistanceSquared(p,start))){var path=region?.FindPath(start,end,4000);if(path is {Count:>1}){tutorialPath=path;TutorialRouteReady=true;return;}}
  // No baked navigation: a short direction marker, never a fake route through walls.
  var dir=centre-start;if(dir.LengthSquared()>1)tutorialPath=[start,start+Vector3.Normalize(dir)*Math.Min(2,dir.Length())];
 }
 public void DrawCampTutorial(RenderContext c,AssetReference font,bool showWorld)
 {
  if(!TutorialEnabled||!TutorialActive&&tutorialPopupClock<=0)return;
  float s=Math.Min(c.TargetWidth/1280f,c.TargetHeight/720f),h=c.TargetHeight/s;
  bool card=TutorialActive&&!(ExpeditionActive&&Phase==MiningPhase.Preparation&&TutorialStep is CampTutorialStep.StartDrill or CampTutorialStep.WinRound);
  int step=TutorialStep==CampTutorialStep.AmmoPress?1:TutorialStep is CampTutorialStep.StartDrill or CampTutorialStep.WinRound?2:TutorialStep is CampTutorialStep.Refinery or CampTutorialStep.WaitForRefining?3:4;
  string title=TutorialStep switch{CampTutorialStep.AmmoPress=>"Collect your ammunition",CampTutorialStep.StartDrill=>"Begin your first contract",CampTutorialStep.WinRound=>"Defend the drill",CampTutorialStep.Refinery=>"Refine your ore",CampTutorialStep.WaitForRefining=>"Refining in progress",CampTutorialStep.CompanyHub=>"Sell your refined ore",_=>"Shipment on its way"};
  string detail=TutorialStep switch{CampTutorialStep.AmmoPress=>"Walk up to the ammo press bucket.",CampTutorialStep.StartDrill=>"Choose a site at the drill.",CampTutorialStep.WinRound=>"Clear the incoming spiders.",CampTutorialStep.Refinery=>"Load raw ore at a refinery.",CampTutorialStep.WaitForRefining=>"Prepare while the machine processes.",CampTutorialStep.CompanyHub=>"Use Trade in the company terminal.",_=>"Payout arrives automatically."};
  if(card){SupportUiArt.Panel(c,s,20,90,340,116);SupportUiArt.Q(c,s,20,102,3,92,SupportUiArt.Gold);SupportUiArt.T(c,s,$"CAMP BASICS  {step} / 4",34,101,13,false,SupportUiArt.Muted);SupportUiArt.T(c,s,title,34,126,20,true);SupportUiArt.T(c,s,detail,34,155,16);for(int i=0;i<4;i++)SupportUiArt.Q(c,s,34+i*17,185,11,6,i<step?SupportUiArt.Gold:new(.3f,.32f,.32f,1));}
  if(TutorialPopup.Length>0&&tutorialPopupClock>0){SupportUiArt.Panel(c,s,20,h-272,340,58);var words=TutorialPopup.Split(' ',StringSplitOptions.RemoveEmptyEntries);string line="";int row=0;foreach(string word in words){if(line.Length+word.Length>38&&line.Length>0){SupportUiArt.T(c,s,line,32,h-261+row*21,14);line="";if(++row>=2)break;}line+=(line.Length>0?" ":"")+word;}if(row<2&&line.Length>0)SupportUiArt.T(c,s,line,32,h-261+row*21,14);}
  if(showWorld&&TutorialActive&&ShowTutorialRoute)DrawTutorialDestination(c,font);
  if(!showWorld||!TutorialActive||!ShowTutorialRoute||tutorialPath.Count<2||Player is not {} player)return;
  DrawTutorialRoute(c);
 }
}
