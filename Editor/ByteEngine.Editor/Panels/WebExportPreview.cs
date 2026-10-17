using System.Net;
using System.Net.Sockets;
using ByteEngine.Core.Runtime;
namespace ByteEngine.Editor.Panels;

/// <summary>Loopback-only preview of an already exported site. No project files are exposed.</summary>
internal sealed class WebExportPreview : IDisposable
{
 readonly HttpListener listener=new();readonly string root;readonly CancellationTokenSource stop=new();
 public string Url {get;}
 public WebExportPreview(string directory){root=Path.GetFullPath(directory);var portProbe=new TcpListener(IPAddress.Loopback,0);portProbe.Start();int port=((IPEndPoint)portProbe.LocalEndpoint).Port;portProbe.Stop();Url=$"http://127.0.0.1:{port}/";listener.Prefixes.Add(Url);listener.Start();_ = Serve();}
 async Task Serve(){try{while(!stop.IsCancellationRequested){var context=await listener.GetContextAsync();_ = Reply(context);}}catch(Exception ex)when(ex is HttpListenerException or ObjectDisposedException or OperationCanceledException){}}
 async Task Reply(HttpListenerContext context){try{if(context.Request.HttpMethod is not ("GET" or "HEAD")){context.Response.StatusCode=405;return;}string path=Uri.UnescapeDataString(context.Request.Url!.AbsolutePath).TrimStart('/');if(path.Length==0)path="index.html";string file=GamePackageExporter.ResolveInside(root,path);if(!File.Exists(file)){context.Response.StatusCode=404;return;}context.Response.ContentType=Path.GetExtension(file).ToLowerInvariant() switch{".html"=>"text/html; charset=utf-8",".js"=>"text/javascript; charset=utf-8",".wasm"=>"application/wasm",".json"=>"application/json",".png"=>"image/png",".gif"=>"image/gif",".svg"=>"image/svg+xml",_=>"application/octet-stream"};context.Response.Headers["Cache-Control"]="no-cache";context.Response.ContentLength64=new FileInfo(file).Length;if(context.Request.HttpMethod=="GET"){await using var stream=File.OpenRead(file);await stream.CopyToAsync(context.Response.OutputStream,stop.Token);}}catch(Exception ex)when(ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException or HttpListenerException or OperationCanceledException){context.Response.StatusCode=400;}finally{context.Response.Close();}}
 public void Dispose(){stop.Cancel();listener.Close();stop.Dispose();}
}
