using ByteEngine.Core.Assets;
namespace ByteEngine.Core.AI;
public enum AIBehaviorRole { Melee, Ranged, Caster, Charger, Boss }
/// <summary>Editable starting templates, not runtime dispatch by enemy names.</summary>
public static class AIBehaviorPresets
{
    public static BehaviorTreeDefinition AttackStages()=>new(){Name="Shared staged attack",Root=new(){Kind=BehaviorNodeKind.Sequence,Name="One readable attack",Children=[
        new(){Kind=BehaviorNodeKind.ReserveAttack,Name="Wait for a squad attack opening",Duration=15,Timeout=3},
        new(){Kind=BehaviorNodeKind.ChooseAttack,Name="Choose an available attack"},
        new(){Kind=BehaviorNodeKind.Telegraph,Name="Show the attack warning"},
        new(){Kind=BehaviorNodeKind.CommitAttack,Name="Commit to the locked aim"},
        new(){Kind=BehaviorNodeKind.Strike,Name="Execute the attack"},
        new(){Kind=BehaviorNodeKind.Recover,Name="Leave a recovery opportunity"},
        new(){Kind=BehaviorNodeKind.ReleasePosition,Name="Release the approach position"}
    ]}};
    public static BehaviorTreeDefinition Create(AIBehaviorRole role,AssetReference sharedAttack,bool flank=false)
    {
        bool ranged=role is AIBehaviorRole.Ranged or AIBehaviorRole.Caster;
        var steps=new List<BehaviorNode>();
        if(role is AIBehaviorRole.Charger or AIBehaviorRole.Boss)steps.Add(new(){Kind=BehaviorNodeKind.MoveToTarget,Name="Approach to charge range",Distance=5,Speed=2.8f,Timeout=2});
        else
        {
            steps.Add(new(){Kind=BehaviorNodeKind.FindPosition,Name=ranged?"Choose a clear firing lane":flank?"Choose a flank":"Choose a separate approach angle",PositionKind=ranged?AIPositionKind.FiringLane:flank?AIPositionKind.Flank:AIPositionKind.Surround,Key="CombatPoint",Distance=ranged?8.5f:1.3f,SearchRadius=5,Spacing=1.5f,Duration=3});
            steps.Add(new(){Kind=BehaviorNodeKind.MoveToPoint,Name=ranged?"Move into the firing lane":"Approach the chosen position",Key="CombatPoint",Distance=ranged?.5f:.3f,Speed=ranged?2.7f:4,Timeout=2});
        }
        steps.Add(new(){Kind=BehaviorNodeKind.FaceTarget,Name="Face the target"});steps.Add(new(){Kind=BehaviorNodeKind.SubBehavior,Name="Warn, commit, strike and recover",SubBehavior=sharedAttack});
        var branches=new List<BehaviorNode>();
        if(ranged)branches.Add(new(){Kind=BehaviorNodeKind.Gate,Name="Create space when pressured",Test=BehaviorTest.WithinDistance,Distance=5.5f,Children=[new(){Kind=BehaviorNodeKind.Cooldown,Name="Avoid constantly changing cover",Duration=3.5f,Children=[new(){Kind=BehaviorNodeKind.Sequence,Name="Take cover briefly",Children=[new(){Kind=BehaviorNodeKind.FindPosition,Name="Find reachable cover",PositionKind=AIPositionKind.Cover,SearchRadius=5,Distance=8.5f,Key="CoverPoint",Duration=2},new(){Kind=BehaviorNodeKind.MoveToPoint,Name="Move behind cover",Key="CoverPoint",Speed=3,Distance=.4f,Timeout=2},new(){Kind=BehaviorNodeKind.Wait,Name="Pause behind cover",Duration=.6f},new(){Kind=BehaviorNodeKind.ReleasePosition,Name="Release cover"}]}]}]});
        branches.Add(new(){Kind=BehaviorNodeKind.Gate,Name="Fight a visible target",Test=BehaviorTest.LineOfSight,Interruption=BehaviorInterruption.FinishCommittedAttack,Children=[new(){Kind=BehaviorNodeKind.Sequence,Name="Position and attack",Children=steps}]});
        branches.Add(new(){Kind=BehaviorNodeKind.Gate,Name="Investigate a lost target or noise",Test=BehaviorTest.HasMemory,Children=[new(){Kind=BehaviorNodeKind.Search,Name="Search the last known area",Duration=6,SearchRadius=3,Speed=ranged?2.7f:3.5f}]});
        branches.Add(new(){Kind=BehaviorNodeKind.Wait,Name="Stay alert",Duration=.5f});
        return new(){Name=role+" perception and combat",Services=[new(){Kind=AIServiceKind.HealthFlag,Name="Watch for low health",Key="LowHealth",Threshold=.3f,Interval=.5f}],Root=new(){Kind=BehaviorNodeKind.Priority,Name="Choose the current response",Interruption=BehaviorInterruption.FinishCommittedAttack,Children=branches}};
    }
}
