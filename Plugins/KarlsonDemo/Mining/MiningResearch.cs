using System.Text.Json;
namespace KarlsonDemo.Plugin;
public enum CompanyUpgradeEffect{Damage,Health,Energy,Extraction,RefineryYield,AmmoProduction}
public sealed class CompanyUpgrade
{
 public string Id{get;set;}="";
 public string Name{get;set;}="Upgrade";
 public string Description{get;set;}="";
 public int Cost{get;set;}=90;
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
  var nodes=JsonSerializer.Deserialize<CompanyUpgrade[]>(LegacyJson)!.ToList();
  var grid=new Dictionary<string,(int X,int Y)>{{"company",(4,0)},{"arms1",(3,1)},{"industry1",(4,1)},{"survival1",(5,1)},{"arms2",(3,2)},{"industry2",(4,2)},{"survival2",(5,2)},{"arms3",(3,3)},{"industry3",(4,3)},{"survival3",(5,3)}};
  foreach(var n in nodes){var p=grid[n.Id];n.X=p.X;n.Y=p.Y;}
  using var stream=typeof(MiningResearchDefaults).Assembly.GetManifestResourceStream("CompanyResearchCatalog.json")!;
  nodes.AddRange(JsonSerializer.Deserialize<CompanyUpgrade[]>(stream)!);return JsonSerializer.Serialize(nodes);
 }
 static string LegacyJson=>JsonSerializer.Serialize(new CompanyUpgrade[]{
  new(){Id="company",Name="Company charter",Description="Magazines hold 3 extra rounds. Damage +5%.",MagazineBonus=3,Cost=60,Effect=CompanyUpgradeEffect.Damage,Amount=.05f,X=290,Y=143},
  new(){Id="arms1",Name="Quick-change feed",Description="Reload 35% faster. Weapon damage +8%.",ReloadSpeedBonus=.35f,Cost=100,Prerequisites=["company"],Effect=CompanyUpgradeEffect.Damage,Amount=.08f,X=95,Y=248},
  new(){Id="industry1",Name="Deep survey",Description="Extract 10% more ore each operation.",Cost=100,Prerequisites=["company"],Effect=CompanyUpgradeEffect.Extraction,Amount=.1f,X=290,Y=248},
  new(){Id="survival1",Name="Field medicine",Description="Increase maximum health by 15.",Cost=90,Prerequisites=["company"],Effect=CompanyUpgradeEffect.Health,Amount=15,X=485,Y=248},
  new(){Id="arms2",Name="Extended feed",Description="Magazines hold 6 extra rounds. Damage +12%.",MagazineBonus=6,Cost=220,Prerequisites=["arms1"],Effect=CompanyUpgradeEffect.Damage,Amount=.12f,X=95,Y=348},
  new(){Id="industry2",Name="Auto refinery",Description="Automatically process cargo. Refined yield +15%.",AutoRefine=true,Cost=200,Prerequisites=["industry1"],Effect=CompanyUpgradeEffect.RefineryYield,Amount=.15f,X=290,Y=348},
  new(){Id="survival2",Name="Trauma plating",Description="Increase maximum health by another 25.",Cost=180,Prerequisites=["survival1"],Effect=CompanyUpgradeEffect.Health,Amount=25,X=485,Y=348},
  new(){Id="arms3",Name="Supply conveyor",Description="Auto-deliver ammo between waves. Production +25%.",AutoCollectAmmo=true,Cost=380,Prerequisites=["arms2","industry1"],Effect=CompanyUpgradeEffect.AmmoProduction,Amount=.25f,X=95,Y=448},
  new(){Id="industry3",Name="Planetary survey",Description="Extract another 20% more ore per operation.",Cost=400,Prerequisites=["industry2"],Effect=CompanyUpgradeEffect.Extraction,Amount=.2f,X=290,Y=448},
  new(){Id="survival3",Name="Field resilience",Description="Increase maximum health by another 30.",Cost=340,Prerequisites=["survival2"],Effect=CompanyUpgradeEffect.Health,Amount=30,X=485,Y=448}
 });
}
