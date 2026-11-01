using System.Text.Json;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningCampTutorialCheck
{
 public static void Run(Scene scene,MiningOperation3D op,MiningTrade3D trade,Action<bool,string> check)
 {
  void Set(object o,string p,object value)=>o.GetType().GetProperty(p)!.SetValue(o,value);
  var audio=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningAudio3D>()).Single();
  check(audio.DrillDistanceGain(0)==1&&audio.DrillDistanceGain(audio.DrillFarDistance)==0&&audio.DrillDistanceGain(10)>audio.DrillDistanceGain(40),"drill attenuation is full nearby, softer with distance and silent at the far limit for either WAV channel format");
  Set(op,"RefinedOre",0);Set(op,"CompletedRounds",1);Set(op,"Phase",MiningPhase.Preparation);
  check(!trade.DeliverContract()&&trade.AcceptDeliveryContract(1)&&op.ActiveDeliveryContract?.Quantity==40,"delivery contracts require an accepted buyer order and can be accepted before gathering ore");
  check(!trade.AcceptDeliveryContract(0)&&!trade.DeliverContract(),"active order cannot be overwritten or delivered without its full cargo");
  var saved=JsonSerializer.Deserialize<MiningOperation3D.MiningSave>(JsonSerializer.Serialize(new MiningOperation3D.MiningSave{ActiveDeliveryContract=op.ActiveDeliveryContract,TutorialStep=(int)CampTutorialStep.Refinery}))!;
  check(saved.ActiveDeliveryContract is {Quantity:40,UnitPrice:5}&&saved.TutorialStep==(int)CampTutorialStep.Refinery,"accepted order terms and tutorial step survive save serialization");
  Set(op,"RefinedOre",40);int credits=op.Credits;check(trade.DeliverContract()&&op.RefinedOre==0&&op.ActiveDeliveryContract==null&&trade.LockedSaleCredits==200,"fulfilled order reserves exact required cargo and the advertised fixed reward");
  trade.Tick(trade.SaleSeconds);check(op.Credits==credits+200&&op.DeliveryContractsCompleted>=2,"contract completion pays once and counts fulfilled jobs");
  var ui=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningBusinessUi3D>()).Single();Set(ui,"Screen",MiningScreen.Market);ui.SelectMarketMethod(true);ui.SelectContract(0);
  check(op.ActiveDeliveryContract?.Quantity==20,"native contract card accepts the chosen order through its existing event binding");
  Set(op,"RefinedOre",20);ui.Primary();check(trade.SaleInProgress&&trade.SaleQuantity==20,"native Deliver button dispatches the accepted order without a quantity slider");trade.Tick(trade.SaleSeconds);Set(ui,"Screen",MiningScreen.None);
  Set(op,"LastMarketSale",null!);Set(op,"RefinedOre",0);Set(op,"CompletedRounds",0);Set(op,"TutorialStep",CampTutorialStep.AmmoPress);
  var bucket=scene.FindGameObject(ui.AmmoMachineObjectId)!.GetComponent<MiningAmmoBucket3D>()!;
  var gun=scene.FindGameObject(op.PlayerObjectId)!.GetComponent<FpsGunLab3D>()!.SelectedWeapon!;gun.SetAmmo(gun.CurrentAmmo,gun.Definition!.ReserveCapacity);Set(bucket,"CollectionSequence",0L);op.TickCampTutorial(.1f);
  check(gun.ReserveAmmo<=op.TutorialStartingReserve,"resuming the ammo tutorial with restored full ammunition preserves room for the first pickup");
  Set(bucket,"CollectionSequence",1L);op.TickCampTutorial(.1f);
  check(op.TutorialStep==CampTutorialStep.StartDrill&&op.TutorialPopup.Contains("AMMO COLLECTED"),"collecting ammo advances the tutorial and explains automatic production");
  Set(op,"Phase",MiningPhase.Drilling);op.TickCampTutorial(.1f);check(op.TutorialStep==CampTutorialStep.WinRound,"starting the drill changes guidance to defending the operation");
  Set(op,"Phase",MiningPhase.Failed);op.TickCampTutorial(.1f);check(op.TutorialStep==CampTutorialStep.WinRound,"a lost first round never falsely completes the win tutorial step");
  Set(op,"Phase",MiningPhase.Preparation);op.TickCampTutorial(.1f);check(op.TutorialStep==CampTutorialStep.StartDrill,"first-round recovery restores drill guidance for another attempt");Set(op,"TutorialStep",CampTutorialStep.WinRound);
  Set(op,"Phase",MiningPhase.Preparation);Set(op,"CompletedRounds",1);op.TickCampTutorial(.1f);check(op.TutorialStep==CampTutorialStep.Refinery,"winning the round directs the player to the refinery");
  Set(op,"RefiningAmount",3);op.TickCampTutorial(.1f);check(op.TutorialStep==CampTutorialStep.WaitForRefining,"tutorial waits for actual refinery output");
  Set(op,"RefiningAmount",0);Set(op,"RefinedOre",3);op.TickCampTutorial(.1f);check(op.TutorialStep==CampTutorialStep.CompanyHub,"finished refined ore directs the player to the company hub");
  check(trade.BeginSale(true,3),"tutorial first sale can use the live market");op.TickCampTutorial(.1f);check(op.TutorialStep==CampTutorialStep.WaitForSale,"tutorial explains shipping after accepting a sale");trade.Tick(trade.SaleSeconds);op.TickCampTutorial(.1f);check(op.TutorialStep==CampTutorialStep.Complete,"paid refined-ore sale completes onboarding");
  op.TickCampTutorial(6);check(op.TutorialPopup.Length==0,"tutorial completion popup expires instead of remaining over the HUD");
  Set(op,"TutorialStep",CampTutorialStep.AmmoPress);op.SkipCampTutorial();check(!op.TutorialActive,"tutorial can be finished using the exposed event action");Set(op,"RefinedOre",0);
 }
}
