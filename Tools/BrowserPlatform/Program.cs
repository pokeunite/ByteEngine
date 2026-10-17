using ByteEngine.Core.Runtime;
if(args.Length!=2)throw new ArgumentException("prepare|verify runtime-directory");
if(args[0]=="shader")BrowserRenderContract.WriteSharedSource(Path.GetFullPath(args[1]));else if(args[0]=="prepare")BrowserRenderContract.PrepareRuntime(Path.GetFullPath(args[1]));else if(args[0]=="verify")BrowserRenderContract.ValidateRuntime(Path.GetFullPath(args[1]));else throw new ArgumentException("Unknown command");
Console.WriteLine("Browser platform contract "+BrowserRenderContract.Hash);
