using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningFieldFixCheck
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS FIELD "+message);}
 public static void Run(Scene scene,MiningOperation3D op)
 {
  var host=new SceneManager();host.LoadScene(scene);
  for(int i=0;i<4;i++){typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[.05d]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,null);}
  op.Initialize();op.TargetEnabled=false;op.TutorialEnabled=false;
  void Set(string n,object value)=>typeof(MiningOperation3D).GetProperty(n)!.SetValue(op,value);
  var player=scene.FindGameObject(op.PlayerObjectId)!;var camp=player.GetComponent<MiningCampLayout3D>()!;
  Set("Phase",MiningPhase.Drilling);Check(camp.EnterEdit(),"arrangement opens during combat");Check(op.Phase==MiningPhase.Drilling&&scene.SimulationTimeScale>0,"arrangement keeps combat live");typeof(MiningOperation3D).GetMethod("CloseCombatBlockingMenus",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(op,null);Check(camp.IsEditing,"wave transition does not close arrangement");camp.ExitEdit();
  var points=scene.GetComponentsSnapshot<MiningSpawnPoint3D>().Where(p=>p.OperationObjectId==op.GameObject.Id&&p.Available).ToArray();
  var choose=typeof(MiningOperation3D).GetMethod("TryChooseSpacedSpawn",BindingFlags.Instance|BindingFlags.NonPublic)!;
  var enemies=(List<GameObject>)typeof(MiningOperation3D).GetField("_enemies",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(op)!;
  for(int i=0;i<12;i++){
   object?[] args=[points,Vector3.Zero,null,null];Check((bool)choose.Invoke(op,args)!,"authored arena supplies grounded spawn "+i);var p=(Vector3)args[1]!;
   Check(enemies.All(e=>Vector2.Distance(new(e.Transform.WorldPosition.X,e.Transform.WorldPosition.Z),new(p.X,p.Z))>=op.SpawnSpacing-.01f),"spawn spacing is preserved");
   var enemy=scene.CreateGameObject("Field spawn fixture");enemy.Transform.WorldPosition=p;enemy.AddComponent(new HealthComponent());enemy.AddComponent(new CapsuleCollider3D{Radius=.3f,Height=1.5f,Center=new(0,.75f,0)});enemies.Add(enemy);
  }
  var last=enemies.Last();last.Transform.WorldPosition+=new Vector3(0,-.25f,0);float before=last.Transform.WorldPosition.Y;
  typeof(MiningOperation3D).GetMethod("TickDirector",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(op,[1f]);Check(last.Transform.WorldPosition.Y>before+.15f,"buried spider is lifted to the visible floor");
  last.Transform.WorldPosition=new(0,-40,-20);last.Active=false;typeof(MiningOperation3D).GetMethod("TickDirector",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(op,[1f]);Check(last.ActiveInHierarchy&&last.Transform.WorldPosition.Y>-2&&op.StrandedEnemiesRecovered>0,"lost inactive enemy returns to a grounded reachable spawn");
  host.UnloadScene();
 }
}
