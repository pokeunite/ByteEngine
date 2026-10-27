using ByteEngine.Core.AI;
using ByteEngine.Core.Assets;
using ImGuiNET;
namespace ByteEngine.Editor.Panels;
internal sealed partial class AIBehaviorWorkspacePanel
{
    public EditorProjectContext? Project {get;set;}
    private string _historyFilter="";
    private void DrawExtendedDetails(BehaviorNode n)
    {
        if(n.Kind is BehaviorNodeKind.Gate or BehaviorNodeKind.Priority)
        {
            int mode=(int)n.Interruption;
            if(ImGui.Combo("Interruption",ref mode,new[]{"Recheck immediately","Finish this branch","Finish a committed attack"},3)){Checkpoint();n.Interruption=(BehaviorInterruption)mode;}
        }
        if(n.Kind==BehaviorNodeKind.FindPosition)
        {
            int kind=(int)n.PositionKind;if(ImGui.Combo("Position type",ref kind,new[]{"Cover","Firing lane","Flank","Surround"},4)){Checkpoint();n.PositionKind=(AIPositionKind)kind;}
            Text("Save position as",n.Key,v=>n.Key=v);Float("Preferred target distance",n.Distance,v=>n.Distance=v);Float("Search radius",n.SearchRadius,v=>n.SearchRadius=v);Float("Minimum spacing",n.Spacing,v=>n.Spacing=v);Float("Hold position seconds",n.Duration,v=>n.Duration=v);
        }
        if(n.Kind==BehaviorNodeKind.Wander){Float("Wander radius around starting position",n.SearchRadius,v=>n.SearchRadius=v);Float("Walking speed",n.Speed,v=>n.Speed=v);Float("Choose destination every seconds",n.Duration,v=>n.Duration=v);Text("Destination memory key",n.Key,v=>n.Key=v);ImGui.TextWrapped("Wanders near this instance's placed starting position. Sight and hearing branches above it can interrupt.");}
        if(n.Kind==BehaviorNodeKind.Search){Float("Search seconds",n.Duration,v=>n.Duration=v);Float("Search radius",n.SearchRadius,v=>n.SearchRadius=v);Float("Search speed",n.Speed,v=>n.Speed=v);}
        if(n.Kind==BehaviorNodeKind.ReserveAttack){Float("Maximum hold seconds",n.Duration,v=>n.Duration=v);Float("Wait timeout",n.Timeout,v=>n.Timeout=v);ImGui.TextWrapped("The enemy component's Squad and Maximum Attackers control sharing.");}
        if(n.Kind==BehaviorNodeKind.ChooseAttack)
        {
            string[] options=["Automatic","Primary","Charge","Slam","Feint"];int option=Array.IndexOf(options,n.AttackOption);if(ImGui.Combo("Attack choice",ref option,options,options.Length)){Checkpoint();n.AttackOption=options[option];}
        }
        if(n.Kind is BehaviorNodeKind.Telegraph or BehaviorNodeKind.CommitAttack or BehaviorNodeKind.Strike or BehaviorNodeKind.Recover)ImGui.TextWrapped("Uses the enemy combat component's authored timing, damage, effects and charge/slam settings. Keep these stages in order after Choose attack.");
        if(n.Kind==BehaviorNodeKind.SubBehavior)
        {
            if(Project!=null&&ImGui.BeginCombo("Reusable behaviour",n.SubBehavior.ProjectPath.Length>0?Path.GetFileNameWithoutExtension(n.SubBehavior.ProjectPath):"Choose asset..."))
            {
                foreach(var asset in Project.AssetDatabase.Assets.Where(a=>a.Type==AssetType.AIBehavior&&a.Guid!=_asset.Guid).OrderBy(a=>a.ProjectPath))
                    if(ImGui.Selectable(asset.ProjectPath,n.SubBehavior.Guid==asset.Guid)){Checkpoint();n.SubBehavior=new(asset.Guid,asset.ProjectPath);}
                ImGui.EndCombo();
            }
            else if(Project==null){string path=n.SubBehavior.ProjectPath;if(ImGui.InputText("Asset path",ref path,300)){Checkpoint();n.SubBehavior=string.IsNullOrWhiteSpace(path)?AssetReference.Empty:new(path);}}
        }
    }
    private void DrawServices()
    {
        ImGui.TextWrapped("Services update memory or send signals while another task is running. Perception already runs in the background; add only the extra checks you need.");
        if(ImGui.Button("+ Add background service")){Checkpoint();_tree.Services.Add(new(){Name="Low health watch",Kind=AIServiceKind.HealthFlag,Key="LowHealth",Interval=.5f});}
        foreach(var service in _tree.Services.ToArray())
        {
            ImGui.PushID(service.Id.ToString());ImGui.Separator();
            bool enabled=service.Enabled;if(ImGui.Checkbox("Enabled",ref enabled)){Checkpoint();service.Enabled=enabled;}
            Text("Name",service.Name,v=>service.Name=v);int kind=(int)service.Kind;
            if(ImGui.Combo("Service",ref kind,new[]{"Update memory value","Watch health threshold","Send event signal","Refresh tactical position"},4)){Checkpoint();service.Kind=(AIServiceKind)kind;}
            Float("Interval seconds",service.Interval,v=>service.Interval=v);Text(service.Kind==AIServiceKind.Signal?"Signal name":"Memory key",service.Key,v=>service.Key=v);
            if(service.Kind==AIServiceKind.HealthFlag)Float("Health fraction threshold",service.Threshold,v=>service.Threshold=v);
            if(service.Kind==AIServiceKind.SetMemory)ValueEditor(service.Value,v=>service.Value=v);
            if(service.Kind==AIServiceKind.PositionQuery){int query=(int)service.PositionKind;if(ImGui.Combo("Query",ref query,new[]{"Cover","Firing lane","Flank","Surround"},4)){Checkpoint();service.PositionKind=(AIPositionKind)query;}Float("Radius",service.Radius,v=>service.Radius=v);}
            if(ImGui.Button("Remove service")){Checkpoint();_tree.Services.Remove(service);}ImGui.PopID();
        }
    }
    private void DrawDecisionHistory(AIBehavior3D ai)
    {
        ImGui.SeparatorText("WHY THE AI DID THAT");ImGui.SetNextItemWidth(260);ImGui.InputTextWithHint("##historyFilter","Filter decisions...",ref _historyFilter,150);
        ImGui.SameLine();if(ImGui.Button("Export history"))
        {
            using var dialog=new System.Windows.Forms.SaveFileDialog{Filter="AI decision history (*.json)|*.json",FileName="AI-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json"};
            if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK){try{File.WriteAllText(dialog.FileName,ai.ExportDecisionHistory());}catch(Exception e){_error=e.Message;}}
        }
        ImGui.SameLine();if(ImGui.Button("Clear history"))ai.ClearDecisionHistory();
        ImGui.BeginChild("##decisionHistory",new System.Numerics.Vector2(0,260),ImGuiChildFlags.Borders);
        foreach(var entry in ai.DecisionHistory.Reverse().Where(e=>_historyFilter.Length==0||(e.Result+e.Reason).Contains(_historyFilter,StringComparison.OrdinalIgnoreCase)))
        {
            ImGui.TextDisabled(entry.Time.ToString("0.00")+"s  "+entry.Result);ImGui.TextWrapped(entry.Reason);ImGui.Separator();
        }
        ImGui.EndChild();
    }
}
