using ByteEngine.Core.Runtime;
using ByteEngine.Core.Variables;
using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Core.VisualLogic;
public sealed partial class VisualLogicRegistry
{
    public sealed class CreatorSaveSlot
    {
        public SceneData Scene {get;set;}=new();
        public Dictionary<string,VariableValue> Globals {get;set;}=new();
    }
    private static void RegisterCreatorSaves(VisualLogicRegistry r)
    {
        var slot=TextArg("slot","Save slot","Slot1");var scope=TextArg("scope","Variable scope","Self");var output=TextArg("output","Result variable","SaveSlots");
        GameSaveService Service(EventExecutionContext c)=>new(c.Scene.ProjectRoot??throw new InvalidOperationException("Scene is not associated with a game project."));
        string Key(VisualInstruction i,EventExecutionContext c)
        {string name=S(i,c,"slot","Slot1");if(name.Length is 0 or >80||name.Any(ch=>!char.IsLetterOrDigit(ch)&&ch!='_'&&ch!='-'))throw new ArgumentException("Slot names use letters, numbers, underscores and hyphens (up to 80 characters).");return "SceneSlots/"+name+".json";}
        string OperationKey(VisualInstruction i,EventExecutionContext c)=>c.Self.Id.ToString("N")+"/"+Key(i,c);
        Add(r,"save.writeSlot","Storage / Save Slots","Save scene and global state to slot",(i,c)=>
        {var serializer=c.Scene.SerializationContext??throw new InvalidOperationException("No native scene serializer is available.");var service=Service(c);string key=Key(i,c);string json=System.Text.Json.JsonSerializer.Serialize(new CreatorSaveSlot{Scene=serializer.Serialize(c.Scene),Globals=c.Globals.ToDictionary(p=>p.Key,p=>p.Value.Clone())},ByteEngine.Core.Serialization.JsonSerialization.Options);CreatorSceneServices.For(c.Scene).StartOperation(OperationKey(i,c),()=>{service.SaveText(key,json);return null;},_=>{});},slot);
        Add(r,"save.loadSlot","Storage / Save Slots","Restore scene and global state from slot",(i,c)=>
        {var serializer=c.Scene.SerializationContext??throw new InvalidOperationException("No native scene serializer is available.");var service=Service(c);string key=Key(i,c),operationKey=OperationKey(i,c);CreatorSceneServices.For(c.Scene).StartOperation(operationKey,()=>service.LoadNative<CreatorSaveSlot>(key)??throw new InvalidDataException("Save slot is empty."),result=>{var saved=(CreatorSaveSlot)result!;var globals=new VariableStore();foreach(var pair in saved.Globals)globals.Set(pair.Key,pair.Value.Clone());var restored=serializer.Deserialize(saved.Scene);CreatorSceneServices.For(restored).RecordOperationSuccess(operationKey);c.Scene.PendingRestore=(restored,globals);});},slot);
        Test(r,"save.pending","Storage / Save Slots","Save slot operation is pending",(i,c)=>CreatorSceneServices.For(c.Scene).GetOperation(OperationKey(i,c)) is {Completed:false},slot);
        Test(r,"save.finished","Storage / Save Slots","Save slot operation finished this update",(i,c)=>CreatorSceneServices.For(c.Scene).GetOperation(OperationKey(i,c))?.FinishedThisUpdate==true,slot);
        Test(r,"save.succeeded","Storage / Save Slots","Save slot operation succeeded",(i,c)=>CreatorSceneServices.For(c.Scene).GetOperation(OperationKey(i,c))?.Succeeded==true,slot);
        Test(r,"save.failed","Storage / Save Slots","Save slot operation failed",(i,c)=>CreatorSceneServices.For(c.Scene).GetOperation(OperationKey(i,c)) is {Completed:true,Succeeded:false},slot);
        Add(r,"save.readError","Storage / Save Slots","Read save slot operation error",(i,c)=>Output(i,c,VariableValue.FromString(CreatorSceneServices.For(c.Scene).GetOperation(OperationKey(i,c))?.Error??"")),slot,scope,output);
        Add(r,"save.clearResult","Storage / Save Slots","Clear completed save slot result",(i,c)=>CreatorSceneServices.For(c.Scene).RemoveOperation(OperationKey(i,c)),slot);
        Add(r,"save.deleteSlot","Storage / Save Slots","Delete save slot",(i,c)=>Service(c).Delete(Key(i,c)),slot);
        Test(r,"save.slotExists","Storage / Save Slots","Save slot exists",(i,c)=>Service(c).Exists(Key(i,c)),slot);
        Add(r,"save.listSlots","Storage / Save Slots","List save slot names",(i,c)=>Output(i,c,VariableValue.FromArray(Service(c).List("SceneSlots").Select(VariableValue.FromString))),scope,output);
    }
}
