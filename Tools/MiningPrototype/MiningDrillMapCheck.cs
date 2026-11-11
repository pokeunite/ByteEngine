using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningDrillMapCheck
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS DRILL MAP "+message);}
 static void Set(object obj,string key,object value)=>obj.GetType().GetProperty(key)!.SetValue(obj,value);
 public static void Run(Scene scene,MiningOperation3D op)
 {
  var path=Program.Root+"/Scenes/Main.bytescene";var hash=System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path));
  var host=new SceneManager();host.LoadScene(scene);var ui=scene.GetComponentsSnapshot<MiningBusinessUi3D>().Single();op.TutorialEnabled=false;op.TargetEnabled=false;op.Initialize();Set(op,"CompletedRounds",0);Set(op,"RawOre",0);Set(op,"RefinedOre",0);op.BaseCargoCapacity=100000;
  Check(op.Contracts.Count==30&&op.Contracts.Select(s=>s.Id).Distinct().Count()==30,"30 distinct configured sites are installed");Check(ui.SurveySectorCount==5,"all 30 sites are accessible through five six-site sectors");
  var original=op.ContractsJson;var sites=op.Contracts.ToArray();op.ContractsJson=JsonSerializer.Serialize(sites.Take(3));Check(op.Contracts.Count==3,"legacy editable three-site catalogs remain valid");op.ContractsJson=JsonSerializer.Serialize(new[]{sites[0],sites[0]});Check(op.Contracts.Count==0,"duplicate IDs are rejected");op.ContractsJson=original;
  for(int i=0;i<30;i++){var wave=op.PreviewContract(i)!;Check(wave.Total==sites[i].BaseEnemyCount&&wave.Raiders+wave.Hunters==wave.Total&&wave.Ore>=sites[i].BaseOreReward,"forecast matches real catalog enemy and ore rules for "+sites[i].Name);}
  void Frame(Vector2 mouse,bool down=false,Key? key=null){var raw=new RawInputSnapshot();if(down)raw.MouseButtonsDown.Add(MouseButton.Left);if(key.HasValue)raw.KeysDown.Add(key.Value);typeof(Input).GetMethod("UpdatePortable",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[raw,true]);typeof(Input).GetMethod("SetGameInputCaptured",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[true]);Input.SetGameViewPointer(mouse/new Vector2(1280,720),new(1280,720),true);typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[.05d]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,null);}
  void Click(Vector2 p){Frame(p);Frame(p,true);Frame(p);}
  ui.Open(MiningScreen.Drill);Frame(new(640,360));
  Vector2[] points=[new(50,421),new(401,421),new(113,275),new(448,230),new(150,103),new(500,72)];
  for(int sector=0;sector<5;sector++)
  {
   Click(new(80+18+sector*142+70,40+617));Check(ui.SurveySector==sector,"visible sector tab selects sector "+sector);
   for(int slot=0;slot<6;slot++){Click(new Vector2(80+70,40+84)+points[slot]);Check(op.SelectedContractId==sites[sector*6+slot].Id,"visible map chip selects "+sites[sector*6+slot].Name);}
  }
  Frame(new(640,360),key:Key.Right);Frame(new(640,360));Check(op.SelectedContractId==sites[0].Id,"keyboard site selection wraps across all 30 sites");Frame(new(640,360),key:Key.PageDown);Frame(new(640,360));Check(ui.SurveySector==1,"keyboard sector paging works");
  ui.SelectContract(29);Frame(new(640,360));var preview=op.SelectedContract!;var button=scene.FindGameObject(ui.PrimaryButtonObjectId)!.GetComponent<UiWidget>()!;var rect=UiLayout.Resolve(button.GameObject,button.Anchor,button.Offset,button.Size,new(1280,720));Click(rect.Position+rect.Size/2);
  Check(op.Phase==MiningPhase.Drilling&&ui.Screen==MiningScreen.None&&op.ActiveWave?.Id==preview.Id&&op.EnemiesScheduled==preview.Total&&op.RoundQuota==preview.Ore,"Start Operation launches exactly the selected displayed mission and reward");
  op.SpawnInterval=.1f;op.MinimumReinforcementGap=.25f;
  for(int i=0;i<200;i++)op.Tick(.1f);
  Check(op.AliveEnemies<=24&&op.EnemiesSpawned<op.EnemiesScheduled,"large missions respect the 24-active cap and retain unspawned reinforcements");
  foreach(var enemy in op.OperationEnemies.ToArray())enemy.GetComponent<HealthComponent>()?.Kill();int spawned=op.EnemiesSpawned;for(int i=0;i<12;i++)op.Tick(.1f);Check(op.EnemiesSpawned>spawned&&op.AliveEnemies<=24,"kills release another staggered batch without exceeding the cap");
  host.UnloadScene();Check(hash.SequenceEqual(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))),"map checks leave the authored scene unchanged");
 }
}
