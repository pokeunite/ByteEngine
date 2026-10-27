using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningTerminalVerify
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS TERMINAL "+message);}
 public static void Run(Scene scene,MiningOperation3D op)
 {
  var hash=System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene"));var host=new SceneManager();host.LoadScene(scene);
  var ui=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningBusinessUi3D>().Single();var player=scene.FindGameObject(op.PlayerObjectId)!;var drill=scene.FindGameObject("Animated drill model")!;var tent=scene.FindGameObject("Company tent")!;
  var drillMesh=drill.GetComponent<SkeletalMeshRenderer>()!;var terminalMesh=tent.GetComponent<SkeletalMeshRenderer>()!;
  void Frame(Key? key=null){var input=new RawInputSnapshot();if(key is {} k)input.KeysDown.Add(k);typeof(Input).GetMethod("UpdatePortable",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[input,true]);typeof(Input).GetMethod("SetGameInputCaptured",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[true]);Input.SetGameViewPointer(new(.5f),new(1280,720),true);typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[.05d]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,null);}
  Frame();Check(drillMesh.Model.ProjectPath.EndsWith("MiningDrill-Massive.glb"),"massive drill is assigned to the live scene");Check(terminalMesh.Model.ProjectPath.EndsWith("CompanyTerminal.glb"),"new company terminal is assigned to the live scene");
  Check(drillMesh.HasAnimation("Operate")&&terminalMesh.HasAnimation("Operate"),"both imported operating clips resolve");
  Check(drillMesh.TryGetCurrentModelBounds(out var bounds)&&bounds.Maximum.Y>8.8f&&bounds.Minimum.Y<-6.3f,"drill retains full massive scale and long auger");
  Check(terminalMesh.SkinnedMeshCount>0&&terminalMesh.TryGetBoneWorldMatrix("TerminalInteract",out var marker),"terminal rig and interaction marker import");
  var anchor=scene.FindGameObject(ui.TerminalObjectId)!;terminalMesh.TryGetBoneWorldMatrix("TerminalInteract",out marker);var d=anchor.Transform.WorldPosition-marker.Translation;d.Y=0;Check(d.Length()<.05f,"scene interaction matches model entrance marker");
  Check(ui.DrillInteractionObjectId!=Guid.Empty&&scene.FindGameObject(ui.DrillInteractionObjectId)!.Parent==scene.FindGameObject(op.DrillObjectId),"large drill has a separately exposed control-panel interaction point");
  player.GetComponent<PlayerController3D>()!.Enabled=false;player.GetComponent<MiningThirdPerson3D>()!.Enabled=false;player.GetComponent<CameraBoom3D>()!.Enabled=false;player.Transform.WorldPosition=tent.Transform.WorldPosition+new Vector3(0,0,-3.4f);
  var character=player.GetComponent<CharacterController3D>()!;
  for(int i=0;i<18;i++){character.Move(Vector3.UnitZ);Frame();}
  float z=player.Transform.WorldPosition.Z-tent.Transform.WorldPosition.Z;
  Check(z>-1.9f&&z<0,"player physically walks through the open tent entrance and stops at the desk");character.Move(Vector3.Zero);Frame(Key.E);Frame();
  Check(ui.Screen==MiningScreen.Research&&Input.IsUiPointerRequested,"E at the computer opens company upgrades with a usable cursor");ui.Secondary();Frame();Check(ui.Screen==MiningScreen.Market,"same terminal opens the working market");Frame(Key.Escape);Frame();Check(ui.Screen==MiningScreen.None&&!Input.IsUiPointerRequested,"Escape closes the terminal and returns gameplay cursor ownership");
  player.Transform.WorldPosition=scene.FindGameObject(ui.DrillInteractionObjectId)!.Transform.WorldPosition+new Vector3(0,0,-1.5f);Frame();Frame(Key.E);Frame();Check(ui.Screen==MiningScreen.Drill,"E at the massive drill control panel opens extraction contracts");ui.Close();
  var region=scene.GameObjects.SelectMany(o=>o.Components).OfType<NavigationRegion3D>().Single();foreach(var spawn in scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningSpawnPoint3D>())Check(region.FindPath(spawn.Transform.WorldPosition,scene.FindGameObject(op.DrillObjectId)!.Transform.WorldPosition,20000).Count>0,"enemy route remains reachable around the massive drill");
  Check(op.StartOperation(),"new drill starts an actual extraction");Frame();drillMesh.TryGetBoneWorldMatrix("Auger",out var before);for(int i=0;i<8;i++)Frame();drillMesh.TryGetBoneWorldMatrix("Auger",out var after);Check(before!=after&&drillMesh.IsPlaying,"massive auger actually rotates during extraction");
  Check(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene")).SequenceEqual(hash),"verification preserves authored scene and progress");host.UnloadScene();Console.WriteLine("TERMINAL AND MASSIVE DRILL NATIVE VERIFICATION COMPLETE");
 }
}
