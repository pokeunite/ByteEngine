using System.Net;
using System.Net.Sockets;
using System.Text;
using ByteEngine.Core.Net;

internal static class LobbyRecoveryChecks
{
 public static async Task Run()
 {
  var portProbe=new TcpListener(IPAddress.Loopback,0);portProbe.Start();int port=((IPEndPoint)portProbe.LocalEndpoint).Port;portProbe.Stop();
  using var server=new HttpListener();server.Prefixes.Add($"http://localhost:{port}/");server.Start();
  int polls=0,creates=0,leaves=0,lists=0;
  var serving=Task.Run(async()=>{while(server.IsListening){HttpListenerContext context;try{context=await server.GetContextAsync();}catch(HttpListenerException){break;}catch(ObjectDisposedException){break;}
   string route=context.Request.Url!.AbsolutePath,body="[]";
   using var input=new StreamReader(context.Request.InputStream);await input.ReadToEndAsync();
   if(route=="/poll"){Interlocked.Increment(ref polls);await Task.Delay(100);}
   else if(route=="/leave"){Interlocked.Increment(ref leaves);body="{}";}
   else if(route=="/join"){context.Response.StatusCode=404;body="{\"error\":\"Match no longer exists\"}";}
   else if(route=="/rooms"&&context.Request.HttpMethod=="POST"){int id=Interlocked.Increment(ref creates);body=$"{{\"id\":\"ROOM{id}\"}}";}
   else if(route=="/rooms"){Interlocked.Increment(ref lists);await Task.Delay(100);body="[{\"id\":\"OTHER\",\"name\":\"Arena\",\"players\":1}]";}
   var bytes=Encoding.UTF8.GetBytes(body);context.Response.ContentType="application/json";context.Response.ContentLength64=bytes.Length;await context.Response.OutputStream.WriteAsync(bytes);context.Response.Close();
  }});
  using var client=new NetworkLobbyClient($"http://localhost:{port}");
  async Task Until(Func<bool> condition,string label){var end=DateTime.UtcNow.AddSeconds(5);while(!condition()&&DateTime.UtcNow<end){client.Poll(.02);await Task.Delay(5);}if(!condition())throw new Exception("Lobby regression: "+label+"; "+NetworkDiagnostics.LastError);}
  NetworkDiagnostics.ClearError();
  client.Refresh();client.Poll(.2);client.Host("Arena");client.Host("ignored repeat");client.Refresh();
  await Until(()=>client.RoomCode=="ROOM1"&&!client.IsOperationPending,"host queued behind list request");
  await Until(()=>Volatile.Read(ref polls)>0,"background poll started");
  int listBefore=lists;client.Refresh();client.Refresh();
  await Until(()=>lists>listBefore,"refresh queued behind poll");
  client.Host("Arena again");
  await Until(()=>client.RoomCode=="ROOM2"&&!client.IsOperationPending,"leave callback preserves nested host completion");
  if(creates!=2||leaves!=1)throw new Exception("Lobby duplicate request or missing leave");
  client.Join("MISSING");
  await Until(()=>!client.IsOperationPending&&NetworkDiagnostics.LastError.Contains("Match no longer exists"),"failed join releases operation");
  NetworkDiagnostics.ClearError();client.Host("Recovered");
  await Until(()=>client.RoomCode=="ROOM3"&&!client.IsOperationPending,"host succeeds after failed join");
  if(NetworkDiagnostics.LastError.Length!=0)throw new Exception(NetworkDiagnostics.LastError);
  client.Dispose();await Task.Delay(200);server.Stop();await serving;
  Console.WriteLine("PASS lobby recovery: queued Host/Refresh during HTTP requests, repeated clicks coalesced, nested leave/create completion, missing-room error and retry.");
 }
}
