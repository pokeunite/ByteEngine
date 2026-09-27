using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics;

public enum UiWidgetKind { Panel, Image, ProgressBar, Button }

public sealed class UiWidget : Component
{
    public UiWidgetKind Kind { get; set; } = UiWidgetKind.Panel;
    public UiAnchor Anchor { get; set; } = UiAnchor.TopLeft;
    public Vector2 Offset { get; set; } = new(24f, 24f);
    public Vector2 Size { get; set; } = new(240f, 48f);
    public bool StretchHorizontal { get; set; }
    public bool StretchVertical { get; set; }
    public Vector4 Color { get; set; } = new(.12f, .15f, .2f, .9f);
    public Vector4 HoverColor { get; set; } = new(.2f, .3f, .45f, 1f);
    public Vector4 PressedColor { get; set; } = new(.12f, .4f, .7f, 1f);
    public Vector4 DisabledColor { get; set; } = new(.16f, .16f, .16f, .6f);
    public Vector4 FillColor { get; set; } = new(.25f, .7f, .35f, 1f);
    public AssetReference ImageReference { get; set; } = AssetReference.Empty;
    public string Label { get; set; } = "Button";
    public string LabelKey { get; set; } = string.Empty;
    public int FontSize { get; set; } = 22;
    public AssetReference FontReference { get; set; } = AssetReference.Empty;
    public float Value { get; set; } = 100f;
    public float Maximum { get; set; } = 100f;
    public bool Visible { get; set; } = true;
    public bool Interactable { get; set; } = true;
    public int OrderInLayer { get; set; }
    public override int? RenderOrder => OrderInLayer;

    public bool Contains(Vector2 screenPoint, Vector2 viewport)
    {
        var rect = UiLayout.Resolve(GameObject, Anchor, Offset, Size, viewport);
        return screenPoint.X >= rect.Position.X &&
            screenPoint.Y >= rect.Position.Y &&
            screenPoint.X < rect.Position.X + rect.Size.X &&
            screenPoint.Y < rect.Position.Y + rect.Size.Y;
    }

    public bool IsHovered =>
        Kind == UiWidgetKind.Button && Visible && Interactable &&
        UiLayout.IsVisible(GameObject) &&
        Input.IsGameViewHovered &&
        Contains(Input.GameViewMousePosition, Input.GameViewSize);

    public bool IsFocused => ReferenceEquals(UiNavigation.Focused, this);

    public bool WasClicked =>
        (IsHovered && Input.IsMouseButtonPressedForUi(MouseButton.Left)) ||
        (IsFocused && (Input.IsKeyPressed(Key.Enter) ||
            Input.IsKeyPressed(Key.Space) || UiNavigation.SouthPressed));

    protected override void OnUpdate()
    {
        if (GameObject.Scene != null) UiNavigation.Update(GameObject.Scene);
        if (IsHovered) Input.NotifyGameViewPointerAim();
    }

    protected override void OnRender(RenderContext context)
    {
        if (!Visible || !UiLayout.IsVisible(GameObject)) return;
        var rect = UiLayout.Resolve(GameObject, Anchor, Offset, Size,
            new Vector2(context.TargetWidth, context.TargetHeight));
        if (rect.Size.X <= 0f || rect.Size.Y <= 0f) return;
        float opacity = UiLayout.ResolveOpacity(GameObject);
        Vector4 background = Color;
        if (Kind == UiWidgetKind.Button)
            background = !Interactable ? DisabledColor :
                IsHovered && Input.IsMouseButtonDownForUi(MouseButton.Left) ? PressedColor :
                IsHovered || IsFocused ? HoverColor : Color;
        background.W *= opacity;
        if (Kind != UiWidgetKind.Image || ImageReference.IsEmpty)
            context.QueueUiQuad(rect.Position, rect.Size, background);
        if (Kind == UiWidgetKind.ProgressBar)
        {
            float fraction = Maximum > 0f ? Math.Clamp(Value / Maximum, 0f, 1f) : 0f;
            if (fraction > 0f)
                context.QueueUiQuad(rect.Position, new Vector2(rect.Size.X * fraction,
                    rect.Size.Y), FillColor with { W = FillColor.W * opacity });
        }
        else if (Kind == UiWidgetKind.Image && !ImageReference.IsEmpty &&
            AnimationRuntimeAssets.TryGet(out AssetManager? assets) && assets != null)
        {
            context.QueueUiImage(assets.LoadTexture(ImageReference), rect.Position,
                rect.Size, Color with { W = Color.W * opacity });
        }
        else if (Kind == UiWidgetKind.Button)
        {
            context.QueueUiText(UiLocalization.Translate(GameObject, LabelKey, Label), FontRuntime.ResolvePath(FontReference),
                Math.Max(8, (int)(FontSize * rect.Scale)),
                rect.Position + rect.Size * .5f, new Vector4(1f, 1f, 1f, opacity), rect.Size.X, UiAnchor.Center);
        }
    }
}
