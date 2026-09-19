using System.Runtime.CompilerServices;

using ByteEngine.Editor;

namespace ByteEngine.Tests;

internal static class EditorDocumentWorkspaceTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        DuplicateDocumentOpenFocusesExisting();
        ActiveDocumentSwitchesAndSaves();
        NativeRegistryRejectsDuplicate();
        MinimizedWindowRestoresMaximized();
        NormalWindowRestoresNormal();
        DirtyCloseCanBeCancelled();
        CloseOthersAndCloseAllRouteOnce();
        SaveAndDiscardRouteAcrossDirtyDocuments();
        UnregisterRemovesExternalDocument();
        ProjectCleanupClearsWindowRegistry();
    }

    private static void DuplicateDocumentOpenFocusesExisting()
    {
        var manager = new EditorDocumentManager();
        int focus = 0;
        EditorDocumentId id = new(EditorDocumentType.Animation, "model:key");
        Assert(manager.RegisterOrFocus(Document(id, "First", focus: () => focus++)), "first open registers");
        Assert(!manager.RegisterOrFocus(Document(id, "Second", focus: () => focus++)), "duplicate open is rejected");
        Assert(manager.Documents.Count == 1 && focus == 1 && manager.ActiveDocument?.Title == "Second",
            "duplicate focuses existing document and refreshes title");
    }

    private static void ActiveDocumentSwitchesAndSaves()
    {
        var manager = new EditorDocumentManager();
        int saves = 0;
        EditorDocumentId scene = new(EditorDocumentType.Scene, "scene");
        EditorDocumentId animation = new(EditorDocumentType.Animation, "animation");
        manager.RegisterOrFocus(Document(scene, "Scene"));
        manager.RegisterOrFocus(Document(animation, "Animation", save: () => { saves++; return true; }));
        Assert(manager.ActiveDocument?.Id == animation && manager.SaveActive() && saves == 1,
            "active document receives save");
        Assert(manager.Activate(scene) && manager.ActiveDocumentType == EditorDocumentType.Scene,
            "activation switches active document without changing main panel state");
    }

    private static void NativeRegistryRejectsDuplicate()
    {
        var registry = new EditorDocumentWindowRegistry();
        EditorDocumentId id = new(EditorDocumentType.Blueprint, "player");
        Assert(registry.Register(id), "first native window registers");
        Assert(!registry.Register(id) && registry.Count == 1, "duplicate native window is rejected");
    }

    private static void MinimizedWindowRestoresMaximized()
    {
        var registry = new EditorDocumentWindowRegistry();
        EditorDocumentId id = new(EditorDocumentType.Animation, "idle");
        registry.Register(id, NativeDocumentWindowState.Maximized);
        registry.Record(id, NativeDocumentWindowState.Minimized);
        Assert(registry.GetState(id) == NativeDocumentWindowState.Minimized, "minimized state is retained");
        Assert(registry.Restore(id) == NativeDocumentWindowState.Maximized,
            "a maximized window restores maximized after taskbar minimization");
    }

    private static void NormalWindowRestoresNormal()
    {
        var registry = new EditorDocumentWindowRegistry();
        EditorDocumentId id = new(EditorDocumentType.EventSheet, "combat");
        registry.Register(id);
        registry.Record(id, NativeDocumentWindowState.Normal);
        registry.Record(id, NativeDocumentWindowState.Minimized);
        Assert(registry.Restore(id) == NativeDocumentWindowState.Normal,
            "a restored window retains its last non-minimized state");
    }

    private static void DirtyCloseCanBeCancelled()
    {
        var manager = new EditorDocumentManager();
        bool dirty = true;
        int closeRequests = 0;
        EditorDocumentId id = new(EditorDocumentType.AnimationProfile, "hero");
        manager.RegisterOrFocus(new EditorDocument(id, "Hero", () => { }, () => closeRequests++,
            () => { dirty = false; return true; }, () => dirty = false, () => dirty));
        manager.RequestClose(id);
        Assert(closeRequests == 1 && manager.Documents.Count == 1 && manager.HasDirtyDocuments,
            "native close request leaves a dirty document registered while its Save/Discard/Cancel prompt resolves");
        Assert(manager.Documents.Single().IsDirty, "Cancel can preserve the dirty open document");
    }

    private static void CloseOthersAndCloseAllRouteOnce()
    {
        var manager = new EditorDocumentManager();
        int first = 0, second = 0;
        EditorDocumentId a = new(EditorDocumentType.Animation, "a");
        EditorDocumentId b = new(EditorDocumentType.Blueprint, "b");
        manager.RegisterOrFocus(Document(a, "A", close: () => first++));
        manager.RegisterOrFocus(Document(b, "B", close: () => second++));
        manager.RequestCloseOthers(b);
        Assert(first == 1 && second == 0, "Close Others preserves the selected document");
        manager.RequestCloseAll();
        Assert(first == 2 && second == 1, "Close All routes every document through its close policy");
    }

    private static void SaveAndDiscardRouteAcrossDirtyDocuments()
    {
        var manager = new EditorDocumentManager();
        bool firstDirty = true, secondDirty = true;
        int saved = 0, discarded = 0;
        manager.RegisterOrFocus(new EditorDocument(new(EditorDocumentType.Animation, "a"), "A", () => { }, () => { },
            () => { saved++; firstDirty = false; return true; },
            () => { discarded++; firstDirty = false; }, () => firstDirty));
        manager.RegisterOrFocus(new EditorDocument(new(EditorDocumentType.Blueprint, "b"), "B", () => { }, () => { },
            () => { saved++; secondDirty = false; return true; },
            () => { discarded++; secondDirty = false; }, () => secondDirty));
        Assert(manager.SaveAllDirty() && saved == 2, "shutdown Save visits every dirty external document");
        firstDirty = secondDirty = true;
        manager.DiscardAllDirty();
        Assert(discarded == 2 && !manager.HasDirtyDocuments, "shutdown Discard clears every dirty external document");
    }

    private static void UnregisterRemovesExternalDocument()
    {
        var registry = new EditorDocumentWindowRegistry();
        EditorDocumentId id = new(EditorDocumentType.EventSheet, "combat");
        registry.Register(id);
        Assert(registry.Unregister(id) && !registry.Contains(id) && registry.Count == 0,
            "closed external document is removed from the native registry");
    }

    private static void ProjectCleanupClearsWindowRegistry()
    {
        var registry = new EditorDocumentWindowRegistry();
        registry.Register(new(EditorDocumentType.Animation, "idle"));
        registry.Register(new(EditorDocumentType.Blueprint, "player"));
        registry.Clear();
        Assert(registry.Count == 0, "project switch clears every native window registration");
    }

    private static EditorDocument Document(
        EditorDocumentId id, string title, Action? focus = null, Action? close = null, Func<bool>? save = null) =>
        new(id, title, focus ?? (() => { }), close ?? (() => { }), save);

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("UX-1 native windows: " + message);
    }
}