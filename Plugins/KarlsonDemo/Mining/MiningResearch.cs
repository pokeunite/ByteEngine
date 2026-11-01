using System.Text.Json;
namespace KarlsonDemo.Plugin;
public enum CompanyUpgradeEffect{Damage,Health,Energy,Extraction,RefineryYield,AmmoProduction}
public sealed class CompanyUpgrade
{
 public string Id{get;set;}="";
 public string Name{get;set;}="Upgrade";
 public string Description{get;set;}="";
 public int Cost{get;set;}=90;
 public int MinimumRound{get;set;}
 public bool Available{get;set;}=true;
 public string[] Prerequisites{get;set;}=[];
 public CompanyUpgradeEffect Effect{get;set;}
 public float Amount{get;set;}
 public float ReloadSpeedBonus{get;set;}
 public int MagazineBonus{get;set;}
 public float DashCooldownReduction{get;set;}
 public bool AutoRefine{get;set;}
 public bool AutoCollectAmmo{get;set;}
 public float X{get;set;}
 public float Y{get;set;}
 public string Stat{get;set;}="";
 public int MaxRank{get;set;}=1;
 public int[] RankCosts{get;set;}=[];
 public string Branch{get;set;}="Company";
}
internal static class MiningResearchDefaults
{
 public static string Json=>Compose();
 static string Compose()
 {
  using var stream=typeof(MiningResearchDefaults).Assembly.GetManifestResourceStream("CompanyResearchCatalog.json")!;
  return JsonSerializer.Serialize(JsonSerializer.Deserialize<CompanyUpgrade[]>(stream)!);
 }
}
