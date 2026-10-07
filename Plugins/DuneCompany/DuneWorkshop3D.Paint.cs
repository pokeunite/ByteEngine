using System.Numerics;
using ByteEngine.Core.Graphics.ThreeD;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 static readonly Vector4[] PaintColors=[Vector4.One,new(.7f,.22f,.13f,1),new(.35f,.48f,.23f,1),new(1.2f,1.16f,1.06f,1),new(.28f,.47f,.68f,1),new(.18f,.19f,.2f,1)];
 void ApplyPaint(PlacedBlock block){if(!_visuals.TryGetValue(block.Id,out var root))return;foreach(var node in Descendants(root))foreach(var renderer in node.Components.OfType<MeshRenderer>()){if(renderer.MaterialReference?.SubAssetKey.Contains("Dune_sand",StringComparison.OrdinalIgnoreCase)!=true)continue;renderer.MaterialOverrides.BaseColor=block.Paint<.5f?null:PaintColors[(int)Math.Clamp(MathF.Round(block.Paint),0,5)];}}
}
