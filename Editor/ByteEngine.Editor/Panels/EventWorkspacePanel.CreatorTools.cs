using ByteEngine.Core.VisualLogic;
using ByteEngine.Core.Variables;
using ImGuiNET;
using System.Numerics;
namespace ByteEngine.Editor.Panels;
internal sealed partial class EventWorkspacePanel
{
    private readonly Dictionary<Guid,string> _functionJsonDrafts=new();
    private string _functionJsonError="";
    private readonly Dictionary<string,string> _collectionConstantDrafts=new();
    private void DrawCollectionConstant(VisualInstruction instruction,string argument,VariableValue value)
    {
        string key=instruction.InstanceId+"/"+argument;
        string text=_collectionConstantDrafts.GetValueOrDefault(key,VariableJson.Write(value));
        if(ImGui.InputTextMultiline("JSON value",ref text,65536,new Vector2(-1,120)))_collectionConstantDrafts[key]=text;
        if(ImGui.Button("Apply JSON value"))
        {try{var parsed=VariableJson.Parse(text);if(parsed.Type!=value.Type)throw new FormatException("Keep the selected collection type.");RecordHistory("Edit Collection Argument");instruction.Arguments[argument]=new EventValue{Constant=parsed};_dirty=true;_functionJsonError="";}catch(Exception error){_functionJsonError=error.Message;}}
        if(_functionJsonError.Length>0)ImGui.TextWrapped(_functionJsonError);
    }
    private void DrawFunctionDefaults(EventRuleDefinition rule)
    {
        if(!_functionJsonDrafts.TryGetValue(rule.Id,out var text))text=VariableJson.Write(rule.FunctionDefaults);
        if(ImGui.InputTextMultiline("Parameter defaults JSON",ref text,32768,new Vector2(-1,100)))_functionJsonDrafts[rule.Id]=text;
        if(ImGui.Button("Apply parameter defaults"))
        {
            try {var value=VariableJson.Parse(text);if(value.Type!=VariableType.Structure)throw new FormatException("Use a JSON object with named parameters.");RecordHistory("Edit Function Parameters");rule.FunctionDefaults=value;_dirty=true;_functionJsonError="";}
            catch(Exception error){_functionJsonError=error.Message;}
        }
        if(_functionJsonError.Length>0)ImGui.TextWrapped(_functionJsonError);
    }
}
