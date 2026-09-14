namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// Distance-fog falloff used by a SkyEnvironment.
/// </summary>
public enum FogMode3D
{
    /// <summary>
    /// Fog fades from FogStartDistance to FogEndDistance.
    /// </summary>
    Linear = 0,

    /// <summary>
    /// Fog increases exponentially with camera distance.
    /// </summary>
    Exponential = 1
}
