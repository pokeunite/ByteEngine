using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningTrade3D
{
 public int ResearchRank(string id)=>_ranks.TryGetValue(id,out int rank)?rank:_purchased.Contains(id)?1:0;
 public int ResearchCost(string id){var n=_research.FirstOrDefault(n=>n.Id==id);if(n==null)return int.MaxValue;int r=ResearchRank(id);return n.RankCosts.Length>0?n.RankCosts[Math.Min(r,n.RankCosts.Length-1)]:n.Cost;}
 public float Stat(string name)=>_research.Where(n=>n.Stat==name).Sum(n=>n.Amount*ResearchRank(n.Id));
 float? baseMaximumPrice;
 void ApplyExtendedResearch()
 {
  if(Operation is not {} op)return;
  op.BaseDrillHealthResearchMultiplier=1+Stat("BunkerHealthPct")/100;
  op.HazardContractLevel=(int)Stat("SirenLevel");
  baseMaximumPrice??=MaximumMultiplier;MaximumMultiplier=baseMaximumPrice.Value*(1+Stat("MarketPeakPct")/100);
  var player=GameObject.Scene?.FindGameObject(op.PlayerObjectId);
  if(player?.GetComponent<CharacterController3D>() is {} move)move.MoveSpeedMultiplier=1+Stat("MoveSpeedPct")/100;
 }
}
