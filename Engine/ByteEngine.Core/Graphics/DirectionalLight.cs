using System.Numerics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics;

public sealed class DirectionalLight : Component
{
    public Vector3 Color { get; set; } = Vector3.One;
    public float Intensity { get; set; } = 1f;
    public Vector3 Direction => Transform.Forward;
}
