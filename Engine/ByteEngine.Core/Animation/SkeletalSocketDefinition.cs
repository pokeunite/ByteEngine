using System.Numerics;

namespace ByteEngine.Core.Animation;

public sealed class SkeletalSocketDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Socket";
    public string BoneName { get; set; } = string.Empty;
    public Vector3 PositionOffset { get; set; }
    public Vector3 RotationOffsetDegrees { get; set; }
    public Vector3 Scale { get; set; } = Vector3.One;
    public bool InheritBoneScale { get; set; }
    public string? PreviewAssetPath { get; set; }
    public Guid? PreviewAssetGuid { get; set; }

    public SkeletalSocketDefinition Clone(bool preserveId = true) => new()
    {
        Id = preserveId ? Id : Guid.NewGuid(), Name = Name, BoneName = BoneName,
        PositionOffset = PositionOffset, RotationOffsetDegrees = RotationOffsetDegrees,
        Scale = Scale, InheritBoneScale = InheritBoneScale,
        PreviewAssetPath = PreviewAssetPath, PreviewAssetGuid = PreviewAssetGuid
    };
}