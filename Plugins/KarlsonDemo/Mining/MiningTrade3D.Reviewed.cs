using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;

namespace KarlsonDemo.Plugin;
public sealed partial class MiningTrade3D
{
 public bool HasReviewedCatalog=>_research.Any(n=>n.Id=="charter");
 public string ResearchRequirement(string id)
 {
  var n=_research.FirstOrDefault(n=>n.Id==id);if(n==null)return "Unknown research";
  if(!n.Available)return "COMING SOON - market prototype awaits approval";
  if(Operation is {} op&&op.CompletedRounds+1<n.MinimumRound)return $"AVAILABLE FROM OPERATION {n.MinimumRound}";
  return "";
 }
 public float CargoSalvageFraction=>Math.Clamp(Stat("CargoSalvagePct")/100,0,1);
 public int ExtractionBonus(bool pressure)=>(int)(Stat("ExtractionFlat")+(pressure?Stat("PressureExtractionFlat"):0));
 HealthComponent? reviewedPlayerHealth;float hitSlow;bool emergencyUsed;
 public void ResetReviewedOperation()=>emergencyUsed=false;
 public bool TryEmergencyDrillRepair()
 {
  if(Operation is not {} op||op.DrillHealthFraction>0||Stat("EmergencyDrillRepairPct")<=0||emergencyUsed)return false;
  var drill=GameObject.Scene?.FindGameObject(op.DrillObjectId)?.GetComponent<HealthComponent>();if(drill==null)return false;
  emergencyUsed=true;drill.Revive();drill.CurrentHealth=drill.MaxHealth*Math.Clamp(Stat("EmergencyDrillRepairPct")/100,0,1);return true;
 }
 float baseMovementMultiplier=1;bool applyingSlow;
 void TickReviewedResearch(float dt)
 {
  if(!HasReviewedCatalog||Operation is not {} op)return;
  if(ResearchRank("charter")==0){_purchased.Add("charter");_ranks["charter"]=1;ApplyResearch();}
  var player=GameObject.Scene?.FindGameObject(op.PlayerObjectId);if(player==null)return;
  if(reviewedPlayerHealth!=player.GetComponent<HealthComponent>())
  {if(reviewedPlayerHealth!=null)reviewedPlayerHealth.Damaged-=OnReviewedPlayerHit;reviewedPlayerHealth=player.GetComponent<HealthComponent>();if(reviewedPlayerHealth!=null)reviewedPlayerHealth.Damaged+=OnReviewedPlayerHit;}
  hitSlow=Math.Max(0,hitSlow-dt);
  if(player.GetComponent<CharacterController3D>() is {} motor)
  {if(!applyingSlow)baseMovementMultiplier=motor.MoveSpeedMultiplier;motor.MoveSpeedMultiplier=baseMovementMultiplier*(hitSlow>0?.8f:1);applyingSlow=hitSlow>0;}
  if(player.GetComponent<FpsGunLab3D>() is not {} equipment)return;
  foreach(var definition in _weaponBaselines.Keys)
  {
   ByteEngine.Core.Scene.GameObject rig;try{rig=definition.GameObject;}catch(InvalidOperationException){continue;}
   if(rig.Scene!=GameObject.Scene)continue;var instance=rig.GetComponent<WeaponInstance3D>();if(instance==null)continue;
   var d=instance.Definition;if(d==null)continue;
   bool licensed=d.RequiredResearchId.Length==0||HasResearch(d.RequiredResearchId);
   if(licensed&&d.RequiredResearchId.Length>0&&!equipment.Owns(instance)&&equipment.GiveWeapon(instance,false))instance.SetAmmo(d.MagazineCapacity,instance.ReserveAmmo);
  }
 }
 void OnReviewedPlayerHit(HealthComponent hp,float damage){if(damage>0)hitSlow=.3f*(1-Math.Clamp(Stat("HitSlowReductionPct")/100,0,.9f));}
 void StopReviewedResearch(){if(reviewedPlayerHealth!=null)reviewedPlayerHealth.Damaged-=OnReviewedPlayerHit;reviewedPlayerHealth=null;if(applyingSlow&&Operation is {} op&&GameObject.Scene?.FindGameObject(op.PlayerObjectId)?.GetComponent<CharacterController3D>() is {} motor)motor.MoveSpeedMultiplier=baseMovementMultiplier;applyingSlow=false;}
}
