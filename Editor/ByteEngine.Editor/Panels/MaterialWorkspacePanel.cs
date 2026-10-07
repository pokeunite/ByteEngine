using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

/// <summary>Native-window authoring for .bmat, using the runtime Standard Material path.</summary>
internal sealed class MaterialWorkspacePanel : IDisposable
{
    private readonly EditorDocumentManager _documents;
    private readonly SceneFramebuffer _framebuffer = new();
    private readonly EditorCamera _camera2D = new();
    private readonly EditorCamera3D _camera3D = new();
    private readonly Material _previewMaterial = new();
    private AssetRecord? _asset;
    private EditorProjectContext? _project;
    private MaterialAsset? _draft;
    private EditorDocumentId? _documentId;
    private Scene? _scene;
    private MeshRenderer? _mesh;
    private bool _dirty;
    private bool _previewDirty = true;
    private bool _closePrompt;
    private int _shape;
    private string? _error;

    public MaterialWorkspacePanel(EditorDocumentManager documents) => _documents = documents;

    public void Open(AssetRecord asset, EditorProjectContext project, EditorLog log)
    {
        _asset = asset;
        _project = project;
        _draft = MaterialAssetSerializer.Load(asset.FullPath);
        _documentId = new EditorDocumentId(EditorDocumentType.Material, asset.Guid.ToString("N"));
        EditorDocumentId id = _documentId.Value;
        _documents.RegisterOrFocus(new EditorDocument(id,
            $"Material: {Path.GetFileNameWithoutExtension(asset.ProjectPath)}",
            () => { }, RequestClose,
            () => Save(log), Reload, () => _dirty));
        _camera3D.Yaw = -135f;
        _camera3D.Pitch = -15f;
        _camera3D.FieldOfView = 35f;
        Frame();
    }

    public void Draw(EditorLog log, Renderer2D renderer, Renderer3D renderer3D,
        int windowWidth, int windowHeight)
    {
        if (_asset == null || _project == null || _draft == null) return;
        bool open = true;
        ImGui.Begin($"Material: {_draft.Name}{(_dirty ? " *" : "")}###MaterialWorkspace",
            ref open, ImGuiWindowFlags.MenuBar);
        if (_documentId.HasValue && ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))
            _documents.Activate(_documentId.Value);
        if (ImGui.BeginMenuBar())
        {
            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("Close")) RequestClose();
                if (ImGui.MenuItem("Close Others") && _documentId.HasValue)
                    _documents.RequestCloseOthers(_documentId.Value);
                if (ImGui.MenuItem("Close All")) _documents.RequestCloseAll();
                ImGui.EndMenu();
            }
            ImGui.EndMenuBar();
        }
        if (ImGui.Button("Save")) Save(log);
        ImGui.SameLine();
        if (ImGui.Button("Revert")) Reload();
        if (_dirty)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1f, .65f, .15f, 1f), "UNSAVED");
        }
        if (ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.S, false)) Save(log);
        if (_error != null) ImGui.TextColored(new Vector4(1f, .4f, .35f, 1f), _error);
        ImGui.Separator();

        float inspectorWidth = Math.Clamp(ImGui.GetContentRegionAvail().X * .38f, 320f, 560f);
        ImGui.BeginChild("##MaterialPreviewColumn",
            new Vector2(Math.Max(260f, ImGui.GetContentRegionAvail().X - inspectorWidth - 8f), 0f));
        ImGui.Combo("Shape", ref _shape, new[] { "Sphere", "Cube", "Plane" }, 3);
        if (ImGui.IsItemEdited()) { SetShape(); }
        ImGui.SameLine();
        if (ImGui.Button("Frame")) Frame();
        DrawPreview(renderer, renderer3D, windowWidth, windowHeight);
        ImGui.EndChild();
        ImGui.SameLine();
        ImGui.BeginChild("##MaterialProperties", new Vector2(0f, 0f), ImGuiChildFlags.Borders);
        DrawProperties();
        ImGui.EndChild();
        ImGui.End();
        if (!open) RequestClose();
        DrawClosePrompt(log);
    }

    private void DrawProperties()
    {
        if (_draft == null || _project == null || _asset == null) return;
        MaterialParameters p;
        try { p = ResolveDraft(); _error = null; }
        catch (Exception exception) { _error = exception.Message; p = _draft.Standard; }

        if (_draft.Kind == MaterialAssetKind.Instance)
        {
            ImGui.TextDisabled("Material Instance - checked properties override the parent.");
            AssetReference parent = _draft.ParentMaterial;
            if (AssetPicker("Parent", AssetType.Material, ref parent, _asset.Guid))
            {
                AssetReference old = _draft.ParentMaterial;
                _draft.ParentMaterial = parent;
                try { ResolveDraft(); Changed(); }
                catch (Exception exception) { _draft.ParentMaterial = old; _error = exception.Message; }
            }
            ImGui.Separator();
        }
        if (ImGui.CollapsingHeader("Surface", ImGuiTreeNodeFlags.DefaultOpen))
        {
            EditEnum("Surface Type", nameof(p.SurfaceType), p.SurfaceType,
                value => p.SurfaceType = value);
            EditEnum("Shading", nameof(p.Shading), p.Shading, value => p.Shading = value);
            foreach(var warning in GraphicsAssetAudit.Material(p,r=>_project.AssetDatabase.Resolve(r)?.Type==AssetType.Texture2D))
                ImGui.TextWrapped("Check: "+warning);
            ImGui.TextDisabled("Albedo/emission: sRGB. Normal and PBR maps: linear.");
            EditBool("Double Sided", nameof(p.DoubleSided), p.DoubleSided,
                value => p.DoubleSided = value);
        }
        if (ImGui.CollapsingHeader("Base Color", ImGuiTreeNodeFlags.DefaultOpen))
        {
            EditColor4("Color", nameof(p.BaseColor), p.BaseColor, value => p.BaseColor = value);
            EditTexture("Albedo Texture", nameof(p.BaseColorTexture), p.BaseColorTexture,
                value => p.BaseColorTexture = value);
        }
        if (ImGui.CollapsingHeader("Normal", ImGuiTreeNodeFlags.DefaultOpen))
        {
            EditTexture("Normal Texture", nameof(p.NormalTexture), p.NormalTexture,
                value => p.NormalTexture = value);
            EditFloat("Strength", nameof(p.NormalStrength), p.NormalStrength, 0f, 4f,
                value => p.NormalStrength = value);
            EditEnum("Convention", nameof(p.NormalConvention), p.NormalConvention,
                value => p.NormalConvention = value);
        }
        if (ImGui.CollapsingHeader("Surface Detail", ImGuiTreeNodeFlags.DefaultOpen))
        {
            EditEnum("PBR Map Mode", nameof(p.PbrMapMode), p.PbrMapMode,
                value => p.PbrMapMode = value);
            if (p.PbrMapMode == MaterialPbrMapMode.Packed)
            {
                EditTexture("Packed ORM", nameof(p.PackedPbrTexture), p.PackedPbrTexture,
                    value => p.PackedPbrTexture = value);
                EditEnum("AO Channel", nameof(p.PackedAoChannel), p.PackedAoChannel,
                    value => p.PackedAoChannel = value);
                EditEnum("Roughness Channel", nameof(p.PackedRoughnessChannel),
                    p.PackedRoughnessChannel, value => p.PackedRoughnessChannel = value);
                EditEnum("Metallic Channel", nameof(p.PackedMetallicChannel),
                    p.PackedMetallicChannel, value => p.PackedMetallicChannel = value);
            }
            EditFloat("Metallic", nameof(p.Metallic), p.Metallic, 0f, 1f, value => p.Metallic = value);
            EditFloat("Roughness", nameof(p.Roughness), p.Roughness, .04f, 1f,
                value => p.Roughness = value);
            if (p.PbrMapMode == MaterialPbrMapMode.Separate)
            {
                EditTexture("Metallic Texture", nameof(p.MetallicTexture), p.MetallicTexture,
                    value => p.MetallicTexture = value);
                EditTexture("Roughness Texture", nameof(p.RoughnessTexture), p.RoughnessTexture,
                    value => p.RoughnessTexture = value);
            }
            // glTF commonly packs metallic/roughness while storing occlusion
            // separately, so the AO field must remain available in both modes.
            EditTexture("AO Texture", nameof(p.AmbientOcclusionTexture),
                p.AmbientOcclusionTexture, value => p.AmbientOcclusionTexture = value);
            EditFloat("AO Strength", nameof(p.AmbientOcclusionStrength),
                p.AmbientOcclusionStrength, 0f, 1f, value => p.AmbientOcclusionStrength = value);
        }
        if (ImGui.CollapsingHeader("Emission"))
        {
            EditBool("Enabled", nameof(p.EmissionEnabled), p.EmissionEnabled,
                value => p.EmissionEnabled = value);
            EditColor3("Emission Color", nameof(p.EmissionColor), p.EmissionColor,
                value => p.EmissionColor = value);
            EditTexture("Emission Texture", nameof(p.EmissionTexture), p.EmissionTexture,
                value => p.EmissionTexture = value);
            EditFloat("Intensity", nameof(p.EmissionIntensity), p.EmissionIntensity, 0f, 20f,
                value => p.EmissionIntensity = value);
        }
        if (ImGui.CollapsingHeader("UV"))
        {
            EditUvMapping(p);
            EditVector2("Offset", nameof(p.UvOffset), p.UvOffset, value => p.UvOffset = value);
        }
        if (ImGui.CollapsingHeader("Advanced"))
        {
            EditFloat("Alpha Cutoff", nameof(p.AlphaCutoff), p.AlphaCutoff, 0f, 1f,
                value => p.AlphaCutoff = value);
            EditBool("Depth Test", nameof(p.DepthTest), p.DepthTest, value => p.DepthTest = value);
            EditEnum("Depth Write", nameof(p.DepthWriteMode), p.DepthWriteMode,
                value => p.DepthWriteMode = value);
            EditEnum("Cull Mode", nameof(p.CullMode), p.CullMode, value => p.CullMode = value);
            EditEnum("Front Face", nameof(p.FrontFace), p.FrontFace, value => p.FrontFace = value);
            EditEnum("Polygon Mode", nameof(p.PolygonMode), p.PolygonMode,
                value => p.PolygonMode = value);
        }
    }

    /// <summary>
    /// Keeps the v1 .bmat schema backward compatible while exposing a simple
    /// Unreal-style world-aligned mapping mode.
    ///
    /// Mesh UV:
    ///     UvTiling contains normal positive tiling values.
    ///
    /// World Aligned:
    ///     both UvTiling components are negative and abs(X) is interpreted by
    ///     Shader3D as world units per texture repeat.
    ///
    /// The editor owns this compact encoding so users never need to type
    /// negative UV values themselves.
    /// </summary>
    private void EditUvMapping(MaterialParameters p)
    {
        Vector2 stored =
            p.UvTiling;

        bool enabled =
            Allow(
                nameof(p.UvTiling),
                stored);

        ImGui.BeginDisabled(
            !enabled);

        bool worldAligned =
            stored.X < 0f &&
            stored.Y < 0f;

        int mode =
            worldAligned
                ? 1
                : 0;

        string[] modes =
        {
            "Mesh UV",
            "World Aligned"
        };

        if (ImGui.Combo(
                "Mapping",
                ref mode,
                modes,
                modes.Length))
        {
            if (mode == 1)
            {
                float worldScale =
                    Math.Max(
                        .01f,
                        MathF.Abs(
                            stored.X));

                stored =
                    new Vector2(
                        -worldScale,
                        -worldScale);
            }
            else
            {
                stored =
                    new Vector2(
                        Math.Max(
                            .01f,
                            MathF.Abs(
                                stored.X)),
                        Math.Max(
                            .01f,
                            MathF.Abs(
                                stored.Y)));
            }

            Commit(
                nameof(p.UvTiling),
                stored,
                value => p.UvTiling = value);

            worldAligned =
                mode == 1;
        }

        if (worldAligned)
        {
            float worldScale =
                Math.Max(
                    .01f,
                    MathF.Abs(
                        stored.X));

            if (ImGui.DragFloat(
                    "World Scale (units/tile)",
                    ref worldScale,
                    .05f))
            {
                worldScale =
                    Math.Clamp(
                        worldScale,
                        .01f,
                        1000f);

                stored =
                    new Vector2(
                        -worldScale,
                        -worldScale);

                Commit(
                    nameof(p.UvTiling),
                    stored,
                    value => p.UvTiling = value);
            }

            ImGui.TextDisabled(
                "World-space triplanar mapping. Scale = world units per texture repeat.");

            ImGui.TextDisabled(
                "Best for ground, walls, rocks and Megascans. Moving meshes may texture-swim.");
        }
        else
        {
            Vector2 tiling =
                new(
                    Math.Max(
                        .01f,
                        MathF.Abs(
                            stored.X)),
                    Math.Max(
                        .01f,
                        MathF.Abs(
                            stored.Y)));

            if (ImGui.DragFloat2(
                    "Tiling",
                    ref tiling,
                    .01f))
            {
                tiling =
                    new Vector2(
                        Math.Clamp(
                            tiling.X,
                            .01f,
                            1000f),
                        Math.Clamp(
                            tiling.Y,
                            .01f,
                            1000f));

                stored =
                    tiling;

                Commit(
                    nameof(p.UvTiling),
                    stored,
                    value => p.UvTiling = value);
            }
        }

        ImGui.EndDisabled();
    }

    private MaterialParameters ResolveDraft()
    {
        if (_draft == null || _asset == null || _project == null) return new MaterialParameters();
        if (_draft.Kind == MaterialAssetKind.Standard) return _draft.Standard;
        return MaterialAssetSerializer.Resolve(
            new AssetReference(_asset.Guid, _asset.ProjectPath),
            reference => reference.Guid == _asset.Guid
                ? _draft : _project.Assets.LoadMaterialAsset(reference));
    }

    private bool Allow(string property, object current)
    {
        if (_draft?.Kind != MaterialAssetKind.Instance) return true;
        bool enabled = _draft.HasOverride(property);
        if (ImGui.Checkbox($"##Override{property}", ref enabled))
        {
            if (enabled) _draft.SetOverride(property, current);
            else _draft.RemoveOverride(property);
            Changed();
        }
        ImGui.SameLine();
        return enabled;
    }
    private void Commit<T>(string property, T value, Action<T> set)
    {
        if (_draft?.Kind == MaterialAssetKind.Instance) _draft.SetOverride(property, value);
        else set(value);
        Changed();
    }
    private void EditFloat(string label, string property, float value, float min, float max,
        Action<float> set)
    {
        bool enabled = Allow(property, value);
        ImGui.BeginDisabled(!enabled);
        if (ImGui.SliderFloat(label, ref value, min, max)) Commit(property, value, set);
        ImGui.EndDisabled();
    }
    private void EditBool(string label, string property, bool value, Action<bool> set)
    {
        bool enabled = Allow(property, value);
        ImGui.BeginDisabled(!enabled);
        if (ImGui.Checkbox(label, ref value)) Commit(property, value, set);
        ImGui.EndDisabled();
    }
    private void EditEnum<T>(string label, string property, T value, Action<T> set)
        where T : struct, Enum
    {
        bool enabled = Allow(property, value);
        int index = Convert.ToInt32(value);
        string[] names = Enum.GetNames<T>();
        ImGui.BeginDisabled(!enabled);
        if (ImGui.Combo(label, ref index, names, names.Length))
            Commit(property, (T)Enum.ToObject(typeof(T), index), set);
        ImGui.EndDisabled();
    }
    private void EditColor4(string label, string property, Vector4 value, Action<Vector4> set)
    {
        bool enabled = Allow(property, value);
        ImGui.BeginDisabled(!enabled);
        if (ImGui.ColorEdit4(label, ref value)) Commit(property, value, set);
        ImGui.EndDisabled();
    }
    private void EditColor3(string label, string property, Vector3 value, Action<Vector3> set)
    {
        bool enabled = Allow(property, value);
        ImGui.BeginDisabled(!enabled);
        if (ImGui.ColorEdit3(label, ref value)) Commit(property, value, set);
        ImGui.EndDisabled();
    }
    private void EditVector2(string label, string property, Vector2 value, Action<Vector2> set)
    {
        bool enabled = Allow(property, value);
        ImGui.BeginDisabled(!enabled);
        if (ImGui.DragFloat2(label, ref value, .01f)) Commit(property, value, set);
        ImGui.EndDisabled();
    }
    private void EditTexture(string label, string property, AssetReference value,
        Action<AssetReference> set)
    {
        bool enabled = Allow(property, value);
        ImGui.BeginDisabled(!enabled);
        if (AssetPicker(label, AssetType.Texture2D, ref value, Guid.Empty))
            Commit(property, value, set);
        ImGui.EndDisabled();
    }
    private bool AssetPicker(string label, AssetType type, ref AssetReference value, Guid exclude)
    {
        if (_project == null) return false;
        bool changed = false;
        AssetRecord? current = value.IsEmpty ? null : _project.AssetDatabase.Resolve(value);
        if (ImGui.BeginCombo(label, current?.ProjectPath ?? "None"))
        {
            if (ImGui.Selectable("None", value.IsEmpty))
            { value = AssetReference.Empty; changed = true; }
            foreach (AssetRecord item in _project.AssetDatabase.Assets
                .Where(item => item.Type == type && item.Guid != exclude)
                .OrderBy(item => item.ProjectPath, StringComparer.OrdinalIgnoreCase))
            {
                if (ImGui.Selectable(item.ProjectPath, item.Guid == current?.Guid))
                { value = new AssetReference(item.Guid, item.ProjectPath); changed = true; }
            }
            ImGui.EndCombo();
        }
        if (ImGui.BeginDragDropTarget())
        {
            Guid? dropped = AssetDragDrop.Accept();
            if (dropped.HasValue && dropped.Value != exclude &&
                _project.AssetDatabase.TryGetAsset(dropped.Value, out AssetRecord? item) &&
                item?.Type == type)
            { value = new AssetReference(item.Guid, item.ProjectPath); changed = true; }
            ImGui.EndDragDropTarget();
        }
        return changed;
    }

    private void EnsurePreview()
    {
        if (_scene != null || _project == null) return;
        _scene = new Scene("Material Preview", _project.Project.Classification);
        GameObject object3D = _scene.CreateGameObject("Preview Mesh");
        _mesh = object3D.AddComponent(new MeshRenderer
        {
            UsePrimitive = true, Primitive = PrimitiveMeshType.Sphere,
            Material = _previewMaterial, FrustumCulling = false
        });
        GameObject light = _scene.CreateGameObject("Preview Light");
        light.Transform.EulerAngles = new Vector3(-35f, -40f, 0f);
        light.AddComponent(new DirectionalLight
        {
            Intensity = 1.4f, AmbientIntensity = .25f, CastShadows = false
        });
        _scene.LoadInternal();
    }
    private void DrawPreview(Renderer2D renderer, Renderer3D renderer3D,
        int windowWidth, int windowHeight)
    {
        if (_project == null) return;
        EnsurePreview();
        if (_scene == null || _mesh == null) return;
        if (_previewDirty)
        {
            try
            {
                _project.Assets.ApplyMaterialParameters(ResolveDraft(), _previewMaterial);
                _framebuffer.Render(renderer, renderer3D, _scene, EditorMode.Edit,
                    _camera2D, _camera3D, true, 512, 320, windowWidth, windowHeight,
                    drawGrid3D: false, prepareEnvironmentLighting3D: false,
                    renderShadows3D: false);
                _previewDirty = false;
            }
            catch (Exception exception) { _error = exception.Message; }
        }
        float width = Math.Max(200f, Math.Min(ImGui.GetContentRegionAvail().X, 800f));
        ImGui.Image(_framebuffer.TextureId, new Vector2(width, width * 320f / 512f),
            new Vector2(0f, 1f), new Vector2(1f, 0f));
        if (ImGui.IsItemHovered())
        {
            bool moved = false;
            if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                Vector2 delta = ImGui.GetIO().MouseDelta;
                _camera3D.Yaw += delta.X * .3f;
                _camera3D.Pitch = Math.Clamp(_camera3D.Pitch - delta.Y * .3f, -80f, 80f);
                moved = true;
            }
            float wheel = ImGui.GetIO().MouseWheel;
            if (MathF.Abs(wheel) > .001f)
            { _cameraDistance = Math.Clamp(_cameraDistance * MathF.Pow(.88f, wheel), 1f, 12f); moved = true; }
            if (moved) { PositionCamera(); _previewDirty = true; }
        }
        ImGui.TextDisabled("LMB orbit  |  Wheel zoom  |  Frame resets view");
    }
    private float _cameraDistance = 3f;
    private void Frame()
    {
        _cameraDistance = _shape == 2 ? 2.5f : 3f;
        PositionCamera();
        _previewDirty = true;
    }
    private void PositionCamera() => _camera3D.Position = -_camera3D.Forward * _cameraDistance;
    private void SetShape()
    {
        if (_mesh != null)
            _mesh.Primitive = _shape switch
            { 1 => PrimitiveMeshType.Cube, 2 => PrimitiveMeshType.Plane, _ => PrimitiveMeshType.Sphere };
        Frame();
    }
    private void Changed() { _dirty = true; _previewDirty = true; }

    private bool Save(EditorLog log)
    {
        if (_asset == null || _project == null || _draft == null) return false;
        try
        {
            ResolveDraft();
            MaterialAssetSerializer.Save(_asset.FullPath, _draft);
            _project.Assets.ReloadMaterialAsset(new AssetReference(_asset.Guid, _asset.ProjectPath));
            _dirty = false;
            _error = null;
            log.Info($"Saved Material '{_asset.ProjectPath}'.");
            return true;
        }
        catch (Exception exception)
        {
            _error = exception.Message;
            log.Error($"Could not save Material: {exception.Message}");
            return false;
        }
    }
    private void Reload()
    {
        if (_asset == null) return;
        try
        {
            _draft = MaterialAssetSerializer.Load(_asset.FullPath);
            _dirty = false;
            _error = null;
            _previewDirty = true;
        }
        catch (Exception exception) { _error = exception.Message; }
    }
    private void RequestClose()
    {
        if (_dirty) _closePrompt = true;
        else CloseNow();
    }
    private void CloseNow()
    {
        if (_documentId.HasValue) _documents.Unregister(_documentId.Value);
        _documentId = null;
    }
    private void DrawClosePrompt(EditorLog log)
    {
        if (_closePrompt) { ImGui.OpenPopup("Unsaved Material"); _closePrompt = false; }
        if (!ImGui.BeginPopupModal("Unsaved Material", ImGuiWindowFlags.AlwaysAutoResize)) return;
        ImGui.Text("Save material changes before closing?");
        if (ImGui.Button("Save") && Save(log)) { CloseNow(); ImGui.CloseCurrentPopup(); }
        ImGui.SameLine();
        if (ImGui.Button("Discard")) { Reload(); CloseNow(); ImGui.CloseCurrentPopup(); }
        ImGui.SameLine();
        if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }
    public void Dispose()
    {
        if (_scene?.IsLoaded == true) _scene.UnloadInternal();
        _scene = null;
        _mesh = null;
        _framebuffer.Dispose();
        CloseNow();
    }
}
