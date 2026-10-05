using System.Numerics;
using System.Runtime.CompilerServices;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Vfx;

/// <summary>Scene-owned, bounded reuse of transient effect objects, meshes and simulation buffers.</summary>
internal sealed class VfxSpawnPool
{
    private static readonly ConditionalWeakTable<ByteEngine.Core.Scene.Scene,VfxSpawnPool> Pools=new();
    private sealed record Slot(VfxPlayer Player,AssetReference Effect,int Revision);
    private readonly List<Slot> _slots=new();
    internal static VfxSpawnPool For(ByteEngine.Core.Scene.Scene scene) => Pools.GetValue(scene,_=>new());
    internal VfxPlayer Spawn(ByteEngine.Core.Scene.Scene scene,AssetReference effect,int revision,VfxEffect definition,Vector3 position,Quaternion rotation,float size)
    {
        for(int n=_slots.Count-1;n>=0;n--) if(_slots[n].Player.AttachedScene()!=scene) _slots.RemoveAt(n);
        int active=0,capacity=0;
        foreach(var obj in scene.GameObjects)
            if(obj.GetComponent<VfxPlayer>() is { DestroyWhenFinished:true } p && obj.Active)
            { active++; capacity+=p.ParticleCapacity; }
        int requested=Math.Min(definition.ParticleBudget,definition.Layers.Sum(l=>l.MaxParticles));
        if(active>=64 || capacity+requested>65536) throw new InvalidOperationException("Transient VFX budget reached (64 effects / 65,536 reserved particles). Reuse a VFX Player for sustained effects.");
        VfxPlayer? player=null;
        foreach(var slot in _slots)
            if(!slot.Player.GameObject.Active && slot.Effect==effect && slot.Revision==revision) { player=slot.Player; break; }
        if(player==null)
        {
            var oneShot=VfxEffectSerializer.Clone(definition); oneShot.Loop=false;
            var obj=scene.CreateGameObject("VFX: "+definition.Name);
            player=obj.AddComponent(new VfxPlayer { PlayOnStart=false,DestroyWhenFinished=true });
            try { player.SetDefinition(oneShot); }
            catch { scene.DestroyGameObject(obj); throw; }
            player.ReturnToPool=Return;
            _slots.Add(new(player,effect,revision));
        }
        else if(player.ParticleCapacity==0)
        {
            var oneShot=VfxEffectSerializer.Clone(definition); oneShot.Loop=false; player.SetDefinition(oneShot);
        }
        player.GameObject.Active=true; player.GameObject.SetParent(null,false);
        player.Transform.WorldScale=Vector3.One; player.Transform.WorldPosition=VfxEffect.Finite(position,Vector3.Zero);
        player.Transform.WorldRotation=rotation; player.Size=size; player.Intensity=1; player.PlaybackSpeed=1;
        player.ClearBeamTarget(); player.Play();
        return player;
    }
    private void Return(VfxPlayer player)
    {
        var scene=player.GameObject.Scene;
        if(scene==null) return;
        if(player.GameObject.IsAttached) SkeletalAttachmentService.Detach(player.GameObject,true);
        player.GameObject.SetParent(null,true); player.GameObject.Active=false; player.Stop(true);
        int idle=0,capacity=0;
        foreach(var slot in _slots)
            if(slot.Player.AttachedScene()==scene && !slot.Player.GameObject.Active) { idle++; capacity+=slot.Player.ParticleCapacity; }
        // Retain only a small warm cache, evicting the oldest idle objects first.
        for(int n=0;n<_slots.Count && (idle>16 || capacity>16384);)
        {
            var slot=_slots[n];
            if(slot.Player.AttachedScene()!=scene || slot.Player.GameObject.Active) { n++; continue; }
            idle--; capacity-=slot.Player.ParticleCapacity; _slots.RemoveAt(n); scene.DestroyGameObject(slot.Player.GameObject);
        }
    }
    internal static Quaternion AlignUp(Vector3 normal)
    {
        normal=VfxEffect.Finite(normal,Vector3.UnitY);
        if(normal.LengthSquared()<.0001f) return Quaternion.Identity;
        normal=Vector3.Normalize(normal); float dot=Vector3.Dot(Vector3.UnitY,normal);
        if(dot<-.9999f) return Quaternion.CreateFromAxisAngle(Vector3.UnitX,MathF.PI);
        return Quaternion.Normalize(new Quaternion(Vector3.Cross(Vector3.UnitY,normal),1+dot));
    }
}
