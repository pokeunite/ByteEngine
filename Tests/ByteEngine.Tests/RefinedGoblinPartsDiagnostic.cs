using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Tests;

internal static class RefinedGoblinPartsDiagnostic
{
    public static void Run(string directory)
    {
        using var catalog=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"parts-catalog.json")));
        int variants=0, clips=0, vertices=0;
        foreach(var part in catalog.RootElement.GetProperty("parts").EnumerateArray())
        {
            string green=part.GetProperty("file").GetString()!;
            string? red=part.TryGetProperty("red_variant",out var variant)?variant.GetString():null;
            var expected=part.GetProperty("animations").EnumerateArray().Select(a=>a.GetProperty("name").GetString()!).ToArray();
            foreach(string relative in red==null?new[]{green}:new[]{green,red})
            {
                string file=Path.Combine(directory,relative);
                var asset=new AssetRecord(Guid.NewGuid(),AssetType.Model3D,"Assets/"+relative.Replace('\\','/'),file,file+".meta",new AssetMetadata{Type=AssetType.Model3D});
                var imported=new GltfModelImporter().Import(asset,new ModelImporterSettings());
                if(imported.Meshes.Count==0 || imported.Meshes.Any(m=>m.Vertices.Length==0 || m.Indices.Length==0 || m.Vertices.Any(v=>!float.IsFinite(v))))
                    throw new InvalidOperationException("Empty/nonfinite geometry: "+relative);
                if(imported.Skeleton==null || imported.Skeleton.Bones.Count==0)
                    throw new InvalidOperationException("Missing mechanical skeleton: "+relative);
                if(imported.Materials.Any(m=>m.BaseColorTexture==null || m.BaseColorTexture.EncodedData.Length==0))
                    throw new InvalidOperationException("Missing embedded material texture: "+relative);
                if(imported.Materials.Any(m=>m.NormalTexture==null || m.NormalTexture.EncodedData.Length==0))
                    throw new InvalidOperationException("Missing embedded normal texture: "+relative);
                foreach(string name in expected)
                {
                    var clip=imported.Animations.FirstOrDefault(a=>a.Name==name);
                    if(clip==null || clip.Duration<=0 || clip.Channels.Count==0 || !clip.Channels.Any(c=>c.HasKeys))
                        throw new InvalidOperationException("Missing usable animation "+name+" in "+relative);
                }
                foreach(var mesh in imported.Meshes)
                {
                    if(mesh.JointWeights.Length==0 || mesh.JointWeights.Any(w=>Math.Abs(w.X+w.Y+w.Z+w.W-1)>.001f))
                        throw new InvalidOperationException("Invalid rigid skin weights in "+relative);
                    vertices+=mesh.Vertices.Length/8;
                }
                clips+=imported.Animations.Count;
                variants++;
                Console.WriteLine($"{relative}: {imported.Meshes.Count} primitives, {imported.Skeleton.Bones.Count} bones, {imported.Animations.Count} clips.");
            }
        }
        string message=$"PASS: {variants} GLB models imported by ByteEngine; {clips} animation clips; {vertices:N0} vertices; embedded PBR textures and normalized mechanical skin weights.";
        Console.WriteLine(message);
        File.WriteAllText(Path.Combine(directory,"byteengine-validation.txt"),message+Environment.NewLine);
    }
}

