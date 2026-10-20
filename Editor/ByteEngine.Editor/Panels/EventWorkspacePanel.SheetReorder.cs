using System.Numerics;
using ByteEngine.Core.VisualLogic;
using ImGuiNET;
namespace ByteEngine.Editor.Panels;
internal sealed partial class EventWorkspacePanel
{
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
    private bool ReorderSheetCondition(EventRuleDefinition rule,Guid source,Guid destination,bool before)
    {
        if(source==destination||SheetConditionOrder(rule,source) is not {} from||SheetConditionOrder(rule,destination) is not {} to||from.Container!=to.Container)return false;
        var ordered=from.Items.ToList();ordered.Remove(source);int index=ordered.IndexOf(destination);if(index<0)return false;ordered.Insert(index+(before?0:1),source);
        if(ordered.SequenceEqual(from.Items))return false;
        RecordHistory("Reorder Condition");
        if(from.Container==Guid.Empty){rule.HasExplicitConditionFlow=true;rule.ConnectedConditionIds=ordered;}
        else{from.Items.Clear();from.Items.AddRange(ordered);}
        SelectSheetItem(source);_dirty=true;return true;
    }
    private unsafe void DrawSheetConditionDrag(EventRuleDefinition rule,VisualInstruction item,Vector2 origin,float width,float height)
    {
        const string payloadType="BYTEENGINE_SHEET_CONDITION";
        if(ImGui.BeginDragDropSource()){
            Span<byte> bytes=stackalloc byte[32];rule.Id.TryWriteBytes(bytes[..16]);item.InstanceId.TryWriteBytes(bytes[16..]);
            fixed(byte* data=bytes)ImGui.SetDragDropPayload(payloadType,(nint)data,32,ImGuiCond.Once);
            ImGui.TextUnformatted("Move condition: "+GetSheetSentence(item,true,EditorState.Active));ImGui.TextDisabled("Drop above or below a condition in the same block.");ImGui.EndDragDropSource();
        }
        if(ImGui.BeginDragDropTarget()){
            var payload=ImGui.AcceptDragDropPayload(payloadType,ImGuiDragDropFlags.AcceptBeforeDelivery|ImGuiDragDropFlags.AcceptNoDrawDefaultRect);
            if(payload.NativePtr!=null&&payload.DataSize==32&&payload.Data!=IntPtr.Zero){
                var bytes=new ReadOnlySpan<byte>((void*)payload.Data,32);var ruleId=new Guid(bytes[..16]);var source=new Guid(bytes[16..]);
                bool allowed=ruleId==rule.Id&&source!=item.InstanceId&&SheetConditionOrder(rule,source) is {} from&&SheetConditionOrder(rule,item.InstanceId) is {} to&&from.Container==to.Container;
                bool before=ImGui.GetMousePos().Y<origin.Y+height*.5f;
                if(allowed){float y=before?origin.Y:origin.Y+height;ImGui.GetWindowDrawList().AddLine(new(origin.X,y),new(origin.X+width,y),ImGui.ColorConvertFloat4ToU32(SheetConditionColor),3);if(payload.Delivery)ReorderSheetCondition(rule,source,item.InstanceId,before);}
            }
            ImGui.EndDragDropTarget();
        }
    }
}
