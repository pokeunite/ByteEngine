using ByteEngine.Core.Audio;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using ByteEngine.Core.InputSystem;
using OpenTK.Windowing.Desktop;

namespace ByteEngine.Core;

public class ByteEngineApplication : GameWindow
{
    public Renderer2D Renderer { get; }
    public Renderer3D Renderer3D { get; }

    public SceneManager Scenes { get; }

    private readonly RuntimePostProcessTarget3D _runtimePostProcessTarget =
        new();

    public int WindowWidth =>
        ClientSize.X;

    public int WindowHeight =>
        ClientSize.Y;

    public bool IsGameInputCaptured => Input.IsGameInputCaptured;

    public void CaptureGameInput(bool grabCursor = true)
    {
        if (Input.IsGameInputCaptured) return;
        CursorState = grabCursor ? CursorState.Grabbed : CursorState.Normal;
        Input.SetGameInputCaptured(true);
    }

    public void ReleaseGameInput()
    {
        if (!Input.IsGameInputCaptured) return;
        CursorState = CursorState.Normal;
        Input.SetGameInputCaptured(false);
    }

    protected virtual bool CloseOnEscape =>
        true;

    protected virtual bool ShouldUpdateScene =>
        true;

    protected virtual bool ShouldRenderSceneToWindow =>
        true;

    public ByteEngineApplication(
        int width,
        int height,
        string title)
        : base(
            GameWindowSettings.Default,
            new NativeWindowSettings
            {
                ClientSize =
                    new Vector2i(
                        width,
                        height
                    ),

                Title =
                    title
            })
    {
        Renderer =
            new Renderer2D();

        Renderer3D =
            new Renderer3D();

        Scenes =
            new SceneManager();
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        GL.ClearColor(
            0.055f,
            0.065f,
            0.085f,
            1.0f
        );

        VSync =
            VSyncMode.On;

        CenterWindow();

        Console.WriteLine(
            "----------------------------------"
        );

        Console.WriteLine(
            $"ByteEngine v{ByteEngineInfo.Version}"
        );

        Console.WriteLine(
            "----------------------------------"
        );

        Console.WriteLine(
            $"OpenGL Version : {GL.GetString(StringName.Version)}"
        );

        Console.WriteLine(
            $"GPU            : {GL.GetString(StringName.Renderer)}"
        );

        Console.WriteLine(
            "----------------------------------"
        );

        Renderer.Initialize(
            WindowWidth,
            WindowHeight
        );

        Renderer3D.Initialize();

        OnEngineStart();

        Console.WriteLine(
            "ByteEngine started successfully."
        );

        Console.WriteLine(
            "----------------------------------"
        );
    }

    protected override void OnUpdateFrame(
        FrameEventArgs args)
    {
        base.OnUpdateFrame(
            args);

        Time.Update(
            args.Time
        );

        Input.Update(
            KeyboardState,
            MouseState,
            JoystickStates
        );

        InputActions.Update(
            Input.Snapshot,
            GameplayInputActionsEnabled
        );

        if (Input.IsGameInputCaptured && Input.IsKeyPressed(Key.Escape))
        {
            ReleaseGameInput();
            return;
        }

        if (CloseOnEscape &&
            Input.IsKeyDown(
                Key.Escape))
        {
            Close();

            return;
        }

        OnEngineUpdate();

        if (ShouldUpdateScene)
        {
            Scenes.UpdateInternal();
        }
    }

    protected virtual bool GameplayInputActionsEnabled => true;

    protected override void OnRenderFrame(
        FrameEventArgs args)
    {
        base.OnRenderFrame(
            args);

        GL.BindFramebuffer(
            FramebufferTarget.Framebuffer,
            0);

        GL.Viewport(
            0,
            0,
            Math.Max(
                WindowWidth,
                1),
            Math.Max(
                WindowHeight,
                1));

        GL.Clear(
            ClearBufferMask.ColorBufferBit |
            ClearBufferMask.DepthBufferBit
        );

        if (ShouldRenderSceneToWindow)
        {
            ByteEngine.Core.Scene.Scene? activeScene =
                Scenes.ActiveScene;

            Camera3D? camera3D =
                activeScene?.ActiveCamera;

            Camera2D? camera =
                camera3D ==
                    null
                    ? activeScene?
                        .FindComponent<Camera2D>()
                    : null;

            if (camera !=
                null)
            {
                Renderer.SetCamera(
                    new System.Numerics.Vector2(
                        camera.Transform.WorldPosition.X,
                        camera.Transform.WorldPosition.Y),
                    camera.Zoom
                );
            }
            else
            {
                Renderer.ResetCamera();
            }

            if (activeScene !=
                null)
            {
                if (camera3D !=
                    null)
                {
                    _runtimePostProcessTarget.Begin(
                        WindowWidth,
                        WindowHeight);

                    var context =
                        new RenderContext(
                            Renderer,
                            Renderer3D,
                            activeScene,
                            WindowWidth,
                            WindowHeight,
                            camera,
                            camera3D);

                    Scenes.RenderInternal(
                        context);

                    float exposure =
                        context
                            .CaptureRenderEnvironment3D()
                            .Exposure;

                    _runtimePostProcessTarget.Present(
                        exposure,
                        WindowWidth,
                        WindowHeight);
                }
                else
                {
                    /*
                     * Preserve ByteEngine's established 2D direct-to-window
                     * path. 2D content is already authored as display-space
                     * color and should not be ACES tone mapped.
                     */
                    Scenes.RenderInternal(
                        new RenderContext(
                            Renderer,
                            Renderer3D,
                            activeScene,
                            WindowWidth,
                            WindowHeight,
                            camera,
                            camera3D)
                    );
                }
            }
        }

        OnEngineRender();

        SwapBuffers();
    }

    protected override void OnResize(
        ResizeEventArgs e)
    {
        base.OnResize(
            e);

        int width =
            Math.Max(
                1,
                e.Width
            );

        int height =
            Math.Max(
                1,
                e.Height
            );

        GL.Viewport(
            0,
            0,
            width,
            height
        );

        Renderer.Resize(
            width,
            height
        );
    }

    protected override void OnUnload()
    {
        Scenes.ShutdownInternal();

        OnEngineShutdown();

        /*
         * Project/runtime shutdown disposes AudioSource clips before this
         * point. Tear down the shared OpenAL context after those resources
         * have released their buffers.
         */
        AudioEngine.Shutdown();

        _runtimePostProcessTarget.Dispose();

        Renderer.Dispose();
        Renderer3D.Dispose();

        Console.WriteLine(
            "ByteEngine shutting down."
        );

        base.OnUnload();
    }

    protected virtual void OnEngineStart()
    {
    }

    protected virtual void OnEngineUpdate()
    {
    }

    protected virtual void OnEngineRender()
    {
    }

    protected virtual void OnEngineShutdown()
    {
    }
}
