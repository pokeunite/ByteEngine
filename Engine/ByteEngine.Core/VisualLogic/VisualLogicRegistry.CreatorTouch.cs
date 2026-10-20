using ByteEngine.Core.Variables;
namespace ByteEngine.Core.VisualLogic;
public sealed partial class VisualLogicRegistry
{
    private static void RegisterCreatorTouch(VisualLogicRegistry r)
    {
        var id=NumberArg("id","Touch ID");var any=BoolArg("any","Any touch ID",true);var scope=TextArg("scope","Variable scope","Self");var output=TextArg("output","Result variable","Touch");
        foreach(var phase in Enum.GetValues<TouchPhase>())
        {
            var p=phase;
            Test(r,"touch."+phase.ToString().ToLowerInvariant(),"Input / Touch","Touch "+phase,(i,c)=>TouchInput.Points.Any(t=>(p==TouchPhase.Held?t.Phase is TouchPhase.Held or TouchPhase.Started:t.Phase==p)&&(B(i,c,"any",true)||t.Id==(int)N(i,c,"id"))),any,id);
        }
        Add(r,"touch.read","Input / Touch","Read touch IDs, positions and phases",(i,c)=>Output(i,c,VariableValue.FromArray(TouchInput.Points.Select(t=>
        {var v=VariableValue.FromStructure();v.Children["id"]=VariableValue.FromNumber(t.Id);v.Children["position"]=VariableValue.FromVector2(t.Position);v.Children["phase"]=VariableValue.FromString(t.Phase.ToString());return v;}))),scope,output);
        Add(r,"touch.count","Input / Touch","Read active touch count",(i,c)=>Output(i,c,VariableValue.FromNumber(TouchInput.Points.Count(t=>t.Phase is TouchPhase.Started or TouchPhase.Held))),scope,output);
    }
}
