using ByteEngine.Core.Assets.Importers;
namespace ByteEngine.Core.Assets;
/// <summary>Content-addressed derived data; safe to evict without touching authored files.</summary>
public static class ModelImportCache
{
 public const long DefaultBudgetBytes=512L*1024*1024;
 public static ImportedModel LoadOrImport(string projectRoot,AssetRecord asset,ModelImporterSettings settings,IEnumerable<string> dependencies,long budgetBytes=DefaultBudgetBytes)
 {
  string key=ImportFingerprint.Create(asset.FullPath,settings,dependencies);
  string root=Path.Combine(projectRoot,".byteengine","ImportCache",key);
  ImportedModel model;
  try { model=CookedModelStore.Load(root,asset.Guid,true); }
  catch(Exception e)when(e is IOException or InvalidDataException or System.Text.Json.JsonException)
  {
   model=ModelImportPipeline.Import(asset,settings);CookedModelStore.Save(root,model);
  }
  Directory.SetLastWriteTimeUtc(root,DateTime.UtcNow);
  Prune(projectRoot,budgetBytes);
  return model;
 }
 public static void Prune(string projectRoot,long budgetBytes)
 {
  string cache=Path.GetFullPath(Path.Combine(projectRoot,".byteengine","ImportCache"));if(!Directory.Exists(cache))return;
  var entries=new List<(string Path,string Key,long Bytes,DateTime Used)>();
  foreach(string path in Directory.EnumerateDirectories(cache))
  {
   string key=Path.GetFileName(path);
   if(key.Length!=64||key.Any(c=>!char.IsAsciiHexDigit(c))||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)continue;
   // Never follow a junction or symlink into files outside this derived-data directory.
   var pending=new Stack<string>();pending.Push(path);long entryBytes=0;bool safe=true;
   while(pending.Count>0&&safe)
    foreach(string item in Directory.EnumerateFileSystemEntries(pending.Pop()))
    {
     var attributes=File.GetAttributes(item);if((attributes&FileAttributes.ReparsePoint)!=0){safe=false;break;}
     if((attributes&FileAttributes.Directory)!=0)pending.Push(item);else entryBytes+=new FileInfo(item).Length;
    }
   if(safe)entries.Add((path,key,entryBytes,Directory.GetLastWriteTimeUtc(path)));
  }
  long bytes=entries.Sum(e=>e.Bytes);
  foreach(var entry in entries.OrderBy(e=>e.Used))
  {
   if(bytes<=Math.Max(0,budgetBytes))break;
   // This path is a validated direct child of the import cache, never a user asset directory.
   if(!string.Equals(Path.GetDirectoryName(Path.GetFullPath(entry.Path)),cache,StringComparison.OrdinalIgnoreCase))throw new IOException("Unsafe cache path");
   Directory.Delete(entry.Path,true);bytes-=entry.Bytes;
  }
 }
}
