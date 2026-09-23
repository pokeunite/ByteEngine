using System.Numerics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Assets;

/// <summary>
/// Exposes the Character Blueprint's Visual transform as a model-only override.
/// The Transform stays authoritative for preview, runtime and serialization.
/// </summary>
public sealed class VisualModelOverride : Component
{
    private float _importScale = 1.0f;

    public float ImportScale
    {
        get => _importScale;
        set => _importScale = float.IsFinite(value) && value > 0 ? value : 1.0f;
    }

    public Vector3 RotationDegrees
    {
        get => Transform.EulerAngles;
        set
        {
            if (float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z))
                Transform.EulerAngles = value;
        }
    }

    public Vector3 ScaleMultiplier
    {
        get => Transform.LocalScale / _importScale;
        set
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
                return;
            Transform.LocalScale = Vector3.Max(value, new Vector3(0.001f)) * _importScale;
        }
    }
}
