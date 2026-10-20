using ByteEngine.Core.Variables;
namespace ByteEngine.Core.VisualLogic;
public sealed partial class VisualLogicRegistry
{
    private static void RegisterCreatorFunctions(VisualLogicRegistry r)
    {
        var scope=TextArg("scope","Variable scope","Self");var output=TextArg("output","Result variable","Result");
        Add(r,"function.call","Flow / Functions","Call event function",(i,c)=>
        {var args=VariableJson.Parse(S(i,c,"parameters","{}"));Output(i,c,(c.TimerRuntime??throw new InvalidOperationException("No event runtime.")).CallFunction(S(i,c,"function"),args,c));},TextArg("function","Function name"),TextArg("parameters","Parameters JSON","{}"),scope,output);
        Add(r,"function.readArgument","Flow / Functions","Read function parameter",(i,c)=>
        {if(c.FunctionArguments==null||!c.FunctionArguments.Children.TryGetValue(S(i,c,"parameter"),out var value))throw new InvalidOperationException("Function parameter does not exist.");Output(i,c,value);},TextArg("parameter","Parameter name"),scope,output);
        Add(r,"function.setReturn","Flow / Functions","Set function return value",(i,c)=>{if(c.FunctionArguments==null)throw new InvalidOperationException("Return values require a function call.");c.FunctionReturn=V(i,c,"value").Clone();},AnyArg("value","Return value"));
        Add(r,"time.cancelWaits","Flow / Timing","Cancel pending waits in this sheet",(i,c)=>c.TimerRuntime?.CancelWaits());
    }
}
