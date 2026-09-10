using ByteEngine.Core.VisualLogic;
namespace ByteEngine.Core.Animation;
public sealed class AnimationEventSheet
{
    public string Name{get;set;}="Animation Events";
    public Guid TargetBlueprintId{get;set;}
    public List<EventRuleDefinition> Rules{get;set;}=new();
}
