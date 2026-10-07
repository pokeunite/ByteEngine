using System.Numerics;
namespace ByteEngine.Core.Characters;

public enum TerrainBrushMode { Raise, Lower, Smooth, Flatten }

/// <summary>Editor authoring capability, separate from runtime deformation. Implementations serialize edits with their component.</summary>
public interface ITerrainSculptSurface
{
    HeightfieldCollider3D SculptCollider { get; }
    int Sculpt(Vector3 worldCenter, float worldRadius, float strength, float seconds, TerrainBrushMode mode, float worldFlattenHeight);
}
