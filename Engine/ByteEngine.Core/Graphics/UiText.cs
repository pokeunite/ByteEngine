using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics;

public enum UiAnchor
{
    TopLeft,
    TopCenter,
    TopRight,
    Center,
    BottomLeft,
    BottomCenter,
    BottomRight
}

public sealed class UiText : Component
{
    public string Text { get; set; } = "New Text";
    public string LocalizationKey { get; set; } = string.Empty;
    public AssetReference FontReference { get; set; } = AssetReference.Empty;
    private int _fontSize = 32;
    public int FontSize { get => _fontSize; set => _fontSize = Math.Clamp(value, 8, 96); }
    public Vector4 Color { get; set; } = Vector4.One;
    public Vector4 ShadowColor { get; set; } = Vector4.Zero;
    public Vector2 ShadowOffset { get; set; } = new(2f, 2f);
    public Vector4 OutlineColor { get; set; } = Vector4.Zero;
    public int OutlineWidth { get; set; }
    public UiAnchor Anchor { get; set; } = UiAnchor.TopLeft;
    public Vector2 Offset { get; set; } = new(24f, 24f);
    private float _wrapWidth;
    public float WrapWidth { get => _wrapWidth; set => _wrapWidth = float.IsFinite(value) ? Math.Max(0f, value) : 0f; }
    public bool AutoFit {get;set;}
    public int MinimumFontSize {get;set;}=12;
    public int MaximumLines {get;set;}
    public bool Visible { get; set; } = true;
    public int OrderInLayer { get; set; }
    public override int? RenderOrder => OrderInLayer;

    protected override void OnRender(RenderContext context)
    {
        if (!Visible || !UiLayout.IsVisible(GameObject) ||
            !UiLayout.TryGetCanvas(GameObject, out UiCanvas? canvas) || canvas == null) return;
        context.QueueUiClip(UiLayout.ResolveClip(GameObject,new(context.TargetWidth,context.TargetHeight)));
        var canvasRect = UiLayout.ResolveParent(GameObject,
            new Vector2(context.TargetWidth, context.TargetHeight));
        float scale = canvasRect.Scale;
        UiAnimator? animator = GameObject.GetComponent<UiAnimator>() is { Enabled: true } active
            ? active : null;
        float opacity = UiLayout.ResolveOpacity(GameObject);
        string displayed = UiLocalization.Translate(GameObject, LocalizationKey, Text);
        Vector2 basePoint = Anchor switch
        {
            UiAnchor.TopCenter => canvasRect.Origin + new Vector2(canvasRect.Size.X * .5f, 0f),
            UiAnchor.TopRight => canvasRect.Origin + new Vector2(canvasRect.Size.X, 0f),
            UiAnchor.Center => canvasRect.Origin + canvasRect.Size * .5f,
            UiAnchor.BottomLeft => canvasRect.Origin + new Vector2(0f, canvasRect.Size.Y),
            UiAnchor.BottomCenter => canvasRect.Origin + new Vector2(canvasRect.Size.X * .5f, canvasRect.Size.Y),
            UiAnchor.BottomRight => canvasRect.Origin + canvasRect.Size,
            _ => canvasRect.Origin
        };
        Vector3 local = Transform.LocalPosition;
        string? fontPath = FontRuntime.ResolvePath(FontReference);
        int size = Math.Max(8, (int)MathF.Round(FontSize * scale * (animator?.Scale ?? 1f)));
        float wrap=WrapWidth*scale;
        if(AutoFit)
        {
            float available=Math.Max(1,wrap>0?wrap:canvasRect.Size.X-Math.Max(0,Offset.X*scale));
            var measured=context.MeasureText(displayed,fontPath,size);
            if(measured.X>available)size=Math.Max(Math.Max(8,(int)(MinimumFontSize*scale)),(int)(size*available/Math.Max(1,measured.X)));
            wrap=available;
        }
        if(MaximumLines>0)
        {
            var lines=UiTextLayout.WrapLines(displayed,wrap,line=>context.MeasureText(line,fontPath,size).X).ToList();
            if(lines.Count>MaximumLines)
            {
                lines=lines.Take(MaximumLines).ToList();string last=lines[^1];
                while(last.Length>0&&wrap>0&&context.MeasureText(last+"...",fontPath,size).X>wrap)last=last[..^1];
                lines[^1]=last+"...";displayed=string.Join("\n",lines);
            }
        }
        Vector2 point = basePoint + (Offset + new Vector2(local.X, local.Y)) * scale;
        point += (animator?.Offset ?? Vector2.Zero) * scale;
        if (OutlineColor.W > 0f && OutlineWidth > 0)
        {
            float width = Math.Clamp(OutlineWidth, 1, 4) * scale;
            foreach (Vector2 direction in new[] { -Vector2.UnitX, Vector2.UnitX,
                         -Vector2.UnitY, Vector2.UnitY })
                context.QueueUiText(displayed, fontPath, size, point + direction * width,
                    OutlineColor with { W = OutlineColor.W * opacity }, wrap, Anchor);
        }
        if (ShadowColor.W > 0f)
            context.QueueUiText(displayed, fontPath, size, point + ShadowOffset * scale,
                ShadowColor with { W = ShadowColor.W * opacity }, wrap, Anchor);
        context.QueueUiText(displayed, fontPath, size, point,
            Color with { W = Color.W * opacity }, wrap, Anchor);
        context.QueueUiClip(null);
    }

    private bool HasCanvasAncestor()
    {
        GameObject? node = GameObject;
        while (node != null)
        {
            if (node.GetComponent<UiCanvas>() is { Enabled: true }) return true;
            node = node.Parent;
        }
        return false;
    }
}
