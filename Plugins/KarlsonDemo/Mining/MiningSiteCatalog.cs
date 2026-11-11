using System.Text.Json;
namespace KarlsonDemo.Plugin;
public static class MiningSiteCatalog
{
 public static readonly string[] Sectors=["Dust frontier","Ember badlands","Prism canyons","Lost industry","Outer scar"];
 public static MiningContract[] Create(){using var stream=typeof(MiningSiteCatalog).Assembly.GetManifestResourceStream("DrillSites.json")??throw new InvalidOperationException("Drill site catalog missing.");return JsonSerializer.Deserialize<MiningContract[]>(stream)??throw new InvalidOperationException("Drill site catalog empty.");}
}
