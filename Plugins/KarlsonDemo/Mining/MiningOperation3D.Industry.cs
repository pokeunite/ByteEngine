using System.ComponentModel;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningOperation3D
{
 [Category("Refinery automation")]public bool AutoRefineryEnabled{get;set;}
 [Category("Refinery automation")]public float AutomaticRefineryTimeMultiplier{get;set;}=1.35f;
 public MiningDefensePlanner3D? DefencePlanner=>GameObject.Scene?.GetComponentsSnapshot<MiningDefensePlanner3D>().FirstOrDefault(p=>p.WorkshopObjectId==GameObject.Id);
 bool automaticBatch,refineryAutoRun;
 [Category("Upgrade costs")]public int DrillMaximumLevel{get;set;}=12;
 public int UpgradeLimit(MiningUpgrade kind)=>kind==MiningUpgrade.Drill?Math.Clamp(DrillMaximumLevel,1,50):Math.Clamp(MaximumUpgradeLevel,1,50);
 public bool ToggleAutoRefinery(){AutoRefineryEnabled=!AutoRefineryEnabled;SaveProgress();return AutoRefineryEnabled;}
 void TickAutomaticRefinery(){if(AutoRefineryEnabled&&CanUseIndustry&&RefiningAmount==0&&RawOre>0){automaticBatch=true;BeginRefining();automaticBatch=false;}}
 public bool IsUpgradeMaxed(MiningUpgrade kind)=>Level(kind)>=UpgradeLimit(kind)||(kind==MiningUpgrade.Refinery&&RefineryBatchSizeAt(RefineryLevel+1)<=RefineryBatchSizeAt(RefineryLevel));
 public string UpgradeAvailability(MiningUpgrade kind)=>!CanUseIndustry?"Unavailable during recovery":IsUpgradeMaxed(kind)?"MAX LEVEL":Credits<Cost(kind)?$"Need {Cost(kind):N0} credits":"Available";
}
