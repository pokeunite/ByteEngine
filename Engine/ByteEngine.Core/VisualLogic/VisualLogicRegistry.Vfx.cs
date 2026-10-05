using ByteEngine.Core.Vfx;
using ByteEngine.Core.Variables;

namespace ByteEngine.Core.VisualLogic;

public sealed partial class VisualLogicRegistry
{
    private static void RegisterVfx(VisualLogicRegistry registry)
    {
        VfxPlayer? Player(VisualInstruction i,EventExecutionContext c,bool warn=true)
        {
            var target=ResolveObjectTarget(i,c,warn);
            var player=target?.GetComponent<VfxPlayer>();
            if(player==null && target!=null && warn) c.WarningSink?.Invoke($"'{target.Name}' needs a VFX Player component.");
            return player;
        }
        void Action(string id,string name,System.Action<VisualInstruction,EventExecutionContext,VfxPlayer> execute,params VisualArgumentDefinition[] args)
            => registry.RegisterAction(new VisualActionDefinition { Id=id, Category="VFX", DisplayName=name, TargetComponent=nameof(VfxPlayer), Arguments=args,
                Execute=(i,c)=> { if(Player(i,c) is {} p) execute(i,c,p); } });
        Action("vfx.play","Play VFX",(i,c,p)=>p.Play());
        Action("vfx.stop","Stop VFX (Let Particles Finish)",(i,c,p)=>p.Stop());
        Action("vfx.clear","Stop and Clear VFX",(i,c,p)=>p.Stop(true));
        Action("vfx.pause","Pause VFX",(i,c,p)=>p.Paused=true);
        Action("vfx.resume","Resume VFX",(i,c,p)=>p.Paused=false);
        Action("vfx.burst","Emit VFX Burst",(i,c,p)=>p.EmitBurst((int)EventValueResolver.GetNumber(i,"count",c,30)),
            new VisualArgumentDefinition("count","Particle Count (per layer)",VariableValue.FromNumber(30)));
        Action("vfx.intensity","Set VFX Intensity",(i,c,p)=>p.Intensity=(float)EventValueResolver.GetNumber(i,"value",c,1),
            new VisualArgumentDefinition("value","Intensity (0–8)",VariableValue.FromNumber(1)));
        Action("vfx.size","Set VFX Size",(i,c,p)=>p.Size=(float)EventValueResolver.GetNumber(i,"value",c,1),
            new VisualArgumentDefinition("value","Size Multiplier",VariableValue.FromNumber(1)));
        registry.RegisterCondition(new VisualConditionDefinition { Id="vfx.isPlaying",Category="VFX",DisplayName="VFX Is Playing",TargetComponent=nameof(VfxPlayer),
            Evaluate=(i,c)=>Player(i,c,false)?.IsPlaying==true });
        registry.RegisterCondition(new VisualConditionDefinition { Id="vfx.finished",Category="VFX",DisplayName="VFX Has Finished",TargetComponent=nameof(VfxPlayer),
            Evaluate=(i,c)=>Player(i,c,false) is {} p && p.HasPlayed && !p.IsPlaying });
        registry.RegisterAction(new VisualActionDefinition { Id="vfx.spawn",Category="VFX",DisplayName="Spawn VFX at Position",
            Execute=(i,c)=>
            {
                // Bound disposable impact effects; existing reusable VFX players are not counted.
                int active=0;
                foreach(var obj in c.Scene.GameObjects) if(obj.GetComponent<VfxPlayer>()?.DestroyWhenFinished==true) active++;
                if(active>=64) { c.WarningSink?.Invoke("VFX transient-effect limit reached (64). Reuse a VFX Player for sustained effects."); return; }
                string token=EventValueResolver.GetString(i,"effect",c);
                if(!ByteEngine.Core.Animation.AnimationRuntimeAssets.TryGet(out var assets) || assets==null) return;
                var reference=System.Guid.TryParse(token,out var guid) ? new ByteEngine.Core.Assets.AssetReference(guid) :
                    string.IsNullOrWhiteSpace(token) ? ByteEngine.Core.Assets.AssetReference.Empty : new ByteEngine.Core.Assets.AssetReference(token);
                ByteEngine.Core.Scene.GameObject? spawned=null;
                try
                {
                    var definition=VfxEffectSerializer.Clone(assets.LoadVfxEffect(reference));
                    definition.Loop=false;
                    spawned=c.Scene.CreateGameObject("VFX: "+definition.Name);
                    spawned.Transform.WorldPosition=EventValueResolver.GetVector3(i,"position",c,c.Self.Transform.WorldPosition);
                    var player=spawned.AddComponent(new VfxPlayer { PlayOnStart=false, DestroyWhenFinished=true,
                        Size=(float)EventValueResolver.GetNumber(i,"size",c,1) });
                    player.SetDefinition(definition); player.Play();
                }
                catch(System.Exception e) { if(spawned!=null)c.Scene.DestroyGameObject(spawned); c.WarningSink?.Invoke("Could not spawn VFX: "+e.Message); }
            }
        });
    }
}
