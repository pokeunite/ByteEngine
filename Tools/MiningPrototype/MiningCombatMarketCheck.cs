using System.Numerics;
using System.Reflection;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using KarlsonDemo.Plugin;
internal static class MiningCombatMarketCheck
{
 public static void Run(Scene scene,MiningOperation3D op,Action<bool,string> check)
 {
  void Set(string key,object value)=>typeof(MiningOperation3D).GetProperty(key)!.SetValue(op,value);
  var trade=op.GameObject.GetComponent<MiningTrade3D>()!;int credits=op.Credits,raw=op.RawOre,refined=op.RefinedOre;
  Set("Credits",1000);Set("RawOre",0);Set("RefinedOre",0);int cost=(int)Math.Ceiling(trade.BuyQuote(true)*5d-.00001);
  check(trade.BuyOre(true,5)&&op.RefinedOre==5&&op.Credits==1000-cost,"buying ore charges ask including fee and adds actual cargo");
  check(trade.BuyQuote(true)>trade.LiveQuote(true),"buy/sell fee spread prevents immediate resale arbitrage");
  int before=op.Credits;check(!trade.BuyOre(true,int.MaxValue)&&op.Credits==before&&op.RefinedOre==5,"oversized purchase is rejected without partial changes");
  Set("RawOre",op.CargoCapacity);check(trade.MaximumBuyQuantity(false)==0&&!trade.BuyOre(false,1),"ore purchases respect the shared cargo limit");
  check(!op.PurchaseOre(false,1,float.NaN),"invalid ore purchase quote is rejected");
  Set("Credits",credits);Set("RawOre",raw);Set("RefinedOre",refined);
  check(trade.ResearchCost("pellet")==180,"Scatter Specialist costs exactly 180 credits after the map multiplier");
  var definitions=scene.GameObjects.SelectMany(o=>o.Components.OfType<WeaponDefinition3D>()).ToArray();var shotgun=definitions.Single(w=>w.RequiredResearchId=="shotgun");var rifle=definitions.Single(w=>w.RequiredResearchId=="ar");
  check(shotgun.DamagePerPellet*shotgun.PelletsPerShot>=70&&shotgun.HitImpulse>=10&&shotgun.EnemyKnockbackSeconds>=.2f,"shotgun combines meaningful damage with crowd-clearing knockback");
  check(rifle.DamagePerPellet>=23&&rifle.RecoilPitchPerShot<=.2f&&rifle.MaximumRecoilDegrees<=2,"AR gains damage and has controlled recoil");
  check(op.RaiderMoveSpeed<op.HunterMoveSpeed,"rig raiders are slower than player hunters");
  var enemy=scene.CreateGameObject("knockback fixture");enemy.Transform.WorldPosition=new(600,0,0);var motor=enemy.AddComponent(new CharacterController3D());enemy.AddComponent(new SimpleEnemyAI3D{Enabled=false});var owner=scene.CreateGameObject("knockback weapon");var weapon=owner.AddComponent(new WeaponInstance3D());
  typeof(WeaponInstance3D).GetMethod("ApplyEnemyKnockback",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(weapon,[new CombatDamageReceipt(enemy.Id,enemy.Transform.WorldPosition,9,false,true),-Vector3.UnitZ,shotgun]);
  check(motor.HasBurstMotion,"shotgun hit starts collision-swept physical displacement on the enemy");scene.DestroyGameObject(enemy);scene.DestroyGameObject(owner);
 }
}
