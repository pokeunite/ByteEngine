using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using ByteEngine.Core;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningProjectileMusicCheck
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS PROJECTILE/MUSIC "+message);}
 public static void Run(Scene scene,MiningOperation3D op)
 {
  var hash=SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene"));
  var host=new SceneManager();host.LoadScene(scene);
  void Frame(int count=1){for(int i=0;i<count;i++){typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[.02d]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,null);}}
  Frame(3);var audio=op.GameObject.GetComponent<MiningAudio3D>()!;var fx=op.GameObject.GetComponent<MiningEffects3D>()!;
  var music=scene.GameObjects.Single(o=>o.Name=="Mining camp music").GetComponent<AudioSource3D>()!;
  Frame(100);Check(music.IsPlaying&&music.ClipLoaded&&!music.Spatial&&music.Bus==AudioBus.Music,"PYNCHON loads and loops through the dedicated music bus");
  Check(Math.Abs(music.Volume-.25f)<.01f,"camp music fades up to moderate adjustable volume");
  Check(fx.MineExplosionScale==1.8f&&fx.MortarExplosionScale==2.3f,"larger mine and mortar visuals are saved in the scene");
  var target=scene.CreateGameObject("Projectile verification target");target.Transform.WorldPosition=new(60,0,-30);target.AddComponent(new CapsuleCollider3D{Radius=.6f,Height=2,Center=new(0,1,0)});var hp=target.AddComponent(new HealthComponent{MaxHealth=1000,CurrentHealth=1000,DestroyOnDeath=false});op.TrackRoamingEnemy(target);
  Frame(100);Check(!music.IsPlaying&&music.Volume<.002f,"roaming enemies fade camp music out even outside drilling");
  var root=scene.CreateGameObject("Projectile verification turret");root.Transform.WorldPosition=new(60,0,-20);
  var turret=root.AddComponent(new MiningDefense3D{Enabled=false,OperationObjectId=op.GameObject.Id,WorkshopObjectId=op.GameObject.Id,ProjectileSpeed=25,BaseInterval=100,BaseDamage=10});
  turret.Tick(.02f);Check(turret.ShotsFired==1&&hp.CurrentHealth==1000,"turret launch creates a bullet without instant damage");
  var bullet=scene.GameObjects.Last(o=>o.Name=="Company defence projectile");Check(bullet.GetComponent<BulletTrail3D>()!=null,"turret bullet uses the pistol tracer renderer");
  Frame(5);Check(hp.CurrentHealth==1000&&bullet.Transform.WorldPosition.Z< -20,"bullet travels visibly before impact");Frame(25);Check(hp.CurrentHealth==990,"swept projectile deals damage on arrival");Frame(20);Check(hp.CurrentHealth==990,"impact damages the enemy only once");
  var wall=scene.CreateGameObject("Thin projectile cover");wall.Transform.WorldPosition=new(60,1,-24);wall.AddComponent(new BoxCollider3D{Size=new(3,3,.04f)});
  typeof(MiningDefense3D).GetMethod("LaunchDefenceProjectile",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(turret,[new Vector3(60,1,-20),-Vector3.UnitZ,50f,0,18f,root.Id]);Frame(40);Check(hp.CurrentHealth==990,"fast swept bullet is blocked by thin cover");
  wall.Active=false;
  var second=scene.CreateGameObject("Piercing verification target");second.Transform.WorldPosition=new(60,0,-33);second.AddComponent(new CapsuleCollider3D{Radius=.6f,Height=2,Center=new(0,1,0)});var hp2=second.AddComponent(new HealthComponent{MaxHealth=1000,CurrentHealth=1000});op.TrackRoamingEnemy(second);
  typeof(MiningDefense3D).GetMethod("LaunchDefenceProjectile",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(turret,[new Vector3(60,1,-20),-Vector3.UnitZ,10f,1,18f,root.Id]);Frame(50);Check(hp.CurrentHealth==980&&hp2.CurrentHealth==990,"piercing research carries through to the next target");
  for(int i=0;i<50;i++)typeof(MiningDefense3D).GetMethod("LaunchDefenceProjectile",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(turret,[new Vector3(60,4,-20),Vector3.UnitX,1f,0,18f,root.Id]);Check(turret.ActiveProjectiles<=turret.MaximumProjectiles,"projectile creation stays bounded per turret");Frame(100);Check(turret.ActiveProjectiles==0,"missed bullets expire at their range limit");
  hp.Kill();hp2.Kill();Frame(110);Check(music.IsPlaying,"camp music returns after roaming combat ends");
  typeof(MiningOperation3D).GetProperty("Phase")!.SetValue(op,MiningPhase.Drilling);Frame(100);Check(!music.IsPlaying,"drilling also fades camp music out");
  host.UnloadScene();Check(!music.IsPlaying,"unloading stops music");Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene"))),"verification preserves scene and saved progress");
 }
}
