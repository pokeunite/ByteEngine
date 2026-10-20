using System.Numerics;
using ByteEngine.Core.VisualLogic;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed partial class EventWorkspacePanel
{
    private bool _showEventGraph;
    private bool _showSheetOutline = true;
    private bool _showSheetDetails = true;
    private bool _sheetShowModuleSettings;
    private float _sheetOutlineWidth = 230;
    private float _sheetDetailsWidth = 300;
    private readonly HashSet<Guid> _collapsedSheetEvents = new();
    private readonly HashSet<Guid> _collapsedSheetLogic = new();
    private static readonly Vector4 SheetConditionColor = new(.34f, .68f, .96f, 1);
    private static readonly Vector4 SheetActionColor = new(1, .63f, .29f, 1);

    private static IEnumerable<EventRuleDefinition> EnumerateSheetRules(IEnumerable<EventRuleDefinition> rules)
    {
        foreach (var rule in rules)
        {
            yield return rule;
            foreach (var child in EnumerateSheetRules(rule.SubEvents)) yield return child;
        }
    }

    private static void DrawSheetSplitter(string id, ref float width, float direction)
    {
        ImGui.InvisibleButton(id, new Vector2(4, MathF.Max(1, ImGui.GetContentRegionAvail().Y)));
        if (ImGui.IsItemHovered() || ImGui.IsItemActive()) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
        if (ImGui.IsItemActive()) width = Math.Clamp(width + ImGui.GetIO().MouseDelta.X * direction, 150, 600);
    }

    private static string SheetTitle(string title) => title.Replace("\u2014", "-").Replace("\u2013", "-").Replace("\u2192", "to");

    private void DrawSheetOutline(float width)
    {
        ImGui.BeginChild("##SheetOutline",new Vector2(width,0),ImGuiChildFlags.Borders);
        ImGui.SeparatorText("EVENTS");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##SheetSearch","Find event...",ref _graphOutlineSearch,96);
        ImGui.Spacing();
        int number=0;
        foreach(var rule in EnumerateSheetRules(_module!.Rules))
        {
            number++;
            string title=SheetTitle(GetRuleDisplayName(rule));
            if(!MatchesOutlineSearch(title)&&!rule.Conditions.Concat(rule.Actions).Any(i=>MatchesOutlineSearch(GetSheetSentence(i,rule.Conditions.Contains(i),EditorState.Active)))) continue;
            ImGui.PushID(rule.Id.ToString());
            bool selected=_selectedGraphNodes.Contains(rule.Id)||rule.Conditions.Concat(rule.Actions).Any(i=>_selectedGraphNodes.Contains(i.InstanceId));
            Vector2 pos=ImGui.GetCursorScreenPos();
            float w=ImGui.GetContentRegionAvail().X;
            if(ImGui.Selectable("##Event",selected,ImGuiSelectableFlags.None,new Vector2(w,64)))
            {SelectSheetEvent(rule.Id,true);}
            if(ImGui.IsItemHovered()) ImGui.SetTooltip(title);
            var draw=ImGui.GetWindowDrawList();
            var textColor=ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
            draw.AddText(pos+new Vector2(7,6),ImGui.ColorConvertFloat4ToU32(SheetEventColor(rule,number)),$"{number:00}");
            // Keep the navigator compact; full sentences and settings live in the sheet.
            string label=title;
            while(label.Length>1&&ImGui.CalcTextSize(label).X>w-45) label=label[..^1];
            if(label.Length<title.Length) label=label.TrimEnd()+"...";
            draw.AddText(pos+new Vector2(34,6),ImGui.ColorConvertFloat4ToU32(textColor),label);
            string counts = $"{rule.Conditions.Count} condition{(rule.Conditions.Count == 1 ? "" : "s")} / {rule.Actions.Count} action{(rule.Actions.Count == 1 ? "" : "s")}";
            draw.AddText(pos+new Vector2(7,32),ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]),counts);
            ImGui.PopID();
        }
        ImGui.EndChild();
    }

    private void DrawEventSheet(EditorState? state)
    {
        if (_module == null) return;
        float width = ImGui.GetContentRegionAvail().X;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 6));
        ImGui.TextColored(SheetConditionColor, "CONDITIONS");
        ImGui.SameLine(width * .5f + 12);
        ImGui.TextColored(SheetActionColor, "ACTIONS");
        ImGui.TextDisabled("If these are true...");
        ImGui.SameLine(width * .5f + 12);
        ImGui.TextDisabled("Do this");
        ImGui.Spacing();
        ImGui.BeginChild("##ReadableEventSheet", Vector2.Zero, ImGuiChildFlags.None);
        ImGui.SetWindowFontScale(1.12f);
        int number = 0;
        foreach (var rule in _module.Rules.ToArray())
        {
            number++;
            var groups=_module.EditorGroups.Where(g=>g.MemberIds.Contains(rule.Id)).ToArray();
            Vector2 start=ImGui.GetCursorScreenPos();
            foreach(var group in groups)
                if(_module.Rules.FirstOrDefault(r=>group.MemberIds.Contains(r.Id))==rule) DrawSheetGroupHeader(group);
            if(groups.Any(g=>g.SheetGroup&&_collapsedSheetGroups.Contains(g.Id))) continue;
            foreach(var group in groups.Where(g=>g.SheetGroup&&_module.Rules.FirstOrDefault(r=>g.MemberIds.Contains(r.Id))!=rule))
                ImGui.TextDisabled("Group: "+group.Title);
            DrawSheetRule(rule,number,0,state);
            if(groups.Length>0)
                ImGui.GetWindowDrawList().AddRect(start, new Vector2(start.X+ImGui.GetContentRegionAvail().X,ImGui.GetCursorScreenPos().Y-6),ImGui.ColorConvertFloat4ToU32(new Vector4(.40f,.52f,.68f,.7f)),6);
        }
        if (ImGui.Button("+ Add event", new Vector2(-1, 36)))
            RequestAddEvent(GetDefaultRulePosition(_module.Rules.Count));
        if (_module.EditorLooseConditions.Count + _module.EditorLooseActions.Count > 0)
        {
            ImGui.SeparatorText("UNCONNECTED DRAFTS");
            foreach (var item in _module.EditorLooseConditions.Concat(_module.EditorLooseActions).ToArray())
                DrawSheetInstruction(new(item, 0, "Not connected", false), _module.EditorLooseConditions.Contains(item), state);
        }
        DrawSheetRenamePopup();
        ImGui.SetWindowFontScale(1);
        ImGui.EndChild();
        ImGui.PopStyleVar();
    }

    private void DrawSheetRule(EventRuleDefinition rule, int number, int depth, EditorState? state)
    {
        ImGui.PushID(rule.Id.ToString());
        bool collapsed = _collapsedSheetEvents.Contains(rule.Id);
        if (_requestFrameNodeId == rule.Id || rule.Conditions.Concat(rule.Actions).Any(i => i.InstanceId == _requestFrameNodeId))
        { ImGui.SetScrollFromPosY(ImGui.GetCursorPosY(),.15f); _sheetFlashId=rule.Id;_sheetFlashUntil=ImGui.GetTime()+2.2; _requestFrameNodeId = Guid.Empty; _collapsedSheetEvents.Remove(rule.Id); collapsed = false; }
        var conditions = EventSheetProjection.Conditions(rule,_collapsedSheetLogic);
        var actions = EventSheetProjection.Actions(rule);
        float columnWidth = MathF.Max(80, (ImGui.GetContentRegionAvail().X - 44) * .5f);
        float RowsHeight(List<EventSheetProjection.Row> rows, bool condition) => rows.Count == 0 ? 34 : rows.Sum(row =>
            row.Instruction == null ? ImGui.GetTextLineHeightWithSpacing() :
            MathF.Max(32, ImGui.CalcTextSize(GetSheetSentence(row.Instruction, condition, state) + (row.Note == null ? "" : " ("+row.Note+")"), false, MathF.Max(40,columnWidth - 24 - Math.Min(row.Depth,8)*14)).Y + 14) + ImGui.GetStyle().ItemSpacing.Y + (condition && row.Instruction != null && IsLogicGate(row.Instruction) && !_collapsedSheetLogic.Contains(row.Instruction.InstanceId) ? 32 : 0));
        float height = collapsed ? 48 : MathF.Max(RowsHeight(conditions,true),RowsHeight(actions,false)) + 98;
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 7);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 10));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(.105f,.12f,.14f,1));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(.23f,.27f,.31f,1));
        ImGui.BeginChild("##EventCard", new Vector2(0,height), ImGuiChildFlags.Borders, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        var accent=SheetEventColor(rule,number);
        Vector2 header=ImGui.GetWindowPos()+new Vector2(1,1);
        ImGui.GetWindowDrawList().AddRectFilled(header,header+new Vector2(ImGui.GetWindowWidth()-2,44),ImGui.ColorConvertFloat4ToU32(new Vector4(accent.X*.28f,accent.Y*.28f,accent.Z*.28f,1)),6);
        ImGui.GetWindowDrawList().AddRectFilled(header,header+new Vector2(4,44),ImGui.ColorConvertFloat4ToU32(accent),3);
        if (ImGui.SmallButton(collapsed ? ">" : "v"))
        { if (collapsed) _collapsedSheetEvents.Remove(rule.Id); else _collapsedSheetEvents.Add(rule.Id); }
        ImGui.SameLine();
        string title = $"{number:00}   " + (depth > 0 ? "Child: " : "") + SheetTitle(GetRuleDisplayName(rule));
        float titleWidth = MathF.Max(40, ImGui.GetContentRegionAvail().X - 100);
        if (ImGui.Selectable(title + "##Title", _selectedGraphNodes.Contains(rule.Id), ImGuiSelectableFlags.None, new Vector2(titleWidth,24))) SelectSheetEvent(rule.Id,false);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(title + "\nRight-click to rename or delete this event.");
        DrawSheetEventMenu(rule,number);
        ImGui.SameLine();
        bool enabled = rule.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled)) { RecordHistory("Enable Event"); rule.Enabled = enabled; _dirty = true; }
        if (!collapsed)
        {
            ImGui.Separator();
            ImGui.Spacing();
            if (ImGui.BeginTable("##EventColumns",2,ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.BordersInnerV))
            {
                ImGui.TableNextRow();ImGui.TableSetColumnIndex(0);
                if (conditions.Count == 0) ImGui.TextDisabled(rule.FunctionName.Length>0?"Function entry: "+rule.FunctionName:"Every update (no conditions)");
                foreach(var row in conditions) DrawSheetInstruction(row,true,state,rule);
                ImGui.PushStyleColor(ImGuiCol.Text,SheetConditionColor);
                var parent=SheetConditionParent(rule);
                string addLabel=parent==null?"+ Add condition":"+ Add inside "+parent.Id[6..].ToUpperInvariant();
                if(ImGui.Button(addLabel,new Vector2(0,28))) {_sheetConditionParentId=parent?.InstanceId;ImGui.OpenPopup("Add Condition");}
                if(parent!=null)
                {
                    ImGui.SameLine();
                    if(ImGui.SmallButton("Outside block")) {_sheetConditionParentId=null;ImGui.OpenPopup("Add Condition");}
                }
                ImGui.PopStyleColor();DrawConditionPicker(rule,rule.Conditions);
                ImGui.TableSetColumnIndex(1);
                if (actions.Count == 0) ImGui.TextDisabled("No actions yet");
                foreach(var row in actions) DrawSheetInstruction(row,false,state);
                ImGui.PushStyleColor(ImGuiCol.Text,SheetActionColor);
                if(ImGui.Button("+ Add action",new Vector2(0,28))) ImGui.OpenPopup("Add Action");
                ImGui.PopStyleColor();DrawActionPicker(rule,rule.Actions);
                ImGui.EndTable();
            }
        }
        ImGui.EndChild();ImGui.PopStyleColor(2);ImGui.PopStyleVar(2);
        if(_sheetFlashId==rule.Id && ImGui.GetTime()<_sheetFlashUntil)
        {
            var min=ImGui.GetItemRectMin();var max=ImGui.GetItemRectMax();
            float pulse=.35f+.65f*(float)Math.Abs(Math.Sin((ImGui.GetTime())*8));
            var draw=ImGui.GetWindowDrawList();
            for(int glow=3;glow>=0;glow--)
                draw.AddRect(min-new Vector2(glow),max+new Vector2(glow),ImGui.ColorConvertFloat4ToU32(new Vector4(accent.X,accent.Y,accent.Z,pulse/(glow+1))),7,ImDrawFlags.None,glow==0?2:1);
        }
        ImGui.Dummy(new Vector2(0,8));
        ImGui.PopID();
        if (!collapsed) foreach(var child in rule.SubEvents.ToArray()) DrawSheetRule(child,number,depth+1,state);
    }

    private void SelectSheetItem(Guid id)
    {
        _selectedGraphNodes.Clear();
        _selectedGraphNodes.Add(id);
    }

    private void DrawSheetInstruction(EventSheetProjection.Row row, bool condition, EditorState? state, EventRuleDefinition? owner = null)
    {
        if (row.Instruction is not { } item)
        { ImGui.TextDisabled(row.Note ?? ""); return; }
        // The same instruction can legitimately appear on two branch paths.
        ImGui.PushID(item.InstanceId.ToString());
        ImGui.PushID(ImGui.GetCursorPosY().ToString(System.Globalization.CultureInfo.InvariantCulture));
        float indent = Math.Min(row.Depth, 8) * 14;
        if (indent > 0) ImGui.Indent(indent);
        if(condition && IsLogicGate(item))
        {
            bool folded=_collapsedSheetLogic.Contains(item.InstanceId);
            if(ImGui.SmallButton(folded?">":"v"))
            {if(folded)_collapsedSheetLogic.Remove(item.InstanceId);else _collapsedSheetLogic.Add(item.InstanceId);}
            if(ImGui.IsItemHovered())ImGui.SetTooltip(folded?"Expand logic block":"Collapse logic block");
            ImGui.SameLine();
        }
        float width = MathF.Max(40, ImGui.GetContentRegionAvail().X - 8);
        string text = GetSheetSentence(item, condition, state);
        if (row.Note != null) text += "  (" + row.Note + ")";
        float height = MathF.Max(32, ImGui.CalcTextSize(text, false, width - 16).Y + 14);
        Vector2 origin = ImGui.GetCursorScreenPos();
        var trace = GetLiveTraceState(item.InstanceId);
        Vector4 color = condition ? SheetConditionColor : SheetActionColor;
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(color.X, color.Y, color.Z, .18f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(color.X, color.Y, color.Z, .12f));
        if (ImGui.Selectable("##Sentence", _selectedGraphNodes.Contains(item.InstanceId), ImGuiSelectableFlags.None, new Vector2(width, height)))
            SelectSheetItem(item.InstanceId);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Click to edit in Details. Right-click for more options.");
        if (ImGui.BeginPopupContextItem("SentenceMenu"))
        {
            if (ImGui.MenuItem("Edit settings")) SelectSheetItem(item.InstanceId);
            if (ImGui.MenuItem("Delete")) { SelectSheetItem(item.InstanceId); DeleteSelectedGraphNodes(); }
            ImGui.EndPopup();
        }
        ImGui.PopStyleColor(2);
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(origin + new Vector2(0, 5), origin + new Vector2(3, height - 5), ImGui.ColorConvertFloat4ToU32(color));
        var textColor = row.Connected ? ImGui.GetStyle().Colors[(int)ImGuiCol.Text] : ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];
        draw.AddText(ImGui.GetFont(), ImGui.GetFontSize(), origin + new Vector2(10, 7), ImGui.ColorConvertFloat4ToU32(textColor), text, width - 16);
        if (trace != VisualLogicTraceState.None)
        {
            Vector2 dot = origin + new Vector2(width - 7, 9);
            draw.AddCircleFilled(dot, 3, ImGui.ColorConvertFloat4ToU32(GetTraceColor(trace)));
        }
        if (row.Depth > 0)
            draw.AddLine(origin-new Vector2(7,0),origin+new Vector2(-7,height),ImGui.ColorConvertFloat4ToU32(new Vector4(color.X,color.Y,color.Z,.5f)),1);
        if(condition && owner!=null && IsLogicGate(item) && !_collapsedSheetLogic.Contains(item.InstanceId))
        {
            if(ImGui.SmallButton("+ Inside "+item.Id[6..].ToUpperInvariant()))
            {_sheetConditionParentId=item.InstanceId;ImGui.OpenPopup("Add Condition");}
            DrawConditionPicker(owner,owner.Conditions);
        }
        if (indent > 0) ImGui.Unindent(indent);
        ImGui.PopID(); ImGui.PopID();
    }

    private string GetSheetSentence(VisualInstruction item, bool condition, EditorState? state)
    {
        string Value(string key, string fallback = "") => item.Arguments.TryGetValue(key, out var value) ? FormatCompactValue(value) : fallback;
        string Object(string key = "target")
        {
            string token = Value(key, "Self");
            if (token.Equals("Self", StringComparison.OrdinalIgnoreCase)) return "This object";
            if (token.StartsWith("id:") && Guid.TryParse(token[3..], out var id))
                return state?.DisplayedScene.FindGameObject(id)?.Name ?? "Missing object";
            return token;
        }
        string key = Value("key", "choose key");
        if (key.Length == 2 && key[0] == 'D' && char.IsDigit(key[1])) key = key[1..];
        string name = item.Id switch
        {
            "input.keyPressed" => $"Key {key} is pressed",
            "input.keyHeld" => $"Key {key} is held",
            "input.keyReleased" => $"Key {key} is released",
            "input.mouseHeld" => $"{Value("button", "Left")} mouse button is held",
            "input.mousePressed" => $"{Value("button", "Left")} mouse button is pressed",
            "input.mouseReleased" => $"{Value("button", "Left")} mouse button is released",
            "logic.and" => "All of these conditions are true:",
            "logic.or" => "At least one of these conditions is true:",
            "logic.not" => "None of these conditions are true:",
            "object.destroy" => $"Destroy {Object()}",
            "system.always" => "Every update",
            "system.triggerOnce" => "Only once when these conditions become true",
            _ => ""
        };
        if (name.Length > 0) return name;
        GetInstructionPresentation(item, condition, out name, out _);
        var arguments = _registry.GetArguments(item.Id);
        string? template = condition
            ? (_registry.TryGetCondition(item.Id, out var cd) ? cd?.SentenceTemplate : null)
            : (_registry.TryGetAction(item.Id, out var ad) ? ad?.SentenceTemplate : null);
        if (!string.IsNullOrWhiteSpace(template))
        {
            foreach (var arg in arguments)
                template = template.Replace("{" + arg.Name + "}", arg.IsObjectReference ? Object(arg.Name) : Value(arg.Name, FormatCompactValue(arg.CreateDefault())));
            return template;
        }
        var values = new List<string>();
        foreach (var pair in item.Arguments)
        {
            var definition = arguments.FirstOrDefault(a => a.Name == pair.Key);
            string label = definition?.DisplayName ?? pair.Key;
            if (pair.Key == "target") values.Insert(0, Object());
            else if (definition?.IsObjectReference == true) values.Add(label + ": " + Object(pair.Key));
            else values.Add(label + ": " + Value(pair.Key));
        }
        return values.Count == 0 ? name : name + " - " + string.Join("; ", values);
    }
}

