using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>A deterministic, editor-visible patch; generated plants are render submissions, not scene objects.</summary>
public sealed class FoliagePatch : Component
{
    private float _brushRadius = 1, _paintDensity = 3;
    public float BrushRadius { get => _brushRadius; set => _brushRadius = Safe(value, 1, .1f, 20); }
    public float PaintDensity { get => _paintDensity; set => _paintDensity = Safe(value, 3, .1f, 50); }
    public bool UsePaintedLayout { get; set; }
    private readonly List<Vector4> _painted = new();
    public IReadOnlyList<Vector4> PaintedPlants => _painted.AsReadOnly();
    public void RestorePaintedPlants(IEnumerable<Vector4> plants)
    {
        _painted.Clear();
        _painted.AddRange(plants.Where(p => float.IsFinite(p.X) && float.IsFinite(p.Y) &&
            float.IsFinite(p.Z) && float.IsFinite(p.W)).Take(2000));
        Rebuild();
    }
    public bool PaintPlant(Vector3 worldPosition, float yaw, float spacing)
    {
        if (_painted.Count >= 2000 || !Matrix4x4.Invert(Transform.WorldMatrix, out var inverse)) return false;
        if (_painted.Any(p => Vector3.DistanceSquared(Vector3.Transform(new(p.X,p.Y,p.Z),
            Transform.WorldMatrix), worldPosition) < spacing * spacing)) return false;
        Vector3 local = Vector3.Transform(worldPosition, inverse);
        _painted.Add(new(local, yaw));
        Rebuild(); return true;
    }
    public int ErasePlants(Vector3 worldCenter, float radius)
    {
        int removed = _painted.RemoveAll(p => Vector3.DistanceSquared(
            Vector3.Transform(new(p.X,p.Y,p.Z), Transform.WorldMatrix), worldCenter) <= radius * radius);
        if (removed > 0) Rebuild();
        return removed;
    }
    public AssetReference Model { get; set; } = AssetReference.Empty;
    public AssetReference MaterialAsset { get; set; } = AssetReference.Empty;
    private Vector2 _area = new(10, 10);
    public Vector2 Area { get => _area; set => _area = new(Safe(value.X, 10, .1f, 500), Safe(value.Y, 10, .1f, 500)); }
    private int _amount = 100;
    public int Amount { get => _amount; set => _amount = Math.Clamp(value, 0, 2000); }
    public int Seed { get; set; } = 1;
    private float _height = 1;
    public float PlantHeight { get => _height; set => _height = Safe(value, 1, .01f, 100); }
    private float _variation = .2f;
    public float SizeVariation { get => _variation; set => _variation = Safe(value, .2f, 0, .9f); }
    public bool SnapToGround { get; set; } = true;
    public bool WindEnabled { get; set; } = true;
    private float _strength = 3, _speed = 1, _distance = 80;
    public float WindStrength { get => _strength; set => _strength = Safe(value, 3, 0, 20); }
    public float WindSpeed { get => _speed; set => _speed = Safe(value, 1, 0, 10); }
    public float ViewDistance { get => _distance; set => _distance = Safe(value, 80, 1, 1000); }
    public bool CastShadows { get; set; } = true;

    private sealed record Part(Mesh Mesh, Material Material, Matrix4x4 Node);
    private Part[] _parts = Array.Empty<Part>();
    private Matrix4x4[] _placements = Array.Empty<Matrix4x4>();
    private ModelAsset? _loadedModel;
    private AssetManager? _assets;
    private AssetReference _modelKey = AssetReference.Empty, _materialKey = AssetReference.Empty;
    private (Vector2, int, int, float, float, bool, bool, Matrix4x4) _layoutKey;
    private bool _rebuild = true;
    private string? _lastError;
    public string Status { get; private set; } = "Choose a static plant model.";
    public void Rebuild() => _rebuild = true;

    public BoundingBox3D GetWorldBounds()
    {
        Vector3 min = new(-Area.X * .5f, 0, -Area.Y * .5f);
        Vector3 max = new(Area.X * .5f, PlantHeight * (1 + SizeVariation), Area.Y * .5f);
        foreach (Matrix4x4 placement in _placements)
        foreach (Part part in _parts)
        {
            BoundingBox3D bounds = part.Mesh.LocalBounds.Transform(part.Node * placement);
            min = Vector3.Min(min, bounds.Minimum); max = Vector3.Max(max, bounds.Maximum);
        }
        float padding = PlantHeight * (1 + SizeVariation) * .4f;
        return new BoundingBox3D(min - new Vector3(padding), max + new Vector3(padding))
            .Transform(Transform.WorldMatrix);
    }

    private static float Safe(float value, float fallback, float min, float max) =>
        Math.Clamp(float.IsFinite(value) ? value : fallback, min, max);

    public static Matrix4x4[] CreateLayout(Vector2 area, int amount, int seed, float height, float variation)
    {
        var random = new Random(seed);
        var result = new Matrix4x4[Math.Clamp(amount, 0, 2000)];
        for (int i = 0; i < result.Length; i++)
        {
            float x = ((float)random.NextDouble() - .5f) * area.X;
            float z = ((float)random.NextDouble() - .5f) * area.Y;
            float yaw = (float)random.NextDouble() * MathF.Tau;
            float scale = height * (1 + ((float)random.NextDouble() * 2 - 1) * variation);
            result[i] = Matrix4x4.CreateScale(Math.Max(.001f, scale)) *
                Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(x, 0, z);
        }
        return result;
    }

    public static Matrix4x4 WindSway(float time, int index, float degrees, float speed)
    {
        float phase = index * 2.399963f;
        float angle = MathF.Sin(time * speed + phase) * degrees * MathF.PI / 180;
        return Matrix4x4.CreateRotationZ(angle) *
            Matrix4x4.CreateRotationX(MathF.Sin(time * speed * .73f + phase) * angle * .35f);
    }

    protected override void OnRender(RenderContext context)
    {
        if (!context.Has3DCamera || Model.IsEmpty) return;
        if (!AnimationRuntimeAssets.TryGet(out AssetManager? assets) || assets == null) return;
        try
        {
            ModelAsset model = assets.LoadModel(Model);
            if (_assets != assets || !ReferenceEquals(_loadedModel, model) ||
                _modelKey != Model || _materialKey != MaterialAsset)
            {
                _parts = LoadParts(model, assets);
                _loadedModel = model; _assets = assets; _modelKey = Model; _materialKey = MaterialAsset;
                _rebuild = true;
            }
            var key = (Area, Amount, Seed, PlantHeight, SizeVariation, SnapToGround, UsePaintedLayout, Transform.WorldMatrix);
            if (_rebuild || key != _layoutKey)
            {
                BuildPlacements(context);
                _layoutKey = key; _rebuild = false;
            }
            Vector3 camera = context.CaptureRenderView3D()?.CameraPosition ?? Vector3.Zero;
            for (int i = 0; i < _placements.Length; i++)
            {
                Matrix4x4 world = _placements[i] * Transform.WorldMatrix;
                if (Vector3.DistanceSquared(world.Translation, camera) > ViewDistance * ViewDistance) continue;
                Matrix4x4 sway = WindEnabled ? WindSway((float)Time.TotalTime, i, WindStrength, WindSpeed) : Matrix4x4.Identity;
                foreach (Part part in _parts)
                {
                    Material material = part.Material;
                    context.RenderWorld.Submit(part.Mesh, material, part.Node * sway * world,
                        material.BlendMode == BlendMode3D.AlphaBlend || material.BlendMode == BlendMode3D.Additive
                            ? RenderQueue3D.Transparent : RenderQueue3D.Opaque,
                        true, CastShadows, true);
                }
            }
            _lastError = null;
        }
        catch (Exception error)
        {
            Status = "Cannot load foliage: " + error.Message;
            if (_lastError != Status) CrashDebugLog.Write(Status);
            _lastError = Status;
        }
    }

    private Part[] LoadParts(ModelAsset model, AssetManager assets)
    {
        if (model.Meshes.Any(mesh => mesh.JointWeights.Length > 0))
            throw new InvalidOperationException("Choose a static plant model, not an animated character.");
        var nodes = model.Nodes.ToDictionary(node => node.Key);
        var matrices = new Dictionary<string, Matrix4x4>();
        Matrix4x4 Resolve(ImportedNode node, HashSet<string> visiting)
        {
            if (matrices.TryGetValue(node.Key, out Matrix4x4 value)) return value;
            if (!visiting.Add(node.Key)) throw new InvalidDataException("Model node hierarchy contains a cycle.");
            Matrix4x4 parent = node.ParentKey != null && nodes.TryGetValue(node.ParentKey, out ImportedNode? parentNode)
                ? Resolve(parentNode, visiting) : Matrix4x4.Identity;
            visiting.Remove(node.Key);
            return matrices[node.Key] = node.LocalTransform * parent;
        }
        var parts = new List<Part>();
        foreach (ImportedNode node in model.Nodes)
        foreach (string meshKey in node.MeshKeys)
        {
            ImportedMesh source = model.Meshes.First(mesh => mesh.Key == meshKey);
            Material material = !MaterialAsset.IsEmpty ? assets.LoadMaterial(MaterialAsset) :
                source.MaterialKey != null ? assets.GetModelMaterial(Model, source.MaterialKey) : new Material();
            parts.Add(new Part(assets.GetModelMesh(Model, meshKey), material, Resolve(node, new HashSet<string>())));
        }
        if (parts.Count == 0) throw new InvalidDataException("This model contains no static render meshes.");
        // Normalize the entire imported hierarchy, retaining all sub-mesh transforms.
        Vector3 min = new(float.PositiveInfinity), max = new(float.NegativeInfinity);
        foreach (Part part in parts)
        {
            BoundingBox3D bounds = part.Mesh.LocalBounds.Transform(part.Node);
            min = Vector3.Min(min, bounds.Minimum); max = Vector3.Max(max, bounds.Maximum);
        }
        float height = Math.Max(.001f, max.Y - min.Y);
        Vector3 center = new((min.X + max.X) * .5f, min.Y, (min.Z + max.Z) * .5f);
        Matrix4x4 normalize = Matrix4x4.CreateTranslation(-center) * Matrix4x4.CreateScale(1 / height);
        return parts.Select(part => part with { Node = part.Node * normalize }).ToArray();
    }

    private void BuildPlacements(RenderContext context)
    {
        if (UsePaintedLayout)
        {
            var random = new Random(Seed);
            _placements = _painted.Select(p => Matrix4x4.CreateScale(PlantHeight *
                (1 + ((float)random.NextDouble() * 2 - 1) * SizeVariation)) *
                Matrix4x4.CreateRotationY(p.W) * Matrix4x4.CreateTranslation(p.X,p.Y,p.Z)).ToArray();
            Status = $"{_placements.Length}/2000 painted plants; {_parts.Length} mesh parts per plant.";
            return;
        }
        _placements = CreateLayout(Area, Amount, Seed, PlantHeight, SizeVariation);
        int grounded = 0;
        if (SnapToGround && Matrix4x4.Invert(Transform.WorldMatrix, out Matrix4x4 inverse))
        {
            for (int i = 0; i < _placements.Length; i++)
            {
                Vector3 position = Vector3.Transform(_placements[i].Translation, Transform.WorldMatrix);
                if (!GameplayQuery3D.Raycast(context.Scene, position + Vector3.UnitY * 20, -Vector3.UnitY,
                    out RaycastHit3D hit, 40, ignore: GameObject, includeTriggers: false)) continue;
                Matrix4x4 placement = _placements[i];
                placement.Translation = Vector3.Transform(hit.Point, inverse);
                _placements[i] = placement; grounded++;
            }
        }
        Status = $"{_placements.Length} plants; {_parts.Length} mesh parts per plant. " +
            (SnapToGround ? $"{grounded} snapped to colliders; others use patch height." : "Using patch height.");
    }

    protected override void OnDestroy()
    {
        // AssetManager owns the shared GPU resources.
        _parts = Array.Empty<Part>(); _placements = Array.Empty<Matrix4x4>();
        _loadedModel = null; _assets = null;
    }
}
