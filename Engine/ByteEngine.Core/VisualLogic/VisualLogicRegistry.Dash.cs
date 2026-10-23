using System.Numerics;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Variables;
namespace ByteEngine.Core.VisualLogic;
public sealed partial class VisualLogicRegistry
{
    private static void RegisterDash(VisualLogicRegistry r)
    {
        const string category="Gameplay / Dash";
        var target=ObjectArg();
        Add(r,"dash.activate",category,"Activate dash",(i,c)=>ResolveObjectTarget(i,c)?.GetComponent<DashAbility3D>()?.TryDash(),target);
        Add(r,"dash.cancel",category,"Cancel dash",(i,c)=>ResolveObjectTarget(i,c)?.GetComponent<DashAbility3D>()?.Cancel(),target);
        Test(r,"dash.canDash",category,"Dash is ready (energy and cooldown)",(i,c)=>ResolveObjectTarget(i,c)?.GetComponent<DashAbility3D>()?.CanDash==true,target);
        Test(r,"dash.isDashing",category,"Character is dashing",(i,c)=>ResolveObjectTarget(i,c)?.GetComponent<DashAbility3D>()?.IsDashing==true,target);
        Test(r,"dash.canRegenerate",category,"Energy regeneration delay has ended",(i,c)=>ResolveObjectTarget(i,c)?.GetComponent<DashAbility3D>()?.CanRegenerateEnergy==true,target);
        Add(r,"dash.configure",category,"Configure dash distance, duration, energy and cooldown",(i,c)=>{
            if(ResolveObjectTarget(i,c)?.GetComponent<DashAbility3D>() is {} d){d.Distance=(float)N(i,c,"distance",4.2);d.Duration=(float)N(i,c,"duration",.18);d.EnergyCost=(float)N(i,c,"energy",25);d.Cooldown=(float)N(i,c,"cooldown",1.25);}
        },target,NumberArg("distance","Distance",4.2),NumberArg("duration","Duration seconds",.18),NumberArg("energy","Energy cost",25),NumberArg("cooldown","Cooldown seconds",1.25));
        Add(r,"dash.updateHud",category,"Update dash cooldown and ready status",(i,c)=>{
            if(ResolveObjectTarget(i,c)?.GetComponent<DashAbility3D>() is not {} d)return;
            if(ResolveObjectArgument(i,"bar",c,false)?.GetComponent<UiWidget>() is {} bar){bar.Maximum=1;bar.Value=d.CooldownFraction;}
            if(ResolveObjectArgument(i,"text",c,false)?.GetComponent<UiText>() is {} text){text.LocalizationKey="";text.Text=d.CooldownRemaining>0?d.CooldownRemaining.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture):d.CanDash?S(i,c,"ready","READY"):d.AvailabilityStatus=="ENERGY"?S(i,c,"blocked","ENERGY"):d.AvailabilityStatus;}
        },target,ObjectArg("bar","Cooldown overlay"),ObjectArg("text","Status text"),TextArg("ready","Ready label","READY"),TextArg("blocked","Unavailable label","ENERGY"));
        Add(r,"ui.setTextFromResource","UI","Set text from resource",(i,c)=>{
            if(ResolveObjectTarget(i,c)?.GetComponent<UiText>() is {} text&&ResolveObjectArgument(i,"source",c)?.GetComponent<ResourceMeter3D>() is {} resource){text.LocalizationKey="";text.Text=S(i,c,"format","{current} / {maximum}").Replace("{current}",resource.Current.ToString("0")).Replace("{maximum}",resource.Capacity.ToString("0")).Replace("{percent}",(resource.Fraction*100).ToString("0"));}
        },target,ObjectArg("source","Resource owner"),TextArg("format","Format ({current}, {maximum}, {percent})","{current} / {maximum}"));
        Add(r,"ui.setWidgetColor","UI","Set widget tint",(i,c)=>{
            if(ResolveObjectTarget(i,c)?.GetComponent<UiWidget>() is {} widget)widget.Color=new Vector4(EventValueResolver.GetVector3(i,"color",c,Vector3.One),(float)Math.Clamp(N(i,c,"alpha",1),0,1));
        },target,new("color","Tint RGB",VariableValue.FromVector3(Vector3.One)),NumberArg("alpha","Opacity",1));
    }
}
