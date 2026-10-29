using System.ComponentModel;
using System.Text.Json;
using ByteEngine.Core.AI;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Navigation;
namespace KarlsonDemo.Plugin;
public sealed class MiningContract
{
 public string Id{get;set;}="survey";
 public string Name{get;set;}="Shallow survey";
 public string Difficulty{get;set;}="LOW RISK";
 public string Description{get;set;}="A small incursion. A reliable place to prepare.";
 public float EnemyMultiplier{get;set;}=1;
 public float OreMultiplier{get;set;}=1;
 public float SpawnIntervalMultiplier{get;set;}=1;
 public float SpeedMultiplier{get;set;}=1;
 public float DamageMultiplier{get;set;}=1;
 public float HealthMultiplier{get;set;}=1;
 public float HunterFraction{get;set;}=.34f;
}
public sealed record MiningWavePreview(string Id,string Name,string Difficulty,string Description,int Raiders,int Hunters,int Ore,float SpawnSeconds,float SpeedMultiplier,float DamageMultiplier)
{public int Total=>Raiders+Hunters;public float RaiderHealth{get;init;}public float HunterHealth{get;init;}public float RaiderSpeed{get;init;}public float HunterSpeed{get;init;}}
public sealed partial class MiningOperation3D
{
 [Category("Contracts"),Description("Three editable contracts. Enemy mix, yield and pressure are previewed from these exact rules before launch.")]
 public string ContractsJson{get;set;}=JsonSerializer.Serialize(new MiningContract[]{new(),new(){Id="deep",Name="Deep vein",Difficulty="HIGH RISK",Description="More hunters and a shorter gap between arrivals.",EnemyMultiplier=1.5f,OreMultiplier=1.7f,HunterFraction=.45f,SpawnIntervalMultiplier=.78f,SpeedMultiplier=1.06f,DamageMultiplier=1.1f,HealthMultiplier=1.2f},new(){Id="overdrive",Name="Overdrive",Difficulty="SEVERE",Description="Heavy pressure on both you and the drill. Prepare defences.",EnemyMultiplier=2.1f,OreMultiplier=2.5f,HunterFraction=.55f,SpawnIntervalMultiplier=.58f,SpeedMultiplier=1.12f,DamageMultiplier=1.2f,HealthMultiplier=1.45f}});
 [Category("Contracts")] public string SelectedContractId{get;set;}="survey";
 [Category("Enemy pressure")] public float HunterMoveSpeed{get;set;}=5f;
 [Category("Enemy pressure")] public float RaiderMoveSpeed{get;set;}=3.8f;
 [Category("Enemy pressure")] public float HunterHealth{get;set;}=50;
 [Category("Enemy pressure")] public float RaiderHealth{get;set;}=75;
 [Category("Enemy pressure")] public float HunterDamage{get;set;}=6;
 [Category("Enemy pressure")] public float RaiderDamage{get;set;}=6;
 [Category("Enemy pressure")] public int HunterAttackSlots{get;set;}=3;
 [Category("Enemy pressure")] public int RaiderAttackSlots{get;set;}=3;
 [Category("Enemy pressure")] public float HealthGrowthPerOperation{get;set;}=.06f;
 [Category("Enemy pressure")] public float MaximumHealthGrowth{get;set;}=2;
 [Category("Enemy pressure")] public float HunterWindup{get;set;}=.38f;
 [Category("Enemy pressure")] public float HunterRecovery{get;set;}=.42f;
 [Category("Enemy pressure")] public float HunterAttackCooldown{get;set;}=.9f;
 [Category("Enemy pressure")] public float RaiderWindup{get;set;}=.65f;
 [Category("Enemy pressure")] public float RaiderRecovery{get;set;}=.6f;
 [Category("Enemy pressure")] public float RaiderAttackCooldown{get;set;}=1.1f;
 [Category("Enemy pressure")] public bool RaiderDodging{get;set;}=true;
 [Category("Enemy pressure")] public float RaiderDodgeChance{get;set;}=.3f;
 [Category("Enemy pressure")] public float RaiderDodgeCooldown{get;set;}=4.5f;
 [Category("Enemy pressure")] public float RaiderDodgeReaction{get;set;}=.18f;
 [Category("Live state")] public MiningWavePreview? ActiveWave{get;private set;}
 public IReadOnlyList<MiningContract> Contracts
 {
  get{if(parsedContractsJson==ContractsJson)return parsedContracts;parsedContractsJson=ContractsJson;try{var list=JsonSerializer.Deserialize<MiningContract[]>(ContractsJson)??[];return parsedContracts=list.Length==3&&list.All(c=>c!=null)&&list.Select(c=>c.Id).Distinct().Count()==3&&list.All(c=>c!=null&&!string.IsNullOrWhiteSpace(c.Id)&&Valid(c.EnemyMultiplier,.1f,10)&&Valid(c.OreMultiplier,.1f,10)&&Valid(c.SpawnIntervalMultiplier,.1f,5)&&Valid(c.SpeedMultiplier,.1f,3)&&Valid(c.DamageMultiplier,.1f,5)&&Valid(c.HealthMultiplier,.1f,5)&&Valid(c.HunterFraction,0,1))?list:[];}catch(JsonException){return parsedContracts=[];}}
 }
 string? parsedContractsJson;IReadOnlyList<MiningContract> parsedContracts=[];
 static bool Valid(float n,float min,float max)=>float.IsFinite(n)&&n>=min&&n<=max;
 public MiningWavePreview? PreviewContract(int index)
 {
  var contracts=Contracts;if(index<0||index>=contracts.Count)return null;var c=contracts[index];
  int total=(int)Math.Clamp(Math.Ceiling(((long)Math.Max(1,BaseEnemies)+(long)CompletedRounds*Math.Max(0,ExtraEnemiesPerRound)+Math.Max(0,HazardContractLevel)*2)*c.EnemyMultiplier),1,Math.Clamp(MaximumEnemiesPerRound,1,200));
  int hunters=(int)Math.Clamp(Math.Round(total*c.HunterFraction,MidpointRounding.AwayFromZero),0,total);
  int ore=(int)Math.Clamp(Math.Floor(((long)BaseOrePerOperation+(long)DrillLevel*OrePerDrillLevel)*(1d+PermanentExtractionBonus+HazardContractLevel*.15f)*c.OreMultiplier),1,100000);
  float healthScale=Math.Clamp(1+CompletedRounds*Math.Max(0,HealthGrowthPerOperation),1,Math.Max(1,MaximumHealthGrowth))*c.HealthMultiplier;
  return new(c.Id,c.Name,c.Difficulty,c.Description,total-hunters,hunters,ore,Math.Max(.05f,SpawnInterval*c.SpawnIntervalMultiplier),c.SpeedMultiplier,c.DamageMultiplier){HunterHealth=healthScale*Math.Clamp(float.IsFinite(HunterHealth)?HunterHealth:50,1,10000),RaiderHealth=healthScale*Math.Clamp(float.IsFinite(RaiderHealth)?RaiderHealth:75,1,10000),HunterSpeed=Math.Clamp(HunterMoveSpeed,.1f,12)*c.SpeedMultiplier,RaiderSpeed=Math.Clamp(RaiderMoveSpeed,.1f,12)*c.SpeedMultiplier};
 }
 public bool SelectContract(int index){if(Phase!=MiningPhase.Preparation||index<0||index>=Contracts.Count)return false;SelectedContractId=Contracts[index].Id;return true;}
 public MiningWavePreview? SelectedContract=>PreviewContract(Contracts.ToList().FindIndex(c=>c.Id==SelectedContractId));
 public static bool IsHunterAt(MiningWavePreview wave,int index)=>index>=0&&index<wave.Total&&(index+1)*wave.Hunters/wave.Total>index*wave.Hunters/wave.Total;
 public void ConfigureEnemy(ByteEngine.Core.Scene.GameObject enemy,bool hunter)
 {
  var ai=enemy.GetComponent<AIBehavior3D>();var combat=enemy.GetComponent<SimpleEnemyAI3D>();if(ai==null||combat==null)return;
  float baseSpeed=hunter?HunterMoveSpeed:RaiderMoveSpeed;float speed=ActiveWave is {} forecast?(hunter?forecast.HunterSpeed:forecast.RaiderSpeed):Math.Clamp(float.IsFinite(baseSpeed)?baseSpeed:3,.1f,12);
  if(enemy.GetComponent<HealthComponent>() is {} health){health.MaxHealth=ActiveWave is {} active?(hunter?active.HunterHealth:active.RaiderHealth):(hunter?HunterHealth:RaiderHealth);health.Revive();}
  ai.MovementSpeedMultiplier=speed/Math.Max(.1f,combat.MoveSpeed);combat.MoveSpeed=speed;
  if(enemy.GetComponent<NavigationAgent3D>() is {} agent)agent.Speed=speed;
  combat.WindupSeconds=Math.Clamp(hunter?HunterWindup:RaiderWindup,.2f,5);combat.RecoverySeconds=Math.Clamp(hunter?HunterRecovery:RaiderRecovery,.2f,5);combat.AttackCooldown=Math.Clamp(hunter?HunterAttackCooldown:RaiderAttackCooldown,.3f,10);
  combat.Damage=Math.Clamp(hunter?HunterDamage:RaiderDamage,0,1000)*(ActiveWave?.DamageMultiplier??1);ai.DecisionInterval=.08f;ai.MaximumAttackers=Math.Clamp(hunter?HunterAttackSlots:RaiderAttackSlots,1,12);
  if(!hunter&&RaiderDodging){combat.DodgeEnabled=true;combat.DodgeCooldown=Math.Clamp(RaiderDodgeCooldown,2,15);combat.DodgeSpeed=5;combat.DodgeSeconds=.18f;var evade=enemy.GetComponent<MiningEnemyEvasion3D>()??enemy.AddComponent(new MiningEnemyEvasion3D{PlayerObjectId=PlayerObjectId,Chance=Math.Clamp(RaiderDodgeChance,0,1),Cooldown=combat.DodgeCooldown,ReactionSeconds=Math.Clamp(RaiderDodgeReaction,.1f,.5f)});}
 }
}
