using ByteEngine.Core;
using ByteEngine.Core.Gameplay;
using ByteEngine.Editor;
using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Tests;

internal sealed class FirstPersonViewDiagnostic(string folder) : ByteEngineApplication(1280,720,"FPS camera validation")
{
    protected override bool ShouldUpdateScene => false;
    protected override bool ShouldRenderSceneToWindow => false;
    protected override void OnEngineStart()
    {
        IsVisible = false;
        using var project = EditorProjectContext.Open(Path.Combine(folder,"fps template.byteproject"),Console.WriteLine);
        var scene = project.Scenes.Load(Path.Combine(folder,"Scenes/Main.bytescene"));
        var root = scene.GameObjects.Single(o=>o.GetComponent<FirstPersonCamera3D>()!=null);
        var rig = root.GetComponent<FirstPersonCamera3D>()!;
        var controller = root.GetComponent<PlayerController3D>()!;
        using var framebuffer = new SceneFramebuffer();
        foreach(float yaw in new[] {0f,45f,90f})
        foreach(float pitch in new[] {0f,-60f,60f})
        {
            controller.ControlYaw=yaw;controller.ControlPitch=pitch;
            root.Transform.WorldRotation = System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY,
                PlayerController3D.TransformYawFromControlYaw(yaw)*MathF.PI/180);
            rig.SnapToView();
            framebuffer.RenderGame(Renderer,Renderer3D,scene,EditorMode.Edit,1280,720,1280,720);
            GL.Finish();
            var pixels = new byte[1280*720*4];
            GL.BindTexture(TextureTarget.Texture2D,(int)framebuffer.TextureId);
            GL.GetTexImage(TextureTarget.Texture2D,0,PixelFormat.Rgba,PixelType.UnsignedByte,pixels);
            GL.BindTexture(TextureTarget.Texture2D,0);
            if(GL.GetError()!=ErrorCode.NoError)throw new Exception("FPS view render failed.");
            RoadmapUiVisualTests.Png(Path.GetFullPath($".artifacts/fps-view-yaw{yaw}-pitch{pitch}.png"),pixels,1280,720);
        }
        Console.WriteLine("PASS FPS camera native rendering at neutral, up and down pitch without GL errors.");
        Close();
    }
}



