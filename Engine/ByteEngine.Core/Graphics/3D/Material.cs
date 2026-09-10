using System.Numerics;
namespace ByteEngine.Core.Graphics.ThreeD;
public sealed class Material { public Vector4 BaseColor { get; set; } = Vector4.One; public Texture2D? MainTexture { get; set; } }
