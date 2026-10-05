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
        Action("vfx.beamTarget","Set VFX Beam Target",(i,c,p)=>p.SetBeamTarget(EventValueResolver.GetVector3(i,"position",c,p.Transform.WorldPosition)),
            new VisualArgumentDefinition("position","World Endpoint",VariableValue.FromVector3(System.Numerics.Vector3.Zero)));
        Action("vfx.resetBeamTarget","Use Authored VFX Beam Endpoint",(i,c,p)=>p.ClearBeamTarget());
        Action("vfx.speed","Set VFX Playback Speed",(i,c,p)=>p.PlaybackSpeed=(float)EventValueResolver.GetNumber(i,"value",c,1),
            new VisualArgumentDefinition("value","Playback Speed (0–4)",VariableValue.FromNumber(1)));
        registry.RegisterCondition(new VisualConditionDefinition { Id="vfx.isPlaying",Category="VFX",DisplayName="VFX Is Playing",TargetComponent=nameof(VfxPlayer),
            Evaluate=(i,c)=>Player(i,c,false)?.IsPlaying==true });
        registry.RegisterCondition(new VisualConditionDefinition { Id="vfx.finished",Category="VFX",DisplayName="VFX Has Finished",TargetComponent=nameof(VfxPlayer),
            Evaluate=(i,c)=>Player(i,c,false) is {} p && p.HasPlayed && !p.IsPlaying });
        void Spawn(VisualInstruction i,EventExecutionContext c,bool onObject,bool impact,bool rayHit=false)
            {
                string token=EventValueResolver.GetString(i,"effect",c);
                if(!ByteEngine.Core.Animation.AnimationRuntimeAssets.TryGet(out var assets) || assets==null) return;
                var reference=System.Guid.TryParse(token,out var guid) ? new ByteEngine.Core.Assets.AssetReference(guid) :
                    string.IsNullOrWhiteSpace(token) ? ByteEngine.Core.Assets.AssetReference.Empty : new ByteEngine.Core.Assets.AssetReference(token);
                VfxPlayer? player=null;
                try
                {
                    var target=onObject ? ResolveObjectArgument(i,"object",c) : null;
                    if(onObject && target==null) return;
                    if(rayHit && c.LastRaycastHit==null) return;
                    var position=rayHit ? c.LastRaycastHit!.Value.Point : target?.Transform.WorldPosition??EventValueResolver.GetVector3(i,"position",c,c.Self.Transform.WorldPosition);
                    var rotation=target?.Transform.WorldRotation??System.Numerics.Quaternion.Identity;
                    string socket=onObject ? EventValueResolver.GetString(i,"socket",c) : "";
                    if(target!=null && !string.IsNullOrWhiteSpace(socket))
                    {
                        if(!ByteEngine.Core.Animation.SkeletalSocketResolver.TryGetSocketWorldTransform(target,socket,out var pose))
                        { c.WarningSink?.Invoke("VFX socket not found: "+socket); return; }
                        position=pose.Position; rotation=pose.Rotation;
                    }
                    if(impact) rotation=VfxSpawnPool.AlignUp(rayHit ? c.LastRaycastHit!.Value.Normal : EventValueResolver.GetVector3(i,"normal",c,System.Numerics.Vector3.UnitY));
                    player=VfxSpawnPool.For(c.Scene).Spawn(c.Scene,reference,assets.VfxRevision,assets.LoadVfxEffect(reference),position,rotation,
                        (float)EventValueResolver.GetNumber(i,"size",c,1));
                    if(target!=null && EventValueResolver.GetBoolean(i,"follow",c,false))
                    {
                        if(string.IsNullOrWhiteSpace(socket)) player.GameObject.SetParent(target,true);
                        else if(!ByteEngine.Core.Scene.SkeletalAttachmentService.AttachToSocket(player.GameObject,target,socket))
                            throw new System.InvalidOperationException("Could not attach VFX to socket.");
                    }
                }
                catch(System.Exception e) { if(player!=null)c.Scene.DestroyGameObject(player.GameObject); c.WarningSink?.Invoke("Could not spawn VFX: "+e.Message); }
            }
        var effectArg=new VisualArgumentDefinition("effect","VFX Effect",VariableValue.FromString(""));
        var sizeArg=new VisualArgumentDefinition("size","Size Multiplier",VariableValue.FromNumber(1));
        var positionArg=new VisualArgumentDefinition("position","World Position",VariableValue.FromVector3(System.Numerics.Vector3.Zero));
        registry.RegisterAction(new VisualActionDefinition { Id="vfx.spawn",Category="VFX",DisplayName="Spawn VFX at Position",Arguments=new[] {effectArg,positionArg,sizeArg},Execute=(i,c)=>Spawn(i,c,false,false) });
        registry.RegisterAction(new VisualActionDefinition { Id="vfx.impact",Category="VFX",DisplayName="Spawn Impact VFX",Arguments=new[] {effectArg,positionArg,new VisualArgumentDefinition("normal","Hit Normal (world)",VariableValue.FromVector3(System.Numerics.Vector3.UnitY)),sizeArg},Execute=(i,c)=>Spawn(i,c,false,true) });
        registry.RegisterAction(new VisualActionDefinition { Id="vfx.rayImpact",Category="VFX",DisplayName="Spawn VFX at Last Raycast Hit",Arguments=new[] {effectArg,sizeArg},Execute=(i,c)=>Spawn(i,c,false,true,true) });
        registry.RegisterAction(new VisualActionDefinition { Id="vfx.spawnOnObject",Category="VFX",DisplayName="Spawn VFX on Object / Socket",Arguments=new[] {effectArg,new VisualArgumentDefinition("object","Source Object",VariableValue.FromString("Self")),new VisualArgumentDefinition("socket","Socket Name (optional)",VariableValue.FromString("")),new VisualArgumentDefinition("follow","Follow Object / Socket",VariableValue.FromBoolean(false)),sizeArg},Execute=(i,c)=>Spawn(i,c,true,false) });
    }
}
