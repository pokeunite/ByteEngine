namespace KarlsonDemo.Plugin;
public sealed partial class MiningTrade3D
{
 // Old bundles become equivalent reload, magazine and accuracy ranks, once.
 public static void MigrateWeaponUpgradeState(State state,ISet<string>? availableIds=null){var purchased=(state.Purchased??[]).ToHashSet();state.Ranks??=[];foreach(string family in new[]{"pistol","rifle","shotgun"}){if(availableIds!=null&&!new[]{"reload","magazine","accuracy"}.All(k=>availableIds.Contains(family+"-"+k)))continue;string old=family+"-handling";int rank=state.Ranks.TryGetValue(old,out int r)?r:purchased.Contains(old)?1:0;rank=Math.Clamp(rank,0,3);if(rank>0)foreach(string kind in new[]{"reload","magazine","accuracy"}){string id=family+"-"+kind;state.Ranks[id]=Math.Max(state.Ranks.GetValueOrDefault(id),rank);purchased.Add(id);}purchased.Remove(old);state.Ranks.Remove(old);}state.Purchased=purchased.ToArray();}
 public string ResearchBenefit(CompanyUpgrade n)
 {
  var weapon=GameObject.Scene?.GetComponentsSnapshot<WeaponDefinition3D>().FirstOrDefault(w=>(w.RequiredResearchId=="shotgun"?"Shotgun":w.RequiredResearchId=="ar"?"Rifle":"Pistol")==n.Branch);if(weapon==null||!_weaponBaselines.TryGetValue(weapon,out var b))return n.Description;bool max=ResearchRank(n.Id)>=n.MaxRank;if(max)return "MAX RANK. "+n.Description;
  if(n.MagazineBonus>0)return $"Magazine {weapon.MagazineCapacity} > {weapon.MagazineCapacity+n.MagazineBonus} rounds. "+n.Description;
  if(n.ReloadSpeedBonus>0){float factor=b.Reload/Math.Max(.01f,weapon.ReloadSeconds);return $"Reload {weapon.ReloadSeconds:0.00}s > {b.Reload/(factor+n.ReloadSpeedBonus):0.00}s. "+n.Description;}
  if(n.Stat=="WeaponDamagePct"){float factor=1+_research.Where(x=>x.Branch==n.Branch&&x.Stat==n.Stat).Sum(x=>x.Amount*ResearchRank(x.Id))/100;return $"Damage {weapon.DamagePerPellet:0.#} > {weapon.DamagePerPellet*(factor+n.Amount/100)/factor:0.#} per hit. "+n.Description;}
  return n.Description;
 }
}
