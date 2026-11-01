using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.AI;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningCleanupCheck
{
 public static void Preview(Scene scene,MiningOperation3D op)
 {
  var player=scene.FindGameObject(op.PlayerObjectId)!;var planner=player.GetComponent<MiningDefensePlanner3D>()!;
  for(int i=0;i<4;i++)typeof(MiningDefensePlanner3D).GetMethod("Spawn",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(planner,[(MiningDefenseKind)i,new Vector3(-10+i*7,0,-7),0f]);
  for(int i=0;i<4;i++){typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[.05d]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(scene,null);}
  var camp=player.GetComponent<MiningCampLayout3D>()!;camp.EnterEdit();camp.BeginMove(scene.GameObjects.First(o=>o.GetComponent<MiningDefense3D>()?.Kind==MiningDefenseKind.Sentry).Id);
 }
 public static void Run(Scene scene,MiningOperation3D op)
 {
  var checks=new List<string>();void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);Console.WriteLine("PASS CLEANUP "+message);}
  var hash=SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene"));
  foreach(var component in scene.GameObjects.SelectMany(o=>o.Components)){component.GetType().GetProperty("PersistProgress")?.SetValue(component,false);component.GetType().GetProperty("PersistSettings")?.SetValue(component,false);}
  var host=new SceneManager();host.LoadScene(scene);
  void Frame(int count=1){for(int i=0;i<count;i++){typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[.05d]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(scene,null);}}
  void Set(object o,string property,object value)=>o.GetType().GetProperty(property)!.SetValue(o,value);
  Frame(3);var player=scene.FindGameObject(op.PlayerObjectId)!;var planner=player.GetComponent<MiningDefensePlanner3D>()!;
  var trade=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningTrade3D>()).Single();
  var playerHealth=player.GetComponent<HealthComponent>()!;
  MiningGameFeelCheck.Run(scene,op,Check);
  Check(trade.Research.Count==22&&trade.Research.All(n=>n.Id is not ("batch" or "yield" or "conveyor" or "medkit" or "plating" or "feed")),"reviewed map removes obsolete company and machine-stat nodes");
  Check(trade.HasResearch("charter")&&!trade.CanBuyResearch("shotgun"),"free charter and operation gates work before weapon unlocks");
  var loadout=player.GetComponent<FpsGunLab3D>()!;
  Check(loadout.CarriedWeapons.All(w=>w.Definition?.Slot==0),"new game starts with the pistol and no unpurchased rifle or shotgun");
  op.RefundResearchCredits(10000);Set(op,"CompletedRounds",8);
  Check(trade.BuyResearch("shotgun")&&trade.BuyResearch("pellet"),"shotgun licence and multi-target scatter research can be purchased in dependency order");
  trade.Tick(.05f);
  Check(loadout.CarriedWeapons.Any(w=>w.Definition?.Slot==2)&&loadout.CarriedWeapons.First(w=>w.Definition?.Slot==2).Definition!.HomingTargetCount==3,"shotgun purchase grants a carried weapon and enables three-target homing");
  var weaponUi=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningBusinessUi3D>()).Single();typeof(MiningBusinessUi3D).GetMethod("TickWeaponHud",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(weaponUi,[.05f]);var unlockHint=(string)typeof(MiningBusinessUi3D).GetField("weaponUnlockNotice",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(weaponUi)!;Check(weaponUi.OwnedWeaponHudCount==2&&unlockHint.Contains("Shotgun")&&unlockHint.EndsWith("2"),"shotgun unlock appears in the owned-weapon HUD with its actual second inventory key");
  Check(trade.BuyResearch("mine")&&trade.BuyResearch("ar"),"mine and rifle unlocks obey their reviewed prerequisites");trade.Tick(.05f);
  Check(loadout.CarriedWeapons.Any(w=>w.Definition?.Slot==1)&&trade.Research.First(n=>n.Id=="ar").Cost==400,"rifle licence grants the AR at the submitted price");
  Check(trade.CanBuyResearch("freight")&&!trade.CanBuyResearch("broker"),"approved market research is available and broker still requires its prerequisite chain");
  var beforeQuote=op.PreviewContract(0)!.Ore;Check(trade.BuyResearch("survey")&&op.PreviewContract(0)!.Ore==beforeQuote+7,"drill research increases forecast and awarded extraction by seven ore");
  Set(op,"CompletedRounds",20);Check(trade.BuyResearch("pierce")&&trade.BuyResearch("precision"),"rifle specialisations unlock without requiring shotgun research");
  var rifle=loadout.CarriedWeapons.First(w=>w.Definition?.Slot==1).Definition!;
  Check(rifle.PenetrationTargets==1&&Math.Abs(rifle.BurstAccuracyBonus-.3f)<.001f,"rifle research enables one-target penetration and controlled-burst accuracy");
  var hpBefore=playerHealth.MaxHealth;Check(trade.BuyResearch("vest")&&playerHealth.MaxHealth==hpBefore+15,"worksite armour increases maximum HP by the reviewed amount");
  Check(trade.BuyResearch("boots")&&trade.BuyResearch("reclaim")&&trade.BuyResearch("terminal"),"survival chain bypasses removed medkit and plating nodes");
  Check(Math.Abs(trade.CargoSalvageFraction-.25f)<.001f,"salvage research retains one quarter of unrefined cargo");
  var rig=scene.FindGameObject(op.DrillObjectId)!.GetComponent<HealthComponent>()!;rig.CurrentHealth=0;trade.ResetReviewedOperation();
  Check(trade.TryEmergencyDrillRepair()&&Math.Abs(rig.CurrentHealth-rig.MaxHealth*.25f)<.01f,"late emergency repair revives the drill at 25 percent HP");rig.CurrentHealth=0;
  Check(!trade.TryEmergencyDrillRepair(),"emergency repair cannot repeat in the same operation");trade.ResetReviewedOperation();Check(trade.TryEmergencyDrillRepair(),"a new operation rearms the one-use emergency repair");rig.Revive();
  MiningLiveMarketCheck.Run(scene,op,trade,Check);MiningCampTutorialCheck.Run(scene,op,trade,Check);MiningWeaponCampUxCheck.Run(scene,op,Check);
  Set(op,"RawOre",40);Set(op,"RefiningAmount",10);Set(op,"RefiningRemaining",2f);typeof(MiningOperation3D).GetMethod("Fail",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(op,["Test defeat"]);
  Check(op.RawOre==10&&op.RefiningAmount==0&&op.RefiningRemaining==0,"defeat salvages 25 percent of raw ore and cancels the pending refinery batch");
  op.TickRecovery(2);op.TickRecovery(2);op.TickRecovery(2);Check(!op.Recovering&&op.Phase==MiningPhase.Preparation,"salvage defeat recovery returns safely to preparation");Set(op,"RawOre",0);
  Set(op,"CompletedRounds",0);
  Check(op.StartOperation(),"operation starts for end-of-round recovery check");playerHealth.CurrentHealth=25;Set(op,"ExtractionProgress",1f);Set(op,"EnemiesScheduled",0);Set(op,"EnemiesSpawned",0);op.Tick(.05f);
  Check(op.Phase==MiningPhase.Preparation&&playerHealth.CurrentHealth==playerHealth.MaxHealth,"successful round restores full player HP immediately without a medkit");
  op.RestoreHealthAfterOperation=false;Check(op.StartOperation(),"recovery remains inspector-configurable");playerHealth.CurrentHealth=25;Set(op,"ExtractionProgress",1f);Set(op,"EnemiesScheduled",0);Set(op,"EnemiesSpawned",0);op.Tick(.05f);Check(playerHealth.CurrentHealth==25,"disabled round recovery preserves remaining HP");op.RestoreHealthAfterOperation=true;playerHealth.Revive();
  MiningDefense3D Spawn(MiningDefenseKind kind,Vector3 p){typeof(MiningDefensePlanner3D).GetMethod("Spawn",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(planner,[kind,p,0f]);Frame();return scene.GameObjects.Last(o=>o.GetComponent<MiningDefense3D>()?.Kind==kind).GetComponent<MiningDefense3D>()!;}
  var sentry=Spawn(MiningDefenseKind.Sentry,new(0,0,-12));var sentryRoot=sentry.GameObject;
  Check(sentry.CanBeAttacked&&sentryRoot.GetComponent<HealthComponent>()!.CurrentHealth==100,"deployed defence has real initialized health");
  sentryRoot.GetComponent<HealthComponent>()!.Damage(200);Frame();
  Check(op.GameObject.GetComponent<MiningGameFeel3D>()?.DefenceLossMessage.Contains("DESTROYED")==true,"destroyed defence produces an explicit HUD warning");
  Check(sentryRoot.Scene==null&&planner.ReplacementCost(MiningDefenseKind.Sentry)==20,"enemy-destroyed defence is removed and requires 20 ore");
  var licence=trade.Research.First(n=>n.Stat=="AutogunGrant");var ranks=(Dictionary<string,int>)typeof(MiningTrade3D).GetField("_ranks",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(trade)!;ranks[licence.Id]=1;
  player.Transform.WorldPosition=new(12,0,-12);Check(planner.SelectBuild(MiningDefenseKind.Sentry),"replacement can be selected using the existing placement flow");
  Set(op,"RefinedOre",19);Check(!planner.TryPlace(new(16,0,-12))&&op.RefinedOre==19,"replacement placement cannot overspend ore");Set(op,"RefinedOre",30);Check(planner.TryPlace(new(16,0,-12))&&op.RefinedOre==10&&planner.ReplacementCost(MiningDefenseKind.Sentry)==0,"replacement placement spends exactly 20 ore and consumes one destroyed entitlement");
  var mine=Spawn(MiningDefenseKind.Mine,new(5,0,-12));var oldMine=mine.GameObject;
  typeof(MiningDefense3D).GetMethod("DetonateVisual",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(mine,null);Frame(35);
  Check(oldMine.Scene==null&&planner.ReplacementCost(MiningDefenseKind.Mine)==0,"normal mine detonation is distinct from enemy destruction");
  planner.RearmMines();Frame();Check(scene.GameObjects.Any(o=>o.GetComponent<MiningDefense3D>() is {Kind:MiningDefenseKind.Mine,Spent:false}&&Vector3.Distance(o.Transform.WorldPosition,new(5,0,-12))<.01f),"spent mine re-arms at the saved position");
  var turret=Spawn(MiningDefenseKind.Repeater,new(10,0,-12));var raider=scene.CreateGameObject("Cleanup priority fixture");raider.Transform.WorldPosition=new(12,0,-12);
  var brain=raider.AddComponent(new AIBehavior3D{Enabled=false,FindNearestCharacter=false});var combat=raider.AddComponent(new SimpleEnemyAI3D{Enabled=false,AttackRange=3,AttackStyle=EnemyAttackStyle.Rusher});var priority=raider.AddComponent(new MiningRaiderPriorities3D{DrillObjectId=op.DrillObjectId,ReconsiderSeconds=.05f});Frame(2);
  Check(brain.TargetObjectId==turret.GameObject.Id&&combat.TargetId==turret.GameObject.Id,"nearby drill raider prioritises a living defence");
  raider.Transform.WorldPosition=new(90,0,0);Frame(2);var contact=scene.FindGameObject(brain.TargetObjectId)!;
  Check(contact.Name=="Raider contact point on drill"&&brain.TargetObjectId!=op.DrillObjectId,"distant raider uses a physical drill contact target");
  Check(brain.AttackReservationTargetId==op.DrillObjectId,"different drill contacts share the logical drill attack budget");
  var first=scene.CreateGameObject("Attack budget fixture A").AddComponent(new AIBehavior3D{Squad="CleanupBudget",MaximumAttackers=1,AttackReservationTargetId=op.DrillObjectId});var second=scene.CreateGameObject("Attack budget fixture B").AddComponent(new AIBehavior3D{Squad="CleanupBudget",MaximumAttackers=1,AttackReservationTargetId=op.DrillObjectId});
  var world=AIWorld.For(scene);Check(world.ReserveAttack(first,Guid.NewGuid(),10)&&!world.ReserveAttack(second,Guid.NewGuid(),10),"separate physical contacts cannot bypass the shared attacker limit");second.AttackReservationTargetId=Guid.Empty;Check(world.ReserveAttack(second,Guid.NewGuid(),10),"different actual defence targets retain independent attack slots");world.ReleaseAttack(first);world.ReleaseAttack(second);scene.DestroyGameObject(first.GameObject);scene.DestroyGameObject(second.GameObject);
  var drill=scene.FindGameObject(op.DrillObjectId)!;var drillHealth=drill.GetComponent<HealthComponent>()!;float before=drillHealth.CurrentHealth;
  contact.GetComponent<HealthComponent>()!.Damage(7);Check(drillHealth.CurrentHealth==before,"damage is rejected outside actual drill-surface melee reach");
  raider.Transform.WorldPosition=contact.Transform.WorldPosition+Vector3.UnitX*.15f;contact.GetComponent<HealthComponent>()!.Damage(7);Check(drillHealth.CurrentHealth==before-7,"damage at the physical drill surface reaches drill health");
  var live=RuntimeSpawnService.SpawnBlueprint(scene,op.RaiderBlueprint,contact.Transform.WorldPosition+Vector3.UnitX*1.1f)!;op.ConfigureEnemy(live,false);live.GetComponent<MiningRaiderPriorities3D>()!.DefenceNoticeRadius=0;
  before=drillHealth.CurrentHealth;Frame(80);Check(drillHealth.CurrentHealth<before,"actual raider blueprint completes its behaviour-tree strike against the drill surface");scene.DestroyGameObject(live);
  var bucket=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningAmmoBucket3D>()).Single();Check(bucket.PickupRadius==4.5f,"authored ammo magnet radius is 4.5 metres");
  var gun=player.GetComponent<FpsGunLab3D>()!;gun.SelectedWeapon!.SetAmmo(5,0);Set(trade,"StoredAmmo",20f);
  var renderer=bucket.GameObject.GetComponent<ByteEngine.Core.Graphics.ThreeD.SkeletalMeshRenderer>()!;renderer.TryGetBoneWorldMatrix(bucket.BucketMarker,out var marker);player.Transform.WorldPosition=marker.Translation+new Vector3(3.5f,0,0);Frame(2);
  Check(bucket.LastCollected>0&&bucket.CollectingRounds>0&&gun.SelectedWeapon.ReserveAmmo>0,"ammo flies to player from 3.5 metres away");
  var anim=player.GetComponent<AnimationController>()!;var control=player.GetComponent<PlayerController3D>();if(control!=null)control.Enabled=false;var motor=player.GetComponent<CharacterController3D>()!;motor.Enabled=false;
  for(int i=0;i<40;i++){motor.SetVelocity(player.Transform.Forward*6+player.Transform.Right*(i==20?2:.05f));typeof(AnimationController).GetMethod("UpdateAdvancedPoseControls",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(anim,null);Frame();}
  var local=(Vector2)typeof(AnimationController).GetField("_stableLocalVelocity",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(anim)!;
  Check(MathF.Abs(local.X)<MathF.Abs(local.Y)*.1f,"brief lateral velocity spike does not become a sustained strafe blend");
  for(int i=0;i<20;i++){motor.SetVelocity(player.Transform.Right*6);typeof(AnimationController).GetMethod("UpdateAdvancedPoseControls",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(anim,null);Frame();}
  local=(Vector2)typeof(AnimationController).GetField("_stableLocalVelocity",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(anim)!;Check(MathF.Abs(local.X)>5&&MathF.Abs(local.Y)<1,"intentional sustained strafing remains available");
  op.TargetPlaySeconds=18000;Set(op,"TargetStarted",true);Set(op,"TargetElapsedSeconds",0d);Set(op,"Credits",20);op.Tick(30);Check(Math.Abs(op.DaysRemaining-(10-1d/60))<.001,"ten days maps to five active hours");
  double elapsed=op.TargetElapsedSeconds;scene.SimulationTimeScale=0;Frame(3);Check(op.TargetElapsedSeconds==elapsed,"pause freezes the contract clock");scene.SimulationTimeScale=1;
  Set(op,"Credits",30000);op.Tick(.05f);Check(op.TargetWon&&!op.TargetExpired&&!op.StartOperation(),"30,000 held credits passes the level and blocks another operation");
  op.Retry();Check(op.Phase==MiningPhase.Failed&&op.TargetWon,"ordinary retry cannot bypass a completed contract");
  Set(op,"TargetWon",false);Set(op,"TargetElapsedSeconds",17999.9d);Set(op,"Credits",20);op.Tick(.2f);Check(op.TargetExpired&&!op.TargetWon,"deadline fails a below-target contract");
  var saved=new MiningOperation3D.MiningSave{TargetElapsed=op.TargetElapsedSeconds,TargetStarted=true,TargetExpired=true};var restored=JsonSerializer.Deserialize<MiningOperation3D.MiningSave>(JsonSerializer.Serialize(saved))!;Check(restored.TargetExpired&&restored.TargetElapsed==18000,"deadline state survives save round trip");
  Check(SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene")).SequenceEqual(hash),"verification does not modify the authored scene");
  Directory.CreateDirectory("artifacts/hazard-cleanup");File.WriteAllText("artifacts/hazard-cleanup/native-results.json",JsonSerializer.Serialize(new{checks,count=checks.Count},new JsonSerializerOptions{WriteIndented=true}));host.UnloadScene();Console.WriteLine("CLEANUP CHECKS COMPLETE "+checks.Count);
 }
}
