using ByteEngine.Core.Plugins;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
namespace KarlsonDemo.Plugin;
internal static class MiningExpeditionEvents
{
 public static void Register(ByteEnginePluginContext c){const string prefix="bytebard.karlsondemo.mining.";var target=new VisualArgumentDefinition("target","Mining operation",VariableValue.FromString("Self")){IsObjectReference=true};
 void A(string id,string label,Action<MiningOperation3D> action)=>c.RegisterAction(new(){Id=prefix+id,Category="Mining",MenuGroup="Expeditions and industry",DisplayName=label,Arguments=[target],Execute=(i,e)=>{if(WeaponEvents.ResolveObject(i,e)?.GetComponent<MiningOperation3D>() is {} op)action(op);}});
 A("acceptExpedition","Accept contract or start next wave early",op=>op.StartOperation());A("startWaveEarly","Start next wave early for bonus ore",op=>op.StartWaveEarly());A("toggleAutoRefinery","Toggle slower automatic refinery loading",op=>op.ToggleAutoRefinery());A("rechargeMines","Recharge depleted mines in place (refined ore)",op=>op.DefencePlanner?.RechargeAllMines());
 c.RegisterCondition(new(){Id=prefix+"expeditionActive",Category="Mining",MenuGroup="Expeditions and industry",DisplayName="A timed expedition is active",Arguments=[target],Evaluate=(i,e)=>WeaponEvents.ResolveObject(i,e)?.GetComponent<MiningOperation3D>()?.ExpeditionActive==true});
 }
}
