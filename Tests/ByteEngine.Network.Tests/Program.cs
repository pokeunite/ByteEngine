using System.Numerics;
using ByteEngine.Core.Net;
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
byte[] packet = new byte[NetworkInputCodec.PacketSize];
var expected = new PlayerInput(123, uint.MaxValue, new(.6f,.8f), new(1,-.4f), 0x12345678);
Check(NetworkInputCodec.TryWrite(expected, packet), "encode");
Check(NetworkInputCodec.TryRead(packet, out var actual) && actual == expected, "round trip");
Check(packet[0] == (byte)'B' && packet[1] == (byte)'E' && packet[2] == (byte)'N' && packet[3] == (byte)'1', "wire magic");
for(int length=0; length<packet.Length; length++) Check(!NetworkInputCodec.TryRead(packet.AsSpan(0,length), out _), "truncated packet");
Check(!NetworkInputCodec.TryRead(new byte[100], out _), "oversized packet");
Check(!NetworkInputCodec.TryWrite(expected with {Move=new(2,0)},packet), "speed input bounds");
Check(!NetworkInputCodec.TryWrite(expected with {Look=new(float.NaN,0)},packet), "NaN rejected");
Check(!NetworkInputCodec.TryWrite(expected with {Look=new(0,float.PositiveInfinity)},packet), "infinity rejected");
NetworkInputCodec.TryWrite(expected,packet); packet[4]=2;
Check(!NetworkInputCodec.TryRead(packet,out _), "version rejected");
NetworkInputCodec.TryWrite(expected,packet); packet[7]=1;
Check(!NetworkInputCodec.TryRead(packet,out _), "reserved flags rejected");
Check(NetworkInputCodec.IsNewer(0,uint.MaxValue) && !NetworkInputCodec.IsNewer(uint.MaxValue,0) && !NetworkInputCodec.IsNewer(5,5), "sequence wrap");
for(int i=0;i<10000;i++) { NetworkInputCodec.TryWrite(expected,packet); NetworkInputCodec.TryRead(packet,out _); }
long before=GC.GetAllocatedBytesForCurrentThread();
for(int i=0;i<10000;i++) { NetworkInputCodec.TryWrite(expected,packet); NetworkInputCodec.TryRead(packet,out _); }
Check(GC.GetAllocatedBytesForCurrentThread()==before,"steady-state codec allocations");
Console.WriteLine("PASS network input codec: roundtrip, wire identity, malformed input, version, wrap and zero steady-state allocations.");

static void SessionChecks()
{
 var compatibility=new NetworkCompatibility("engine-v1","plugins-v1","scene-v1");
 var (a,b)=LoopbackNetworkLink.Pair();a.Loss=.2;b.Loss=.2;a.Latency=b.Latency=.03;a.Jitter=b.Jitter=.02;
 using var host=new NetworkSession(true,compatibility);using var client=new NetworkSession(false,compatibility);
 client.ReadInput=()=>new(0,0,new(1,0),Vector2.Zero,0);
 host.AddLink(a,true);client.AddLink(b,false);
 for(int i=0;i<600;i++){a.Advance(1d/60);b.Advance(1d/60);host.Update(1d/60);client.Update(1d/60);}
 Check(client.LocalPeerId==1 && host.Players.Count==2 && client.Players.Count==2,"handshake and registry");
 Check(host.Players[1].Position.X>40 && host.Players[1].Position.X<=60,"host movement bounds under loss");
 Check(Vector3.Distance(host.Players[1].Position,client.PredictedLocal.Position)<2,"prediction/reconciliation under loss");
 client.ReadInput=()=>default;
 for(int i=0;i<180;i++){a.Advance(1d/60);b.Advance(1d/60);host.Update(1d/60);client.Update(1d/60);}
 Check(Vector3.Distance(host.Players[1].Position,client.PredictedLocal.Position)<.01,"stationary convergence below1cm");
 int events=0,intents=0;client.EventReceived+=(kind,data)=>{if(kind!=42||data.Length!=40000)throw new Exception("event corrupt");events++;};host.IntentReceived+=(peer,kind,data)=>{if(peer!=1)throw new Exception("intent owner");intents++;};
 host.BroadcastEvent(42,new byte[40000]);a.Advance(.2);b.Advance(.2);client.Update(.01);Check(events==1,"host-only large authoritative event");
 for(int i=0;i<64;i++)client.SendIntent(1,new byte[1]);a.Advance(.2);host.Update(.01);Check(intents==32,"client intent rate limit");
 bool forbidden=false;try{client.BroadcastEvent(1,[]);}catch(InvalidOperationException){forbidden=true;}Check(forbidden,"client cannot publish authoritative event");
 bool ended=false;client.SessionEnded+=reason=>ended=reason=="host left";host.Dispose();client.Update(.01);Check(ended,"host departure");
 foreach(var bad in new[]{new NetworkCompatibility("wrong","plugins-v1","scene-v1"),new("engine-v1","wrong","scene-v1"),new("engine-v1","plugins-v1","wrong")}){
  var(x,y)=LoopbackNetworkLink.Pair();using var h=new NetworkSession(true,compatibility);using var c=new NetworkSession(false,bad);string reason="";c.SessionEnded+=r=>reason=r;h.AddLink(x,true);c.AddLink(y,false);
  for(int i=0;i<10;i++){h.Update(.02);c.Update(.02);}Check(reason.EndsWith("mismatch"),"compatibility rejection");
 }
 var(u,v)=LoopbackNetworkLink.Pair();u.Start(true);v.Start(false);byte[] payload=new byte[40000];payload[39999]=7;bool large=false;v.Message+=(ch,data)=>large=data.Length==40000&&data[39999]==7;
 Check(u.Send(NetworkChannel.Events,payload),"40KB reliable event");v.Poll();Check(large,"40KB delivery");
 Check(!u.Send(NetworkChannel.State,payload),"state MTU");for(int i=0;i<64;i++)Check(u.Send(NetworkChannel.State,new byte[1]),"bounded enqueue");Check(!u.Send(NetworkChannel.Events,new byte[1]),"backpressure");u.Dispose();v.Dispose();
 Console.WriteLine("PASS session: lossy/jittery authoritative movement, prediction, 1cm convergence, fingerprints, host leave, 40KB reliable event, MTU and backpressure.");
}
SessionChecks();


static void LateJoinChecks(){
 var compat=new NetworkCompatibility("a","b","c");using var host=new NetworkSession(true,compat);
 var clients=new List<NetworkSession>();var pairs=new List<(LoopbackNetworkLink A,LoopbackNetworkLink B)>();
 void Join(){var pair=LoopbackNetworkLink.Pair(50+clients.Count);var client=new NetworkSession(false,compat);pairs.Add(pair);clients.Add(client);host.AddLink(pair.A,true);client.AddLink(pair.B,false);}
 void Advance(int frames){for(int n=0;n<frames;n++){foreach(var pair in pairs){pair.A.Advance(1d/60);pair.B.Advance(1d/60);}host.Update(1d/60);foreach(var client in clients)if(client.IsActive)client.Update(1d/60);}}
 Join();Advance(60);Join();Advance(60);Join();Advance(60);
 Check(host.Players.Count==4&&clients.All(c=>c.Players.Count==4),"late join baseline includes all peers");Check(host.SentBytes<60000,"three-second avatar payload bandwidth budget");
 bool cap=false;var extra=LoopbackNetworkLink.Pair();try{host.AddLink(extra.A,true);}catch(InvalidOperationException){cap=true;}finally{extra.A.Dispose();extra.B.Dispose();}Check(cap,"peer cap");
 clients[0].Dispose();Advance(10);Check(host.Players.Count==3&&clients[1].Players.Count==3,"client departure replicates");
 foreach(var client in clients)client.Dispose();Console.WriteLine("PASS four-peer session: late joins, complete baseline, peer cap and departure replication.");
}
LateJoinChecks();
