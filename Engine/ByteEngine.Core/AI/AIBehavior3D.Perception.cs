using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
namespace ByteEngine.Core.AI;
public sealed partial class AIBehavior3D
{
    [Category("Perception")] public bool PerceptionMemory {get;set;}
    [Category("Perception")] public float FieldOfView {get;set;}=240;
    [Category("Perception")] public float MemorySeconds {get;set;}=6;
    [Category("Perception")] public float HearingRange {get;set;}=22;
    [Category("Perception")] public float HearingMemorySeconds {get;set;}=5;
    [Category("Perception")] public float OccludedHearingMultiplier {get;set;}=.5f;
    [Category("Squad")] public string Squad {get;set;}="Enemies";
    [Category("Squad")] public int MaximumAttackers {get;set;}=2;
    [Category("Squad")] public float PositionSpacing {get;set;}=1.5f;
    [Category("Position queries")] public float PositionQueryInterval {get;set;}=.5f;
    [Category("Position queries")] public int PositionCandidates {get;set;}=12;
    [Category("Position queries")] public int PositionPathsPerQuery {get;set;}=3;
    [Category("Position queries")] public int PositionVisitedBudget {get;set;}=2000;
    [Category("Live State")] public int PositionQueriesCompleted {get;private set;}
    [Category("Live State")] public double LastPositionQueryMilliseconds {get;private set;}
    [Category("Live State")] public string Awareness {get;private set;}="Unaware";
    [Category("Live State")] public Vector3 LastKnownPosition {get;private set;}
    [Browsable(false)] public bool HasInterest=>_interestUntil>_clock;
    [Browsable(false)] public double BehaviorTime=>_clock;
    private double _interestUntil;
    private Guid _rememberedTarget;
    private long _noiseRead;
    private string _previousAwareness="";
    private void SenseAdvanced()
    {
        var scene=GameObject.Scene!;
        var candidates=TargetObjectId!=Guid.Empty?new[]{scene.FindGameObject(TargetObjectId)}:
            FindNearestCharacter?scene.GameObjects.Where(o=>o!=GameObject&&o.GetComponent<CharacterController3D>() is {Enabled:true}).OrderBy(o=>Vector3.DistanceSquared(o.Transform.WorldPosition,Transform.WorldPosition)).Cast<GameObject?>().Take(32).ToArray():[];
        var visible=candidates.FirstOrDefault(CanSee);
        bool heard=false;
        foreach(var noise in AIWorld.For(scene).Noises.Where(n=>n.Sequence>_noiseRead).ToArray())
        {
            _noiseRead=Math.Max(_noiseRead,noise.Sequence);
            if(noise.Source==GameObject.Id||scene.SimulationTime-noise.Time>1)continue;
            float range=Math.Min(noise.Radius,Safe(HearingRange,22,0,1000));
            var origin=Transform.WorldPosition+Vector3.UnitY*EyeHeight;var end=noise.Position+Vector3.UnitY*.3f;var delta=end-origin;
            if(delta.LengthSquared()>.01f&&GameplayQuery3D.Raycast(scene,origin,Vector3.Normalize(delta),out var hit,delta.Length(),GameObject,includeTriggers:false)&&hit.GameObject.Id!=noise.Source)range*=Safe(OccludedHearingMultiplier,.5f,0,1);
            if(Vector3.Distance(Transform.WorldPosition,noise.Position)>range)continue;
            if(visible==null){LastKnownPosition=noise.Position;_interestUntil=_clock+Safe(HearingMemorySeconds,5,0,60);if(noise.Source==TargetObjectId||scene.FindGameObject(noise.Source)?.GetComponent<CharacterController3D>()!=null)_rememberedTarget=noise.Source;}
            heard=true;Record(Guid.Empty,"Heard "+noise.Kind,"Investigate "+noise.Position);
        }
        if(visible!=null)
        {
            LastKnownPosition=visible.Transform.WorldPosition;_rememberedTarget=visible.Id;_interestUntil=_clock+Safe(MemorySeconds,6,0,60);Awareness="Visible";
        }
        else Awareness=HasInterest?(heard?"Heard a noise":"Searching last known position"):"Unaware";
        if(Awareness!=_previousAwareness){Record(Guid.Empty,"Perception",Awareness+"; last known "+LastKnownPosition);_previousAwareness=Awareness;}
        Target=visible??(HasInterest?scene.FindGameObject(_rememberedTarget):null);
        if(Target?.ActiveInHierarchy!=true||Target?.GetComponent<HealthComponent>()?.IsDead==true)Target=null;
        Blackboard.Set("HasTarget",VariableValue.FromBoolean(Target!=null));
        Blackboard.Set("HasLineOfSight",VariableValue.FromBoolean(visible!=null));
        Blackboard.Set("HasMemory",VariableValue.FromBoolean(HasInterest));
        Blackboard.Set("HeardNoise",VariableValue.FromBoolean(heard));
        Blackboard.Set("TargetDistance",VariableValue.FromNumber(HasInterest?FlatDistance(LastKnownPosition):Safe(DetectionRange,25,0,10000)));
        Blackboard.Set("TargetPosition",VariableValue.FromVector3(LastKnownPosition));
        Blackboard.Set("LastKnownPosition",VariableValue.FromVector3(LastKnownPosition));
        if(GameObject.GetComponent<SimpleEnemyAI3D>() is {} combat){Blackboard.Set("EvadedCharges",VariableValue.FromNumber(combat.EvadedCharges));Blackboard.Set("EvadedSlams",VariableValue.FromNumber(combat.EvadedSlams));Blackboard.Set("AttackPhase",VariableValue.FromString(combat.AttackPhase));Blackboard.Set("IsDodging",VariableValue.FromBoolean(combat.CurrentTactic=="Dodge"));}
        Blackboard.Set("IsEnraged",VariableValue.FromBoolean(GameObject.GetComponent<CombatArmour3D>()?.IsEnraged==true));
        Blackboard.Set("HealthFraction",VariableValue.FromNumber(GameObject.GetComponent<HealthComponent>()?.HealthPercent??1));
    }
    private bool CanSee(GameObject? target)
    {
        if(target==null||target==GameObject||!target.ActiveInHierarchy||target.GetComponent<HealthComponent>()?.IsDead==true)return false;
        var delta=target.Transform.WorldPosition-Transform.WorldPosition;
        if(delta.Length()>Safe(DetectionRange,25,0,10000))return false;
        delta.Y=0;
        if(delta.LengthSquared()>.001f&&Vector3.Dot(Vector3.Normalize(delta),Transform.Forward)<MathF.Cos(Safe(FieldOfView,240,1,360)*MathF.PI/360))return false;
        return SightFrom(Transform.WorldPosition,target.Transform.WorldPosition,target);
    }
    private bool SightFrom(Vector3 from,Vector3 to,GameObject? target)
    {
        var origin=from+Vector3.UnitY*Safe(EyeHeight,.8f,0,100);var delta=to+Vector3.UnitY*Safe(EyeHeight,.8f,0,100)-origin;
        return delta.LengthSquared()<.001f||!GameplayQuery3D.Raycast(GameObject.Scene!,origin,Vector3.Normalize(delta),out var hit,delta.Length(),GameObject,new ByteEngine.Core.Classification.LayerMask(unchecked((uint)SightMaskBits)),includeTriggers:false)||hit.GameObject==target||(target!=null&&hit.GameObject.IsDescendantOf(target));
    }
    public void ForgetTarget(){_interestUntil=0;_rememberedTarget=Guid.Empty;Target=null;Awareness="Unaware";LastKnownPosition=Vector3.Zero;foreach(var key in new[]{"HasTarget","HasMemory","HasLineOfSight","HeardNoise"})Blackboard.Set(key,VariableValue.FromBoolean(false));Blackboard.Set("LastKnownPosition",VariableValue.FromVector3(Vector3.Zero));Blackboard.Set("TargetPosition",VariableValue.FromVector3(Vector3.Zero));}
}
