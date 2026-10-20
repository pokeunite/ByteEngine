using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Editor.Panels;

// A read-only projection of the runtime links. Opening a sheet must never rewire it.
internal static class EventSheetProjection
{
    internal sealed record Row(VisualInstruction? Instruction, int Depth, string? Note = null, bool Connected = true);

    internal static List<Row> Conditions(EventRuleDefinition rule)
    {
        var rows = new List<Row>();
        var map = rule.Conditions.ToDictionary(i => i.InstanceId);
        var reached = new HashSet<Guid>();
        var path = new HashSet<Guid>();
        void Visit(Guid id, int depth)
        {
            if (!map.TryGetValue(id, out var item)) { rows.Add(new(null, depth, "Missing condition")); return; }
            if (!path.Add(id)) { rows.Add(new(item, depth, "Circular condition link")); return; }
            reached.Add(id);
            rows.Add(new(item, depth));
            if (item.Id is "logic.and" or "logic.or")
                foreach (var child in item.ConditionInputIds) Visit(child, depth + 1);
            path.Remove(id);
        }
        foreach (var id in rule.HasExplicitConditionFlow ? rule.ConnectedConditionIds : rule.Conditions.Select(i => i.InstanceId))
            Visit(id, 0);
        foreach (var item in rule.Conditions.Where(i => !reached.Contains(i.InstanceId)))
            rows.Add(new(item, 0, "Not connected", false));
        return rows;
    }

    internal static List<Row> Actions(EventRuleDefinition rule)
    {
        if (!rule.HasExplicitExecutionFlow) return rule.Actions.Select(i => new Row(i, 0)).ToList();
        var rows = new List<Row>();
        var map = rule.Actions.ToDictionary(i => i.InstanceId);
        var reached = new HashSet<Guid>();
        var path = new HashSet<Guid>();
        void Visit(Guid? id, int depth)
        {
            if (!id.HasValue) return;
            if (!map.TryGetValue(id.Value, out var item)) { rows.Add(new(null, depth, "Missing action")); return; }
            if (!path.Add(id.Value)) { rows.Add(new(item, depth, "Action loop")); return; }
            reached.Add(id.Value);
            rows.Add(new(item, depth));
            if (item.Id == "flow.branch")
            {
                rows.Add(new(null, depth + 1, "If true")); Visit(item.TrueActionId, depth + 1);
                rows.Add(new(null, depth + 1, "If false")); Visit(item.FalseActionId, depth + 1);
            }
            else Visit(item.NextActionId, depth);
            path.Remove(id.Value);
        }
        Visit(rule.FirstActionId, 0);
        foreach (var item in rule.Actions.Where(i => !reached.Contains(i.InstanceId)))
            rows.Add(new(item, 0, "Not connected", false));
        return rows;
    }
}
