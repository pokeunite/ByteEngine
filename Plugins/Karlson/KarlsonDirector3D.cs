using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
namespace Karlson.Plugin;

public enum CourseMode{Menu,Playing,Paused,Failed,Completed}
public sealed class CourseRecords
{
    public Dictionary<string,float> BestTimes{get;set;}=new();
    public float Sensitivity{get;set;}=.13f;
    public bool Music{get;set;}=true;
    public bool Shake{get;set;}=true;
}
public sealed class KarlsonDirector3D:Component
{
    internal string ProjectRoot="";
    public string CourseTitle{get;set;}="01 / ORIENTATION";
    public string CourseHint{get;set;}="Reach the milk. Use E to pick up the pistol.";
    public int CourseNumber{get;set;}=1;
    public float GoldTime{get;set;}=35;
    public bool StartInMenu{get;set;}
    public CourseMode Mode{get;private set;}=CourseMode.Menu;
    public float Elapsed{get;private set;}
    public float Focus=>_focus?.Current??100;
    public bool Playing=>Mode==CourseMode.Playing;
    public bool EnemiesCleared=>_enemies.Length>0&&_enemies.All(e=>!e.Alive);
    public int RemainingEnemies=>_enemies.Count(e=>e.Alive);
    public float BestTime=>_records.BestTimes.GetValueOrDefault(CourseNumber.ToString(),0);
    public int HitCount{get;private set;}
    public override int UpdateOrder=>-900;
    ParkourPlayer3D? _player;HealthComponent? _health;ResourceMeter3D? _focus;KarlsonEnemy3D[] _enemies=[];GameSaveService? _saves;CourseRecords _records=new();
    readonly Dictionary<string,UiText> _texts=new();readonly Dictionary<string,UiWidget> _widgets=new();readonly Dictionary<string,GameObject> _groups=new();readonly Dictionary<string,AudioSource3D> _sounds=new();
    float _hit,_hurt,_uiTimer;bool _settings,_capturedOnce;
    protected override void OnStart()
    {
        var scene=GameObject.Scene!;_player=scene.FindComponent<ParkourPlayer3D>();_health=_player?.GameObject.GetComponent<HealthComponent>();_focus=_player?.GameObject.GetComponent<ResourceMeter3D>();
        _enemies=scene.GameObjects.Select(o=>o.GetComponent<KarlsonEnemy3D>()).OfType<KarlsonEnemy3D>().ToArray();
        foreach(var obj in scene.GameObjects){if(obj.GetComponent<UiText>() is {} text)_texts[obj.Name]=text;if(obj.GetComponent<UiWidget>() is {} widget)_widgets[obj.Name]=widget;if(obj.Name.EndsWith("Group"))_groups[obj.Name]=obj;if(obj.GetComponent<AudioSource3D>() is {} sound)_sounds[obj.Name]=sound;}
        if(ProjectRoot.Length>0){_saves=new(ProjectRoot);try{if(_saves.Exists("course-records.json"))_records=_saves.Load<CourseRecords>("course-records.json")??new();}catch(Exception ex){Console.WriteLine("Course record load: "+ex.Message);}}
        ApplySettings();if(_health!=null){_health.Died+=OnDeath;_health.Damaged+=OnDamage;}
        Mode=StartInMenu?CourseMode.Menu:CourseMode.Playing;UpdateGroups();if(_records.Music)_sounds.GetValueOrDefault("Sound-music")?.Play();
    }
    public void Begin(){Mode=CourseMode.Playing;Elapsed=0;_focus?.Reset();UpdateGroups();}
    public void Pause(){if(Playing){Mode=CourseMode.Paused;UpdateGroups();}}
    public void Resume(){if(Mode==CourseMode.Paused){_capturedOnce=false;Mode=CourseMode.Playing;UpdateGroups();}}
    public void Die()
    {
        if(!Playing)return;
        var details=$"Karlson failure: course={CourseTitle}; elapsed={Elapsed:0.000}; position={_player?.Transform.WorldPosition}; velocity={_player?.Motor?.Velocity}; health={_health?.CurrentHealth}; grounded={_player?.IsGrounded}; lastGround={_player?.LastGroundName}; lastGroundPosition={_player?.LastGroundedPosition}";
        ByteEngine.Core.Diagnostics.RuntimeDiagnostics.OutputSink?.Invoke(details);Console.WriteLine(details);
        if(ProjectRoot.Length>0){try{Directory.CreateDirectory(Path.Combine(ProjectRoot,"Logs"));File.WriteAllText(Path.Combine(ProjectRoot,"Logs","Karlson last failure.txt"),details);}catch(Exception ex){Console.WriteLine("Failure diagnostic: "+ex.Message);}}
        Mode=CourseMode.Failed;Sound("death");UpdateGroups();
    }
    public void Complete()
    {
        if(!Playing)return;Mode=CourseMode.Completed;Sound("goal");
        if(BestTime<=0||Elapsed<BestTime)_records.BestTimes[CourseNumber.ToString()]=Elapsed;
        Save();UpdateGroups();
    }
    void OnDeath(HealthComponent health)=>Die();
    void OnDamage(HealthComponent health,float amount){_hurt=.55f;Sound("hurt");}
    public void HitMarker(){_hit=.16f;HitCount++;Sound("hit");}
    public void Sound(string name){if(_sounds.TryGetValue("Sound-"+name,out var source))source.Play();}
    protected override void OnUpdate()
    {
        float real=(float)Time.UnscaledDeltaTime;
        if(Playing)
        {
            if(Input.IsGameInputCaptured)_capturedOnce=true;
            if(_capturedOnce)Elapsed+=real;
            if(Input.IsKeyPressed(Key.Escape)||(_capturedOnce&&!Input.IsGameInputCaptured))Pause();
            if(_player?.Transform.WorldPosition.Y<-12)Die();
        }
        else if(Input.IsKeyPressed(Key.Escape)&&Mode==CourseMode.Paused)Resume();
        if(Input.IsKeyPressed(Key.R)&&Mode is CourseMode.Failed or CourseMode.Completed)GameObject.Scene?.RequestRestart();
        bool slow=Playing&&Input.IsKeyDown(Key.Tab)&&(_focus?.TrySpend(real*34)??false);
        if(!slow&&!Input.IsKeyDown(Key.Tab))_focus?.Restore(real*17);
        GameObject.Scene!.SimulationTimeScale=Playing&&_capturedOnce?(slow?.25f:1):0;
        if(!Playing)Input.NotifyGameViewPointerAim();
        _hit=Math.Max(0,_hit-real);_hurt=Math.Max(0,_hurt-real);
        for(int i=0;i<4;i++)if(Clicked("Course"+i))GameObject.Scene.RequestLoad($"Scenes/Course{i+1:00}.bytescene");
        if(Clicked("ResumeButton"))Resume();
        if(Clicked("RetryButton")||Clicked("ResultRetry"))GameObject.Scene.RequestRestart();
        if(Clicked("MenuButton")||Clicked("ResultMenu"))GameObject.Scene.RequestLoad("Scenes/MainMenu.bytescene");
        if(Clicked("NextButton"))GameObject.Scene.RequestLoad(CourseNumber<4?$"Scenes/Course{CourseNumber+1:00}.bytescene":"Scenes/MainMenu.bytescene");
        if(Clicked("OptionsButton")){_settings=!_settings;if(_groups.TryGetValue("HelpGroup",out var group))group.Active=_settings;}
        if(Clicked("MusicButton")||Input.IsKeyPressed(Key.M)){_records.Music=!_records.Music;ApplySettings();Save();}
        if(Clicked("ShakeButton")){_records.Shake=!_records.Shake;ApplySettings();Save();}
        if(Clicked("SensitivityDown")){_records.Sensitivity=Math.Max(.04f,_records.Sensitivity-.02f);ApplySettings();Save();}
        if(Clicked("SensitivityUp")){_records.Sensitivity=Math.Min(.4f,_records.Sensitivity+.02f);ApplySettings();Save();}
        _uiTimer-=real;if(_uiTimer<=0){_uiTimer=.05f;UpdateHud();}
        if(_widgets.TryGetValue("DamageOverlay",out var overlay))overlay.Color=new(1,.08f,.04f,_hurt*.25f);
        if(_texts.TryGetValue("HitMarker",out var marker))marker.Visible=_hit>0&&Playing;
    }
    bool Clicked(string name)=>_widgets.TryGetValue(name,out var widget)&&widget.WasClicked;
    void ApplySettings(){if(_player!=null){_player.MouseSensitivity=_records.Sensitivity;_player.ShakeMultiplier=_records.Shake?1:0;}if(_sounds.TryGetValue("Sound-music",out var music)){music.Volume=_records.Music?.15f:0;}}
    void Save(){try{_saves?.Save("course-records.json",_records);}catch(Exception ex){Console.WriteLine("Course record save: "+ex.Message);}}
    void UpdateGroups(){if(GameObject.Scene!=null)GameObject.Scene.SimulationTimeScale=Playing?1:0;foreach(var pair in _groups)pair.Value.Active=pair.Key switch{"MenuGroup"=>Mode==CourseMode.Menu,"HUDGroup"=>Playing,"PauseGroup"=>Mode==CourseMode.Paused,"ResultsGroup"=>Mode is CourseMode.Completed or CourseMode.Failed,"HelpGroup"=>_settings,_=>pair.Value.Active};}
    void Text(string name,string value){if(_texts.TryGetValue(name,out var text)&&text.Text!=value)text.Text=value;}
    void UpdateHud()
    {
        Text("CourseTitle",CourseTitle);Text("Timer",Elapsed.ToString("00.00")+" s");Text("Speed",$"{_player?.Speed??0:0.0} m/s");Text("Weapon",$"{_player?.EquippedName??"EMPTY HANDS"}   {_player?.Ammo??0:00}");
        Text("Hint",Playing&&!_capturedOnce?"Click Game View to capture the mouse and start.":CourseHint);Text("EnemyCount",RemainingEnemies+" HOSTILES");Text("MoveState",_player?.Grappling==true?"GRAPPLE":_player?.WallRunning==true?"WALL RUN":_player?.Sliding==true?"SLIDE":"");
        Text("ResultTitle",Mode==CourseMode.Completed?"MILK SECURED":"TRY THAT AGAIN");Text("ResultDetails",Mode==CourseMode.Completed?$"{CourseTitle}\nTIME {Elapsed:0.00}s   BEST {BestTime:0.00}s\n{(Elapsed<=GoldTime?"GOLD":Elapsed<=GoldTime*1.6f?"SILVER":"BRONZE")} ROUTE": "R to retry instantly. Keep moving and watch the red shots.");
        Text("SettingsReadout",$"LOOK {_records.Sensitivity:0.00}  |  MUSIC {(_records.Music?"ON":"OFF")}  |  SHAKE {(_records.Shake?"ON":"OFF")}");
        if(_widgets.TryGetValue("FocusBar",out var focus))focus.Value=Focus;
        if(_widgets.TryGetValue("HealthBar",out var hp))hp.Value=_health?.CurrentHealth??100;
        if(_widgets.TryGetValue("NextButton",out var next))next.Visible=Mode==CourseMode.Completed;
    }
    protected override void OnStop(){if(_health!=null){_health.Died-=OnDeath;_health.Damaged-=OnDamage;}if(GameObject.Scene!=null)GameObject.Scene.SimulationTimeScale=1;}
}
