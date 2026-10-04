using ByteEngine.Core.Plugins;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
using GoblinScrapper.Construction;
namespace GoblinScrapper;
internal static class GoblinEvents
{
 internal static void Register(ByteEnginePluginContext context)
 {
  const string prefix="bytebard.goblinscrapper.";
  VisualArgumentDefinition Arg(string name,VariableValue value)=>new(name,char.ToUpper(name[0])+name[1..],value);
  var target=Arg("target",VariableValue.FromString("Self"));
  void Action(string id,string label,Action<VehicleBuilder3D,VisualInstruction,EventExecutionContext> run,params VisualArgumentDefinition[] args)=>context.RegisterAction(new(){Id=prefix+id,Category="Goblin / Builder",DisplayName=label,TargetComponent="VehicleBuilder3D",Arguments=[target,..args],Execute=(i,e)=>{var b=Resolve(i,e,true);if(b!=null)run(b,i,e);}});
  void Condition(string id,string label,Func<VehicleBuilder3D,VisualInstruction,EventExecutionContext,bool> test,params VisualArgumentDefinition[] args)=>context.RegisterCondition(new(){Id=prefix+id,Category="Goblin / Builder",DisplayName=label,TargetComponent="VehicleBuilder3D",Arguments=[target,..args],Evaluate=(i,e)=>Resolve(i,e,false) is {} b&&test(b,i,e)});
  bool Bool(VisualInstruction i,EventExecutionContext e,string key)=>EventValueResolver.GetBoolean(i,key,e);
  float Num(VisualInstruction i,EventExecutionContext e,string key)=> (float)EventValueResolver.GetNumber(i,key,e);
  string Text(VisualInstruction i,EventExecutionContext e,string key)=>EventValueResolver.GetString(i,key,e);
  var enabled=Arg("enabled",VariableValue.FromBoolean(true));var value=Arg("value",VariableValue.FromNumber(1));
  Action("setBuiltInControls","Use Built-in Keyboard / Driving Controls",(b,i,e)=>{b.UseBuiltInControls=Bool(i,e,"enabled");b.ResetEventInput();},enabled);
  Action("setPointerControls","Use Built-in Pointer Controls",(b,i,e)=>b.UseBuiltInPointerControls=Bool(i,e,"enabled"),enabled);
  Action("setCamera","Use Automatic Workshop Camera",(b,i,e)=>b.AutomaticCamera=Bool(i,e,"enabled"),enabled);
  Action("setHud","Show Workshop HUD",(b,i,e)=>b.ShowWorkshopHud=Bool(i,e,"enabled"),enabled);
  Action("resetInput","Reset Drive Input",(b,i,e)=>b.ResetEventInput());
  Action("setInput","Set Drive Input",(b,i,e)=>b.SetEventInput(Num(i,e,"throttle"),Num(i,e,"steering"),Bool(i,e,"brake"),Bool(i,e,"drift")),Arg("throttle",VariableValue.FromNumber(0)),Arg("steering",VariableValue.FromNumber(0)),Arg("brake",VariableValue.FromBoolean(false)),Arg("drift",VariableValue.FromBoolean(false)));
  Action("addThrottle","Add Throttle Input",(b,i,e)=>b.AddEventThrottle(Num(i,e,"value")),value);
  Action("addSteering","Add Steering Input",(b,i,e)=>b.AddEventSteering(Num(i,e,"value")),value);
  Action("setBrake","Set Brake Input",(b,i,e)=>b.SetEventBrake(Bool(i,e,"enabled")),enabled);
  Action("setDrift","Set Drift Input",(b,i,e)=>b.SetEventDrift(Bool(i,e,"enabled")),enabled);
  Action("beginDrive","Start Simulation",(b,i,e)=>{if(!b.BeginDriving())e.WarningSink?.Invoke("Goblin: machine is not ready for simulation.");});
  Action("buildMode","Return to Build",(b,i,e)=>b.ReturnToBuild());
  Action("toggleMode","Toggle Build / Simulation",(b,i,e)=>b.ToggleSimulation());
  Action("fire","Fire Weapons",(b,i,e)=>b.FireWeapons());
  Action("poweredWeapons","Set Powered Weapons",(b,i,e)=>b.SetPoweredWeapons(Bool(i,e,"enabled")),enabled);
  Action("mechanisms","Toggle Mechanisms",(b,i,e)=>b.ActivateMechanisms());
  Action("selectPart","Select Build Part",(b,i,e)=>{if(!b.SelectBuildPart(Text(i,e,"file")))e.WarningSink?.Invoke("Goblin: cannot select that part in the current build.");},Arg("file",VariableValue.FromString("goblin_double_wooden_block")));
  Action("addBrace","Connect Brace Endpoints",(b,i,e)=>{if(b.AddAssemblyBrace((int)Num(i,e,"blockA"),Text(i,e,"socketA"),(int)Num(i,e,"blockB"),Text(i,e,"socketB"))<0)e.WarningSink?.Invoke("Goblin: brace endpoints are invalid.");},Arg("blockA",VariableValue.FromNumber(0)),Arg("socketA",VariableValue.FromString("Front")),Arg("blockB",VariableValue.FromNumber(1)),Arg("socketB",VariableValue.FromString("SOCKET_Surface_Root_Top")));
  Action("placePreview","Place Preview",(b,i,e)=>b.PlacePreview());
  Action("rotatePreview","Rotate Preview",(b,i,e)=>b.RotatePreview(Num(i,e,"degrees")),Arg("degrees",VariableValue.FromNumber(90)));
  Action("cycleMountFace","Cycle Beam Mounting Face",(b,i,e)=>b.CyclePreviewMountFace());
  Action("nextConnector","Next Preview Connector",(b,i,e)=>b.NextPreviewConnector());
  Action("eraseTool","Set Erase Tool",(b,i,e)=>b.SetEraseTool(Bool(i,e,"enabled")),enabled);
  Action("removeSelected","Delete Selected Block",(b,i,e)=>b.DeleteSelectedBlock());
  Action("removeHovered","Delete Hovered Block",(b,i,e)=>b.DeleteHoveredBlock());
  Action("removePart","Delete Block by ID",(b,i,e)=>b.RemoveAssemblyPart((int)Num(i,e,"id")),Arg("id",VariableValue.FromNumber(1)));
  Action("copyHovered","Copy Hovered Part",(b,i,e)=>b.CopyHoveredPart());
  Action("moveHovered","Move Hovered Branch",(b,i,e)=>b.MoveHoveredBranch());
  Action("cancel","Cancel Build Operation",(b,i,e)=>b.CancelBuildOperation());
  Action("attachPart","Place Part at Connector",(b,i,e)=>{if(b.PlacePartAtConnector(Text(i,e,"file"),(int)Num(i,e,"parent"),Text(i,e,"connector"),Text(i,e,"ownConnector"),(int)Num(i,e,"twist"))<0)e.WarningSink?.Invoke("Goblin: connector placement rejected.");},Arg("file",VariableValue.FromString("goblin_double_wooden_block")),Arg("parent",VariableValue.FromNumber(0)),Arg("connector",VariableValue.FromString("Front")),Arg("ownConnector",VariableValue.FromString("")),Arg("twist",VariableValue.FromNumber(0)));
  Action("undo","Undo Build",(b,i,e)=>b.UndoBuild());Action("redo","Redo Build",(b,i,e)=>b.RedoBuild());
  Action("save","Save Machine",(b,i,e)=>b.SaveBuild());Action("load","Load Machine",(b,i,e)=>b.LoadBuild());Action("resetPosition","Reset Machine Position",(b,i,e)=>b.ResetPosition());
  Condition("building","Is in Build Mode",(b,i,e)=>b.Building);Condition("driving","Is Simulating",(b,i,e)=>!b.Building);
  Condition("canDrive","Can Start Simulation",(b,i,e)=>b.CanSimulate);Condition("placementReady","Preview Placement Is Valid",(b,i,e)=>b.PlacementReady);
  Condition("drifting","Is Drifting",(b,i,e)=>b.IsDrifting);
  Condition("speedAbove","Speed Is Above",(b,i,e)=>b.Speed>Num(i,e,"speed"),Arg("speed",VariableValue.FromNumber(1)));
  Action("restartBattle","Restart Battlefield Encounter",(b,i,e)=>b.RestartBattle());
  Action("battleEnemyCount","Set Next Battlefield Enemy Count",(b,i,e)=>{if(b.Building)b.BattlefieldEnemyCount=Math.Clamp((int)Num(i,e,"count"),1,24);},Arg("count",VariableValue.FromNumber(18)));
  Condition("battleRunning","Battlefield Encounter Is Running",(b,i,e)=>b.BattleRunning);
  Condition("battleWon","Red Camp Cleared",(b,i,e)=>b.BattleWon);
  Condition("battleLost","Battlefield Encounter Lost",(b,i,e)=>b.BattleLost);
  Condition("battleKills","Battle Kills Are at Least",(b,i,e)=>b.BattleKills>=Num(i,e,"count"),Arg("count",VariableValue.FromNumber(1)));
  Condition("hasPart","Has Block ID",(b,i,e)=>b.Assembly?.Parts.ContainsKey((int)Num(i,e,"id"))==true,Arg("id",VariableValue.FromNumber(1)));
  Condition("partCount","Block Count Is at Least",(b,i,e)=>b.Assembly?.Parts.Count>=Num(i,e,"count"),Arg("count",VariableValue.FromNumber(4)));
 }
 private static VehicleBuilder3D? Resolve(VisualInstruction i,EventExecutionContext e,bool warn)
 {
  string token=EventValueResolver.GetString(i,"target",e,"Self");GameObject? obj;
  if(string.IsNullOrWhiteSpace(token)||token.Equals("Self",StringComparison.OrdinalIgnoreCase))obj=e.Self;
  else if(token.Equals("Last Ray Hit",StringComparison.OrdinalIgnoreCase))obj=e.LastRaycastHit?.GameObject;
  else if(token.StartsWith("id:",StringComparison.OrdinalIgnoreCase)&&Guid.TryParse(token[3..],out var id))obj=e.Scene.FindGameObject(id);
  else obj=e.Scene.FindGameObject(token);
  var result=obj?.GetComponent<VehicleBuilder3D>();if(result==null&&warn)e.WarningSink?.Invoke("Goblin: target has no Goblin Contraption Builder: "+token);return result;
 }
}
