using System.Numerics;
using System.Text.Json.Serialization;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Gameplay;

/// <summary>Inspectable/clickable local bounds linked to existing UI and visual events.
/// This component exposes input state; it never implements a game's puzzle rules.</summary>
public sealed class PointerTarget3D : Component
{
    public Vector3 PickCenter { get; set; }
    public Vector3 PickSize { get; set; } = Vector3.One;
    public string LinkedButton { get; set; } = "";
    public string HighlightObjects { get; set; } = "";
    public string StatusText { get; set; } = "";
    public string ActionText { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Interactable { get; set; } = true;
    [JsonIgnore] public bool IsHovered { get; internal set; }
    [JsonIgnore] public bool IsSelected { get; internal set; }
    [JsonIgnore] public bool WasActivated { get; internal set; }
    [JsonIgnore] public bool IsHighlighted { get; internal set; }
    internal UiWidget? Button;
    internal readonly List<(MeshRenderer renderer,Vector3? color,float? intensity)> Renderers=[];

    internal void Initialize()
    {
        Button=GameObject.Scene!.FindGameObject(LinkedButton)?.GetComponent<UiWidget>();
        Renderers.Clear();
        foreach(var name in HighlightObjects.Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries))
            if(GameObject.Scene.FindGameObject(name) is {} root) Add(root);
        void Add(GameObject o){if(o.GetComponent<MeshRenderer>() is {} r&&!Renderers.Any(x=>x.renderer==r))Renderers.Add((r,r.MaterialOverrides.EmissionColor,r.MaterialOverrides.EmissionIntensity));foreach(var child in o.Children)Add(child);}
    }
    internal void Highlight(bool on,Vector3 color)
    {
        if(IsHighlighted==on)return;IsHighlighted=on;
        foreach(var (r,c,i) in Renderers){r.MaterialOverrides.EmissionColor=on?color:c;r.MaterialOverrides.EmissionIntensity=on?.14f:i;}
        if(Button!=null)Button.ContextHighlighted=on;
    }
    public bool IntersectsRay(Vector3 origin,Vector3 direction,out float distance)
    {
        distance=0;
        if(!Matrix4x4.Invert(Transform.WorldMatrix,out var inverse))return false;
        var p=Vector3.Transform(origin,inverse)-PickCenter;var d=Vector3.TransformNormal(direction,inverse);
        var half=Vector3.Max(Vector3.Abs(PickSize)*.5f,new(.001f));float near=0,far=float.PositiveInfinity;
        for(int axis=0;axis<3;axis++){
            float a=axis==0?p.X:axis==1?p.Y:p.Z,b=axis==0?d.X:axis==1?d.Y:d.Z,h=axis==0?half.X:axis==1?half.Y:half.Z;
            if(Math.Abs(b)<1e-7f){if(a < -h||a > h)return false;continue;}
            float t0=(-h-a)/b,t1=(h-a)/b;if(t0>t1)(t0,t1)=(t1,t0);near=Math.Max(near,t0);far=Math.Min(far,t1);if(near>far)return false;
        }
        distance=near;return far>=0;
    }
}

/// <summary>One nearest-hit picker per scene, shared world/UI highlighting and one contextual label.
/// Explicit pick volumes are intentional: small handles stay usable without adding physics bodies.</summary>
public sealed class PointerInteraction3D : Component
{
    public string CanvasName { get; set; } = "";
    public AssetReference FontReference { get; set; } = AssetReference.Empty;
    public Vector3 HighlightColor { get; set; } = new(.1f,.75f,.85f);
    public Vector2 PanelOffset { get; set; } = new(24,536);
    public float PanelWidth { get; set; } = 590;
    public bool ShowDescriptions { get; set; }
    public bool AllowActivation { get; set; } = true;
    [JsonIgnore] public string SelectedTarget => selected?.GameObject.Name??"";
    PointerTarget3D[] targets=[];
    UiWidget[] widgets=[];
    PointerTarget3D? selected,active;
    GameObject? canvas;
    protected override void OnStart()=>RefreshTargets();
    /// <summary>Focus an inspection from another UI or accessibility command; never activates it.</summary>
    public bool SelectTarget(string name)
    {
        selected=targets.FirstOrDefault(t=>t.GameObject.Name==name&&t.Enabled&&t.Interactable&&t.GameObject.ActiveInHierarchy);
        active=selected;foreach(var t in targets){t.IsSelected=t==selected;t.Highlight(t==active,HighlightColor);}return selected!=null;
    }
    public void RefreshTargets()
    {
        foreach(var t in targets)t.Highlight(false,HighlightColor);
        var s=GameObject.Scene!;targets=s.GameObjects.SelectMany(o=>o.Components).OfType<PointerTarget3D>().ToArray();widgets=s.GameObjects.SelectMany(o=>o.Components).OfType<UiWidget>().ToArray();
        foreach(var t in targets)t.Initialize();canvas=s.FindGameObject(CanvasName);selected=active=null;
    }
    protected override void OnUpdate()
    {
        foreach(var t in targets){t.WasActivated=false;t.IsHovered=false;t.IsSelected=false;}
        var eligible=targets.Where(t=>t.Enabled&&t.GameObject.ActiveInHierarchy&&t.Interactable);
        var hover=eligible.FirstOrDefault(t=>t.Button is {IsHovered:true}||(UiNavigation.UsingKeyboardNavigation&&t.Button is {IsFocused:true}));
        bool overUi=widgets.Any(w=>w.Enabled&&w.GameObject.ActiveInHierarchy&&w.Visible&&(w.BlocksWorldPointer||w.Kind is UiWidgetKind.Button or UiWidgetKind.Slider)&&w.Contains(Input.GameViewMousePosition,Input.GameViewSize)&&UiLayout.IsVisible(w.GameObject));
        if(hover==null&&!overUi&&Input.IsGameViewHovered&&GameObject.Scene!.ActiveCamera is {} camera){
            var ray=camera.ScreenPointToRay(Input.GameViewPointerNormalized,Input.GameViewSize.X/Math.Max(1,Input.GameViewSize.Y));float nearest=float.PositiveInfinity;
            foreach(var t in eligible)if(t.IntersectsRay(ray.Origin,ray.Direction,out float hit)&&hit<nearest){nearest=hit;hover=t;}
        }
        if(hover!=null)hover.IsHovered=true;
        bool click=Input.IsMouseButtonPressedForUi(MouseButton.Left);
        var activated=eligible.FirstOrDefault(t=>t.Button?.WasClicked==true)??(click?hover:null);
        if(activated!=null){selected=activated;if(AllowActivation)activated.WasActivated=true;}
        else if(click)selected=null;
        if(selected!=null&&(!selected.Enabled||!selected.GameObject.ActiveInHierarchy))selected=null;
        if(selected!=null)selected.IsSelected=true;
        active=hover??selected;
        foreach(var t in targets)t.Highlight(t==active,HighlightColor);
    }
    protected override void OnRender(RenderContext context)
    {
        if(active==null||canvas?.GetComponent<UiCanvas>() is not {Enabled:true} ui)return;
        var rect=UiLayout.ResolveCanvas(ui,new(context.TargetWidth,context.TargetHeight));float scale=rect.Scale;
        Vector2 p=rect.Origin+PanelOffset*scale;string? font=FontRuntime.ResolvePath(FontReference);
        float contentWidth=Math.Max(context.MeasureText(active.StatusText,font,(int)(24*scale)).X,context.MeasureText(active.ActionText,font,(int)(22*scale)).X)+28*scale;
        float desired=ShowDescriptions&&active.Description.Length>0?PanelWidth*scale:Math.Max(280*scale,contentWidth);
        float width=Math.Min(Math.Min(PanelWidth*scale,desired),context.TargetWidth-p.X-16*scale);
        bool description=ShowDescriptions&&active.Description.Length>0;float height=(description?112:72)*scale;
        context.QueueUiClip(null);context.QueueUiQuad(p,new(width,height),new(.97f,.96f,.91f,.97f));
        var ink=new Vector4(.075f,.2f,.22f,1);
        context.QueueUiText(active.StatusText,font,(int)(24*scale),p+new Vector2(14,7)*scale,ink,width-28*scale,UiAnchor.TopLeft);
        context.QueueUiText(active.ActionText,font,(int)(22*scale),p+new Vector2(14,37)*scale,new(.02f,.39f,.45f,1),width-28*scale,UiAnchor.TopLeft);
        if(description)context.QueueUiText(active.Description,font,(int)(18*scale),p+new Vector2(14,65)*scale,ink,width-28*scale,UiAnchor.TopLeft);
    }
    protected override void OnStop(){foreach(var t in targets)t.Highlight(false,HighlightColor);}
}
