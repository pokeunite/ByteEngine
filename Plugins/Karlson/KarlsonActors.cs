using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
namespace Karlson.Plugin;

public sealed class KarlsonWeapon3D:Component
{
    public string DisplayName{get;set;}="PULSE PISTOL";
    public int StartingAmmo{get;set;}=12;
    public float Recoil{get;set;}=1.4f;
    public int Ammo{get;private set;}
    public bool Held{get;private set;}
    Rigidbody3D? _body;Collider3D? _collider;HitscanEmitter3D? _emitter;KarlsonDirector3D? _director;float _thrown;
    MeshRenderer? _mesh;MaterialShadingMode _worldShading;Vector4 _worldColor;
    protected override void OnStart(){Ammo=StartingAmmo;_body=GameObject.GetComponent<Rigidbody3D>();_collider=GameObject.GetComponent<Collider3D>();_emitter=GameObject.GetComponent<HitscanEmitter3D>();_director=GameObject.Scene?.FindComponent<KarlsonDirector3D>();_mesh=GameObject.GetComponent<MeshRenderer>();if(_mesh!=null){_worldShading=_mesh.Material.Shading;_worldColor=_mesh.Material.BaseColor;}if(_emitter!=null)_emitter.Hit+=Impact;}
    public void Equip(ParkourPlayer3D player)
    {
        Held=true;_thrown=0;if(_body!=null){_body.Enabled=false;_body.Velocity=Vector3.Zero;}if(_collider!=null)_collider.Enabled=false;
        var eye=GameObject.Scene?.FindGameObject(player.CameraObjectId);if(eye!=null){GameObject.SetParent(eye,false);Transform.LocalPosition=new(.31f,-.22f,-.92f);Transform.LocalRotation=Quaternion.Identity;Transform.ResetInterpolation();}
        // Flat view-model color stays readable under intense facility lighting.
        if(_mesh!=null){_mesh.Material.Shading=MaterialShadingMode.Unlit;_mesh.Material.BaseColor=new(.08f,.22f,.29f,1);}
    }
    public void Drop(ParkourPlayer3D player,bool launch)
    {
        Held=false;GameObject.SetParent(null,true);Transform.WorldPosition=player.EyePosition+player.AimDirection*.8f;
        if(_mesh!=null){_mesh.Material.Shading=_worldShading;_mesh.Material.BaseColor=_worldColor;}
        if(_collider!=null)_collider.Enabled=true;if(_body!=null){_body.Enabled=true;_body.BodyType=RigidbodyBodyType3D.Dynamic;_body.Velocity=player.AimDirection*(launch?20:2)+Vector3.UnitY*2;_body.AngularVelocity=new(3,7,1);}
        Transform.ResetInterpolation();_thrown=launch?2:0;if(launch)_director?.Sound("throw");
    }
    public bool Shoot(ParkourPlayer3D player)
    {
        if(!Held||Ammo<=0||_emitter==null||!_emitter.Fire(player.EyePosition,player.AimDirection,player.GameObject))return false;
        Ammo--;player.Recoil(Recoil);_director?.Sound("shot");
        DrawTrace(GameObject.Scene!,player.EyePosition+player.AimDirection*.7f,player.EyePosition+player.AimDirection*35,new(.2f,.85f,1,1),.035f,.055f);
        return true;
    }
    void Impact(RaycastHit3D hit){hit.GameObject.GetComponent<KarlsonProp3D>()?.Impact(_emitter?.Damage??25);_director?.HitMarker();}
    protected override void OnUpdate(){_thrown=Math.Max(0,_thrown-(float)Time.DeltaTime);if(Held)Transform.LocalPosition=new(.31f,-.22f,-.92f);}
    protected override void OnCollisionEnter(PhysicsContact3D contact)
    {if(_thrown<=0)return;if(contact.Other.GetComponent<KarlsonEnemy3D>()!=null){contact.Other.GetComponent<HealthComponent>()?.Damage(100);_director?.HitMarker();_thrown=0;}}
    protected override void OnStop(){if(_emitter!=null)_emitter.Hit-=Impact;}
    internal static void DrawTrace(Scene scene,Vector3 start,Vector3 end,Vector4 color,float width,float life)
    {
        if(scene.GameObjectCount>550)return;var delta=end-start;float length=delta.Length();if(length<.001f)return;
        var obj=scene.CreateGameObject("Transient combat trail");obj.Layer=5;obj.Transform.WorldPosition=(start+end)*.5f;obj.Transform.LocalScale=new(width,width,length);
        var dir=delta/length;float dot=Vector3.Dot(-Vector3.UnitZ,dir);obj.Transform.WorldRotation=dot<-.999f?Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI):Quaternion.Normalize(new Quaternion(Vector3.Cross(-Vector3.UnitZ,dir),1+dot));
        obj.AddComponent(new MeshRenderer{UsePrimitive=true,CastShadows=false,Material=new(){BaseColor=color,Shading=MaterialShadingMode.Unlit}});obj.AddComponent(new LifetimeComponent{LifetimeSeconds=life});
    }
}

public sealed class KarlsonEnemy3D:Component
{
    public float SightRange{get;set;}=22;
    public float AttackInterval{get;set;}=1.4f;
    public float ReactionDelay{get;set;}=.7f;
    public float MoveSpeed{get;set;}=2.5f;
    public bool Pursue{get;set;}=true;
    public int EnemyIndex{get;set;}
    public bool Alive=>_health?.IsDead==false;
    ParkourPlayer3D? _player;KarlsonDirector3D? _director;HealthComponent? _health;CharacterController3D? _motor;float _sense,_visible,_fire;bool _dead;int _shots;
    protected override void OnStart(){_player=GameObject.Scene?.FindComponent<ParkourPlayer3D>();_director=GameObject.Scene?.FindComponent<KarlsonDirector3D>();_health=GameObject.GetComponent<HealthComponent>();_motor=GameObject.GetComponent<CharacterController3D>();if(_health!=null)_health.Died+=Die;_fire=ReactionDelay;_sense=EnemyIndex*.03f;}
    protected override void OnUpdate()
    {
        if(_dead||_player==null||_director?.Playing!=true)return;float dt=(float)Time.DeltaTime;_fire-=dt;_sense-=dt;
        if(_sense<=0){_sense=.12f;var origin=Transform.WorldPosition+Vector3.UnitY*1.4f;var delta=_player.EyePosition-origin;_visible=delta.Length()<SightRange&&GameplayQuery3D.Raycast(GameObject.Scene!,origin,delta,out var hit,SightRange,GameObject,includeTriggers:false)&&hit.GameObject==_player.GameObject?1:0;}
        if(_visible==0)return;
        var offset=_player.Transform.WorldPosition-Transform.WorldPosition;offset.Y=0;if(offset.LengthSquared()>.01f)Transform.WorldRotation=Quaternion.CreateFromYawPitchRoll(MathF.Atan2(-offset.X,-offset.Z),0,0);
        if(Pursue&&offset.Length()>7)_motor?.Move(Vector3.Normalize(offset));
        if(_fire<=0){_fire=AttackInterval;var from=Transform.WorldPosition+Vector3.UnitY*1.3f;var dir=Vector3.Normalize(_player.EyePosition-from);var right=Vector3.Cross(dir,Vector3.UnitY);float spread=Math.Max(.012f,.065f-_shots++*.022f);dir=Vector3.Normalize(dir+right*((EnemyIndex%2==0?1:-1)*spread));
            if(GameplayQuery3D.Raycast(GameObject.Scene!,from,dir,out var impact,SightRange,GameObject,includeTriggers:false)){KarlsonWeapon3D.DrawTrace(GameObject.Scene!,from,impact.Point,new(1,.18f,.12f,1),.05f,.12f);if(impact.GameObject==_player.GameObject)_player.GameObject.GetComponent<HealthComponent>()?.Damage(35);}
        }
    }
    void Die(HealthComponent health)
    {
        if(_dead)return;_dead=true;if(_motor!=null)_motor.Enabled=false;
        foreach(var child in GameObject.Children)if(child.GetComponent<MeshRenderer>() is {} renderer)renderer.Material.BaseColor=new(.16f,.2f,.25f,1);
        var body=GameObject.GetComponent<Rigidbody3D>();if(body!=null){body.Enabled=true;body.BodyType=RigidbodyBodyType3D.Dynamic;body.SimulateRotation=true;body.AngularVelocity=new(1,2,4);body.AddImpulse(-Transform.Forward*6+Vector3.UnitY*2);}
        var gun=GameObject.Children.FirstOrDefault(o=>o.GetComponent<KarlsonWeapon3D>()!=null);if(gun!=null){gun.SetParent(null,true);gun.Transform.WorldPosition+=Vector3.UnitY*.5f;var rb=gun.GetComponent<Rigidbody3D>();if(rb!=null){rb.Enabled=true;rb.Velocity=Vector3.UnitY*3;}if(gun.GetComponent<Collider3D>() is {} col)col.Enabled=true;}
        GameObject.AddComponent(new LifetimeComponent{LifetimeSeconds=8});_director?.Sound("hit");
    }
    protected override void OnStop(){if(_health!=null)_health.Died-=Die;}
}

public sealed class KarlsonProp3D:Component
{
    public bool Breakable{get;set;}
    public bool Explosive{get;set;}
    public float Threshold{get;set;}=20;
    public float BlastRadius{get;set;}=5;
    bool _broken;float _last;readonly HashSet<Guid> _damaged=new();
    public void Impact(float damage)
    {
        if(_broken||!Breakable||damage<Threshold)return;_broken=true;var scene=GameObject.Scene;if(scene==null)return;
        scene.FindComponent<KarlsonDirector3D>()?.Sound(Explosive?"explosion":"glass");
        if(Explosive){foreach(var overlap in GameplayQuery3D.OverlapSphere(scene,Transform.WorldPosition,BlastRadius,GameObject,includeTriggers:false))if(_damaged.Add(overlap.GameObject.Id)){var delta=overlap.GameObject.Transform.WorldPosition-Transform.WorldPosition;var direction=delta.LengthSquared()>.01f?Vector3.Normalize(delta):Vector3.UnitY;overlap.GameObject.GetComponent<HealthComponent>()?.Damage(90);overlap.GameObject.GetComponent<Rigidbody3D>()?.AddImpulse(direction*15);}}
        for(int i=0;i<6&&scene.GameObjectCount<500;i++){var debris=scene.CreateGameObject("Budgeted breakable fragment");debris.Layer=5;debris.Transform.WorldPosition=Transform.WorldPosition+new Vector3((i%3-1)*.2f,.1f*i,0);debris.Transform.LocalScale=new(.16f);debris.AddComponent(new MeshRenderer{UsePrimitive=true,CastShadows=false,Material=new(){BaseColor=Explosive?new(1,.42f,.08f,1):new(.35f,.8f,.95f,1)}});debris.AddComponent(new BoxCollider3D());debris.AddComponent(new Rigidbody3D{Mass=.1f,Velocity=new((i%3-1)*3,3+i*.2f,(i%2==0?1:-1)*3)});debris.AddComponent(new LifetimeComponent{LifetimeSeconds=2});}
        scene.DestroyGameObject(GameObject);
    }
    protected override void OnCollisionEnter(PhysicsContact3D contact)
    {
        if(GameObject.Scene==null)return;var now=(float)GameObject.Scene.SimulationTime;if(now-_last<.2f)return;_last=now;
        float speed=GameObject.GetComponent<Rigidbody3D>()?.Velocity.Length()??0;
        if(speed>5&&contact.Other.GetComponent<KarlsonEnemy3D>()!=null)contact.Other.GetComponent<HealthComponent>()?.Damage(speed*8);
        if(Breakable&&contact.Other.GetComponent<ParkourPlayer3D>() is {} p&&p.Speed>7)Impact(50);
    }
}

public sealed class MilkGoal3D:Component
{
    public float Radius{get;set;}=1.5f;
    public bool Reached{get;private set;}
    ParkourPlayer3D? _player;Vector3 _home;
    protected override void OnStart(){_home=Transform.LocalPosition;_player=GameObject.Scene?.FindComponent<ParkourPlayer3D>();}
    protected override void OnUpdate(){if(_player?.Playing==true&&Vector3.Distance(_player.Transform.WorldPosition+Vector3.UnitY*.8f,Transform.WorldPosition)<Radius)Reached=true;Transform.LocalPosition=_home+Vector3.UnitY*MathF.Sin((float)(GameObject.Scene?.SimulationTime??0)*2)*.08f;}
}
public sealed class CourseGate3D:Component
{
    public float OpenHeight{get;set;}=4;
    public bool IsOpen{get;private set;}
    Vector3 _home;
    protected override void OnStart()=>_home=Transform.LocalPosition;
    public void Open(){IsOpen=true;if(GameObject.GetComponent<Collider3D>() is {} col)col.Enabled=false;}
    protected override void OnUpdate(){if(IsOpen)Transform.LocalPosition=Vector3.Lerp(Transform.LocalPosition,_home+Vector3.UnitY*OpenHeight,1-MathF.Exp(-(float)Time.DeltaTime*5));}
}
