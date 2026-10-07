using System.Text;

namespace ByteEngine.Core.Assets;

/// <summary>Explicit authoring checks, never a per-frame mesh scan or automatic art rewrite.</summary>
public static class GraphicsAssetAudit
{
    public static IReadOnlyList<string> Material(MaterialParameters p,Func<AssetReference,bool>? resolves=null)
    {
        var warnings=new List<string>();
        foreach(var (name,reference) in new[] { ("Albedo",p.BaseColorTexture),("Normal",p.NormalTexture),
            ("Metallic",p.MetallicTexture),("Roughness",p.RoughnessTexture),("AO",p.AmbientOcclusionTexture),
            ("Packed PBR",p.PackedPbrTexture),("Emission",p.EmissionTexture) })
            if(!reference.IsEmpty && resolves!=null && !resolves(reference)) warnings.Add(name+" texture is missing or is not an image asset.");
        if(p.PbrMapMode==MaterialPbrMapMode.Packed && p.PackedPbrTexture.IsEmpty) warnings.Add("Packed PBR selected without a packed texture.");
        if(p.PbrMapMode==MaterialPbrMapMode.Packed && (!p.RoughnessTexture.IsEmpty || !p.MetallicTexture.IsEmpty)) warnings.Add("Packed mode ignores the separate roughness/metallic maps.");
        if(p.Shading==MaterialShadingMode.Unlit) warnings.Add("Unlit does not respond to lights or normal maps.");
        if(!p.NormalTexture.IsEmpty && p.NormalStrength==0) warnings.Add("Normal map strength is zero.");
        if(!p.NormalTexture.IsEmpty && p.NormalConvention==MaterialNormalConvention.Auto) warnings.Add("If bumps look inverted, choose the normal map's OpenGL or DirectX convention.");
        if(p.Roughness<.12f) warnings.Add("Very low roughness produces a polished surface; increase it for matte materials.");
        if(p.Metallic>.8f) warnings.Add("Metal needs environment lighting/reflections; assign a suitable HDRI on Sky Environment.");
        if(!p.DepthTest) warnings.Add("Depth testing is disabled: this material can draw through other objects.");
        return warnings;
    }
    public static string Model(ModelAsset model)
    {
        var report=new StringBuilder().AppendLine("Graphics audit: "+model.Name);
        foreach(var mesh in model.Meshes)
        {
            int invalid=0,zeroNormals=0; bool variedUv=false;
            var v=mesh.Vertices;
            for(int i=0;i+7<v.Length;i+=8)
            {
                for(int j=0;j<8;j++) if(!float.IsFinite(v[i+j])) { invalid++; break; }
                if(v[i+3]*v[i+3]+v[i+4]*v[i+4]+v[i+5]*v[i+5]<.0001f) zeroNormals++;
                if(Math.Abs(v[i+6]-v[6])>.00001f || Math.Abs(v[i+7]-v[7])>.00001f) variedUv=true;
            }
            report.AppendLine($"Mesh {mesh.Name}: {v.Length/8} vertices, invalid={invalid}, missing normals={zeroNormals}, varying UV={variedUv}.");
            if(!variedUv) report.AppendLine("  Check UVs before using image textures/normal maps (constant UVs may be intentional for palette art).");
            if(mesh.MaterialKey!=null && !model.Materials.Any(m=>m.Key==mesh.MaterialKey)) report.AppendLine("  Referenced imported material is missing.");
        }
        foreach(var m in model.Materials)
        {
            report.AppendLine($"Material {m.Name}: roughness={m.Roughness:0.###}, metallic={m.Metallic:0.###}, albedo={m.BaseColorTexture!=null}, normal={m.NormalTexture!=null}.");
            foreach(var t in new[] { m.BaseColorTexture,m.NormalTexture,m.MetallicTexture,m.RoughnessTexture,m.MetallicRoughnessTexture,m.AmbientOcclusionTexture,m.EmissionTexture })
                if(t!=null && t.EncodedData.Length==0 && (string.IsNullOrWhiteSpace(t.SourcePath) || !File.Exists(t.SourcePath))) report.AppendLine("  Missing texture: "+t.Name);
        }
        report.AppendLine("Albedo/emission use sRGB; normal and PBR data maps use linear values.");
        report.AppendLine("Also check the model material override, light direction, Sky Environment lighting/HDRI and exposure. This audit does not modify assets.");
        return report.ToString();
    }
}
