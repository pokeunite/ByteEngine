using System.Numerics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics;

public sealed class DirectionalLight : Component
{
    public Vector3 Color { get; set; } = Vector3.One;
    public float Intensity { get; set; } = 1f;
    public float AmbientIntensity { get; set; } = .25f;

    // Direction is the direction the light rays travel. The editor arrow/transform
    // points toward the scene, while shaders use -Direction as the surface-to-light
    // vector. Keeping that convention here also fixes existing starter scenes.
    public Vector3 Direction => -Transform.Forward;
}
