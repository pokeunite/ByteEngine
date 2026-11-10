using ByteEngine.Core.Net;
using System.Text.Json;
namespace ByteEngine.Player;
internal sealed class NetworkLobbyForm : System.Windows.Forms.Form
{
 readonly System.Windows.Forms.TextBox outgoingBox=new(){Multiline=true,ReadOnly=true,Width=400,Height=100};
 readonly System.Windows.Forms.TextBox incomingBox=new(){Multiline=true,Width=400,Height=100};
 readonly System.Windows.Forms.Label status=new(){Width=400,Height=50};
 readonly Dictionary<int,List<NetworkSignal>> signals=[];int connection=-1;
 readonly NetworkSettings settings;readonly Action<int,NetworkSignal> onSignal;
 internal NetworkLobbyForm(NetworkSettings settings)
 {
  this.settings=settings;Text="ByteEngine Multiplayer";Width=450;Height=450;TopMost=true;
  var panel=new System.Windows.Forms.FlowLayoutPanel(){Dock=System.Windows.Forms.DockStyle.Fill,FlowDirection=System.Windows.Forms.FlowDirection.TopDown,WrapContents=false,AutoScroll=true};Controls.Add(panel);
  void Button(string text,Action action){var button=new System.Windows.Forms.Button(){Text=text,AutoSize=true};button.Click+=(_,_)=>{try{action();}catch(Exception e){status.Text=e.Message;}};panel.Controls.Add(button);}
  Button("Host",()=>{settings.StartSession(true);signals.Clear();connection=-1;status.Text="Hosting. Create an invite for each guest.";});
  Button("Join",()=>{settings.StartSession(false);signals.Clear();connection=NetworkRuntime.OpenConnection(false);status.Text="Paste host connection data, then return the answer.";});
  Button("New invite",()=>{connection=NetworkRuntime.OpenConnection(true);signals[connection]=[];status.Text=$"Connection {connection}: wait for connection data, then copy it.";});
  panel.Controls.Add(outgoingBox);Button("Copy connection data",()=>System.Windows.Forms.Clipboard.SetText(outgoingBox.Text));panel.Controls.Add(incomingBox);
  Button("Apply incoming data",()=>{if(connection<0)throw new InvalidOperationException("Join or create an invite first");using var json=JsonDocument.Parse(incomingBox.Text);if(json.RootElement.ValueKind!=JsonValueKind.Array||json.RootElement.GetArrayLength()>128)throw new ArgumentException("Invalid connection data");foreach(var item in json.RootElement.EnumerateArray())NetworkRuntime.Accept(connection,new(item.GetProperty("kind").GetString()!,item.GetProperty("content").GetString()!,item.GetProperty("mid").GetString()??""));incomingBox.Clear();});
  Button("Leave",()=>{NetworkRuntime.Stop();status.Text="Offline";});panel.Controls.Add(status);
  onSignal=(id,s)=>{if(!signals.TryGetValue(id,out var list))signals[id]=list=[];if(list.Count<128)list.Add(s);if(id==connection)outgoingBox.Text=JsonSerializer.Serialize(list.Select(x=>new{kind=x.Kind,content=x.Content,mid=x.Mid}));};NetworkRuntime.Signal+=onSignal;
 }
 protected override void Dispose(bool disposing){if(disposing)NetworkRuntime.Signal-=onSignal;base.Dispose(disposing);}
}
