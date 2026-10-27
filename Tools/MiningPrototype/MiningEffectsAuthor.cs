using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Vfx;
using KarlsonDemo.Plugin;
internal static class MiningEffectsAuthor
{
 static AssetReference Ref(string path,string type){string full=Program.Root+"/"+path;Directory.CreateDirectory(Path.GetDirectoryName(full)!);Guid id=File.Exists(full+".meta")?JsonDocument.Parse(File.ReadAllText(full+".meta")).RootElement.GetProperty("guid").GetGuid():Guid.NewGuid();File.WriteAllText(full+".meta",JsonSerializer.Serialize(new{guid=id,type}));return new(id,path);}
 static AssetReference Save(string name,VfxEffect effect){string path="Assets/VFX/Mining/"+name+".bvfx";Directory.CreateDirectory(Path.GetDirectoryName(Program.Root+"/"+path)!);VfxEffectSerializer.Save(Program.Root+"/"+path,effect);return Ref(path,"VfxEffect");}
 static VfxLayer Smoke(string name)=>new(){Name=name,Sprite=VfxSprite.Smoke,Additive=false,Burst=0,Rate=7,MaxParticles=18,Lifetime=1.4f,LifetimeVariation=.18f,Speed=.5f,SpeedVariation=.25f,Spread=20,Gravity=new(0,.25f,0),Wind=new(.08f,0,.03f),StartSize=.12f,EndSize=.7f,StartColor=new(.7f,.72f,.7f,.2f),EndColor=new(.7f,.72f,.7f,0),RotationSpeed=12,SpriteResolution=64};
 static VfxEffect Explosion(string name,AssetReference atlas)
 {
  var smoke=Smoke("Trailing soot");smoke.Burst=12;smoke.Rate=0;smoke.Delay=.18f;smoke.Shape=VfxShape.Sphere;smoke.Extents=new(.3f);smoke.Lifetime=1.6f;smoke.Speed=1.5f;smoke.Spread=180;smoke.Drag=2;smoke.StartSize=.45f;smoke.EndSize=1.5f;smoke.StartColor=new(.2f,.19f,.22f,.55f);smoke.EndColor=new(.2f,.19f,.22f,0);
  return new(){Name=name,Duration=2.1f,ParticleBudget=96,Layers=[
   new(){Name="Animated explosion - CC0 Sinestesia",Offset=new(0,.85f,0),Texture=atlas,FlipbookColumns=8,FlipbookRows=8,SpriteResolution=128,MaxParticles=1,Burst=1,Rate=0,Speed=0,Gravity=Vector3.Zero,Lifetime=.95f,LifetimeVariation=0,StartSize=3.8f,EndSize=4.4f,SizeVariation=0,StartColor=Vector4.One,EndColor=Vector4.One,Additive=false,Brightness=1.2f,RandomRotation=false},smoke,
   new(){Name="Ground pressure ring",Sprite=VfxSprite.Ring,RenderMode=VfxRenderMode.Horizontal,MaxParticles=1,Burst=1,Rate=0,Speed=0,Gravity=Vector3.Zero,Lifetime=.4f,LifetimeVariation=0,StartSize=.4f,EndSize=7,Offset=new(0,-.18f,0),StartColor=new(1,.65f,.2f,.6f),EndColor=new(.8f,.4f,.1f,0),Additive=true,SpriteResolution=64},
   new(){Name="Hot chips",Sprite=VfxSprite.Spark,RenderMode=VfxRenderMode.Stretched,MaxParticles=24,Burst=20,Rate=0,Speed=5,Spread=80,Lifetime=.7f,Gravity=new(0,-7,0),StartSize=.045f,EndSize=.015f,GroundBounce=true,Bounciness=.2f,StartColor=new(1,.65f,.16f,1),EndColor=new(.45f,.2f,.06f,0),SpriteResolution=32}
  ]};
 }
 public static void Build(Scene scene,GameProjectRuntime runtime)
 {
  string backup=Program.Root+"/.byteengine/Backups/effects-pass-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  string textureFolder="Assets/VFX/Mining/Textures";Directory.CreateDirectory(Program.Root+"/"+textureFolder);
  var atlases=new List<AssetReference>();foreach(var (source,name) in new[]{("4.png","Mine-Explosion-Atlas.png"),("3.png","Mortar-Explosion-Atlas.png")}){string path=textureFolder+"/"+name;File.Copy("artifacts/mining-effects-pass/explosion-originals/Free Explosion Animations 2/Half Sized/"+source,Program.Root+"/"+path,true);atlases.Add(Ref(path,"Texture2D"));}
  var mine=Save("Mine blast",Explosion("Mine blast",atlases[0]));var mortar=Save("Mortar impact",Explosion("Mortar impact",atlases[1]));
  var smoke=Smoke("Muzzle smoke");smoke.Burst=3;smoke.Rate=0;smoke.MaxParticles=6;smoke.Delay=.04f;smoke.Lifetime=.6f;smoke.StartSize=.055f;smoke.EndSize=.4f;smoke.StartColor=new(.45f,.44f,.42f,.3f);smoke.EndColor=new(.4f,.4f,.4f,0);
  var muzzle=Save("Turret muzzle flash",new(){Name="Turret muzzle flash and smoke",Duration=.8f,ParticleBudget=20,Layers=[new(){Name="Bright flash",Sprite=VfxSprite.Star,MaxParticles=1,Burst=1,Rate=0,Speed=0,Gravity=Vector3.Zero,Lifetime=.065f,LifetimeVariation=0,StartSize=.6f,EndSize=.12f,Brightness=2.5f,StartColor=new(1,.85f,.5f,1),EndColor=new(1,.4f,.1f,0)},new(){Name="Forward sparks",Sprite=VfxSprite.Spark,RenderMode=VfxRenderMode.Stretched,MaxParticles=6,Burst=5,Rate=0,Speed=7,Spread=15,Gravity=Vector3.Zero,Lifetime=.09f,StartSize=.04f,EndSize=0,StartColor=new(1,.8f,.4f,1),EndColor=new(1,.3f,.1f,0)},smoke]});
  var tracer=Save("Defence tracer",new(){Name="Defence tracer",Duration=.085f,ParticleBudget=1,Layers=[new(){Name="Warm streak",RenderMode=VfxRenderMode.Beam,Sprite=VfxSprite.Spark,MaxParticles=1,Burst=1,Rate=0,Speed=0,Gravity=Vector3.Zero,LocalSpace=true,Lifetime=.075f,LifetimeVariation=0,StartSize=.035f,EndSize=.015f,SizeVariation=0,Brightness=2,StartColor=new(1,.9f,.55f,1),EndColor=new(1,.5f,.15f,0)}]});
  var stamp=Save("Press stamp flecks",new(){Name="Press stamp flecks",Duration=.25f,ParticleBudget=8,Layers=[new(){Name="Metal flecks",Sprite=VfxSprite.Spark,RenderMode=VfxRenderMode.Stretched,MaxParticles=6,Burst=4,Rate=0,Speed=1.8f,Spread=80,Gravity=new(0,-3,0),Lifetime=.18f,StartSize=.025f,EndSize=0,StartColor=new(1,.75f,.3f,.9f),EndColor=new(.8f,.3f,.1f,0)}]});
  var landing=Save("Material landing puff",new(){Name="Material landing puff",Duration=.65f,ParticleBudget=8,Layers=[new(){Name="Fine dust",Sprite=VfxSprite.Smoke,Additive=false,MaxParticles=8,Burst=5,Rate=0,Speed=.45f,Spread=80,Gravity=new(0,.15f,0),Lifetime=.5f,StartSize=.1f,EndSize=.5f,StartColor=new(.55f,.48f,.37f,.24f),EndColor=new(.55f,.48f,.37f,0),SpriteResolution=64}]});
  var trail=Smoke("Mortar smoke trail");trail.Rate=18;trail.MaxParticles=24;trail.Lifetime=.5f;trail.StartSize=.08f;trail.EndSize=.4f;trail.Speed=.1f;trail.StartColor=new(.45f,.44f,.42f,.4f);trail.EndColor=new(.4f,.4f,.4f,0);var trailRef=Save("Mortar shell trail",new(){Name="Mortar shell smoke trail",Loop=true,Duration=1,ParticleBudget=28,Layers=[trail]});
  var dust=Smoke("Rock drilling dust");dust.Rate=24;dust.MaxParticles=42;dust.Shape=VfxShape.Ring;dust.Extents=new(.75f);dust.StartSize=.24f;dust.EndSize=1.25f;dust.Spread=75;dust.Speed=.8f;dust.Lifetime=1.25f;dust.StartColor=new(.65f,.35f,.17f,.3f);dust.EndColor=new(.65f,.35f,.17f,0);
  var drill=Save("Drill rock dust and chips",new(){Name="Drill rock dust and chips",Loop=true,Duration=2,ParticleBudget=64,Layers=[dust,new(){Name="Stone chips",Sprite=VfxSprite.Solid,Additive=false,MaxParticles=16,Burst=0,Rate=8,Shape=VfxShape.Ring,Extents=new(.65f),Spread=70,Speed=2,Gravity=new(0,-6,0),Lifetime=.85f,StartSize=.07f,EndSize=.025f,StartColor=new(.35f,.21f,.14f,1),EndColor=new(.35f,.21f,.14f,0),GroundBounce=true,Bounciness=.2f,SpriteResolution=32}]});
  var steam=Save("Refinery exhaust",new(){Name="Refinery exhaust",Loop=true,Duration=2,ParticleBudget=20,Layers=[Smoke("Warm process vapour")]});
  var pressAir=Smoke("Compressed air seep");pressAir.Rate=3;pressAir.MaxParticles=5;pressAir.Lifetime=.4f;pressAir.StartSize=.04f;pressAir.EndSize=.2f;pressAir.StartColor=new(.7f,.7f,.7f,.12f);pressAir.EndColor=new(.7f,.7f,.7f,0);var pressAirRef=Save("Press compressed air",new(){Name="Press compressed air",Loop=true,Duration=1,ParticleBudget=8,Layers=[pressAir]});
  var op=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningOperation3D>()).Single();var audio=op.GameObject.GetComponent<MiningAudio3D>()!;
  var effects=op.GameObject.GetComponent<MiningEffects3D>()??op.GameObject.AddComponent(new MiningEffects3D());effects.TurretMuzzle=muzzle;effects.Tracer=tracer;effects.MineExplosion=mine;effects.MortarExplosion=mortar;effects.MachineStamp=stamp;effects.MaterialLanding=landing;effects.MortarTrail=trailRef;
  void Machine(GameObject owner,MiningMachineKind kind,AssetReference effect,string bone,Vector3 offset){var fx=owner.GetComponent<MiningMachineVfx3D>()??owner.AddComponent(new MiningMachineVfx3D());fx.OperationObjectId=op.GameObject.Id;fx.WorkshopObjectId=audio.WorkshopObjectId;fx.Machine=kind;fx.OperatingEffect=effect;fx.AnchorBone=bone;fx.AnchorOffset=offset;}
  Machine(scene.FindGameObject("Animated drill model")!,MiningMachineKind.Drill,drill,"",new(0,.16f,0));Machine(scene.FindGameObject(audio.RefineryObjectId)!,MiningMachineKind.Refinery,steam,"Root",new(.44f,3.25f,-.49f));Machine(scene.FindGameObject(audio.PressObjectId)!,MiningMachineKind.AmmoPress,pressAirRef,"Ram",new(0,-.38f,0));
  string audioFolder="Assets/Audio/Mining/Effects Pass";Directory.CreateDirectory(Program.Root+"/"+audioFolder);
  foreach(string file in Directory.GetFiles("artifacts/mining-effects-pass/audio","*.wav")){File.Copy(file,Program.Root+"/"+audioFolder+"/"+Path.GetFileName(file),true);Ref(audioFolder+"/"+Path.GetFileName(file),"AudioClip");}
  AssetReference Sound(string name)=>Ref(audioFolder+"/"+name+".wav","AudioClip");audio.DrillLoop=Sound("rock-drill-loop");audio.TurretShot=Sound("sentry-shot");audio.RepeaterShot=Sound("repeater-shot");audio.Explosion=Sound("defence-explosion");audio.Cannon=Sound("mortar-launch");audio.TerminalOpen=Sound("terminal-open");audio.TerminalSelect=Sound("terminal-select");audio.TerminalPurchase=Sound("terminal-purchase");audio.MaximumOneShotVoices=24;
  runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");string sourceFolder=Program.Root+"/SourceArt/Mining Effects Pass";Directory.CreateDirectory(sourceFolder);File.Copy("artifacts/mining-effects-pass/SOURCES.json",sourceFolder+"/SOURCES.json",true);
  Console.WriteLine("MINING SOUND AND EFFECTS PASS INSTALLED. Backup: "+backup);
 }
}
