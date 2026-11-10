using ByteEngine.Core.Net;
using ByteEngine.Core.Scene;
using ImGuiNET;
using System.Text.Json;
namespace ByteEngine.Editor;
internal static class NetworkLobby
{
 static readonly Dictionary<int,List<NetworkSignal>> outgoing=[];
 static int connection=-1;static string incoming="",error="";
 static NetworkLobby(){NetworkRuntime.Signal+=(id,signal)=>{if(!outgoing.TryGetValue(id,out var list))outgoing[id]=list=[];if(list.Count<128)list.Add(signal);};}
 internal static void Draw(ByteEngine.Core.Scene.Scene? scene)
 {
  if(scene?.GameObjects.Any(o=>o.Components.Any(c=>c.GetType().Name=="PvpMatch3D"))==true)return;
  if(!ImGui.BeginMenu("Network"))return;
  try {
   var settings=scene?.GameObjects.Select(o=>o.GetComponent<NetworkSettings>()).FirstOrDefault(s=>s!=null);
   if(settings==null){ImGui.TextDisabled("Play a scene with Network Settings.");ImGui.EndMenu();return;}
   if(ImGui.Button("Host")){settings.StartSession(true);outgoing.Clear();connection=-1;error="";}
   ImGui.SameLine();if(ImGui.Button("Join")){settings.StartSession(false);outgoing.Clear();connection=NetworkRuntime.OpenConnection(false);error="";}
   ImGui.SameLine();if(ImGui.Button("Leave")){NetworkRuntime.Stop();outgoing.Clear();connection=-1;}
   if(ImGui.Button("New invite")){connection=NetworkRuntime.OpenConnection(true);outgoing[connection]=[];}
   foreach(var id in outgoing.Keys.ToArray()){ImGui.SameLine();if(ImGui.SmallButton($"Link {id}"))connection=id;}
   if(ImGui.Button("Copy connection data") && connection>=0){var data=outgoing.GetValueOrDefault(connection)??[];ImGui.SetClipboardText(JsonSerializer.Serialize(data.Select(s=>new {kind=s.Kind,content=s.Content,mid=s.Mid})));}
   ImGui.InputTextMultiline("Incoming data",ref incoming,262144,new(360,100));
   if(ImGui.Button("Apply incoming data")){
    if(connection<0)throw new InvalidOperationException("Join or create an invite first");
    using var json=JsonDocument.Parse(incoming);if(json.RootElement.ValueKind!=JsonValueKind.Array||json.RootElement.GetArrayLength()>128)throw new ArgumentException("Invalid connection data");
    foreach(var item in json.RootElement.EnumerateArray())NetworkRuntime.Accept(connection,new(item.GetProperty("kind").GetString()!,item.GetProperty("content").GetString()!,item.TryGetProperty("mid",out var mid)?mid.GetString()??"":""));incoming="";
   }
   if(NetworkRuntime.Session is {} s)ImGui.Text($"{(s.IsHost?"Host":"Client")} · peer {s.LocalPeerId} · {s.Players.Count} players");
   if(NetworkRuntime.Session is {} stats)ImGui.Text($"NET: {stats.SentBytes/1024f:0.0} KB sent · {stats.ReceivedBytes/1024f:0.0} KB received · {stats.RoundTripMilliseconds:0}ms RTT");
  }catch(Exception e){error=e.Message;}
  if(error.Length>0)ImGui.TextWrapped(error);ImGui.EndMenu();
 }
}
