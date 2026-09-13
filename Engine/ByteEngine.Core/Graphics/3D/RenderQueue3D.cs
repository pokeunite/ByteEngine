namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// High-level 3D render queue.
///
/// The numeric values intentionally leave room for future sub-queues.
/// v0.9-a only establishes the ordering architecture; it does not yet add
/// blending or transparency state changes.
/// </summary>
public enum RenderQueue3D
{
    Opaque = 2000,
    Transparent = 3000,
    Overlay = 4000
}
