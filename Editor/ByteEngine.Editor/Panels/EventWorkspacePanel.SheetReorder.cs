using System.Numerics;
using ByteEngine.Core.VisualLogic;
using ImGuiNET;
namespace ByteEngine.Editor.Panels;
internal sealed partial class EventWorkspacePanel
{
    private sealed record SheetActionSequence(Guid? Branch, bool FalseArm, List<Guid> Items);

    private static SheetActionSequence? SheetActionOrder(EventRuleDefinition rule, Guid id)
    {
        if (!rule.Actions.Any(a => a.InstanceId == id)) return null;
        if (!rule.HasExplicitExecutionFlow) return new(null, false, rule.Actions.Select(a => a.InstanceId).ToList());
        var map = rule.Actions.ToDictionary(a => a.InstanceId);
        var sequences = new List<SheetActionSequence>();
        var visits = new Dictionary<Guid, int>();
        var invalid = new HashSet<SheetActionSequence>();
        void Walk(Guid? first, Guid? branch, bool falseArm, HashSet<Guid> path)
        {
            var sequence = new SheetActionSequence(branch, falseArm, []); sequences.Add(sequence);
            while (first is {} next)
            {
                if (!map.TryGetValue(next, out var action) || !path.Add(next)) { invalid.Add(sequence); break; }
                visits[next] = visits.GetValueOrDefault(next) + 1;
                sequence.Items.Add(next);
                if (action.Id == "flow.branch")
                {
                    Walk(action.TrueActionId, action.InstanceId, false, new(path));
                    Walk(action.FalseActionId, action.InstanceId, true, new(path));
                    break;
                }
                first = action.NextActionId;
            }
        }
        Walk(rule.FirstActionId, null, false, []);
        var matches = sequences.Where(s => s.Items.Contains(id)).ToArray();
        return matches.Length == 1 && !invalid.Contains(matches[0]) && matches[0].Items.All(i => visits[i] == 1) ? matches[0] : null;
    }

    private static (SheetActionSequence Sequence, List<Guid> Order)? PlanSheetActionMove(EventRuleDefinition rule, Guid source, Guid destination, bool before)
    {
        if (source == destination || SheetActionOrder(rule, source) is not {} from || SheetActionOrder(rule, destination) is not {} to || from.Branch != to.Branch || from.FalseArm != to.FalseArm) return null;
        // A branch terminates its sequence. Moving through it would change which arm executes.
        if (rule.HasExplicitExecutionFlow && (rule.Actions.Single(a => a.InstanceId == source).Id == "flow.branch" || (!before && rule.Actions.Single(a => a.InstanceId == destination).Id == "flow.branch"))) return null;
        var order = from.Items.ToList(); order.Remove(source);
        int index = order.IndexOf(destination); if (index < 0) return null;
        order.Insert(index + (before ? 0 : 1), source);
        return order.SequenceEqual(from.Items) ? null : (from, order);
    }

    private bool CanMoveSheetAction(EventRuleDefinition rule, Guid id, int direction)
    {
        if (SheetActionOrder(rule, id) is not {} sequence) return false;
        int index = sequence.Items.IndexOf(id) + direction;
        return index >= 0 && index < sequence.Items.Count && PlanSheetActionMove(rule, id, sequence.Items[index], direction < 0) != null;
    }

    private bool MoveSheetAction(EventRuleDefinition rule, Guid id, int direction)
    {
        if (!CanMoveSheetAction(rule, id, direction) || SheetActionOrder(rule, id) is not {} sequence) return false;
        return ReorderSheetAction(rule, id, sequence.Items[sequence.Items.IndexOf(id) + direction], direction < 0);
    }

    private bool ReorderSheetAction(EventRuleDefinition rule, Guid source, Guid destination, bool before)
    {
        if (PlanSheetActionMove(rule, source, destination, before) is not {} plan) return false;
        RecordHistory("Reorder Action");
        var map = rule.Actions.ToDictionary(a => a.InstanceId);
        if (!rule.HasExplicitExecutionFlow) rule.Actions = plan.Order.Select(id => map[id]).ToList();
        else
        {
            if (plan.Sequence.Branch is {} branch)
            {
                if (plan.Sequence.FalseArm) map[branch].FalseActionId = plan.Order[0];
                else map[branch].TrueActionId = plan.Order[0];
            }
            else rule.FirstActionId = plan.Order[0];
            for (int i = 0; i < plan.Order.Count; i++)
                if (map[plan.Order[i]].Id != "flow.branch") map[plan.Order[i]].NextActionId = i + 1 < plan.Order.Count ? plan.Order[i + 1] : null;
        }
        SelectSheetItem(source); _dirty = true; return true;
    }

    private unsafe void DrawSheetActionDrag(EventRuleDefinition rule, VisualInstruction item, Vector2 origin, float width, float height)
    {
        const string payloadType = "BYTEENGINE_SHEET_ACTION";
        if (ImGui.BeginDragDropSource())
        {
            Span<byte> bytes = stackalloc byte[32]; rule.Id.TryWriteBytes(bytes[..16]); item.InstanceId.TryWriteBytes(bytes[16..]);
            fixed (byte* data = bytes) ImGui.SetDragDropPayload(payloadType, (nint)data, 32, ImGuiCond.Once);
            ImGui.TextUnformatted("Move action: " + GetSheetSentence(item, false, EditorState.Active));
            ImGui.TextDisabled("Drop above or below an action in the same execution path."); ImGui.EndDragDropSource();
        }
        if (ImGui.BeginDragDropTarget())
        {
            var payload = ImGui.AcceptDragDropPayload(payloadType, ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoDrawDefaultRect);
            if (payload.NativePtr != null && payload.DataSize == 32 && payload.Data != IntPtr.Zero)
            {
                var bytes = new ReadOnlySpan<byte>((void*)payload.Data, 32); var ruleId = new Guid(bytes[..16]); var source = new Guid(bytes[16..]);
                bool before = ImGui.GetMousePos().Y < origin.Y + height * .5f;
                if (ruleId == rule.Id && PlanSheetActionMove(rule, source, item.InstanceId, before) != null)
                {
                    float y = before ? origin.Y : origin.Y + height;
                    ImGui.GetWindowDrawList().AddLine(new(origin.X, y), new(origin.X + width, y), ImGui.ColorConvertFloat4ToU32(SheetActionColor), 3);
                    if (payload.Delivery) ReorderSheetAction(rule, source, item.InstanceId, before);
                }
            }
            ImGui.EndDragDropTarget();
        }
    }
    private (Guid Container,List<Guid> Items)? SheetConditionOrder(EventRuleDefinition rule,Guid id)
    {
        if(!rule.Conditions.Any(c=>c.InstanceId==id))return null;
        var parents=rule.Conditions.Where(c=>IsLogicGate(c)&&c.ConditionInputIds.Contains(id)).ToArray();
        if(parents.Length>1)return null;
        if(parents.Length==1){if(!rule.HasExplicitConditionFlow||rule.ConnectedConditionIds.Contains(id))return null;return (parents[0].InstanceId,parents[0].ConditionInputIds);}
        if(rule.HasExplicitConditionFlow)return rule.ConnectedConditionIds.Contains(id)?(Guid.Empty,rule.ConnectedConditionIds):null;
        return (Guid.Empty,rule.Conditions.Select(c=>c.InstanceId).ToList());
    }
    private bool CanMoveSheetCondition(EventRuleDefinition rule,Guid id,int direction)
        =>SheetConditionOrder(rule,id) is {} order&&order.Items.IndexOf(id)+direction>=0&&order.Items.IndexOf(id)+direction<order.Items.Count;
    private bool MoveSheetCondition(EventRuleDefinition rule,Guid id,int direction)
    {
        if(!CanMoveSheetCondition(rule,id,direction)||SheetConditionOrder(rule,id) is not {} order)return false;
        int index=order.Items.IndexOf(id);return ReorderSheetCondition(rule,id,order.Items[index+direction],direction<0);
    }
    private bool CanPlaceSheetCondition(EventRuleDefinition rule, Guid source, Guid destination, bool inside)
    {
        if (source == destination || SheetConditionOrder(rule, source) == null || SheetConditionOrder(rule, destination) == null) return false;
        var map = rule.Conditions.ToDictionary(c => c.InstanceId);
        if (inside && !IsLogicGate(map[destination])) return false;
        Guid container = inside ? destination : SheetConditionOrder(rule, destination)!.Value.Container;
        var visited = new HashSet<Guid>();
        bool Contains(Guid id)
        {
            if (id == container) return true;
            return visited.Add(id) && map.TryGetValue(id, out var node) && IsLogicGate(node) && node.ConditionInputIds.Any(Contains);
        }
        return container == Guid.Empty || !Contains(source);
    }
    private bool ReorderSheetCondition(EventRuleDefinition rule, Guid source, Guid destination, bool before, bool inside = false)
    {
        if (!CanPlaceSheetCondition(rule, source, destination, inside)) return false;
        var from = SheetConditionOrder(rule, source)!.Value;
        var to = SheetConditionOrder(rule, destination)!.Value;
        Guid container = inside ? destination : to.Container;
        RecordHistory("Move Condition");
        if (!rule.HasExplicitConditionFlow)
        {
            rule.ConnectedConditionIds = rule.Conditions.Select(c => c.InstanceId).ToList();
            rule.HasExplicitConditionFlow = true;
        }
        var sourceItems = from.Container == Guid.Empty ? rule.ConnectedConditionIds : from.Items;
        sourceItems.Remove(source);
        var targetItems = container == Guid.Empty ? rule.ConnectedConditionIds : rule.Conditions.Single(c => c.InstanceId == container).ConditionInputIds;
        int index = inside ? targetItems.Count : targetItems.IndexOf(destination) + (before ? 0 : 1);
        targetItems.Insert(index, source);
        _collapsedSheetLogic.Remove(container);
        SelectSheetItem(source); _dirty = true; return true;
    }
    private unsafe void DrawSheetConditionDrag(EventRuleDefinition rule,VisualInstruction item,Vector2 origin,float width,float height)
    {
        const string payloadType="BYTEENGINE_SHEET_CONDITION";
        if(ImGui.BeginDragDropSource()){
            Span<byte> bytes=stackalloc byte[32];rule.Id.TryWriteBytes(bytes[..16]);item.InstanceId.TryWriteBytes(bytes[16..]);
            fixed(byte* data=bytes)ImGui.SetDragDropPayload(payloadType,(nint)data,32,ImGuiCond.Once);
            ImGui.TextUnformatted("Move condition: "+GetSheetSentence(item,true,EditorState.Active));ImGui.TextDisabled("Drop on the center of a logic block to put the condition inside. Drop at an edge to reorder.");ImGui.EndDragDropSource();
        }
        if(ImGui.BeginDragDropTarget()){
            var payload=ImGui.AcceptDragDropPayload(payloadType,ImGuiDragDropFlags.AcceptBeforeDelivery|ImGuiDragDropFlags.AcceptNoDrawDefaultRect);
            if(payload.NativePtr!=null&&payload.DataSize==32&&payload.Data!=IntPtr.Zero){
                var bytes=new ReadOnlySpan<byte>((void*)payload.Data,32);var ruleId=new Guid(bytes[..16]);var source=new Guid(bytes[16..]);
                float fraction = (ImGui.GetMousePos().Y - origin.Y) / height;
                bool inside = IsLogicGate(item) && fraction >= .25f && fraction <= .75f;
                bool before = fraction < .5f;
                bool allowed = ruleId == rule.Id && CanPlaceSheetCondition(rule, source, item.InstanceId, inside);
                if (allowed)
                {
                    var draw = ImGui.GetWindowDrawList();
                    uint color = ImGui.ColorConvertFloat4ToU32(SheetConditionColor);
                    if (inside) draw.AddRect(origin, origin + new Vector2(width, height), color, 3, ImDrawFlags.None, 2);
                    else { float y = before ? origin.Y : origin.Y + height; draw.AddLine(new(origin.X, y), new(origin.X + width, y), color, 3); }
                    ImGui.SetTooltip(inside ? "Move inside logic block" : before ? "Move before condition" : "Move after condition");
                    if (payload.Delivery) ReorderSheetCondition(rule, source, item.InstanceId, before, inside);
                }
            }
            ImGui.EndDragDropTarget();
        }
    }
}
