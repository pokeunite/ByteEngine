using ByteEngine.Core.Variables;
namespace ByteEngine.Core.VisualLogic;
public sealed partial class VisualLogicRegistry
{
    private static void RegisterCreatorNetwork(VisualLogicRegistry r)
    {
        var name=TextArg("name","Request name","Request");
        string Key(VisualInstruction i,EventExecutionContext c)=>c.Self.Id.ToString("N")+"/"+S(i,c,"name","Request");
        Add(r,"network.send","Network / HTTP","Send HTTP request asynchronously",(i,c)=>CreatorSceneServices.For(c.Scene).SendRequest(Key(i,c),S(i,c,"url"),S(i,c,"method","GET"),S(i,c,"body"),S(i,c,"contentType","application/json")),name,TextArg("url","HTTP / HTTPS URL","https://example.com"),TextArg("method","Method (GET, POST, PUT, PATCH, DELETE)","GET"),TextArg("body","Body"),TextArg("contentType","Content type","application/json"));
        Add(r,"network.cancel","Network / HTTP","Cancel request",(i,c)=>CreatorSceneServices.For(c.Scene).CancelRequest(Key(i,c)),name);
        Add(r,"network.remove","Network / HTTP","Remove request result",(i,c)=>CreatorSceneServices.For(c.Scene).RemoveRequest(Key(i,c)),name);
        Test(r,"network.finished","Network / HTTP","Request finished this update",(i,c)=>CreatorSceneServices.For(c.Scene).GetRequest(Key(i,c))?.FinishedThisUpdate==true,name);
        Test(r,"network.succeeded","Network / HTTP","Request has succeeded",(i,c)=>CreatorSceneServices.For(c.Scene).GetRequest(Key(i,c))?.Result?.Succeeded==true,name);
        Test(r,"network.failed","Network / HTTP","Request has failed",(i,c)=>CreatorSceneServices.For(c.Scene).GetRequest(Key(i,c))?.Result is {Succeeded:false},name);
        Add(r,"network.readResponse","Network / HTTP","Copy response to structure variable",(i,c)=>
        {var result=CreatorSceneServices.For(c.Scene).GetRequest(Key(i,c))?.Result??throw new InvalidOperationException("The request has not completed.");var data=VariableValue.FromStructure();data.Children["status"]=VariableValue.FromNumber(result.StatusCode);data.Children["body"]=VariableValue.FromString(result.Body);data.Children["error"]=VariableValue.FromString(result.Error);data.Children["cancelled"]=VariableValue.FromBoolean(result.Cancelled);Output(i,c,data);},name,TextArg("scope","Variable scope","Self"),TextArg("output","Response variable","Response"));
        Add(r,"network.openUrl","Network","Open external web URL",(i,c)=>CreatorHost.OpenExternalUrl(S(i,c,"url")),TextArg("url","HTTP / HTTPS URL","https://example.com"));
    }
}
