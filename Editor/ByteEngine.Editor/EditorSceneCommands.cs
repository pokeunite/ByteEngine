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
        if (state.Mode != EditorMode.Edit || state.SelectedObject == null) return;
        string name = state.SelectedObject.Name;
        state.EditorScene.DestroyGameObject(state.SelectedObject);
        state.SelectedObject = null;
        state.MarkDirty();
        log.Info($"Deleted GameObject '{name}'.");
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
        gameObject.Transform.Size = new Vector2(texture.Width, texture.Height);
        gameObject.AddComponent(new SpriteRenderer(texture, reference));
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
