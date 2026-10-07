using System.Numerics;
using ByteEngine.Core.Scene;
namespace DuneCompany;
/// <summary>Static collision proxy for world props in the vehicle simulation.</summary>
public sealed class DuneWorldObstacle3D : Component
{
 public Vector3 Size {get;set;}=Vector3.One;
 public Vector3 Center {get;set;}=Vector3.Zero;
}
