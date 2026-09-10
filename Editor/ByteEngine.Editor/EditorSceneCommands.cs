using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Editor;

internal static class EditorSceneCommands
{
    public static GameObject CreateGameObject(EditorState state, string baseName, EditorLog log)
    {
        if (state.Mode != EditorMode.Edit)
            throw new InvalidOperationException("GameObjects can only be created in Edit mode.");

        string name = CreateUniqueName(state.EditorScene, baseName);
        GameObject gameObject = state.EditorScene.CreateGameObject(name);
        state.SelectedObject = gameObject;
        state.SelectedAssetId = null;
        state.SelectedAssetPath = null;
        state.MarkDirty();
        log.Info($"Created GameObject '{name}'.");
        return gameObject;
    }

    public static void DeleteSelected(EditorState state, EditorLog log)
    {
        if (state.Mode != EditorMode.Edit || state.Selection.Count == 0) return;
        GameObject[] selected = state.Selection.Objects.ToArray();
        GameObject[] roots = selected.Where(item => !selected.Any(other => !ReferenceEquals(item, other) && item.IsDescendantOf(other))).ToArray();
        foreach (GameObject gameObject in roots) state.EditorScene.DestroyGameObject(gameObject);
        state.Selection.Clear();
        state.MarkDirty();
        log.Info($"Deleted {selected.Length} GameObject(s).");
    }

    public static void CreateSprite(
        EditorState state,
        EditorProjectContext project,
        AssetRecord asset,
        Vector2 worldPosition,
        EditorLog log)
    {
        if (state.Mode != EditorMode.Edit) return;
        var reference = new AssetReference(asset.Guid, asset.ProjectPath);
        Texture2D texture = project.Assets.LoadTexture(reference);
        GameObject gameObject = CreateGameObject(state, Path.GetFileNameWithoutExtension(asset.ProjectPath), log);
        gameObject.Transform.Position = worldPosition;
        gameObject.AddComponent(new SpriteRenderer(texture, reference) { Size = new Vector2(texture.Width, texture.Height) });
        state.MarkDirty();
        log.Info($"Created sprite '{gameObject.Name}' from '{asset.ProjectPath}' at native size {texture.Width}x{texture.Height}.");
    }

    private static string CreateUniqueName(Scene scene, string baseName)
    {
        if (scene.FindGameObject(baseName) == null) return baseName;
        int suffix = 2;
        while (scene.FindGameObject($"{baseName} {suffix}") != null) suffix++;
        return $"{baseName} {suffix}";
    }
}
