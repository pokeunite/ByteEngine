using System.Text.Json;
using System.Text.Json.Serialization;
using ByteEngine.Core.Variables;

namespace ByteEngine.Core.AI;

public enum BehaviorNodeKind { Priority, Sequence, Gate, Cooldown, Wait, MoveToTarget, MoveToPoint, Retreat, FaceTarget, SetMemory, Signal, WaitForMemory, DamageTarget, StopMovement, EnemyCombat, FindPosition, Search, ReserveAttack, ReleaseAttack, ReleasePosition, ChooseAttack, Telegraph, CommitAttack, Strike, Recover, SubBehavior, Wander }
public enum BehaviorTest { HasTarget, TargetAlive, WithinDistance, OutsideDistance, LineOfSight, HealthBelow, MemoryTrue, MemoryNumberAtLeast, HasMemory, HeardNoise, AttackReserved, AttackReady }
public enum BehaviorInterruption { Recheck, FinishBranch, FinishCommittedAttack }
public enum AIPositionKind { Cover, FiringLane, Flank, Surround }
public enum AIServiceKind { SetMemory, HealthFlag, Signal, PositionQuery }
public sealed class BehaviorService
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Service";
    public bool Enabled { get; set; } = true;
    public AIServiceKind Kind { get; set; }
    public float Interval { get; set; } = .5f;
    public string Key { get; set; } = "LowHealth";
    public VariableValue Value { get; set; } = VariableValue.FromBoolean(false);
    public float Threshold { get; set; } = .3f;
    public AIPositionKind PositionKind { get; set; }
    public float Radius { get; set; } = 6;
}
public enum BehaviorStatus { Idle, Running, Success, Failure, Aborted }
public sealed class BehaviorNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New task";
    public BehaviorNodeKind Kind { get; set; }
    public List<BehaviorNode> Children { get; set; } = [];
    public BehaviorTest Test { get; set; }
    public bool Invert { get; set; }
    public string Key { get; set; } = "Ready";
    public VariableValue Value { get; set; } = VariableValue.FromBoolean(true);
    public float Amount { get; set; } = 1;
    public float Duration { get; set; } = .5f;
    public float Timeout { get; set; } = 8;
    public float Speed { get; set; } = 3;
    public float Distance { get; set; } = 1.4f;
    public bool RequireLineOfSight { get; set; } = true;
    public BehaviorInterruption Interruption { get; set; }
    public AIPositionKind PositionKind { get; set; }
    public float SearchRadius { get; set; } = 4;
    public float Spacing { get; set; } = 1.5f;
    public string AttackOption { get; set; } = "Automatic";
    public ByteEngine.Core.Assets.AssetReference SubBehavior { get; set; } = ByteEngine.Core.Assets.AssetReference.Empty;
    public string Notes { get; set; } = "";
    [JsonIgnore] public bool IsComposite => Kind is BehaviorNodeKind.Priority or BehaviorNodeKind.Sequence or BehaviorNodeKind.Gate or BehaviorNodeKind.Cooldown;
}
public sealed class BehaviorMemoryDefault
{
    public string Name { get; set; } = "Ready";
    public VariableValue Value { get; set; } = VariableValue.FromBoolean(false);
}
public sealed class BehaviorTreeDefinition
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "New AI Behaviour";
    public List<BehaviorMemoryDefault> Blackboard { get; set; } = [];
    public List<BehaviorService> Services { get; set; } = [];
    public BehaviorNode Root { get; set; } = new() { Name = "Choose first available behaviour", Kind = BehaviorNodeKind.Priority };
    public List<string> Validate()
    {
        var errors = new List<string>(); var seen = new HashSet<Guid>(); int count = 0;
        void Visit(BehaviorNode? node, int depth)
        {
            if (node == null) { errors.Add("Missing node."); return; }
            if (++count > 512 || depth > 48) { if (errors.Count < 20) errors.Add("Tree exceeds 512 nodes or 48 nesting levels."); return; }
            if (node.Id == Guid.Empty || !seen.Add(node.Id)) { errors.Add("Duplicate/empty node identity or cycle: " + node.Name); return; }
            if(node.Kind==BehaviorNodeKind.SubBehavior&&node.SubBehavior.IsEmpty)errors.Add(node.Name+" needs a reusable behaviour asset.");
            if(node.Kind==BehaviorNodeKind.ChooseAttack&&!new[]{"Automatic","Primary","Charge","Slam","Feint"}.Contains(node.AttackOption))errors.Add(node.Name+" has an unknown attack choice.");
            if (node.Value == null) errors.Add("Missing value: " + node.Name);
            if (!Enum.IsDefined(node.Kind) || !Enum.IsDefined(node.Test) || !Enum.IsDefined(node.Interruption) || !Enum.IsDefined(node.PositionKind)) errors.Add("Unknown task or condition: " + node.Name);
            if (!float.IsFinite(node.Duration) || !float.IsFinite(node.Timeout) || !float.IsFinite(node.Speed) || !float.IsFinite(node.Distance) || !float.IsFinite(node.Amount) || !float.IsFinite(node.SearchRadius) || !float.IsFinite(node.Spacing)) errors.Add("Invalid numeric setting: " + node.Name);
            if (node.Children == null) { errors.Add("Missing children list: " + node.Name); return; }
            if (node.Kind is BehaviorNodeKind.Gate or BehaviorNodeKind.Cooldown && node.Children.Count != 1) errors.Add(node.Name + " needs exactly one child.");
            if (node.Kind is BehaviorNodeKind.Priority or BehaviorNodeKind.Sequence && node.Children.Count == 0) errors.Add(node.Name + " needs a child.");
            if (!node.IsComposite && node.Children.Count != 0) errors.Add(node.Name + " is a task and cannot contain children.");
            if (node.Kind is BehaviorNodeKind.SetMemory or BehaviorNodeKind.Signal or BehaviorNodeKind.WaitForMemory or BehaviorNodeKind.MoveToPoint && string.IsNullOrWhiteSpace(node.Key)) errors.Add(node.Name + " needs a memory key/signal name.");
            foreach (var child in node.Children) Visit(child, depth + 1);
        }
        if (Services == null || Services.Count > 32) errors.Add("Use at most 32 background services.");
        var serviceIds = new HashSet<Guid>();
        foreach(var service in Services ?? [])
            if(service == null || service.Id == Guid.Empty || !serviceIds.Add(service.Id) || !Enum.IsDefined(service.Kind) || !Enum.IsDefined(service.PositionKind) || !float.IsFinite(service.Interval) || service.Interval < .02f || !float.IsFinite(service.Threshold) || !float.IsFinite(service.Radius) || service.Value == null || string.IsNullOrWhiteSpace(service.Key)) errors.Add("Invalid background service settings.");
        if (Version != 1) errors.Add("Unsupported behaviour version.");
        Visit(Root, 0);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Blackboard == null) errors.Add("Missing blackboard defaults.");
        foreach (var key in Blackboard ?? []) if (key == null || key.Value == null || string.IsNullOrWhiteSpace(key.Name) || !names.Add(key.Name)) errors.Add("Blackboard names must be nonempty and unique.");
        return errors;
    }
    public static BehaviorTreeDefinition CreateExample()
    {
        BehaviorNode Task(BehaviorNodeKind kind, string name) => new() { Kind = kind, Name = name };
        var attack = Task(BehaviorNodeKind.Sequence, "Warn, strike, recover");
        attack.Children = [new() { Kind = BehaviorNodeKind.StopMovement, Name = "Stop before attack" }, new() { Kind = BehaviorNodeKind.Signal, Name = "Tell events: attack windup", Key = "AttackWindup" }, new() { Kind = BehaviorNodeKind.Wait, Name = "Readable windup", Duration = .45f }, new() { Kind = BehaviorNodeKind.DamageTarget, Name = "Strike only if still in reach", Distance = 1.8f, Amount = 10 }, new() { Kind = BehaviorNodeKind.Wait, Name = "Recovery opportunity", Duration = .7f }];
        return new() { Name = "Chase and readable melee", Root = new() { Name = "Choose behaviour (top has priority)", Kind = BehaviorNodeKind.Priority, Children = [
            new() { Kind = BehaviorNodeKind.Gate, Name = "If target is close", Test = BehaviorTest.WithinDistance, Distance = 1.6f, Children = [attack] },
            new() { Kind = BehaviorNodeKind.Gate, Name = "If target is known", Test = BehaviorTest.HasTarget, Children = [new() { Kind = BehaviorNodeKind.MoveToTarget, Name = "Approach target", Speed = 3, Distance = 1.3f }] },
            new() { Kind = BehaviorNodeKind.Wait, Name = "Idle briefly", Duration = .5f }
        ] } };
    }
}
public static class BehaviorTreeSerializer
{
    public const string Extension = ".byteai";
    public static JsonSerializerOptions Options { get; } = new() { WriteIndented = true, IncludeFields = true, PropertyNameCaseInsensitive = true, MaxDepth = 128, Converters = { new JsonStringEnumConverter() } };
    public static BehaviorTreeDefinition Load(string path)
    {
        var tree = JsonSerializer.Deserialize<BehaviorTreeDefinition>(File.ReadAllText(path), Options) ?? throw new InvalidDataException("Empty behaviour file.");
        var errors = tree.Validate(); if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors)); return tree;
    }
    public static void Save(string path, BehaviorTreeDefinition tree)
    {
        var errors = tree.Validate(); if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors));
        string json = JsonSerializer.Serialize(tree, Options); File.WriteAllText(path + ".tmp", json); File.Move(path + ".tmp", path, true);
    }
}
