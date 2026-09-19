using System.ComponentModel;

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
    private readonly EditorDocumentId _documentId;
    private readonly EditorDocumentManager _documents;
    private readonly EditorDocumentWindowRegistry _registry;
    private readonly Action<Renderer2D, Renderer3D, int, int> _draw;
    private readonly Renderer2D _renderer = new();
    private readonly Renderer3D _renderer3D = new();
    private readonly Action _disposeContent;
    private readonly string _cleanTitle;
    private ImGuiController? _imgui;
    private bool _allowClose;
    private bool _disposedContent;

    public NativeEditorDocumentWindow(
        GameWindow owner,
        EditorDocumentId documentId,
        string cleanTitle,
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
        _documents = documents;
        _registry = registry;
        _draw = draw;
        _disposeContent = disposeContent;

        MakeCurrent();
        VSync = VSyncMode.On;
        GL.ClearColor(0.055f, 0.065f, 0.085f, 1.0f);
        _renderer.Initialize(Math.Max(ClientSize.X, 1), Math.Max(ClientSize.Y, 1));
        _renderer3D.Initialize();
        string safeKey = string.Concat(documentId.ToString().Select(c => char.IsLetterOrDigit(c) ? c : '_'));
        _imgui = new ImGuiController(this, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ByteEngine", "Editor", "AssetWindows", safeKey + ".ini"));
        TextInput += OnWindowTextInput;
    }

    public void PumpAndRender(float deltaTime)
    {
        ProcessEvents(0.0);
        if (!Exists || IsExiting) return;

        NativeDocumentWindowState state = ToRegistryState(WindowState);
        _registry.Record(_documentId, state);
        if (WindowState == WindowState.Minimized) return;

        if (!_documents.Documents.Any(document => document.Id == _documentId))
        {
            CloseImmediately();
            return;
        }

        MakeCurrent();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.Viewport(0, 0, Math.Max(FramebufferSize.X, 1), Math.Max(FramebufferSize.Y, 1));
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        EditorDocument? document = _documents.Documents.FirstOrDefault(item => item.Id == _documentId);
        Title = (document?.IsDirty == true ? "* " : string.Empty) + _cleanTitle;
        _imgui!.Update(deltaTime);
        _draw(_renderer, _renderer3D, Math.Max(ClientSize.X, 1), Math.Max(ClientSize.Y, 1));
        _imgui.Render();
        SwapBuffers();
    }

    public void RestoreAndFocus()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = _registry.Restore(_documentId) == NativeDocumentWindowState.Normal
                ? WindowState.Normal
                : WindowState.Maximized;
        }
        Focus();
    }

    public void CloseImmediately()
    {
        if (!Exists || IsExiting) return;
        _allowClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            _documents.RequestClose(_documentId);
        }
        base.OnClosing(e);
    }

    protected override void OnUnload()
    {
        MakeCurrent();
        _imgui?.Dispose();
        _imgui = null;
        if (!_disposedContent)
        {
            _disposeContent();
            _disposedContent = true;
        }
        _registry.Unregister(_documentId);
        base.OnUnload();
    }

    private void OnWindowTextInput(TextInputEventArgs args) => _imgui?.AddInputCharacter((uint)args.Unicode);

    private static NativeDocumentWindowState ToRegistryState(WindowState state) => state switch
    {
        WindowState.Minimized => NativeDocumentWindowState.Minimized,
        WindowState.Maximized => NativeDocumentWindowState.Maximized,
        _ => NativeDocumentWindowState.Normal
    };
}