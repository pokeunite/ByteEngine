using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core.Gameplay;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningDefense3D
{
 [Category("Durability")] public float MaximumHealth{get;set;}=100;
 [Browsable(false)] public bool DestroyedByEnemy{get;private set;}
 [Browsable(false)] public bool CanBeAttacked=>!Spent&&!DestroyedByEnemy&&Kind!=MiningDefenseKind.SupplyCache&&GameObject.GetComponent<HealthComponent>() is {IsDead:false};
 void InitializeDurability(){if(Kind!=MiningDefenseKind.SupplyCache&&GameObject.GetComponent<HealthComponent>()==null)GameObject.AddComponent(new HealthComponent{MaxHealth=Math.Max(1,MaximumHealth),CurrentHealth=Math.Max(1,MaximumHealth),DestroyOnDeath=false});}
 bool ResolveDestruction()
 {
  if(DestroyedByEnemy)return true;
  if(Kind==MiningDefenseKind.SupplyCache||GameObject.GetComponent<HealthComponent>()?.IsDead!=true)return false;
  DestroyedByEnemy=true;Spent=true;NotifyRetired(true);
  var planner=Op?.GameObject.GetComponent<MiningDefensePlanner3D>();Op?.GameObject.GetComponent<MiningGameFeel3D>()?.NotifyDefenceDestroyed(Kind==MiningDefenseKind.Sentry?"Sentry":Kind.ToString(),planner?.ReplacementOreCost??20);
  MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.Alert);
  MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.Explosion,Transform.WorldPosition);
  var fx=MiningEffects3D.Find(GameObject);fx?.Emit(MiningEffectCue.MineExplosion,Transform.WorldPosition+Vector3.UnitY*.4f,size:fx.MineExplosionScale);
  GameObject.Scene?.DestroyGameObject(GameObject);return true;
 }
 [Category("Durability")]public bool ShowHealthBar{get;set;}=true;
 public override int? RenderOrder=>940;
 [Browsable(false)] public bool HealthBarVisible
 {
  get
  {
   if(!ShowHealthBar||Spent||Kind is MiningDefenseKind.Mine or MiningDefenseKind.SupplyCache||GameObject.GetComponent<HealthComponent>() is not {IsDead:false})return false;
   if(GameObject.Scene is not {} scene)return false;
   for(int i=0;i<scene.GameObjects.Count;i++)
   {
    var o=scene.GameObjects[i];if(!o.ActiveInHierarchy)continue;
    if(o.GetComponent<MiningBusinessUi3D>() is {Enabled:true,Screen:not MiningScreen.None}||o.GetComponent<MiningPauseMenu3D>() is {Enabled:true,IsOpen:true}||o.GetComponent<MiningResearchStation3D>() is {Enabled:true,IsOpen:true})return false;
   }
   return true;
  }
 }
 protected override void OnRender(ByteEngine.Core.Graphics.RenderContext c)
 {
  if(!HealthBarVisible||!c.Has3DCamera||GameObject.GetComponent<HealthComponent>() is not {} hp)return;
  var p=Vector4.Transform(new Vector4(Transform.WorldPosition+Vector3.UnitY*1.9f,1),c.GetViewMatrix3D()*c.GetProjectionMatrix3D());if(p.W<=0)return;
  float x=(p.X/p.W+1)*c.TargetWidth/2,y=(1-p.Y/p.W)*c.TargetHeight/2,scale=Math.Min(c.TargetWidth/1280f,c.TargetHeight/720f);if(x<0||x>c.TargetWidth||y<0||y>c.TargetHeight)return;
  c.DrawUiQuad(new(x-35*scale,y),new(70*scale,7*scale),new(.02f,.025f,.03f,.95f));
  c.DrawUiQuad(new(x-34*scale,y+scale),new(68*scale*Math.Clamp(hp.HealthPercent,0,1),5*scale),hp.HealthPercent<.3f?new(1,.3f,.12f,1):new(.9f,.75f,.23f,1));
 }
 public float EffectiveDamage=>BaseDamage+(Trade?.Stat(Kind switch{MiningDefenseKind.Mine=>"LandMineDamageFlat",MiningDefenseKind.Mortar=>"MortarStrikeDamageFlat",MiningDefenseKind.Repeater=>"MachineGunDamageFlat",_=>"AutogunDamageFlat"})??0);
 public float EffectiveBlastRadius=>BlastRadius+(Kind==MiningDefenseKind.Mine?Trade?.Stat("LandMineBlastRadius")??0:0);
 void NotifyRetired(bool destroyed){var scene=GameObject.Scene;if(scene==null)return;foreach(var p in scene.GameObjects.Select(o=>o.GetComponent<MiningDefensePlanner3D>()).OfType<MiningDefensePlanner3D>())p.Retire(this,destroyed);}
}
