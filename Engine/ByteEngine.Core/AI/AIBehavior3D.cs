using System.ComponentModel;
using Component = ByteEngine.Core.Scene.Component;
using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;

namespace ByteEngine.Core.AI;

/// <summary>Independent per-agent execution state for reusable, data-authored behaviour trees.</summary>
public sealed partial class AIBehavior3D : Component, ICharacterDebugSource
{
    [Category("Behaviour"), Description("Reusable .byteai asset. For staged attacks, keep Simple Enemy AI enabled and turn on its Behaviour Controlled setting.")]
    public AssetReference Behavior { get; set; } = AssetReference.Empty;
    [Category("Perception"), SceneObjectReference] public Guid TargetObjectId { get; set; }
    [Category("Perception")] public bool FindNearestCharacter { get; set; } = true;
    [Category("Perception")] public float DetectionRange { get; set; } = 25;
    [Category("Perception")] public int SightMaskBits { get; set; } = -1;
    [Category("Perception")] public float EyeHeight { get; set; } = .8f;
    [Category("Behaviour"), Description("Decision/perception interval; navigation still moves smoothly each frame.")]
    public float DecisionInterval { get; set; } = .1f;
    [Category("Behaviour"), Description("Scales all movement task speeds without changing the shared behaviour asset.")] public float MovementSpeedMultiplier {get;set;}=1;
    internal float SafeMovementMultiplier=>float.IsFinite(MovementSpeedMultiplier)?Math.Clamp(MovementSpeedMultiplier,.1f,10):1;
    [Category("Behaviour")] public bool Paused { get; set; }
    [Category("Live State")] public string CurrentTask { get; private set; } = "Idle";
    [Category("Live State")] public BehaviorStatus Status { get; private set; }
    [Category("Live State")] public string LastError { get; private set; } = "";
    [Category("Live State")] public string LastSignal { get; private set; } = "";
    [Browsable(false)] public long SignalSequence { get; private set; }
    [Browsable(false)] public VariableStore Blackboard { get; } = new();
    [Browsable(false)] public IReadOnlyDictionary<Guid, BehaviorStatus> NodeStatus => _status;
    [Browsable(false)] public IReadOnlyList<(long Sequence,string Name)> Signals => _signals;
    [Browsable(false)] public GameObject? Target { get; private set; }
    [Browsable(false)] public BehaviorTreeDefinition? Definition => _definition;
    private BehaviorTreeDefinition? _definition;
    private Func<AssetReference,BehaviorTreeDefinition>? _loader;
    private readonly Dictionary<Guid, BehaviorStatus> _status = [];
    private readonly Dictionary<Guid, int> _indices = [];
    private readonly Dictionary<Guid, double> _started = [], _cooldowns = [];
    private readonly List<(long Sequence,string Name)> _signals = [];
    private readonly HashSet<Guid> _visited = [];
    private readonly List<Guid> _previousRunning=new(),_obsoleteKeys=new();
    private Guid? _movementOwner;
    private NavigationAgent3D? _ownedAgent;
    private SimpleEnemyAI3D? _combatDriver;
    private Guid? _combatNode;
    private double _clock, _accumulator;
    private bool _attemptedLoad;
    private Vector3? _wanderHome;
    public override int UpdateOrder => -1050;
    internal void Configure(Func<AssetReference,BehaviorTreeDefinition> loader) => _loader = loader;
    public void SetDefinition(BehaviorTreeDefinition definition)
    {
        var errors = definition.Validate(); if(errors.Count>0) throw new InvalidDataException(string.Join("\n",errors));
        Stop(); _definition = definition; _attemptedLoad = true; Blackboard.Clear();
        foreach(var key in definition.Blackboard) Blackboard.Set(key.Name,key.Value.Clone());
        LastError = "";
    }
    public void SetMemory(string name, VariableValue value) { Blackboard.Set(name,value.Clone()); _accumulator = Math.Max(_accumulator, Interval); }
    public void Restart()
    {
        Stop(); _attemptedLoad = false; _definition = null; Blackboard.Clear(); LastError = "";
    }
    public void Stop()
    {
        CancelMovement(); CancelCombat(); ResetAdvanced(); _indices.Clear(); _started.Clear(); _cooldowns.Clear(); _status.Clear(); _visited.Clear();
        Status = BehaviorStatus.Idle; CurrentTask = "Idle"; _clock = _accumulator = 0;
    }
    public void SetPaused(bool paused) { Paused=paused; if(paused){CancelMovement();CancelCombat();CancelAdvanced("Paused");} }
    protected override void OnUpdate() { if(GameObject.Scene?.FixedSimulation!=true)Tick((float)Time.DeltaTime); }
    protected override void OnFixedUpdate() { if(GameObject.Scene?.FixedSimulation==true)Tick((float)Time.FixedDeltaTime); }
    protected override void OnStop() => Stop();
    protected override void OnDestroy() => Stop();
    internal GameObject? ReservationOwner => AttachedGameObject;
    private float Interval => float.IsFinite(DecisionInterval)?Math.Clamp(DecisionInterval,.02f,2):.1f;
    /// <summary>Public simulation entry point also permits deterministic headless tests.</summary>
    public void Tick(float delta)
    {
        if(!float.IsFinite(delta)||delta<=0||AttachedGameObject?.Scene==null)return;
        if(GameObject.GetComponent<HealthComponent>()?.IsDead==true){Stop();return;}
        if(GameObject.GetComponent<SimpleEnemyAI3D>() is {} stagger && stagger.StaggerRemaining>0)
        {
            float remaining=stagger.StaggerRemaining;CancelMovement();CancelCombat();CancelAdvanced("Designated stagger");
            stagger.Stagger(remaining);stagger.TickStagger(delta);_indices.Clear();_started.Clear();_status.Clear();
            Status=BehaviorStatus.Running;CurrentTask="Staggered — normal decisions suspended";return;
        }
        if(Paused){CancelMovement();CancelCombat();CancelAdvanced("Paused");return;}
        if(GameObject.GetComponent<SimpleEnemyAI3D>() is {BehaviorControlled:true,Enabled:false}){CancelMovement();CancelCombat();CancelAdvanced("Encounter inactive");_indices.Clear();_started.Clear();Status=BehaviorStatus.Idle;CurrentTask="Waiting for encounter";LastError="";return;}
        if(GameObject.GetComponent<SimpleEnemyAI3D>() is { Enabled:true, BehaviorControlled:false }){LastError="Disable Simple Enemy AI or enable its Behaviour Controlled setting.";CancelMovement();CancelCombat();return;}
        if(_definition==null&&!_attemptedLoad)
        {
            _attemptedLoad=true;
            try
            {
                if(_loader!=null)SetDefinition(_loader(Behavior));
                else
                {
                    var root=GameObject.Scene!.ProjectRoot??throw new InvalidDataException("No project context for AI asset.");
                    using var db=new AssetDatabase(root,["Assets","Scenes"],readOnly:true);
                    var record=db.Resolve(Behavior)??throw new FileNotFoundException("Choose an AI Behaviour asset.");
                    SetDefinition(BehaviorTreeSerializer.Load(record.FullPath));
                }
            }
            catch(Exception e)when(e is IOException or InvalidDataException or System.Text.Json.JsonException or ArgumentException){LastError=e.Message;CancelMovement();return;}
        }
        if(_definition==null)return;
        _wanderHome??=Transform.WorldPosition;
        _clock+=delta;
        bool dodging=_combatDriver==null&&GameObject.GetComponent<SimpleEnemyAI3D>() is {BehaviorControlled:true} background&&background.TickBehaviorBackground(delta);
        TickStagedCombat(delta);
        if (!dodging&&_movementOwner.HasValue){var before=Transform.WorldPosition;_ownedAgent?.Tick(delta);GameObject.GetComponent<SimpleEnemyAI3D>()?.SetBehaviorMovement(Vector3.DistanceSquared(before,Transform.WorldPosition)>.000001f);}
        _combatDriver?.TickBehaviorCombat(delta);
        _accumulator+=delta;if(_accumulator<Interval)return;_accumulator=0;
        _previousRunning.Clear();foreach(var pair in _status)if(pair.Value==BehaviorStatus.Running)_previousRunning.Add(pair.Key);
        LastError="";Sense();RunServices();_visited.Clear();_status.Clear();CurrentTask="Idle";
        try { Status=Evaluate(_definition.Root,0); }
        catch(Exception e) {LastError="Behaviour stopped: "+e.Message;Status=BehaviorStatus.Failure;CancelMovement();CancelCombat();}
        foreach(var id in _previousRunning)if(!_visited.Contains(id)){_status[id]=BehaviorStatus.Aborted;Record(id,"Aborted","Higher-priority rule or condition changed");}
        _obsoleteKeys.Clear();foreach(var id in _started.Keys)if(!_visited.Contains(id))_obsoleteKeys.Add(id);foreach(var id in _obsoleteKeys)_started.Remove(id);
        _obsoleteKeys.Clear();foreach(var id in _indices.Keys)if(!_visited.Contains(id))_obsoleteKeys.Add(id);foreach(var id in _obsoleteKeys)_indices.Remove(id);
        if(_movementOwner.HasValue&&!_visited.Contains(_movementOwner.Value))CancelMovement();
        if(_combatNode.HasValue&&!_visited.Contains(_combatNode.Value))CancelCombat();
        CleanupAdvanced();
        if(Status!=BehaviorStatus.Running){_indices.Clear();_started.Clear();CancelMovement();}
    }
    private void Sense()
    {
        if(PerceptionMemory){SenseAdvanced();return;}
        var scene=GameObject.Scene!;float range=Safe(DetectionRange,25,0,10000);
        Target=TargetObjectId==Guid.Empty?null:scene.FindGameObject(TargetObjectId);
        if(Target==null&&FindNearestCharacter)Target=scene.GameObjects.Where(o=>o!=GameObject&&o.ActiveInHierarchy&&o.GetComponent<CharacterController3D>() is {Enabled:true}&&o.GetComponent<HealthComponent>()?.IsDead!=true).OrderBy(o=>Vector3.DistanceSquared(Transform.WorldPosition,o.Transform.WorldPosition)).FirstOrDefault();
        if(Target?.ActiveInHierarchy!=true||Target==GameObject||Vector3.Distance(Transform.WorldPosition,Target.Transform.WorldPosition)>range||Target.GetComponent<HealthComponent>()?.IsDead==true)Target=null;
        Blackboard.Set("HasTarget",VariableValue.FromBoolean(Target!=null));
        Blackboard.Set("TargetDistance",VariableValue.FromNumber(Target==null?range:FlatDistance(Target.Transform.WorldPosition)));
        Blackboard.Set("HasLineOfSight",VariableValue.FromBoolean(HasSight()));
        Blackboard.Set("HealthFraction",VariableValue.FromNumber(GameObject.GetComponent<HealthComponent>() is {} h?h.CurrentHealth/Math.Max(1,h.MaxHealth):1));
        if(Target!=null)Blackboard.Set("TargetPosition",VariableValue.FromVector3(Target.Transform.WorldPosition));
    }
    private bool HasSight()
    {
        if(Target==null)return false;var origin=Transform.WorldPosition+Vector3.UnitY*Safe(EyeHeight,.8f,0,100);var d=Target.Transform.WorldPosition+Vector3.UnitY*Safe(EyeHeight,.8f,0,100)-origin;
        if(d.LengthSquared()<.000001f)return true;
        return !GameplayQuery3D.Raycast(GameObject.Scene!,origin,Vector3.Normalize(d),out var hit,d.Length(),GameObject,new ByteEngine.Core.Classification.LayerMask(unchecked((uint)SightMaskBits)),includeTriggers:false)||hit.GameObject==Target||hit.GameObject.IsDescendantOf(Target);
    }
    private bool Test(BehaviorNode n)
    {
        bool value=n.Test switch {
            BehaviorTest.AttackReady=>GameObject.GetComponent<SimpleEnemyAI3D>() is {AttackPhase:"Ready",AttackCooldownRemaining:<=0,StaggerRemaining:<=0},
            BehaviorTest.HasMemory=>HasInterest,
            BehaviorTest.HeardNoise=>ReadBool("HeardNoise"),
            BehaviorTest.AttackReserved=>AIWorld.For(GameObject.Scene!).OwnsAttack(this),
            BehaviorTest.HasTarget=>Target!=null,
            BehaviorTest.TargetAlive=>Target!=null&&Target.GetComponent<HealthComponent>()?.IsDead!=true,
            BehaviorTest.WithinDistance=>Target!=null&&FlatDistance(Target.Transform.WorldPosition)<=Math.Max(0,n.Distance),
            BehaviorTest.OutsideDistance=>Target!=null&&FlatDistance(Target.Transform.WorldPosition)>Math.Max(0,n.Distance),
            BehaviorTest.LineOfSight=>ReadBool("HasLineOfSight"),
            BehaviorTest.HealthBelow=>Blackboard.TryGet("HealthFraction",out var h)&&h!.Number<n.Amount,
            BehaviorTest.MemoryTrue=>ReadBool(n.Key),
            BehaviorTest.MemoryNumberAtLeast=>Blackboard.TryGet(n.Key,out var number)&&number!.Type==VariableType.Number&&number.Number>=n.Amount,
            _=>false };
        return n.Invert?!value:value;
    }
    private BehaviorStatus Evaluate(BehaviorNode n,int depth)
    {
        if(depth>48||_visited.Count>=512)return BehaviorStatus.Failure;
        _visited.Add(n.Id);BehaviorStatus result;
        switch(n.Kind)
        {
            case BehaviorNodeKind.Priority:
                result=BehaviorStatus.Failure;
                int begin=0;
                if(_indices.TryGetValue(n.Id,out int active)&&(n.Interruption==BehaviorInterruption.FinishBranch||n.Interruption==BehaviorInterruption.FinishCommittedAttack&&_attackCommitted))begin=active;
                for(int choice=begin;choice<n.Children.Count;choice++){result=Evaluate(n.Children[choice],depth+1);if(result!=BehaviorStatus.Failure){if(result==BehaviorStatus.Running)_indices[n.Id]=choice;else _indices.Remove(n.Id);break;}}
                if(result==BehaviorStatus.Failure)_indices.Remove(n.Id);
                break;
            case BehaviorNodeKind.Sequence:
                int index=_indices.GetValueOrDefault(n.Id);result=BehaviorStatus.Success;
                for(;index<n.Children.Count;index++){result=Evaluate(n.Children[index],depth+1);if(result!=BehaviorStatus.Success)break;}
                if(result==BehaviorStatus.Running)_indices[n.Id]=index;else _indices.Remove(n.Id);
                break;
            case BehaviorNodeKind.Gate:
                bool latched=_runningGates.Contains(n.Id)&&(n.Interruption==BehaviorInterruption.FinishBranch||n.Interruption==BehaviorInterruption.FinishCommittedAttack&&_attackCommitted);
                bool allowed=latched||Test(n);
                result=allowed?Evaluate(n.Children[0],depth+1):BehaviorStatus.Failure;
                if(result==BehaviorStatus.Running)_runningGates.Add(n.Id);else _runningGates.Remove(n.Id);
                if(!allowed&&_previousDecisions.GetValueOrDefault(n.Id)!=BehaviorStatus.Failure)Record(n.Id,"Condition failed",ExplainCondition(n));break;
            case BehaviorNodeKind.Cooldown:
                if(_cooldowns.GetValueOrDefault(n.Id)>_clock){result=BehaviorStatus.Failure;break;}
                result=Evaluate(n.Children[0],depth+1);if(result==BehaviorStatus.Success)_cooldowns[n.Id]=_clock+Math.Max(0,n.Duration);break;
            default:
                CurrentTask=n.Name;result=Task(n);break;
        }
        if(result!=BehaviorStatus.Running)_started.Remove(n.Id);
        if(!_previousDecisions.TryGetValue(n.Id,out var prior)||prior!=result){Record(n.Id,result.ToString(),n.Name);_previousDecisions[n.Id]=result;}
        _status[n.Id]=result;return result;
    }
    private BehaviorStatus Task(BehaviorNode n)
    {
        bool first=!_started.ContainsKey(n.Id);if(first)_started[n.Id]=_clock;
        double elapsed=_clock-_started[n.Id];
        if(IsAdvancedTask(n.Kind))return AdvancedTask(n,first,elapsed);
        switch(n.Kind)
        {
            case BehaviorNodeKind.EnemyCombat:
                if(Target==null)return BehaviorStatus.Failure;
                var combat=GameObject.GetComponent<SimpleEnemyAI3D>();
                if(combat is not {Enabled:true,BehaviorControlled:true}){LastError="Enemy combat needs Simple Enemy AI with Behaviour Controlled enabled.";return BehaviorStatus.Failure;}
                CancelMovement();_combatDriver=combat;_combatNode=n.Id;combat.TargetId=Target.Id;return BehaviorStatus.Running;
            case BehaviorNodeKind.Wait:return elapsed>=Math.Max(0,n.Duration)?BehaviorStatus.Success:BehaviorStatus.Running;
            case BehaviorNodeKind.SetMemory:SetMemory(n.Key,n.Value);return BehaviorStatus.Success;
            case BehaviorNodeKind.Signal:
                LastSignal=n.Key;SignalSequence++;_signals.Add((SignalSequence,n.Key));if(_signals.Count>64)_signals.RemoveAt(0);return BehaviorStatus.Success;
            case BehaviorNodeKind.WaitForMemory:
                if(ReadBool(n.Key))return BehaviorStatus.Success;
                return n.Timeout>0&&elapsed>=n.Timeout?BehaviorStatus.Failure:BehaviorStatus.Running;
            case BehaviorNodeKind.StopMovement:CancelMovement();return BehaviorStatus.Success;
            case BehaviorNodeKind.FaceTarget:
                if(Target==null)return BehaviorStatus.Failure;Face(Target.Transform.WorldPosition);return BehaviorStatus.Success;
            case BehaviorNodeKind.DamageTarget:
                if(Target?.GetComponent<HealthComponent>() is not {} health||FlatDistance(Target.Transform.WorldPosition)>Math.Max(0,n.Distance)||(n.RequireLineOfSight&&!HasSight()))return BehaviorStatus.Failure;
                health.Damage(Math.Max(0,n.Amount));return BehaviorStatus.Success;
            case BehaviorNodeKind.MoveToTarget:
            case BehaviorNodeKind.MoveToPoint:
            case BehaviorNodeKind.Retreat:
                Vector3 point;
                if(n.Kind==BehaviorNodeKind.MoveToPoint){if(!Blackboard.TryGet(n.Key,out var v)||v!.Type!=VariableType.Vector3)return BehaviorStatus.Failure;point=v.Vector3;}
                else {if(Target==null)return BehaviorStatus.Failure;point=PerceptionMemory&&!ReadBool("HasLineOfSight")?LastKnownPosition:Target.Transform.WorldPosition;}
                float distance=FlatDistance(point);
                if(n.Kind==BehaviorNodeKind.Retreat)
                {
                    if(distance>=Math.Max(.1f,n.Distance)){CancelMovement();return BehaviorStatus.Success;}
                    var away=Transform.WorldPosition-point;away.Y=0;if(away.LengthSquared()<.001f)away=Transform.Right;
                    point=Transform.WorldPosition+Vector3.Normalize(away)*(Math.Max(.1f,n.Distance)-distance+.5f);
                }
                else if(distance<=Math.Max(.05f,n.Distance)){CancelMovement();return BehaviorStatus.Success;}
                if(n.Timeout>0&&elapsed>=n.Timeout){CancelMovement();return BehaviorStatus.Failure;}
                CancelCombat();
                var agent=GameObject.GetComponent<NavigationAgent3D>();
                if(agent is not {Enabled:true}||(agent.ExternallyDriven&&agent!=_ownedAgent)){LastError="Movement task needs an enabled Navigation Agent with Externally Driven off.";return BehaviorStatus.Failure;}
                if(_movementOwner==n.Id&&agent.Status is NavigationAgentStatus.Unreachable or NavigationAgentStatus.MissingRegion){CancelMovement();return BehaviorStatus.Failure;}
                _movementOwner=n.Id;_ownedAgent=agent;agent.ExternallyDriven=true;agent.Destination=point;agent.Speed=Math.Max(0,n.Speed)*SafeMovementMultiplier;agent.Moving=true;Face(point);return BehaviorStatus.Running;
            default:return BehaviorStatus.Failure;
        }
    }
    private bool ReadBool(string key)=>Blackboard.TryGet(key,out var v)&&v!.Type==VariableType.Boolean&&v.Boolean;
    private float FlatDistance(Vector3 point){var d=point-Transform.WorldPosition;d.Y=0;return d.Length();}
    private void Face(Vector3 point){var d=point-Transform.WorldPosition;d.Y=0;if(d.LengthSquared()<.000001f)return;var e=Transform.EulerAngles;e.Y=MathF.Atan2(-d.X,-d.Z)*180/MathF.PI;Transform.EulerAngles=e;}
    private void CancelCombat(){if(_combatDriver!=null){_combatDriver.ResetAttackPattern();GameObject.GetComponent<NavigationAgent3D>()?.Stop();if(GameObject.GetComponent<NavigationAgent3D>() is {} nav)nav.ExternallyDriven=false;}_combatDriver=null;_combatNode=null;}
    private void CancelMovement(){if(_ownedAgent!=null){_ownedAgent.Stop();_ownedAgent.ExternallyDriven=false;}_ownedAgent=null;_movementOwner=null;}
    private static float Safe(float v,float fallback,float min,float max)=>float.IsFinite(v)?Math.Clamp(v,min,max):fallback;
    public string GetCharacterDebugText()=>"AI: "+Status+" / "+CurrentTask+(LastError.Length>0?"\n"+LastError:"");
}
