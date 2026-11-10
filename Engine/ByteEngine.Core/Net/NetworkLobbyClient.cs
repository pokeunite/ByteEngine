using System.Text;
using System.Text.Json;
namespace ByteEngine.Core.Net;
public static class NetworkDiagnostics
{
 static readonly Queue<string> lines=new();static readonly object gate=new();
 public static string LastError {get;private set;}="";
 public static Action<string,string>? Download {get;set;}
 public static void Record(string message,bool error=false){lock(gate){if(lines.Count>=256)lines.Dequeue();lines.Enqueue(DateTime.UtcNow.ToString("O")+" "+message);if(error)LastError=message;}}
 public static string Export(){lock(gate)return "ByteEngine multiplayer diagnostics\nSource: "+NetworkBuildIdentity.Value+"\n"+string.Join("\n",lines);}
 public static void Save(){string name="ByteEngine-network-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".log";if(Download!=null){Download(name,Export());return;}string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");Directory.CreateDirectory(folder);string file=Path.Combine(folder,name);File.WriteAllText(file,Export());Record("Saved log: "+file);}
}
public sealed record LobbyRoom(string Id,string Name,int Players);
public sealed class NetworkLobbyClient : IDisposable
{
 readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(8)};readonly string address;readonly string token=Guid.NewGuid().ToString("N");
 readonly Dictionary<int,string> destinations=[];readonly Dictionary<string,int> connections=[];Task<string>? request;Action<string>? done;double timer;bool host,disposed;string room="";
 public IReadOnlyList<LobbyRoom> Rooms {get;private set;}=[];public string Status {get;private set;}="Ready";
 public string RoomCode=>room;
 public NetworkLobbyClient(string address){this.address=address.TrimEnd('/');NetworkRuntime.Signal+=Signal;}
 void Call(string route,object? body,Action<string> complete){if(request!=null)throw new InvalidOperationException("Lobby request is busy");done=complete;request=Fetch(route,body);}
 async Task<string> Fetch(string route,object? body){using var response=body==null?await http.GetAsync(address+route):await http.PostAsync(address+route,new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json"));response.EnsureSuccessStatusCode();return await response.Content.ReadAsStringAsync();}
 public void Refresh()=>Call("/rooms",null,text=>Rooms=JsonSerializer.Deserialize<LobbyRoom[]>(text,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??[]);
 public void Host(string name){host=true;Call("/rooms",new{token,name},text=>{room=JsonDocument.Parse(text).RootElement.GetProperty("id").GetString()!;Status="Hosting "+room;NetworkDiagnostics.Record(Status);});}
 public void Join(string id){host=false;Call("/join",new{token,id},text=>{room=id;string remote=JsonDocument.Parse(text).RootElement.GetProperty("host").GetString()!;int connection=NetworkRuntime.OpenConnection(false);destinations[connection]=remote;connections[remote]=connection;Status="Joining "+room;});}
 readonly Queue<(string Destination,NetworkSignal Signal)> signals=new();
 void Signal(int connection,NetworkSignal signal){if(destinations.TryGetValue(connection,out var destination)){if(signals.Count>=128){NetworkDiagnostics.Record("Signaling queue overflow",true);return;}signals.Enqueue((destination,signal));}}
 public void Poll(double delta){if(disposed)return;timer+=delta;if(request is {IsCompleted:true}){var task=request;request=null;try{string result=task.GetAwaiter().GetResult();done?.Invoke(result);}catch(Exception e){Status=e.Message;NetworkDiagnostics.Record("Lobby: "+e.Message,true);}done=null;}
  if(request!=null||room.Length==0||timer<.1)return;timer=0;
  if(signals.TryDequeue(out var signal)){Call("/signal",new{token,to=signal.Destination,signal=signal.Signal},_=>{});return;}
  Call("/poll?token="+token,null,text=>{using var json=JsonDocument.Parse(text);foreach(var message in json.RootElement.EnumerateArray()){
    string kind=message.GetProperty("kind").GetString()!,from=message.GetProperty("from").GetString()!;
    if(kind=="join"&&host){int c=NetworkRuntime.OpenConnection(true);connections[from]=c;destinations[c]=from;}
    else if(kind=="signal"&&connections.TryGetValue(from,out int connection)){var signal=message.GetProperty("signal").Deserialize<NetworkSignal>();NetworkRuntime.Accept(connection,signal);}
    else if(kind=="left"&&connections.Remove(from,out int c)){NetworkRuntime.CloseConnection(c);destinations.Remove(c);}
  }});
 }
 public void Dispose(){if(disposed)return;disposed=true;NetworkRuntime.Signal-=Signal;try{_ = http.PostAsync(address+"/leave",new StringContent(JsonSerializer.Serialize(new{token}),Encoding.UTF8,"application/json"));}catch{}request=null;}
}
