using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ByteEngine.Core.AI;
using ByteEngine.Core.Assets;
namespace ByteEngine.Editor.Assistant;
internal sealed partial class AssistantTools
{
    private void CreateAIBehavior(JsonElement data,List<string> files)
    {
        var asset=NewAsset(data,".byteai",files);
        BehaviorTreeDefinition definition;
        if(data.TryGetProperty("tree",out var tree))
        {
            if(tree.GetRawText().Length>128*1024)throw new InvalidOperationException("AI tree authoring is limited to 128 KB.");
            var json=JsonNode.Parse(tree.GetRawText(),documentOptions:new JsonDocumentOptions{MaxDepth=128})!;
            void ResolveAssets(JsonNode? node)
            {
                if(node is JsonObject obj)
                {
                    foreach(var item in obj.ToArray())
                    {
                        if(item.Key.Equals("SubBehavior",StringComparison.OrdinalIgnoreCase)&&item.Value is JsonValue scalar&&scalar.TryGetValue<string>(out var value))
                        {
                            string path=_assetAliases.GetValueOrDefault(value,value);if(!File.Exists(Inside(path))||!path.EndsWith(".byteai",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Sub-behaviour must be an existing .byteai asset.");
                            obj[item.Key]=JsonSerializer.SerializeToNode(new AssetReference(path),BehaviorTreeSerializer.Options);
                        }
                        else ResolveAssets(item.Value);
                    }
                }
                else if(node is JsonArray array)foreach(var child in array)ResolveAssets(child);
            }
            ResolveAssets(json);
            var strict=new JsonSerializerOptions(BehaviorTreeSerializer.Options){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
            definition=json.Deserialize<BehaviorTreeDefinition>(strict)??throw new InvalidOperationException("Missing AI definition.");
        }
        else if(Text(data,"source").Length>0)
        {
            string source=_assetAliases.GetValueOrDefault(Text(data,"source"),Text(data,"source"));
            if(!source.EndsWith(".byteai",StringComparison.OrdinalIgnoreCase)||new FileInfo(Inside(source)).Length>512*1024)throw new InvalidOperationException("Choose an existing AI asset within the inspection budget.");
            definition=BehaviorTreeSerializer.Load(Inside(source));
        }
        else
        {
            string preset=Text(data,"preset","Melee");
            if(preset.Equals("Basic",StringComparison.OrdinalIgnoreCase))definition=BehaviorTreeDefinition.CreateExample();
            else if(preset.Equals("AttackStages",StringComparison.OrdinalIgnoreCase))definition=AIBehaviorPresets.AttackStages();
            else
            {
                bool flank=preset.Equals("Flanker",StringComparison.OrdinalIgnoreCase);
                if(!Enum.TryParse<AIBehaviorRole>(flank?"Melee":preset,true,out var role)||!Enum.IsDefined(role))throw new InvalidOperationException("AI preset must be Basic, AttackStages, Melee, Flanker, Ranged, Caster, Charger or Boss.");
                string shared=Text(data,"sharedAttack");AssetReference reference=AssetReference.Empty;
                if(shared.Length>0){shared=_assetAliases.GetValueOrDefault(shared,shared);if(!shared.EndsWith(".byteai",StringComparison.OrdinalIgnoreCase)||!File.Exists(Inside(shared)))throw new InvalidOperationException("Shared attack must be an existing .byteai asset.");BehaviorTreeSerializer.Load(Inside(shared));reference=new(shared);}
                definition=AIBehaviorPresets.Create(role,reference,flank);
                if(reference.IsEmpty)
                {
                    void Inline(BehaviorNode node){for(int i=0;i<node.Children.Count;i++){var child=node.Children[i];if(child.Kind==BehaviorNodeKind.SubBehavior&&child.SubBehavior.IsEmpty)node.Children[i]=AIBehaviorPresets.AttackStages().Root;else Inline(child);}}
                    Inline(definition.Root);
                }
            }
        }
        void CheckReferences(BehaviorNode node)
        {
            if(node.Kind==BehaviorNodeKind.SubBehavior)
            {
                string path=node.SubBehavior.ProjectPath;
                if(path.Length==0&&EditorProjectContext.Active is {} project&&Path.GetFullPath(project.ProjectRoot)==Path.GetFullPath(projectRoot))path=project.AssetDatabase.Resolve(node.SubBehavior)?.ProjectPath??"";
                if(path.Length==0||!path.EndsWith(".byteai",StringComparison.OrdinalIgnoreCase)||!File.Exists(Inside(path)))throw new InvalidOperationException("Sub-behaviour references must resolve to existing project .byteai files.");
            }
            foreach(var child in node.Children)CheckReferences(child);
        }
        var errors=definition.Validate();if(errors.Count>0)throw new InvalidOperationException(string.Join("; ",errors));
        CheckReferences(definition.Root);
        definition.Name=Name(data,"name");BehaviorTreeSerializer.Save(asset.Path,definition);FinishAsset(asset,AssetType.AIBehavior);
    }
    private static object AIAssetSchema()=>new{
        kind="create_ai_behavior",extension=".byteai",
        data="{name,path,preset?:Basic|AttackStages|Melee|Flanker|Ranged|Caster|Charger|Boss,sharedAttack?:existingPathOr$asset:alias,source?:existingAIPath,tree?:nativeDefinition}. tree takes precedence, then source, then preset. Never overwrites. Presets inline attack stages unless sharedAttack is supplied. IDs may be omitted; native defaults generate them. Conditions and numeric settings remain editable in the WHEN/DO editor.",
        definition=Schema(typeof(BehaviorTreeDefinition)),node=Schema(typeof(BehaviorNode)),nodeKinds=Enum.GetNames<BehaviorNodeKind>(),tests=Enum.GetNames<BehaviorTest>(),interruptions=Enum.GetNames<BehaviorInterruption>(),positionKinds=Enum.GetNames<AIPositionKind>(),service=Schema(typeof(BehaviorService)),serviceKinds=Enum.GetNames<AIServiceKind>(),
        limits="Native validation: <=512 nodes, depth<=48, <=32 services, finite numbers and valid child counts. Custom tree <=128 KB. No arbitrary code. Use SubBehavior as an existing project path or $asset alias. Native memory Value uses {Type:'Boolean',Boolean:true}, {Type:'Number',Number:3}, {Type:'String',String:'text'} or {Type:'Vector3',Vector3:{X:1,Y:0,Z:2}}."
    };
}
