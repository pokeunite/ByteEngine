using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Runtime;
static class MiningPcMovementProbe
{
 public static void Run()
 {
  using var runtime=new GameProjectRuntime(Program.Root+"/palclone.byteproject",Console.WriteLine);
  var scene=runtime.Serializer.Load(Program.Root+"/Scenes/Main.bytescene");var player=scene.GameObjects.First(o=>o.GetComponent<CharacterController3D>()!=null);
  var motor=player.GetComponent<CharacterController3D>()!;
  var step=typeof(CharacterController3D).GetMethod("Simulate",BindingFlags.NonPublic|BindingFlags.Instance)!;
  var cast=typeof(CharacterController3D).GetMethod("CapsuleCastCurrent",BindingFlags.NonPublic|BindingFlags.Instance)!;
  var time=typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!;
  motor.MoveSpeed=6;
  foreach(float x in new[]{-4f,2f,10f,18f,26f}){
   player.Transform.WorldPosition=new(x,.2f,-12);float travel=0;int stopped=0;
   for(int i=0;i<900;i++){time.Invoke(null,[1d/60]);var before=player.Transform.WorldPosition;motor.Move(-Vector3.UnitZ);step.Invoke(motor,[1f/60]);float d=Vector3.Distance(before,player.Transform.WorldPosition);travel+=d;
    if(i>30&&d<.02f){stopped++;if(stopped==1){object[] args={-Vector3.UnitZ,.15f,default(ByteEngine.Core.Physics.RaycastHit3D)};bool blocked=(bool)cast.Invoke(motor,args)!;var hit=(ByteEngine.Core.Physics.RaycastHit3D)args[2];Console.WriteLine($"STALL x={x} position={player.Transform.WorldPosition} grounded={motor.IsGrounded} velocity={motor.Velocity} blocked={blocked} hit={hit.GameObject?.Name} normal={hit.Normal}");}}
   }
   Console.WriteLine($"LANE x={x} travel={travel:F2} stopped={stopped} end={player.Transform.WorldPosition}");
  }
 }
}
