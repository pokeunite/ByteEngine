using System.ComponentModel;

using Vector2 = System.Numerics.Vector2;

using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;

using ImGuiNET;

using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace ByteEngine.Editor;

internal sealed class NativeEditorDocumentWindow : GameWindow
{
    private readonly int _graphicsContextId =
        GraphicsContextScope.AllocateContextId();

    private readonly EditorDocumentId _documentId;
    private readonly EditorDocumentManager _documents;
    private readonly EditorDocumentWindowRegistry _registry;
    private readonly Action<Renderer2D, Renderer3D, int, int> _draw;
    private readonly Renderer2D _renderer = new();
    private readonly Renderer3D _renderer3D = new();
    private readonly Action _disposeContent;
    private readonly string _cleanTitle;
    private readonly string _mainImGuiWindowName;
    private readonly string? _inspectorImGuiWindowName;

    private ImGuiController? _imgui;
    private bool _allowClose;
    private bool _disposedContent;
    private bool _dockLayoutInitialized;
    private uint _nativeDockSpaceId;
    private uint _mainDockNodeId;

    public NativeEditorDocumentWindow(
        GameWindow owner,
        EditorDocumentId documentId,
        string cleanTitle,
        string mainImGuiWindowName,
        string? inspectorImGuiWindowName,
        EditorDocumentManager documents,
        EditorDocumentWindowRegistry registry,
        Action<Renderer2D, Renderer3D, int, int> draw,
        Action disposeContent)
        : base(
            GameWindowSettings.Default,
            new NativeWindowSettings
            {
                ClientSize = new Vector2i(1280, 800),
                Title = cleanTitle,
                WindowBorder = WindowBorder.Resizable,
                WindowState = WindowState.Maximized,
                StartVisible = true,
                StartFocused = true,
                SharedContext = owner.Context
            })
    {
        _documentId = documentId;
        _cleanTitle = cleanTitle;
        _mainImGuiWindowName = mainImGuiWindowName;
        _inspectorImGuiWindowName = inspectorImGuiWindowName;
        _documents = documents;
        _registry = registry;
        _draw = draw;
        _disposeContent = disposeContent;

        using (GraphicsContextScope.Enter(
                   _graphicsContextId))
        {
            MakeCurrent();

            VSync =
                VSyncMode.On;

            GL.ClearColor(
                0.055f,
                0.065f,
                0.085f,
                1.0f);

            _renderer.Initialize(
                Math.Max(
                    ClientSize.X,
                    1),
                Math.Max(
                    ClientSize.Y,
                    1));

            _renderer3D.Initialize();

            string safeKey =
                string.Concat(
                    documentId
                        .ToString()
                        .Select(
                            c =>
                                char.IsLetterOrDigit(c)
                                    ? c
                                    : '_'));

            _imgui =
                new ImGuiController(
                    this,
                    Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.LocalApplicationData),
                        "ByteEngine",
                        "Editor",
                        "AssetWindows",
                        safeKey + ".ini"));
        }

        TextInput +=
            OnWindowTextInput;
    }

    public void PumpAndRender(
        float deltaTime)
    {
        ProcessEvents(
            0.0);

        if (!Exists ||
            IsExiting)
        {
            return;
        }

        NativeDocumentWindowState state =
            ToRegistryState(
                WindowState);

        _registry.Record(
            _documentId,
            state);

        if (WindowState ==
            WindowState.Minimized)
        {
            return;
        }

        if (!_documents.Documents.Any(
                document =>
                    document.Id ==
                    _documentId))
        {
            CloseImmediately();
            return;
        }

        using (GraphicsContextScope.Enter(
                   _graphicsContextId))
        {
            MakeCurrent();

            GL.BindFramebuffer(
                FramebufferTarget.Framebuffer,
                0);

            GL.Viewport(
                0,
                0,
                Math.Max(
                    FramebufferSize.X,
                    1),
                Math.Max(
                    FramebufferSize.Y,
                    1));

            GL.Clear(
                ClearBufferMask.ColorBufferBit |
                ClearBufferMask.DepthBufferBit);

            EditorDocument? document =
                _documents.Documents
                    .FirstOrDefault(
                        item =>
                            item.Id ==
                            _documentId);

            Title =
                (document?.IsDirty == true
                    ? "* "
                    : string.Empty) +
                _cleanTitle;

            _imgui!.Update(
                deltaTime);

            DrawNativeDockHost();

            uint previousSceneDocumentDockId =
                EditorWorkspaceDocking.SceneDocumentDockId;

            /*
             * Some existing specialist editors (notably Event Sheet) ask
             * EditorWorkspaceDocking for their preferred document dock node.
             * While they are hosted by a native asset window, point that route
             * at this window's full-size dock node rather than the main editor.
             */
            EditorWorkspaceDocking.SceneDocumentDockId =
                _mainDockNodeId;

            try
            {
                _draw(
                    _renderer,
                    _renderer3D,
                    Math.Max(
                        ClientSize.X,
                        1),
                    Math.Max(
                        ClientSize.Y,
                        1));
            }
            finally
            {
                EditorWorkspaceDocking.SceneDocumentDockId =
                    previousSceneDocumentDockId;
            }

            _imgui.Render();
            SwapBuffers();
        }
    }

    private void DrawNativeDockHost()
    {
        ImGuiViewportPtr viewport =
            ImGui.GetMainViewport();

        ImGui.SetNextWindowPos(
            viewport.WorkPos);

        ImGui.SetNextWindowSize(
            viewport.WorkSize);

        ImGui.SetNextWindowViewport(
            viewport.ID);

        ImGui.PushStyleVar(
            ImGuiStyleVar.WindowRounding,
            0.0f);

        ImGui.PushStyleVar(
            ImGuiStyleVar.WindowBorderSize,
            0.0f);

        ImGui.PushStyleVar(
            ImGuiStyleVar.WindowPadding,
            Vector2.Zero);

        ImGuiWindowFlags hostFlags =
            ImGuiWindowFlags.NoDocking |
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoBringToFrontOnFocus |
            ImGuiWindowFlags.NoNavFocus |
            ImGuiWindowFlags.NoSavedSettings;

        ImGui.Begin(
            "ByteEngine Native Document Host",
            hostFlags);

        ImGui.PopStyleVar(
            3);

        _nativeDockSpaceId =
            ImGui.GetID(
                "ByteEngine Native Document DockSpace");

        if (!_dockLayoutInitialized)
        {
            ImGuiDockBuilder.BuildNativeDocumentLayout(
                _nativeDockSpaceId,
                viewport.WorkSize,
                _mainImGuiWindowName,
                _inspectorImGuiWindowName,
                out _mainDockNodeId,
                out _);

            _dockLayoutInitialized =
                true;
        }

        /*
         * AutoHideTabBar removes the redundant inner document tab when a dock
         * node contains only one editor window. NoUndocking prevents the
         * specialist editor from becoming a movable "window inside a window".
         * The actual native OS window remains fully movable/resizable.
         */
        ImGui.DockSpace(
            _nativeDockSpaceId,
            Vector2.Zero,
            ImGuiDockNodeFlags.AutoHideTabBar |
            ImGuiDockNodeFlags.NoUndocking);

        ImGui.End();
    }

    public void RestoreAndFocus()
    {
        if (WindowState ==
            WindowState.Minimized)
        {
            WindowState =
                _registry.Restore(
                    _documentId) ==
                NativeDocumentWindowState.Normal
                    ? WindowState.Normal
                    : WindowState.Maximized;
        }

        Focus();
    }

    public void CloseImmediately()
    {
        if (!Exists ||
            IsExiting)
        {
            return;
        }

        _allowClose =
            true;

        Close();
    }

    protected override void OnClosing(
        CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel =
                true;

            _documents.RequestClose(
                _documentId);
        }

        base.OnClosing(
            e);
    }

    protected override void OnUnload()
    {
        using (GraphicsContextScope.Enter(
                   _graphicsContextId))
        {
            MakeCurrent();

            _imgui?.Dispose();
            _imgui =
                null;

            if (!_disposedContent)
            {
                _disposeContent();

                _disposedContent =
                    true;
            }
        }

        _registry.Unregister(
            _documentId);

        base.OnUnload();
    }

    private void OnWindowTextInput(
        TextInputEventArgs args)
    {
        _imgui?.AddInputCharacter(
            (uint)args.Unicode);
    }

    private static NativeDocumentWindowState ToRegistryState(
        WindowState state)
    {
        return state switch
        {
            WindowState.Minimized =>
                NativeDocumentWindowState.Minimized,

            WindowState.Maximized =>
                NativeDocumentWindowState.Maximized,

            _ =>
                NativeDocumentWindowState.Normal
        };
    }
}
