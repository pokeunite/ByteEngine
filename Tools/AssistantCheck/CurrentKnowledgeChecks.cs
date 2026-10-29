using ByteEngine.Editor.Assistant;
static class CurrentKnowledgeChecks
{
 public static void Run()
 {
  void Check(string query,string expected){var text=AssistantTools.Serialize(AssistantKnowledge.Search(query));if(!text.Contains(expected,StringComparison.Ordinal))throw new Exception("Current assistant guide missing: "+expected);Console.WriteLine("PASS current assistant knowledge: "+expected);}
  Check("mining raider damage attack slots","RaiderDamage");Check("projectile spider hitbox trigger","BodyHitboxSize");
  Check("menu hover music confirmation","UnscaledDeltaTime");Check("character explorer skeleton rig bind pose","rest transforms");
  Check("windows runtime exposed security native aot","decompiled");Check("web browser performance capture profiling","universal FPS");
  var reference=AssistantTools.Serialize(AssistantReference.Search("RaiderDamage","Docs.HazardPay-PC-Polish-2026-10-29.md"));
  if(!reference.Contains("PC playtest fixes"))throw new Exception("New PC reference is not embedded");
  Console.WriteLine("PASS current assistant documentation is embedded in the editor");
 }
}
