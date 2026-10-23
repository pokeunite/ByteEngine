using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using Component = ByteEngine.Core.Scene.Component;

namespace ByteEngine.Core.Gameplay;

/// <summary>Optional diagnostic overlay; reads live movement and animation rather than driving either.</summary>
public sealed class CharacterMotionDebugHud3D : Component
{
    [Category("Display")] public bool Visible { get; set; } = true;
    [Category("Display")] public Key ToggleKey { get; set; } = Key.F8;
    [Category("Display")] public Key InteractionKey { get; set; } = Key.E;
    private double _lastInteractionPress = double.NegativeInfinity;
    [Category("Display")] public Vector2 PanelOffset { get; set; } = new(18,18);
    [Category("Display")] public float PanelWidth { get; set; } = 740;
    [Category("Display")] public int FontSize { get; set; } = 20;
    [Category("Markers")] public bool ShowVectors { get; set; } = true;
    [Category("Markers")] public float VectorLength { get; set; } = 2;
    [Category("Markers")] public float MarkerHeight { get; set; } = 1.1f;
    [Category("Markers")] public Vector4 AimColor { get; set; } = new(0,.85f,1,1);
    [Category("Markers")] public Vector4 MoveColor { get; set; } = new(.1f,1,.2f,1);
    [Category("Markers")] public Vector4 FacingColor { get; set; } = new(1,.75f,.1f,1);
    [Category("Live State")] public string Report { get; private set; } = "";
    public override int? RenderOrder => 10000;
    protected override void OnUpdate()
    {
        if (Input.IsKeyPressed(InteractionKey)) _lastInteractionPress = Time.TotalTime;
        if (Input.IsGameViewFocused && Input.IsKeyPressed(ToggleKey)) Visible = !Visible;
    }
    public static string DirectionName(Vector3 velocity, Vector3 forward, Vector3 right)
    {
        velocity.Y = 0;
        if (velocity.LengthSquared() < .0025f) return "Stationary";
        float angle = MathF.Atan2(Vector3.Dot(velocity,right), Vector3.Dot(velocity,forward)) * 180 / MathF.PI;
        string[] names = ["Forward", "Forward-right", "Right", "Back-right", "Backward", "Back-left", "Left", "Forward-left"];
        return names[((int)MathF.Round(angle / 45) + 8) % 8];
    }
    protected override void OnRender(RenderContext context)
    {
        if (!Visible || !context.Scene.IsLoaded) return;
        var motor = GameObject.GetComponent<CharacterController3D>();
        var animation = GameObject.GetComponent<AnimationController>();
        var control = GameObject.GetComponent<TopDownController3D>();
        var renderer = animation?.AnimationRenderers.FirstOrDefault();
        Vector3 velocity = motor?.Velocity ?? Vector3.Zero;
        float localRight = Vector3.Dot(velocity, Transform.Right), localForward = Vector3.Dot(velocity, Transform.Forward);
        var cursor = Input.GameViewPointerNormalized * new Vector2(context.TargetWidth,context.TargetHeight);
        var aim = control?.AimWorldPoint ?? Vector3.Zero;
        Report = $"MOVEMENT / ANIMATION DEBUG  [{ToggleKey} hide]\n" +
            $"Cursor pixels: {cursor.X:F0}, {cursor.Y:F0} | Valid aim: {control?.HasCursorAim == true}\n" +
            $"Cursor world: X {aim.X:F2}  Y {aim.Y:F2}  Z {aim.Z:F2} | Aim angle: {control?.SignedAimAngleDegrees ?? 0:F1} deg\n" +
            $"Movement: {DirectionName(velocity,Transform.Forward,Transform.Right)} | Speed: {motor?.Speed ?? 0:F2} m/s\n" +
            $"Facing mode: {control?.Facing} | Cursor facing now: {control?.IsFacingCursor == true}\n" +
            $"Local movement: Right {localRight:F2}  Forward {localForward:F2} | World X {velocity.X:F2} Z {velocity.Z:F2}\n" +
            $"Animation state: {animation?.State} | Classified direction: {animation?.Direction}\n" +
            $"Blendspace: {animation?.CurrentBlendSpace} | Turn: {Short(animation?.CurrentTurn)} | Pending: {animation?.PendingTurnAngle ?? 0:F0} deg\n" +
            $"Contributors: {Short(animation?.BlendSamplesSummary)}\n" +
            $"Action: {animation?.CurrentAction} | Base clip: {Short(renderer?.CurrentAnimation)}\n" +
            $"Layers: {animation?.LayersSummary}\n" +
            $"Gameplay input: {InputSystem.InputActions.GameplayEnabled} | Mouse captured: {Input.IsGameInputCaptured} | {InteractionKey}: {(Time.TotalTime-_lastInteractionPress < 3 ? "PRESSED" : Input.IsKeyDown(InteractionKey) ? "held" : "up")}\n" +
            string.Join("\n",GameObject.Components.OfType<ICharacterDebugSource>().Select(source=>source.GetCharacterDebugText())) + "\n" +
            "CYAN cursor / aim   GREEN travel   GOLD facing";
        float scale = Math.Clamp(context.TargetWidth / 1600f,.75f,1.2f);
        int font = Math.Clamp((int)(FontSize*scale),14,32);
        float width = Math.Max(200, Math.Min(PanelWidth*scale,context.TargetWidth*.46f));
        string rendered = string.Join("\n",UiTextLayout.WrapLines(Report,width-20*scale,line=>context.MeasureText(line,null,font).X));
        float height = context.MeasureText(rendered,null,font).Y + 24*scale;
        context.QueueUiClip(null);
        context.QueueUiQuad(PanelOffset,new(width,height),new(.025f,.04f,.06f,.9f));
        context.QueueUiText(rendered,null,font,PanelOffset+new Vector2(10*scale),Vector4.One,0,UiAnchor.TopLeft);
        if (!ShowVectors || !context.Has3DCamera) return;
        bool Project(Vector3 point, out Vector2 screen)
        {
            var clip = Vector4.Transform(new Vector4(point,1),context.GetViewMatrix3D()*context.GetProjectionMatrix3D());
            screen = clip.W > .00001f ? new((clip.X/clip.W+1)*context.TargetWidth/2,(1-clip.Y/clip.W)*context.TargetHeight/2) : default;
            return clip.W > .00001f && float.IsFinite(screen.LengthSquared());
        }
        void Line(Vector2 from,Vector2 to,Vector4 color)
        {
            context.QueueUiLine(from,to,2.5f,color);
        }
        void Arrow(Vector2 from, Vector2 to, Vector4 color)
        {
            Line(from,to,color);var direction=to-from;if(direction.LengthSquared()<1)return;
            direction=Vector2.Normalize(direction);var side=new Vector2(-direction.Y,direction.X);
            Line(to,to-direction*11+side*5,color);Line(to,to-direction*11-side*5,color);
        }
        Vector3 origin = Transform.WorldPosition + Vector3.UnitY*MarkerHeight;
        if(Project(origin,out var start))
        {
            if(Project(origin+Transform.Forward*VectorLength,out var facing))Arrow(start,facing,FacingColor);
            Vector3 horizontal=velocity;horizontal.Y=0;
            if(horizontal.LengthSquared()>.0025f&&Project(origin+Vector3.Normalize(horizontal)*VectorLength,out var travel))Arrow(start,travel,MoveColor);
            if(control?.HasCursorAim==true&&Project(aim,out var target))
            {Line(start,target,AimColor);Line(target-new Vector2(8,0),target+new Vector2(8,0),AimColor);Line(target-new Vector2(0,8),target+new Vector2(0,8),AimColor);}
        }
        if(Input.IsPointerOverGameView)
        {Line(cursor-new Vector2(7,0),cursor+new Vector2(7,0),AimColor);Line(cursor-new Vector2(0,7),cursor+new Vector2(0,7),AimColor);}
    }
    static string Short(string? value) => string.IsNullOrEmpty(value) ? "(none)" : value.Replace("PolygonSyntyCharacter_", "");
}
