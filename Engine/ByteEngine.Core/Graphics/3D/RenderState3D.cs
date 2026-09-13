namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// Controls how a standard 3D material participates in the render pipeline.
/// </summary>
public enum BlendMode3D
{
    Opaque,
    Cutout,
    AlphaBlend,
    Additive
}

/// <summary>
/// Controls whether a material writes its fragments into the depth buffer.
/// Automatic writes depth for Opaque/Cutout and disables depth writes for
/// AlphaBlend/Additive materials.
/// </summary>
public enum DepthWriteMode3D
{
    Automatic,
    Enabled,
    Disabled
}

/// <summary>
/// Selects which triangle faces are discarded before rasterization.
/// None is ByteEngine's double-sided mode and remains the compatibility
/// default for imported FBX content.
/// </summary>
public enum CullMode3D
{
    None,
    Back,
    Front
}

public enum FrontFaceWinding3D
{
    CounterClockwise,
    Clockwise
}

public enum PolygonMode3D
{
    Fill,
    Wireframe
}
