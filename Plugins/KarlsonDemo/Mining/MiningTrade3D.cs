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
 [Category("Ammo press")] public int OutputCost{get;set;}=35;
 [Category("Ammo press")] public int StorageCost{get;set;}=25;
 [Category("Ammo press")] public int EfficiencyCost{get;set;}=40;
 [Category("Ammo press")] public int MaximumLevel{get;set;}=5;
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
 public float Output=>Math.Max(0,BaseOutputPerSecond+OutputLevel*ExtraOutputPerLevel)*(1+EfficiencyLevel*Math.Max(0,EfficiencyPerLevel))*(1+ProductionResearchBonus);
 public float OutputAt(int speedLevel,int payloadLevel)=>Math.Max(0,BaseOutputPerSecond+speedLevel*ExtraOutputPerLevel)*(1+payloadLevel*Math.Max(0,EfficiencyPerLevel))*(1+ProductionResearchBonus);
 public int Capacity=>Math.Max(1,BaseStorage+StorageLevel*ExtraStoragePerLevel);
 public int CollectibleAmmo{get{var op=Operation;var weapon=op==null?null:GameObject.Scene?.FindGameObject(op.PlayerObjectId)?.GetComponent<FpsGunLab3D>()?.SelectedWeapon;return weapon?.Definition is {} d?Math.Min((int)StoredAmmo,Math.Max(0,d.ReserveCapacity-weapon.ReserveAmmo)):0;}}
 public MiningOperation3D? Operation=>GameObject.Scene?.FindGameObject(OperationObjectId)?.GetComponent<MiningOperation3D>();
 readonly List<float> _history=[];bool initialized;float saveClock;
 readonly List<CompanyUpgrade> _research=[];readonly HashSet<string> _purchased=new(StringComparer.Ordinal);float? _baseHealth,_baseEnergy;readonly Dictionary<string,int> _ranks=new(StringComparer.Ordinal);
 readonly Dictionary<WeaponDefinition3D,(int Magazine,float Reload,float EmptyReload,int Reserve,float Rpm,int Pellets)> _weaponBaselines=[];
 float? _baseDashCooldown;
 public string LastError{get;private set;}="";
 public int LastSaleTotal{get;private set;}
 public float LastSaleQuote{get;private set;}
 protected override void OnStart()=>Initialize();
 protected override void OnUpdate()=>Tick((float)Time.DeltaTime);
 protected override void OnStop()=>Save();
 public void Initialize()
 {
  if(initialized)return;initialized=true;
  try{var nodes=JsonSerializer.Deserialize<CompanyUpgrade[]>(ResearchDefinitionsJson)??[];if(nodes.Length>128||nodes.Select(n=>n.Id).Distinct().Count()!=nodes.Length||nodes.Any(n=>string.IsNullOrWhiteSpace(n.Id)||n.Cost<0||n.MaxRank<1||n.MaxRank>50||n.RankCosts.Any(c=>c<0)||(n.RankCosts.Length>0&&n.RankCosts.Length!=n.MaxRank)||!float.IsFinite(n.Amount)||(n.Amount<0&&n.Stat!="MortarStrikeInterval")||!float.IsFinite(n.ReloadSpeedBonus)||n.ReloadSpeedBonus<0||n.MagazineBonus<0||!float.IsFinite(n.DashCooldownReduction)||n.DashCooldownReduction<0||!Enum.IsDefined(n.Effect)||n.Prerequisites.Any(p=>!nodes.Any(x=>x.Id==p))))throw new InvalidDataException("Invalid company upgrade definitions.");
   bool Cycle(string id,HashSet<string> path){if(!path.Add(id))return true;bool result=nodes.Single(n=>n.Id==id).Prerequisites.Any(p=>Cycle(p,new(path)));return result;}
   if(nodes.Any(n=>Cycle(n.Id,[])))throw new InvalidDataException("Company upgrade prerequisites contain a cycle.");_research.AddRange(nodes);
  }catch(Exception e)when(e is JsonException or InvalidDataException){LastError=e.Message;}
  if(PersistProgress)try{if(File.Exists(PathName)){var d=JsonSerializer.Deserialize<State>(File.ReadAllText(PathName));if(d?.Version==1){if(d.Ranks!=null)foreach(var pair in d.Ranks){var node=_research.FirstOrDefault(n=>n.Id==pair.Key);if(node!=null)_ranks[pair.Key]=Math.Clamp(pair.Value,0,node.MaxRank);}OutputLevel=Math.Clamp(d.Output,0,MaximumLevel);StorageLevel=Math.Clamp(d.Storage,0,MaximumLevel);EfficiencyLevel=Math.Clamp(d.Efficiency,0,MaximumLevel);StoredAmmo=float.IsFinite(d.Ammo)?Math.Clamp(d.Ammo,0,Capacity):0;PriceMultiplier=float.IsFinite(d.Price)?Math.Clamp(d.Price,.1f,10):1;MarketSeed=d.Seed;PriceSamples=Math.Max(0,d.Samples);PriceClock=float.IsFinite(d.Clock)?Math.Clamp(d.Clock,0,Math.Max(.1f,PriceIntervalSeconds)):0;_history.AddRange((d.History??[]).Where(float.IsFinite).TakeLast(80));_purchased.UnionWith((d.Purchased??[]).Where(id=>_research.Any(n=>n.Id==id)));}}}catch(Exception e)when(e is IOException or JsonException or UnauthorizedAccessException){LastError=e.Message;}
  if(_history.Count==0)_history.Add(PriceMultiplier);
  ApplyResearch();
 }
 public void Tick(float delta)
 {
  if(!float.IsFinite(delta)||delta<=0)return;Initialize();var op=Operation;if(op==null)return;
  TickShipment(delta);
  if(op.Phase!=MiningPhase.Failed)StoredAmmo=Math.Min(Capacity,StoredAmmo+Output*delta);
  if(op.Phase==MiningPhase.Preparation){if(AutomaticRefining&&op.RawOre>0&&op.RefiningAmount==0)op.BeginRefining();if(AutomaticAmmoDelivery)Collect();}
  PriceClock=Math.Min(PriceClock+delta,Math.Max(.1f,PriceIntervalSeconds)*4);int budget=4;while(PriceClock>=Math.Max(.1f,PriceIntervalSeconds)&&budget-->0){PriceClock-=Math.Max(.1f,PriceIntervalSeconds);StepPrice();}
  saveClock+=delta;if(saveClock>=5){saveClock=0;Save();}
 }
 void StepPrice(){MarketSeed=unchecked(MarketSeed*1664525+1013904223);float n=((MarketSeed>>8)/16777215f)*2-1;float low=Math.Clamp(MinimumMultiplier,.1f,10),high=Math.Clamp(MaximumMultiplier,low,10);PriceMultiplier=MathF.Round(Math.Clamp(PriceMultiplier+n*Math.Clamp(PriceVolatility,0,1)+(1-PriceMultiplier)*.006f,low,high),3);_history.Add(PriceMultiplier);if(_history.Count>80)_history.RemoveAt(0);PriceSamples++;}
 public float Quote(bool refined)=>MathF.Round(Math.Max(0,refined?Operation?.RefinedOrePrice??5:Operation?.RawOrePrice??2)*PriceMultiplier+Stat("OilPriceFloor"),2);
 public int Stock(bool refined)=>Math.Max(0,(refined?Operation?.RefinedOre??0:Math.Max(0,(Operation?.RawOre??0)-(Operation?.RefiningAmount??0)))-(SaleInProgress&&SaleRefined==refined?SaleQuantity-SaleProcessed:0));
 public int Level(int row)=>row switch{0=>OutputLevel,1=>StorageLevel,_=>EfficiencyLevel};
 public int Cost(int row)=>(int)Math.Clamp(Math.Ceiling((row switch{0=>OutputCost,1=>StorageCost,_=>EfficiencyCost})*Math.Pow(1.65,Level(row))),1,1000000000);
 public bool Buy(int row){Initialize();if(row is <0 or >2||Level(row)>=MaximumLevel||Operation?.SpendCredits(Cost(row))!=true)return false;switch(row){case 0:OutputLevel++;break;case 1:StorageLevel++;break;default:EfficiencyLevel++;break;}Save();return true;}
 public int Collect()
 {
  if(Operation is not {} op||op.Phase==MiningPhase.Failed)return 0;
  var player=GameObject.Scene?.FindGameObject(op.PlayerObjectId);var weapon=player?.GetComponent<FpsGunLab3D>()?.SelectedWeapon;var def=weapon?.Definition;if(weapon==null||def==null)return 0;
  int amount=Math.Min((int)StoredAmmo,Math.Max(0,def.ReserveCapacity-weapon.ReserveAmmo));if(amount<=0)return 0;weapon.SetAmmo(weapon.CurrentAmmo,weapon.ReserveAmmo+amount);StoredAmmo-=amount;Save();return amount;
 }
 public bool Sell(bool refined,int quantity){if(SaleInProgress)return false;float quote=Quote(refined);if(Operation?.SellUnits(refined,quantity,quote)!=true)return false;LastSaleQuote=quote;LastSaleTotal=MiningOperation3D.SaleTotal(quantity,quote);Save();return true;}
 public bool HasResearch(string id)=>_purchased.Contains(id);
 public bool ResearchUnlocked(string id)=>_research.FirstOrDefault(n=>n.Id==id) is {} node&&node.Prerequisites.All(_purchased.Contains);
 public bool CanBuyResearch(string id)=>Operation is {Phase:MiningPhase.Preparation} op&&_research.FirstOrDefault(n=>n.Id==id) is {} node&&ResearchRank(id)<node.MaxRank&&ResearchUnlocked(id)&&op.Credits>=ResearchCost(id);
 public bool BuyResearch(string id){Initialize();if(!CanBuyResearch(id))return false;var node=_research.Single(n=>n.Id==id);if(Operation?.SpendCredits(ResearchCost(id))!=true)return false;_ranks[id]=ResearchRank(id)+1;_purchased.Add(id);ApplyResearch();Save();return true;}
 void ApplyResearch()
 {
  var op=Operation;if(op==null)return;
  float Sum(CompanyUpgradeEffect effect)=>_research.Where(n=>_purchased.Contains(n.Id)&&n.Effect==effect&&string.IsNullOrEmpty(n.Stat)).Sum(n=>n.Amount*ResearchRank(n.Id));
  op.SetPermanentBonuses(Sum(CompanyUpgradeEffect.Damage),Sum(CompanyUpgradeEffect.Extraction),Sum(CompanyUpgradeEffect.RefineryYield));ProductionResearchBonus=Sum(CompanyUpgradeEffect.AmmoProduction);ApplyExtendedResearch();
  var player=GameObject.Scene?.FindGameObject(op.PlayerObjectId);
  var owned=_research.Where(n=>_purchased.Contains(n.Id)).ToArray();
  AutomaticRefining=owned.Any(n=>n.AutoRefine);AutomaticAmmoDelivery=owned.Any(n=>n.AutoCollectAmmo);
  float reload=1+Math.Clamp(owned.Sum(n=>n.ReloadSpeedBonus),0,10);int magazine=Math.Clamp(owned.Sum(n=>n.MagazineBonus),0,1000);
  foreach(var weapon in GameObject.Scene!.GameObjects.SelectMany(o=>o.Components).OfType<WeaponDefinition3D>())
  {
   if(!_weaponBaselines.TryGetValue(weapon,out var baseline))_weaponBaselines[weapon]=baseline=(weapon.MagazineCapacity,weapon.ReloadSeconds,weapon.EmptyReloadSeconds,weapon.ReserveCapacity,weapon.RoundsPerMinute,weapon.PelletsPerShot);
   weapon.MagazineCapacity=Math.Clamp(baseline.Magazine+magazine,1,10000);weapon.ReloadSeconds=baseline.Reload/reload;weapon.EmptyReloadSeconds=baseline.EmptyReload/reload;weapon.ReserveCapacity=Math.Clamp((int)((baseline.Reserve+Stat("SmallAmmoCap"))*(1+Stat("AmmoCapacityPct")/100)),12,100000);weapon.RoundsPerMinute=baseline.Rpm*(1+Stat("PistolFireRatePct")/100);weapon.PelletsPerShot=Math.Clamp(baseline.Pellets+(int)Stat("PistolPellets"),1,16);weapon.DamagePerPellet+=Stat("PistolDamageFlat");
  }
  if(player?.GetComponent<DashAbility3D>() is {} dash){_baseDashCooldown??=dash.Cooldown;dash.Cooldown=_baseDashCooldown.Value*(1-Math.Clamp(owned.Sum(n=>n.DashCooldownReduction),0,.8f));}
  if(player?.GetComponent<HealthComponent>() is {} hp){_baseHealth??=hp.MaxHealth;float fraction=hp.HealthPercent;hp.MaxHealth=_baseHealth.Value+Sum(CompanyUpgradeEffect.Health)+Stat("PlayerMaxHealth");hp.CurrentHealth=hp.MaxHealth*fraction;}
  if(player?.GetComponent<ResourceMeter3D>() is {} energy){_baseEnergy??=energy.Capacity;float fraction=energy.Fraction;energy.Capacity=_baseEnergy.Value+Sum(CompanyUpgradeEffect.Energy);energy.SetAmount(energy.Capacity*fraction);}
 }
 string PathName{get{if(Path.GetFileName(SaveSlot)!=SaveSlot||string.IsNullOrWhiteSpace(SaveSlot))throw new InvalidDataException("Workshop save must be a filename.");return Path.Combine(GameSaveStorage.GetDirectory(GameObject.Scene?.ProjectRoot??throw new InvalidDataException("No project save context.")),SaveSlot);}}
 public void Save(){if(!PersistProgress||!initialized)return;try{Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);File.WriteAllText(PathName+".tmp",JsonSerializer.Serialize(new State{Output=OutputLevel,Storage=StorageLevel,Efficiency=EfficiencyLevel,Ammo=StoredAmmo,Price=PriceMultiplier,Seed=MarketSeed,Samples=PriceSamples,Clock=PriceClock,History=_history.ToArray(),Purchased=_purchased.ToArray(),Ranks=_ranks}));File.Move(PathName+".tmp",PathName,true);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){LastError=e.Message;}}
 public sealed class State{public Dictionary<string,int>? Ranks{get;set;}public int Version{get;set;}=1;public int Output{get;set;}public int Storage{get;set;}public int Efficiency{get;set;}public float Ammo{get;set;}public float Price{get;set;}=1;public uint Seed{get;set;}=247319;public int Samples{get;set;}public float Clock{get;set;}public float[]? History{get;set;}public string[]? Purchased{get;set;}}
}
