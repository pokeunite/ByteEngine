using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;

internal static class WeaponEventAuthor
{
 const string Root="C:/Users/codex/Documents/ultra violence";
 const string Prefix="bytebard.karlsondemo.";
 static VisualInstruction I(string id,params (string Name,EventValue Value)[] args)=>new(){Id=id,Arguments=args.ToDictionary(a=>a.Name,a=>a.Value)};
 static EventRuleDefinition Rule(string name,VisualInstruction[] conditions,params VisualInstruction[] actions)=>new(){DisplayName=name,Conditions=conditions.ToList(),Actions=actions.ToList()};
 public static void UpgradeTargetTest()
 {
  string path=Root+"/Assets/Events/est.byteevents";if(!File.Exists(path))return;
  var serializer=new EventModuleSerializer();var module=serializer.Load(path);
  foreach(var rule in module.Rules.Where(r=>r.Actions.Any(a=>a.Id=="object.destroy")&&r.Conditions.Any(c=>c.Id=="object.hasTag")))
  {
   string backup=Root+"/.byteengine/Backups/Before weapon hit events/est.byteevents";Directory.CreateDirectory(Path.GetDirectoryName(backup)!);if(!File.Exists(backup))File.Copy(path,backup);
   var shot=rule.Conditions.FirstOrDefault(c=>c.Id==Prefix+"hit");if(shot==null){shot=I(Prefix+"hit",("target",EventValue.String("FPS Character")));rule.Conditions.Insert(0,shot);}
   shot.Arguments["checkLastRayHit"]=EventValue.Boolean(true);
   shot.Arguments["hitObject"]=EventValue.String("Self");
   foreach(var obsolete in rule.Conditions.Where(c=>c.Id=="physics.lastRayHitObject").ToArray())
   {
    rule.ConnectedConditionIds.Remove(obsolete.InstanceId);
    foreach(var node in rule.Conditions)node.ConditionInputIds.Remove(obsolete.InstanceId);
    rule.Conditions.Remove(obsolete);
   }
   if(rule.HasExplicitConditionFlow)
   {
    var and=rule.Conditions.FirstOrDefault(c=>c.Id=="logic.and"&&rule.ConnectedConditionIds.Contains(c.InstanceId));
    var links=and?.ConditionInputIds??rule.ConnectedConditionIds;
    links.Remove(shot.InstanceId);links.Insert(0,shot.InstanceId);
   }
  }
  serializer.Save(module,path);
  Console.WriteLine("TARGET TEST NOW REQUIRES THE ACTUAL WEAPON HIT ON SELF; EXISTING TAG FILTER AND ACTIONS RETAINED");
 }
 public static void Run(GameProjectRuntime runtime)
 {
  var input=new EventModuleDefinition{Name="FPS controls — editable weapon input"};
  for(int n=0;n<3;n++)input.Rules.Add(Rule("Press "+(n+1)+" — equip weapon slot "+n,[I("input.keyPressed",("key",EventValue.String("D"+(n+1))))],I(Prefix+"equip",("slot",EventValue.Number(n)))));
  input.Rules.Add(Rule("Hold left mouse — automatic fire",[I(Prefix+"inputActive"),I("input.mouseHeld",("button",EventValue.String("Left"))),I(Prefix+"automatic"),I(Prefix+"canFire")],I(Prefix+"fire")));
  input.Rules.Add(Rule("Press left mouse — single shot",[I(Prefix+"inputActive"),I("input.mousePressed",("button",EventValue.String("Left"))),I(Prefix+"semiAutomatic"),I(Prefix+"canFire")],I(Prefix+"fire")));
  input.Rules.Add(Rule("Press R — reload",[I("input.keyPressed",("key",EventValue.String("R")))],I(Prefix+"reload")));
  var presentation=new EventModuleDefinition{Name="Weapon presentation — editable animation and shot-hit events"};
  presentation.Rules.Add(Rule("Reload cancelled — stop reload animation",[I(Prefix+"reloadCancelled")],I("animation.cancelAction")));
  VisualInstruction Play(string action)=>I("animation.playNamedAction",("action",EventValue.String(action)),("retrigger",EventValue.Boolean(true)),("forceInterrupt",EventValue.Boolean(true)));
  presentation.Rules.Add(Rule("Weapon equipped — play Equip",[I(Prefix+"equipped")],Play("Equip")));
  presentation.Rules.Add(Rule("Weapon fired — play Fire",[I(Prefix+"fired")],Play("Fire")));
  presentation.Rules.Add(Rule("Reload started — play Reload",[I(Prefix+"reloadStarted")],Play("Reload")));
  presentation.Rules.Add(Rule("Shot hit — expose actual hit object, position and normal",[I(Prefix+"hit")],I("physics.saveRayHit",("prefix",EventValue.String("Shot")))));
  presentation.Rules.Add(Rule("Shot missed — clear saved hit result",[I(Prefix+"missed")],I("physics.saveRayHit",("prefix",EventValue.String("Shot")))));
  const string controlsPath="Assets/Events/FPS controls.byteevents",presentationPath="Assets/Events/Weapon presentation.byteevents";
  var serializer=new EventModuleSerializer();serializer.Save(input,Root+"/"+controlsPath);serializer.Save(presentation,Root+"/"+presentationPath);
  Directory.CreateDirectory("Plugins/KarlsonDemo/Samples");serializer.Save(input,"Plugins/KarlsonDemo/Samples/FPS controls.byteevents");serializer.Save(presentation,"Plugins/KarlsonDemo/Samples/Weapon presentation.byteevents");
  using(var db=new AssetDatabase(Root,["Assets","Scenes"]))db.Scan();runtime.Database.Scan();
  var scene=runtime.LoadStartupScene();
  var player=scene.FindGameObject("FPS Character")!;var equipment=player.Components.Single(c=>c.GetType().Name=="FpsGunLab3D");equipment.GetType().GetProperty("UseBuiltInFireInput")!.SetValue(equipment,false);
  foreach(var old in player.Components.OfType<EventModuleComponent>().ToArray())player.RemoveComponent(old);
  player.AddComponent(new EventModuleComponent()).AddModuleReference(runtime.Database.ResolveReference(controlsPath));
  foreach(var weapon in scene.GameObjects.Where(o=>o.Components.Any(c=>c.GetType().Name=="WeaponInstance3D")))
  {
   foreach(var old in weapon.Components.OfType<EventModuleComponent>().ToArray())weapon.RemoveComponent(old);
   weapon.AddComponent(new EventModuleComponent()).AddModuleReference(runtime.Database.ResolveReference(presentationPath));
  }
  runtime.Serializer.Save(scene,Root+"/Scenes/Weapons Range.bytescene");
  bool Under(GameObject obj){for(GameObject? current=obj;current!=null;current=current.Parent)if(current==player)return true;return false;}
  var data=runtime.Serializer.Serialize(scene);var ids=scene.GameObjects.Where(Under).Select(o=>o.Id).ToHashSet();
  new BlueprintSerializer().Save(new(){Name="Ultra Violence Sketchfab FPS — event-sheet controlled",Type=BlueprintType.Character,Root=data.GameObjects.Single(o=>o.Id==player.Id),Children=data.GameObjects.Where(o=>ids.Contains(o.Id)&&o.Id!=player.Id).ToList()},Root+"/Assets/Blueprints/Ultra Violence FPS.byteblueprint");
  Console.WriteLine("NATIVE EVENT SHEETS CONTROL INPUT AND ALL WEAPON ANIMATIONS; HIT RESULTS ARE SAVED ON EACH WEAPON");
 }
}
