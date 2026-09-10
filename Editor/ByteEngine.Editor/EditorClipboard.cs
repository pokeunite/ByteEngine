using System.Text.Json;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Editor;

internal sealed class EditorClipboard
{
    private List<GameObjectData>? _objects;
    public bool HasData => _objects?.Count > 0;

    public void Copy(EditorState state, SceneSerializer serializer)
    {
        HashSet<Guid> selected = new();
        foreach (GameObject root in state.Selection.Objects) AddWithDescendants(root, selected);
        SceneData snapshot = serializer.Serialize(state.EditorScene);
        _objects = Clone(snapshot.GameObjects.Where(item => selected.Contains(item.Id)).ToList());
    }

    public void Paste(EditorState state, SceneSerializer serializer, bool offset = true)
    {
        if (_objects == null || _objects.Count == 0 || state.Mode != EditorMode.Edit) return;
        List<GameObjectData> objects = Clone(_objects);
        Dictionary<Guid, Guid> ids = objects.ToDictionary(item => item.Id, _ => Guid.NewGuid());
        foreach (GameObjectData item in objects)
        {
            Guid oldId = item.Id;
            item.Id = ids[oldId];
            item.ParentId = item.ParentId.HasValue && ids.TryGetValue(item.ParentId.Value, out Guid parentId) ? parentId : null;
            item.Name = UniqueName(state.EditorScene, item.Name);
            if (offset && item.ParentId == null)
            {
                item.Transform.Position.X += 16f;
                item.Transform.Position.Y += 16f;
            }
        }

        var data = new SceneData { Name = "Clipboard", SceneId = Guid.NewGuid(), GameObjects = objects };
        Scene pasted = serializer.Deserialize(data);
        foreach (GameObject gameObject in pasted.GameObjects) state.EditorScene.AddGameObject(gameObject);
        state.Selection.Set(pasted.GameObjects);
        state.MarkDirty();
    }

    private static List<GameObjectData> Clone(List<GameObjectData> source) =>
        JsonSerializer.Deserialize<List<GameObjectData>>(JsonSerializer.Serialize(source)) ?? new();

    private static void AddWithDescendants(GameObject gameObject, HashSet<Guid> result)
    {
        if (!result.Add(gameObject.Id)) return;
        foreach (GameObject child in gameObject.Children) AddWithDescendants(child, result);
    }

    private static string UniqueName(Scene scene, string baseName)
    {
        if (scene.FindGameObject(baseName) == null) return baseName;
        string copy = baseName + " Copy";
        if (scene.FindGameObject(copy) == null) return copy;
        int suffix = 2;
        while (scene.FindGameObject($"{copy} {suffix}") != null) suffix++;
        return $"{copy} {suffix}";
    }
}
