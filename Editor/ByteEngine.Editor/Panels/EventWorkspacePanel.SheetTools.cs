using System.Globalization;
using System.Numerics;
using ByteEngine.Core.VisualLogic;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed partial class EventWorkspacePanel
{
    private string _conditionMenuSearch = "", _actionMenuSearch = "";
    private Guid? _sheetConditionParentId, _sheetRangeAnchor, _sheetFlashId;
    private Guid? _sheetColorEditId, _sheetGroupEditId;
    private double _sheetFlashUntil;
    private readonly Vector4[] _sheetPalette = [new(.26f,.57f,.82f,1),new(.54f,.42f,.78f,1),new(.20f,.64f,.56f,1),new(.80f,.50f,.24f,1),new(.72f,.38f,.50f,1),new(.42f,.58f,.35f,1)];
    private sealed record InstructionMenuEntry(string Id,string DisplayName,string Category);
    private IEnumerable<InstructionMenuEntry> InstructionMenuEntries(bool condition)
    {
        var entries=(condition
            ? _registry.Conditions.Select(d=>new InstructionMenuEntry(d.Id,d.DisplayName,ByteEngine.Core.Plugins.ByteEnginePluginRegistry.GetVisualLogicMenuPath(d.Id,d.Category,d.TargetComponent,d.MenuGroup)))
            : _registry.Actions.Select(d=>new InstructionMenuEntry(d.Id,d.DisplayName,ByteEngine.Core.Plugins.ByteEnginePluginRegistry.GetVisualLogicMenuPath(d.Id,d.Category,d.TargetComponent,d.MenuGroup)))).ToList();
        InstructionMenuEntry[] special=condition
            ? [new("logic.and","AND - all conditions","Logic"),new("logic.or","OR - any condition","Logic")]
            : [new("flow.branch","If / Else","Flow"),new("object.spawnEmpty","Spawn Empty Object","Object"),new("object.spawnBlueprint","Spawn Blueprint","Object")];
        foreach(var entry in special)if(!entries.Any(e=>e.Id==entry.Id))entries.Add(entry);
        return entries;
    }

    private void AddPickedInstruction(EventRuleDefinition rule,bool condition,string id)
    {
        RecordHistory(condition?"Add Condition":"Add Action");
        var before=rule.Conditions.Select(i=>i.InstanceId).ToHashSet();
        var created=CreateInstruction(id);
        if(condition){rule.Conditions.Add(created);ConnectCondition(rule,created.InstanceId);FixSheetConditionParent(rule,before);}
        else {rule.Actions.Add(created);AppendActionToExecutionFlow(rule,created);}
        _dirty=true;
    }

    private void DrawOrganizedInstructionMenu(EventRuleDefinition rule,bool condition)
    {
        var entries=InstructionMenuEntries(condition).Where(d=>!IsLegacyRaycastCreationDefinition(d.Id))
            .Select(d=>(Entry:d,Path:d.Category.Split('/',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))).ToArray();
        void Draw(IEnumerable<(InstructionMenuEntry Entry,string[] Path)> nodes,int depth)
        {
            foreach(var node in nodes.Where(n=>n.Path.Length<=depth).OrderBy(n=>n.Entry.DisplayName))
                if(ImGui.MenuItem(node.Entry.DisplayName+"##"+node.Entry.Id)){AddPickedInstruction(rule,condition,node.Entry.Id);ImGui.CloseCurrentPopup();}
            foreach(var group in nodes.Where(n=>n.Path.Length>depth).GroupBy(n=>n.Path[depth]).OrderBy(g=>g.Key))
            {
                if(!ImGui.BeginMenu(group.Key))continue;
                Draw(group,depth+1);ImGui.EndMenu();
            }
        }
        Draw(entries,0);
    }

    private bool DrawInstructionSearch(EventRuleDefinition rule, bool condition)
    {
        ref string search = ref (condition ? ref _conditionMenuSearch : ref _actionMenuSearch);
        if (ImGui.IsWindowAppearing()) { search = ""; ImGui.SetKeyboardFocusHere(); }
        ImGui.SetNextItemWidth(360);
        ImGui.InputTextWithHint("##InstructionSearch",condition ? "Search conditions..." : "Search actions...",ref search,160);
        ImGui.Separator();
        if (string.IsNullOrWhiteSpace(search)) return false;
        var words=search.Split(' ',StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var entries=InstructionMenuEntries(condition);
        var matches=entries.Where(d=>!IsLegacyRaycastCreationDefinition(d.Id)&&words.All(w=>(d.DisplayName+" "+d.Category+" "+d.Id).Contains(w,StringComparison.OrdinalIgnoreCase)))
            .OrderBy(d=>d.DisplayName).ToArray();
        ImGui.BeginChild("##SearchResults",new Vector2(380,Math.Clamp(matches.Length*30,40,330)));
        if(matches.Length==0) ImGui.TextDisabled("No matching results");
        foreach(var entry in matches)
        {
            if(!ImGui.Selectable(entry.DisplayName+"  /  "+entry.Category+"##"+entry.Id)) continue;
            AddPickedInstruction(rule,condition,entry.Id);
            ImGui.CloseCurrentPopup();
            break;
        }
        ImGui.EndChild();
        return true;
    }

    private void FixSheetConditionParent(EventRuleDefinition rule, HashSet<Guid> before)
    {
        if(_showEventGraph) return;
        foreach(var created in rule.Conditions.Where(i=>!before.Contains(i.InstanceId)))
        {
            var parent=rule.Conditions.FirstOrDefault(i=>i.InstanceId==_sheetConditionParentId && IsLogicGate(i));
            if(parent!=null && parent!=created)
            {
                rule.ConnectedConditionIds.Remove(created.InstanceId);
                parent.ConditionInputIds.Add(created.InstanceId);
            }
            SelectSheetItem(created.InstanceId);
        }
    }

    private VisualInstruction? SheetConditionParent(EventRuleDefinition rule)
    {
        var selected=rule.Conditions.FirstOrDefault(i=>_selectedGraphNodes.Contains(i.InstanceId));
        if(selected!=null)
        {
            if(IsLogicGate(selected)) return selected;
            var parent=rule.Conditions.FirstOrDefault(i=>IsLogicGate(i)&&i.ConditionInputIds.Contains(selected.InstanceId));
            if(parent!=null) return parent;
        }
        var roots=rule.Conditions.Where(i=>IsLogicGate(i)&&rule.ConnectedConditionIds.Contains(i.InstanceId)).ToArray();
        return roots.Length==1?roots[0]:null;
    }

    private void SelectSheetEvent(Guid id, bool navigate)
    {
        var rules=EnumerateSheetRules(_module!.Rules).ToList();
        var io=ImGui.GetIO();
        if(io.KeyShift && _sheetRangeAnchor.HasValue)
        {
            int a=rules.FindIndex(r=>r.Id==_sheetRangeAnchor),b=rules.FindIndex(r=>r.Id==id);
            if(!io.KeyCtrl) _selectedGraphNodes.Clear();
            if(a>=0&&b>=0) foreach(var rule in rules.Skip(Math.Min(a,b)).Take(Math.Abs(a-b)+1)) _selectedGraphNodes.Add(rule.Id);
        }
        else if(io.KeyCtrl)
        { if(!_selectedGraphNodes.Add(id)) _selectedGraphNodes.Remove(id);_sheetRangeAnchor=id; }
        else {SelectSheetItem(id);_sheetRangeAnchor=id;}
        if(navigate)
        {
            _requestFrameNodeId=id;_sheetFlashId=id;_sheetFlashUntil=ImGui.GetTime()+2.2;
            _collapsedSheetEvents.Remove(id);
            foreach(var group in _module.EditorGroups.Where(g=>g.MemberIds.Contains(id))) _collapsedSheetGroups.Remove(group.Id);
        }
    }

    private Vector4 SheetEventColor(EventRuleDefinition rule, int number)
    {
        if(uint.TryParse(rule.EditorHeaderColor,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out uint rgb))
            return new((rgb>>16&255)/255f,(rgb>>8&255)/255f,(rgb&255)/255f,1);
        return _sheetPalette[(number-1)%_sheetPalette.Length];
    }

    private void DrawSheetEventMenu(EventRuleDefinition rule, int number)
    {
        if(!ImGui.BeginPopupContextItem("EventMenu")) {if(_sheetColorEditId==rule.Id)_sheetColorEditId=null;return;}
        if(!_selectedGraphNodes.Contains(rule.Id)) SelectSheetItem(rule.Id);
        if(ImGui.MenuItem("Rename")) RequestRenameEvent(rule);
        if(ImGui.BeginMenu("Header color"))
        {
            var c=SheetEventColor(rule,number);var rgb=new Vector3(c.X,c.Y,c.Z);
            if(ImGui.ColorPicker3("##HeaderColor",ref rgb))
            {
                if(_sheetColorEditId!=rule.Id){RecordHistory("Change Event Color");_sheetColorEditId=rule.Id;}
                rule.EditorHeaderColor=$"{(int)(rgb.X*255):X2}{(int)(rgb.Y*255):X2}{(int)(rgb.Z*255):X2}";
                _dirty=true;
            }
            if(ImGui.MenuItem("Reset to automatic color")){RecordHistory("Reset Event Color");rule.EditorHeaderColor="";_dirty=true;}
            ImGui.EndMenu();
        }
        ImGui.Separator();
        if(ImGui.MenuItem("Add comment around event")) CreateSheetGroup(false,rule);
        int count=_module!.Rules.Count(r=>_selectedGraphNodes.Contains(r.Id));
        if(ImGui.MenuItem($"Group selected events ({count})", "",false,count>=2)) CreateSheetGroup(true,rule);
        foreach(var group in _module.EditorGroups.Where(g=>g.MemberIds.Contains(rule.Id)).ToArray())
            if(ImGui.MenuItem("Ungroup / remove "+group.Title+"##"+group.Id)) {RecordHistory("Ungroup Events");_module.EditorGroups.Remove(group);_dirty=true;}
        ImGui.Separator();
        if(ImGui.MenuItem("Delete selected events")) DeleteSelectedGraphNodes();
        ImGui.EndPopup();
    }

    private readonly HashSet<Guid> _collapsedSheetGroups = new();
    private Guid? _renameSheetGroupId;
    private string _renameSheetGroupText="";
    private bool _openSheetRename;

    private void RequestSheetGroupRename(EventGraphGroupDefinition group)
    {_renameSheetGroupId=group.Id;_renameSheetGroupText=group.Title;_openSheetRename=true;}

    private void DrawSheetRenamePopup()
    {
        if(_openSheetRename){ImGui.OpenPopup("Rename group or comment");_openSheetRename=false;}
        if(!ImGui.BeginPopup("Rename group or comment"))return;
        if(ImGui.IsWindowAppearing())ImGui.SetKeyboardFocusHere();
        ImGui.TextUnformatted("Name / comment");ImGui.SetNextItemWidth(360);
        bool commit=ImGui.InputText("##RenameText",ref _renameSheetGroupText,512,ImGuiInputTextFlags.EnterReturnsTrue);
        commit=ImGui.Button("Rename")||commit;ImGui.SameLine();
        bool cancel=ImGui.Button("Cancel")||ImGui.IsKeyPressed(ImGuiKey.Escape);
        if(commit && !string.IsNullOrWhiteSpace(_renameSheetGroupText))
        {
            var group=_module!.EditorGroups.FirstOrDefault(g=>g.Id==_renameSheetGroupId);
            if(group!=null){RecordHistory("Rename Group / Comment");group.Title=_renameSheetGroupText.Trim();_dirty=true;}
            ImGui.CloseCurrentPopup();
        }
        else if(cancel)ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }
    private void CreateSheetGroup(bool group, EventRuleDefinition clicked)
    {
        var members=group?_module!.Rules.Where(r=>_selectedGraphNodes.Contains(r.Id)).ToArray():[clicked];
        RecordHistory(group?"Group Events":"Comment Event");
        _module!.EditorGroups.Add(new(){Title=group?"Event group":"Comment",SheetGroup=group,
            MemberIds=members.SelectMany(r=>new[]{r.Id}.Concat(r.Conditions.Concat(r.Actions).Select(i=>i.InstanceId))).ToList()});
        _dirty=true;
    }

    private void DrawSheetGroupHeader(EventGraphGroupDefinition group)
    {
        ImGui.PushID(group.Id.ToString());
        bool collapsed=_collapsedSheetGroups.Contains(group.Id);
        ImGui.PushStyleColor(ImGuiCol.ChildBg,new Vector4(.15f,.17f,.21f,1));
        ImGui.BeginChild("##GroupHeader",new Vector2(0,48),ImGuiChildFlags.Borders);
        if(group.SheetGroup)
        {
            if(ImGui.SmallButton(collapsed?">":"v")) {if(collapsed)_collapsedSheetGroups.Remove(group.Id);else _collapsedSheetGroups.Add(group.Id);}
            ImGui.SameLine();
        }
        ImGui.TextUnformatted((group.SheetGroup?"GROUP: ":"COMMENT: ")+group.Title);
        if(ImGui.IsItemHovered())
        {ImGui.SetTooltip("Double-click to rename. Right-click for more options.");if(ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))RequestSheetGroupRename(group);}
        if(ImGui.BeginPopupContextItem("GroupMenu"))
        {
            string title=group.Title;
            ImGui.SetNextItemWidth(300);
            if(ImGui.InputText("Text",ref title,512)){if(_sheetGroupEditId!=group.Id){RecordHistory("Edit Comment / Group");_sheetGroupEditId=group.Id;}group.Title=title;_dirty=true;}
            if(ImGui.MenuItem(group.SheetGroup?"Ungroup (keep events)":"Remove comment (keep event)"))
            {RecordHistory("Remove Event Group");_module!.EditorGroups.Remove(group);_dirty=true;}
            ImGui.EndPopup();
        }
        else if(_sheetGroupEditId==group.Id) _sheetGroupEditId=null;
        ImGui.EndChild();ImGui.PopStyleColor();ImGui.PopID();
    }
}
