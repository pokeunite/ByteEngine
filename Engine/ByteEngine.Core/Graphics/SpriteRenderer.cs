using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;

using Vector4 = System.Numerics.Vector4;

namespace ByteEngine.Core.Graphics;

public sealed class SpriteRenderer : Component
{
    public Texture2D? Texture { get; set; }

    public AssetReference? TextureReference { get; set; }

    public Vector4 Tint { get; set; } =
        Vector4.One;

    public bool Visible { get; set; } =
        true;

    public SpriteRenderer(
        Texture2D? texture = null,
        AssetReference? textureReference = null)
    {
        Texture = texture;

        TextureReference =
            textureReference;
    }

    protected override void OnRender(
        Renderer2D renderer)
    {
        if (!Visible || Texture == null)
        {
            return;
        }

        renderer.DrawSprite(
            Texture,
            Transform.Position,
            Transform.Size,
            Transform.Rotation,
            Tint
        );
    }
}
