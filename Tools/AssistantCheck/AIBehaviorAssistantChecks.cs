using System.Text.Json;
using ByteEngine.Core.AI;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Navigation;
using ByteEngine.Editor;
using ByteEngine.Editor.Assistant;
static class AIBehaviorAssistantChecks
{
    public static void Run(EditorState state,string root)
    {
        int count=0;
        void Check(bool value,string message){if(!value)throw new Exception(message);count++;Console.WriteLine("PASS "+message);}
        object Call(string tool,object args){using var doc=JsonDocument.Parse(JsonSerializer.Serialize(args));return new AssistantTools(state,root).Execute(tool,doc.RootElement);}
        object Apply(params object[] operations)=>Call("byte_apply",new{label="AI setup verification",operations});
        bool Throws(Action run){try{run();return false;}catch{return true;}}
        string Text(object value)=>AssistantTools.Serialize(value);
        Check(Text(AssistantKnowledge.Search("AI behaviour enemy setup")).Contains("BehaviorControlled=true"),"AI recipe explains native combat ownership");
        Check(Text(AssistantKnowledge.Search("perception hearing noise memory")).Contains("NoiseRadius"),"Perception recipe explains explicit gunshot hearing");
        Check(Text(Call("byte_catalog",new{kind="asset_types",search="AI behaviour"})).Contains("create_ai_behavior"),"Catalogue exposes AI authoring schema");
        Check(Text(Call("byte_catalog",new{kind="components",search="AIBehavior3D"})).Contains("PositionPathsPerQuery"),"Component catalogue includes current perception/tactical budgets");
        Check(Text(AssistantReference.Search("","Docs.AI-Perception-Tactics-and-Combat.md",1)).Contains("Separate"),"Build-matched AI guide is embedded and readable");
        Check(Text(AssistantReference.Search("","Plugins.KarlsonDemo.WeaponDefinition3D.cs",1)).Contains("WeaponDefinition3D"),"Current first-party weapon source is embedded");
        var selectedId=state.SelectedObject!.Id;
        Apply(
            new{kind="create_ai_behavior",target="",data=new{name="Shared attack",path="Assets/AI/Shared.byteai",preset="AttackStages"}},
            new{kind="create_ai_behavior",target="",data=new{name="Enemy role",path="Assets/AI/Enemy.byteai",preset="Melee",sharedAttack="$asset:Shared attack"}},
            new{kind="add_component",target="$selected",data=new{component="SimpleEnemyAI3D",properties=new{Enabled=true,BehaviorControlled=true}}},
            new{kind="add_component",target="$selected",data=new{component="NavigationAgent3D",properties=new{ExternallyDriven=false}}},
            new{kind="add_component",target="$selected",data=new{component="AIBehavior3D",properties=new{Behavior="$asset:Enemy role",PerceptionMemory=true,MaximumAttackers=2}}});
        var owner=state.EditorScene.FindGameObject(selectedId)!;var ai=owner.GetComponent<AIBehavior3D>()!;
        Check(ai.Behavior.ProjectPath=="Assets/AI/Enemy.byteai"&&ai.PerceptionMemory,"One batch authors and binds a valid AI asset through an alias");
        Check(owner.GetComponent<SimpleEnemyAI3D>() is {Enabled:true,BehaviorControlled:true}&&owner.GetComponent<NavigationAgent3D>() is {ExternallyDriven:false},"Assistant setup keeps combat enabled and navigation ownership correct");
        var definition=BehaviorTreeSerializer.Load(root+"/Assets/AI/Enemy.byteai");
        IEnumerable<BehaviorNode> Nodes(BehaviorNode n){yield return n;foreach(var child in n.Children)foreach(var sub in Nodes(child))yield return sub;}
        Check(definition.Validate().Count==0&&Nodes(definition.Root).Any(n=>n.Kind==BehaviorNodeKind.SubBehavior&&n.SubBehavior.ProjectPath=="Assets/AI/Shared.byteai"),"Preset keeps editable reusable staged attacks");
        Check(Text(Call("byte_inspect",new{asset="Assets/AI/Enemy.byteai"})).Contains("Fight a visible target"),"Native AI inspection returns authored conditions and actions");
        state.Undo!.Undo(state);Check(state.EditorScene.FindGameObject(selectedId)!.GetComponent<AIBehavior3D>()==null&&File.Exists(root+"/Assets/AI/Enemy.byteai"),"Scene Undo restores bindings and retains authored AI assets for recovery");
        Apply(new{kind="create_ai_behavior",target="",data=new{name="Inline ranged",path="Assets/AI/Inline.byteai",preset="Ranged"}});
        var inline=BehaviorTreeSerializer.Load(root+"/Assets/AI/Inline.byteai");Check(inline.Validate().Count==0&&!Nodes(inline.Root).Any(n=>n.Kind==BehaviorNodeKind.SubBehavior),"A role preset works without forcing a second shared asset");
        var original=File.ReadAllBytes(root+"/Assets/AI/Enemy.byteai");
        Apply(new{kind="create_ai_behavior",target="",data=new{name="Variant",path="Assets/AI/Variant.byteai",source="Assets/AI/Enemy.byteai"}});
        Check(File.ReadAllBytes(root+"/Assets/AI/Enemy.byteai").SequenceEqual(original)&&BehaviorTreeSerializer.Load(root+"/Assets/AI/Variant.byteai").Name=="Variant","Existing AI can be copied into a new variant without overwriting the source");
        Check(Throws(()=>Apply(new{kind="create_ai_behavior",target="",data=new{name="Overwrite",path="Assets/AI/Enemy.byteai",preset="Basic"}})),"AI authoring refuses overwrite");
        Check(Throws(()=>Apply(new{kind="create_ai_behavior",target="",data=new{name="Escape",path="../Escape.byteai",preset="Basic"}})),"AI authoring rejects paths outside project Assets");
        Check(Throws(()=>Apply(new{kind="create_ai_behavior",target="",data=new{name="Invalid",path="Assets/AI/Invalid.byteai",tree=new{Root=new{Kind="Gate",Children=Array.Empty<object>()}}}}))&&!File.Exists(root+"/Assets/AI/Invalid.byteai"),"Invalid child structure fails with no leftover AI asset");
        Check(Throws(()=>Apply(new{kind="create_ai_behavior",target="",data=new{name="Invented",path="Assets/AI/Invented.byteai",tree=new{Root=new{Kind="Wait",ImaginarySetting=true}}}})),"Custom AI authoring rejects invented node settings");
        Check(Throws(()=>Apply(new{kind="create_ai_behavior",target="",data=new{name="Rollback",path="Assets/AI/Rollback.byteai",preset="Melee"}},new{kind="set_property",target="$selected",data=new{component="MissingComponent",property="Nope",value=true}}))&&!File.Exists(root+"/Assets/AI/Rollback.byteai")&&!File.Exists(root+"/Assets/AI/Rollback.byteai.meta"),"Later batch failure rolls back AI file and metadata");
        Apply(new{kind="create_ai_behavior",target="",data=new{name="Custom service",path="Assets/AI/Service.byteai",tree=new{Root=new{Kind="Wait",Duration=2},Services=new[]{new{Kind="HealthFlag",Key="NeedsHelp",Interval=.5,Threshold=.3}}}}});
        Check(BehaviorTreeSerializer.Load(root+"/Assets/AI/Service.byteai").Services.Single().Key=="NeedsHelp","Custom native tree can author background services");
        Check(Throws(()=>Apply(new{kind="create_ai_behavior",target="",data=new{name="Bad reference",path="Assets/AI/BadReference.byteai",tree=new{Root=new{Kind="SubBehavior",SubBehavior="Assets/AI/Missing.byteai"}}}})),"Missing sub-behaviour references are rejected before saving");
        Console.WriteLine(count+" assistant AI checks passed without a model/API request");
    }
}
