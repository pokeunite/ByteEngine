using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 public bool AlwaysShowEconomyHud{get;set;}
 public bool AlwaysShowWeaponInventory{get;set;}
 public bool AlwaysShowDrillHealth{get;set;}
 public float WeaponInventoryRevealSeconds{get;set;}=4;
 public bool CombatHud=>Op?.Phase==MiningPhase.Drilling;
 public bool EconomyHudVisible=>AlwaysShowEconomyHud||!CombatHud;
 public bool DrillHudVisible=>AlwaysShowDrillHealth||CombatHud||Op?.DrillHealthFraction<.999f;
 public bool InventoryHudVisible=>AlwaysShowWeaponInventory||weaponRevealClock>0||weaponUnlockClock>0;
 void RenderContextHud(RenderContext c,float width,float height,float scale)
 {
  if(Op is not {} op)return;if(DefencePlanner is {BuildMode:true} building){DrawSupportBuildTray(c,width,height,scale,building);return;}var player=GameObject.Scene!.FindGameObject(op.PlayerObjectId);var health=player?.GetComponent<HealthComponent>();var gun=player?.GetComponent<FpsGunLab3D>();
  void Image(string name,float x,float y,float w,float h)=>FieldImage(c,name,x,y,w,h,scale);
  void T(string text,float x,float y,int size=17,bool bold=false,UiAnchor anchor=UiAnchor.TopLeft,Vector4? color=null)=>FieldText(c,text,x,y,size,scale,bold,anchor,color);
  void Line(float x,float y,float xx,float yy)=>c.DrawUiLine(new(x*scale,y*scale),new(xx*scale,yy*scale),scale,new(.76f,.74f,.65f,.8f));
  void Bar(float x,float y,float w,float fraction){c.DrawUiQuad(new(x*scale,y*scale),new(w*scale,6*scale),new(.23f,.23f,.21f,1));c.DrawUiQuad(new(x*scale,y*scale),new(w*Math.Clamp(fraction,0,1)*scale,6*scale),new(.95f,.76f,.18f,1));}
  if(EconomyHudVisible){string credits=op.Credits.ToString("N0");float creditRight=132;foreach(char digit in credits)creditRight+=char.IsDigit(digit)?11:5;float dx=creditRight-170;Image("resource-strip-border",20,20,560+dx,52);Image("credits",30,31,26,26);T("Credits",63,36,13);T(credits,120,34,18,true);Line(creditRight,30,creditRight,62);Image("raw-ore",182+dx,31,26,26);T("Raw",218+dx,25,13);T(op.RawOre.ToString("N0"),218+dx,43,18,true);Line(290+dx,30,290+dx,62);Image("refined-ore",302+dx,31,26,26);T("Refined",338+dx,25,13);T(op.RefinedOre.ToString("N0"),338+dx,43,18,true);Line(415+dx,30,415+dx,62);Image("cargo",427+dx,31,26,26);T("Cargo",463+dx,25,13);T($"{op.CargoUsed:N0} / {op.CargoCapacity:N0}",463+dx,43,17,true,color:op.CargoFull?new(1,.5f,.25f,1):null);}
  else if(PayoutReceiptVisible&&op.LastMarketSale is {} paid){Image("resource-strip-border",20,20,330,58);T($"Shipment +{paid.CampPayout:N0} camp / +{paid.CompanyDeposit:N0} company",32,27,14);T($"Camp funds: {op.Credits:N0} cr",32,47,17,true);}
  else if(op.CargoFull)FieldHint(c,"CARGO FULL",20,20,160,scale);
  float vx=20,vy=height-104;Image("vitals-border",vx,vy,350,84);Image("health",vx+12,vy+10,24,24);T("HP",vx+46,vy+7,17,true);T($"{health?.CurrentHealth??0:0} / {health?.MaxHealth??100:0}",vx+210,vy+7,17,true,UiAnchor.TopRight);Bar(vx+46,vy+32,164,health?.HealthPercent??0);
  Image("drill",vx+12,vy+47,24,24);T("Drill",vx+46,vy+44,17,true);T($"{op.DrillHealthFraction:P0}",vx+210,vy+44,17,true,UiAnchor.TopRight);Bar(vx+46,vy+69,164,op.DrillHealthFraction);Line(vx+225,vy+45,vx+225,vy+72);
  string state=op.TargetWon?"Target reached":op.TargetExpired?"Deadline missed":op.Phase==MiningPhase.Failed?"Failed":CombatHud?$"{op.AliveEnemies} active":op.OverclockAvailable?$"Overclock +{op.OverclockBonusOre}":op.ExpeditionActive?$"Next {WaveCountdownText}":"Ready";
  c.DrawUiQuad(new((vx+236)*scale,(vy+52)*scale),new(7*scale),CombatHud?new(1,.65f,.12f,1):new(.27f,.8f,.38f,1));T(op.ExpeditionActive?$"Wave {Math.Min(op.WavesPerExpedition,op.ExpeditionWave+(op.Phase==MiningPhase.Preparation?1:0))}/{op.WavesPerExpedition}":$"Operation {op.CompletedRounds+1:00}",vx+253,vy+41,13);T(state,vx+253,vy+59,16,true);
  if(CombatHud)T($"{Math.Max(0,op.EnemiesScheduled-op.EnemiesSpawned)} incoming",vx+253,vy+14,14,color:MenuMuted);
  var weapon=gun?.SelectedWeapon;float ax=width-250,ay=height-80;Image("ammunition-border",ax,ay,230,60);
  if(weapon!=null){var icon=weapon.Definition?.HudIcon;if(icon is {IsEmpty:false}&&ByteEngine.Core.Animation.AnimationRuntimeAssets.TryGet(out var assets)&&assets!=null)c.DrawUiImage(assets.LoadTexture(icon),new((ax+14)*scale,(ay+15)*scale),new(53*scale,30*scale),Vector4.One);else Image("pistol",ax+14,ay+15,40,34);Line(ax+82,ay+13,ax+82,ay+47);T($"{weapon.CurrentAmmo} / {weapon.ReserveAmmo}",ax+101,ay+14,25,true);}else T("Unarmed",ax+60,ay+18,20,true);
  if(DefencePlanner?.BuildMode!=true)FieldHint(c,$"[{MiningPauseMenu3D.KeyLabel(Key.Tab)}] Arrange camp",width-214,height-156,194,scale);
  DrawOwnedWeaponHud(c,width,height,scale);DrawDefenceHud(c,width,height,scale);
  if(Feedback.Length>0)FieldHint(c,Feedback,width/2-230,height-150,460,scale);
  string prompt=op.Phase==MiningPhase.Failed?$"[{MiningPauseMenu3D.KeyLabel(Key.E)}] Recover site":DefencePlanner?.BuildMode==true||op.OverclockAvailable&&NearestScreen==MiningScreen.Drill?"":NearestScreen switch{MiningScreen.AmmoPress=>"Ammo press",MiningScreen.Refinery=>"Refinery",MiningScreen.Drill=>CombatHud?"":"Drill",MiningScreen.Company=>"Company terminal",_=>""};
  if(prompt.Length>0){if(op.Phase!=MiningPhase.Failed)prompt=$"[{MiningPauseMenu3D.KeyLabel(Key.E)}] {prompt}";FieldHint(c,prompt,width/2-120,height-55,240,scale);}
 }
}
