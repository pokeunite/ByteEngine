using System.ComponentModel;
using System.Numerics;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.AI;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;
using Component=ByteEngine.Core.Scene.Component;
namespace KarlsonDemo.Plugin;
public enum MiningPhase { Preparation, Drilling, Failed }
public enum MiningUpgrade { Drill, AmmoPress, Refinery, CompanyResearch, RefinerySpeed, RefineryYield }
public sealed class MiningSpawnPoint3D:Component
{
 [SceneObjectReference] public Guid OperationObjectId {get;set;}
 public bool Available {get;set;}=true;
}
/// <summary>Editable finite extraction/wave/economy rules. Input and purchase bindings live in Event Sheets.</summary>
public sealed partial class MiningOperation3D:Component
{
 [Category("Bindings"),SceneObjectReference] public Guid PlayerObjectId {get;set;}
 [Category("Bindings"),SceneObjectReference] public Guid DrillObjectId {get;set;}
 [Category("Enemies")] public AssetReference RaiderBlueprint {get;set;}=AssetReference.Empty;
 [Category("Enemies")] public AssetReference HunterBlueprint {get;set;}=AssetReference.Empty;
 [Category("Extraction")] public int BaseOrePerOperation {get;set;}=18;
 [Category("Extraction")] public int OrePerDrillLevel {get;set;}=7;
 [Category("Extraction")] public float ExtractionSeconds {get;set;}=16;
 [Category("Extraction")] public float DrillSpeedPerLevel {get;set;}=.12f;
 [Category("Extraction")] public float BaseDrillHealth {get;set;}=180;
 [Category("Extraction")] public float DrillHealthPerLevel {get;set;}=30;
 [Category("Enemies")] public int BaseEnemies {get;set;}=6;
 [Category("Enemies")] public int ExtraEnemiesPerRound {get;set;}=2;
 [Category("Enemies")] public int MaximumEnemiesPerRound {get;set;}=24;
 [Category("Enemies")] public int MaximumAlive {get;set;}=12;
 [Browsable(false)] public int HunterEvery {get;set;}=3;
 [Category("Enemies")] public float SpawnInterval {get;set;}=.9f;
 [Category("Economy")] public int StartingCredits {get;set;}=20;
 [Category("Economy")] public int RawOrePrice {get;set;}=2;
 [Category("Economy")] public int RefinedOrePrice {get;set;}=5;
 [Category("Refinery")] public int RefineryBatch {get;set;}=20;
 [Category("Refinery")] public int ExtraBatchPerLevel {get;set;}=12;
 [Category("Refinery")] public float RefinerySeconds {get;set;}=3;
 [Category("Refinery")] public float RefinerySpeedPerLevel {get;set;}=.2f;
 [Category("Refinery")] public float RefineryYieldPerLevel {get;set;}=.1f;
 [Category("Ammo press")] public int BaseReservePerRound {get;set;}=60;
  [Category("Ammo press")] public bool FreeAmmoEveryOperation {get;set;}
 [Category("Ammo press")] public int ExtraReservePerLevel {get;set;}=24;
 [Category("Upgrade costs")] public int DrillBaseCost {get;set;}=40;
 [Category("Upgrade costs")] public int AmmoPressBaseCost {get;set;}=35;
 [Category("Upgrade costs")] public int RefineryBaseCost {get;set;}=30;
 [Category("Upgrade costs")] public int ResearchBaseCost {get;set;}=90;
 [Category("Upgrade costs")] public int RefinerySpeedBaseCost {get;set;}=40;
 [Category("Upgrade costs")] public int RefineryYieldBaseCost {get;set;}=55;
 [Category("Upgrade costs")] public float UpgradeCostGrowth {get;set;}=1.65f;
 [Category("Upgrade costs")] public int MaximumUpgradeLevel {get;set;}=5;
 [Category("Company research")] public float DamageBonusPerResearch {get;set;}=.1f;
 [Category("Persistence")] public bool PersistProgress {get;set;}=true;
 [Category("Persistence")] public string SaveSlot {get;set;}="mining-business-v1.json";
 [Category("Live state")] public MiningPhase Phase {get;private set;}
 [Category("Live state")] public int Credits {get;private set;}
 [Category("Live state")] public int RawOre {get;private set;}
 [Category("Live state")] public int RefinedOre {get;private set;}
 [Category("Live state")] public int CompletedRounds {get;private set;}
 [Category("Live state")] public int DrillLevel {get;private set;}
 [Category("Live state")] public int AmmoPressLevel {get;private set;}
 [Category("Live state")] public int RefineryLevel {get;private set;}
 [Category("Live state")] public int CompanyLevel {get;private set;}
 [Category("Live state")] public int RefinerySpeedLevel {get;private set;}
 [Category("Live state")] public int RefineryYieldLevel {get;private set;}
 [Category("Live state")] public int RefiningOutput {get;private set;}
 public int BaseCargoCapacity{get;set;}=180;
 public int CargoCapacity=>Math.Clamp(BaseCargoCapacity+(int)(GameObject.GetComponent<MiningTrade3D>()?.Stat("OilTankCapacity")??0),1,1000000);
 [Category("Live state")] public int CargoUsed=>(int)Math.Clamp((long)RawOre+RefinedOre,0,int.MaxValue);
 [Category("Live state")] public int CargoReservedSpace=>Phase==MiningPhase.Drilling?RoundQuota:RefiningAmount>0?Math.Max(0,RefiningOutput-RefiningAmount):0;
 [Category("Live state")] public float CargoFillFraction=>Math.Clamp((float)CargoUsed/CargoCapacity,0,1);
 [Category("Live state")] public bool CargoFull=>(long)CargoUsed+CargoReservedSpace>=CargoCapacity;
 public float BaseDrillHealthResearchMultiplier{get;set;}=1;
 public int HazardContractLevel{get;set;}
 public float PermanentDamageBonus {get;private set;}
 public float PermanentExtractionBonus {get;private set;}
 public float PermanentRefineryBonus {get;private set;}
 public float RefineryDuration=>Math.Max(.1f,RefinerySeconds)/(1+Math.Max(0,RefinerySpeedPerLevel)*RefinerySpeedLevel);
  public float RefiningTotalSeconds{get;private set;}
  public float RefiningProgress=>RefiningAmount>0?Math.Clamp(1-RefiningRemaining/Math.Max(.1f,RefiningTotalSeconds),0,1):0;
 public float RefineryYieldMultiplier=>1+Math.Max(0,RefineryYieldPerLevel)*RefineryYieldLevel+PermanentRefineryBonus;
 public void SetPermanentBonuses(float damage,float extraction,float refinery){PermanentDamageBonus=Math.Clamp(damage,0,10);PermanentExtractionBonus=Math.Clamp(extraction,0,10);PermanentRefineryBonus=Math.Clamp(refinery,0,10);ApplyResearch();}
 [Category("Live state")] public int RoundQuota {get;private set;}
 [Category("Live state")] public float ExtractionProgress {get;private set;}
 [Category("Live state")] public int EnemiesScheduled {get;private set;}
 [Category("Live state")] public int EnemiesSpawned {get;private set;}
 [Category("Live state")] public int AliveEnemies=>_enemies.Count(o=>o.Scene==GameObject.Scene&&o.GetComponent<HealthComponent>() is {IsDead:false});
 [Category("Live state")] public string Message {get;private set;}="Start the drill to begin operation 1.";
 [Category("Live state")] public float RefiningRemaining {get;private set;}
 [Category("Live state")] public int RefiningAmount {get;private set;}
 [Browsable(false)] public float DrillHealthFraction=>Drill?.HealthPercent??0;
 [Browsable(false)] public IReadOnlyList<GameObject> SpawnedEnemies=>_roamingEnemies.Count==0?_enemies:_enemies.Concat(_roamingEnemies).ToArray();
 [Category("Live state")] public int RoamingEnemiesAlive=>_roamingEnemies.Count(o=>o.Scene==AttachedGameObject?.Scene&&o.GetComponent<HealthComponent>() is {IsDead:false});
 readonly List<GameObject> _roamingEnemies=[];
 public void TrackRoamingEnemy(GameObject enemy){if(enemy.Scene==AttachedGameObject?.Scene&&!_roamingEnemies.Contains(enemy))_roamingEnemies.Add(enemy);}
 readonly List<GameObject> _enemies=[];readonly Dictionary<WeaponDefinition3D,float> _baseDamage=[];
 float _spawnClock;bool _initialized;int _pointCursor;
 GameObject? Player=>GameObject.Scene?.FindGameObject(PlayerObjectId);
 HealthComponent? Drill=>GameObject.Scene?.FindGameObject(DrillObjectId)?.GetComponent<HealthComponent>();
 protected override void OnStart()=>Initialize();
 protected override void OnUpdate()=>Tick((float)Time.DeltaTime);
 protected override void OnStop()=>CleanupEnemies(true);
 public void Initialize()
 {
  if(_initialized)return;_initialized=true;Credits=Math.Max(0,StartingCredits);if(PersistProgress)LoadProgress();
  ApplyResearch();RecoverActors();Phase=MiningPhase.Preparation;
  Message=CompletedRounds==0?"Ready: start drilling, protect the drill, then sell your haul.":"Company progress loaded. Ready for the next operation.";
 }
 public int Level(MiningUpgrade kind)=>kind switch{MiningUpgrade.Drill=>DrillLevel,MiningUpgrade.AmmoPress=>AmmoPressLevel,MiningUpgrade.Refinery=>RefineryLevel,MiningUpgrade.RefinerySpeed=>RefinerySpeedLevel,MiningUpgrade.RefineryYield=>RefineryYieldLevel,_=>CompanyLevel};
 public int Cost(MiningUpgrade kind)
 {
  int basis=kind switch{MiningUpgrade.Drill=>DrillBaseCost,MiningUpgrade.AmmoPress=>AmmoPressBaseCost,MiningUpgrade.Refinery=>RefineryBaseCost,MiningUpgrade.RefinerySpeed=>RefinerySpeedBaseCost,MiningUpgrade.RefineryYield=>RefineryYieldBaseCost,_=>ResearchBaseCost};
  return (int)Math.Clamp(Math.Ceiling(Math.Max(1,basis)*Math.Pow(Math.Clamp(UpgradeCostGrowth,1,3),Level(kind))),1,1_000_000_000);
 }
 public bool StartOperation()
 {
  Initialize();if(Phase!=MiningPhase.Preparation)return Reject("Finish or retry the current operation first.");
  if(RefiningAmount>0)return Reject("Wait for the refinery to finish before launching.");
  var points=SpawnPoints();if(Player?.GetComponent<HealthComponent>()==null||Drill==null||points.Length==0||RaiderBlueprint.IsEmpty||HunterBlueprint.IsEmpty||!RuntimeSpawnService.IsBlueprintSpawnerConfigured)return Reject("Setup incomplete: check player, drill, alien blueprints and spawn points.");
  var preview=SelectedContract;if(preview==null)return Reject("Choose a valid extraction contract. Check Contracts JSON.");
  if((long)RawOre+RefinedOre+preview.Ore>CargoCapacity)return Reject("Cargo hold is full. Sell ore or upgrade storage.");
  CleanupEnemies();RecoverActors();RestockAmmo();
  ActiveWave=preview;RoundQuota=preview.Ore;EnemiesScheduled=preview.Total;
  EnemiesSpawned=0;ExtractionProgress=0;_spawnClock=0;Phase=MiningPhase.Drilling;Message="Drill running. Protect it and clear every attacker.";return true;
 }
 public void Tick(float delta)
 {
  if(!float.IsFinite(delta)||delta<=0||AttachedGameObject?.Scene==null)return;Initialize();
  _roamingEnemies.RemoveAll(o=>o.Scene!=GameObject.Scene);
  if(RoamingEnemiesAlive>0&&Player?.GetComponent<HealthComponent>()?.IsDead==true){Fail("You disturbed a spider nest. Operator down; company upgrades kept.");return;}
  if(Phase==MiningPhase.Drilling)
  {
   if(Drill?.IsDead!=false){Fail("Drill destroyed. Unbanked ore lost; company upgrades kept.");return;}
   if(Player?.GetComponent<HealthComponent>()?.IsDead!=false){Fail("Operator down. Unbanked ore lost; company upgrades kept.");return;}
   ExtractionProgress=Math.Min(1,ExtractionProgress+delta*(1+DrillLevel*Math.Max(0,DrillSpeedPerLevel))/Math.Max(1,ExtractionSeconds));
   _spawnClock-=delta;
   if(EnemiesSpawned<EnemiesScheduled&&AliveEnemies<Math.Clamp(MaximumAlive,1,100)&&_spawnClock<=0)
   {
    var points=SpawnPoints();if(points.Length==0){Fail("All spawn points are unavailable.");return;}
    bool hunter=ActiveWave!=null&&IsHunterAt(ActiveWave,EnemiesSpawned);var point=points[_pointCursor++%points.Length];
    var enemy=RuntimeSpawnService.SpawnBlueprint(GameObject.Scene!,hunter?HunterBlueprint:RaiderBlueprint,point.Transform.WorldPosition);
    if(enemy==null){Fail("Enemy blueprint failed to spawn; no resources awarded.");return;}
    var ai=enemy.GetComponent<AIBehavior3D>();var combat=enemy.GetComponent<SimpleEnemyAI3D>();
    if(ai==null||combat==null||enemy.GetComponent<HealthComponent>()==null){GameObject.Scene!.DestroyGameObject(enemy);Fail("Alien blueprint requires health, behaviour and combat components.");return;}
    ai.TargetObjectId=hunter?PlayerObjectId:DrillObjectId;ai.FindNearestCharacter=false;combat.TargetId=ai.TargetObjectId;ConfigureEnemy(enemy,hunter);
    _enemies.Add(enemy);EnemiesSpawned++;_spawnClock=ActiveWave?.SpawnSeconds??Math.Max(.05f,SpawnInterval);
   }
   if(ExtractionProgress>=1&&EnemiesSpawned==EnemiesScheduled&&AliveEnemies==0)
   {RawOre=Add(RawOre,RoundQuota);CompletedRounds++;Phase=MiningPhase.Preparation;Message=$"Operation complete: +{RoundQuota} ore. Refine or sell, then reinvest.";CleanupEnemies();SaveProgress();}
  }
  else if(Phase==MiningPhase.Preparation&&RefiningAmount>0)
  {
   RefiningRemaining=Math.Max(0,RefiningRemaining-delta);
   if(RefiningRemaining<=0){RawOre-=RefiningAmount;RefinedOre=Add(RefinedOre,RefiningOutput);Message=$"Refinery finished: {RefiningOutput} refined ore ready to sell.";RefiningAmount=0;SaveProgress();}
  }
 }
 MiningSpawnPoint3D[] SpawnPoints()=>GameObject.Scene!.GameObjects.Where(o=>o.ActiveInHierarchy).Select(o=>o.GetComponent<MiningSpawnPoint3D>()).OfType<MiningSpawnPoint3D>().Where(p=>p.Enabled&&p.Available&&p.OperationObjectId==GameObject.Id).ToArray();
 public bool BeginRefining()
 {
  Initialize();if(Phase!=MiningPhase.Preparation||RefiningAmount>0)return Reject("Refine only between operations, one batch at a time.");
  var shipping=GameObject.GetComponent<MiningTrade3D>();int available=RawOre-(shipping is {SaleInProgress:true,SaleRefined:false}?shipping.SaleQuantity-shipping.SaleProcessed:0);if(available<=0)return Reject("No unreserved raw ore to refine.");
  int input=Math.Min(available,Math.Clamp(RefineryBatch+RefineryLevel*ExtraBatchPerLevel,1,100000));if((long)RawOre+RefinedOre-input+(int)(input*RefineryYieldMultiplier)>CargoCapacity)return Reject("Make room in the cargo hold before refining.");
  RefiningAmount=Math.Min(available,Math.Clamp(RefineryBatch+RefineryLevel*ExtraBatchPerLevel,1,100000));RefiningOutput=(int)Math.Clamp(Math.Floor(RefiningAmount*RefineryYieldMultiplier),0,1000000000);RefiningRemaining=RefiningTotalSeconds=RefineryDuration;Message=$"Refining {RefiningAmount} ore...";return true;
 }
 public bool SellCargo()
 {
  Initialize();if(Phase!=MiningPhase.Preparation)return Reject("Trading is available between operations.");
  int availableRaw=Math.Max(0,RawOre-RefiningAmount);if(availableRaw==0&&RefinedOre==0)return Reject(RefiningAmount>0?"Ore in the refinery is reserved. Wait for the batch.":"No cargo to sell.");
  long income=(long)availableRaw*Math.Max(0,RawOrePrice)+(long)RefinedOre*Math.Max(0,RefinedOrePrice);
  Credits=Add(Credits,income);RawOre-=availableRaw;RefinedOre=0;Message=$"Cargo sold for {income} credits.";SaveProgress();return true;
 }
 public bool SpendCredits(int amount)
 {Initialize();if(Phase!=MiningPhase.Preparation||amount<0||Credits<amount)return false;Credits-=amount;SaveProgress();return true;}
 public bool SellUnits(bool refined,int quantity,float quote,int processedBefore=0)=>SellUnitsCore(refined,quantity,quote,processedBefore,false);
 internal bool ProcessShipmentUnits(bool refined,int quantity,float quote,int processedBefore)=>SellUnitsCore(refined,quantity,quote,processedBefore,true);
 bool SellUnitsCore(bool refined,int quantity,float quote,int processedBefore,bool shipment)
 {
  Initialize();if((!shipment&&Phase!=MiningPhase.Preparation)||quantity<=0||!float.IsFinite(quote)||quote<0)return false;
  int available=refined?RefinedOre:Math.Max(0,RawOre-RefiningAmount);if(quantity>available)return Reject("Not enough available cargo.");
  long income=SaleTotal(Math.Clamp(processedBefore,0,100000000)+quantity,quote)-SaleTotal(Math.Clamp(processedBefore,0,100000000),quote);
  if(refined)RefinedOre-=quantity;else RawOre-=quantity;Credits=Add(Credits,income);Message=$"Sold {quantity} units for {income:N0} credits.";SaveProgress();return true;
 }
 public static int SaleTotal(int quantity,float quote)=>quantity<=0||!float.IsFinite(quote)||quote<0?0:(int)Math.Clamp(decimal.Floor(quantity*decimal.Round((decimal)quote,2,MidpointRounding.AwayFromZero)),0,1000000000);
 public bool Buy(MiningUpgrade kind)
 {
  Initialize();if(!Enum.IsDefined(kind)||Phase!=MiningPhase.Preparation)return Reject("Buy upgrades between operations.");
  if(Level(kind)>=Math.Clamp(MaximumUpgradeLevel,1,50))return Reject("Upgrade is already at its maximum level.");
  int cost=Cost(kind);if(Credits<cost)return Reject($"Need {cost} credits for this upgrade.");Credits-=cost;
  switch(kind){case MiningUpgrade.Drill:DrillLevel++;break;case MiningUpgrade.AmmoPress:AmmoPressLevel++;break;case MiningUpgrade.Refinery:RefineryLevel++;break;case MiningUpgrade.RefinerySpeed:RefinerySpeedLevel++;break;case MiningUpgrade.RefineryYield:RefineryYieldLevel++;break;default:CompanyLevel++;ApplyResearch();break;}
  Message=$"{kind} upgraded to level {Level(kind)}.";SaveProgress();return true;
 }
 public void Retry()
 {if(Phase!=MiningPhase.Failed)return;CleanupEnemies(true);RecoverActors();RestockAmmo();Phase=MiningPhase.Preparation;ExtractionProgress=0;Message="Site recovered. Company progress kept. Start the drill when ready.";}
 void RecoverActors(){Player?.GetComponent<HealthComponent>()?.Revive();if(Drill is {} h){h.MaxHealth=Math.Max(1,BaseDrillHealth*BaseDrillHealthResearchMultiplier+DrillLevel*DrillHealthPerLevel);h.Revive();}}
 void RestockAmmo()
 {
   if(!FreeAmmoEveryOperation)return;
  int allowance=Math.Clamp(BaseReservePerRound+AmmoPressLevel*ExtraReservePerLevel,0,100000);
  foreach(var weapon in GameObject.Scene!.GameObjects.Select(o=>o.GetComponent<WeaponInstance3D>()).OfType<WeaponInstance3D>().Where(w=>w.Equipped))
   if(weapon.Definition is {} def)weapon.SetAmmo(def.MagazineCapacity,Math.Max(weapon.ReserveAmmo,allowance));
 }
 void ApplyResearch()
 {
  var scene=AttachedGameObject?.Scene;if(scene==null)return;
  foreach(var def in scene.GameObjects.Select(o=>o.GetComponent<WeaponDefinition3D>()).OfType<WeaponDefinition3D>())
  {if(!_baseDamage.ContainsKey(def))_baseDamage[def]=def.DamagePerPellet;def.DamagePerPellet=_baseDamage[def]*(1+CompanyLevel*Math.Max(0,DamageBonusPerResearch)+PermanentDamageBonus);}
 }
 void Fail(string reason){Phase=MiningPhase.Failed;ExtractionProgress=0;Message=reason;CleanupEnemies(true);SaveProgress();}
 void CleanupEnemies(bool includeRoaming=false){foreach(var enemy in _enemies.ToArray())if(enemy.Scene==AttachedGameObject?.Scene&&enemy.Scene!=null)enemy.Scene.DestroyGameObject(enemy);_enemies.Clear();if(includeRoaming){foreach(var enemy in _roamingEnemies.ToArray())if(enemy.Scene==AttachedGameObject?.Scene&&enemy.Scene!=null)enemy.Scene.DestroyGameObject(enemy);_roamingEnemies.Clear();}}
 bool Reject(string reason){Message=reason;return false;}
 static int Add(int value,long amount)=>(int)Math.Clamp((long)value+amount,0,1_000_000_000);
 string SavePath
 {
  get{if(string.IsNullOrWhiteSpace(SaveSlot)||Path.GetFileName(SaveSlot)!=SaveSlot||SaveSlot.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new InvalidDataException("Save Slot must be a file name only.");return Path.Combine(GameSaveStorage.GetDirectory(GameObject.Scene?.ProjectRoot??throw new InvalidDataException("No project save context.")),SaveSlot);}
 }
 public void SaveProgress()
 {
  if(!PersistProgress)return;
  try{var path=SavePath;Directory.CreateDirectory(Path.GetDirectoryName(path)!);var data=new MiningSave{Credits=Credits,Raw=RawOre,Refined=RefinedOre,Rounds=CompletedRounds,Drill=DrillLevel,Ammo=AmmoPressLevel,Refinery=RefineryLevel,Company=CompanyLevel,RefinerySpeed=RefinerySpeedLevel,RefineryYield=RefineryYieldLevel};File.WriteAllText(path+".tmp",JsonSerializer.Serialize(data));File.Move(path+".tmp",path,true);}
  catch(Exception e)when(e is IOException or UnauthorizedAccessException or InvalidDataException){Message+=" Progress could not be saved: "+e.Message;}
 }
 void LoadProgress()
 {
  try{var path=SavePath;if(!File.Exists(path))return;var d=JsonSerializer.Deserialize<MiningSave>(File.ReadAllText(path));if(d?.Version!=1)throw new InvalidDataException("Unsupported company save.");Credits=Add(0,d.Credits);RawOre=Add(0,d.Raw);RefinedOre=Add(0,d.Refined);CompletedRounds=Math.Clamp(d.Rounds,0,100000);int max=Math.Clamp(MaximumUpgradeLevel,1,50);DrillLevel=Math.Clamp(d.Drill,0,max);AmmoPressLevel=Math.Clamp(d.Ammo,0,max);RefineryLevel=Math.Clamp(d.Refinery,0,max);CompanyLevel=Math.Clamp(d.Company,0,max);RefinerySpeedLevel=Math.Clamp(d.RefinerySpeed,0,max);RefineryYieldLevel=Math.Clamp(d.RefineryYield,0,max);}
  catch(Exception e)when(e is IOException or UnauthorizedAccessException or InvalidDataException or JsonException){Message="Company save could not be read: "+e.Message;}
 }
 public sealed class MiningSave{public int Version{get;set;}=1;public int Credits{get;set;}public int Raw{get;set;}public int Refined{get;set;}public int Rounds{get;set;}public int Drill{get;set;}public int Ammo{get;set;}public int Refinery{get;set;}public int Company{get;set;}public int RefinerySpeed{get;set;}public int RefineryYield{get;set;}}
}
