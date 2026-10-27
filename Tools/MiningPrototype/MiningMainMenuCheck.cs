using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using ByteEngine.Core;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningMainMenuCheck
{
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS MAIN MENU/REFRESH "+text);}
 public static void Run(Scene scene,MiningOperation3D op)
 {
  var hash=SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene"));var menu=op.GameObject.GetComponent<MiningPauseMenu3D>()!;menu.ShowMainMenuOnStart=true;menu.PersistSettings=false;
  var host=new SceneManager();host.LoadScene(scene);
  void Frame(int n=1){for(int i=0;i<n;i++){typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[.05d]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,null);}}
  Frame(4);Check(menu.IsOpen&&menu.MainMenuMode,"startup scene opens the functional main menu");double time=scene.SimulationTime;Frame(8);Check(scene.SimulationTime==time,"gameplay stays frozen behind the main menu");
  var gun=scene.FindGameObject(op.PlayerObjectId)!.GetComponent<FpsGunLab3D>()!;var weapon=gun.SelectedWeapon;Check(!gun.AllowWeaponDropping&&!gun.Drop()&&gun.SelectedWeapon==weapon,"equipped weapons cannot be dropped through any input route");
  var audio=op.GameObject.GetComponent<MiningAudio3D>()!;Check(audio.CampMusicVolume==.25f&&audio.DrillLoop.ProjectPath.EndsWith("cnc-drill-loop.wav"),"softer camp volume and supplied drill recording are assigned");
  Check(op.HunterMoveSpeed==6&&op.RaiderMoveSpeed==4.8f&&op.HunterAttackCooldown<.7f&&op.RaiderAttackCooldown<.8f,"both roles move faster and attack more frequently");Check(op.HunterHealth==50&&op.RaiderHealth==75,"enemy health stays unchanged");
  var fx=op.GameObject.GetComponent<MiningEffects3D>()!;ByteEngine.Core.Animation.AnimationRuntimeAssets.TryGet(out var assets);
  foreach(var reference in new[]{fx.MineExplosion,fx.MortarExplosion}){var effect=assets!.LoadVfxEffect(reference);Check(effect.Layers[0].FlipbookColumns==5&&effect.Layers[0].FlipbookRows==5&&effect.Layers[0].Texture.ProjectPath.Contains("UnityLabs"),"native VFX imports the Unity Labs 25-frame explosion");Check(effect.Layers.Any(l=>l.Name=="Initial blast flash")&&effect.ParticleBudget<=96,"blast flash is layered within the existing particle budget");}
  menu.ShowPage(MiningPausePage.Settings);Check(menu.IsOpen&&menu.MainMenuMode,"main menu can open settings while keeping gameplay frozen");menu.ShowPage(MiningPausePage.Pause);menu.Resume();Frame(3);Check(!menu.MainMenuMode&&!menu.IsOpen&&scene.SimulationTime>time,"Continue enters gameplay and restores its controls");
  Check(!scene.FindGameObject(op.PlayerObjectId)!.GetComponent<PlayerController3D>()!.AllowJump,"project controller disallows jumping");
  foreach(var point in scene.GameObjects.Where(o=>o.GetComponent<MiningSpawnPoint3D>()!=null).ToArray())foreach(bool hunter in new[]{false,true}){
   var e=ByteEngine.Core.VisualLogic.RuntimeSpawnService.SpawnBlueprint(scene,hunter?op.HunterBlueprint:op.RaiderBlueprint,point.Transform.WorldPosition)!;var ai=e.GetComponent<ByteEngine.Core.AI.AIBehavior3D>()!;ai.TargetObjectId=hunter?op.PlayerObjectId:op.DrillObjectId;ai.FindNearestCharacter=false;e.GetComponent<SimpleEnemyAI3D>()!.TargetId=ai.TargetObjectId;op.ConfigureEnemy(e,hunter);var start=e.Transform.WorldPosition;Frame(50);Check(System.Numerics.Vector3.Distance(start,e.Transform.WorldPosition)>2,$"{(hunter?"hunter":"raider")} advances from {point.Name}: {ai.LastError}");Check(ai.LastError.Length==0,"behaviour and sub-behaviour references resolve");scene.DestroyGameObject(e);
  }
  Check(op.StartOperation(),"faster combat operation starts");Frame(12);var positions=op.SpawnedEnemies.ToDictionary(e=>e,e=>e.Transform.WorldPosition);Frame(100);foreach(var e in positions.Keys.Where(e=>e.Scene==scene)){var ai=e.GetComponent<ByteEngine.Core.AI.AIBehavior3D>()!;var nav=e.GetComponent<ByteEngine.Core.Navigation.NavigationAgent3D>()!;Check(System.Numerics.Vector3.Distance(positions[e],e.Transform.WorldPosition)>2,"live spawned spider advances from distant lane");Check(ai.LastError.Length==0,"assigned spider behaviour resolves");Console.WriteLine($"CHASE PROBE {e.Name} moved={System.Numerics.Vector3.Distance(positions[e],e.Transform.WorldPosition)} task={ai.CurrentTask} error={ai.LastError} nav={nav.Status} target={ai.TargetObjectId} pos={e.Transform.WorldPosition}");}var drill=scene.GameObjects.Single(o=>o.Name=="Mining loop - Drill").GetComponent<AudioSource3D>()!;Check(drill.IsPlaying&&drill.ClipLoaded,"user-supplied drill loop plays during real extraction");
  string dist="C:/Users/codex/ByteEngine/Dist/ByteEngine";
  Check(SHA256.HashData(File.ReadAllBytes(dist+"/ByteEngine.Core.dll")).SequenceEqual(SHA256.HashData(File.ReadAllBytes(dist+"/PlayerRuntime/ByteEngine.Core.dll"))),"installed editor and Windows exporter have identical engine builds");
  using(var stream=File.OpenRead(dist+"/PlayerRuntime/ByteEngine.Core.dll"))using(var pe=new PEReader(stream)){var metadata=pe.GetMetadataReader();var names=metadata.TypeDefinitions.Select(t=>metadata.GetString(metadata.GetTypeDefinition(t).Name)).ToHashSet();Check(names.Contains("ICharacterDebugSource")&&names.Contains("CombatFeedback3D")&&names.Contains("TopDownController3D"),"published player includes the exact engine types missing from the failed export");}
  host.UnloadScene();Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene"))),"verification preserves scene and company progress");
 }
}
