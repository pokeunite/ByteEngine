using ByteEngine.Core.Variables;

namespace ByteEngine.Core.VisualLogic;

public sealed partial class VisualLogicRegistry
{
    private static VariableValue DialogueState(VisualInstruction i,EventExecutionContext c)
    {
        if(!Store(i,c).TryGet(S(i,c,"name","Dialogue"),out var state)||state!.Type!=VariableType.Structure)
            throw new InvalidOperationException("Load a dialogue before using it.");
        return state;
    }
    private static VariableValue DialogueNode(VariableValue state)=>state.Children["nodes"].Children[state.Children["current"].String];
    private static void EnterDialogue(VariableValue state,string next)
    {
        if(next.Length>0&&!state.Children["nodes"].Children.ContainsKey(next))throw new InvalidDataException("Unknown dialogue node: "+next);
        state.Children["current"]=VariableValue.FromString(next);
        state.Children["running"]=VariableValue.FromBoolean(next.Length>0);
    }
    private static void RegisterCreatorDialogue(VisualLogicRegistry r)
    {
        var scope=TextArg("scope","Variable scope","Self");var name=TextArg("name","Dialogue variable","Dialogue");
        VisualArgumentDefinition[] args=[scope,name];
        Add(r,"dialogue.load","Dialogue","Load dialogue JSON",(i,c)=>
        {
            var data=VariableJson.Parse(S(i,c,"json"));
            if(data.Type!=VariableType.Structure||!data.Children.TryGetValue("nodes",out var nodes)||nodes.Type!=VariableType.Structure||!data.Children.TryGetValue("start",out var start)||start.Type!=VariableType.String||!nodes.Children.ContainsKey(start.String))throw new InvalidDataException("Dialogue requires a start node and a nodes structure.");
            if(nodes.Children.Count>10000)throw new InvalidDataException("Dialogue exceeds 10000 nodes.");
            foreach(var node in nodes.Children.Values)
            {
                if(node.Type!=VariableType.Structure)throw new InvalidDataException("Dialogue nodes must be structures.");
                void ValidateNext(VariableValue entry){if(entry.Children.TryGetValue("next",out var next)&&(next.Type!=VariableType.String||(next.String.Length>0&&!nodes.Children.ContainsKey(next.String))))throw new InvalidDataException("Dialogue contains an invalid next node.");}
                ValidateNext(node);
                if(node.Children.TryGetValue("choices",out var choices))
                {if(choices.Type!=VariableType.Array)throw new InvalidDataException("Choices must be an array.");foreach(var choice in choices.Items){if(choice.Type!=VariableType.Structure||!choice.Children.ContainsKey("text"))throw new InvalidDataException("Choice requires text.");ValidateNext(choice);}}
            }
            data.Children["current"]=VariableValue.FromString();data.Children["running"]=VariableValue.FromBoolean();
            Store(i,c).Set(S(i,c,"name","Dialogue"),data);
        },[..args,TextArg("json","Dialogue JSON","{\"start\":\"intro\",\"nodes\":{\"intro\":{\"speaker\":\"Guide\",\"text\":\"Hello.\"}}}")]);
        Add(r,"dialogue.start","Dialogue","Start dialogue",(i,c)=>{var state=DialogueState(i,c);EnterDialogue(state,state.Children["start"].String);},args);
        Add(r,"dialogue.stop","Dialogue","Stop dialogue",(i,c)=>EnterDialogue(DialogueState(i,c),""),args);
        Add(r,"dialogue.advance","Dialogue","Advance dialogue without choices",(i,c)=>{var state=DialogueState(i,c);if(!state.Children["running"].Boolean)return;var node=DialogueNode(state);if(node.Children.TryGetValue("choices",out var choices)&&choices.Items.Count>0)throw new InvalidOperationException("Select a dialogue choice first.");EnterDialogue(state,node.Children.GetValueOrDefault("next")?.String??"");},args);
        Add(r,"dialogue.choose","Dialogue","Select dialogue choice",(i,c)=>{var state=DialogueState(i,c);if(!state.Children["running"].Boolean)throw new InvalidOperationException("Dialogue is not running.");var choices=DialogueNode(state).Children.GetValueOrDefault("choices");int index=(int)N(i,c,"index");if(choices==null||index<0||index>=choices.Items.Count)throw new ArgumentOutOfRangeException("index");EnterDialogue(state,choices.Items[index].Children.GetValueOrDefault("next")?.String??"");},[..args,NumberArg("index","Choice index (zero based)")]);
        Add(r,"dialogue.read","Dialogue","Read current speaker, text and choices",(i,c)=>{var state=DialogueState(i,c);Output(i,c,state.Children["running"].Boolean?DialogueNode(state):VariableValue.FromStructure());},[..args,TextArg("output","Result variable","DialogueLine")]);
        Test(r,"dialogue.running","Dialogue","Dialogue is running",(i,c)=>DialogueState(i,c).Children["running"].Boolean,args);
        Test(r,"dialogue.ended","Dialogue","Dialogue has ended",(i,c)=>!DialogueState(i,c).Children["running"].Boolean,args);
        Test(r,"dialogue.hasChoices","Dialogue","Dialogue awaits a choice",(i,c)=>{var state=DialogueState(i,c);return state.Children["running"].Boolean&&DialogueNode(state).Children.TryGetValue("choices",out var choices)&&choices.Items.Count>0;},args);
    }
}
