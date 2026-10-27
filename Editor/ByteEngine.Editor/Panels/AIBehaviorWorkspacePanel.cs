using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.AI;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ImGuiNET;
namespace ByteEngine.Editor.Panels;

/// <summary>Readable outline authoring with node ordering, safe reparenting and live per-agent inspection.</summary>
internal sealed partial class AIBehaviorWorkspacePanel : IDisposable
{
    private readonly EditorDocumentManager _documents;
    private readonly Func<Scene?> _scene;
    private readonly AssetRecord _asset;
    private readonly EditorDocumentId _id;
    private BehaviorTreeDefinition _tree;
    private Guid _selected, _debugActor;
    private bool _dirty, _closePrompt, _advanced;
    private string _error="", _search="";
    private readonly Stack<string> _undo=[],_redo=[];
    private Action? _pending;
    public AIBehaviorWorkspacePanel(EditorDocumentManager documents, AssetRecord asset, Func<Scene?> scene)
    {
        _documents=documents;_asset=asset;_scene=scene;_tree=BehaviorTreeSerializer.Load(asset.FullPath);_selected=_tree.Root.Children.FirstOrDefault()?.Id??_tree.Root.Id;
        _id=new(EditorDocumentType.AIBehavior,asset.Guid.ToString("N"));
        documents.RegisterOrFocus(new EditorDocument(_id,"AI: "+_tree.Name,()=>{},RequestClose,Save,Reload,()=>_dirty));
    }
    public void Draw()
    {
        bool open=true;
        ImGui.Begin("AI Behaviour: "+_tree.Name+(_dirty?" *":"")+"###AIBehaviorWorkspace",ref open);
        if(ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))_documents.Activate(_id);
        if(ImGui.Button("Save"))Save();ImGui.SameLine();if(ImGui.Button("Revert"))Reload();
        ImGui.SameLine();ImGui.BeginDisabled(_undo.Count==0);if(ImGui.Button("Undo"))History(_undo,_redo);ImGui.EndDisabled();
        ImGui.SameLine();ImGui.BeginDisabled(_redo.Count==0);if(ImGui.Button("Redo"))History(_redo,_undo);ImGui.EndDisabled();
        if(ImGui.GetIO().KeyCtrl&&ImGui.IsKeyPressed(ImGuiKey.S,false))Save();
        ImGui.SameLine();ImGui.TextDisabled(_dirty?"Unsaved changes":"Saved asset");
        if(_error.Length>0)ImGui.TextWrapped(_error);
        ImGui.Separator();
        if(ImGui.BeginTabBar("##aiTabs"))
        {
            if(ImGui.BeginTabItem("Behaviour")){DrawBehavior();ImGui.EndTabItem();}
            if(ImGui.BeginTabItem("Background services")){DrawServices();ImGui.EndTabItem();}
            if(ImGui.BeginTabItem("Memory")){DrawMemory();ImGui.EndTabItem();}
            if(ImGui.BeginTabItem("Test / Live view")){DrawDebugger(true);ImGui.EndTabItem();}
            ImGui.EndTabBar();
        }
        ImGui.End();_pending?.Invoke();_pending=null;
        if(!open)RequestClose();
        if(_closePrompt){ImGui.OpenPopup("Unsaved AI behaviour");_closePrompt=false;}
        if(ImGui.BeginPopupModal("Unsaved AI behaviour",ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Save this behaviour before closing?");
            if(ImGui.Button("Save")&&Save()){Close();ImGui.CloseCurrentPopup();}ImGui.SameLine();
            if(ImGui.Button("Discard")){_dirty=false;Close();ImGui.CloseCurrentPopup();}ImGui.SameLine();
            if(ImGui.Button("Cancel"))ImGui.CloseCurrentPopup();ImGui.EndPopup();
        }
    }
    private void DrawBehavior()
    {
        ImGui.TextUnformatted("How this enemy behaves");
        ImGui.SameLine();ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(),ImGui.GetWindowWidth()-210));
        if(ImGui.Button(_advanced?"Back to simple rules":"Advanced tree"))_advanced=!_advanced;
        if(_advanced){DrawAdvancedTree();return;}
        ImGui.TextDisabled("Rules run from top to bottom. The first matching rule takes control.");
        ImGui.Spacing();
        float available=ImGui.GetContentRegionAvail().X;
        ImGui.BeginChild("##simpleRules",new Vector2(Math.Max(430,available-350),0));
        if(ImGui.Button("+ Add rule"))ImGui.OpenPopup("##newRule");
        if(ImGui.BeginPopup("##newRule"))
        {
            if(ImGui.MenuItem("When a target is nearby"))AddRule(BehaviorTest.HasTarget,"Target nearby");
            if(ImGui.MenuItem("When a target is close"))AddRule(BehaviorTest.WithinDistance,"Close-range attack");
            if(ImGui.MenuItem("When health is low"))AddRule(BehaviorTest.HealthBelow,"Low-health response");
            ImGui.EndPopup();
        }
        ImGui.Spacing();
        var rules=_tree.Root.Kind==BehaviorNodeKind.Priority?_tree.Root.Children.ToArray():new[]{_tree.Root};
        for(int i=0;i<rules.Length;i++)DrawRuleCard(rules[i],i);
        if(rules.Length==0)ImGui.TextWrapped("Add a rule, choose when it runs, then add the actions it should perform.");
        ImGui.EndChild();ImGui.SameLine();
        ImGui.BeginChild("##simpleSettings",new Vector2(0,0),ImGuiChildFlags.Borders);
        ImGui.SeparatorText("EDIT SELECTED ITEM");DrawDetails();
        ImGui.Spacing();ImGui.Separator();var errors=_tree.Validate();foreach(var error in errors.Take(5))ImGui.TextWrapped(error);
        if(errors.Count==0)ImGui.TextDisabled("Changes are ready to save.");
        ImGui.Spacing();ImGui.SeparatorText("PREVIEW");DrawDebugger(false);ImGui.EndChild();
    }
    private void DrawRuleCard(BehaviorNode rule,int order)
    {
        ImGui.PushID(rule.Id.ToString());
        var group=rule.Kind==BehaviorNodeKind.Gate&&rule.Children.Count==1?rule.Children[0]:rule;
        int count=All(group).Count(n=>!n.IsComposite);
        float height=Math.Max(175,100+count*46);
        ImGui.BeginChild("##rule",new Vector2(-8,height),ImGuiChildFlags.Borders);
        var colors=new[]{new Vector4(.3f,.7f,1,1),new Vector4(.73f,.57f,1,1),new Vector4(.3f,.83f,.68f,1)};
        ImGui.TextColored(colors[order%3],(order+1).ToString("00")+"  "+rule.Name);
        if(_tree.Root.Kind==BehaviorNodeKind.Priority)
        {
            ImGui.SameLine();if(ImGui.SmallButton("Up")){_selected=rule.Id;Reorder(-1);}ImGui.SameLine();if(ImGui.SmallButton("Down")){_selected=rule.Id;Reorder(1);}
            ImGui.SameLine();if(ImGui.SmallButton("Remove")){_selected=rule.Id;_pending=Delete;}
        }
        ImGui.Separator();
        if(ImGui.BeginTable("##whenDo",2,ImGuiTableFlags.BordersInnerV|ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("When",ImGuiTableColumnFlags.WidthStretch,.37f);ImGui.TableSetupColumn("Do",ImGuiTableColumnFlags.WidthStretch,.63f);
            ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.TextColored(new(.4f,.75f,1,1),"WHEN");
            string condition=rule.Kind==BehaviorNodeKind.Gate?ConditionText(rule):"Always";
            if(ImGui.Selectable(condition+"##condition",_selected==rule.Id,ImGuiSelectableFlags.None,new Vector2(0,48)))_selected=rule.Id;
            ImGui.TableNextColumn();ImGui.TextColored(new(1,.69f,.35f,1),"DO");
            DrawSimpleSteps(group,0);
            if(CanContain(group))
            {
                if(ImGui.SmallButton("+ Add step")){_selected=group.Id;ImGui.OpenPopup("##addStep");}
                if(ImGui.BeginPopup("##addStep"))
                {
                    ImGui.SetNextItemWidth(240);ImGui.InputTextWithHint("##findStep","Find an action...",ref _search,80);
                    foreach(var kind in Enum.GetValues<BehaviorNodeKind>().Where(k=>k is not (BehaviorNodeKind.Gate or BehaviorNodeKind.Sequence or BehaviorNodeKind.Priority or BehaviorNodeKind.Cooldown)))
                        if(Label(kind).Contains(_search,StringComparison.OrdinalIgnoreCase)&&ImGui.MenuItem(Label(kind)))Add(kind);
                    ImGui.EndPopup();
                }
            }
            ImGui.EndTable();
        }
        ImGui.EndChild();ImGui.Spacing();ImGui.PopID();
    }
    private void DrawSimpleSteps(BehaviorNode node,int depth)
    {
        if(node.Kind==BehaviorNodeKind.Sequence){foreach(var child in node.Children.ToArray())DrawSimpleSteps(child,depth);return;}
        ImGui.PushID(node.Id.ToString());
        string caption=node.Kind==BehaviorNodeKind.Gate?"Only when "+ConditionText(node):node.Name;
        var status=DebugAgent()?.NodeStatus.GetValueOrDefault(node.Id)??BehaviorStatus.Idle;
        if(status==BehaviorStatus.Running)ImGui.PushStyleColor(ImGuiCol.Text,new Vector4(.4f,.85f,1,1));
        if(ImGui.Selectable(caption+"\n"+StepSummary(node)+"##step",_selected==node.Id,ImGuiSelectableFlags.None,new Vector2(0,42)))_selected=node.Id;
        if(status==BehaviorStatus.Running)ImGui.PopStyleColor();
        if(ImGui.BeginPopupContextItem("##stepMenu"))
        {
            if(ImGui.MenuItem("Move up")){_selected=node.Id;Reorder(-1);}
            if(ImGui.MenuItem("Move down")){_selected=node.Id;Reorder(1);}
            if(ImGui.MenuItem("Remove step")){_selected=node.Id;_pending=Delete;}
            ImGui.EndPopup();
        }
        if(node.Children.Count>0){ImGui.Indent(14);foreach(var child in node.Children.ToArray())DrawSimpleSteps(child,depth+1);ImGui.Unindent(14);}
        ImGui.PopID();
    }
    private void AddRule(BehaviorTest test,string name)
    {
        Checkpoint();
        if(_tree.Root.Kind!=BehaviorNodeKind.Priority)_tree.Root=new(){Kind=BehaviorNodeKind.Priority,Name="Choose behaviour",Children=[_tree.Root]};
        var rule=new BehaviorNode{Kind=BehaviorNodeKind.Gate,Name=name,Test=test,Amount=.3f,Distance=2,Children=[new(){Kind=BehaviorNodeKind.Sequence,Name="Actions",Children=[new(){Kind=BehaviorNodeKind.Wait,Name="Pause briefly",Duration=.5f}]}]};
        _tree.Root.Children.Insert(0,rule);_selected=rule.Id;
    }
    private static string ConditionText(BehaviorNode n)
    {
        string text=n.Test switch {
            BehaviorTest.HasMemory=>"A last-known position is remembered",BehaviorTest.HeardNoise=>"A noise was heard",BehaviorTest.AttackReserved=>"An attack slot is reserved",BehaviorTest.HasTarget=>"A target is nearby",BehaviorTest.TargetAlive=>"The target is alive",BehaviorTest.WithinDistance=>$"Target is within {n.Distance:0.#} m",BehaviorTest.OutsideDistance=>$"Target is farther than {n.Distance:0.#} m",BehaviorTest.LineOfSight=>"The target is visible",BehaviorTest.HealthBelow=>$"Health is below {n.Amount*100:0}%",BehaviorTest.MemoryTrue=>$"{n.Key} is true",BehaviorTest.MemoryNumberAtLeast=>$"{n.Key} is at least {n.Amount:0.#}",_=>n.Test.ToString()};
        return n.Invert?"NOT: "+text:text;
    }
    private static string StepSummary(BehaviorNode n)=>n.Kind switch {
        BehaviorNodeKind.EnemyCombat=>"Uses this enemy's attack, dodge and movement settings",
        BehaviorNodeKind.Wait=>$"Wait {n.Duration:0.##} seconds",BehaviorNodeKind.MoveToTarget=>$"Approach at {n.Speed:0.#} m/s; stop {n.Distance:0.#} m away",BehaviorNodeKind.DamageTarget=>$"Deal {n.Amount:0.#} damage within {n.Distance:0.#} m",BehaviorNodeKind.Signal=>$"Tell Event Sheets: {n.Key}",BehaviorNodeKind.WaitForMemory=>$"Continue when {n.Key} is true",BehaviorNodeKind.Retreat=>$"Move back to {n.Distance:0.#} m",BehaviorNodeKind.Cooldown=>$"Available again after {n.Duration:0.#} seconds",BehaviorNodeKind.StopMovement=>"Pause movement before the next action",_=>Label(n.Kind)};
    private void DrawAdvancedTree()
    {
        float width=ImGui.GetContentRegionAvail().X;
        ImGui.BeginChild("##palette",new Vector2(205,0),ImGuiChildFlags.Borders);
        ImGui.SeparatorText("ADD A TASK");ImGui.SetNextItemWidth(-1);ImGui.InputTextWithHint("##search","Search tasks...",ref _search,80);
        ImGui.TextWrapped("Select a group, then add a child. Top branches have priority.");
        foreach(var kind in Enum.GetValues<BehaviorNodeKind>())
        {
            string label=Label(kind);if(!label.Contains(_search,StringComparison.OrdinalIgnoreCase))continue;
            ImGui.BeginDisabled(Selected() is not {} parent||!CanContain(parent));
            if(ImGui.Button(label,new Vector2(-1,0)))Add(kind);
            ImGui.EndDisabled();if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip(Description(kind));
        }
        ImGui.EndChild();ImGui.SameLine();
        ImGui.BeginChild("##outline",new Vector2(Math.Max(270,width-575),0),ImGuiChildFlags.Borders);
        ImGui.SeparatorText("DECISION TREE");
        ImGui.TextWrapped("Drag a row onto a group to nest it. Use Up / Down to change priority.");
        ImGui.BeginDisabled(Selected()==_tree.Root);
        if(ImGui.Button("Up"))Reorder(-1);ImGui.SameLine();if(ImGui.Button("Down"))Reorder(1);ImGui.SameLine();if(ImGui.Button("Delete subtree"))Delete();ImGui.EndDisabled();
        ImGui.Separator();DrawNode(_tree.Root);ImGui.EndChild();ImGui.SameLine();
        ImGui.BeginChild("##properties",new Vector2(0,0),ImGuiChildFlags.Borders);
        ImGui.SeparatorText("SELECTED NODE");DrawDetails();
        ImGui.SeparatorText("VALIDATION");var errors=_tree.Validate();
        if(errors.Count==0)ImGui.TextColored(new(.35f,.85f,.55f,1),"Ready to save");
        foreach(var e in errors.Take(8))ImGui.TextWrapped(e);
        ImGui.SeparatorText("LIVE STATUS");DrawDebugger(false);ImGui.EndChild();
    }
    private unsafe void DrawNode(BehaviorNode node)
    {
        ImGui.PushID(node.Id.ToString());
        var flags=ImGuiTreeNodeFlags.OpenOnArrow|ImGuiTreeNodeFlags.SpanAvailWidth|ImGuiTreeNodeFlags.DefaultOpen;
        if(node.Id==_selected)flags|=ImGuiTreeNodeFlags.Selected;
        if(node.Children.Count==0)flags|=ImGuiTreeNodeFlags.Leaf|ImGuiTreeNodeFlags.NoTreePushOnOpen;
        var live=DebugAgent();var status=live?.NodeStatus.GetValueOrDefault(node.Id)??BehaviorStatus.Idle;
        if(status!=BehaviorStatus.Idle)ImGui.PushStyleColor(ImGuiCol.Text,StatusColor(status));
        bool expanded=ImGui.TreeNodeEx("##node",flags,"["+Label(node.Kind)+"] "+node.Name);
        if(status!=BehaviorStatus.Idle)ImGui.PopStyleColor();
        if(ImGui.IsItemClicked())_selected=node.Id;
        if(ImGui.IsItemHovered())ImGui.SetTooltip(Description(node.Kind)+(status!=BehaviorStatus.Idle?"\nLast decision: "+status:""));
        if(node!=_tree.Root&&ImGui.BeginDragDropSource())
        {
            Span<byte> bytes=stackalloc byte[16];node.Id.TryWriteBytes(bytes);fixed(byte* p=bytes)ImGui.SetDragDropPayload("BYTE_AI_NODE",(nint)p,16,ImGuiCond.Once);
            ImGui.Text(node.Name);ImGui.EndDragDropSource();
        }
        if(CanContain(node)&&ImGui.BeginDragDropTarget())
        {
            var payload=ImGui.AcceptDragDropPayload("BYTE_AI_NODE");
            if(payload.NativePtr!=null&&payload.Delivery&&payload.DataSize==16)
            {
                var bytes=new byte[16];System.Runtime.InteropServices.Marshal.Copy(payload.Data,bytes,0,16);var id=new Guid(bytes);
                _pending=()=>Reparent(id,node.Id);
            }
            ImGui.EndDragDropTarget();
        }
        if(expanded&&node.Children.Count>0){foreach(var child in node.Children.ToArray())DrawNode(child);ImGui.TreePop();}
        ImGui.PopID();
    }
    private void DrawDetails()
    {
        var n=Selected();if(n==null)return;
        string name=n.Name;if(ImGui.InputText("Name",ref name,140)){Checkpoint();n.Name=name;}
        ImGui.TextWrapped(Description(n.Kind));
        DrawExtendedDetails(n);
        if(n.Kind==BehaviorNodeKind.EnemyCombat)ImGui.TextWrapped("Select the enemy in the scene to edit Simple Enemy AI: damage, range, movement, dodge, cover and attack timings. This action continues until its rule stops matching.");
        if(n.Kind==BehaviorNodeKind.Gate)
        {
            var test=n.Test;int index=(int)test;if(ImGui.Combo("Condition",ref index,new[]{"A target is nearby","The target is alive","Target is within distance","Target is farther away","The target is visible","Health is low","A memory flag is true","Memory number meets threshold","A last-known position is remembered","A noise was heard","An attack slot is reserved"},11)){Checkpoint();n.Test=(BehaviorTest)index;}
            bool invert=n.Invert;if(ImGui.Checkbox("Invert condition (NOT)",ref invert)){Checkpoint();n.Invert=invert;}
            if(n.Test is BehaviorTest.WithinDistance or BehaviorTest.OutsideDistance)Float("Distance",n.Distance,v=>n.Distance=v);
            if(n.Test is BehaviorTest.MemoryTrue or BehaviorTest.MemoryNumberAtLeast)Text("Memory key",n.Key,v=>n.Key=v);
            if(n.Test is BehaviorTest.MemoryNumberAtLeast or BehaviorTest.HealthBelow)Float("Threshold",n.Amount,v=>n.Amount=v);
            if(n.Test==BehaviorTest.HealthBelow)ImGui.TextWrapped("Health is a fraction: 0.3 means 30%.");
        }
        if(n.Kind is BehaviorNodeKind.Wait or BehaviorNodeKind.Cooldown)Float("Seconds",n.Duration,v=>n.Duration=v);
        if(n.Kind is BehaviorNodeKind.MoveToTarget or BehaviorNodeKind.MoveToPoint or BehaviorNodeKind.Retreat)
        {
            Float("Movement speed",n.Speed,v=>n.Speed=v);Float(n.Kind==BehaviorNodeKind.Retreat?"Desired range":"Stop distance",n.Distance,v=>n.Distance=v);Float("Timeout (0 = none)",n.Timeout,v=>n.Timeout=v);
            if(n.Kind==BehaviorNodeKind.MoveToPoint)Text("Position memory key",n.Key,v=>n.Key=v);
        }
        if(n.Kind is BehaviorNodeKind.SetMemory or BehaviorNodeKind.Signal or BehaviorNodeKind.WaitForMemory)Text(n.Kind==BehaviorNodeKind.Signal?"Signal name":"Memory key",n.Key,v=>n.Key=v);
        if(n.Kind==BehaviorNodeKind.SetMemory)ValueEditor(n.Value,v=>n.Value=v);
        if(n.Kind==BehaviorNodeKind.WaitForMemory){Float("Timeout (0 = none)",n.Timeout,v=>n.Timeout=v);ImGui.TextWrapped("Waits until the chosen memory boolean is true. Clear it before starting a repeated task.");}
        if(n.Kind==BehaviorNodeKind.DamageTarget){Float("Damage",n.Amount,v=>n.Amount=v);Float("Reach",n.Distance,v=>n.Distance=v);bool sight=n.RequireLineOfSight;if(ImGui.Checkbox("Require line of sight",ref sight)){Checkpoint();n.RequireLineOfSight=sight;}}
        string notes=n.Notes;ImGui.Text("Notes");if(ImGui.InputTextMultiline("##notes",ref notes,2000,new Vector2(-1,90))){Checkpoint();n.Notes=notes;}
    }
    private void DrawMemory()
    {
        ImGui.TextWrapped("Defaults are copied separately for each enemy. Built-in perception keys: HasTarget, TargetDistance, HasLineOfSight, HealthFraction and TargetPosition. Those keys are refreshed by perception.");
        if(ImGui.Button("+ Memory key")){Checkpoint();int i=1;while(_tree.Blackboard.Any(k=>k.Name=="Key"+i))i++;_tree.Blackboard.Add(new(){Name="Key"+i});}
        foreach(var entry in _tree.Blackboard.ToArray())
        {
            ImGui.PushID(_tree.Blackboard.IndexOf(entry));string name=entry.Name;if(ImGui.InputText("Key",ref name,100)){Checkpoint();entry.Name=name;}
            ValueEditor(entry.Value,v=>entry.Value=v);if(ImGui.Button("Remove key")){Checkpoint();_tree.Blackboard.Remove(entry);}ImGui.Separator();ImGui.PopID();
        }
    }
    private void ValueEditor(VariableValue value,Action<VariableValue> assign)
    {
        var types=new[]{VariableType.Boolean,VariableType.Number,VariableType.String,VariableType.Vector3};int index=Array.IndexOf(types,value.Type);
        if(ImGui.Combo("Value type",ref index,new[]{"Boolean","Number","Text","Position"},4)){Checkpoint();assign(index switch{0=>VariableValue.FromBoolean(),1=>VariableValue.FromNumber(),2=>VariableValue.FromString(),_=>VariableValue.FromVector3(Vector3.Zero)});return;}
        if(value.Type==VariableType.Boolean){bool v=value.Boolean;if(ImGui.Checkbox("Value",ref v)){Checkpoint();assign(VariableValue.FromBoolean(v));}}
        if(value.Type==VariableType.Number){float v=(float)value.Number;if(ImGui.InputFloat("Value",ref v)){Checkpoint();assign(VariableValue.FromNumber(v));}}
        if(value.Type==VariableType.String){string v=value.String;if(ImGui.InputText("Value",ref v,500)){Checkpoint();assign(VariableValue.FromString(v));}}
        if(value.Type==VariableType.Vector3){var v=value.Vector3;if(ImGui.DragFloat3("Value",ref v,.1f)){Checkpoint();assign(VariableValue.FromVector3(v));}}
    }
    private AIBehavior3D? DebugAgent()=>_scene()?.GameObjects.Where(o=>o.ActiveInHierarchy).Select(o=>o.GetComponent<AIBehavior3D>()).FirstOrDefault(a=>a!=null&&a.GameObject.Id==_debugActor);
    private void DrawDebugger(bool full)
    {
        var agents=_scene()?.GameObjects.Select(o=>o.GetComponent<AIBehavior3D>()).OfType<AIBehavior3D>().Where(a=>a.Behavior.Guid==_asset.Guid||a.Behavior.CachedProjectPath==_asset.ProjectPath).ToArray()??[];
        if(agents.Length==0){ImGui.TextWrapped("Assign this asset to an AI Behaviour component and enter Play to inspect an enemy.");return;}
        if(!agents.Any(a=>a.GameObject.Id==_debugActor))_debugActor=agents[0].GameObject.Id;
        var ai=agents.First(a=>a.GameObject.Id==_debugActor);
        if(ImGui.BeginCombo("Inspect enemy",ai.GameObject.Name)){foreach(var a in agents)if(ImGui.Selectable(a.GameObject.Name+"##"+a.GameObject.Id,a==ai))_debugActor=a.GameObject.Id;ImGui.EndCombo();}
        ImGui.TextWrapped("Status: "+ai.Status+" | "+ai.CurrentTask);
        ImGui.TextWrapped("Awareness: "+ai.Awareness);
        if(ai.LastError.Length>0)ImGui.TextWrapped(ai.LastError);
        if(full)
        {
            if(ImGui.Button(ai.Paused?"Resume AI":"Pause AI"))ai.SetPaused(!ai.Paused);ImGui.SameLine();if(ImGui.Button("Restart from saved asset"))ai.Restart();
            ImGui.TextWrapped("Save asset edits, then restart this actor to load them. Pausing affects this actor only.");
            foreach(var pair in ai.Blackboard)ImGui.Text(pair.Key+" = "+ValueText(pair.Value));
            DrawDecisionHistory(ai);
            ImGui.SeparatorText("RECENT SIGNALS");foreach(var signal in ai.Signals.TakeLast(12))ImGui.Text(signal.Sequence+": "+signal.Name);
        }
    }
    private static string ValueText(VariableValue v)=>v.Type switch{VariableType.Boolean=>v.Boolean.ToString(),VariableType.Number=>v.Number.ToString("0.##"),VariableType.Vector3=>v.Vector3.ToString(),_=>v.String};
    private static Vector4 StatusColor(BehaviorStatus s)=>s switch{BehaviorStatus.Running=>new(.35f,.8f,1,1),BehaviorStatus.Success=>new(.4f,.85f,.5f,1),BehaviorStatus.Failure=>new(1,.5f,.4f,1),_=>Vector4.One};
    private void Float(string label,float value,Action<float> assign){if(ImGui.DragFloat(label,ref value,.05f,0,100000)){Checkpoint();assign(value);}}
    private void Text(string label,string value,Action<string> assign){if(ImGui.InputText(label,ref value,140)){Checkpoint();assign(value);}}
    private BehaviorNode? Selected()=>Find(_tree.Root,_selected);
    private static BehaviorNode? Find(BehaviorNode root,Guid id)=>root.Id==id?root:root.Children.Select(c=>Find(c,id)).FirstOrDefault(n=>n!=null);
    private BehaviorNode? Parent(Guid id)=>All(_tree.Root).FirstOrDefault(n=>n.Children.Any(c=>c.Id==id));
    private static IEnumerable<BehaviorNode> All(BehaviorNode n){yield return n;foreach(var c in n.Children)foreach(var sub in All(c))yield return sub;}
    private static bool CanContain(BehaviorNode n)=>n.IsComposite&&(!(n.Kind is BehaviorNodeKind.Gate or BehaviorNodeKind.Cooldown)||n.Children.Count==0);
    private void Add(BehaviorNodeKind kind){var parent=Selected();if(parent==null||!CanContain(parent))return;Checkpoint();var n=new BehaviorNode{Kind=kind,Name=Label(kind)};parent.Children.Add(n);_selected=n.Id;}
    private void Delete(){var parent=Parent(_selected);if(parent==null)return;Checkpoint();parent.Children.RemoveAll(c=>c.Id==_selected);_selected=parent.Id;}
    private void Reorder(int direction){var parent=Parent(_selected);if(parent==null)return;int i=parent.Children.FindIndex(c=>c.Id==_selected),j=i+direction;if(j<0||j>=parent.Children.Count)return;Checkpoint();(parent.Children[i],parent.Children[j])=(parent.Children[j],parent.Children[i]);}
    private void Reparent(Guid id,Guid parentId){var n=Find(_tree.Root,id);var p=Find(_tree.Root,parentId);var old=Parent(id);if(n==null||p==null||old==null||!CanContain(p)||Find(n,parentId)!=null)return;Checkpoint();old.Children.Remove(n);p.Children.Add(n);_selected=n.Id;}
    private string Snapshot()=>JsonSerializer.Serialize(_tree,BehaviorTreeSerializer.Options);
    private void Checkpoint(){_undo.Push(Snapshot());if(_undo.Count>80){var keep=_undo.Take(80).Reverse().ToArray();_undo.Clear();foreach(var s in keep)_undo.Push(s);}_redo.Clear();_dirty=true;}
    private void History(Stack<string> source,Stack<string> destination){if(source.Count==0)return;destination.Push(Snapshot());_tree=JsonSerializer.Deserialize<BehaviorTreeDefinition>(source.Pop(),BehaviorTreeSerializer.Options)!;_dirty=true;}
    private bool Save(){try{BehaviorTreeSerializer.Save(_asset.FullPath,_tree);_dirty=false;_error="";return true;}catch(Exception e){_error=e.Message;return false;}}
    private void Reload(){try{_tree=BehaviorTreeSerializer.Load(_asset.FullPath);_selected=_tree.Root.Children.FirstOrDefault()?.Id??_tree.Root.Id;_undo.Clear();_redo.Clear();_dirty=false;_error="";}catch(Exception e){_error=e.Message;}}
    private void RequestClose(){if(_dirty)_closePrompt=true;else Close();}
    private void Close()=>_documents.Unregister(_id);
    public void Dispose()=>Close();
    private static string Label(BehaviorNodeKind kind)=>kind switch{BehaviorNodeKind.Wander=>"Wander near starting position",BehaviorNodeKind.FindPosition=>"Choose tactical position",BehaviorNodeKind.Search=>"Search last known area",BehaviorNodeKind.ReserveAttack=>"Reserve squad attack",BehaviorNodeKind.ReleaseAttack=>"Release squad attack",BehaviorNodeKind.ReleasePosition=>"Release tactical position",BehaviorNodeKind.ChooseAttack=>"Choose attack",BehaviorNodeKind.Telegraph=>"Warn before attack",BehaviorNodeKind.CommitAttack=>"Commit and lock aim",BehaviorNodeKind.Strike=>"Execute attack",BehaviorNodeKind.Recover=>"Recover after attack",BehaviorNodeKind.SubBehavior=>"Run reusable behaviour",BehaviorNodeKind.EnemyCombat=>"Use enemy combat pattern",BehaviorNodeKind.Priority=>"Choose first available",BehaviorNodeKind.Sequence=>"Do in order",BehaviorNodeKind.Gate=>"If condition",BehaviorNodeKind.Cooldown=>"Cooldown",BehaviorNodeKind.MoveToTarget=>"Move to target",BehaviorNodeKind.MoveToPoint=>"Move to memory position",BehaviorNodeKind.WaitForMemory=>"Wait for memory",BehaviorNodeKind.SetMemory=>"Set memory",BehaviorNodeKind.DamageTarget=>"Damage target",BehaviorNodeKind.StopMovement=>"Stop moving",BehaviorNodeKind.FaceTarget=>"Face target",BehaviorNodeKind.Signal=>"Send event signal",_=>kind.ToString()};
    private static string Description(BehaviorNodeKind kind)=>kind switch{
        BehaviorNodeKind.EnemyCombat=>"Runs the existing enemy combat settings under this behaviour. Edit attacks, movement, dodge, cover and boss settings on the enemy combat component. This preserves the established enemy role.",
        BehaviorNodeKind.Priority=>"Try children from top to bottom. The first running or successful child wins. Higher priorities are reconsidered each decision.",
        BehaviorNodeKind.Sequence=>"Run children in order. Resume the current task until it finishes. A failed task fails the sequence.",
        BehaviorNodeKind.Gate=>"Run one child while a condition is true. If it becomes false, its running tasks are cancelled.",
        BehaviorNodeKind.Cooldown=>"Run one child, then block it for the chosen duration after success.",
        BehaviorNodeKind.MoveToTarget=>"Follow the target using Navigation Agent. Requires a baked navigation region.",
        BehaviorNodeKind.Wander=>"Choose reachable destinations near this instance's original placement. Higher-priority sight or hearing rules can interrupt.",
        BehaviorNodeKind.MoveToPoint=>"Navigate to a world position stored in the blackboard.",
        BehaviorNodeKind.Retreat=>"Request a destination away from the target until the desired range is reached. It is not a cover-search task.",
        BehaviorNodeKind.Signal=>"Emit a named signal once when reached. Event Sheets can respond with animations, audio or custom attacks.",
        BehaviorNodeKind.WaitForMemory=>"Wait for an Event Sheet to set a boolean memory key true. Timeout fails the task.",
        BehaviorNodeKind.DamageTarget=>"Apply damage once if target is still in reach and visible. Do not also damage it in a signal event.",
        BehaviorNodeKind.Wait=>"Remain on this task for the given simulation seconds.",
        _=>"Editable task. Use memory and Event Sheets to connect it to your own gameplay."};
}
