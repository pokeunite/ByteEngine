using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Gameplay;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 bool overclockPressStarted;
 void TickOperationAssist(MiningOperation3D op)
 {
  bool near=NearestScreen==MiningScreen.Drill&&Screen==MiningScreen.None&&!CampEditing&&Input.IsGameInputCaptured;
  bool held=near&&Input.IsKeyDown(Key.E);
  if(op.OverclockAvailable&&near&&Input.IsKeyPressed(Key.E))overclockPressStarted=true;
  if(overclockPressStarted&&!held){overclockPressStarted=false;if(near&&op.OverclockAvailable){op.HoldOverclock(false,0);Open(MiningScreen.Drill);return;}}
  if(!op.OverclockAvailable)overclockPressStarted=false;
  op.HoldOverclock(held,(float)Time.DeltaTime);
 }
 void DrawOperationAssist(RenderContext c,MiningOperation3D op)
 {
  float scale=Math.Min(c.TargetWidth/1280f,c.TargetHeight/720f),width=c.TargetWidth/scale,height=c.TargetHeight/scale;
  if(op.OverclockAvailable&&NearestScreen==MiningScreen.Drill){
   bool near=NearestScreen==MiningScreen.Drill;float x=width/2-180,y=near?height-65:94;
   c.DrawUiQuad(new(x*scale,y*scale),new(360*scale,(near?46:28)*scale),Surface);
   string label=near?$"Hold [{MiningPauseMenu3D.KeyLabel(Key.E)}] OVERCLOCK  +{op.OverclockBonusOre} ORE":$"OVERCLOCK AT DRILL  +{op.OverclockBonusOre} ORE  /  {op.OverclockSecondsLeft:0}s";
   c.DrawUiText(label,BodyFont,(int)(16*scale),new((x+12)*scale,(y+6)*scale),Accent,UiAnchor.TopLeft);
   if(near){c.DrawUiText("Tap to choose next operation",BodyFont,(int)(13*scale),new((x+12)*scale,(y+26)*scale),MenuMuted,UiAnchor.TopLeft);c.DrawUiQuad(new(x*scale,(y+44)*scale),new(360*scale*op.OverclockHoldProgress,2*scale),Accent);}
  }
  if(op.Phase!=MiningPhase.Drilling||op.FinalEnemyMarkerSeconds<op.FinalEnemyMarkerDelay)return;
  var vp=c.GetViewMatrix3D()*c.GetProjectionMatrix3D();
  foreach(var enemy in op.OperationEnemies){
   if(enemy.Scene!=GameObject.Scene||enemy.GetComponent<HealthComponent>() is not {IsDead:false})continue;
   var position=enemy.Transform.WorldPosition;if(!float.IsFinite(position.X)||!float.IsFinite(position.Y)||!float.IsFinite(position.Z))continue;
   var clip=Vector4.Transform(new Vector4(position+Vector3.UnitY*Math.Clamp(op.FinalEnemyMarkerHeight,.5f,5),1),vp);
   float sx,sy;
   if(clip.W>.01f){sx=(clip.X/clip.W*.5f+.5f)*width;sy=(.5f-clip.Y/clip.W*.5f)*height;}
   else{sx=width/2+MathF.Sign(clip.X)*width;sy=height-140;}
   // Stay above the enemy when visible. At the edge, retain only the tiny icon.
   sx=Math.Clamp(sx,24,width-24);sy=Math.Clamp(sy,120,height-110);
   float r=Math.Clamp(op.FinalEnemyMarkerSize,10,28)*.5f;
   void Line(float ax,float ay,float bx,float by,float thickness=1.5f)=>c.DrawUiLine(new((sx+ax)*scale,(sy+ay)*scale),new((sx+bx)*scale,(sy+by)*scale),thickness*scale,Accent);
   Line(0,-r,r,0);Line(r,0,0,r);Line(0,r,-r,0);Line(-r,0,0,-r);
   Line(0,-r*.42f,0,r*.10f,2);Line(0,r*.40f,0,r*.49f,2);
  }
 }
}
