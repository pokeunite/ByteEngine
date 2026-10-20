using System.Globalization;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Variables;

namespace ByteEngine.Core.VisualLogic;

public sealed partial class VisualLogicRegistry
{
    private static void RegisterAuthoring(VisualLogicRegistry registry)
    {
        registry.RegisterAction(new VisualActionDefinition
        {
            Id="input.keepPointerFree",Category="Input",DisplayName="Keep Pointer Visible / Free",
            Execute=(_,_)=>Input.NotifyGameViewPointerAim()
        });
        registry.RegisterAction(new VisualActionDefinition
        {
            Id="scene.setTimeScale",Category="Scenes",DisplayName="Set Simulation Time Scale",
            Arguments=[new("scale","Time scale (0 pauses)",VariableValue.FromNumber(1))],
            Execute=(i,c)=>c.Scene.SimulationTimeScale=Math.Clamp((float)EventValueResolver.GetNumber(i,"scale",c,1),0,8)
        });
        registry.RegisterAction(new VisualActionDefinition
        {
            Id="ui.setFormattedText",Category="UI",DisplayName="Set Formatted Text From Values",
            Arguments=[new("target","Text object",VariableValue.FromString("Self")){IsObjectReference=true},new("format","Format ({0}, {1}, {2}, {3})",VariableValue.FromString("{0:0.00}")),
                new("value0","Value 0",VariableValue.FromNumber()){AcceptsAnyValue=true},new("value1","Value 1",VariableValue.FromNumber()){AcceptsAnyValue=true},new("value2","Value 2",VariableValue.FromNumber()){AcceptsAnyValue=true},new("value3","Value 3",VariableValue.FromNumber()){AcceptsAnyValue=true}],
            Execute=(i,c)=>
            {
                if(ResolveObjectTarget(i,c,false)?.GetComponent<UiText>() is not {} text)return;
                var values=new object?[4];
                for(int n=0;n<4;n++)if(i.Arguments.TryGetValue("value"+n,out var v))EventValueResolver.TryResolve(v,c,out values[n]);
                try{text.LocalizationKey="";text.Text=string.Format(CultureInfo.InvariantCulture,EventValueResolver.GetString(i,"format",c,"{0}"),values);}
                catch(FormatException){c.WarningSink?.Invoke("Formatted UI text contains an invalid format string.");}
            }
        });
        registry.RegisterCondition(new VisualConditionDefinition
        {
            Id="physics.supportedBy",Category="Physics / Contacts",DisplayName="Body Is Supported By Object",
            Arguments=[new("target","Supported body",VariableValue.FromString("Self")),new("support","Supporting object",VariableValue.FromString(""))],
            Evaluate=(i,c)=>
            {
                var body=ResolveObjectTarget(i,c,false);var support=ResolveObjectArgument(i,"support",c,false);
                if(body==null||support==null)return false;
                return c.Scene.Physics.Contacts.Any(p=>!p.IsTrigger&&
                    (p.A==body&&p.B==support&&p.Normal.Y<-.5f || p.B==body&&p.A==support&&p.Normal.Y>.5f));
            }
        });
        registry.RegisterCondition(new VisualConditionDefinition
        {
            Id="physics.speedBelow",Category="Physics / Contacts",DisplayName="Body Speed Is Below",
            Arguments=[new("target","Body",VariableValue.FromString("Self")),new("speed","Speed (m/s)",VariableValue.FromNumber(.5))],
            Evaluate=(i,c)=>ResolveObjectTarget(i,c,false)?.GetComponent<Rigidbody3D>() is {} body&&body.Velocity.Length()<EventValueResolver.GetNumber(i,"speed",c,.5)
        });
    }
}
