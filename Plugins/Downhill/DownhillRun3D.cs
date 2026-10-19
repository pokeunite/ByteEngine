using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.InputSystem;
namespace Downhill;
public sealed class DownhillRun3D:Component
{
 public string BoardObject {get;set;}="Board";
 public string SpeedText {get;set;}="Speed";
 public string StateText {get;set;}="Ride state";
 public string TimerText {get;set;}="Run time";
 public string ResultPanel {get;set;}="Result panel";
 public string PausePanel {get;set;}="Pause panel";
 public Vector3 FinishCenter {get;set;}
 public float FinishRadius {get;set;}=6;
 public float FallHeight {get;set;}=-15;
 public Key RetryKey {get;set;}=Key.R;
 public Key PauseKey {get;set;}=Key.Escape;
 public Key QualityKey {get;set;}=Key.Q;
 public bool StartOnPush {get;set;}=true;
 public bool Started {get;private set;}
 public bool Finished {get;private set;}
 public bool Paused {get;private set;}
 public double ElapsedSeconds {get;private set;}
 BoardController3D? board;UiText? speed,state,clock;float airborne;bool bailed,waiting;
 protected override void OnStart(){var scene=GameObject.Scene!;board=scene.FindGameObject(BoardObject)?.GetComponent<BoardController3D>();waiting=StartOnPush;if(board!=null&&waiting){board.ControlsEnabled=false;if(board.Body!=null)board.Body.UseGravity=false;}speed=scene.FindGameObject(SpeedText)?.GetComponent<UiText>();state=scene.FindGameObject(StateText)?.GetComponent<UiText>();clock=scene.FindGameObject(TimerText)?.GetComponent<UiText>();}
 protected override void OnUpdate(){
  if(board==null)return;var s=GameObject.Scene!;
  if(Input.IsKeyPressed(RetryKey)){s.RequestRestart();return;}
  if(Input.IsKeyPressed(QualityKey)&&s.FindComponent<GraphicsSettings3D>() is {} graphics)graphics.Quality=graphics.Quality==GraphicsQuality.Fast?GraphicsQuality.Balanced:graphics.Quality==GraphicsQuality.Balanced?GraphicsQuality.High:GraphicsQuality.Fast;
  if(Input.IsKeyPressed(PauseKey)){Paused=!Paused;s.SimulationTimeScale=Paused?0:1;}
  var pause=s.FindGameObject(PausePanel);if(pause!=null)pause.Active=Paused;
  if(waiting&&board.PushInput&&!Paused){waiting=false;board.ControlsEnabled=true;if(board.Body!=null)board.Body.UseGravity=true;}
  if(board.Speed>.5f)Started=true;if(Started&&!Finished&&!Paused)ElapsedSeconds+=Time.DeltaTime;
  if(!Finished&&Vector3.Distance(board.Transform.WorldPosition,FinishCenter)<FinishRadius){Finished=true;board.ControlsEnabled=false;board.Body?.AddImpulse(-board.Body.Velocity*board.Body.Mass*.9f);}
  if(Finished&&board.Body!=null){board.Body.UseGravity=false;board.Body.Velocity*=MathF.Exp(-5*(float)Time.DeltaTime);}
  var result=s.FindGameObject(ResultPanel);if(result!=null)result.Active=Finished;
  airborne=board.Grounded?0:airborne+(float)Time.DeltaTime;
  bool fallen=board.Transform.WorldPosition.Y<FallHeight||board.Crashed||(Started&&airborne>1);
  if(fallen&&!bailed){bailed=true;board.ControlsEnabled=false;if(board.Body!=null){board.Body.Velocity=Vector3.Zero;board.Body.UseGravity=false;}}
  if(speed!=null)speed.Text=$"{board.Speed*3.6f:0} km/h";
  if(clock!=null)clock.Text=$"{ElapsedSeconds:00.0}s";
  if(state!=null)state.Text=Finished?"RUN COMPLETE / R TO RIDE AGAIN":bailed?"BAILED / R TO RESTART":Paused?"PAUSED":board.IsSliding?(board.HandDragInput?"HANDS-DOWN SLIDE":"SPEED CHECK"):board.BrakeInput?"FOOT BRAKE":board.TuckInput?"TUCK":Started?"CARVE":"W TO PUSH / GRAVITY TAKES OVER";
 }
}


