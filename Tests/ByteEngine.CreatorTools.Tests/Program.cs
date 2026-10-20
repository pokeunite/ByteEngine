using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Core.Variables;
using ByteEngine.Core.Graphics;

int count=0;
void Check(bool test,string label){if(!test)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
var registry=VisualLogicRegistry.CreateDefault();var scene=new Scene("Creator tools");var owner=scene.CreateGameObject("Owner");var globals=new VariableStore();
EventExecutionContext Context()=>new(){Scene=scene,Self=owner,Globals=globals};
VisualInstruction I(string id,params (string,EventValue)[] args)=>new(){Id=id,Arguments=args.ToDictionary(a=>a.Item1,a=>a.Item2)};
void Action(VisualInstruction i){if(!registry.TryGetAction(i.Id,out var a))throw new Exception(i.Id);a!.Execute(i,Context());}
void Tick(EventModuleRuntime runtime,EventModuleDefinition module,double delta){typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[delta]);runtime.Update(module,globals,scene,owner);}
int hits=0;registry.RegisterAction(new(){Id="probe",Category="Tests",DisplayName="Probe",Execute=(i,c)=>hits++});
var data=VariableJson.Parse("{\"items\":[1,true,null,{\"text\":\"water\"}]}");
Check(data.Children["items"].Items[2].Type==VariableType.Null,"JSON preserves null, arrays and structures");
var clone=data.Clone();clone.Children["items"].Items[3].Children["text"].String="changed";
Check(data.Children["items"].Items[3].Children["text"].String=="water","Nested variable clones do not share mutable children");
Check(VariableJson.Write(VariableJson.Parse(VariableJson.Write(data)))==VariableJson.Write(data),"Data JSON round trips without engine-specific metadata");
Action(I("collection.createArray",("name",EventValue.String("Bag"))));Action(I("collection.append",("name",EventValue.String("Bag")),("value",EventValue.Number(7))));
Action(I("collection.getAt",("name",EventValue.String("Bag")),("index",EventValue.Number(0)),("output",EventValue.String("First"))));
Check(owner.Variables["First"].Number==7,"Collection actions write and read the selected variable scope");
var wait=I("time.wait",("seconds",EventValue.Number(1)));var probe=I("probe");var once=I("system.triggerOnce");var always=I("system.always");
var module=new EventModuleDefinition();module.Rules.Add(new(){Conditions=[always,once],Actions=[wait,probe]});var runtime=new EventModuleRuntime(registry);
Tick(runtime,module,.1);Check(hits==0&&runtime.PendingWaitCount==1,"Wait suspends the remaining legacy action chain");
Tick(runtime,module,.4);Check(hits==0,"Wait does not resume early");Tick(runtime,module,.6);Check(hits==1&&runtime.PendingWaitCount==0,"Wait resumes once after its duration");
runtime.Reset();Tick(runtime,module,.1);runtime.Reset();Tick(runtime,new EventModuleDefinition(),2);Check(hits==1,"Runtime reset cancels pending continuations");
var missing=I("object.exists",("target",EventValue.String("Missing")));var not=I("logic.not");not.ConditionInputIds=[missing.InstanceId];
module=new();module.Rules.Add(new(){HasExplicitConditionFlow=true,ConnectedConditionIds=[not.InstanceId],Conditions=[missing,not],Actions=[probe]});runtime=new(registry);Tick(runtime,module,.1);
Check(hits==2,"NOT executes when its nested condition is false");not.ConditionInputIds.Add(always.InstanceId);module.Rules[0].Conditions.Add(always);Tick(runtime,module,.1);Check(hits==2,"NOT blocks when any nested condition is true");
var tag=scene.Classification.AddTag("Targets")!;var a=scene.CreateGameObject("A");a.AddTag(tag.Id);var b=scene.CreateGameObject("B");b.AddTag(tag.Id);
module=new();module.Rules.Add(new(){Conditions=[I("pick.all",("tag",EventValue.String("Targets")))],Actions=[I("object.setActive",("target",EventValue.String("Picked")),("active",EventValue.Boolean(false)))]});runtime=new(registry);Tick(runtime,module,.1);
Check(!a.Active&&!b.Active&&owner.Active,"Picked-object actions run over a stable selection without changing Self");
var camera=scene.CreateGameObject("Camera").AddComponent(new Camera3D());scene.SetActiveCamera(camera);
Action(I("camera.setFov",("degrees",EventValue.Number(75))));Check(camera.FieldOfView==75,"Camera event actions operate on the native active camera");
string root=Path.Combine(Path.GetTempPath(),"ByteCreatorTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
typeof(Scene).GetProperty("ProjectRoot")!.SetValue(scene,root);
try{Action(I("storage.save",("key",EventValue.String("test")),("value",EventValue.String("saved"))));Action(I("storage.load",("key",EventValue.String("test")),("output",EventValue.String("Loaded"))));Check(owner.Variables["Loaded"].String=="saved","Saved values persist through the native project-scoped save service");}
finally{Directory.Delete(root,true);}
bool Condition(VisualInstruction i){registry.TryGetCondition(i.Id,out var definition);return definition!.Evaluate(i,Context());}
Action(I("inventory.add",("item",EventValue.String("sword")),("amount",EventValue.Number(2))));
Action(I("inventory.equip",("item",EventValue.String("sword"))));
Check(Condition(I("inventory.equipped",("item",EventValue.String("sword")))),"Inventory equipment refers to owned items");
Action(I("inventory.remove",("item",EventValue.String("sword")),("amount",EventValue.Number(2))));
Check(!Condition(I("inventory.equipped",("item",EventValue.String("sword")))),"Removing the last item clears equipment references");
Action(I("links.connect",("other",EventValue.String("A"))));
Check(Condition(I("links.connected",("other",EventValue.String("A")))),"Gameplay links connect objects without transform parenting");
Action(I("links.clear"));Check(!Condition(I("links.connected",("other",EventValue.String("A")))),"Clearing relationships removes outgoing links");
Action(I("dialogue.load",("json",EventValue.String("{\"start\":\"a\",\"nodes\":{\"a\":{\"text\":\"Choose\",\"choices\":[{\"text\":\"Continue\",\"next\":\"b\"}]},\"b\":{\"text\":\"Done\"}}}"))));
Action(I("dialogue.start"));Check(Condition(I("dialogue.hasChoices")),"Dialogue exposes branching choices");
Action(I("dialogue.choose"));Action(I("dialogue.read",("output",EventValue.String("Line"))));
Check(owner.Variables["Line"].Children["text"].String=="Done","Dialogue follows the selected destination");
Action(I("dialogue.advance"));Check(Condition(I("dialogue.ended")),"Dialogue finishes at a terminal node");
double tweenValue=0;var services=CreatorSceneServices.For(scene);services.StartTween("test",1,"Linear",v=>tweenValue=v);
services.Advance(.4,[]);services.PauseTween("test",true);services.Advance(1,[]);
Check(Math.Abs(tweenValue-.4)<.001,"Paused tweens do not advance");services.PauseTween("test",false);services.Advance(.6,[]);
Check(tweenValue==1&&services.GetTween("test")!.FinishedThisUpdate,"Tween reaches its destination and emits completion");services.Advance(.1,[]);Check(!services.GetTween("test")!.FinishedThisUpdate,"Tween completion pulse clears next update");
var defaults=VariableJson.Parse("{\"amount\":1}");var function=new EventRuleDefinition{FunctionName="Echo",FunctionDefaults=defaults,Actions=[I("function.readArgument",("parameter",EventValue.String("amount")),("output",EventValue.String("Argument"))),I("function.setReturn",("value",new EventValue{Kind=EventValueKind.Reference,Reference=new VariableReference{Scope=VariableScope.Self,MemberName="Argument"}}))]};
module=new();module.Rules.Add(function);module.Rules.Add(new(){Actions=[I("function.call",("function",EventValue.String("Echo")),("parameters",EventValue.String("{\"amount\":9}")),("output",EventValue.String("Returned")))]});runtime=new(registry);Tick(runtime,module,.1);
Check(owner.Variables["Returned"].Number==9,"Callable events receive typed parameters and return values");
int before=hits;
function.HasExplicitConditionFlow=true;function.SubEvents.Add(new(){Actions=[I("probe")]});runtime=new(registry);Tick(runtime,module,.1);
Check(hits==before+1,"Function entry without conditions still executes its nested events");
var container=new EventRuleDefinition{Conditions=[I("object.exists",("target",EventValue.String("Missing")))],SubEvents=[function]};module.Rules[0]=container;runtime=new(registry);Tick(runtime,module,.1);
Check(owner.Variables["Returned"].Number==9&&hits==before+2,"Nested function definitions remain callable independently of their parent event");
TouchInput.Feed(7,.2f,.3f,TouchPhase.Started);typeof(TouchInput).GetMethod("BeginFrame",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,null);
Check(TouchInput.Points.Single().Phase==TouchPhase.Started,"Touch start is visible for the whole frame");typeof(TouchInput).GetMethod("BeginFrame",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,null);
Check(TouchInput.Points.Single().Id==7&&TouchInput.Points.Single().Phase==TouchPhase.Held,"Touch ID remains stable while held");TouchInput.CancelAll();typeof(TouchInput).GetMethod("BeginFrame",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,null);
Check(TouchInput.Points.Single().Phase==TouchPhase.Cancelled,"Focus loss can cancel active touches");
if(args.Length>0)
{
    using var video=new ByteEngine.Core.Media.WindowsVideoReader(args[0]);var first=video.ReadFrame();
    Check(first!=null&&first.Value.Pixels.Length==video.Width*video.Height*4,"Native video decodes a complete RGBA frame");
    video.Seek(1);var sought=video.ReadFrame();Check(sought!=null&&sought.Value.Seconds>=.9,"Native video seek moves the decoder timeline");
}
var colliderA=a.AddComponent(new ByteEngine.Core.Characters.BoxCollider3D());var colliderB=b.AddComponent(new ByteEngine.Core.Characters.BoxCollider3D());
var contact=new ByteEngine.Core.Physics.PhysicsContactPair3D(a,colliderA,b,colliderB,Vector3.Zero,Vector3.UnitY,.1f,false);
services.Advance(.1,[contact]);Check(services.Contact(a.Id,b.Id,"enter",false)!=null,"Contact lifecycle identifies collision entry");
services.Advance(.1,[contact]);Check(services.Contact(a.Id,b.Id,"enter",false)==null&&services.Contact(a.Id,b.Id,"stay",false)!=null,"Persistent contact does not retrigger entry");
services.Advance(.1,[]);Check(services.Contact(a.Id,b.Id,"exit",false)!=null&&services.Contact(a.Id,b.Id,"exit",true)==null,"Collision exit is separate from trigger exit");
services.StartTween("instant",0,"Linear",v=>tweenValue=v);services.Advance(.1,[]);Check(services.GetTween("instant")!.FinishedThisUpdate,"Zero-duration tween exposes completion on the next update");
before=hits;var branch=I("flow.branch",("condition",EventValue.Boolean(true)));wait=I("time.wait",("seconds",EventValue.Number(.2)));probe=I("probe");branch.TrueActionId=wait.InstanceId;wait.NextActionId=probe.InstanceId;
module=new();module.Rules.Add(new(){Conditions=[I("system.triggerOnce")],HasExplicitExecutionFlow=true,FirstActionId=branch.InstanceId,Actions=[probe,branch,wait]});runtime=new(registry);Tick(runtime,module,.1);Tick(runtime,module,.2);
Check(hits==before+1,"Wait resumes the wired branch rather than action list order");
a.Active=b.Active=true;
registry.RegisterCondition(new(){Id="test.onlyA",Category="Tests",DisplayName="Only A",Evaluate=(i,c)=>c.PickedObject==a});
registry.RegisterCondition(new(){Id="test.onlyB",Category="Tests",DisplayName="Only B",Evaluate=(i,c)=>c.PickedObject==b});
var pick=I("pick.all",("tag",EventValue.String("Targets")),("set",EventValue.String("Union")));var onlyA=I("test.onlyA",("target",EventValue.String("Picked:Union")));var onlyB=I("test.onlyB",("target",EventValue.String("Picked:Union")));var either=I("logic.or");either.ConditionInputIds=[onlyA.InstanceId,onlyB.InstanceId];
module=new();module.Rules.Add(new(){HasExplicitConditionFlow=true,ConnectedConditionIds=[pick.InstanceId,either.InstanceId],Conditions=[pick,either,onlyA,onlyB],Actions=[I("object.setActive",("target",EventValue.String("Picked:Union")),("active",EventValue.Boolean(false)))]});runtime=new(registry);Tick(runtime,module,.1);
Check(!a.Active&&!b.Active,"OR branches union independently filtered named selections");
a.Active=b.Active=true;var inverse=I("logic.not");inverse.ConditionInputIds=[onlyA.InstanceId];module.Rules[0].ConnectedConditionIds=[pick.InstanceId,inverse.InstanceId];module.Rules[0].Conditions=[pick,inverse,onlyA];runtime=new(registry);Tick(runtime,module,.1);
Check(a.Active&&!b.Active,"NOT preserves the complement of an object selection");
root=Path.Combine(Path.GetTempPath(),"ByteCreatorSlots-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
try
{
    using var database=new ByteEngine.Core.Assets.AssetDatabase(root,["Assets"]);using var assets=new ByteEngine.Core.Assets.AssetManager(database);
    var serializer=new ByteEngine.Core.Serialization.SceneSerializer(new ByteEngine.Core.Serialization.ComponentSerializer(root,database,assets));
    typeof(Scene).GetProperty("ProjectRoot")!.SetValue(scene,root);typeof(Scene).GetProperty("SerializationContext",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(scene,serializer);
    owner.Transform.WorldPosition=new(1,2,3);globals.Set("Nested",data.Clone());
    void CompleteSave(){var end=DateTime.UtcNow.AddSeconds(5);while(Condition(I("save.pending"))&&DateTime.UtcNow<end){Thread.Sleep(10);services.Advance(.01,[]);}Check(Condition(I("save.succeeded")),"Save slot operation completes through a readable success state");}
    Action(I("save.writeSlot"));CompleteSave();owner.Transform.WorldPosition=Vector3.Zero;globals.Clear();Action(I("save.loadSlot"));CompleteSave();
    var restore=typeof(Scene).GetProperty("PendingRestore",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(scene)!;
    var restored=(Scene)restore.GetType().GetField("Item1")!.GetValue(restore)!;var restoredGlobals=(VariableStore)restore.GetType().GetField("Item2")!.GetValue(restore)!;
    Check(restored.FindGameObject(owner.Id)!.Transform.WorldPosition==new Vector3(1,2,3)&&restoredGlobals["Nested"].Children["items"].Items[2].Type==VariableType.Null,"Save slots restore scene transforms and nested global values");
    bool rejected=false;try{Action(I("save.writeSlot",("slot",EventValue.String("../escape"))));}catch(ArgumentException){rejected=true;}Check(rejected,"Save slot names cannot escape their namespace");
    typeof(Scene).GetProperty("PendingRestore",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(scene,null);
    Action(I("save.loadSlot",("slot",EventValue.String("Missing"))));var failedEnd=DateTime.UtcNow.AddSeconds(5);
    while(Condition(I("save.pending",("slot",EventValue.String("Missing"))))&&DateTime.UtcNow<failedEnd){Thread.Sleep(10);services.Advance(.01,[]);}
    Check(Condition(I("save.failed",("slot",EventValue.String("Missing"))))&&typeof(Scene).GetProperty("PendingRestore",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(scene)==null,"Missing save reports failure without replacing the live scene");
}
finally{Directory.Delete(root,true);}
var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);listener.Start();int port=((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
var response=Task.Run(async()=>{using var client=await listener.AcceptTcpClientAsync();var stream=client.GetStream();using var reader=new StreamReader(stream,System.Text.Encoding.ASCII,leaveOpen:true);while(!string.IsNullOrEmpty(await reader.ReadLineAsync())){}await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK"));});
try
{
    services.SendRequest("local","http://127.0.0.1:"+port,"GET","","application/json");var timeout=DateTime.UtcNow.AddSeconds(5);
    while(services.GetRequest("local")!.Result==null&&DateTime.UtcNow<timeout){await Task.Delay(10);services.Advance(.01,[]);}
    Check(services.GetRequest("local")!.Result is {Succeeded:true,Body:"OK"},"HTTP completes asynchronously and publishes response on the update thread");await response;
    services.SendRequest("invalid","http://127.0.0.1:"+port,"POST","body","bad/type;broken");timeout=DateTime.UtcNow.AddSeconds(2);
    while(services.GetRequest("invalid")!.Result==null&&DateTime.UtcNow<timeout){await Task.Delay(10);services.Advance(.01,[]);}
    Check(services.GetRequest("invalid")!.Result is {Succeeded:false},"Malformed HTTP content type becomes a readable failure");
}
finally{listener.Stop();}
string wavePath=Path.Combine(Path.GetTempPath(),"ByteSeek-"+Guid.NewGuid().ToString("N")+".wav");
try
{
    using(var wave=new BinaryWriter(File.Create(wavePath)))
    {wave.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));wave.Write(36+16000);wave.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));wave.Write(16);wave.Write((short)1);wave.Write((short)1);wave.Write(8000);wave.Write(16000);wave.Write((short)2);wave.Write((short)16);wave.Write(System.Text.Encoding.ASCII.GetBytes("data"));wave.Write(16000);for(short sample=0;sample<8000;sample++)wave.Write(sample);}
    using(var reader=new ByteEngine.Core.Audio.StreamingPcmReader(wavePath)){reader.Seek(.5);var samples=new short[8];reader.Read(samples);Check(samples[0]==4000,"Streaming audio seek is aligned to the requested PCM frame");reader.Rewind();reader.Read(samples);Check(samples[0]==0,"Streaming audio rewind returns to the payload start");}
    var backend=new TestAudio();ByteEngine.Core.Audio.PortableAudio.Backend=backend;
    ByteEngine.Core.Audio.AudioClip.TryLoadWave(wavePath,out var clip);var source=owner.AddComponent(new ByteEngine.Core.Audio.AudioSource3D());source.SetClip(clip);source.Play();source.Seek(.5);source.Pause();
    Check(source.IsPaused&&Math.Abs(source.PlaybackPosition-.5)<.001,"Audio events can inspect pause and seek position through a portable backend");source.Play();backend.Playing=false;
    Check(source.HasFinished,"Natural audio completion remains observable");source.Stop();Check(!source.HasFinished&&source.IsStopped,"Explicit stop is not reported as natural completion");source.SetClip(null);
}
finally{ByteEngine.Core.Audio.PortableAudio.Backend=null;File.Delete(wavePath);}
Action(I("audio.setBusVolume",("bus",EventValue.String("Music")),("volume",EventValue.Number(.4))));
Check(Math.Abs(ByteEngine.Core.Audio.AudioMixer.GetVolume(ByteEngine.Core.Audio.AudioBus.Music)-.4)<.001,"Audio bus controls adjust the native mixer");Action(I("audio.setBusVolume",("bus",EventValue.String("Music")),("volume",EventValue.Number(1))));
var sharedMaterial=new ByteEngine.Core.Graphics.ThreeD.Material();var mesh=owner.AddComponent(new ByteEngine.Core.Graphics.ThreeD.MeshRenderer{Material=sharedMaterial});
Action(I("renderer.setOpacity",("opacity",EventValue.Number(.5))));Check(mesh.MaterialOverrides.Opacity==.5f&&sharedMaterial.BaseColor.W==1,"Opacity overrides leave the shared material unchanged");
Action(I("tween.opacity",("end",EventValue.Number(0)),("seconds",EventValue.Number(1))));services.Advance(1,[]);Check(mesh.MaterialOverrides.Opacity==0,"Opacity tween reaches the requested alpha");
services.Dispose();Console.WriteLine($"{count} checks passed");

sealed class TestAudio : ByteEngine.Core.Audio.IPortableAudio
{
    public bool Playing,Paused;private double _position;
    public bool IsPlaying(Guid id)=>Playing;public bool IsPaused(Guid id)=>Paused;public double Position(Guid id)=>_position;
    public void Seek(Guid id,double seconds)=>_position=seconds;
    public void Play(Guid id,ByteEngine.Core.Audio.AudioClip clip,ByteEngine.Core.Audio.AudioSource3D source){Playing=true;Paused=false;}
    public void Pause(Guid id){Playing=false;Paused=true;}
    public void Stop(Guid id){Playing=Paused=false;_position=0;}
    public void Update(Guid id,ByteEngine.Core.Audio.AudioSource3D source){}
    public void Listener(Vector3 position,Vector3 forward,Vector3 up,float volume){}
}


