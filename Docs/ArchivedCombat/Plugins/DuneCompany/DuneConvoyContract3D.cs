using System.Numerics;
using ByteEngine.Core.Scene;
namespace DuneCompany;
/// <summary>Moving salvage contract; vehicles remain in the workshop's shared simulation.</summary>
public sealed class DuneConvoyContract3D : Component
{
 public Guid RouteObject { get; set; }
 public Vector3 Extraction { get; set; } = new(0,0,-240);
 public float DeliveryRadius { get; set; } = 12;
 public float ConvoySpeed { get; set; } = 7;
 public float PlayerHealth { get; set; } = 300;
 public float CargoHealth { get; set; } = 220;
}
