using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using KarlsonDemo.Plugin;
static class MiningWorkshopBalanceTests
{
 public static void Run()
 {
  void Check(bool pass,string label){if(!pass)throw new Exception(label);Console.WriteLine("PASS workshop: "+label);}
  void Set(object item,string name,object value)=>item.GetType().GetProperty(name)!.SetValue(item,value);
  var scene=new Scene("Workshop balance");var player=scene.CreateGameObject("Operator");player.Transform.WorldPosition=new(4,0,6);player.AddComponent(new HealthComponent());var motor=player.AddComponent(new CharacterController3D());player.AddComponent(new PlayerController3D());
  var drill=scene.CreateGameObject("Drill");drill.AddComponent(new HealthComponent());
  var company=scene.CreateGameObject("Company");var op=company.AddComponent(new MiningOperation3D{PlayerObjectId=player.Id,DrillObjectId=drill.Id,PersistProgress=false,StartingCredits=100000,RefineryBatch=3,ExtraBatchPerLevel=1,RefinerySeconds=3,RefinerySpeedPerLevel=.25f,LinearRefinerySpeedUpgrades=true,QueueRefineryBatches=true});op.Initialize();
  Set(op,"RawOre",9);Check(op.BeginRefining()&&op.RefiningAmount==3,"queue reserves three ore, not the entire cargo");op.Tick(2.9f);Check(op.RefinedOre==0,"batch cannot pay out early");op.Tick(.11f);Check(op.RefinedOre==3&&op.RawOre==6&&op.RefiningAmount==3,"first cycle pays only three and queues the next batch");op.Tick(3);op.Tick(3);Check(op.RefinedOre==9&&op.RawOre==0&&!op.RefineryQueueActive,"queue completes without duplicate payout");
  Check(op.Buy(MiningUpgrade.RefinerySpeed)&&Math.Abs(op.RefineryDuration-2.75f)<.001f,"speed upgrade subtracts exactly a quarter second");Check(op.Buy(MiningUpgrade.Refinery)&&op.RefineryBatch+op.RefineryLevel*op.ExtraBatchPerLevel==4,"batch upgrade adds one input per cycle independently");
  op.RefineryBatch=3;Set(op,"RefineryLevel",0);Set(op,"RefineryYieldLevel",1);Set(op,"RawOre",30);Check(op.BeginRefining(),"yield queue starts");for(int i=0;i<10;i++)op.Tick(3);Check(op.RawOre==0&&op.RefinedOre==42,"fractional yield carries between small batches instead of discarding recovery upgrades");
  var trade=company.AddComponent(new MiningTrade3D{OperationObjectId=company.Id,PersistProgress=false,UseProductionCycles=true,BaseCycleSeconds=5,BaseRoundsPerCycle=5,ExtraRoundsPerCycle=1,BaseStorage=35,ExtraStoragePerLevel=15});trade.Initialize();trade.Tick(4.9f);Check(trade.StoredAmmo==0&&trade.ProductionCycleProgress>.97f,"press waits for the full five-second cycle");trade.Tick(.11f);Check(trade.StoredAmmo==5,"press ejects five rounds per completed cycle");trade.Tick(100);Check(trade.StoredAmmo==35&&trade.ProductionCycleProgress==0,"bucket clamps at 35 and banks no overflow");
  Check(trade.Buy(0)&&trade.CycleDuration==4.5f,"cycle time upgrade changes duration, not a misleading rounds-per-second value");Check(trade.Buy(2)&&trade.RoundsPerCycle==6,"payload upgrade adds a real round per cycle");
  // Collection visuals are covered by the real-project runtime check, where the
  // inventory, attachment and model marker systems are present.
  player.Transform.WorldPosition=new(22,0,20);motor.SetVelocity(new(8,0,2));Set(op,"Phase",MiningPhase.Failed);typeof(MiningOperation3D).GetMethod("BeginRecovery",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(op,null);op.TickRecovery(.3f);Check(op.RecoveryOpacity>0&&op.RecoveryOpacity<1,"defeat fades out before teleporting");op.TickRecovery(.4f);Check(player.Transform.WorldPosition==new Vector3(4,0,6)&&motor.Velocity==Vector3.Zero&&op.Phase==MiningPhase.Preparation&&op.RecoveryOpacity==1,"player respawns with zero velocity while screen is black");op.TickRecovery(.5f);op.TickRecovery(.7f);Check(!op.Recovering&&op.RecoveryOpacity==0&&player.GetComponent<PlayerController3D>()!.Enabled,"fade completes and restores controls");
 }
}
