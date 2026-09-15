namespace ByteEngine.Core.Physics;

public enum RigidbodyBodyType3D
{
    /// <summary>
    /// Infinite-mass body. It participates in contacts but is not moved by physics.
    /// A collider without any Rigidbody3D is treated the same way.
    /// </summary>
    Static,

    /// <summary>
    /// Simulated body affected by forces, gravity and collision impulses.
    /// </summary>
    Dynamic,

    /// <summary>
    /// Infinite-mass body moved explicitly by gameplay through its Transform.
    /// It collides with dynamic bodies but physics never changes its transform.
    /// </summary>
    Kinematic
}
