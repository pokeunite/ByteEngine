using System.ComponentModel;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Gameplay;
using Component=ByteEngine.Core.Scene.Component;
namespace KarlsonDemo.Plugin;
/// <summary>Real workshop production and a bounded fictional commodity market; independent of presentation.</summary>
public sealed partial class MiningTrade3D:Component
{
 [SceneObjectReference] public Guid OperationObjectId{get;set;}
 [Category("Ammo press")] public float BaseOutputPerSecond{get;set;}=3;
 [Category("Ammo press")] public float ExtraOutputPerLevel{get;set;}=1;
 [Category("Ammo press")] public int BaseStorage{get;set;}=100;
 [Category("Ammo press")] public int ExtraStoragePerLevel{get;set;}=50;
 [Category("Ammo press")] public float EfficiencyPerLevel{get;set;}=.1f;
 [Category("Ammo press / cycles")] public bool UseProductionCycles{get;set;}
 [Category("Ammo press / cycles")] public float BaseCycleSeconds{get;set;}=5;
 [Category("Ammo press / cycles")] public float CycleSecondsReductionPerLevel{get;set;}=.5f;
 [Category("Ammo press / cycles")] public float MinimumCycleSeconds{get;set;}=1;
 [Category("Ammo press / cycles")] public int BaseRoundsPerCycle{get;set;}=5;
 [Category("Ammo press / cycles")] public int ExtraRoundsPerCycle{get;set;}=1;
 public float CycleDurationAt(int level)=>Math.Max(Math.Max(.1f,MinimumCycleSeconds),BaseCycleSeconds-Math.Max(0,level)*Math.Max(0,CycleSecondsReductionPerLevel));
 public float CycleDuration=>CycleDurationAt(OutputLevel);
 public float RoundsAt(int level)=>Math.Max(1,BaseRoundsPerCycle+Math.Max(0,level)*Math.Max(0,ExtraRoundsPerCycle))*(1+ProductionResearchBonus);
 public float RoundsPerCycle=>RoundsAt(EfficiencyLevel);
 public float ProductionCycleProgress=>Math.Clamp(productionClock/Math.Max(.1f,CycleDuration),0,1);
 float productionClock;
 [Category("Ammo press")] public int OutputCost{get;set;}=140;
 [Category("Ammo press")] public int StorageCost{get;set;}=110;
 [Category("Ammo press")] public int EfficiencyCost{get;set;}=180;
 [Category("Ammo press")] public float UpgradeCostGrowth{get;set;}=2.15f;
 [Category("Ammo press")] public int MaximumLevel{get;set;}=5;
 [Category("Market")] public float LiveMarketPriceBonus{get;set;}=1.2f;
 [Category("Market")] public float MinimumMultiplier{get;set;}=.65f;
 [Category("Market")] public float MaximumMultiplier{get;set;}=1.6f;
 [Category("Market")] public float PriceIntervalSeconds{get;set;}=1;
 [Category("Market")] public float PriceVolatility{get;set;}=.035f;
 [Category("Permanent upgrades")] public string ResearchDefinitionsJson{get;set;}=MiningResearchDefaults.Json;
 public IReadOnlyList<CompanyUpgrade> Research=>_research;
 public IReadOnlyCollection<string> PurchasedResearch=>_purchased;
 public float ProductionResearchBonus{get;private set;}
 public bool AutomaticRefining{get;private set;}
 public bool AutomaticAmmoDelivery{get;private set;}
 [Category("Persistence")] public bool PersistProgress{get;set;}=true;
 [Category("Persistence")] public string SaveSlot{get;set;}="mining-workshop-v1.json";
 public int OutputLevel{get;private set;}
 public int StorageLevel{get;private set;}
 public int EfficiencyLevel{get;private set;}
 public float StoredAmmo{get;private set;}
 public float PriceMultiplier{get;private set;}=1;
 public int PriceSamples{get;private set;}
 public float PriceClock{get;private set;}
 public uint MarketSeed{get;private set;}=247319;
 public IReadOnlyList<float> History=>_history;
 public float Output=>OutputAt(OutputLevel,EfficiencyLevel);
 public float OutputAt(int speedLevel,int payloadLevel)=>UseProductionCycles?RoundsAt(payloadLevel)/CycleDurationAt(speedLevel):Math.Max(0,BaseOutputPerSecond+speedLevel*ExtraOutputPerLevel)*(1+payloadLevel*Math.Max(0,EfficiencyPerLevel))*(1+ProductionResearchBonus);
 public int Capacity=>Math.Max(1,BaseStorage+StorageLevel*ExtraStoragePerLevel);
 public int CollectibleAmmo{get{var gun=Operation is {} op?GameObject.Scene?.FindGameObject(op.PlayerObjectId)?.GetComponent<FpsGunLab3D>():null;long room=0;if(gun!=null)foreach(var w in gun.CarriedWeapons)if(w.Definition is {} d)room+=Math.Max(0,d.ReserveCapacity-w.ReserveAmmo);return (int)Math.Min((int)StoredAmmo,room);}}
 public MiningOperation3D? Operation=>GameObject.Scene?.FindGameObject(OperationObjectId)?.GetComponent<MiningOperation3D>();
 readonly List<float> _history=[];bool initialized;float saveClock;
 readonly List<CompanyUpgrade> _research=[];readonly HashSet<string> _purchased=new(StringComparer.Ordinal);float? _baseHealth,_baseEnergy;readonly Dictionary<string,int> _ranks=new(StringComparer.Ordinal);
 readonly Dictionary<WeaponDefinition3D,float> weaponAccuracy=[];
 readonly Dictionary<WeaponDefinition3D,(int Magazine,float Reload,float EmptyReload,int Reserve,float Rpm,int Pellets,float Damage)> _weaponBaselines=[];
 float? _baseDashCooldown;
 public string LastError{get;private set;}="";
 public int LastSaleTotal=>Operation?.LastMarketSale?.Credits??0;
 public float LastSaleQuote=>Operation?.LastMarketSale?.Quote??0;
 protected override void OnStart()=>Initialize();
 protected override void OnUpdate()=>Tick((float)Time.DeltaTime);
 protected override void OnStop(){StopReviewedResearch();Save();}
 public void Initialize()
 {
  if(initialized)return;initialized=true;
  try{var nodes=JsonSerializer.Deserialize<CompanyUpgrade[]>(ResearchDefinitionsJson)??[];if(nodes.Length>128||nodes.Select(n=>n.Id).Distinct().Count()!=nodes.Length||nodes.Any(n=>string.IsNullOrWhiteSpace(n.Id)||n.Cost<0||n.MaxRank<1||n.MaxRank>50||n.RankCosts.Any(c=>c<0)||(n.RankCosts.Length>0&&n.RankCosts.Length!=n.MaxRank)||!float.IsFinite(n.Amount)||(n.Amount<0&&n.Stat!="MortarStrikeInterval")||!float.IsFinite(n.ReloadSpeedBonus)||n.ReloadSpeedBonus<0||n.MagazineBonus<0||!float.IsFinite(n.DashCooldownReduction)||n.DashCooldownReduction<0||!Enum.IsDefined(n.Effect)||n.Prerequisites.Any(p=>!nodes.Any(x=>x.Id==p))))throw new InvalidDataException("Invalid company upgrade definitions.");
   bool Cycle(string id,HashSet<string> path){if(!path.Add(id))return true;bool result=nodes.Single(n=>n.Id==id).Prerequisites.Any(p=>Cycle(p,new(path)));return result;}
   if(nodes.Any(n=>Cycle(n.Id,[])))throw new InvalidDataException("Company upgrade prerequisites contain a cycle.");_research.AddRange(nodes);
  }catch(Exception e)when(e is JsonException or InvalidDataException){LastError=e.Message;}
  if(PersistProgress)try{if(File.Exists(PathName)){var d=JsonSerializer.Deserialize<State>(File.ReadAllText(PathName));if(d?.Version==1){MigrateReviewedSave(d);MigrateWeaponUpgradeState(d,_research.Select(n=>n.Id).ToHashSet());if(d.Ranks!=null)foreach(var pair in d.Ranks){var node=_research.FirstOrDefault(n=>n.Id==pair.Key);if(node!=null)_ranks[pair.Key]=Math.Clamp(pair.Value,0,node.MaxRank);}OutputLevel=Math.Clamp(d.Output,0,MaximumLevel);StorageLevel=Math.Clamp(d.Storage,0,MaximumLevel);EfficiencyLevel=Math.Clamp(d.Efficiency,0,MaximumLevel);StoredAmmo=float.IsFinite(d.Ammo)?Math.Clamp(d.Ammo,0,Capacity):0;PriceMultiplier=float.IsFinite(d.Price)?Math.Clamp(d.Price,.1f,10):1;MarketSeed=d.Seed;PriceSamples=Math.Max(0,d.Samples);PriceClock=float.IsFinite(d.Clock)?Math.Clamp(d.Clock,0,Math.Max(.1f,PriceIntervalSeconds)):0;DemandClock=float.IsFinite(d.DemandClock)?Math.Clamp(d.DemandClock,0,Math.Max(3,DemandPeriodSeconds*3)):0;_history.AddRange((d.History??[]).Where(float.IsFinite).TakeLast(80));_purchased.UnionWith((d.Purchased??[]).Where(id=>_research.Any(n=>n.Id==id)));}}}catch(Exception e)when(e is IOException or JsonException or UnauthorizedAccessException){LastError=e.Message;}
  if(HasReviewedCatalog){_purchased.Add("charter");_ranks["charter"]=1;}
  if(_history.Count==0)_history.Add(PriceMultiplier);
  ApplyResearch();if(HasReviewedCatalog)Save();
 }
 public void Tick(float delta)
 {
  if(!float.IsFinite(delta)||delta<=0)return;Initialize();var op=Operation;if(op==null)return;
  TickShipment(delta);TickReviewedResearch(delta);
  if(op.Phase!=MiningPhase.Failed)
  {
   if(!UseProductionCycles)StoredAmmo=Math.Min(Capacity,StoredAmmo+Output*delta);
   else if(StoredAmmo>=Capacity)productionClock=0;
   else{productionClock+=delta;int cycles=(int)Math.Min(100000,Math.Floor(productionClock/CycleDuration));if(cycles>0){StoredAmmo=Math.Min(Capacity,StoredAmmo+cycles*RoundsPerCycle);productionClock-=cycles*CycleDuration;if(StoredAmmo>=Capacity)productionClock=0;}}
  }
  if(op.CanUseIndustry){if(AutomaticRefining)op.AutoRefineryEnabled=true;if(AutomaticAmmoDelivery)Collect();}
  DemandClock=(DemandClock+delta)%Math.Max(3,DemandPeriodSeconds*3);
  PriceClock=Math.Min(PriceClock+delta,Math.Max(.1f,PriceIntervalSeconds)*4);int budget=4;while(PriceClock>=Math.Max(.1f,PriceIntervalSeconds)&&budget-->0){PriceClock-=Math.Max(.1f,PriceIntervalSeconds);StepPrice();}
  saveClock+=delta;if(saveClock>=5){saveClock=0;Save();}
 }
 void StepPrice(){MarketSeed=unchecked(MarketSeed*1664525+1013904223);float n=((MarketSeed>>8)/16777215f)*2-1;float low=Math.Clamp(MinimumMultiplier,.1f,10),high=Math.Clamp(MaximumMultiplier,low,10);PriceMultiplier=MathF.Round(Math.Clamp(PriceMultiplier+n*Math.Clamp(PriceVolatility,0,1)+(DemandTarget-PriceMultiplier)*.02f,low,high),3);_history.Add(PriceMultiplier);if(_history.Count>80)_history.RemoveAt(0);PriceSamples++;}
 public float Quote(bool refined)=>MathF.Round(Math.Max(0,refined?Operation?.RefinedOrePrice??5:Operation?.RawOrePrice??2)*PriceMultiplier*Math.Clamp(LiveMarketPriceBonus,.1f,5)+Stat("OilPriceFloor"),2);
 public int Stock(bool refined)=>Math.Max(0,(refined?Operation?.RefinedOre??0:Math.Max(0,(Operation?.RawOre??0)-(Operation?.RefiningAmount??0))));
 public int Level(int row)=>row switch{0=>OutputLevel,1=>StorageLevel,_=>EfficiencyLevel};
 [System.ComponentModel.Category("Ammo press costs"),System.ComponentModel.Description("Additional price scale for every ammo-press upgrade rank; 0.8 reduces existing prices by 20%.")]public float AmmoPressUpgradePriceScale{get;set;}=.8f;
 [System.ComponentModel.Category("Ammo press costs")]public float AmmoPressCostMultiplier{get;set;}=.8f;
 public int Cost(int row)=>(int)Math.Clamp(Math.Round((row switch{0=>OutputCost,1=>StorageCost,_=>EfficiencyCost})*Math.Pow(Math.Clamp(UpgradeCostGrowth,1,3),Level(row))*Math.Clamp(float.IsFinite(AmmoPressCostMultiplier)?AmmoPressCostMultiplier:.8f,.01f,10)*Math.Clamp(float.IsFinite(AmmoPressUpgradePriceScale)?AmmoPressUpgradePriceScale:.8f,.01f,10)),1,1000000000);
 public bool Buy(int row){Initialize();if(row is <0 or >2||Level(row)>=MaximumLevel||Operation?.SpendCredits(Cost(row))!=true)return false;switch(row){case 0:OutputLevel++;break;case 1:StorageLevel++;break;default:EfficiencyLevel++;break;}Save();return true;}
 public int Collect()
 {
  if(Operation is not {} op||op.Phase==MiningPhase.Failed)return 0;
  var gun=GameObject.Scene?.FindGameObject(op.PlayerObjectId)?.GetComponent<FpsGunLab3D>();if(gun==null)return 0;
  int total=0;void Fill(WeaponInstance3D w){if(w.Definition is not {} d)return;int n=Math.Min((int)StoredAmmo,Math.Max(0,d.ReserveCapacity-w.ReserveAmmo));if(n<=0)return;w.SetAmmo(w.CurrentAmmo,w.ReserveAmmo+n);StoredAmmo-=n;total+=n;}
  if(gun.SelectedWeapon is {} selected)Fill(selected);foreach(var w in gun.CarriedWeapons)if(w!=gun.SelectedWeapon)Fill(w);
  if(total>0)Save();return total;
 }
 public bool Sell(bool refined,int quantity)=>BeginSale(refined,quantity);
 public bool HasResearch(string id)=>_purchased.Contains(id);
 public bool ResearchUnlocked(string id)=>_research.FirstOrDefault(n=>n.Id==id) is {} node&&node.Prerequisites.All(_purchased.Contains);
 public bool CanBuyResearch(string id)=>Operation is {Phase:MiningPhase.Preparation} op&&_research.FirstOrDefault(n=>n.Id==id) is {} node&&node.Available&&op.CompletedRounds+1>=node.MinimumRound&&ResearchRank(id)<node.MaxRank&&ResearchUnlocked(id)&&op.Credits>=ResearchCost(id);
 public bool BuyResearch(string id){Initialize();if(!CanBuyResearch(id))return false;var node=_research.Single(n=>n.Id==id);if(Operation?.SpendCredits(ResearchCost(id))!=true)return false;_ranks[id]=ResearchRank(id)+1;_purchased.Add(id);ApplyResearch();Save();return true;}

 void ApplyResearch()
 {
  var op=Operation;if(op==null)return;
  float Sum(CompanyUpgradeEffect effect)=>_research.Where(n=>_purchased.Contains(n.Id)&&n.Effect==effect&&string.IsNullOrEmpty(n.Stat)).Sum(n=>n.Amount*ResearchRank(n.Id));
  op.SetPermanentBonuses(Sum(CompanyUpgradeEffect.Damage),Sum(CompanyUpgradeEffect.Extraction),Sum(CompanyUpgradeEffect.RefineryYield));ProductionResearchBonus=Sum(CompanyUpgradeEffect.AmmoProduction);ApplyExtendedResearch();
  var player=GameObject.Scene?.FindGameObject(op.PlayerObjectId);
  var owned=_research.Where(n=>_purchased.Contains(n.Id)).ToArray();
  AutomaticRefining=owned.Any(n=>n.AutoRefine);AutomaticAmmoDelivery=owned.Any(n=>n.AutoCollectAmmo);

  foreach(var weapon in GameObject.Scene!.GameObjects.SelectMany(o=>o.Components).OfType<WeaponDefinition3D>())
  {
   string branch=weapon.RequiredResearchId=="shotgun"?"Shotgun":weapon.RequiredResearchId=="ar"?"Rifle":"Pistol";var matching=owned.Where(n=>n.Branch==branch||n.Branch=="Company");float reload=1+Math.Clamp(matching.Sum(n=>n.ReloadSpeedBonus*ResearchRank(n.Id)),0,10);int magazine=Math.Clamp(matching.Sum(n=>n.MagazineBonus*ResearchRank(n.Id)),0,1000);if(!weaponAccuracy.ContainsKey(weapon))weaponAccuracy[weapon]=weapon.AccuracyDegrees;weapon.AccuracyDegrees=weaponAccuracy[weapon]/(1+matching.Where(n=>n.Stat=="HandlingAccuracyPct").Sum(n=>n.Amount*ResearchRank(n.Id))/100);
   if(!_weaponBaselines.TryGetValue(weapon,out var baseline))_weaponBaselines[weapon]=baseline=(weapon.MagazineCapacity,weapon.ReloadSeconds,weapon.EmptyReloadSeconds,weapon.ReserveCapacity,weapon.RoundsPerMinute,weapon.PelletsPerShot,weapon.DamagePerPellet);
   weapon.MagazineCapacity=Math.Clamp(baseline.Magazine+magazine,1,10000);weapon.ReloadSeconds=baseline.Reload/reload;weapon.EmptyReloadSeconds=baseline.EmptyReload/reload;weapon.ReserveCapacity=Math.Clamp((int)((baseline.Reserve+Stat("SmallAmmoCap"))*(1+Stat("AmmoCapacityPct")/100)),12,100000);weapon.RoundsPerMinute=baseline.Rpm*(1+(branch=="Pistol"?Stat("PistolFireRatePct"):0)/100);weapon.PelletsPerShot=Math.Clamp(baseline.Pellets+(branch=="Pistol"?(int)Stat("PistolPellets"):0),1,16);weapon.DamagePerPellet=(baseline.Damage+(branch=="Pistol"?Stat("PistolDamageFlat"):0))*(1+Math.Clamp(matching.Where(n=>n.Stat=="WeaponDamagePct").Sum(n=>n.Amount*ResearchRank(n.Id)),0,500)/100);
   if(HasReviewedCatalog){bool shotgun=weapon.RequiredResearchId=="shotgun",rifle=weapon.RequiredResearchId=="ar";weapon.HomingTargetCount=shotgun?(int)Stat("ShotgunHomingTargets"):0;weapon.PenetrationTargets=rifle?(int)Stat("RiflePierce"):0;weapon.BurstAccuracyBonus=rifle?Stat("RifleBurstAccuracyPct")/100:0;}
  }
  if(player?.GetComponent<DashAbility3D>() is {} dash){_baseDashCooldown??=dash.Cooldown;dash.Cooldown=_baseDashCooldown.Value*(1-Math.Clamp(owned.Sum(n=>n.DashCooldownReduction),0,.8f));}
  if(player?.GetComponent<HealthComponent>() is {} hp){_baseHealth??=hp.MaxHealth;float fraction=hp.HealthPercent;hp.MaxHealth=_baseHealth.Value+Sum(CompanyUpgradeEffect.Health)+Stat("PlayerMaxHealth");hp.CurrentHealth=hp.MaxHealth*fraction;}
  if(player?.GetComponent<ResourceMeter3D>() is {} energy){_baseEnergy??=energy.Capacity;float fraction=energy.Fraction;energy.Capacity=_baseEnergy.Value+Sum(CompanyUpgradeEffect.Energy);energy.SetAmount(energy.Capacity*fraction);}
 }
 string PathName{get{if(Path.GetFileName(SaveSlot)!=SaveSlot||string.IsNullOrWhiteSpace(SaveSlot))throw new InvalidDataException("Workshop save must be a filename.");return Path.Combine(GameSaveStorage.GetDirectory(GameObject.Scene?.ProjectRoot??throw new InvalidDataException("No project save context.")),SaveSlot);}}
 public void Save(){if(!PersistProgress||!initialized)return;try{Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);File.WriteAllText(PathName+".tmp",JsonSerializer.Serialize(new State{DemandClock=DemandClock,ResearchRevision=HasReviewedCatalog?2:0,Output=OutputLevel,Storage=StorageLevel,Efficiency=EfficiencyLevel,Ammo=StoredAmmo,Price=PriceMultiplier,Seed=MarketSeed,Samples=PriceSamples,Clock=PriceClock,History=_history.ToArray(),Purchased=_purchased.ToArray(),Ranks=_ranks}));File.Move(PathName+".tmp",PathName,true);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){LastError=e.Message;}}
 public sealed class State{public float DemandClock{get;set;}public int ResearchRevision{get;set;}public Dictionary<string,int>? Ranks{get;set;}public int Version{get;set;}=1;public int Output{get;set;}public int Storage{get;set;}public int Efficiency{get;set;}public float Ammo{get;set;}public float Price{get;set;}=1;public uint Seed{get;set;}=247319;public int Samples{get;set;}public float Clock{get;set;}public float[]? History{get;set;}public string[]? Purchased{get;set;}}
}
