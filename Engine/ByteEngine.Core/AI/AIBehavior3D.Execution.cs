using System.ComponentModel;
using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Variables;
namespace ByteEngine.Core.AI;
public sealed record AIDecisionRecord(double Time,Guid Node,string Result,string Reason);
public sealed partial class AIBehavior3D
{
    [Category("Debugging")] public bool RecordHistory {get;set;}=true;
    [Category("Debugging")] public int HistoryCapacity {get;set;}=160;
    [Browsable(false)] public IReadOnlyList<AIDecisionRecord> DecisionHistory=>_history;
    private readonly List<AIDecisionRecord> _history=[];
    private readonly Dictionary<Guid,BehaviorStatus> _previousDecisions=[];
    private readonly HashSet<Guid> _runningGates=[];
    private readonly Dictionary<Guid,double> _serviceNext=[];
    private readonly Dictionary<Guid,BehaviorTreeDefinition> _subtrees=[];
    private readonly HashSet<string> _subtreeStack=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid,(Vector3 Point,double Next)> _searchPoints=[];
    private Guid? _attackOwner;
    private SimpleEnemyAI3D? _stagedCombat;
    private ByteEngine.Core.Scene.GameObject? _attackTarget;
    private bool _attackCommitted,_phaseFinished;
    private BehaviorNodeKind? _stagedPhase;
    private AIPositionKind? _positionKind;
    private Vector3 _positionCenter;
    private readonly Dictionary<AIPositionKind,double> _positionNext=[];
    private bool _positionDeferred;
    private readonly Dictionary<AIPositionKind,int> _positionCursor=[];
    public void SetSubBehaviorResolver(Func<AssetReference,BehaviorTreeDefinition> resolver)=>_loader=resolver;
    public void ClearDecisionHistory()=>_history.Clear();
    public string ExportDecisionHistory()=>JsonSerializer.Serialize(_history,BehaviorTreeSerializer.Options);
    private void Record(Guid node,string result,string reason)
    {
        if(!RecordHistory)return;
        if(_history.Count>0&&_history[^1].Node==node&&_history[^1].Result==result&&_history[^1].Reason==reason)return;
        _history.Add(new(_clock,node,result,reason));int capacity=Math.Clamp(HistoryCapacity,16,1000);if(_history.Count>capacity)_history.RemoveRange(0,_history.Count-capacity);
    }
    private void ResetAdvanced()
    {
        CancelAdvanced("Stopped");_runningGates.Clear();_previousDecisions.Clear();_serviceNext.Clear();_subtrees.Clear();_subtreeStack.Clear();_searchPoints.Clear();_positionNext.Clear();_positionCursor.Clear();_positionKind=null;ForgetTarget();
    }
    private void CancelAdvanced(string reason)
    {
        if(_stagedCombat!=null){_stagedCombat.ResetAttackPattern();_stagedCombat=null;Record(_attackOwner??Guid.Empty,"Attack cancelled",reason);}
        _attackTarget=null;_attackOwner=null;_stagedPhase=null;_attackCommitted=false;_phaseFinished=false;
        if(AttachedGameObject?.Scene is {} scene){var world=AIWorld.For(scene);world.ReleaseAttack(this);world.ReleasePosition(this);}
    }
    private void CleanupAdvanced()
    {
        foreach(var id in _runningGates.Where(id=>!_visited.Contains(id)).ToArray())_runningGates.Remove(id);
        foreach(var id in _searchPoints.Keys.Where(id=>!_visited.Contains(id)).ToArray())_searchPoints.Remove(id);
        if(_attackOwner.HasValue&&(!_visited.Contains(_attackOwner.Value)||_status.GetValueOrDefault(_attackOwner.Value)==BehaviorStatus.Failure))CancelAdvanced("Branch interrupted");
        if(Status!=BehaviorStatus.Running&&!_attackCommitted)AIWorld.For(GameObject.Scene!).ReleaseAttack(this);
    }
    private void RunServices()=>RunServicesFor(_definition!);
    private void RunServicesFor(BehaviorTreeDefinition definition)
    {
        foreach(var service in definition.Services.Where(s=>s.Enabled))
        {
            if(_serviceNext.GetValueOrDefault(service.Id)>_clock)continue;
            _serviceNext[service.Id]=_clock+Math.Max(.02f,service.Interval);
            switch(service.Kind)
            {
                case AIServiceKind.SetMemory:Blackboard.Set(service.Key,service.Value.Clone());break;
                case AIServiceKind.HealthFlag:Blackboard.Set(service.Key,VariableValue.FromBoolean((GameObject.GetComponent<HealthComponent>()?.HealthPercent??1)<service.Threshold));break;
                case AIServiceKind.Signal:EmitSignal(service.Key);break;
                case AIServiceKind.PositionQuery:FindTacticalPosition(service.PositionKind,service.Radius,service.Radius,PositionSpacing,service.Interval,service.Key);break;
            }
            Record(service.Id,"Service",service.Name);
        }
    }
    private void EmitSignal(string name){LastSignal=name;SignalSequence++;_signals.Add((SignalSequence,name));if(_signals.Count>64)_signals.RemoveAt(0);}
    private static bool IsAdvancedTask(BehaviorNodeKind kind)=>kind>=BehaviorNodeKind.FindPosition;
    private BehaviorStatus AdvancedTask(BehaviorNode node,bool first,double elapsed)
    {
        var world=AIWorld.For(GameObject.Scene!);
        switch(node.Kind)
        {
            case BehaviorNodeKind.FindPosition:
                bool found=FindTacticalPosition(node.PositionKind,node.Distance,node.SearchRadius,Math.Max(PositionSpacing,node.Spacing),Math.Max(.5f,node.Duration),node.Key);
                return found?BehaviorStatus.Success:_positionDeferred&&(node.Timeout<=0||elapsed<node.Timeout)?BehaviorStatus.Running:BehaviorStatus.Failure;
            case BehaviorNodeKind.ReserveAttack:
                if(Target==null)return BehaviorStatus.Failure;
                if(world.ReserveAttack(this,Target.Id,Math.Max(4,node.Duration))){Blackboard.Set("AttackReserved",VariableValue.FromBoolean(true));return BehaviorStatus.Success;}
                Record(node.Id,"Waiting","Squad attack slots are occupied");return node.Timeout>0&&elapsed>=node.Timeout?BehaviorStatus.Failure:BehaviorStatus.Running;
            case BehaviorNodeKind.ReleaseAttack:world.ReleaseAttack(this);Blackboard.Set("AttackReserved",VariableValue.FromBoolean(false));return BehaviorStatus.Success;
            case BehaviorNodeKind.ReleasePosition:world.ReleasePosition(this);return BehaviorStatus.Success;
            case BehaviorNodeKind.Wander:
                var regionForWander=GetRegion();
                if(regionForWander?.Ready!=true){LastError="Wandering needs a baked navigation region.";return BehaviorStatus.Failure;}
                if(!_searchPoints.TryGetValue(node.Id,out var wander)||wander.Next<=_clock)
                {
                    Vector3 home=_wanderHome??Transform.WorldPosition;Vector3 chosen=home;
                    // Stable per-instance phase; only a few local path queries when choosing a destination.
                    float phase=(GameObject.Id.GetHashCode()&0xffff)*.001f+(float)_clock;
                    for(int attempt=0;attempt<3;attempt++)
                    {
                        float angle=phase+attempt*2.4f;var candidate=home+new Vector3(MathF.Cos(angle),0,MathF.Sin(angle))*Math.Max(.5f,node.SearchRadius);
                        var route=regionForWander.FindPath(Transform.WorldPosition,candidate,3000);
                        if(route.Count>0&&Vector3.Distance(route[^1],candidate)<1){chosen=route[^1];break;}
                    }
                    _searchPoints[node.Id]=(chosen,_clock+Math.Max(2,node.Duration));Blackboard.Set(node.Key,VariableValue.FromVector3(chosen));
                }
                if(Blackboard.TryGet(node.Key,out var wanderValue)&&FlatDistance(wanderValue!.Vector3)<.3f){CancelMovement();return BehaviorStatus.Running;}
                return MoveAdvanced(node,node.Key,Math.Max(.1f,node.Speed),.3f);
            case BehaviorNodeKind.Search:
                if(ReadBool("HasLineOfSight"))return BehaviorStatus.Success;
                if(!HasInterest||elapsed>=Math.Max(.1f,node.Duration)){CancelMovement();return BehaviorStatus.Success;}
                if(!_searchPoints.TryGetValue(node.Id,out var search)||search.Next<=_clock)
                {
                    int step=(int)(elapsed/1.2);float angle=step*MathF.PI*.75f;
                    var point=LastKnownPosition+(step==0?Vector3.Zero:new Vector3(MathF.Sin(angle),0,MathF.Cos(angle))*Math.Max(.5f,node.SearchRadius));
                    var region=GetRegion();if(region!=null&&region.FindPath(Transform.WorldPosition,point,3000) is {Count:>0} path)point=path[^1];
                    _searchPoints[node.Id]=(point,_clock+1.2);Blackboard.Set("SearchPoint",VariableValue.FromVector3(point));Record(node.Id,"Search","Check last known area: "+point);
                }
                return MoveAdvanced(node,"SearchPoint",Math.Max(.2f,node.Speed),.3f);
            case BehaviorNodeKind.SubBehavior:return RunSubtree(node);
            case BehaviorNodeKind.ChooseAttack:
                if(Target==null||(PerceptionMemory&&!ReadBool("HasLineOfSight")))return BehaviorStatus.Failure;
                var combat=GameObject.GetComponent<SimpleEnemyAI3D>();
                if(combat is not {Enabled:true,BehaviorControlled:true}){LastError="Staged attacks require the enemy combat component in Behaviour Controlled mode.";return BehaviorStatus.Failure;}
                if(FlatDistance(Target.Transform.WorldPosition)>combat.AttackRange||(PerceptionMemory&&!ReadBool("HasLineOfSight"))){world.ReleaseAttack(this);Record(node.Id,"Attack denied","Target moved outside attack reach or sight");return BehaviorStatus.Failure;}
                if(!world.OwnsAttack(this)){Record(node.Id,"Attack denied","Reserve a squad attack slot before choosing an attack");return BehaviorStatus.Failure;}
                if(combat.PredatoryRush && !combat.CanBeginRush(Target)){world.ReleaseAttack(this);Record(node.Id,"Attack denied","Rush needs clear sight, cooldown and minimum/maximum range");return BehaviorStatus.Failure;}
                combat.TargetId=Target.Id;
                CancelMovement();CancelCombat();_stagedCombat=combat;_attackTarget=Target;_attackOwner=node.Id;_attackCommitted=false;
                if(!combat.SelectBehaviorAttack(Target,node.AttackOption)){_stagedCombat=null;_attackOwner=null;world.ReleaseAttack(this);Record(node.Id,"Attack unavailable",combat.BehaviorAttackError);return BehaviorStatus.Failure;}Blackboard.Set("ChosenAttack",VariableValue.FromString(combat.SelectedBehaviorAttack));EmitSignal("AttackChosen");return BehaviorStatus.Success;
            case BehaviorNodeKind.Telegraph:
                if(_stagedCombat==null||_attackTarget==null)return BehaviorStatus.Failure;
                _attackOwner=node.Id;
                if(first){CancelMovement();_stagedCombat.BeginBehaviorTelegraph(_attackTarget);_stagedPhase=node.Kind;_phaseFinished=false;EmitSignal("AttackTelegraph");Record(node.Id,"Telegraph",_stagedCombat.SelectedBehaviorAttack);}
                return _phaseFinished?BehaviorStatus.Success:BehaviorStatus.Running;
            case BehaviorNodeKind.CommitAttack:
                if(_stagedCombat==null||!world.OwnsAttack(this))return BehaviorStatus.Failure;
                _attackOwner=node.Id;_attackCommitted=true;EmitSignal("AttackCommitted");Record(node.Id,"Committed","Aim is locked; the attack will not track the target");return BehaviorStatus.Success;
            case BehaviorNodeKind.Strike:
                if(_stagedCombat==null||!_attackCommitted||_attackTarget==null)return BehaviorStatus.Failure;
                _attackOwner=node.Id;
                if(first){_stagedCombat.BeginBehaviorStrike(_attackTarget);_stagedPhase=node.Kind;_phaseFinished=false;EmitSignal("AttackStrike");}
                return _phaseFinished?BehaviorStatus.Success:BehaviorStatus.Running;
            case BehaviorNodeKind.Recover:
                if(_stagedCombat==null)return BehaviorStatus.Failure;_attackOwner=node.Id;
                if(first){_stagedPhase=node.Kind;_phaseFinished=false;EmitSignal("AttackRecovery");}
                if(!_phaseFinished)return BehaviorStatus.Running;
                _stagedCombat=null;_stagedPhase=null;_attackOwner=null;_attackCommitted=false;world.ReleaseAttack(this);Blackboard.Set("AttackReserved",VariableValue.FromBoolean(false));return BehaviorStatus.Success;
            default:return BehaviorStatus.Failure;
        }
    }
    private void TickStagedCombat(float delta)
    {
        if(_stagedCombat==null||_attackTarget==null||_phaseFinished)return;
        if(_attackCommitted&&!AIWorld.For(GameObject.Scene!).OwnsAttack(this)){CancelAdvanced("Attack reservation expired");return;}
        if(_stagedPhase==BehaviorNodeKind.Telegraph)_phaseFinished=_stagedCombat.TickBehaviorTelegraph(delta);
        if(_stagedPhase==BehaviorNodeKind.Strike)_phaseFinished=_stagedCombat.TickBehaviorStrike(_attackTarget,delta);
        if(_stagedPhase==BehaviorNodeKind.Recover)_phaseFinished=_stagedCombat.TickBehaviorRecovery(_attackTarget,delta);
    }
    private BehaviorStatus MoveAdvanced(BehaviorNode node,string key,float speed,float distance)
    {
        if(!Blackboard.TryGet(key,out var value)||value!.Type!=VariableType.Vector3)return BehaviorStatus.Failure;
        var agent=GameObject.GetComponent<NavigationAgent3D>();if(agent is not {Enabled:true}){LastError="Search needs Navigation Agent.";return BehaviorStatus.Failure;}
        _movementOwner=node.Id;_ownedAgent=agent;agent.ExternallyDriven=true;agent.Destination=value.Vector3;agent.Speed=speed*SafeMovementMultiplier;agent.Moving=true;Face(value.Vector3);return BehaviorStatus.Running;
    }
    private NavigationRegion3D? GetRegion(){var agent=GameObject.GetComponent<NavigationAgent3D>();return agent==null?null:agent.RegionObject==Guid.Empty?GameObject.Scene?.GameObjects.Select(o=>o.GetComponent<NavigationRegion3D>()).FirstOrDefault(r=>r is {Enabled:true}):GameObject.Scene?.FindGameObject(agent.RegionObject)?.GetComponent<NavigationRegion3D>();}
    private BehaviorStatus RunSubtree(BehaviorNode node)
    {
        string identity=node.SubBehavior.Guid!=Guid.Empty?node.SubBehavior.Guid.ToString():node.SubBehavior.ProjectPath;
        if(node.SubBehavior.IsEmpty||_subtreeStack.Count>=8||!_subtreeStack.Add(identity)){LastError="Missing or recursive sub-behaviour: "+identity;Record(node.Id,"Sub-behaviour rejected",LastError);return BehaviorStatus.Failure;}
        try
        {
            if(!_subtrees.TryGetValue(node.Id,out var definition))
            {
                BehaviorTreeDefinition original;
                if(_loader!=null)original=_loader(node.SubBehavior);
                else {using var db=new AssetDatabase(GameObject.Scene!.ProjectRoot!,["Assets","Scenes"],readOnly:true);var asset=db.Resolve(node.SubBehavior)??throw new FileNotFoundException("Sub-behaviour missing.");original=BehaviorTreeSerializer.Load(asset.FullPath);}
                if(original.Validate().Count>0)throw new InvalidDataException("Invalid sub-behaviour.");
                definition=JsonSerializer.Deserialize<BehaviorTreeDefinition>(JsonSerializer.Serialize(original,BehaviorTreeSerializer.Options),BehaviorTreeSerializer.Options)!;
                void Scope(BehaviorNode n){var a=n.Id.ToByteArray();var b=node.Id.ToByteArray();for(int i=0;i<a.Length;i++)a[i]^=b[i];n.Id=new Guid(a);foreach(var c in n.Children)Scope(c);}Scope(definition.Root);foreach(var service in definition.Services){var bytes=service.Id.ToByteArray();var salt=node.Id.ToByteArray();for(int i=0;i<bytes.Length;i++)bytes[i]^=salt[i];service.Id=new Guid(bytes);}
                foreach(var key in definition.Blackboard)if(!Blackboard.Contains(key.Name))Blackboard.Set(key.Name,key.Value.Clone());_subtrees[node.Id]=definition;
            }
            RunServicesFor(definition);
            return Evaluate(definition.Root,_subtreeStack.Count+1);
        }
        catch(Exception e)when(e is IOException or InvalidDataException or JsonException){LastError=e.Message;Record(node.Id,"Sub-behaviour failed",e.Message);return BehaviorStatus.Failure;}
        finally{_subtreeStack.Remove(identity);}
    }
    private string ExplainCondition(BehaviorNode n)=>n.Test switch {
        BehaviorTest.HasTarget=>"No valid target is currently known",
        BehaviorTest.LineOfSight=>"Target is not visible: outside sight range/view cone or blocked by cover",
        BehaviorTest.WithinDistance=>$"Distance {(Target==null?-1:FlatDistance(PerceptionMemory?LastKnownPosition:Target.Transform.WorldPosition)):0.##} m; required <= {n.Distance:0.##} m",
        BehaviorTest.OutsideDistance=>$"Target is not farther than {n.Distance:0.##} m",
        BehaviorTest.MemoryTrue=>$"Memory '{n.Key}' is false or missing",
        BehaviorTest.MemoryNumberAtLeast=>$"Memory '{n.Key}' is below {n.Amount:0.##} or missing",
        BehaviorTest.HealthBelow=>$"Health is not below {n.Amount*100:0}%",
        BehaviorTest.HasMemory=>"No unexpired sight/hearing memory",
        _=>n.Test+" did not match"};
    private bool FindTacticalPosition(AIPositionKind kind,float distance,float radius,float spacing,float hold,string key)
    {
        _positionDeferred=false;
        if(Target==null&&!HasInterest)return false;var region=GetRegion();if(region?.Ready!=true){Record(Guid.Empty,"Position query failed","A baked navigation region is required");return false;}
        var world=AIWorld.For(GameObject.Scene!);Vector3 target=PerceptionMemory?LastKnownPosition:Target!.Transform.WorldPosition;
        if(_positionKind==kind&&Vector3.Distance(_positionCenter,target)<2&&world.TryPosition(this,out var held)&&world.PositionAvailable(this,held,spacing)) {Blackboard.Set(key,VariableValue.FromVector3(held));return true;}
        if(_positionNext.GetValueOrDefault(kind)>_clock||!world.TryPositionQuery(this)){_positionDeferred=true;return false;}
        _positionNext[kind]=_clock+Safe(PositionQueryInterval,.5f,.1f,5);
        Vector3? best=null;var from=Transform.WorldPosition-target;from.Y=0;if(from.LengthSquared()<.01f)from=Vector3.UnitZ;from=Vector3.Normalize(from);
        long queryStart=System.Diagnostics.Stopwatch.GetTimestamp();PositionQueriesCompleted++;
        int candidates=Math.Clamp(PositionCandidates,8,24);
        var options=new List<(Vector3 Point,float Score)>();
        for(int i=0;i<candidates;i++)
        {
            float angle=i*MathF.Tau/candidates;Vector3 offset=new(MathF.Sin(angle),0,MathF.Cos(angle));
            var candidate=kind==AIPositionKind.Cover?Transform.WorldPosition+offset*Math.Clamp(radius,1,15):target+offset*Math.Clamp(distance,1,20);candidate.Y=Transform.WorldPosition.Y;
            if(!world.PositionAvailable(this,candidate,Math.Max(.5f,spacing)))continue;
            bool visible=SightFrom(candidate,target,Target);if(kind==AIPositionKind.Cover&&visible||kind==AIPositionKind.FiringLane&&!visible)continue;
            float alignment=Vector3.Dot(offset,from);float score=Vector3.Distance(Transform.WorldPosition,candidate);
            if(kind==AIPositionKind.Flank){if(Math.Abs(alignment)>.85f)continue;score+=Math.Abs(alignment)*4;}
            if(kind==AIPositionKind.Surround)score+=Math.Abs(alignment)*.4f;
            if(kind==AIPositionKind.Cover)score+=Math.Abs(Vector3.Distance(candidate,target)-distance)*.6f;
            options.Add((candidate,score));
        }
        options.Sort((a,b)=>a.Score.CompareTo(b.Score));
        int cursor=_positionCursor.GetValueOrDefault(kind);if(cursor>=options.Count)cursor=0;
        int checks=0;
        for(;cursor<options.Count;cursor++)
        {
            if(checks++>=Math.Clamp(PositionPathsPerQuery,1,8)){_positionCursor[kind]=cursor;_positionDeferred=true;break;}
            var candidate=options[cursor].Point;
            var path=region.FindPath(Transform.WorldPosition,candidate,Math.Clamp(PositionVisitedBudget,100,10000));if(path.Count==0||Vector3.Distance(path[^1],candidate)>1)continue;
            candidate=path[^1];if(!world.PositionAvailable(this,candidate,Math.Max(.5f,spacing)))continue;
            bool visible=SightFrom(candidate,target,Target);if(kind==AIPositionKind.Cover&&visible||kind==AIPositionKind.FiringLane&&!visible)continue;
            best=candidate;_positionCursor[kind]=0;break;
        }
        if(best==null&&!_positionDeferred)_positionCursor[kind]=0;
        LastPositionQueryMilliseconds=System.Diagnostics.Stopwatch.GetElapsedTime(queryStart).TotalMilliseconds;
        if(best==null){Record(Guid.Empty,"Position query failed","No reachable "+kind+" candidate with valid spacing/visibility");return false;}
        _positionKind=kind;_positionCenter=target;world.ReservePosition(this,best.Value,Math.Max(.5f,hold),Math.Max(.5f,spacing));Blackboard.Set(key,VariableValue.FromVector3(best.Value));Record(Guid.Empty,"Position chosen",kind+": "+best.Value);return true;
    }
}
