using ByteEngine.Core.Plugins;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
namespace KarlsonDemo.Plugin;
internal static class MiningGameFeelEvents
{
 public static void Register(ByteEnginePluginContext c)
 {
  const string prefix="bytebard.karlsondemo.mining.";var target=new VisualArgumentDefinition("target","Game feel / operation object",VariableValue.FromString("Self")){IsObjectReference=true};
  MiningGameFeel3D? Feel(VisualInstruction i,EventExecutionContext e)=>WeaponEvents.ResolveObject(i,e)?.GetComponent<MiningGameFeel3D>();
  c.RegisterAction(new(){Id=prefix+"cameraFeedbackStrength",Category="Mining",MenuGroup="Game feel",DisplayName="Set camera feedback strength",Arguments=[target,new("strength","Strength (0 to 100)",VariableValue.FromNumber(100))],Execute=(i,e)=>{if(Feel(i,e) is {} f)f.ShakeIntensity=(float)Math.Clamp(EventValueResolver.GetNumber(i,"strength",e)/100,0,1);}});
  c.RegisterAction(new(){Id=prefix+"readGameFeel",Category="Mining",MenuGroup="Game feel",DisplayName="Read confirmed hits, kills or rewards",Arguments=[target,new("field","Value",VariableValue.FromString("Confirmed hits")){Choices=["Confirmed hits","Confirmed kills","Ammo collected","Credits received"]},new("variable","Output variable",VariableValue.FromString("FeedbackValue"))],Execute=(i,e)=>{if(Feel(i,e) is not {} f)return;int value=EventValueResolver.GetString(i,"field",e) switch{"Confirmed hits"=>f.ConfirmedHits,"Confirmed kills"=>f.ConfirmedKills,"Ammo collected"=>f.AmmoGain,"Credits received"=>f.CreditGain,_=>0};e.Self.Variables.Set(EventValueResolver.GetString(i,"variable",e,"FeedbackValue"),VariableValue.FromNumber(value));}});
  c.RegisterCondition(new(){Id=prefix+"confirmedEnemyHit",Category="Mining",MenuGroup="Game feel",DisplayName="Player has a confirmed enemy hit",Arguments=[target],Evaluate=(i,e)=>Feel(i,e)?.HitMarkerVisible==true});
 }
}
