using System.Net.Http;
using System.Text;
using System.Diagnostics;

namespace ByteEngine.Core.VisualLogic;

public sealed partial class CreatorSceneServices
{
    private static readonly HttpClient Client=new(){Timeout=TimeSpan.FromSeconds(30),MaxResponseContentBufferSize=4*1024*1024};
    public sealed class RequestResult
    {
        public int StatusCode {get;init;}
        public string Body {get;init;}="";
        public string Error {get;init;}="";
        public bool Cancelled {get;init;}
        public bool Succeeded=>StatusCode is >=200 and <300 && Error.Length==0;
    }
    public sealed class Request
    {
        internal CancellationTokenSource Cancellation=new();
        internal Task<RequestResult> Task=System.Threading.Tasks.Task.FromResult(new RequestResult());
        public RequestResult? Result {get;internal set;}
        public bool FinishedThisUpdate {get;internal set;}
    }
    private readonly Dictionary<string,Request> _requests=new(StringComparer.Ordinal);
    public Request? GetRequest(string key)=>_requests.GetValueOrDefault(key);
    public void SendRequest(string key,string url,string method,string body,string contentType)
    {
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme is not ("http" or "https"))throw new ArgumentException("Use an HTTP or HTTPS URL.");
        if(_requests.Count>=256&&!_requests.ContainsKey(key))throw new InvalidOperationException("Remove completed request results before starting more than 256 named requests in a scene.");
        if(_requests.Count(p=>p.Key!=key&&!p.Value.Task.IsCompleted)>=16)throw new InvalidOperationException("At most 16 requests may run concurrently in a scene.");
        RemoveRequest(key);
        var request=new Request();_requests[key]=request;
        request.Task=Send(uri,method,body,contentType,request.Cancellation.Token);
    }
    private static async Task<RequestResult> Send(Uri url,string method,string body,string contentType,CancellationToken token)
    {
        try
        {
            using var message=new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()),url);
            if(method.ToUpperInvariant() is not ("GET" or "HEAD"))message.Content=new StringContent(body,Encoding.UTF8,contentType);
            using var response=await Client.SendAsync(message,HttpCompletionOption.ResponseContentRead,token).ConfigureAwait(false);
            return new(){StatusCode=(int)response.StatusCode,Body=await response.Content.ReadAsStringAsync(token).ConfigureAwait(false)};
        }
        catch(OperationCanceledException){return new(){Cancelled=token.IsCancellationRequested,Error=token.IsCancellationRequested?"Request cancelled":"Request timed out"};}
        catch(Exception error) when(error is HttpRequestException or IOException or ArgumentException or FormatException or InvalidOperationException){return new(){Error=error.Message};}
    }
    public void CancelRequest(string key){if(_requests.TryGetValue(key,out var request))request.Cancellation.Cancel();}
    public void RemoveRequest(string key){if(_requests.Remove(key,out var request)){request.Cancellation.Cancel();request.Cancellation.Dispose();}}
    private void PollRequests()
    {
        foreach(var request in _requests.Values)
        {
            request.FinishedThisUpdate=false;
            if(request.Result!=null||!request.Task.IsCompleted)continue;
            try {request.Result=request.Task.GetAwaiter().GetResult();}
            catch(Exception error){request.Result=new(){Error=error.Message};}
            request.FinishedThisUpdate=true;
        }
    }
    private void DisposeRequests(){foreach(string key in _requests.Keys.ToArray())RemoveRequest(key);}
}

public static class CreatorHost
{
    public static Action<string>? OpenUrl {get;set;}
    public static void OpenExternalUrl(string value)
    {
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme is not ("https" or "http"))throw new ArgumentException("Only HTTP / HTTPS links are supported.");
        if(OpenUrl is {} host){host(uri.AbsoluteUri);return;}
        if(OperatingSystem.IsBrowser())throw new PlatformNotSupportedException("The browser host has not installed its URL opener.");
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});
    }
}
