using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using Component=ByteEngine.Core.Scene.Component;

namespace ByteEngine.Core.Gameplay;

/// <summary>Scene-level hit feedback. Popups outlive defeated objects; no enemy-specific HUD scripts are needed.</summary>
public sealed class CombatFeedback3D : Component
{
    public bool DamageNumbers { get; set; } = true;
    public bool Knockback { get; set; } = true;
    public bool EnemyAIOnly { get; set; } = true;
    [SceneObjectReference] public Guid FallbackDamageSourceId { get; set; }
    public float NumberLifetime { get; set; } = .85f;
    public float NumberHeight { get; set; } = 1.8f;
    public float NumberRise { get; set; } = 1.1f;
    public int FontSize { get; set; } = 28;
    public Vector4 NumberColor { get; set; } = new(1,.94f,.7f,1);
    public Vector4 ArmourColor {get;set;}=new(.55f,.6f,.65f,1);
    public Vector4 WeakPointColor { get; set; } = new(1,.62f,.06f,1);
    public float KnockbackSpeed { get; set; } = 4f;
    public float KnockbackDecay { get; set; } = 22f;
    public float CollisionRadius { get; set; } = .35f;
    [Browsable(false)] public int ActiveNumberCount => _numbers.Count;
    private sealed class Number { public Guid Owner; public Vector3 Position; public float Amount, Age; public bool Weak,Armoured; }
    private readonly List<Number> _numbers=[];
    private readonly HashSet<HealthComponent> _observed=[];
    private readonly Dictionary<GameObject,Vector3> _velocities=[];
    public override int UpdateOrder => 100;
    public override int? RenderOrder => 12000;
    protected override void OnStart() => Observe();
    private void Observe()
    {
        foreach(var o in GameObject.Scene!.GameObjects)
        {
            if(EnemyAIOnly && o.GetComponent<SimpleEnemyAI3D>()==null)continue;
            if(o.GetComponent<HealthComponent>() is {} h && _observed.Add(h))h.Damaged+=OnDamage;
        }
    }
    private void OnDamage(HealthComponent health,float amount)
    {
        if(amount<=0)return;
        var victim=health.GameObject;
        if(DamageNumbers)
        {
            // Multiple shotgun pellets in the same simulation frame become one readable total.
            var point=victim.Transform.WorldPosition+Vector3.UnitY*Math.Max(0,NumberHeight);
            var recent=_numbers.LastOrDefault(n=>n.Age==0 && n.Owner==victim.Id);
            if(recent!=null)recent.Amount+=amount;
            else { if(_numbers.Count>=96)_numbers.RemoveAt(0);_numbers.Add(new(){Owner=victim.Id,Position=point,Amount=amount,Weak=health.LastHitKind==CombatHitKind.WeakPoint || (health.LastHitKind==CombatHitKind.Normal&&health.IncomingDamageMultiplier>1),Armoured=health.LastHitKind==CombatHitKind.Armour}); }
        }
        if(!Knockback||health.IsDead)return;
        var direction=health.LastDamageDirection;direction.Y=0;
        if(direction.LengthSquared()<.0001f&&GameObject.Scene!.FindGameObject(FallbackDamageSourceId) is {} source)
        {direction=victim.Transform.WorldPosition-source.Transform.WorldPosition;direction.Y=0;}
        if(!float.IsFinite(direction.LengthSquared())||direction.LengthSquared()<.0001f)return;
        _velocities[victim]=Vector3.Normalize(direction)*Math.Clamp(KnockbackSpeed,0,20)*Math.Clamp(health.KnockbackMultiplier,0,2);
    }
    protected override void OnUpdate()
    {
        Observe();float dt=Math.Max(0,(float)Time.DeltaTime);
        foreach(var n in _numbers)n.Age+=dt;
        _numbers.RemoveAll(n=>n.Age>=Math.Max(.1f,NumberLifetime));
        foreach(var (o,velocity) in _velocities.ToArray())
        {
            float speed=velocity.Length();
            if(!o.ActiveInHierarchy||speed<.05f){_velocities.Remove(o);continue;}
            var dir=velocity/speed;float distance=speed*dt;
            float radius=o.GetComponent<SimpleEnemyAI3D>()?.CollisionRadius??CollisionRadius;
            if(distance>0&&!GameplayQuery3D.SphereCast(GameObject.Scene!,o.Transform.WorldPosition+Vector3.UnitY*Math.Max(.7f,radius+.08f),dir,Math.Max(.05f,radius),out _,distance+.025f,o,includeTriggers:false))o.Transform.WorldPosition+=dir*distance;
            _velocities[o]=dir*Math.Max(0,speed-Math.Max(1,KnockbackDecay)*dt);
        }
    }
    protected override void OnRender(RenderContext context)
    {
        if(!DamageNumbers||!context.Has3DCamera)return;
        context.QueueUiClip(null);var view=context.GetViewMatrix3D()*context.GetProjectionMatrix3D();
        foreach(var n in _numbers)
        {
            float t=Math.Clamp(n.Age/Math.Max(.1f,NumberLifetime),0,1);
            var p=Vector4.Transform(new Vector4(n.Position+Vector3.UnitY*(t*NumberRise),1),view);
            if(p.W<=0)continue;var ndc=new Vector2(p.X,p.Y)/p.W;if(Math.Abs(ndc.X)>1.1f||Math.Abs(ndc.Y)>1.1f)continue;
            var screen=new Vector2((ndc.X+1)*context.TargetWidth*.5f,(1-ndc.Y)*context.TargetHeight*.5f);
            var color=n.Weak?WeakPointColor:n.Armoured?ArmourColor:NumberColor;color.W*=Math.Clamp((1-t)*3,0,1);
            int size=Math.Clamp((int)(FontSize*Math.Clamp(context.TargetWidth/1600f,.75f,1.5f)*(1+.18f*(1-t))),12,72);
            string text=n.Amount.ToString("0");screen.X-=context.MeasureText(text,null,size).X*.5f;
            context.QueueUiText(text,null,size,screen+new Vector2(2),new(0,0,0,color.W*.9f),0,UiAnchor.TopLeft);
            context.QueueUiText(text,null,size,screen,color,0,UiAnchor.TopLeft);
        }
    }
    protected override void OnStop()
    {foreach(var h in _observed)h.Damaged-=OnDamage;_observed.Clear();_numbers.Clear();_velocities.Clear();}
}
