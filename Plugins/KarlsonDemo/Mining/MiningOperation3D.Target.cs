using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningOperation3D
{
 [Category("Level target")] public bool TargetEnabled{get;set;}=true;
 [Category("Level target")] public int TargetCredits{get;set;}=30000;
 [Category("Level target")] public float TargetPlaySeconds{get;set;}=18000;
 [Category("Level target")] public float TargetDays{get;set;}=10;
 [Category("Level target")] public AssetReference TargetFont{get;set;}=AssetReference.Empty;
 [Category("Raider combat")] public float RaiderDefenceNoticeRadius{get;set;}=6;
 [Category("Raider combat")] public float RaiderMeleeReach{get;set;}=1.35f;
 [Browsable(false)] public double TargetElapsedSeconds{get;private set;}
 [Browsable(false)] public bool TargetStarted{get;private set;}
 [Browsable(false)] public bool TargetWon{get;private set;}
 [Browsable(false)] public bool TargetExpired{get;private set;}
 [Browsable(false)] public float TargetProgress=>Math.Clamp(CompanyAccountCredits/(float)Math.Max(1,TargetCredits),0,1);
 [Browsable(false)] public double DaysRemaining=>Math.Max(0,TargetDays*(1-TargetElapsedSeconds/Math.Max(1,TargetPlaySeconds)));
 float targetSaveClock;
 void TickTarget(float dt)
 {
  if(GameObject.Scene?.GetComponentsSnapshot<MiningAuxiliaryRefinery3D>().Any(r=>r.IsOpen)==true||!TargetEnabled||TargetWon||TargetExpired||!TargetStarted||GameObject.Scene?.SimulationTimeScale<=0)return;
  TargetElapsedSeconds=Math.Min(Math.Max(1,TargetPlaySeconds),TargetElapsedSeconds+Math.Max(0,dt));
  if(CompanyAccountCredits>=Math.Max(1,TargetCredits)){TargetWon=true;FinishTarget("CONTRACT COMPLETE — credit target reached.");}
  else if(TargetElapsedSeconds>=Math.Max(1,TargetPlaySeconds)){TargetExpired=true;FinishTarget("CONTRACT EXPIRED — ten-day deadline reached.");}
  else if((targetSaveClock+=dt)>=15){targetSaveClock=0;SaveProgress();}
 }
 void FinishTarget(string message){CleanupEnemies(true);Phase=MiningPhase.Failed;Message=message;RefineryQueueActive=false;SaveProgress();}
 public bool SpendRefinedOre(int amount){Initialize();if(amount<0||RefinedOre<amount||!CanUseIndustry)return false;RefinedOre-=amount;SaveProgress();return true;}
 MiningBusinessUi3D? targetUi;
 void DrawTarget(RenderContext c)
 {
  targetUi??=GameObject.Scene?.GameObjects.Select(o=>o.GetComponent<MiningBusinessUi3D>()).OfType<MiningBusinessUi3D>().FirstOrDefault();
  if(GameObject.Scene?.GetComponentsSnapshot<MiningAuxiliaryRefinery3D>().Any(r=>r.IsOpen)==true||!TargetEnabled||GameObject.GetComponent<MiningResearchStation3D>()?.IsOpen==true||Player?.GetComponent<MiningCampLayout3D>()?.IsEditing==true||targetUi is {Screen:not MiningScreen.None})return;
  if(Player?.GetComponent<MiningDefensePlanner3D>()?.BuildMode==true)return;
  if(Phase==MiningPhase.Drilling&&!TargetWon&&!TargetExpired)return;
  if(targetUi==null)return;
  float s=Math.Min(c.TargetWidth/1280f,c.TargetHeight/720f),x=c.TargetWidth/s-230;
  void Q(float px,float py,float w,float h,Vector4 color)=>c.DrawUiQuad(new(px*s,py*s),new(w*s,h*s),color);
  void T(string text,float px,float py,int size,Vector4 color)=>targetUi.FieldText(c,text,px,py,size,s,false,UiAnchor.TopLeft,color);
  var gold=new Vector4(1,.77f,.14f,1);var text=new Vector4(.94f,.94f,.86f,1);
  targetUi.FieldImage(c,"goal-border",x,20,210,66,s);
  targetUi.FieldImage(c,"company-goal",x+12,31,28,28,s);
  T("Company account",x+51,27,13,text);
  T($"{CompanyAccountCredits:N0} / {TargetCredits:N0} cr",x+51,44,15,gold);
  T($"{DaysRemaining:0.0} days left",x+51,64,11,text);
  Q(x+12,78,186,3,new(.2f,.23f,.24f,1));Q(x+12,78,186*TargetProgress,3,gold);
  if(TargetWon||TargetExpired){Q(c.TargetWidth/s/2-230,125,460,68,new(.035f,.045f,.045f,.96f));T(TargetWon?"CONTRACT COMPLETE":"CONTRACT EXPIRED",c.TargetWidth/s/2-210,137,26,gold);T(TargetWon?$"{TargetCredits:N0} credits secured. Level passed.":"Restart from the main menu to try again.",c.TargetWidth/s/2-210,170,17,text);}
 }
}
