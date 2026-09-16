using ImGuiNET;

using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

using Matrix4 = OpenTK.Mathematics.Matrix4;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace ByteEngine.Editor;

internal sealed class ImGuiController
    : IDisposable
{
    private readonly GameWindow _window;

    private readonly string _settingsPath;

    private readonly List<uint> _pendingCharacters =
        new();

    private int _vertexArray;

    private int _vertexBuffer;

    private int _indexBuffer;

    private int _shader;

    private int _fontTexture;

    private int _projectionLocation;

    private bool _disposed;

    private float _settingsSaveElapsed;

    private static readonly (
        Keys OpenTk,
        ImGuiKey ImGui)[] KeyMappings =
    {
        (Keys.Tab, ImGuiKey.Tab),
        (Keys.Left, ImGuiKey.LeftArrow),
        (Keys.Right, ImGuiKey.RightArrow),
        (Keys.Up, ImGuiKey.UpArrow),
        (Keys.Down, ImGuiKey.DownArrow),
        (Keys.PageUp, ImGuiKey.PageUp),
        (Keys.PageDown, ImGuiKey.PageDown),
        (Keys.Home, ImGuiKey.Home),
        (Keys.End, ImGuiKey.End),
        (Keys.Insert, ImGuiKey.Insert),
        (Keys.Delete, ImGuiKey.Delete),
        (Keys.Backspace, ImGuiKey.Backspace),
        (Keys.Space, ImGuiKey.Space),
        (Keys.Enter, ImGuiKey.Enter),
        (Keys.Escape, ImGuiKey.Escape),
        (Keys.A, ImGuiKey.A),
        (Keys.C, ImGuiKey.C),
        (Keys.D, ImGuiKey.D),
        (Keys.Q, ImGuiKey.Q),
        (Keys.F, ImGuiKey.F),
        (Keys.W, ImGuiKey.W),
        (Keys.E, ImGuiKey.E),
        (Keys.R, ImGuiKey.R),
        (Keys.F2, ImGuiKey.F2),
        (Keys.N, ImGuiKey.N),
        (Keys.S, ImGuiKey.S),
        (Keys.V, ImGuiKey.V),
        (Keys.X, ImGuiKey.X),
        (Keys.Y, ImGuiKey.Y),
        (Keys.Z, ImGuiKey.Z)
    };

    public unsafe ImGuiController(
        GameWindow window,
        string settingsPath)
    {
        _window =
            window;

        _settingsPath =
            settingsPath;

        ImGui.CreateContext();

        ImGuiIOPtr io =
            ImGui.GetIO();

        io.NativePtr->IniFilename =
            null;

        io.ConfigFlags |=
            ImGuiConfigFlags.NavEnableKeyboard;

        io.ConfigFlags |=
            ImGuiConfigFlags.DockingEnable;
        io.ConfigWindowsMoveFromTitleBarOnly = true;

        io.BackendFlags |=
            ImGuiBackendFlags.RendererHasVtxOffset;

        ImGui.StyleColorsDark();

        if (File.Exists(
                _settingsPath))
        {
            ImGui.LoadIniSettingsFromDisk(
                _settingsPath
            );
        }

        ConfigureStyle();
        CreateDeviceResources();
    }

    public void AddInputCharacter(
        uint character)
    {
        _pendingCharacters.Add(
            character
        );
    }

    public void Update(
        float deltaTime)
    {
        ThrowIfDisposed();

        ImGuiIOPtr io =
            ImGui.GetIO();

        int width =
            Math.Max(
                _window.ClientSize.X,
                1
            );

        int height =
            Math.Max(
                _window.ClientSize.Y,
                1
            );

        io.DisplaySize =
            new Vector2(
                width,
                height
            );

        io.DisplayFramebufferScale =
            new Vector2(
                _window.FramebufferSize.X /
                (float)width,
                _window.FramebufferSize.Y /
                (float)height
            );

        io.DeltaTime =
            Math.Max(
                deltaTime,
                1.0f / 1000.0f
            );

        UpdateMouse(io);
        UpdateKeyboard(io);

        foreach (uint character
                 in _pendingCharacters)
        {
            io.AddInputCharacter(
                character
            );
        }

        _pendingCharacters.Clear();

        ImGui.NewFrame();

        _settingsSaveElapsed +=
            deltaTime;

        if (_settingsSaveElapsed >=
            2.0f)
        {
            SaveSettings();

            _settingsSaveElapsed =
                0.0f;
        }
    }

    public void Render()
    {
        ThrowIfDisposed();

        ImGui.Render();

        RenderDrawData(
            ImGui.GetDrawData()
        );
    }

    private void UpdateMouse(
        ImGuiIOPtr io)
    {
        Vector2 mousePosition =
            new(
                _window.MousePosition.X,
                _window.MousePosition.Y
            );

        io.AddMousePosEvent(
            mousePosition.X,
            mousePosition.Y
        );

        MouseState mouse =
            _window.MouseState;

        io.AddMouseButtonEvent(
            0,
            mouse.IsButtonDown(
                MouseButton.Left
            )
        );

        io.AddMouseButtonEvent(
            1,
            mouse.IsButtonDown(
                MouseButton.Right
            )
        );

        io.AddMouseButtonEvent(
            2,
            mouse.IsButtonDown(
                MouseButton.Middle
            )
        );

        io.AddMouseWheelEvent(
            mouse.ScrollDelta.X,
            mouse.ScrollDelta.Y
        );
    }

    private void UpdateKeyboard(
        ImGuiIOPtr io)
    {
        KeyboardState keyboard =
            _window.KeyboardState;

        foreach ((
                     Keys openTk,
                     ImGuiKey imgui) in KeyMappings)
        {
            io.AddKeyEvent(
                imgui,
                keyboard.IsKeyDown(openTk)
            );
        }

        io.AddKeyEvent(
            ImGuiKey.ModCtrl,
            keyboard.IsKeyDown(
                Keys.LeftControl
            ) ||
            keyboard.IsKeyDown(
                Keys.RightControl
            )
        );

        io.AddKeyEvent(
            ImGuiKey.ModShift,
            keyboard.IsKeyDown(
                Keys.LeftShift
            ) ||
            keyboard.IsKeyDown(
                Keys.RightShift
            )
        );

        io.AddKeyEvent(
            ImGuiKey.ModAlt,
            keyboard.IsKeyDown(
                Keys.LeftAlt
            ) ||
            keyboard.IsKeyDown(
                Keys.RightAlt
            )
        );

        io.AddKeyEvent(
            ImGuiKey.ModSuper,
            keyboard.IsKeyDown(
                Keys.LeftSuper
            ) ||
            keyboard.IsKeyDown(
                Keys.RightSuper
            )
        );
    }

    private static void ConfigureStyle()
    {
        EditorTheme.ApplyGodotInspired();
    }

    private unsafe void CreateDeviceResources()
    {
        _vertexArray =
            GL.GenVertexArray();

        _vertexBuffer =
            GL.GenBuffer();

        _indexBuffer =
            GL.GenBuffer();

        _shader =
            CreateShaderProgram(
                VertexShaderSource,
                FragmentShaderSource
            );

        _projectionLocation =
            GL.GetUniformLocation(
                _shader,
                "projection_matrix"
            );

        int textureLocation =
            GL.GetUniformLocation(
                _shader,
                "in_fontTexture"
            );

        GL.UseProgram(_shader);
        GL.Uniform1(
            textureLocation,
            0
        );

        GL.BindVertexArray(
            _vertexArray
        );

        GL.BindBuffer(
            BufferTarget.ArrayBuffer,
            _vertexBuffer
        );

        GL.BindBuffer(
            BufferTarget.ElementArrayBuffer,
            _indexBuffer
        );

        const int vertexSize = 20;

        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(
            0,
            2,
            VertexAttribPointerType.Float,
            false,
            vertexSize,
            0
        );

        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(
            1,
            2,
            VertexAttribPointerType.Float,
            false,
            vertexSize,
            8
        );

        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(
            2,
            4,
            VertexAttribPointerType.UnsignedByte,
            true,
            vertexSize,
            16
        );

        GL.BindVertexArray(0);

        ImGuiIOPtr io =
            ImGui.GetIO();

        string fontsDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Fonts);

        string segoeUi =
            Path.Combine(
                fontsDirectory,
                "segoeui.ttf");

        if (File.Exists(segoeUi))
        {
            io.Fonts.AddFontFromFileTTF(
                segoeUi,
                18.0f);
        }
        else
        {
            io.Fonts.AddFontDefault();
        }

        io.FontGlobalScale =
            1.0f;

        io.Fonts.GetTexDataAsRGBA32(
            out byte* pixels,
            out int width,
            out int height,
            out int bytesPerPixel
        );

        _fontTexture =
            GL.GenTexture();

        GL.BindTexture(
            TextureTarget.Texture2D,
            _fontTexture
        );

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter,
            (int)TextureMinFilter.Linear
        );

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter,
            (int)TextureMagFilter.Linear
        );

        GL.PixelStore(
            PixelStoreParameter.UnpackRowLength,
            0
        );

        GL.TexImage2D(
            TextureTarget.Texture2D,
            0,
            PixelInternalFormat.Rgba,
            width,
            height,
            0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            (IntPtr)pixels
        );

        io.Fonts.SetTexID(
            _fontTexture
        );

        io.Fonts.ClearTexData();

        GL.BindTexture(
            TextureTarget.Texture2D,
            0
        );
    }

    private static int CreateShaderProgram(
        string vertexSource,
        string fragmentSource)
    {
        int vertexShader =
            CompileShader(
                ShaderType.VertexShader,
                vertexSource
            );

        int fragmentShader =
            CompileShader(
                ShaderType.FragmentShader,
                fragmentSource
            );

        int program =
            GL.CreateProgram();

        GL.AttachShader(
            program,
            vertexShader
        );

        GL.AttachShader(
            program,
            fragmentShader
        );

        GL.LinkProgram(program);

        GL.GetProgram(
            program,
            GetProgramParameterName.LinkStatus,
            out int linked
        );

        GL.DetachShader(
            program,
            vertexShader
        );

        GL.DetachShader(
            program,
            fragmentShader
        );

        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        if (linked == 0)
        {
            string log =
                GL.GetProgramInfoLog(
                    program
                );

            GL.DeleteProgram(program);

            throw new InvalidOperationException(
                $"ImGui shader failed to link:{Environment.NewLine}{log}"
            );
        }

        return program;
    }

    private static int CompileShader(
        ShaderType type,
        string source)
    {
        int shader =
            GL.CreateShader(type);

        GL.ShaderSource(
            shader,
            source
        );

        GL.CompileShader(shader);

        GL.GetShader(
            shader,
            ShaderParameter.CompileStatus,
            out int compiled
        );

        if (compiled == 0)
        {
            string log =
                GL.GetShaderInfoLog(
                    shader
                );

            GL.DeleteShader(shader);

            throw new InvalidOperationException(
                $"ImGui {type} failed to compile:{Environment.NewLine}{log}"
            );
        }

        return shader;
    }

    private unsafe void RenderDrawData(
        ImDrawDataPtr drawData)
    {
        int framebufferWidth =
            (int)(
                drawData.DisplaySize.X *
                drawData.FramebufferScale.X
            );

        int framebufferHeight =
            (int)(
                drawData.DisplaySize.Y *
                drawData.FramebufferScale.Y
            );

        if (framebufferWidth <= 0 ||
            framebufferHeight <= 0)
        {
            return;
        }

        GL.Enable(
            EnableCap.Blend
        );

        GL.BlendEquation(
            BlendEquationMode.FuncAdd
        );

        GL.BlendFunc(
            BlendingFactor.SrcAlpha,
            BlendingFactor.OneMinusSrcAlpha
        );

        GL.Disable(
            EnableCap.CullFace
        );

        GL.Disable(
            EnableCap.DepthTest
        );

        GL.Enable(
            EnableCap.ScissorTest
        );

        GL.Viewport(
            0,
            0,
            framebufferWidth,
            framebufferHeight
        );

        Matrix4 projection =
            Matrix4.CreateOrthographicOffCenter(
                drawData.DisplayPos.X,
                drawData.DisplayPos.X +
                drawData.DisplaySize.X,
                drawData.DisplayPos.Y +
                drawData.DisplaySize.Y,
                drawData.DisplayPos.Y,
                -1.0f,
                1.0f
            );

        GL.UseProgram(_shader);

        GL.UniformMatrix4(
            _projectionLocation,
            false,
            ref projection
        );

        GL.ActiveTexture(
            TextureUnit.Texture0
        );

        GL.BindVertexArray(
            _vertexArray
        );

        Vector2 clipOffset =
            drawData.DisplayPos;

        Vector2 clipScale =
            drawData.FramebufferScale;

        for (int listIndex = 0;
             listIndex < drawData.CmdListsCount;
             listIndex++)
        {
            ImDrawListPtr commandList =
                drawData.CmdLists[listIndex];

            GL.BindBuffer(
                BufferTarget.ArrayBuffer,
                _vertexBuffer
            );

            GL.BufferData(
                BufferTarget.ArrayBuffer,
                commandList.VtxBuffer.Size *
                20,
                commandList.VtxBuffer.Data,
                BufferUsageHint.StreamDraw
            );

            GL.BindBuffer(
                BufferTarget.ElementArrayBuffer,
                _indexBuffer
            );

            GL.BufferData(
                BufferTarget.ElementArrayBuffer,
                commandList.IdxBuffer.Size *
                sizeof(ushort),
                commandList.IdxBuffer.Data,
                BufferUsageHint.StreamDraw
            );

            for (int commandIndex = 0;
                 commandIndex <
                 commandList.CmdBuffer.Size;
                 commandIndex++)
            {
                ImDrawCmdPtr command =
                    commandList.CmdBuffer[
                        commandIndex
                    ];

                if (command.UserCallback !=
                    IntPtr.Zero)
                {
                    continue;
                }

                Vector4 clipRectangle =
                    command.ClipRect;

                float clipMinimumX =
                    (
                        clipRectangle.X -
                        clipOffset.X
                    ) *
                    clipScale.X;

                float clipMinimumY =
                    (
                        clipRectangle.Y -
                        clipOffset.Y
                    ) *
                    clipScale.Y;

                float clipMaximumX =
                    (
                        clipRectangle.Z -
                        clipOffset.X
                    ) *
                    clipScale.X;

                float clipMaximumY =
                    (
                        clipRectangle.W -
                        clipOffset.Y
                    ) *
                    clipScale.Y;

                if (clipMaximumX <=
                        clipMinimumX ||
                    clipMaximumY <=
                        clipMinimumY)
                {
                    continue;
                }

                GL.Scissor(
                    (int)clipMinimumX,
                    (int)(
                        framebufferHeight -
                        clipMaximumY
                    ),
                    (int)(
                        clipMaximumX -
                        clipMinimumX
                    ),
                    (int)(
                        clipMaximumY -
                        clipMinimumY
                    )
                );

                GL.BindTexture(
                    TextureTarget.Texture2D,
                    (int)command.TextureId
                );

                GL.DrawElementsBaseVertex(
                    PrimitiveType.Triangles,
                    (int)command.ElemCount,
                    DrawElementsType.UnsignedShort,
                    (IntPtr)(
                        command.IdxOffset *
                        sizeof(ushort)
                    ),
                    (int)command.VtxOffset
                );
            }
        }

        GL.Disable(
            EnableCap.ScissorTest
        );

        GL.BindVertexArray(0);
        GL.UseProgram(0);
        GL.BindTexture(
            TextureTarget.Texture2D,
            0
        );
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        SaveSettings();

        GL.DeleteTexture(
            _fontTexture
        );

        GL.DeleteProgram(
            _shader
        );

        GL.DeleteBuffer(
            _vertexBuffer
        );

        GL.DeleteBuffer(
            _indexBuffer
        );

        GL.DeleteVertexArray(
            _vertexArray
        );

        ImGui.DestroyContext();

        _disposed = true;
    }

    private void SaveSettings()
    {
        string? directory =
            Path.GetDirectoryName(
                _settingsPath
            );

        if (!string.IsNullOrWhiteSpace(
                directory))
        {
            Directory.CreateDirectory(
                directory
            );
        }

        ImGui.SaveIniSettingsToDisk(
            _settingsPath
        );
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this
        );
    }

    private const string VertexShaderSource =
        """
        #version 330 core

        layout(location = 0) in vec2 in_position;
        layout(location = 1) in vec2 in_texCoord;
        layout(location = 2) in vec4 in_color;

        uniform mat4 projection_matrix;

        out vec2 fragment_texCoord;
        out vec4 fragment_color;

        void main()
        {
            fragment_texCoord = in_texCoord;
            fragment_color = in_color;
            gl_Position =
                projection_matrix *
                vec4(
                    in_position,
                    0.0,
                    1.0
                );
        }
        """;

    private const string FragmentShaderSource =
        """
        #version 330 core

        in vec2 fragment_texCoord;
        in vec4 fragment_color;

        uniform sampler2D in_fontTexture;

        out vec4 out_color;

        void main()
        {
            out_color =
                fragment_color *
                texture(
                    in_fontTexture,
                    fragment_texCoord
                );
        }
        """;
}

