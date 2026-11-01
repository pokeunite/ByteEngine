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
 readonly Material tutorialLineMaterial=new(){BaseColor=new(1,.76f,.12f,1),EmissionEnabled=true,EmissionColor=new(1,.64f,.05f),EmissionIntensity=.7f};
 MiningBusinessUi3D? TutorialUi=>GameObject.Scene?.GameObjects.SelectMany(o=>o.Components.OfType<MiningBusinessUi3D>()).FirstOrDefault();
 public string TutorialInstruction=>TutorialStep switch{
 CampTutorialStep.AmmoPress=>"Collect ammo from the ammo press bucket",
 CampTutorialStep.StartDrill=>"Approach the drill and start your first operation",
 CampTutorialStep.WinRound=>"Defend the drill / eliminate the incoming spiders",
 CampTutorialStep.Refinery=>"Take your raw ore to the refinery",
 CampTutorialStep.WaitForRefining=>"The refinery is turning raw ore into refined ore",
 CampTutorialStep.CompanyHub=>"Use the company tent: MARKET / sell refined ore",
 CampTutorialStep.WaitForSale=>"Your ore is shipping / payout arrives automatically",_=>"Camp basics complete"};
 public void SkipCampTutorial(){TutorialStep=CampTutorialStep.Complete;tutorialPath=[];SaveProgress();}
 void TutorialAdvance(CampTutorialStep next,string popup="")
 {TutorialStep=next;tutorialPath=[];tutorialPathClock=0;TutorialPopup=popup;tutorialPopupClock=5;SaveProgress();}
 public void TickCampTutorial(float delta)
 {
  if(!float.IsFinite(delta)||delta<=0||GameObject.Scene is not {} scene)return;
  tutorialPopupClock-=delta;if(tutorialPopupClock<=0)TutorialPopup="";
  if(!TutorialActive)return;
  var ui=TutorialUi;var bucket=scene.FindGameObject(ui?.AmmoMachineObjectId??Guid.Empty)?.GetComponent<MiningAmmoBucket3D>();
  if(TutorialStep==CampTutorialStep.AmmoPress)
  {
   if((bucket?.CollectionSequence??0)<=tutorialCollection&&Player?.GetComponent<FpsGunLab3D>()?.SelectedWeapon is {} weapon&&(!tutorialAmmoPrepared||weapon.Definition is {} definition&&weapon.ReserveAmmo>=definition.ReserveCapacity)){weapon.SetAmmo(weapon.CurrentAmmo,Math.Min(weapon.ReserveAmmo,Math.Max(0,TutorialStartingReserve)));tutorialAmmoPrepared=true;SaveProgress();}
   if(bucket!=null&&bucket.CollectionSequence>tutorialCollection)TutorialAdvance(CampTutorialStep.StartDrill,"AMMO COLLECTED / the press makes reserve ammo automatically.");
   // If the player starts a round first, don't strand them in an irrelevant step.
   else if(Phase==MiningPhase.Drilling)TutorialAdvance(CampTutorialStep.WinRound);
  }
  if(TutorialStep==CampTutorialStep.StartDrill&&Phase==MiningPhase.Drilling)TutorialAdvance(CampTutorialStep.WinRound);
  if(TutorialStep==CampTutorialStep.WinRound&&CompletedRounds>0&&Phase==MiningPhase.Preparation)TutorialAdvance(CampTutorialStep.Refinery,"OPERATION COMPLETE / raw ore is ready to refine.");
  if(TutorialStep==CampTutorialStep.WinRound&&CompletedRounds==0&&Phase==MiningPhase.Preparation)TutorialAdvance(CampTutorialStep.StartDrill,"Try again / return to the drill when ready.");
  if(TutorialStep==CampTutorialStep.Refinery){if(RefiningAmount>0)TutorialAdvance(CampTutorialStep.WaitForRefining);else if(RefinedOre>0)TutorialAdvance(CampTutorialStep.CompanyHub);}
  if(TutorialStep==CampTutorialStep.WaitForRefining&&RefinedOre>0)TutorialAdvance(CampTutorialStep.CompanyHub,"REFINED ORE READY / sell it at the company tent.");
  if(TutorialStep==CampTutorialStep.CompanyHub&&PendingMarketSale is {Refined:true})TutorialAdvance(CampTutorialStep.WaitForSale);
  if(TutorialStep is CampTutorialStep.CompanyHub or CampTutorialStep.WaitForSale&&PendingMarketSale==null&&LastMarketSale is {Refined:true})TutorialAdvance(CampTutorialStep.Complete,"FIRST SALE COMPLETE / reinvest your credits to grow the camp.");
  tutorialPathClock-=delta;
  if(tutorialPathClock<=0){tutorialPathClock=1;UpdateTutorialPath(ui);}
 }
 GameObject? TutorialDestination(MiningBusinessUi3D? ui)=>GameObject.Scene?.FindGameObject(TutorialStep switch{CampTutorialStep.AmmoPress=>ui?.AmmoMachineObjectId??Guid.Empty,CampTutorialStep.StartDrill=>DrillObjectId,CampTutorialStep.Refinery=>ui?.RefineryMachineObjectId??Guid.Empty,CampTutorialStep.CompanyHub=>ui?.TerminalObjectId??Guid.Empty,_=>Guid.Empty});
 void UpdateTutorialPath(MiningBusinessUi3D? ui)
 {
  tutorialPath=[];if(Player is not {} player||TutorialDestination(ui) is not {} target)return;
  Vector3 start=player.Transform.WorldPosition;
  var bounds=MiningCampGeometry.Transform(MiningCampGeometry.LocalBounds(target),target.Transform.WorldMatrix);
  var centre=(bounds.Minimum+bounds.Maximum)/2;centre.Y=start.Y;
  // End outside the machine footprint, on the nearest reachable side.
  var candidates=new[]{new Vector3(bounds.Minimum.X-1.4f,start.Y,Math.Clamp(start.Z,bounds.Minimum.Z,bounds.Maximum.Z)),new Vector3(bounds.Maximum.X+1.4f,start.Y,Math.Clamp(start.Z,bounds.Minimum.Z,bounds.Maximum.Z)),new Vector3(Math.Clamp(start.X,bounds.Minimum.X,bounds.Maximum.X),start.Y,bounds.Minimum.Z-1.4f),new Vector3(Math.Clamp(start.X,bounds.Minimum.X,bounds.Maximum.X),start.Y,bounds.Maximum.Z+1.4f)};
  var region=GameObject.Scene!.GameObjects.SelectMany(o=>o.Components.OfType<NavigationRegion3D>()).FirstOrDefault(r=>r.Enabled&&r.Ready);
  foreach(var end in candidates.OrderBy(p=>Vector3.DistanceSquared(p,start))){var path=region?.FindPath(start,end,4000);if(path is {Count:>1}){tutorialPath=path;return;}}
  // No baked navigation: a short direction marker, never a fake route through walls.
  var dir=centre-start;if(dir.LengthSquared()>1)tutorialPath=[start,start+Vector3.Normalize(dir)*Math.Min(2,dir.Length())];
 }
 public void DrawCampTutorial(RenderContext c,AssetReference font,bool showWorld)
 {
  if(!TutorialEnabled||!TutorialActive&&tutorialPopupClock<=0)return;
  float s=Math.Min(c.TargetWidth/1280f,c.TargetHeight/720f),w=c.TargetWidth/s;
  void Text(string t,float y,int size,Vector4 color)=>c.DrawUiText(t,font,(int)(size*s),new(w*.5f*s,y*s),color,UiAnchor.TopCenter);
  if(TutorialActive){c.DrawUiQuad(new((w/2-300)*s,128*s),new(600*s,54*s),new(.035f,.045f,.055f,.94f));Text("CAMP BASICS",133,13,new(1,.76f,.12f,1));Text(TutorialInstruction,153,18,new(.95f,.95f,.9f,1));}
  if(TutorialPopup.Length>0&&tutorialPopupClock>0){c.DrawUiQuad(new((w/2-350)*s,192*s),new(700*s,36*s),new(.035f,.045f,.055f,.96f));Text(TutorialPopup,201,16,new(1,.83f,.35f,1));}
  if(!showWorld||!TutorialActive||!ShowTutorialRoute||tutorialPath.Count<2||Player is not {} player)return;
  float budget=24;Vector3 a=player.Transform.WorldPosition;
  foreach(var b0 in tutorialPath.Skip(1))
  {
   var b=b0;float distance=Vector3.Distance(a,b);if(distance<.01f){a=b;continue;}var dir=(b-a)/distance;
   for(float d=0;d<distance&&budget>0;d+=.9f,budget-=.9f){float length=Math.Min(.5f,distance-d);var mid=a+dir*(d+length/2)+Vector3.UnitY*.16f;float yaw=MathF.Atan2(dir.X,dir.Z);c.RenderWorld.Submit(c.Renderer3D.GetPrimitive(PrimitiveMeshType.Cube),tutorialLineMaterial,Matrix4x4.CreateScale(.12f,.025f,length)*Matrix4x4.CreateRotationY(yaw)*Matrix4x4.CreateTranslation(mid),RenderQueue3D.Opaque,true,false,false);}
   if(budget<=0)break;a=b;
  }
 }
}

