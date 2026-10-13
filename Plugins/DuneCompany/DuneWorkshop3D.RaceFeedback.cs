using ByteEngine.Core.AI;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 int _raceAudioCount=-1,_raceAudioGate;RaceState _raceAudioState=RaceState.Waiting;
 void ResetRaceFeedback(){_raceAudioCount=-1;_raceAudioGate=0;_raceAudioState=RaceState.Waiting;foreach(string name in new[]{"race-countdown","race-go","race-checkpoint"})if(_jobSounds.TryGetValue(name,out var sound))sound.Stop();}
 void UpdateRaceFeedback(){
  if(_race==null)return;
  int count=(int)Math.Ceiling(_race.Remaining),gate=_race.Gate("Player");
  if(_race.State==RaceState.Countdown&&count>0&&count!=_raceAudioCount){JobSound("race-countdown",.65f);_raceAudioCount=count;}
  if(_race.State==RaceState.Racing&&_raceAudioState!=RaceState.Racing){JobSound("race-go",.8f);JobNotice("GO!");}
  if(gate>_raceAudioGate&&_race.State==RaceState.Racing&&!(DragRace&&gate==1)){JobSound("race-checkpoint",.65f);JobNotice($"CHECKPOINT {gate} CLEARED");}
  if(_race.State==RaceState.Finished&&_raceAudioState!=RaceState.Finished&&_race.Winner!="Player"){JobSound("race-countdown",.5f);JobNotice("RIVAL FINISHED / RETRY ["+DriveKey("Recover")+"]");}
  _raceAudioGate=gate;_raceAudioState=_race.State;
 }
}
