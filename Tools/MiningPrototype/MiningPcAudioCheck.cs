using System.Reflection;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Audio;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
static class MiningPcAudioCheck
{
 public static void Run(Scene scene,MiningOperation3D op)
 {
  var menu=op.GameObject.GetComponent<MiningPauseMenu3D>()!;menu.ShowMainMenuOnStart=true;menu.PersistSettings=false;
  var host=new SceneManager();host.LoadScene(scene);
  void Frame(){typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[.05d]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(scene,null);}
  for(int i=0;i<24;i++)Frame();
  var audio=op.GameObject.GetComponent<MiningAudio3D>()!;var music=scene.GameObjects.Single(o=>o.Name=="Mining camp music").GetComponent<AudioSource3D>()!;
  if(!menu.MainMenuMode||scene.SimulationTimeScale!=0||!music.IsPlaying||music.Volume<=.1f)throw new Exception("Main-menu music failed while simulation paused");
  Console.WriteLine("PASS PC AUDIO music plays and fades with gameplay paused");
  void Point(Vector2 p){typeof(Input).GetMethod("UpdatePortable",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[new RawInputSnapshot(),false]);Input.SetGameViewPointer(p/new Vector2(1280,720),new(1280,720),true);Frame();}
  Point(new(180,340));if(audio.LastCue!="TerminalSelect")throw new Exception("Menu hover did not produce the quiet selection cue");int count=audio.PlayedCues;for(int i=0;i<10;i++)Point(new(180,340));
  if(audio.PlayedCues!=count)throw new Exception("Stationary pointer repeats hover cue");
  var voice=scene.GameObjects.Last(o=>o.Name=="Mining audio voice - TerminalSelect").GetComponent<AudioSource3D>()!;
  if(voice.Volume>audio.InterfaceVolume*.23f)throw new Exception("Hover cue is not subtle");
  Console.WriteLine("PASS PC AUDIO hover cue is quiet and fires once per pointer entry");host.UnloadScene();
 }
}
