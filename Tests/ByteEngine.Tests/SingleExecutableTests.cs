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
  bool duplicate=false;try{SingleExecutablePackage.Append(exe,pak);}catch(InvalidDataException){duplicate=true;}Check(duplicate,"an executable cannot accidentally accumulate multiple payloads");
  using(var file=new FileStream(exe,FileMode.Open,FileAccess.Write)){file.Position=payload.Offset+123;file.WriteByte((byte)(data[123]^255));}
  string empty=Path.Combine(root,"tamper-cache");bool tamper=false;try{SingleExecutablePackage.Prepare(exe,empty);}catch(InvalidDataException){tamper=true;}Check(tamper&&!Directory.EnumerateFiles(empty,"*.tmp",SearchOption.AllDirectories).Any(),"corrupt embedded bytes fail closed and clean temporary files");
  File.WriteAllBytes(exe,new byte[80]);Check(SingleExecutablePackage.Prepare(exe,cache)==null,"legacy loose runtime remains supported");
 }
}
