using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics;

public enum UiWidgetKind { Panel, Image, ProgressBar, Button, Slider }

public sealed class UiWidget : Component
{
    public string ThemeKey {get;set;}=string.Empty;
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
    private float _value=100;
    public float Value { get => _value; set => _value=Kind==UiWidgetKind.Slider?Math.Clamp(float.IsFinite(value)?value:0,0,Math.Max(.001f,Maximum)):value; }
    public float Maximum { get; set; } = 100f;
    public float CornerRadius { get; set; }
    private bool _dragging;
    public bool Visible { get; set; } = true;
    public bool Interactable { get; set; } = true;
    public bool BlocksWorldPointer { get; set; }
    /// <summary>Transient highlight supplied by a linked world-space interaction.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public bool ContextHighlighted { get; set; }
    public bool ActivateWithSpace { get; set; } = true;
    public int OrderInLayer { get; set; }
    public override int? RenderOrder => OrderInLayer;

    public bool AutoFitLabel {get;set;}
    public bool FitContent {get;set;}
    public Vector2 MinimumSize {get;set;}
    public Vector2 MaximumSize {get;set;}=new(100000);
    public Vector2 ContentPadding {get;set;}=new(16,8);
    private Vector2 _measuredLabel;
    internal Vector2 LayoutSize()
    {
        Vector2 desired=FitContent&&_measuredLabel.X>0?_measuredLabel+ContentPadding*2:Size;
        return Vector2.Clamp(desired,Vector2.Max(Vector2.Zero,MinimumSize),Vector2.Max(MinimumSize,MaximumSize));
    }
    public int MinimumFontSize {get;set;}=12;
    public bool Contains(Vector2 screenPoint, Vector2 viewport)
    {
        var rect = UiLayout.Resolve(GameObject, Anchor, Offset, Size, viewport);
        return UiLayout.IsInsideClip(GameObject,screenPoint,viewport) && screenPoint.X >= rect.Position.X &&
            screenPoint.Y >= rect.Position.Y &&
            screenPoint.X < rect.Position.X + rect.Size.X &&
            screenPoint.Y < rect.Position.Y + rect.Size.Y;
    }

    public bool IsHovered =>
        Enabled && GameObject.ActiveInHierarchy && Kind is UiWidgetKind.Button or UiWidgetKind.Slider && Visible && Interactable &&
        UiLayout.IsVisible(GameObject) &&
        Input.IsGameViewHovered &&
        Contains(Input.GameViewMousePosition, Input.GameViewSize);

    public bool IsFocused => ReferenceEquals(UiNavigation.Focused, this);

    public bool WasClicked =>
        Enabled && GameObject.ActiveInHierarchy && Visible && Interactable && UiLayout.IsVisible(GameObject) && ((IsHovered && Input.IsMouseButtonPressedForUi(MouseButton.Left)) ||
        (IsFocused && (Input.IsKeyPressed(Key.Enter) ||
            (ActivateWithSpace && Input.IsKeyPressed(Key.Space)) || UiNavigation.SouthPressed)));

    protected override void OnUpdate()
    {
        if(Kind==UiWidgetKind.Slider)
        {
            Value=Value;
            if(!Visible||!Interactable||!UiLayout.IsVisible(GameObject)||!Input.IsGameViewFocused) {_dragging=false;return;}
            if(Input.IsMouseButtonPressedForUi(MouseButton.Left)&&IsHovered) _dragging=true;
            if(!Input.IsMouseButtonDownForUi(MouseButton.Left)) _dragging=false;
            if(_dragging)
            {
                var rect=UiLayout.Resolve(GameObject,Anchor,Offset,Size,Input.GameViewSize);
                float inset=Math.Min(rect.Size.Y*.35f,8*rect.Scale);
                Value=Math.Clamp((Input.GameViewMousePosition.X-rect.Position.X-inset)/Math.Max(1,rect.Size.X-inset*2),0,1)*Maximum;
            }
            if(IsHovered||_dragging) Input.NotifyGameViewPointerAim();
            return;
        }
        if (GameObject.Scene != null) UiNavigation.Update(GameObject.Scene);
        if (IsHovered) Input.NotifyGameViewPointerAim();
    }

    protected override void OnRender(RenderContext context)
    {
        if (!Visible || !UiLayout.IsVisible(GameObject)) return;
        if(FitContent&&Kind==UiWidgetKind.Button)_measuredLabel=context.MeasureText(UiLocalization.Translate(GameObject,LabelKey,Label),FontRuntime.ResolvePath(FontReference),FontSize);
        var rect = UiLayout.Resolve(GameObject, Anchor, Offset, LayoutSize(),
            new Vector2(context.TargetWidth, context.TargetHeight));
        if (rect.Size.X <= 0f || rect.Size.Y <= 0f) return;
        context.QueueUiClip(UiLayout.ResolveClip(GameObject,new(context.TargetWidth,context.TargetHeight)));
        float opacity = UiLayout.ResolveOpacity(GameObject);
        Vector4 background = Color;
        if (Kind == UiWidgetKind.Button)
            background = !Interactable ? DisabledColor :
                IsHovered && Input.IsMouseButtonDownForUi(MouseButton.Left) ? PressedColor :
                IsHovered || (IsFocused && UiNavigation.UsingKeyboardNavigation) || ContextHighlighted ? HoverColor : Color;
        UiTheme? theme=Kind==UiWidgetKind.Button&&ThemeKey.Length>0?UiTheme.Find(GameObject):null;
        if(theme!=null)background=theme.Background(ThemeKey,Interactable,IsHovered||(IsFocused&&UiNavigation.UsingKeyboardNavigation)||ContextHighlighted,IsHovered&&Input.IsMouseButtonDownForUi(MouseButton.Left));
        if(ContextHighlighted&&theme!=null)background=Vector4.Lerp(background,theme.AccentColor,.18f);
        background.W *= opacity;
        if(Kind==UiWidgetKind.Slider)
        {
            float diameter=Math.Min(rect.Size.Y,16*rect.Scale),radius=diameter/2;
            Vector2 trackPosition=rect.Position+new Vector2(radius,(rect.Size.Y-5*rect.Scale)/2);
            Vector2 trackSize=new(Math.Max(1,rect.Size.X-diameter),5*rect.Scale);
            float fraction=Maximum>0?Math.Clamp(Value/Maximum,0,1):0;
            Rounded(context,trackPosition,trackSize,Color,2.5f*rect.Scale);
            Rounded(context,trackPosition,new(trackSize.X*fraction,trackSize.Y),FillColor,2.5f*rect.Scale);
            Rounded(context,new(trackPosition.X+trackSize.X*fraction-radius,rect.Position.Y+(rect.Size.Y-diameter)/2),new(diameter),FillColor,radius);
            context.QueueUiClip(null);return;
        }
        if (Kind != UiWidgetKind.Image || ImageReference.IsEmpty)
            Rounded(context,rect.Position,rect.Size,background,CornerRadius*rect.Scale);
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
            if(ContextHighlighted)context.QueueUiQuad(rect.Position+new Vector2(0,CornerRadius*rect.Scale),new Vector2(4*rect.Scale,Math.Max(0,rect.Size.Y-2*CornerRadius*rect.Scale)),(theme?.AccentColor??FillColor) with{W=opacity});
            string label=UiLocalization.Translate(GameObject,LabelKey,Label);
            string? font=FontRuntime.ResolvePath(FontReference);
            int size=Math.Max(8,(int)(FontSize*rect.Scale));
            if(AutoFitLabel)
            {
                var measured=context.MeasureText(label,font,size);
                float factor=Math.Min(1,Math.Min((rect.Size.X-16*rect.Scale)/Math.Max(1,measured.X),(rect.Size.Y-8*rect.Scale)/Math.Max(1,measured.Y)));
                size=Math.Max(Math.Max(8,(int)(MinimumFontSize*rect.Scale)),(int)(size*Math.Max(0,factor)));
            }
            context.QueueUiText(label, font, size,
                rect.Position + rect.Size * .5f, (theme==null?Vector4.One:theme.TextColor(ThemeKey)) with {W=opacity}, rect.Size.X, UiAnchor.Center);
        }
        context.QueueUiClip(null);
    }
    private static void Rounded(RenderContext context,Vector2 position,Vector2 size,Vector4 color,float radius)
    {
        if(size.X<=0||size.Y<=0||color.W<=0)return;
        radius=Math.Clamp(radius,0,Math.Min(size.X,size.Y)/2);
        if(radius<.5f){context.QueueUiQuad(position,size,color);return;}
        context.QueueUiQuad(position+new Vector2(0,radius),new(size.X,Math.Max(0,size.Y-radius*2)),color);
        const int bands=10;
        for(int i=0;i<bands;i++)
        {
            float y=radius*i/bands,h=radius/bands;
            float inset=radius-MathF.Sqrt(Math.Max(0,radius*radius-MathF.Pow(radius-y-h/2,2)));
            var strip=new Vector2(Math.Max(0,size.X-inset*2),h+.03f);
            context.QueueUiQuad(position+new Vector2(inset,y),strip,color);
            context.QueueUiQuad(position+new Vector2(inset,size.Y-y-h),strip,color);
        }
    }
}
