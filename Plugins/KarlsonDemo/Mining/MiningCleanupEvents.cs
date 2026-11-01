using ByteEngine.Core.Plugins;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
namespace KarlsonDemo.Plugin;
internal static class MiningCleanupEvents
{
 public static void Register(ByteEnginePluginContext c)
 {
  const string prefix="bytebard.karlsondemo.mining.";
  var target=new VisualArgumentDefinition("target","Mining operation",VariableValue.FromString("Self")){IsObjectReference=true};
  MiningOperation3D? Op(VisualInstruction i,EventExecutionContext e)=>WeaponEvents.ResolveObject(i,e)?.GetComponent<MiningOperation3D>();
  c.RegisterCondition(new(){Id=prefix+"targetWon",Category="Mining",MenuGroup="Level Target",DisplayName="Credit target has been reached",Arguments=[target],Evaluate=(i,e)=>Op(i,e)?.TargetWon==true});
  c.RegisterCondition(new(){Id=prefix+"targetExpired",Category="Mining",MenuGroup="Level Target",DisplayName="Contract deadline has expired",Arguments=[target],Evaluate=(i,e)=>Op(i,e)?.TargetExpired==true});
  c.RegisterAction(new(){Id=prefix+"configureTarget",Category="Mining",MenuGroup="Level Target",DisplayName="Set credit target and active play deadline",Arguments=[target,new("credits","Credits to hold",VariableValue.FromNumber(30000)),new("seconds","Active play seconds",VariableValue.FromNumber(18000)),new("days","Displayed days",VariableValue.FromNumber(10))],Execute=(i,e)=>{if(Op(i,e) is {} op){op.TargetCredits=Math.Max(1,(int)EventValueResolver.GetNumber(i,"credits",e));op.TargetPlaySeconds=Math.Max(1,(float)EventValueResolver.GetNumber(i,"seconds",e));op.TargetDays=Math.Max(1,(float)EventValueResolver.GetNumber(i,"days",e));}}});
  c.RegisterAction(new(){Id=prefix+"setAmmoMagnet",Category="Mining",MenuGroup="Ammo Press",DisplayName="Set ammo bucket collection radius",Arguments=[new("target","Ammo bucket object",VariableValue.FromString("Self")){IsObjectReference=true},new("radius","Pickup radius in metres",VariableValue.FromNumber(4.5))],Execute=(i,e)=>{if(WeaponEvents.ResolveObject(i,e)?.GetComponent<MiningAmmoBucket3D>() is {} b)b.PickupRadius=Math.Max(.1f,(float)EventValueResolver.GetNumber(i,"radius",e));}});
  c.RegisterAction(new(){Id=prefix+"rearmMines",Category="Mining",MenuGroup="Company Defences",DisplayName="Re-arm spent mines at saved positions",Arguments=[new("target","Defence planner",VariableValue.FromString("Self")){IsObjectReference=true}],Execute=(i,e)=>WeaponEvents.ResolveObject(i,e)?.GetComponent<MiningDefensePlanner3D>()?.RearmMines()});
  c.RegisterCondition(new(){Id=prefix+"defenceNeedsReplacement",Category="Mining",MenuGroup="Company Defences",DisplayName="Selected defence needs replacement ore",Arguments=[new("target","Defence planner",VariableValue.FromString("Self")){IsObjectReference=true}],Evaluate=(i,e)=>WeaponEvents.ResolveObject(i,e)?.GetComponent<MiningDefensePlanner3D>() is {} p&&p.ReplacementCost(p.SelectedKind)>0});
 }
}
