using System.ComponentModel;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningOperation3D
{
 [Category("Cargo upgrades")]public int CargoCapacityPerUpgrade{get;set;}=100;
 [Category("Cargo upgrades")]public int CargoUpgradeBaseCost{get;set;}=80;
 [Category("Live state")]public int CargoUpgradeLevel{get;private set;}
 public int CargoUpgradeCost=>(int)Math.Clamp(Math.Ceiling(Math.Max(1,CargoUpgradeBaseCost)*Math.Pow(1.5,Math.Clamp(CargoUpgradeLevel,0,50))),1,1000000000);
 public bool UpgradeCargo(){Initialize();if(CargoCapacity>=1000000||!SpendCredits(CargoUpgradeCost))return false;CargoUpgradeLevel++;SaveProgress();return true;}
}
