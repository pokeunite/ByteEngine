namespace ByteEngine.Core.Graphics;

/// <summary>
/// Convenience presets for shadow-map cost and filtering.
/// The underlying resolution/softness values remain independently editable
/// and are what get serialized, so older scenes stay fully compatible.
/// </summary>
public enum ShadowQuality3D
{
    Low,
    Medium,
    High,
    Ultra
}
