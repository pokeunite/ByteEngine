using System.Numerics;
using ByteEngine.Core.Variables;
using ByteEngine.Core.Runtime;

namespace ByteEngine.Core.VisualLogic;

public sealed partial class VisualLogicRegistry
{
    private static VisualArgumentDefinition TextArg(string name,string label,string value="")=>new(name,label,VariableValue.FromString(value))
    {Choices=name switch {"scope"=>label.Contains("Object",StringComparison.OrdinalIgnoreCase)?["Self","Scene","Global","Object"]:["Self","Scene","Global"],"bus"=>["Master","Music","Sfx","Ui"],"easing"=>["Linear","Smoothstep","EaseIn","EaseOut","EaseInOut"],"method"=>["GET","POST","PUT","PATCH","DELETE","HEAD"],_=>Array.Empty<string>()}};
    private static VisualArgumentDefinition NumberArg(string name,string label,double value=0)=>new(name,label,VariableValue.FromNumber(value));
    private static VisualArgumentDefinition AnyArg(string name,string label)=>new(name,label,VariableValue.FromNumber()){AcceptsAnyValue=true};
    private static VisualArgumentDefinition BoolArg(string name,string label,bool value=false)=>new(name,label,VariableValue.FromBoolean(value));
    private static VisualArgumentDefinition ObjectArg(string name="target",string label="Object",string value="Self")=>new(name,label,VariableValue.FromString(value)){IsObjectReference=true};
    private static string S(VisualInstruction i,EventExecutionContext c,string key,string fallback="")=>EventValueResolver.GetString(i,key,c,fallback);
    private static double N(VisualInstruction i,EventExecutionContext c,string key,double fallback=0)
    {double value=EventValueResolver.GetNumber(i,key,c,fallback);return double.IsFinite(value)?value:throw new ArgumentOutOfRangeException(key,"Use a finite number.");}
    private static bool B(VisualInstruction i,EventExecutionContext c,string key,bool fallback=false)=>EventValueResolver.GetBoolean(i,key,c,fallback);
    private static VariableValue V(VisualInstruction i,EventExecutionContext c,string key)=>i.Arguments.TryGetValue(key,out var arg)&&EventValueResolver.TryResolve(arg,c,out var raw)?VariableJson.FromObject(raw):VariableValue.FromNumber();
    private static VariableStore Store(VisualInstruction i,EventExecutionContext c)=>S(i,c,"scope","Self").ToLowerInvariant() switch
    {"global"=>c.Globals,"scene"=>c.Scene.Variables,"object"=>ResolveObjectTarget(i,c)?.Variables??throw new InvalidOperationException("Variable object was not found."),_=>c.Self.Variables};
    private static VariableValue Collection(VisualInstruction i,EventExecutionContext c)
    {var store=Store(i,c);string name=S(i,c,"name","Items");return store.TryGet(name,out var value)?value!:throw new InvalidOperationException("Collection variable does not exist: "+name);}
    private static void Output(VisualInstruction i,EventExecutionContext c,VariableValue value)=>Store(i,c).Set(S(i,c,"output","Result"),value.Clone());
    private static void Add(VisualLogicRegistry r,string id,string category,string name,Action<VisualInstruction,EventExecutionContext> action,params VisualArgumentDefinition[] args)=>r.RegisterAction(new(){Id=id,Category=category,DisplayName=name,Arguments=args,Execute=action});
    private static void Test(VisualLogicRegistry r,string id,string category,string name,Func<VisualInstruction,EventExecutionContext,bool> test,params VisualArgumentDefinition[] args)=>r.RegisterCondition(new(){Id=id,Category=category,DisplayName=name,Arguments=args,Evaluate=test});

    private static void RegisterCreatorData(VisualLogicRegistry r)
    {
        var scope=TextArg("scope","Variable scope (Self, Scene, Global, Object)","Self");var name=TextArg("name","Collection variable","Items");
        VisualArgumentDefinition[] collection=[scope,ObjectArg(),name];var output=TextArg("output","Result variable","Result");var value=AnyArg("value","Value");var index=NumberArg("index","Index (zero based)");var key=TextArg("key","Child name","key");
        Add(r,"collection.createArray","Variables / Collections","Create / clear array",(i,c)=>Store(i,c).Set(S(i,c,"name","Items"),VariableValue.FromArray()),collection);
        Add(r,"collection.createStructure","Variables / Collections","Create / clear structure",(i,c)=>Store(i,c).Set(S(i,c,"name","Items"),VariableValue.FromStructure()),collection);
        Add(r,"collection.append","Variables / Collections","Append value to array",(i,c)=>{var v=Collection(i,c);if(v.Type!=VariableType.Array)throw new InvalidOperationException("Expected an array.");v.Items.Add(V(i,c,"value"));},[..collection,value]);
        Add(r,"collection.setChild","Variables / Collections","Set structure child",(i,c)=>{var v=Collection(i,c);if(v.Type!=VariableType.Structure)throw new InvalidOperationException("Expected a structure.");v.Children[S(i,c,"key")]=V(i,c,"value");},[..collection,key,value]);
        Add(r,"collection.getChild","Variables / Collections","Read structure child",(i,c)=>{var v=Collection(i,c);Output(i,c,v.Children.TryGetValue(S(i,c,"key"),out var item)?item:VariableValue.FromString());},[..collection,key,output]);
        Add(r,"collection.getAt","Variables / Collections","Read array index",(i,c)=>{var v=Collection(i,c);int n=(int)N(i,c,"index");if(v.Type!=VariableType.Array||n<0||n>=v.Items.Count)throw new IndexOutOfRangeException("Array index is outside its bounds.");Output(i,c,v.Items[n]);},[..collection,index,output]);
        Add(r,"collection.setAt","Variables / Collections","Set array index",(i,c)=>{var v=Collection(i,c);int n=(int)N(i,c,"index");if(v.Type!=VariableType.Array||n<0||n>=v.Items.Count)throw new IndexOutOfRangeException();v.Items[n]=V(i,c,"value");},[..collection,index,value]);
        Add(r,"collection.removeAt","Variables / Collections","Remove array index",(i,c)=>{var v=Collection(i,c);int n=(int)N(i,c,"index");if(v.Type==VariableType.Array&&n>=0&&n<v.Items.Count)v.Items.RemoveAt(n);},[..collection,index]);
        Add(r,"collection.removeChild","Variables / Collections","Remove structure child",(i,c)=>Collection(i,c).Children.Remove(S(i,c,"key")),[..collection,key]);
        Add(r,"collection.count","Variables / Collections","Copy collection count",(i,c)=>{var v=Collection(i,c);Output(i,c,VariableValue.FromNumber(v.Type==VariableType.Array?v.Items.Count:v.Children.Count));},[..collection,output]);
        Add(r,"collection.copy","Variables / Collections","Deep copy collection",(i,c)=>Output(i,c,Collection(i,c)),[..collection,output]);
        Test(r,"collection.hasChild","Variables / Collections","Structure has child",(i,c)=>Collection(i,c).Children.ContainsKey(S(i,c,"key")),[..collection,key]);
        Test(r,"collection.contains","Variables / Collections","Array contains value",(i,c)=>Collection(i,c).Items.Any(v=>VariableJson.Write(v)==VariableJson.Write(V(i,c,"value"))),[..collection,value]);
        Add(r,"data.parseJson","Variables / JSON","Parse JSON into variable",(i,c)=>Output(i,c,VariableJson.Parse(S(i,c,"json","{}"))),scope,TextArg("json","JSON text","{}"),output);
        Add(r,"data.toJson","Variables / JSON","Serialize variable to JSON text",(i,c)=>Output(i,c,VariableValue.FromString(VariableJson.Write(Collection(i,c)))),[..collection,output]);
        var saveKey=TextArg("key","Save key","progress");
        GameSaveService Saves(EventExecutionContext c)=>new(c.Scene.ProjectRoot??throw new InvalidOperationException("This scene has no project save context."));
        Add(r,"storage.save","Storage / Values","Save value",(i,c)=>Saves(c).SaveNative("EventValues/"+S(i,c,"key","progress")+".json",V(i,c,"value")),saveKey,value);
        Add(r,"storage.load","Storage / Values","Load value",(i,c)=>Output(i,c,Saves(c).LoadNative<VariableValue>("EventValues/"+S(i,c,"key","progress")+".json")??VariableValue.FromString()),saveKey,scope,output);
        Add(r,"storage.delete","Storage / Values","Delete saved value",(i,c)=>Saves(c).Delete("EventValues/"+S(i,c,"key","progress")+".json"),saveKey);
        Test(r,"storage.exists","Storage / Values","Saved value exists",(i,c)=>Saves(c).Exists("EventValues/"+S(i,c,"key","progress")+".json"),saveKey);
        Add(r,"time.pauseTimer","Time / Timers","Pause timer",(i,c)=>c.TimerRuntime?.PauseTimer(S(i,c,"name"),true),TextArg("name","Timer name","Timer"));
        Add(r,"time.resumeTimer","Time / Timers","Resume timer",(i,c)=>c.TimerRuntime?.PauseTimer(S(i,c,"name"),false),TextArg("name","Timer name","Timer"));
        Add(r,"time.readTimer","Time / Timers","Read timer elapsed seconds",(i,c)=>Output(i,c,VariableValue.FromNumber(c.TimerRuntime?.TimerElapsed(S(i,c,"name"))??0)),TextArg("name","Timer name","Timer"),scope,output);
        Test(r,"time.timerPaused","Time / Timers","Timer is paused",(i,c)=>c.TimerRuntime?.TimerPaused(S(i,c,"name"))==true,TextArg("name","Timer name","Timer"));
        // Wait is intercepted by the runtime so the remaining action chain resumes later.
        Add(r,"time.wait","Flow / Timing","Wait, then continue",(i,c)=>{},NumberArg("seconds","Wait seconds",1));
        Test(r,"logic.not","Logic","NOT - none of these conditions are true",(i,c)=>false);
    }
}
