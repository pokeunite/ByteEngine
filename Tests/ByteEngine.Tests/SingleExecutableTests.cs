using ByteEngine.Core.Runtime;
namespace ByteEngine.Tests;
internal static class SingleExecutableTests
{
 public static void Run(string root)
 {
  void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS single executable: "+message);}
  string exe=Path.Combine(root,"Game.exe"),pak=Path.Combine(root,"Game.bytepak"),cache=Path.Combine(root,"cache");File.WriteAllBytes(exe,Enumerable.Range(0,256).Select(i=>(byte)i).ToArray());byte[] data=new byte[300000];new Random(42).NextBytes(data);File.WriteAllBytes(pak,data);
  var payload=SingleExecutablePackage.Append(exe,pak);Check(payload.Offset==256&&payload.Length==data.Length,"footer points to the exact appended payload");
  string folder=SingleExecutablePackage.Prepare(exe,cache)!;Check(File.ReadAllBytes(Path.Combine(folder,"Game.bytepak")).SequenceEqual(data),"bounded streaming extraction preserves all bytes");GamePackageExporter.ValidatePackage(folder);
  File.WriteAllText(Path.Combine(folder,"Game.bytepak"),"damaged");Check(SingleExecutablePackage.Prepare(exe,cache)==folder&&File.ReadAllBytes(Path.Combine(folder,"Game.bytepak")).SequenceEqual(data),"corrupt cached content is repaired");
  string concurrentCache=Path.Combine(root,"concurrent-cache");
  var launches=Enumerable.Range(0,8).Select(_=>Task.Run(()=>SingleExecutablePackage.Prepare(exe,concurrentCache)!)).ToArray();
  Task.WaitAll(launches);Check(launches.All(t=>t.Result==launches[0].Result),"eight simultaneous launches share one verified cache without replacement races");
  GamePackageExporter.ValidatePackage(launches[0].Result);
  string lockedPath=Path.Combine(folder,"Game.bytepak");File.WriteAllText(lockedPath,"stale locked content");
  using(var held=new FileStream(lockedPath,FileMode.Open,FileAccess.Read,FileShare.Read))
  {
   string recovered=SingleExecutablePackage.Prepare(exe,cache)!;
   Check(recovered!=folder&&File.ReadAllBytes(Path.Combine(recovered,"Game.bytepak")).SequenceEqual(data),"locked corrupt cache recovers to a verified independent directory");
   GamePackageExporter.ValidatePackage(recovered);
  }
  File.SetAttributes(lockedPath,FileAttributes.ReadOnly);
  try{string recovered=SingleExecutablePackage.Prepare(exe,cache)!;GamePackageExporter.ValidatePackage(recovered);Check(recovered!=folder,"read-only corrupt cache does not prevent startup");}
  finally{File.SetAttributes(lockedPath,FileAttributes.Normal);}
  bool duplicate=false;try{SingleExecutablePackage.Append(exe,pak);}catch(InvalidDataException){duplicate=true;}Check(duplicate,"an executable cannot accidentally accumulate multiple payloads");
  using(var file=new FileStream(exe,FileMode.Open,FileAccess.Write)){file.Position=payload.Offset+123;file.WriteByte((byte)(data[123]^255));}
  string empty=Path.Combine(root,"tamper-cache");bool tamper=false;try{SingleExecutablePackage.Prepare(exe,empty);}catch(InvalidDataException){tamper=true;}Check(tamper&&!Directory.EnumerateFiles(empty,"*.tmp",SearchOption.AllDirectories).Any(),"corrupt embedded bytes fail closed and clean temporary files");
  File.WriteAllBytes(exe,new byte[80]);Check(SingleExecutablePackage.Prepare(exe,cache)==null,"legacy loose runtime remains supported");
 }
}
