using System.Numerics;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Tests;
internal static class RopeWinchTests
{
 public static void Run()
 {
  if(RopeWinchDynamics.Tension(2,3,10,14000,1800,12000)!=0)throw new Exception("Slack rope pushed its bodies");
  if(RopeWinchDynamics.Tension(10,2,10,14000,1800,12000)!=12000)throw new Exception("Pull limit failed");
  var scene=new Scene("Rope fixture");scene.Physics.SolverIterations=1;
  var a=scene.CreateGameObject("Winch");var b=scene.CreateGameObject("Cargo");b.Transform.LocalPosition=new(10,0,0);
  var bodyA=a.AddComponent(new Rigidbody3D{Mass=100,UseGravity=false});var bodyB=b.AddComponent(new Rigidbody3D{Mass=100,UseGravity=false});
  var rope=a.AddComponent(new RopeWinch3D{ConnectedObject=b.Id,Length=5,MaximumPull=1000,RenderCable=false});rope.Solve(1f/60);
  if(bodyA.Velocity.X<=0||bodyB.Velocity.X>=0||(bodyA.Velocity+bodyB.Velocity).Length()>.0001f)throw new Exception("Rope lost equal/opposite endpoint impulses");
  rope.Disconnect();var velocity=bodyA.Velocity;rope.Solve(1f/60);if(bodyA.Velocity!=velocity)throw new Exception("Disconnected rope still pulled");
  if(RopeWinchDynamics.Reel(2,1,10,1,.7f,24)!=.7f||RopeWinchDynamics.Reel(23,-1,10,1,.7f,24)!=24)throw new Exception("Reel bounds failed");
  rope.Reconnect();rope.BreakForce=100;rope.Solve(1f/60);if(!rope.Broken||rope.Connected)throw new Exception("Rope break force failed");
  Console.WriteLine("PASS native Rope Winch: slack, equal/opposite pull, reel bounds, disconnect and break force");
 }
}
