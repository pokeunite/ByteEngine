namespace ByteEngine.Core.VisualLogic;
public sealed class EventModuleDefinition
{
    public string Name{get;set;}="Event Module";
    public List<string> RequiredComponents{get;set;}=new();
    public List<EventRuleDefinition> Rules{get;set;}=new();
    public IReadOnlyList<string> Validate(IEnumerable<string> componentTypes)=>RequiredComponents.Where(r=>!componentTypes.Contains(r,StringComparer.OrdinalIgnoreCase)).Select(r=>$"{Name} requires {r}.").ToList();
}
public sealed class EventRuleDefinition { public List<VisualInstruction> Conditions{get;set;}=new(); public List<VisualInstruction> Actions{get;set;}=new(); }
public sealed class VisualInstruction { public string Id{get;set;}=string.Empty; public Dictionary<string,string> Arguments{get;set;}=new(); }
