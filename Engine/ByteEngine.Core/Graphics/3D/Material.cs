using System.Numerics;
namespace ByteEngine.Core.Graphics.ThreeD;

public sealed class Material
{
    public Vector4 BaseColor { get; set; } = Vector4.One;
    public Texture2D? MainTexture { get; set; }
    public Texture2D? NormalTexture { get; set; }
    public float Metallic { get; set; }
    public float Roughness { get; set; } = 1f;
}
