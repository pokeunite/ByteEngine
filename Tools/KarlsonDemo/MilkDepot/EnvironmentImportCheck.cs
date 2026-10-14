using System.Text.Json;
namespace KarlsonDemoTool;
internal static class EnvironmentImportCheck
{
    internal static void Run()
    {
        using var runtime=new DemoRuntime();runtime.Database.Scan();
        var root=Program.Root+"/Assets/Environment/MilkDepot";
        var reports=new List<object>();
        using var catalog=JsonDocument.Parse(File.ReadAllText(root+"/asset_catalog.json"));
        var expected=catalog.RootElement.GetProperty("assets").EnumerateArray().ToDictionary(a=>a.GetProperty("file").GetString()!,a=>a.GetProperty("triangles").GetInt32());
        foreach(var path in Directory.GetFiles(root+"/Models","*.glb").Append(root+"/MilkDepot_Assembled.glb"))
        {
            var relative=Path.GetRelativePath(Program.Root,path).Replace('\\','/');
            var model=runtime.Assets.LoadModel(runtime.Database.ResolveReference(relative));
            int triangles=model.Meshes.Sum(m=>m.Indices.Length/3);
            if(expected.TryGetValue(Path.GetFileName(path),out int authored)&&triangles!=authored)throw new Exception("Asset contains unexpected geometry: "+relative+" expected "+authored+" actual "+triangles);
            if(triangles==0||model.Meshes.Any(m=>m.Vertices.Any(v=>!float.IsFinite(v))))throw new Exception("Invalid geometry: "+relative);
            if(model.Materials.Count==0||model.Meshes.Any(m=>!model.Materials.Any(material=>material.Key==m.MaterialKey)))throw new Exception("Missing materials: "+relative);
            bool pbr=model.Materials.Any(m=>m.BaseColorTexture?.EncodedData.Length>0&&m.NormalTexture?.EncodedData.Length>0&&m.MetallicRoughnessTexture?.EncodedData.Length>0);
            if(Path.GetFileName(path) is "Crate_Large.glb" or "Wall_2m.glb" or "Floor_2m.glb" && !pbr)throw new Exception("Missing PBR maps: "+relative);
            reports.Add(new{asset=Path.GetFileName(path),triangles,materials=model.Materials.Count,pbr});
            Console.WriteLine("IMPORT PASS "+Path.GetFileName(path)+" "+triangles+" triangles");
        }
        File.WriteAllText(root+"/import-validation.json",JsonSerializer.Serialize(reports,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("ENVIRONMENT IMPORT PASS "+reports.Count);
    }
}
