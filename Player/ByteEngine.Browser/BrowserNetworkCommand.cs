using ByteEngine.Core.Net;
internal sealed record BrowserNetworkCommand(int id,string kind,int channel=0,int buffer=-1,bool offerer=false,NetworkSignal? signal=null);
