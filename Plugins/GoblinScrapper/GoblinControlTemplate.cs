using ByteEngine.Core.VisualLogic;
namespace GoblinScrapper;
public static class GoblinControlTemplate
{
 public static EventModuleDefinition Create()
 {
  var module=new EventModuleDefinition{Name="Goblin - Editable Controls",RequiredComponents=["VehicleBuilder3D"]};
  VisualInstruction Command(string id,params (string Key,EventValue Value)[] args)=>new(){Id=id.StartsWith("input.")||id.StartsWith("system.")?id:"bytebard.goblinscrapper."+id,Arguments=args.ToDictionary(a=>a.Key,a=>a.Value)};
  VisualInstruction Key(string key,bool held=false)=>Command(held?"input.keyHeld":"input.keyPressed",("key",EventValue.String(key)));
  void Rule(string name,VisualInstruction[] conditions,params VisualInstruction[] actions)=>module.Rules.Add(new(){DisplayName=name,Conditions=[..conditions],Actions=[..actions]});
  VisualInstruction Mode(bool build)=>Command(build?"building":"driving");
  Rule("Use the editable keyboard controls",[Command("system.triggerOnce")],Command("setBuiltInControls",("enabled",EventValue.Boolean(false))));
  Rule("Reset frame commands and held weapons",[Command("system.always")],Command("resetInput"),Command("poweredWeapons",("enabled",EventValue.Boolean(false))));
  Rule("B - toggle Build / Simulation",[Key("B")],Command("toggleMode"));
  Rule("Space - start simulation",[Mode(true),Key("Space")],Command("beginDrive"));
  foreach(var item in new[]{("W","addThrottle",1),("S","addThrottle",-1),("D","addSteering",1),("A","addSteering",-1)})
   Rule(item.Item1+" - "+item.Item2,[Mode(false),Key(item.Item1,true)],Command(item.Item2,("value",EventValue.Number(item.Item3))));
  Rule("Space - brake",[Mode(false),Key("Space",true)],Command("setBrake",("enabled",EventValue.Boolean(true))));
  foreach(string shift in new[]{"LeftShift","RightShift"})Rule(shift+" - drift",[Mode(false),Key(shift,true)],Command("setDrift",("enabled",EventValue.Boolean(true))));
  Rule("F - fire a shot",[Mode(false),Key("F")],Command("fire"));
  Rule("Hold F - powered weapons",[Mode(false),Key("F",true)],Command("poweredWeapons",("enabled",EventValue.Boolean(true))));
  Rule("G - mechanisms",[Mode(false),Key("G")],Command("mechanisms"));
  Rule("Enter - place preview",[Mode(true),Key("Enter")],Command("placePreview"));
  Rule("R - rotate preview 90 degrees",[Mode(true),Key("R")],Command("rotatePreview",("degrees",EventValue.Number(90))));
  Rule("F - flip preview",[Mode(true),Key("F")],Command("rotatePreview",("degrees",EventValue.Number(180))));
  Rule("Tab - next own connector",[Mode(true),Key("Tab")],Command("nextConnector"));
  Rule("Delete - selected block",[Mode(true),Key("Delete")],Command("removeSelected"));
  Rule("X - hovered block",[Mode(true),Key("X")],Command("removeHovered"));
  Rule("C - copy hovered part",[Mode(true),Key("C")],Command("copyHovered"));
  Rule("M - move hovered branch",[Mode(true),Key("M")],Command("moveHovered"));
  Rule("Escape - cancel operation",[Mode(true),Key("Escape")],Command("cancel"));
  Rule("Ctrl+Z - undo",[Mode(true),Key("Z"),Key("LeftControl",true)],Command("undo"));
  Rule("Ctrl+Y - redo",[Mode(true),Key("Y"),Key("LeftControl",true)],Command("redo"));
  Rule("F5 - save authored machine",[Key("F5")],Command("save"));
  Rule("F9 - load saved machine",[Mode(true),Key("F9")],Command("load"));
  Rule("R - reset physical position",[Mode(false),Key("R")],Command("resetPosition"));
  return module;
 }
}
