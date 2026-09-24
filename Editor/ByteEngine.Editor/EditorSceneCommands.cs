using System.Numerics;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Editor;

internal static class EditorSceneCommands
{
    public static GameObject CreateGameObject(
        EditorState state,
        string baseName,
        EditorLog log)
    {
        if (state.Mode !=
            EditorMode.Edit)
        {
            throw new InvalidOperationException(
                "GameObjects can only be created in Edit mode."
            );
        }

        string name =
            CreateUniqueName(
                state.EditorScene,
                baseName
            );

        GameObject gameObject =
            state.EditorScene.CreateGameObject(
                name
            );

        state.SelectedObject =
            gameObject;

        state.SelectedAssetId =
            null;

        state.SelectedAssetPath =
            null;

        state.MarkDirty();

        log.Info(
            $"Created GameObject '{name}'."
        );

        return gameObject;
    }

    public static void DeleteSelected(
        EditorState state,
        EditorLog log)
    {
        if (state.Mode !=
                EditorMode.Edit ||
            state.Selection.Count ==
                0)
        {
            return;
        }

        /*
         * Snapshot every selected object first. The editor must clear its
         * selection before DestroyGameObject() detaches any components or
         * hierarchy nodes, otherwise panels rendered later in this frame can
         * still see a destroyed object as the active selection.
         */
        GameObject[] selected =
            state.Selection.Objects
                .ToArray();

        GameObject[] roots =
            selected
                .Where(
                    item =>
                        !selected.Any(
                            other =>
                                !ReferenceEquals(
                                    item,
                                    other
                                ) &&
                                item.IsDescendantOf(
                                    other
                                )
                        )
                )
                .ToArray();

        /*
         * Clear selection BEFORE destruction. This is important for the Sky
         * Environment because deleting that object invalidates its attached
         * component immediately, while Inspector/Scene/Game panels may still
         * run later in the same editor frame.
         */
        state.Selection.Clear();

        foreach (GameObject gameObject
                 in roots)
        {
            state.EditorScene.DestroyGameObject(
                gameObject
            );
        }

        state.MarkDirty();

        log.Info(
            $"Deleted {selected.Length} GameObject(s)."
        );
    }

    public static void CreateSprite(
        EditorState state,
        EditorProjectContext project,
        AssetRecord asset,
        Vector2 worldPosition,
        EditorLog log)
    {
        if (state.Mode !=
            EditorMode.Edit)
        {
            return;
        }

        var reference =
            new AssetReference(
                asset.Guid,
                asset.ProjectPath
            );

        Texture2D texture =
            project.Assets.LoadTexture(
                reference
            );

        GameObject gameObject =
            CreateGameObject(
                state,
                Path.GetFileNameWithoutExtension(
                    asset.ProjectPath
                ),
                log
            );

        gameObject.Transform.Position =
            worldPosition;

        gameObject.AddComponent(
            new SpriteRenderer(
                texture,
                reference
            )
            {
                Size =
                    new Vector2(
                        texture.Width,
                        texture.Height
                    )
            }
        );

        state.MarkDirty();

        log.Info(
            $"Created sprite '{gameObject.Name}' from '{asset.ProjectPath}' at native size {texture.Width}x{texture.Height}."
        );
    }

    public static GameObject CreateModel(
        EditorState state,
        EditorProjectContext project,
        AssetRecord asset,
        Vector3 worldPosition,
        EditorLog log)
    {
        if (state.Mode !=
            EditorMode.Edit)
        {
            throw new InvalidOperationException(
                "Models can only be created in Edit mode."
            );
        }

        var reference =
            new AssetReference(
                asset.Guid,
                asset.ProjectPath
            );

        ModelAsset model =
            project.Assets.LoadModel(
                reference
            );

        GameObject root =
            CreateGameObject(
                state,
                model.Name,
                log
            );

        root.Transform.WorldPosition =
            worldPosition;

        ModelScaleAnalysis scaleAnalysis =
            ModelImportScaleUtility.Analyze(
                asset,
                model);

        float appliedScale =
            scaleAnalysis.AppliedScale;

        root.Transform.LocalScale = Vector3.One;

        root.AddComponent(
            new ModelHierarchyInstance
            {
                Model =
                    reference,

                AppliedImportScale =
                    appliedScale
            });

        var objects =
            new Dictionary<string, GameObject>();

        foreach (ImportedNode node
                 in model.Nodes)
        {
            GameObject gameObject =
                state.EditorScene.CreateGameObject(
                    node.Name
                );

            objects[node.Key] =
                gameObject;
        }

        foreach (ImportedNode node
                 in model.Nodes)
        {
            GameObject gameObject =
                objects[node.Key];

            GameObject parent =
                node.ParentKey != null &&
                objects.TryGetValue(
                    node.ParentKey,
                    out GameObject? nodeParent)
                    ? nodeParent
                    : root;

            gameObject.SetParent(
                parent,
                false
            );

            ApplyLocalTransform(
                gameObject,
                node.LocalTransform
            );

            for (int index = 0;
                 index < node.MeshKeys.Count;
                 index++)
            {
                string meshKey =
                    node.MeshKeys[index];

                ImportedMesh importedMesh =
                    model.Meshes.First(
                        mesh =>
                            mesh.Key ==
                            meshKey
                    );

                GameObject meshObject =
                    index == 0 &&
                    node.MeshKeys.Count ==
                    1
                        ? gameObject
                        : CreateMeshChild(
                            state,
                            gameObject,
                            importedMesh.Name
                        );

                var renderer =
                    new MeshRenderer
                    {
                        MeshReference =
                            new ModelMeshReference(
                                reference,
                                meshKey
                            ),

                        Mesh =
                            project.Assets.GetModelMesh(
                                reference,
                                meshKey
                            )
                    };

                if (importedMesh.MaterialKey !=
                    null)
                {
                    renderer.MaterialReference =
                        new ModelMaterialReference(
                            reference,
                            importedMesh.MaterialKey
                        );

                    renderer.Material =
                        project.Assets.GetModelMaterial(
                            reference,
                            importedMesh.MaterialKey
                        );
                }

                meshObject.AddComponent(
                    renderer
                );
            }
        }

        state.SelectedObject =
            root;

        state.MarkDirty();

        string scaleNote =
            scaleAnalysis.Normalized
                ? $" {scaleAnalysis.Summary}."
                : string.Empty;

        log.Info(
            $"Instantiated model '{asset.ProjectPath}' with {model.Meshes.Count} mesh(es) at {worldPosition}.{scaleNote}"
        );

        return root;
    }

    public static GameObject CreateBlueprintInstance(
        EditorState state,
        EditorProjectContext project,
        AssetRecord asset,
        Vector3 worldPosition,
        EditorLog log)
    {
        BlueprintDefinition blueprint =
            new BlueprintSerializer()
                .Load(
                    asset.FullPath
                );

        var data =
            new List<
                ByteEngine.Core.Serialization
                    .SerializationModels
                    .GameObjectData>
            {
                blueprint.Root
            };

        data.AddRange(
            blueprint.Children
        );

        IReadOnlyList<GameObject> roots =
            project.Scenes.InstantiateHierarchy(
                state.EditorScene,
                data,
                null,
                out IReadOnlyDictionary<Guid, Guid> objectMap
            );

        GameObject root =
            roots.FirstOrDefault()
            ?? throw new InvalidDataException(
                "Blueprint contains no root GameObject."
            );

        root.Transform.WorldPosition =
            worldPosition;

        foreach (var variable
                 in blueprint.Variables)
        {
            root.Variables.Set(
                variable.Name,
                variable.Value.Clone()
            );
        }

        BlueprintInstance instance = root.AddComponent(
            new BlueprintInstance
            {
                Blueprint =
                    new AssetReference(
                        asset.Guid,
                        asset.ProjectPath
                    ),

                InstanceId =
                    Guid.NewGuid()
            });

        BlueprintInstanceSynchronizer.Initialize(instance, blueprint, objectMap);

        AttachBlueprintEventModules(
            root,
            blueprint,
            project,
            log
        );

        state.SelectedObject =
            root;

        state.MarkDirty();

        log.Info(
            $"Instantiated Blueprint '{asset.ProjectPath}' at {worldPosition}."
        );

        return root;
    }

    private static void AttachBlueprintEventModules(
        GameObject root,
        BlueprintDefinition blueprint,
        EditorProjectContext project,
        EditorLog log)
    {
        if (blueprint.EventModules.Count ==
            0)
        {
            return;
        }

        EventModuleComponent runner =
            root.GetComponent<EventModuleComponent>()
            ?? root.AddComponent(
                new EventModuleComponent()
            );

        var serializer =
            new EventModuleSerializer();

        foreach (Guid moduleGuid
                 in blueprint.EventModules)
        {
            if (moduleGuid ==
                Guid.Empty)
            {
                continue;
            }

            if (!project.AssetDatabase.TryGetAsset(
                    moduleGuid,
                    out AssetRecord? asset) ||
                asset == null)
            {
                runner.AddModuleReference(
                    new AssetReference(
                        moduleGuid
                    )
                );

                log.Warning(
                    $"Blueprint '{blueprint.Name}' references missing Event Module {moduleGuid}."
                );

                continue;
            }

            var reference =
                new AssetReference(
                    asset.Guid,
                    asset.ProjectPath
                );

            runner.AddModuleReference(
                reference
            );

            if (asset.Type !=
                AssetType.EventModule)
            {
                log.Warning(
                    $"Blueprint '{blueprint.Name}' references '{asset.ProjectPath}', but it is not an Event Module."
                );

                continue;
            }

            try
            {
                EventModuleDefinition definition =
                    serializer.Load(
                        asset.FullPath
                    );

                runner.AddResolvedModule(
                    reference,
                    definition
                );
            }
            catch (Exception exception)
            {
                log.Warning(
                    $"Could not load Event Module '{asset.ProjectPath}' for Blueprint '{blueprint.Name}': {exception.Message}"
                );
            }
        }
    }

    private static GameObject CreateMeshChild(
        EditorState state,
        GameObject parent,
        string name)
    {
        GameObject child =
            state.EditorScene.CreateGameObject(
                name
            );

        child.SetParent(
            parent,
            false
        );

        return child;
    }

    public static bool RefreshImportSpace(GameObject modelRoot, ModelAsset model)
    {
        ImportedNode? correction = model.Nodes.FirstOrDefault(node =>
            node.Key == ImportedModelSpace.CorrectionNodeKey);
        GameObject? correctionObject = modelRoot.Children.FirstOrDefault(child =>
            child.Name.Equals("Import Space", StringComparison.Ordinal));
        if (correctionObject == null)
            return RebaseLegacyImportSpace(modelRoot, model);

        Matrix4x4 target = correction?.LocalTransform ?? Matrix4x4.Identity;
        if (!Matrix4x4.Decompose(target, out Vector3 scale,
                out Quaternion rotation, out Vector3 translation))
            return false;
        Transform current = correctionObject.Transform;
        if (Vector3.DistanceSquared(current.LocalPosition, translation) < 0.0000001f &&
            Vector3.DistanceSquared(current.LocalScale, scale) < 0.0000001f &&
            MathF.Abs(Quaternion.Dot(current.LocalRotation, rotation)) > 0.999999f)
            return false;

        ApplyLocalTransform(correctionObject, target);
        return true;
    }
    private static bool RebaseLegacyImportSpace(GameObject modelRoot, ModelAsset model)
    {
        ImportedNode? correction = model.Nodes.FirstOrDefault(node =>
            node.Key == ImportedModelSpace.CorrectionNodeKey);
        if (correction == null || modelRoot.Scene == null ||
            !Matrix4x4.Invert(correction.LocalTransform, out Matrix4x4 inverse))
            return false;

        // Old scenes put import compensation on Visual/Model. The new asset
        // contains it internally. Insert that internal node and multiply the
        // outer authored pose by its inverse so every child keeps its current
        // effective world matrix. Repeating this is a no-op.
        Matrix4x4 rebasedOuter = inverse * modelRoot.Transform.LocalMatrix;
        if (!Matrix4x4.Decompose(rebasedOuter, out Vector3 scale,
                out Quaternion rotation, out Vector3 position) ||
            !float.IsFinite(scale.X) || !float.IsFinite(scale.Y) ||
            !float.IsFinite(scale.Z))
            return false;

        GameObject[] priorChildren = modelRoot.Children.ToArray();
        var attachedChildren = priorChildren
            .Where(child => !string.IsNullOrWhiteSpace(child.ParentSocket))
            .Select(child => (Object: child, Position: child.Transform.WorldPosition,
                Rotation: child.Transform.WorldRotation, Scale: child.Transform.WorldScale))
            .ToArray();
        GameObject internalNode = modelRoot.Scene.CreateGameObject("Import Space");
        internalNode.SetParent(modelRoot, false);
        foreach (GameObject child in priorChildren)
            if (string.IsNullOrWhiteSpace(child.ParentSocket))
                child.SetParent(internalNode, false);
        ApplyLocalTransform(internalNode, correction.LocalTransform);
        modelRoot.Transform.LocalPosition = position;
        modelRoot.Transform.LocalRotation = rotation;
        modelRoot.Transform.LocalScale = scale;
        foreach (var attached in attachedChildren)
        {
            attached.Object.Transform.WorldPosition = attached.Position;
            attached.Object.Transform.WorldRotation = attached.Rotation;
            attached.Object.Transform.WorldScale = attached.Scale;
        }
        if (modelRoot.GetComponent<ModelHierarchyInstance>() is { } instance)
            instance.AppliedImportScale = 1.0f;
        return true;
    }
    private static void ApplyLocalTransform(
        GameObject gameObject,
        Matrix4x4 matrix)
    {
        if (!Matrix4x4.Decompose(
                matrix,
                out Vector3 scale,
                out Quaternion rotation,
                out Vector3 translation))
        {
            return;
        }

        gameObject.Transform.LocalPosition =
            translation;

        gameObject.Transform.LocalRotation =
            rotation;

        gameObject.Transform.LocalScale =
            scale;
    }

    private static string CreateUniqueName(
        Scene scene,
        string baseName)
    {
        if (scene.FindGameObject(
                baseName) ==
            null)
        {
            return baseName;
        }

        int suffix =
            2;

        while (scene.FindGameObject(
                   $"{baseName} {suffix}") !=
               null)
        {
            suffix++;
        }

        return
            $"{baseName} {suffix}";
    }
}
