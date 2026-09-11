using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Blueprints;
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

    public static GameObject CreateModel(
        EditorState state,
        EditorProjectContext project,
        AssetRecord asset,
        Vector3 worldPosition,
        EditorLog log)
    {
        if (state.Mode != EditorMode.Edit)
            throw new InvalidOperationException("Models can only be created in Edit mode.");

        var reference = new AssetReference(asset.Guid, asset.ProjectPath);
        ModelAsset model = project.Assets.LoadModel(reference);
        GameObject root = CreateGameObject(state, model.Name, log);
        root.Transform.WorldPosition = worldPosition;
        root.Transform.LocalScale = Vector3.One * Math.Max(asset.Metadata.ModelImporter.ImportScale, .0001f);

        var objects = new Dictionary<string, GameObject>();
        foreach (ImportedNode node in model.Nodes)
        {
            GameObject gameObject = state.EditorScene.CreateGameObject(node.Name);
            objects[node.Key] = gameObject;
        }

        foreach (ImportedNode node in model.Nodes)
        {
            GameObject gameObject = objects[node.Key];
            GameObject parent = node.ParentKey != null && objects.TryGetValue(node.ParentKey, out GameObject? nodeParent)
                ? nodeParent
                : root;
            gameObject.SetParent(parent, false);
            ApplyLocalTransform(gameObject, node.LocalTransform);

            for (int index = 0; index < node.MeshKeys.Count; index++)
            {
                string meshKey = node.MeshKeys[index];
                ImportedMesh importedMesh = model.Meshes.First(mesh => mesh.Key == meshKey);
                GameObject meshObject = index == 0 && node.MeshKeys.Count == 1
                    ? gameObject
                    : CreateMeshChild(state, gameObject, importedMesh.Name);
                var renderer = new MeshRenderer
                {
                    MeshReference = new ModelMeshReference(reference, meshKey),
                    Mesh = project.Assets.GetModelMesh(reference, meshKey)
                };
                if (importedMesh.MaterialKey != null)
                {
                    renderer.MaterialReference = new ModelMaterialReference(reference, importedMesh.MaterialKey);
                    renderer.Material = project.Assets.GetModelMaterial(reference, importedMesh.MaterialKey);
                }
                meshObject.AddComponent(renderer);
            }
        }

        state.SelectedObject = root;
        state.MarkDirty();
        log.Info($"Instantiated model '{asset.ProjectPath}' with {model.Meshes.Count} mesh(es) at {worldPosition}.");
        return root;
    }

    public static GameObject CreateBlueprintInstance(
        EditorState state,
        EditorProjectContext project,
        AssetRecord asset,
        Vector3 worldPosition,
        EditorLog log)
    {
        var blueprint = new BlueprintSerializer().Load(asset.FullPath);
        var data = new List<ByteEngine.Core.Serialization.SerializationModels.GameObjectData> { blueprint.Root };
        data.AddRange(blueprint.Children);
        IReadOnlyList<GameObject> roots = project.Scenes.InstantiateHierarchy(state.EditorScene, data);
        GameObject root = roots.FirstOrDefault()
            ?? throw new InvalidDataException("Blueprint contains no root GameObject.");
        root.Transform.WorldPosition = worldPosition;
        foreach (var variable in blueprint.Variables)
            root.Variables.Set(variable.Name, variable.Value.Clone());
        root.AddComponent(new BlueprintInstance
        {
            Blueprint = new AssetReference(asset.Guid, asset.ProjectPath),
            InstanceId = Guid.NewGuid()
        });
        state.SelectedObject = root;
        state.MarkDirty();
        log.Info($"Instantiated Blueprint '{asset.ProjectPath}' at {worldPosition}.");
        return root;
    }

    private static GameObject CreateMeshChild(EditorState state, GameObject parent, string name)
    {
        GameObject child = state.EditorScene.CreateGameObject(name);
        child.SetParent(parent, false);
        return child;
    }

    private static void ApplyLocalTransform(GameObject gameObject, Matrix4x4 matrix)
    {
        if (!Matrix4x4.Decompose(matrix, out Vector3 scale, out Quaternion rotation, out Vector3 translation))
            return;
        gameObject.Transform.LocalPosition = translation;
        gameObject.Transform.LocalRotation = rotation;
        gameObject.Transform.LocalScale = scale;
    }

    private static string CreateUniqueName(Scene scene, string baseName)
    {
        if (scene.FindGameObject(baseName) == null) return baseName;
        int suffix = 2;
        while (scene.FindGameObject($"{baseName} {suffix}") != null) suffix++;
        return $"{baseName} {suffix}";
    }
}
