using System.Numerics;
using System.Buffers.Binary;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Net;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Physics;
using ByteEngine.Core.VisualLogic;
namespace KarlsonDemo.Plugin;
/// <summary>Configurable host-authoritative free-for-all using authored avatars and existing weapon definitions.</summary>
public sealed class PvpMatch3D:Component
{
 public string LobbyAddress {get;set;}="http://127.0.0.1:8099";
 public string MatchName {get;set;}="PvP Arena";
 public AssetReference Font {get;set;}=AssetReference.Empty;
 public float WalkSpeed {get;set;}=4.2f;public float SprintSpeed {get;set;}=6;
 public float Gravity {get;set;}=20;public float JumpSpeed {get;set;}=7;
 public float MaximumHealth {get;set;}=100;public float RespawnSeconds {get;set;}=3;
 public int ScoreToWin {get;set;}=10;public float RoundSeconds {get;set;}=300;
 public float LookSensitivity {get;set;}=.12f;public float CapsuleRadius {get;set;}=.3f;public float CapsuleHeight {get;set;}=2;
 public bool ShowMenuOnStart {get;set;}=true;
 public string Status {get;private set;}="Choose Host or Find Matches";public bool MenuOpen {get;private set;}
 public int Winner {get;private set;}=-1;public float RemainingSeconds {get;private set;}
 readonly GameObject?[] actors=new GameObject?[4];readonly FpsGunLab3D?[] guns=new FpsGunLab3D?[4];readonly Vector3[] spawns=new Vector3[4];
 readonly Vector2[] looks=new Vector2[4];readonly int[] kills=new int[4],deaths=new int[4],slots=new int[4];readonly float[] deadFor=new float[4];readonly uint[] previousButtons=new uint[4];
 readonly HashSet<WeaponInstance3D> observed=[];
 readonly Dictionary<Component,bool> visibility=[];NetworkLobbyClient? lobby;NetworkSession? bound;int local=-1,selected;float yaw,pitch,timer;uint queuedButtons;bool initialized;string shownError="";
 readonly List<(string Text,Vector2 P,Vector2 S,Action Action)> buttons=[];
 public override int UpdateOrder=>-9000;public override int? RenderOrder=>6000;
 protected override void OnStart(){Initialize();MenuOpen=ShowMenuOnStart;RemainingSeconds=RoundSeconds;Input.RegisterGameplayEscapeHandler(this,true);lobby=new(LobbyAddress);NetworkDiagnostics.Record("Match scene loaded: "+GameObject.Scene!.Name);}
 void Initialize(){if(initialized)return;initialized=true;foreach(var o in GameObject.Scene!.GameObjects)if(o.GetComponent<NetworkIdentity>() is {} identity&&identity.PeerId is >=0 and <4){int id=identity.PeerId;actors[id]=o;spawns[id]=o.Transform.WorldPosition;guns[id]=o.GetComponent<FpsGunLab3D>();var hp=o.GetComponent<HealthComponent>();if(hp!=null){hp.MaxHealth=MaximumHealth;hp.Revive();hp.DestroyOnDeath=false;}
 foreach(var c in o.Components)if(c is PlayerController3D or TopDownController3D or CharacterController3D or EventModuleComponent or MiningThirdPerson3D)c.Enabled=false;
 if(o.GetComponent<CameraBoom3D>() is {} boom){boom.UseControlRotation=false;boom.Enabled=false;}
 if(guns[id]!=null)guns[id]!.UseBuiltInFireInput=false;}
 foreach(var canvas in GameObject.Scene.GameObjects.Where(o=>o.Name=="HUD canvas"))canvas.Active=false;
 }
 static IEnumerable<GameObject> Self(GameObject root){yield return root;foreach(var child in root.Children)foreach(var item in Self(child))yield return item;}
 public void HostMatch(){try{Start(true);lobby?.Host(MatchName);MenuOpen=false;Status="Hosting — waiting for players";}catch(Exception e){Fail(e);}}
 public void FindMatches(){try{lobby?.Refresh();Status="Finding matches";}catch(Exception e){Fail(e);}}
 public void JoinMatch(string code){try{Start(false);lobby?.Join(code);MenuOpen=false;Status="Joining "+code;}catch(Exception e){Fail(e);}}
 void Start(bool host){Initialize();NetworkRuntime.InputProvider=ReadInput;GameObject.GetComponent<NetworkSettings>()!.StartSession(host);bound=NetworkRuntime.Session!;bound.SimulatePlayer=Simulate;bound.AuthoritativeInput+=Authoritative;bound.EventReceived+=Receive;bound.SessionEnded+=reason=>{Status=reason;MenuOpen=true;NetworkDiagnostics.Record(reason,true);};bound.PeerJoined+=id=>NetworkDiagnostics.Record("Peer joined: "+id);bound.PeerLeft+=id=>NetworkDiagnostics.Record("Peer left: "+id);RemainingSeconds=RoundSeconds;Winner=-1;Array.Clear(kills);Array.Clear(deaths);Array.Clear(deadFor);Array.Clear(previousButtons);}
 PlayerInput ReadInput(){if(MenuOpen||local>=0&&deadFor[local]>0||Winner>=0)return new(0,0,Vector2.Zero,new(yaw,pitch),0);var movement=new Vector2((Input.IsKeyDown(Key.D)?1:0)-(Input.IsKeyDown(Key.A)?1:0),(Input.IsKeyDown(Key.S)?1:0)-(Input.IsKeyDown(Key.W)?1:0));if(movement.LengthSquared()>1)movement=Vector2.Normalize(movement);
 if(Input.IsKeyPressed(Key.D1))selected=0;if(Input.IsKeyPressed(Key.D2))selected=1;if(Input.IsKeyPressed(Key.D3))selected=2;
 uint latched=queuedButtons;queuedButtons=0;uint bits=latched|(Input.IsKeyDown(Key.Space)?1u:0)|(Input.IsMouseButtonDown(MouseButton.Left)?2u:0)|(Input.IsKeyDown(Key.R)?4u:0)|(Input.IsKeyDown(Key.LeftShift)?8u:0)|(Input.IsMouseButtonDown(MouseButton.Right)?16u:0)|((uint)selected<<5);return new(0,0,movement,new(yaw,pitch),bits);}
 NetworkPlayerState Simulate(NetworkPlayerState state,PlayerInput input,float dt){int id=state.PeerId;if(deadFor[id]>0||Winner>=0)return state with{Velocity=Vector3.Zero,AcknowledgedInput=input.Sequence};var move=CameraBoom3D.CalculateCameraRight(input.Look.X*180/MathF.PI)*input.Move.X-PlayerController3D.ForwardFromYaw(input.Look.X*180/MathF.PI)*input.Move.Y;float speed=(input.Buttons&8)!=0?SprintSpeed:WalkSpeed;var velocity=move*speed;velocity.Y=state.Velocity.Y-Gravity*dt;
 bool grounded=Grounded(id,state.Position);if(grounded){velocity.Y=Math.Max(velocity.Y,0);if((input.Buttons&1)!=0)velocity.Y=JumpSpeed;}
 Vector3 position=Constrain(id,state.Position,state.Position+velocity*dt);if(Math.Abs(position.Y-state.Position.Y)<.0001f&&velocity.Y<0)velocity.Y=0;looks[id]=input.Look;return state with{Position=position,Velocity=velocity,AcknowledgedInput=input.Sequence};}
 bool Grounded(int id,Vector3 position)=>GameplayQuery3D.Raycast(GameObject.Scene!,position+Vector3.UnitY*.12f,-Vector3.UnitY,out _, .22f,actors[id],source:actors[id],includeTriggers:false);
 Vector3 Constrain(int id,Vector3 from,Vector3 to){var result=from;Vector3[] parts=[new(to.X-from.X,0,to.Z-from.Z),new(0,to.Y-from.Y,0)];foreach(var delta in parts){float length=delta.Length();if(length<.00001f)continue;var direction=delta/length;float distance=length;if(GameplayQuery3D.CapsuleCast(GameObject.Scene!,result+Vector3.UnitY*(CapsuleRadius+.03f),result+Vector3.UnitY*(CapsuleHeight-CapsuleRadius),direction,CapsuleRadius,out var hit,length+.015f,ignore:actors[id],source:actors[id],includeTriggers:false))distance=Math.Clamp(hit.Distance-.015f,0,length);result+=direction*distance;}return result;}
 void Authoritative(int id,PlayerInput input){if(actors[id]==null||guns[id] is not {} gun||deadFor[id]>0||Winner>=0)return;int slot=(int)((input.Buttons>>5)&3);if(slot<gun.WeaponCount)gun.SelectInventoryIndex(slot);slots[id]=gun.SelectedInventoryIndex;
 if((input.Buttons&4)!=0&&(previousButtons[id]&4)==0)gun.Reload();
 bool trigger=(input.Buttons&2)!=0&&(gun.SelectedWeapon?.Definition?.TriggerMode==WeaponTriggerMode.Automatic||(previousButtons[id]&2)==0);
 if(trigger&&gun.SelectedWeapon is {} weapon){var rotation=Quaternion.CreateFromYawPitchRoll(input.Look.X,input.Look.Y,0);var direction=CameraBoom3D.CalculateViewForward(input.Look.X*180/MathF.PI,input.Look.Y*180/MathF.PI);var origin=actors[id]!.Transform.WorldPosition+Vector3.UnitY*1.4f;if(weapon.Fire(origin,direction,actors[id]!)){Span<byte> packet=stackalloc byte[20];BinaryPrimitives.WriteInt32LittleEndian(packet,id);BinaryPrimitives.WriteInt32LittleEndian(packet[4..],slots[id]);Write(packet[8..],direction);bound!.BroadcastEvent(201,packet);}}
 previousButtons[id]=input.Buttons;}
 protected override void OnUpdate(){Initialize();
 foreach(var gun in guns)if(gun!=null)foreach(var weapon in gun.CarriedWeapons)if(observed.Add(weapon)){var owner=gun.GameObject;weapon.DamageConfirmed+=receipt=>{if(NetworkRuntime.Session?.IsHost==true&&receipt.Killed){int killer=Array.FindIndex(actors,a=>a==owner),victim=Array.FindIndex(actors,a=>a?.Id==receipt.TargetId);if(killer>=0&&victim>=0&&killer!=victim)kills[killer]++;}};}
try{lobby?.Poll(Time.UnscaledDeltaTime);}catch(Exception e){Fail(e);}if(NetworkDiagnostics.LastError.Length>0&&shownError!=NetworkDiagnostics.LastError){shownError=NetworkDiagnostics.LastError;Status=shownError;MenuOpen=true;}bound=NetworkRuntime.Session;local=bound?.LocalPeerId??-1;
 if(Input.IsKeyPressed(Key.Escape))MenuOpen=!MenuOpen;if(!MenuOpen){if(Input.IsKeyPressed(Key.D1))selected=0;if(Input.IsKeyPressed(Key.D2))selected=1;if(Input.IsKeyPressed(Key.D3))selected=2;if(Input.IsKeyPressed(Key.R))queuedButtons|=4;if(Input.IsKeyPressed(Key.Space))queuedButtons|=1;if(Input.IsMouseButtonPressed(MouseButton.Left))queuedButtons|=2;}Input.RequestUiPointer(this,MenuOpen);if(!MenuOpen&&local>=0){yaw=MathF.IEEERemainder(yaw+Input.MouseDelta.X*LookSensitivity*MathF.PI/180,MathF.PI*2);pitch=Math.Clamp(pitch+Input.MouseDelta.Y*LookSensitivity*MathF.PI/180,-1.2f,1.2f);}
 if(bound is {IsActive:true}){foreach(var state in bound.Players.Values){int id=state.PeerId;if(actors[id] is not {} actor)continue;bool owned=id==local;actor.Transform.WorldPosition=owned&&!bound.IsHost?bound.PredictedLocal.Position+bound.PredictionErrorOffset:Vector3.Lerp(actor.Transform.WorldPosition,state.Position,1-MathF.Exp(-20*(float)Time.DeltaTime));actor.Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,-(owned?yaw:looks[id].X));
 if(actor.GetComponent<AnimationController>() is {} animator){animator.ReplicatedVelocity=state.Velocity;animator.ReplicatedGrounded=Grounded(id,state.Position);animator.SetAim(0,-(owned?pitch:looks[id].Y)*180/MathF.PI);}
 if(actor.GetComponent<CameraBoom3D>() is {} boom){boom.Enabled=owned;boom.Yaw=yaw*180/MathF.PI;boom.Pitch=pitch*180/MathF.PI;}
 foreach(var camera in Self(actor).SelectMany(o=>o.Components.OfType<Camera3D>())){camera.Enabled=owned;camera.ActiveGameCamera=owned;}
 }
 if(bound.IsHost){RemainingSeconds=Math.Max(0,RemainingSeconds-(float)Time.DeltaTime);foreach(var state in bound.Players.Values){int id=state.PeerId;var hp=actors[id]?.GetComponent<HealthComponent>();if(hp==null)continue;if(hp.IsDead&&deadFor[id]<=0){deadFor[id]=RespawnSeconds;deaths[id]++;}if(deadFor[id]>0){deadFor[id]-=(float)Time.DeltaTime;if(deadFor[id]<=0){hp.Revive();NetworkDiagnostics.Record("Respawned player "+id);bound.Teleport(id,spawns[id]);foreach(var weapon in guns[id]?.CarriedWeapons??[])weapon.SetAmmo(weapon.Definition!.MagazineCapacity,weapon.Definition.ReserveCapacity);}}
 if(kills[id]>=ScoreToWin&&Winner<0){Winner=id;NetworkDiagnostics.Record("Match winner: "+id);}}
 if(RemainingSeconds<=0&&Winner<0)Winner=bound.Players.Keys.OrderByDescending(id=>kills[id]).First();timer+=(float)Time.DeltaTime;if(timer>=.1f){timer=0;SendState();}}
 }
 foreach(var actor in actors)if(actor!=null){int id=actor.GetComponent<NetworkIdentity>()!.PeerId;bool present=bound is {IsActive:true}&&bound.Players.ContainsKey(id);foreach(var item in Self(actor))foreach(var c in item.Components)if(c is ByteEngine.Core.Graphics.ThreeD.MeshRenderer or ByteEngine.Core.Graphics.ThreeD.SkeletalMeshRenderer or Collider3D){if(!visibility.ContainsKey(c))visibility[c]=c.Enabled;c.Enabled=present&&deadFor[id]<=0&&visibility[c];}}
 if(MenuOpen&&Input.IsMouseButtonPressedForUi(MouseButton.Left)){var point=Input.GameViewMousePosition;foreach(var b in buttons.ToArray())if(point.X>=b.P.X&&point.Y>=b.P.Y&&point.X<b.P.X+b.S.X&&point.Y<b.P.Y+b.S.Y){try{b.Action();}catch(Exception e){Fail(e);}break;}}
 }
 void SendState(){byte[] packet=new byte[12+4*44];BinaryPrimitives.WriteInt32LittleEndian(packet,Winner);BinaryPrimitives.WriteSingleLittleEndian(packet.AsSpan(4),RemainingSeconds);int mask=0;foreach(int id in bound!.Players.Keys)mask|=1<<id;BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(8),mask);
 for(int id=0;id<4;id++){int o=12+44*id;var hp=actors[id]?.GetComponent<HealthComponent>();var gun=guns[id];BinaryPrimitives.WriteSingleLittleEndian(packet.AsSpan(o),hp?.CurrentHealth??0);BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(o+4),slots[id]);BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(o+8),gun?.Ammo??0);BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(o+12),gun?.ReserveAmmo??0);BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(o+16),kills[id]);BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(o+20),deaths[id]);BinaryPrimitives.WriteSingleLittleEndian(packet.AsSpan(o+24),looks[id].X);BinaryPrimitives.WriteSingleLittleEndian(packet.AsSpan(o+28),looks[id].Y);BinaryPrimitives.WriteSingleLittleEndian(packet.AsSpan(o+32),deadFor[id]);BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(o+36),gun?.Reloading==true?1:0);}
 bound.BroadcastEvent(200,packet);}
 void Receive(ushort kind,byte[] bytes){if(kind==201&&bytes.Length==20){int id=BinaryPrimitives.ReadInt32LittleEndian(bytes),slot=BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4));if(id is <0 or >3||guns[id]==null)return;guns[id]!.SelectInventoryIndex(slot);var direction=Read(bytes.AsSpan(8));guns[id]!.SelectedWeapon?.PresentNetworkShot(direction);if(actors[id]?.Scene is {} scene&&guns[id]!.SelectedWeapon?.TryGetMuzzleWorldPosition(out var muzzle)==true)WeaponTrace.Draw(scene,muzzle,muzzle+direction*30,.08f,.025f,new(1,.8f,.3f,1));return;}
 if(kind!=200||bytes.Length!=188)return;Winner=BinaryPrimitives.ReadInt32LittleEndian(bytes);RemainingSeconds=BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(4));for(int id=0;id<4;id++){int o=12+44*id;var hp=actors[id]?.GetComponent<HealthComponent>();if(hp!=null)hp.CurrentHealth=BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(o));slots[id]=BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(o+4));if(guns[id] is {} gun){gun.SelectInventoryIndex(slots[id]);if(gun.SelectedWeapon is {} weapon){weapon.CurrentAmmo=BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(o+8));weapon.ReserveAmmo=BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(o+12));weapon.PresentNetworkReload(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(o+36))!=0);}}
 kills[id]=BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(o+16));deaths[id]=BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(o+20));looks[id]=new(BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(o+24)),BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(o+28)));deadFor[id]=BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(o+32));}}
 protected override void OnRender(RenderContext c){buttons.Clear();float width=c.TargetWidth,height=c.TargetHeight;Vector4 white=new(.94f,.96f,1,1),panel=new(.045f,.06f,.085f,.96f),accent=new(.2f,.7f,1,1);void Text(string value,float x,float y,int size=20)=>c.DrawUiText(value,Font,size,new(x,y),white);
 void Button(string text,float x,float y,Action action){var p=new Vector2(x,y);var s=new Vector2(350,38);c.DrawUiQuad(p,s,new(.12f,.22f,.32f,1));Text(text,x+12,y+8,18);buttons.Add((text,p,s,action));}
 if(MenuOpen||bound is not {IsActive:true}){MenuOpen=true;c.DrawUiQuad(new(width/2-205,35),new(410,Math.Min(height-55,600)),panel);Text(MatchName,width/2-180,55,28);Text("FREE-FOR-ALL • HOST AUTHORITATIVE",width/2-180,95,14);float x=width/2-175;Button("HOST MATCH",x,130,HostMatch);Button("FIND / REFRESH MATCHES",x,176,FindMatches);float y=222;foreach(var room in lobby?.Rooms??[]){var selectedRoom=room;Button(room.Name+"  "+room.Players+"/4  ["+room.Id+"]",x,y,()=>JoinMatch(selectedRoom.Id));y+=44;if(y>height-160)break;}if(bound is {IsActive:true}){Button("RESUME MATCH",x,y,()=>MenuOpen=false);y+=44;}Button("DOWNLOAD ERROR LOG",x,Math.Min(y+8,height-112),NetworkDiagnostics.Save);Text(Status,x,height-66,14);if(NetworkDiagnostics.LastError.Length>0)Text(NetworkDiagnostics.LastError[..Math.Min(52,NetworkDiagnostics.LastError.Length)],x,height-45,12);return;}
 if(local>=0&&actors[local]?.GetComponent<HealthComponent>() is {} hp){c.DrawUiQuad(new(24,height-70),new(230,20),panel);c.DrawUiQuad(new(26,height-68),new(226*hp.HealthPercent,16),new(.2f,.85f,.5f,1));Text($"HP {hp.CurrentHealth:0}/{hp.MaxHealth:0}",30,height-106);Text($"{guns[local]?.WeaponName}   {guns[local]?.Ammo}/{guns[local]?.ReserveAmmo}",width-340,height-70,24);Text("1 2 3  weapons   R reload   RMB aim",width-340,height-38,14);if(deadFor[local]>0)Text($"RESPAWNING {deadFor[local]:0.0}",width/2-120,height/2,26);}
 Text($"{RemainingSeconds:0}s  •  First to {ScoreToWin}  •  ESC menu",24,20,17);float row=48;foreach(int id in bound!.Players.Keys){Text($"Player {id+1}   {kills[id]} kills / {deaths[id]} deaths",24,row,16);row+=24;}c.DrawUiQuad(new(width/2-6,height/2),new(12,2),white);c.DrawUiQuad(new(width/2,height/2-6),new(2,12),white);if(Winner>=0)Text($"PLAYER {Winner+1} WINS",width/2-140,110,32);
 foreach(var state in bound.Players.Values)if(state.PeerId!=local&&actors[state.PeerId]?.GetComponent<HealthComponent>() is {} health&&GameObject.Scene!.ActiveCamera is {} camera){Vector4 clip=Vector4.Transform(new Vector4(state.Position+Vector3.UnitY*2.35f,1),camera.GetRenderViewMatrix()*camera.GetProjectionMatrix(width/height));if(clip.W>0){var point=new Vector2((clip.X/clip.W+1)*width/2,(1-clip.Y/clip.W)*height/2);c.DrawUiQuad(point-new Vector2(35,0),new(70,7),panel);c.DrawUiQuad(point-new Vector2(34,-1),new(68*health.HealthPercent,5),accent);}}
 }
 void Fail(Exception e){Status=e.Message;MenuOpen=true;NetworkDiagnostics.Record(e.ToString(),true);}
 static void Write(Span<byte> bytes,Vector3 v){BinaryPrimitives.WriteSingleLittleEndian(bytes,v.X);BinaryPrimitives.WriteSingleLittleEndian(bytes[4..],v.Y);BinaryPrimitives.WriteSingleLittleEndian(bytes[8..],v.Z);}static Vector3 Read(ReadOnlySpan<byte> b)=>new(BinaryPrimitives.ReadSingleLittleEndian(b),BinaryPrimitives.ReadSingleLittleEndian(b[4..]),BinaryPrimitives.ReadSingleLittleEndian(b[8..]));
 void Cleanup(){lobby?.Dispose();NetworkRuntime.Stop();NetworkRuntime.InputProvider=null;Input.RequestUiPointer(this,false);Input.RegisterGameplayEscapeHandler(this,false);}
 protected override void OnStop()=>Cleanup();protected override void OnDestroy()=>Cleanup();
}
