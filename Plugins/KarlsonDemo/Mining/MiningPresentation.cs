using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using Component=ByteEngine.Core.Scene.Component;
namespace KarlsonDemo.Plugin;
public sealed class MiningRigVisual3D:Component
{
 [SceneObjectReference] public Guid OperationObjectId {get;set;}
 [SceneObjectReference] public Guid SpinningPartObjectId {get;set;}
 public float RotationDegreesPerSecond {get;set;}=140;
 protected override void OnUpdate()
 {if(GameObject.Scene?.FindGameObject(OperationObjectId)?.GetComponent<MiningOperation3D>() is {Phase:MiningPhase.Drilling}&&GameObject.Scene.FindGameObject(SpinningPartObjectId) is {} bit)bit.Transform.EulerAngles+=new Vector3(0,RotationDegreesPerSecond*(float)Time.DeltaTime,0);}
}
/// <summary>Presentation only; Event Sheets own every button/key binding.</summary>
public sealed class MiningHud3D:Component
{
 [SceneObjectReference] public Guid OperationObjectId {get;set;}
 [SceneObjectReference] public Guid StatusObjectId {get;set;}
 [SceneObjectReference] public Guid WalletObjectId {get;set;}
 [SceneObjectReference] public Guid MessageObjectId {get;set;}
 [SceneObjectReference] public Guid MessagePanelObjectId {get;set;}
 [SceneObjectReference] public Guid ExtractionBarObjectId {get;set;}
 [SceneObjectReference] public Guid ShopPanelObjectId {get;set;}
 [SceneObjectReference] public Guid StartButtonObjectId {get;set;}
 [SceneObjectReference] public Guid RefineButtonObjectId {get;set;}
 [SceneObjectReference] public Guid SellButtonObjectId {get;set;}
 [SceneObjectReference] public Guid DrillButtonObjectId {get;set;}
 [SceneObjectReference] public Guid AmmoButtonObjectId {get;set;}
 [SceneObjectReference] public Guid RefineryButtonObjectId {get;set;}
 [SceneObjectReference] public Guid ResearchButtonObjectId {get;set;}
 float _refresh;
 protected override void OnUpdate(){_refresh-=(float)Time.DeltaTime;if(_refresh<=0){_refresh=.08f;Refresh();}}
 public void Refresh()
 {
  var s=GameObject.Scene;if(s?.FindGameObject(OperationObjectId)?.GetComponent<MiningOperation3D>() is not {} op)return;
  void Text(Guid id,string value){if(s.FindGameObject(id)?.GetComponent<UiText>() is {} t)t.Text=value;}
  UiWidget? Button(Guid id)=>s.FindGameObject(id)?.GetComponent<UiWidget>();
  bool prep=op.Phase==MiningPhase.Preparation;
  Text(StatusObjectId,op.Phase==MiningPhase.Drilling?$"OPERATION {op.CompletedRounds+1}  |  ORE {(int)(op.RoundQuota*op.ExtractionProgress)}/{op.RoundQuota}\nDrill {op.DrillHealthFraction*100:0}%  /  {op.AliveEnemies} active  /  {op.EnemiesScheduled-op.EnemiesSpawned} incoming":op.Phase==MiningPhase.Failed?"OPERATION FAILED  |  RECOVER YOUR SITE":$"OPERATION {op.CompletedRounds+1}  |  PREPARATION\nChoose upgrades, then start the drill.");
  Text(WalletObjectId,$"{op.Credits:N0} CREDITS\nORE {op.RawOre}   |   REFINED {op.RefinedOre}");Text(MessageObjectId,op.Message);
  if(s.FindGameObject(ExtractionBarObjectId)?.GetComponent<UiWidget>() is {} bar){bar.Maximum=1;bar.Value=op.ExtractionProgress;}
  if(s.FindGameObject(MessagePanelObjectId) is {} feedbackPanel)feedbackPanel.Active=op.Phase!=MiningPhase.Drilling;
  if(s.FindGameObject(ShopPanelObjectId) is {} panel)panel.Active=op.Phase!=MiningPhase.Drilling;
  if(Button(StartButtonObjectId) is {} start){start.Label=op.Phase==MiningPhase.Failed?"Recover site [E]":"Start drill [E]";start.Interactable=op.Phase==MiningPhase.Failed||op.RefiningAmount==0;}
  if(Button(RefineButtonObjectId) is {} refine){refine.Label=op.RefiningAmount>0?$"Refining {op.RefiningAmount}  |  {op.RefiningRemaining:0.0}s":$"Refine ore [F]  |  {op.RawOrePrice} -> {op.RefinedOrePrice} cr / unit";refine.Interactable=prep&&op.RawOre>0&&op.RefiningAmount==0;}
  if(Button(SellButtonObjectId) is {} sell){sell.Label="Sell available cargo [X]";sell.Interactable=prep&&(op.RawOre>op.RefiningAmount||op.RefinedOre>0);}
  void Upgrade(Guid id,MiningUpgrade kind,string name,string benefit)
  {if(Button(id) is not {} b)return;bool max=op.Level(kind)>=op.MaximumUpgradeLevel;b.Label=max?$"{name}  L{op.Level(kind)}  |  MAX":$"{name}  L{op.Level(kind)} -> {op.Level(kind)+1}  |  {op.Cost(kind)} cr\n{benefit}";b.Interactable=prep&&!max&&op.Credits>=op.Cost(kind);}
  Upgrade(DrillButtonObjectId,MiningUpgrade.Drill,"Drill",$"+{op.OrePerDrillLevel} ore, +{op.DrillHealthPerLevel:0} HP, faster");
  Upgrade(AmmoButtonObjectId,MiningUpgrade.AmmoPress,"Ammo press",$"+{op.ExtraReservePerLevel} reserve each operation");
  Upgrade(RefineryButtonObjectId,MiningUpgrade.Refinery,"Refinery",$"+{op.ExtraBatchPerLevel} batch size, faster processing");
  Upgrade(ResearchButtonObjectId,MiningUpgrade.CompanyResearch,"Company research",$"+{op.DamageBonusPerResearch*100:0}% weapon damage, permanent");
 }
}
