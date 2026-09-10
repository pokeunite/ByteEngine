using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;

using Vector4 = System.Numerics.Vector4;
using Vector2 = System.Numerics.Vector2;

namespace ByteEngine.Core.Graphics;

public sealed class SpriteRenderer : Component
{
    public Texture2D? Texture { get; set; }

    public AssetReference? TextureReference { get; set; }

    public Vector4 Tint { get; set; } =
        Vector4.One;

    public bool Visible { get; set; } =
        true;

    public int OrderInLayer { get; set; }

    public override int? RenderOrder => OrderInLayer;

    public Vector2 Size { get; set; } = new(64f, 64f);

    public SpriteRenderer(
        Texture2D? texture = null,
        AssetReference? textureReference = null)
    {
        Texture = texture;

        TextureReference =
            textureReference;
    }

    protected override void OnRender(
        RenderContext context)
    {
        if (!Visible || Texture == null || context.Has3DCamera)
        {
            return;
        }

        context.Renderer2D.DrawSprite(
            Texture,
            new Vector2(Transform.WorldPosition.X, Transform.WorldPosition.Y),
            Size * new Vector2(Transform.WorldScale.X, Transform.WorldScale.Y),
            Transform.EulerAngles.Z,
            Tint
        );
    }
}
