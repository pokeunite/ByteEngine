using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using ByteEngine.Core;
using ByteEngine.Core.AI;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Vfx;
using ByteEngine.Core.VisualLogic;
using KarlsonDemo.Plugin;
internal static class MiningEffectsCheck
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS MINING EFFECTS "+message);}
 public static void Run(Scene scene,MiningOperation3D op)
 {
  var hash=SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene"));var host=new SceneManager();var timer=System.Diagnostics.Stopwatch.StartNew();host.LoadScene(scene);
  var fx=op.GameObject.GetComponent<MiningEffects3D>()!;var audio=op.GameObject.GetComponent<MiningAudio3D>()!;var trade=op.GameObject.GetComponent<MiningTrade3D>()!;trade.Enabled=false;
  void Frame(int n=1){for(int i=0;i<n;i++){typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[.05d]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,null);}}
  Frame(3);Console.WriteLine("PREWARM milliseconds "+timer.ElapsedMilliseconds);Check(fx.PooledEffects>=20&&fx.PooledEffects<=fx.MaximumTransientEffects&&fx.LastError.Length==0,"bounded VFX library prewarms before combat");
  foreach(var asset in new[]{fx.MineExplosion,fx.MortarExplosion}){ByteEngine.Core.Animation.AnimationRuntimeAssets.TryGet(out var assets);var effect=assets!.LoadVfxEffect(asset);Check(effect.Layers.Any(l=>!l.Texture.IsEmpty&&l.FlipbookRows==5&&l.FlipbookColumns==5),"explosion uses a downloaded 25-frame Unity Labs flipbook");}
  var fire=fx.Emit(MiningEffectCue.MineExplosion,new(12,.3f,-29))!;var mortar=fx.Emit(MiningEffectCue.MortarExplosion,new(18,.3f,-29))!;Frame(3);Check(fire.ActiveParticles>0&&mortar.ActiveParticles>0,"both explosion assets emit native particles");
  var tracer=fx.Emit(MiningEffectCue.Tracer,new(10,1,-28),target:new(18,1,-30))!;Check(tracer.IsPlaying&&tracer.ParticleCapacity==1,"tracer uses a bounded beam layer");
  var machineEffects=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningMachineVfx3D>()).ToArray();Check(machineEffects.Length==3,"drill, refinery and press have editable machine effect components");
  var drill=machineEffects.Single(c=>c.Machine==MiningMachineKind.Drill);Check(!drill.OperatingPlayer!.IsEmitting,"drill dust is silent while idle");
  Check(op.StartOperation(),"actual drilling operation starts");Frame(10);Check(drill.Running&&drill.OperatingPlayer.IsEmitting&&drill.OperatingPlayer.ActiveParticles>0,"drilling emits dust and stone chips");
  var drillSound=scene.GameObjects.Single(o=>o.Name=="Mining loop - Drill").GetComponent<AudioSource3D>()!;Check(drillSound.IsPlaying&&audio.DrillLoop.ProjectPath.EndsWith("cnc-drill-loop.wav"),"construction drill recording plays during drilling");
  GameObject Spider(Vector3 position){var enemy=RuntimeSpawnService.SpawnBlueprint(scene,op.HunterBlueprint,position)!;op.TrackRoamingEnemy(enemy);return enemy;}
  var survivor=Spider(new(14,0,-31));Frame(3);var skin=survivor.GetComponent<MiningSpiderAnimation3D>()!;var initial=survivor.Transform.WorldPosition;
  Check(skin.ApplyExplosionImpact(Vector3.UnitX,6,3.5f),"living spider enters native skeletal knockdown");survivor.GetComponent<HealthComponent>()!.Damage(1);Frame(5);
  Console.WriteLine($"SURVIVOR movement {initial} -> {survivor.Transform.WorldPosition}");Check(survivor.Transform.WorldPosition.X>initial.X+.1f&&survivor.Transform.WorldPosition.Y>initial.Y+.03f,"blast impulse moves and lifts the living spider");Check(skin.Renderer!.LightRagdollActive&&!survivor.GetComponent<AIBehavior3D>()!.Enabled&&!survivor.GetComponent<NavigationAgent3D>()!.Enabled,"ragdoll suspends behaviour and navigation");
  Frame(45);Check(!skin.IsBlastKnockedDown&&!skin.Renderer.LightRagdollActive&&survivor.GetComponent<AIBehavior3D>()!.Enabled&&survivor.GetComponent<NavigationAgent3D>()!.Enabled,"surviving spider recovers and resumes its AI");
  var victim=Spider(new(18,0,-30));Frame(2);var mineObject=scene.CreateGameObject("Explosion verification mine");var mine=mineObject.AddComponent(new MiningDefense3D{Enabled=false,Kind=MiningDefenseKind.Mine,OperationObjectId=op.GameObject.Id,WorkshopObjectId=trade.GameObject.Id,BaseDamage=10000,BlastRadius=2});mine.DetonateAt(victim.Transform.WorldPosition);Frame();
  var corpse=scene.GameObjects.LastOrDefault(o=>o.Name=="Spider death remains"&&o.GetComponent<Rigidbody3D>()!=null);Check(victim.Scene==null&&corpse!=null,"lethal mine blast detaches a physics corpse before root destruction");var body=corpse!.GetComponent<Rigidbody3D>()!;Check(body.SimulateRotation&&body.Velocity.Length()>1,"explosion corpse has ballistic and angular motion");var deathSkin=scene.GameObjects.Single(o=>o.Parent==corpse).GetComponent<SkeletalMeshRenderer>()!;Check(deathSkin.LightRagdollActive,"fatal explosion uses skeletal ragdoll instead of death clip");
  var first=corpse.Transform.WorldPosition;Frame(5);Check(Vector3.Distance(first,corpse.Transform.WorldPosition)>.1f,"physics corpse actually moves after damage");Frame(90);Check(corpse.Scene==null,"physics corpse expires within its bounded lifetime");
  for(int i=0;i<70;i++)fx.Emit(MiningEffectCue.TurretMuzzle,new(12,1,-28));Check(fx.PooledEffects<=fx.MaximumTransientEffects&&fx.DroppedEffects>0,"effect storms drop excess work instead of growing objects without limit");
  var ui=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningBusinessUi3D>()).Single();typeof(MiningOperation3D).GetProperty("Phase")!.SetValue(op,MiningPhase.Preparation);var tent=scene.FindGameObject("Company tent")!;scene.FindGameObject(op.PlayerObjectId)!.Transform.WorldPosition=tent.Transform.WorldPosition;ui.Open(MiningScreen.Research);Check(audio.LastCue==nameof(MiningSoundCue.TerminalOpen),"company terminal has its own non-chime opening cue");ui.Close();
  Check(audio.DefenceVolume>=.9f&&audio.ExplosionVolume>=.95f&&audio.DefenceHearingDistance>=10,"defence sounds use stronger independent gain and distance settings");
  typeof(MiningOperation3D).GetProperty("Phase")!.SetValue(op,MiningPhase.Failed);Frame(90);Check(!drill.OperatingPlayer.IsEmitting,"operation failure stops drilling effects");Check(fx.PooledEffects<=fx.MaximumTransientEffects,"sustained work retains bounded pool size");host.UnloadScene();Check(SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene")).SequenceEqual(hash),"test preserves scene and company progression");Console.WriteLine("MINING EFFECTS NATIVE VERIFICATION COMPLETE");
 }
 public static void Preview(Scene scene,MiningOperation3D op)
 {
  MiningDefenceVisualCheck.Preview(scene,op);var camera=scene.ActiveCamera!;var eye=new Vector3(27,12,-12);camera.Transform.WorldPosition=eye;Matrix4x4.Invert(Matrix4x4.CreateLookAt(eye,new Vector3(16,.8f,-27),Vector3.UnitY),out var pose);camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(pose);camera.OrthographicWidth=20;
 }
 public static void PreviewFrame(Scene scene,MiningOperation3D op,int frame)
 {
  var fx=op.GameObject.GetComponent<MiningEffects3D>()!;
  if(frame==33){fx.Emit(MiningEffectCue.MineExplosion,new(13,.2f,-30));fx.Emit(MiningEffectCue.MortarExplosion,new(20,.2f,-29),size:1.2f);}
  if(frame is 25 or 30 or 35){var sentry=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningDefense3D>()).First(d=>d.Kind==MiningDefenseKind.Sentry);var origin=(Vector3)typeof(MiningDefense3D).GetMethod("ShotOrigin",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(sentry,null)!;fx.Emit(MiningEffectCue.TurretMuzzle,origin,new(0,0,-1));fx.Emit(MiningEffectCue.Tracer,origin,target:origin-new Vector3(0,0,7));}
 }
}
