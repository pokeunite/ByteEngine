using ByteEngine.Core.Net;
using ByteEngine.Network.Native;
using System.Diagnostics;
if(args.Contains("--validate-demo")){DemoFactory.Validate(args[Array.IndexOf(args,"--validate-demo")+1]);return;}
if(args.Contains("--create-demo")){DemoFactory.Create(args[Array.IndexOf(args,"--create-demo")+1]);return;}
if(args.Contains("--session")){
 bool host=args.Contains("--host");string sceneId=args[Array.IndexOf(args,"--scene")+1];
 var compatibility=System.Text.Json.JsonSerializer.Deserialize<NetworkCompatibility>(args[Array.IndexOf(args,"--compat")+1])!;
 if(compatibility.Engine!=NetworkBuildIdentity.Value+":1")throw new Exception("Core source fingerprint mismatch");
 using var link=new WebRtcNetworkLink();using var session=new NetworkSession(host,compatibility);
 for(int i=0;i<4;i++)session.SetSpawnPosition(i,new(i*2-3,0,0));bool moving=true;
 session.ReadInput=()=>new(0,0,moving?new(1,0):default,default,0);
 var lines=new System.Collections.Concurrent.ConcurrentQueue<string>();_=Task.Run(()=>{string? line;while((line=Console.ReadLine())!=null)lines.Enqueue(line);});
 link.Signal+=s=>Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{type="signal",signal=s}));
 session.SessionEnded+=r=>Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{type="ended",reason=r}));
 session.AddLink(link,host);var timer=Stopwatch.StartNew();double last=0,next=0;
 while(timer.Elapsed.TotalSeconds<120){while(lines.TryDequeue(out var line)){if(line=="quit")return;if(line=="stopinput"){moving=false;continue;}link.Accept(System.Text.Json.JsonSerializer.Deserialize<NetworkSignal>(line));}double now=timer.Elapsed.TotalSeconds;session.Update(now-last);last=now;if(now>=next){next=now+.2;Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{type="state",peer=session.LocalPeerId,engine=NetworkBuildIdentity.Value,players=session.Players.Values.Select(p=>new{id=p.PeerId,x=p.Position.X,y=p.Position.Y,z=p.Position.Z})}));}Thread.Sleep(2);}
 throw new Exception("Session bridge timeout");
}
if(args.Contains("--bridge")) {
    using var link=new WebRtcNetworkLink();
    var lines=new System.Collections.Concurrent.ConcurrentQueue<string>();
    _=Task.Run(()=>{string? line;while((line=Console.ReadLine())!=null)lines.Enqueue(line);});
    link.Signal+=s=>Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {type="signal",signal=s}));
    link.Message+=(channel,data)=>{Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {type="received",channel=(int)channel,bytes=data.Length}));link.Send(channel,data);};
    link.Start(args.Contains("--offer"));var timeout=Stopwatch.StartNew();
    while(timeout.Elapsed.TotalSeconds<45){while(lines.TryDequeue(out var line)){if(line=="quit")return;link.Accept(System.Text.Json.JsonSerializer.Deserialize<NetworkSignal>(line));}link.Poll();Thread.Sleep(2);}
    throw new Exception("Bridge timeout");
}
for(int round=0;round<3;round++){
using var a=new WebRtcNetworkLink();using var b=new WebRtcNetworkLink();
a.Signal+=b.Accept;b.Signal+=a.Accept;
int received=0; b.Message+=(channel,data)=>{if(data.Length!=36 || data[0]!=42)throw new Exception("Corrupt message");received++;};
b.Start(false);a.Start(true);var clock=Stopwatch.StartNew();
while(!a.IsConnected || !b.IsConnected){a.Poll();b.Poll();if(clock.Elapsed.TotalSeconds>20)throw new Exception("WebRTC timeout");Thread.Sleep(5);}
byte[] packet=new byte[36];packet[0]=42;
if(!a.Send(NetworkChannel.State,packet)||!a.Send(NetworkChannel.Events,packet))throw new Exception("Send failed");
while(received<2){a.Poll();b.Poll();if(clock.Elapsed.TotalSeconds>25)throw new Exception("Receive timeout");Thread.Sleep(5);}
Console.WriteLine("PASS actual native WebRTC: ICE, DTLS, SCTP, reliable and unreliable binary data channels.");








}
