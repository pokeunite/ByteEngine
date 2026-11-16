using System.Numerics;

namespace ByteEngine.Core.Vfx;

/// <summary>Editable reference effects; projectile damage, speed, mana and timing remain authored gameplay.</summary>
internal static class VfxFireboltPresets
{
    private static VfxLayer Layer(string name, VfxSprite sprite, float lifetime, float size) => new()
    {
        Name = name, Sprite = sprite, Lifetime = lifetime, LifetimeVariation = 0,
        StartSize = size, EndSize = 0, SizeVariation = .15f, Gravity = Vector3.Zero,
        Speed = 0, Rate = 0, Burst = 1, MaxParticles = 16,
        StartColor = new(1, .42f, .04f, .8f), EndColor = new(1, .08f, .01f, 0)
    };

    internal static VfxEffect Create(VfxPreset preset)
    {
        var effect = new VfxEffect { Name = preset.ToString(), Duration = .12f, ParticleBudget = 96 };
        effect.Light = new() { Enabled = true, Intensity = 4, Range = 3, IntensityOverLife = VfxCurve.Linear(1, 0) };
        var core = Layer("Deforming 3D ember core", VfxSprite.Solid, .26f, .36f);
        core.RenderMode = VfxRenderMode.Mesh; core.LocalSpace = true; core.Additive = false;
        core.MeshScale = new(.85f, .85f, 1.3f); core.MeshDeformation = .18f; core.MeshEmission = .45f;
        core.MeshSurfaceFlow = .9f;
        core.StartColor = new(1, .28f, .025f, 1); core.EndColor = new(1, .1f, .01f, .65f);
        core.EndSize = .32f; core.MaxParticles = 2; core.RandomRotation = false;
        effect.Layers.Add(core);
        if (preset == VfxPreset.Firebolt)
        {
            effect.Loop = true; effect.Duration = .5f;
            effect.Light.Intensity = 3; effect.Light.Range = 3.5f;
            effect.Light.IntensityOverLife = new() { Keys = new() { new(0, .85f), new(.5f, 1), new(1, .85f) } };
            core.Rate = 4;
            var halo = Layer("Thin glowing envelope", VfxSprite.SoftDisc, .26f, .6f);
            halo.LocalSpace = true; halo.Rate = 4; halo.MaxParticles = 2; halo.EndSize = .55f;
            halo.StartColor = new(1, .32f, .025f, .18f); halo.EndColor = new(1, .15f, .01f, .08f);
            effect.Layers.Add(halo);
            var wisps = Layer("World-space flame wisps", VfxSprite.Flame, .24f, .15f);
            wisps.RenderMode = VfxRenderMode.Stretched; wisps.Rate = 45; wisps.Burst = 0; wisps.MaxParticles = 20;
            wisps.Direction = Vector3.UnitZ; wisps.Speed = .7f; wisps.Spread = 30; wisps.Gravity = new(0, .8f, 0); wisps.Stretch = .02f;
            effect.Layers.Add(wisps);
            var trail = Layer("Tapered travel ribbon", VfxSprite.SoftDisc, .16f, .16f);
            trail.RenderMode = VfxRenderMode.Trail; trail.Rate = 100; trail.Burst = 0; trail.MaxParticles = 24;
            trail.TrailBreakDistance = 2; trail.StartColor = new(1, .28f, .02f, .55f);
            effect.Layers.Add(trail);
            var sparks = Layer("Trailing embers", VfxSprite.Spark, .3f, .035f);
            sparks.RenderMode = VfxRenderMode.Stretched; sparks.Rate = 12; sparks.Burst = 0; sparks.MaxParticles = 10;
            sparks.Speed = 1.3f; sparks.Spread = 100; sparks.Gravity = new(0, -.6f, 0); sparks.Direction = Vector3.UnitZ;
            effect.Layers.Add(sparks);
            var smoke = Layer("Sparse warm smoke", VfxSprite.Smoke, .5f, .08f);
            smoke.Additive = false; smoke.Rate = 14; smoke.Burst = 0; smoke.MaxParticles = 12; smoke.EndSize = .3f;
            smoke.Speed = .1f; smoke.Gravity = new(0, .7f, 0);
            smoke.StartColor = new(.2f, .09f, .04f, .16f); smoke.EndColor = new(.13f, .1f, .08f, 0);
            effect.Layers.Add(smoke);
        }
        else
        {
            bool release = preset == VfxPreset.FireboltRelease;
            effect.Duration = release ? .08f : .16f;
            core.Lifetime = release ? .09f : .13f; core.StartSize = release ? .08f : .18f; core.EndSize = release ? .2f : .5f;
            core.LocalSpace = false;
            var flash = Layer("Release / contact flash", VfxSprite.Star, release ? .055f : .09f, release ? .32f : .7f);
            flash.StartColor = new(1, .76f, .25f, .8f);
            effect.Layers.Add(flash);
            var sparks = Layer("Outward sparks", VfxSprite.Spark, release ? .15f : .35f, .045f);
            sparks.RenderMode = VfxRenderMode.Stretched; sparks.Burst = release ? 6 : 18; sparks.MaxParticles = 20;
            sparks.Direction = Vector3.UnitY; sparks.Spread = 75; sparks.Speed = release ? 1.4f : 3;
            sparks.Gravity = new(0, -3, 0); sparks.Drag = 1;
            effect.Layers.Add(sparks);
            if (!release)
            {
                var ring = Layer("Surface-aligned expanding ring", VfxSprite.Ring, .2f, .1f);
                ring.RenderMode = VfxRenderMode.SurfacePlane; ring.LocalSpace = true; ring.EndSize = 1;
                effect.Layers.Add(ring);
                var smoke = Layer("Impact smoke", VfxSprite.Smoke, .6f, .2f);
                smoke.Additive = false; smoke.Burst = 7; smoke.MaxParticles = 10; smoke.EndSize = .7f;
                smoke.Speed = .5f; smoke.Spread = 70; smoke.Direction = Vector3.UnitY; smoke.Gravity = new(0, .5f, 0);
                smoke.StartColor = new(.22f, .12f, .07f, .35f); smoke.EndColor = new(.15f, .12f, .1f, 0);
                effect.Layers.Add(smoke);
            }
        }
        effect.Validate();
        return effect;
    }
}
