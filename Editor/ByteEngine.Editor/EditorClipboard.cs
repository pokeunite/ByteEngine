using System.Text.Json;
using System.Text.Json.Nodes;
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

    public void Paste(EditorState state, SceneSerializer serializer, bool offset = false)
    {
        if (_objects == null || _objects.Count == 0 || state.Mode != EditorMode.Edit) return;

        List<GameObjectData> objects = Clone(_objects);
        Dictionary<Guid, Guid> ids = objects.ToDictionary(item => item.Id, _ => Guid.NewGuid());
        Dictionary<Guid, Guid> originalsByNewId = new();
        Dictionary<Guid, Guid> externalParentsByNewId = new();

        foreach (GameObjectData item in objects)
        {
            Guid oldId = item.Id;
            Guid newId = ids[oldId];
            Guid? oldParentId = item.ParentId;

            RemapBlueprintInstanceData(item, ids);

            item.Id = newId;
            originalsByNewId[newId] = oldId;

            if (oldParentId.HasValue && ids.TryGetValue(oldParentId.Value, out Guid copiedParentId))
            {
                item.ParentId = copiedParentId;
            }
            else
            {
                item.ParentId = null;

                if (oldParentId.HasValue && state.EditorScene.FindGameObject(oldParentId.Value) != null)
                    externalParentsByNewId[newId] = oldParentId.Value;
            }

            item.Name = UniqueName(state.EditorScene, item.Name);

            if (offset && item.ParentId == null && !externalParentsByNewId.ContainsKey(newId))
            {
                if (item.Transform.LocalPosition is { } localPosition)
                {
                    localPosition.X += 16f;
                    localPosition.Y += 16f;
                    item.Transform.LocalPosition = localPosition;
                }
                else if (item.Transform.Position != null)
                {
                    item.Transform.Position.X += 16f;
                    item.Transform.Position.Y += 16f;
                }
            }
        }

        var data = new SceneData { Name = "Clipboard", SceneId = Guid.NewGuid(), GameObjects = objects };
        Scene pasted = serializer.Deserialize(data);
        GameObject[] pastedObjects = pasted.GameObjects.ToArray();

        foreach (GameObject gameObject in pastedObjects)
            state.EditorScene.AddGameObject(gameObject);

        /*
         * When the copied root was a child of an object outside the copied
         * selection, SceneSerializer cannot recreate that external relation in
         * the temporary clipboard scene. Restore it after the clone enters the
         * actual editor scene and preserve the original local transform.
         */
        foreach ((Guid newId, Guid parentId) in externalParentsByNewId)
        {
            GameObject? pastedObject = state.EditorScene.FindGameObject(newId);
            GameObject? parent = state.EditorScene.FindGameObject(parentId);
            if (pastedObject != null && parent != null)
                pastedObject.SetParent(parent, false);
        }

        /*
         * Keep duplicates beside their originals instead of throwing them to
         * the end of the Hierarchy. This is especially important for children.
         */
        foreach ((Guid newId, Guid originalId) in originalsByNewId)
        {
            GameObject? pastedObject = state.EditorScene.FindGameObject(newId);
            GameObject? original = state.EditorScene.FindGameObject(originalId);
            if (pastedObject != null && original != null &&
                ReferenceEquals(pastedObject.Parent, original.Parent))
            {
                pastedObject.MoveAfter(original);
            }
        }

        state.Selection.Set(pastedObjects);
        state.MarkDirty();
    }

    private static void RemapBlueprintInstanceData(
        GameObjectData gameObject,
        IReadOnlyDictionary<Guid, Guid> ids)
    {
        foreach (ComponentData component
                 in gameObject.Components)
        {
            if (!component.Type.Equals(
                    "BlueprintInstance",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            component.Properties["instanceId"] =
                Guid.NewGuid().ToString();

            if (component.Properties["objectMap"] is not
                JsonObject objectMap)
            {
                continue;
            }

            foreach (string sourceId
                     in objectMap.Select(pair => pair.Key).ToArray())
            {
                string? placedText =
                    objectMap[sourceId]?
                        .GetValue<string>();

                if (Guid.TryParse(
                        placedText,
                        out Guid oldPlacedId) &&
                    ids.TryGetValue(
                        oldPlacedId,
                        out Guid newPlacedId))
                {
                    objectMap[sourceId] =
                        newPlacedId.ToString();
                }
            }
        }
    }

    private static List<GameObjectData> Clone(List<GameObjectData> source) =>
        JsonSerializer.Deserialize<List<GameObjectData>>(JsonSerializer.Serialize(source, JsonSerialization.Options), JsonSerialization.Options) ?? new();

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
