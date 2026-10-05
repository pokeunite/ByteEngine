using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using ByteEngine.Core.Assets;

namespace ByteEngine.Core.Vfx;

public enum VfxPreset { Sparks, Fire, Smoke, Explosion, MuzzleFlash, Rain, Snow, Magic, Dust, Trail, Beam, Impact, Portal, Heal, Footstep, EnergyShot }
public enum VfxShape { Point, Sphere, Box, Cone, Ring }
public enum VfxRenderMode { Billboard, Stretched, Trail, Beam }
public enum VfxSprite { SoftDisc, Spark, Ring, Solid }

/// <summary>Portable, versioned effect. Layers share a bounded per-instance budget.</summary>
public sealed class VfxEffect
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "New Effect";
    public bool Loop { get; set; }
    public float Duration { get; set; } = 1;
    public int ParticleBudget { get; set; } = 1024;
    public List<VfxLayer> Layers { get; set; } = new();
    public void Validate()
    {
        if (Version != 1) throw new InvalidDataException("Unsupported VFX version.");
        Name = string.IsNullOrWhiteSpace(Name) ? "New Effect" : Name;
        Duration = Safe(Duration, 1, .01f, 120);
        ParticleBudget = Math.Clamp(ParticleBudget, 1, 8192);
        Layers ??= new();
        if (Layers.Count > 8) throw new InvalidDataException("An effect supports up to eight layers.");
        foreach (var layer in Layers) { if (layer == null) throw new InvalidDataException("Null VFX layer."); layer.Validate(); }
    }
    internal static float Safe(float v, float fallback, float min, float max) => Math.Clamp(float.IsFinite(v) ? v : fallback, min, max);
    internal static Vector3 Finite(Vector3 v, Vector3 fallback) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z) ? v : fallback;
}

public sealed class VfxLayer
{
    public string Name { get; set; } = "Particles";
    public bool Enabled { get; set; } = true;
    public VfxShape Shape { get; set; }
    public VfxRenderMode RenderMode { get; set; }
    public VfxSprite Sprite { get; set; }
    public bool Additive { get; set; } = true;
    public bool LocalSpace { get; set; }
    public AssetReference Texture { get; set; } = AssetReference.Empty;
    public int FlipbookColumns { get; set; } = 1;
    public int FlipbookRows { get; set; } = 1;
    public int MaxParticles { get; set; } = 256;
    public float Rate { get; set; } = 40;
    public int Burst { get; set; } = 30;
    public float Delay { get; set; }
    public float Lifetime { get; set; } = .8f;
    public float LifetimeVariation { get; set; } = .2f;
    public float Speed { get; set; } = 3;
    public float SpeedVariation { get; set; } = .3f;
    public Vector3 Direction { get; set; } = Vector3.UnitY;
    public float Spread { get; set; } = 45;
    public Vector3 Extents { get; set; } = Vector3.One;
    public Vector3 Offset { get; set; }
    public Vector3 Gravity { get; set; } = new(0, -9.81f, 0);
    public float Drag { get; set; } = .2f;
    public float StartSize { get; set; } = .12f;
    public float EndSize { get; set; } = .01f;
    public float SizeVariation { get; set; } = .2f;
    public Vector4 StartColor { get; set; } = new(1, .6f, .1f, 1);
    public Vector4 EndColor { get; set; } = new(1, .1f, 0, 0);
    public float RotationSpeed { get; set; }
    public float Stretch { get; set; } = .1f;
    public Vector3 BeamEnd { get; set; } = new(0, 0, -5);
    public bool GroundBounce { get; set; }
    public float GroundHeight { get; set; }
    public float Bounciness { get; set; } = .3f;
    public VfxCurve? SizeOverLife { get; set; }
    public VfxCurve? OpacityOverLife { get; set; }
    public VfxCurve? SpeedOverLife { get; set; }
    public VfxCurve? ColorBlendOverLife { get; set; }
    public Vector3 Wind { get; set; }
    public float Turbulence { get; set; }
    public float NoiseFrequency { get; set; } = 1;
    public Vector3 AttractionPoint { get; set; }
    public float Attraction { get; set; }
    public float TrailBreakDistance { get; set; } = 3;
    public void Validate()
    {
        Name = string.IsNullOrWhiteSpace(Name) ? "Particles" : Name;
        if (!Enum.IsDefined(Shape) || !Enum.IsDefined(RenderMode) || !Enum.IsDefined(Sprite)) throw new InvalidDataException("Invalid VFX mode.");
        MaxParticles = Math.Clamp(MaxParticles, 1, 8192); Burst = Math.Clamp(Burst, 0, 8192);
        Rate = VfxEffect.Safe(Rate, 40, 0, 10000); Delay = VfxEffect.Safe(Delay, 0, 0, 120);
        Lifetime = VfxEffect.Safe(Lifetime, .8f, .02f, 120); LifetimeVariation = VfxEffect.Safe(LifetimeVariation, .2f, 0, .95f);
        Speed = VfxEffect.Safe(Speed, 3, 0, 1000); SpeedVariation = VfxEffect.Safe(SpeedVariation, .3f, 0, 1);
        SizeVariation = VfxEffect.Safe(SizeVariation, .2f, 0, .95f);
        Spread = VfxEffect.Safe(Spread, 45, 0, 180); Drag = VfxEffect.Safe(Drag, .2f, 0, 100);
        StartSize = VfxEffect.Safe(StartSize, .12f, .001f, 100); EndSize = VfxEffect.Safe(EndSize, .01f, 0, 100);
        RotationSpeed = VfxEffect.Safe(RotationSpeed, 0, -3600, 3600); Stretch = VfxEffect.Safe(Stretch, .1f, 0, 10);
        GroundHeight = VfxEffect.Safe(GroundHeight, 0, -10000, 10000); Bounciness = VfxEffect.Safe(Bounciness, .3f, 0, 1);
        Direction = VfxEffect.Finite(Direction, Vector3.UnitY); if (Direction.LengthSquared() < .0001f) Direction = Vector3.UnitY; Direction = Vector3.Normalize(Direction);
        Extents = Vector3.Clamp(Vector3.Abs(VfxEffect.Finite(Extents, Vector3.One)), Vector3.Zero, new(1000));
        Offset = VfxEffect.Finite(Offset, Vector3.Zero); Gravity = VfxEffect.Finite(Gravity, Vector3.Zero); BeamEnd = VfxEffect.Finite(BeamEnd, new(0,0,-5));
        StartColor = Color(StartColor); EndColor = Color(EndColor); Texture ??= AssetReference.Empty;
        FlipbookColumns = Math.Clamp(FlipbookColumns, 1, 8); FlipbookRows = Math.Clamp(FlipbookRows, 1, 8);
        SizeOverLife?.Validate(); OpacityOverLife?.Validate(1); SpeedOverLife?.Validate(); ColorBlendOverLife?.Validate(1);
        Wind=Vector3.Clamp(VfxEffect.Finite(Wind,Vector3.Zero),new(-1000),new(1000));
        Turbulence=VfxEffect.Safe(Turbulence,0,0,100); NoiseFrequency=VfxEffect.Safe(NoiseFrequency,1,.01f,100);
        AttractionPoint=VfxEffect.Finite(AttractionPoint,Vector3.Zero); Attraction=VfxEffect.Safe(Attraction,0,-100,100);
        TrailBreakDistance=VfxEffect.Safe(TrailBreakDistance,3,.01f,1000);
    }
    private static Vector4 Color(Vector4 c) => new(VfxEffect.Safe(c.X,1,0,1), VfxEffect.Safe(c.Y,1,0,1), VfxEffect.Safe(c.Z,1,0,1), VfxEffect.Safe(c.W,1,0,1));
}

public static class VfxEffectSerializer
{
    public const string FileExtension = ".bvfx";
    internal static readonly JsonSerializerOptions Options = new() { IncludeFields = true, WriteIndented = true, NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals, Converters = { new JsonStringEnumConverter() } };
    public static VfxEffect Load(string path)
    {
        if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("VFX asset exceeds 1 MB.");
        var effect = JsonSerializer.Deserialize<VfxEffect>(File.ReadAllText(path), Options) ?? throw new InvalidDataException("Empty VFX asset.");
        effect.Validate(); return effect;
    }
    public static void Save(string path, VfxEffect effect)
    {
        effect.Validate();
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(effect, Options));
        File.Move(temporary, path, true);
    }
    public static VfxEffect Clone(VfxEffect effect) => JsonSerializer.Deserialize<VfxEffect>(JsonSerializer.Serialize(effect, Options), Options)!;
}

public static class VfxPresets
{
    public static VfxEffect Create(VfxPreset preset)
    {
        var effect = new VfxEffect { Name = preset.ToString(), Loop = preset is VfxPreset.Fire or VfxPreset.Smoke or VfxPreset.Rain or VfxPreset.Snow or VfxPreset.Magic or VfxPreset.Trail or VfxPreset.Beam };
        var p = new VfxLayer { Name = preset.ToString() }; effect.Layers.Add(p);
        switch (preset)
        {
            case VfxPreset.Sparks: p.RenderMode=VfxRenderMode.Stretched; p.Sprite=VfxSprite.Spark; p.Rate=0; p.Spread=80; p.GroundBounce=true; break;
            case VfxPreset.Fire: p.Rate=55; p.Burst=0; p.Shape=VfxShape.Sphere; p.Extents=new(.2f); p.Gravity=new(0,2,0); p.Speed=.7f; p.StartSize=.4f; p.EndSize=.05f; p.Spread=15; p.Lifetime=.9f; break;
            case VfxPreset.Smoke: p.Additive=false; p.Burst=0; p.Speed=.4f; p.Gravity=new(.1f,.4f,0); p.Rate=18; p.Lifetime=3; p.StartSize=.25f; p.EndSize=1.2f; p.StartColor=new(.35f,.35f,.35f,.5f); p.EndColor=new(.15f,.15f,.15f,0); p.RotationSpeed=15; break;
            case VfxPreset.Explosion:
                p.Shape=VfxShape.Sphere; p.Spread=180; p.Burst=90; p.Rate=0; p.Speed=6; p.StartSize=.3f; p.EndSize=.7f; p.Drag=3; p.Gravity=new(0,.5f,0);
                var smoke=Create(VfxPreset.Smoke).Layers[0]; smoke.Name="After-smoke"; smoke.Delay=.1f; smoke.Burst=20; smoke.Rate=0; effect.Layers.Add(smoke); break;
            case VfxPreset.MuzzleFlash: p.Burst=8; p.Rate=0; p.Lifetime=.07f; p.Gravity=Vector3.Zero; p.Direction=-Vector3.UnitZ; p.Speed=2; p.Spread=18; p.StartSize=.2f; break;
            case VfxPreset.Rain: p.Shape=VfxShape.Box; p.Extents=new(8,.1f,8); p.Offset=new(0,7,0); p.Direction=-Vector3.UnitY; p.Spread=2; p.Rate=350; p.MaxParticles=1024; p.Burst=0; p.Speed=10; p.Lifetime=.8f; p.RenderMode=VfxRenderMode.Stretched; p.StartSize=.015f; p.EndSize=.015f; p.Additive=false; p.StartColor=new(.7f,.8f,1,.6f); p.EndColor=new(.7f,.8f,1,0); break;
            case VfxPreset.Snow: p.Shape=VfxShape.Box; p.Extents=new(7,.1f,7); p.Offset=new(0,5,0); p.Direction=-Vector3.UnitY; p.Spread=20; p.Rate=70; p.Burst=0; p.Speed=.7f; p.Gravity=new(.1f,-.1f,0); p.Lifetime=6; p.MaxParticles=600; p.StartSize=.05f; p.EndSize=.03f; p.StartColor=Vector4.One; p.EndColor=new(1,1,1,0); break;
            case VfxPreset.Magic: p.Shape=VfxShape.Ring; p.Extents=new(.6f); p.Gravity=new(0,.4f,0); p.Speed=.5f; p.StartColor=new(.2f,.5f,1,1); p.EndColor=new(.8f,.2f,1,0); p.Sprite=VfxSprite.Spark; break;
            case VfxPreset.Dust: p.Additive=false; p.Rate=0; p.Burst=25; p.Gravity=new(0,-.5f,0); p.Speed=1; p.StartSize=.15f; p.EndSize=.6f; p.StartColor=new(.5f,.4f,.3f,.4f); p.EndColor=new(.5f,.4f,.3f,0); break;
            case VfxPreset.Trail: p.RenderMode=VfxRenderMode.Trail; p.Rate=90; p.Burst=0; p.Gravity=Vector3.Zero; p.Speed=0; p.Lifetime=.5f; p.StartSize=.15f; p.EndSize=0; p.StartColor=new(.2f,.8f,1,1); break;
            case VfxPreset.Beam: p.RenderMode=VfxRenderMode.Beam; p.MaxParticles=1; p.Burst=1; p.Rate=0; p.LocalSpace=true; p.Speed=0; p.Gravity=Vector3.Zero; p.StartSize=.08f; p.EndSize=.08f; p.StartColor=new(.2f,.8f,1,1); p.EndColor=p.StartColor; break;
            case VfxPreset.Impact:
                p.RenderMode=VfxRenderMode.Stretched; p.Sprite=VfxSprite.Spark; p.Burst=24; p.Rate=0; p.Speed=4; p.Lifetime=.35f; p.Spread=65;
                p.StartSize=.05f; p.SpeedOverLife=VfxCurve.Linear(1,.2f);
                var dust=Create(VfxPreset.Dust).Layers[0]; dust.Name="Impact dust"; dust.Burst=8; dust.Lifetime=.6f; effect.Layers.Add(dust); break;
            case VfxPreset.Portal:
                effect.Loop=true; p.Shape=VfxShape.Ring; p.Extents=new(1); p.Burst=0; p.Rate=80; p.Speed=.4f; p.Lifetime=1.5f; p.Gravity=Vector3.Zero;
                p.StartColor=new(.5f,.1f,1,1); p.EndColor=new(.1f,.5f,1,0); p.Turbulence=.3f;
                p.OpacityOverLife=new() { Keys=new() { new(0,0),new(.2f,1),new(1,0) } }; break;
            case VfxPreset.Heal:
                p.Shape=VfxShape.Ring; p.Extents=new(.4f); p.Burst=45; p.Rate=0; p.Speed=1; p.Spread=10; p.Gravity=new(0,.3f,0); p.Lifetime=1.5f;
                p.StartColor=new(.2f,1,.3f,1); p.EndColor=new(.6f,1,.7f,0); p.SizeOverLife=VfxCurve.Linear(.3f,1); break;
            case VfxPreset.Footstep:
                p.Additive=false; p.Burst=8; p.Rate=0; p.Speed=.4f; p.Lifetime=.4f; p.StartSize=.07f; p.EndSize=.2f; p.Gravity=new(0,-.2f,0);
                p.StartColor=new(.5f,.4f,.3f,.3f); p.EndColor=new(.5f,.4f,.3f,0); break;
            case VfxPreset.EnergyShot:
                p.Burst=25; p.Rate=0; p.Direction=-Vector3.UnitZ; p.Spread=5; p.Speed=12; p.Gravity=Vector3.Zero; p.Lifetime=.3f;
                p.RenderMode=VfxRenderMode.Stretched; p.StartColor=new(.1f,.7f,1,1); p.EndColor=new(.3f,1,1,0); p.StartSize=.07f; p.Stretch=.04f; break;
        }
        effect.Validate(); return effect;
    }
}
