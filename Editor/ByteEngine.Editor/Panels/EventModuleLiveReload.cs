using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Editor.Panels;

/// <summary>
/// Pushes saved .byteevents changes into the currently running preview scene.
///
/// ByteGraph remains asset-authored, but Play mode no longer needs to be
/// restarted just to test logic changes.
/// </summary>
internal static class EventModuleLiveReload
{
    private static readonly EventModuleSerializer Serializer =
        new();

    public static int Apply(
        AssetRecord asset,
        EditorLog log)
    {
        ArgumentNullException.ThrowIfNull(
            asset);

        EditorState? state =
            EditorState.Active;

        if (state ==
                null ||
            state.Mode ==
                EditorMode.Edit ||
            state.RuntimeScene ==
                null)
        {
            return 0;
        }

        EventModuleDefinition definition;

        try
        {
            /*
             * Reload from disk rather than sharing the editor's mutable
             * in-memory definition with runtime GameObjects.
             */
            definition =
                Serializer.Load(
                    asset.FullPath);
        }
        catch (Exception exception)
        {
            log.Error(
                $"Could not hot reload Event Module '{asset.ProjectPath}': {exception.Message}");

            return 0;
        }

        var reference =
            new AssetReference(
                asset.Guid,
                asset.ProjectPath);

        int reloaded =
            0;

        foreach (GameObject gameObject
                 in state.RuntimeScene.GameObjects)
        {
            EventModuleComponent? component =
                gameObject.GetComponent<EventModuleComponent>();

            if (component ==
                null)
            {
                continue;
            }

            if (component.TryReloadResolvedModule(
                    reference,
                    definition))
            {
                reloaded++;
            }
        }

        return reloaded;
    }
}
