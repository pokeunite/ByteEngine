using System.Numerics;
namespace ByteEngine.Core.Vfx;

/// <summary>Editable layered examples. These are presentation templates, not attack/damage logic.</summary>
internal static class VfxAttackPresets
{
    public static VfxEffect Create(VfxPreset preset)
    {
        var e=new VfxEffect{Name=preset.ToString(),Duration=.6f,ParticleBudget=512};
        VfxLayer Layer(string name,VfxSprite sprite,Vector4 color,bool additive=true) => new(){Name=name,Sprite=sprite,StartColor=color,EndColor=new(color.X,color.Y,color.Z,0),Additive=additive,MaxParticles=96,Rate=0,Burst=1,Speed=0,Gravity=Vector3.Zero,StartSize=1,EndSize=1,Lifetime=.6f,LifetimeVariation=0,SizeVariation=.15f,RandomRotation=false,OpacityOverLife=new(){Keys=[new(0,0),new(.08f,1),new(.65f,.7f),new(1,0)]}};
        void Add(VfxLayer layer)=>e.Layers.Add(layer);
        var orange=new Vector4(1,.35f,.04f,1);var blue=new Vector4(.15f,.65f,1,1);var green=new Vector4(.2f,1,.45f,1);
        switch(preset)
        {
            case VfxPreset.Fireball:
            {
                e.Loop=true;e.Duration=1;
                var core=Layer("Hot core",VfxSprite.SoftDisc,new(1,.8f,.3f,1));core.LocalSpace=true;core.Rate=18;core.StartSize=.45f;core.EndSize=.15f;core.Lifetime=.18f;core.Brightness=2;Add(core);
                var flame=Layer("Animated flame",VfxSprite.Flame,orange);flame.LocalSpace=true;flame.Rate=22;flame.StartSize=.9f;flame.EndSize=.2f;flame.Lifetime=.35f;flame.Turbulence=.3f;flame.Dissolve=.8f;Add(flame);
                var ember=Layer("Ember wake",VfxSprite.Spark,orange);ember.Rate=35;ember.Speed=.5f;ember.Spread=180;ember.Lifetime=.55f;ember.StartSize=.07f;ember.EndSize=0;ember.Gravity=new(0,.5f,0);ember.RandomRotation=true;Add(ember);break;
            }
            case VfxPreset.WaterBolt:
            {
                e.Loop=true;
                var core=Layer("Water body",VfxSprite.Smoke,new(.2f,.65f,1,.75f),false);core.LocalSpace=true;core.Rate=16;core.Lifetime=.3f;core.StartSize=.65f;core.EndSize=.3f;core.RandomRotation=true;Add(core);
                var glint=Layer("White highlights",VfxSprite.Star,new(.8f,.95f,1,1));glint.LocalSpace=true;glint.Rate=8;glint.StartSize=.25f;glint.EndSize=0;glint.Shape=VfxShape.Sphere;glint.Extents=new(.2f);glint.RotationSpeed=80;Add(glint);
                var wake=Layer("Water wake",VfxSprite.SoftDisc,blue);wake.Rate=35;wake.Lifetime=.45f;wake.StartSize=.25f;wake.EndSize=.04f;wake.Speed=.25f;wake.Spread=180;Add(wake);break;
            }
            case VfxPreset.LightningStrike:
            {
                e.Duration=.3f;
                var bolt=Layer("Forked bolt",VfxSprite.Lightning,new(.65f,.8f,1,1));bolt.StartSize=3;bolt.EndSize=3;bolt.Lifetime=.22f;bolt.Offset=new(0,1.4f,0);bolt.Brightness=3;Add(bolt);
                var flash=Layer("Contact flash",VfxSprite.Star,new(.8f,.9f,1,1));flash.StartSize=.7f;flash.EndSize=1.5f;flash.Lifetime=.18f;flash.Brightness=2;Add(flash);
                var sparks=Layer("Electric sparks",VfxSprite.Spark,blue);sparks.Burst=24;sparks.Speed=4;sparks.Spread=180;sparks.StartSize=.06f;sparks.EndSize=0;sparks.Lifetime=.35f;sparks.RenderMode=VfxRenderMode.Stretched;Add(sparks);break;
            }
            case VfxPreset.ClawSlash:
            {
                e.Duration=.25f;
                for(int i=0;i<3;i++){var slash=Layer("Claw arc "+(i+1),VfxSprite.Arc,new(.7f,.9f,1,1));slash.Offset=new(i*.16f-.16f,i*.14f,0);slash.Delay=i*.025f;slash.StartSize=1.2f;slash.EndSize=1.6f;slash.Lifetime=.28f;slash.Dissolve=1;slash.Brightness=1.8f;Add(slash);}break;
            }
            case VfxPreset.HealingPulse:
            {
                e.Duration=1;
                var ring=Layer("Ground pulse",VfxSprite.Ripple,green);ring.RenderMode=VfxRenderMode.Horizontal;ring.Offset=new(0,.03f,0);ring.StartSize=2;ring.EndSize=3;ring.Lifetime=1.1f;Add(ring);
                var motes=Layer("Rising motes",VfxSprite.Star,green);motes.Burst=18;motes.Shape=VfxShape.Ring;motes.Extents=new(.6f);motes.Speed=.8f;motes.Spread=12;motes.StartSize=.12f;motes.EndSize=0;motes.Lifetime=1.2f;motes.RandomRotation=true;Add(motes);
                var aura=Layer("Soft aura",VfxSprite.SoftDisc,new(.2f,1,.6f,.3f));aura.StartSize=1;aura.EndSize=2;aura.Lifetime=1;Add(aura);break;
            }
            case VfxPreset.PoisonCloud:
            {
                e.Loop=true;e.Duration=2;
                var fog=Layer("Rolling poison",VfxSprite.Smoke,new(.45f,.65f,.15f,.5f),false);fog.Burst=8;fog.Rate=10;fog.Lifetime=2.5f;fog.StartSize=.5f;fog.EndSize=1.8f;fog.Speed=.2f;fog.Shape=VfxShape.Sphere;fog.Extents=new(.6f);fog.Gravity=new(0,.1f,0);fog.Turbulence=.4f;fog.Dissolve=.9f;fog.RandomRotation=true;Add(fog);
                var bubbles=Layer("Poison flecks",VfxSprite.Ring,new(.55f,1,.1f,.6f));bubbles.Rate=8;bubbles.Shape=VfxShape.Sphere;bubbles.Extents=new(.6f);bubbles.Speed=.35f;bubbles.StartSize=.09f;bubbles.EndSize=.02f;bubbles.Lifetime=1.6f;Add(bubbles);break;
            }
            case VfxPreset.IceBurst:
            {
                var shards=Layer("Ice shards",VfxSprite.Star,new(.4f,.85f,1,1));shards.RenderMode=VfxRenderMode.Stretched;shards.Burst=28;shards.Speed=4;shards.Spread=180;shards.Gravity=new(0,-3,0);shards.Lifetime=.8f;shards.StartSize=.1f;shards.EndSize=0;Add(shards);
                var mist=Layer("Cold mist",VfxSprite.Smoke,new(.65f,.85f,1,.4f),false);mist.Burst=9;mist.Speed=.6f;mist.Spread=180;mist.StartSize=.4f;mist.EndSize=1.3f;mist.Lifetime=1;mist.Dissolve=1;mist.RandomRotation=true;Add(mist);break;
            }
            case VfxPreset.Shield:
            {
                e.Loop=true;
                var field=Layer("Shield field",VfxSprite.Ring,new(.15f,.5f,1,.7f));field.LocalSpace=true;field.Rate=2;field.Lifetime=1.2f;field.StartSize=1.6f;field.EndSize=2;Add(field);
                var glints=Layer("Shield glints",VfxSprite.Star,new(.5f,.85f,1,1));glints.LocalSpace=true;glints.Rate=8;glints.Shape=VfxShape.Ring;glints.Extents=new(.7f);glints.StartSize=.14f;glints.EndSize=0;glints.Lifetime=.5f;Add(glints);break;
            }
            default:throw new ArgumentOutOfRangeException(nameof(preset));
        }
        e.Validate();return e;
    }
}
