using System.Numerics;
using System.Runtime.CompilerServices;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// Quantized pose and geometry sharing for repeated animated actors.
///
/// Evaluation, skinning and submesh merging are performed once per quantized
/// animation frame and then reused by every renderer of the same model. The
/// resulting mesh/material submissions feed the existing RenderBatcher, so
/// copies of one enemy become one instanced draw instead of dozens of private
/// skinned draws.
///
/// Sharing is deliberately conservative:
/// - it only engages when at least two renderers use the same model pool;
/// - a lone character keeps its full-rate private animation;
/// - hidden mesh parts, blends, advanced pose layers, IK/reactions and physical
///   bone deformations keep the private path.
/// </summary>
public sealed partial class SkeletalMeshRenderer
{
    private const int MaxCrowdPosesPerModel = 256;
    private const int MaxCrowdSubmeshesPerDraw = 16;

    private static readonly ConditionalWeakTable<ModelAsset, CrowdPool> CrowdPools = new();

    private bool _shareCrowdPoses = true;

    /// <summary>
    /// Share quantized pose/geometry caches between renderers of the same
    /// model. The cache only activates once a second renderer joins the same
    /// model pool, so solo characters stay smooth.
    /// </summary>
    public bool ShareCrowdPoses
    {
        get => _shareCrowdPoses;
        set
        {
            if (_shareCrowdPoses == value)
            {
                return;
            }

            _shareCrowdPoses = value;

            if (!value)
            {
                ReleaseCrowdPool();
            }

            _poseDirty = true;
        }
    }

    /// <summary>
    /// Temporal resolution of the shared crowd cache. 20 Hz keeps attacks and
    /// locomotion readable while removing most redundant skinning work.
    /// </summary>
    public int CrowdPoseFramesPerSecond { get; set; } = 20;

    /// <summary>
    /// Merge submeshes that use equal opaque materials into one cached mesh.
    /// This is what turns multi-part enemies into a single instanced draw.
    /// </summary>
    public bool BatchCrowdSubmeshes { get; set; } = true;

    private CrowdPool? _crowdPool;
    private ModelAsset? _crowdModel;
    private CrowdDraw[]? _crowdDraws;
    private long _crowdMaterialSignature;
    private bool _crowdMaterialSignatureValid;

    private sealed class CrowdPool
    {
        public int Users;
        public long Hits;
        public long Misses;
        public long Refused;
        public readonly Dictionary<CrowdKey, CrowdPose> Poses = new();
    }

    private readonly record struct CrowdKey(
        ImportedAnimation Clip,
        int Frame,
        int Rate,
        long MaterialSignature);

    private sealed record CrowdPose(
        Matrix4x4[] Globals,
        Matrix4x4 Root,
        Mesh[] Meshes,
        Matrix4x4[] Placement,
        CrowdDraw[] Draws);

    private readonly record struct CrowdDraw(
        Mesh Mesh,
        Matrix4x4 Placement,
        int Slot);

    /// <summary>
    /// Registers this renderer with the shared model pool. Registration is
    /// independent of pose eligibility so the second renderer is enough to
    /// switch every existing renderer of that model onto the shared path.
    /// </summary>
    private void EnsureCrowdPool()
    {
        if (!_shareCrowdPoses || _crowdPool != null || _model == null)
        {
            return;
        }

        _crowdModel = _model;
        _crowdPool = CrowdPools.GetValue(_model, _ => new CrowdPool());
        _crowdPool.Users++;
    }

    private bool CrowdEligible =>
        _shareCrowdPoses &&
        _crowdPool is { Users: >= 2 } &&
        _hiddenMeshKeys.Count == 0 &&
        _model != null &&
        _currentAnimation != null &&
        !HasAdvancedPose &&
        _previousAnimation == null &&
        !_lightRagdollActive &&
        _recoveryDuration <= 0f &&
        _hitReaction <= 0.001f &&
        _physicalBoneDeformations.Count == 0 &&
        _runtimeMeshes.Count > 0 &&
        MaterialAssetSlots.All(asset => asset is not { IsEmpty: false }) &&
        _materialOverrides.Values.All(state => state.IsEmpty);

    private long CrowdMaterialSignature
    {
        get
        {
            if (!_crowdMaterialSignatureValid)
            {
                var hash = new HashCode();

                foreach (RuntimeSkinnedMesh runtime in _runtimeMeshes)
                {
                    hash.Add(
                        MaterialRenderStateComparer.Instance.GetHashCode(
                            runtime.Material));
                }

                _crowdMaterialSignature = hash.ToHashCode();
                _crowdMaterialSignatureValid = true;
            }

            return _crowdMaterialSignature;
        }
    }

    /// <summary>
    /// Attempts to replace a private pose evaluation with a cached crowd pose.
    /// A miss leaves the normal skinning path in charge and reports the exact
    /// key/time that should be stored afterwards.
    /// </summary>
    private bool TryCrowdPose(
        out CrowdKey key,
        out float sampleTime)
    {
        key = default;
        sampleTime = _currentTime;
        _crowdDraws = null;

        foreach (RuntimeSkinnedMesh runtime in _runtimeMeshes)
        {
            runtime.SharedCrowdMesh = null;
        }

        if (!CrowdEligible)
        {
            return false;
        }

        int rate = Math.Clamp(CrowdPoseFramesPerSecond, 8, 30);
        int frame = (int)MathF.Floor(_currentTime * rate);

        key = new CrowdKey(
            _currentAnimation!,
            frame,
            rate,
            CrowdMaterialSignature);

        sampleTime = frame / (float)rate;

        if (!_crowdPool!.Poses.TryGetValue(key, out CrowdPose? pose) ||
            pose.Meshes.Length != _runtimeMeshes.Count)
        {
            _crowdPool.Misses++;
            return false;
        }

        _crowdPool.Hits++;
        _crowdDraws = pose.Draws;
        _currentPoseGlobals = pose.Globals;
        _currentRootMotionCorrection = pose.Root;

        for (int i = 0; i < _runtimeMeshes.Count; i++)
        {
            _runtimeMeshes[i].SharedCrowdMesh = pose.Meshes[i];
            _runtimeMeshes[i].MeshToModelMatrix = pose.Placement[i];
        }

        SkeletalAttachmentService.UpdateForRenderer(this);
        return true;
    }

    /// <summary>
    /// Freezes the just-evaluated pose into shared meshes. The cached geometry
    /// is immutable per quantized frame; scene movement stays in the live
    /// render-time world matrix.
    /// </summary>
    private void StoreCrowdPose(CrowdKey key)
    {
        if (!CrowdEligible || _crowdPool == null)
        {
            return;
        }

        if (_crowdPool.Poses.Count >= MaxCrowdPosesPerModel)
        {
            _crowdPool.Refused++;
            return;
        }

        var meshes = new Mesh[_runtimeMeshes.Count];
        var placement = new Matrix4x4[meshes.Length];

        for (int i = 0; i < meshes.Length; i++)
        {
            RuntimeSkinnedMesh runtime = _runtimeMeshes[i];

            meshes[i] = new Mesh(
                runtime.DeformedVertices.ToArray(),
                runtime.Source.Indices,
                dynamicVertices: true);

            placement[i] = runtime.MeshToModelMatrix;
            runtime.SharedCrowdMesh = meshes[i];
        }

        _crowdDraws = BuildCrowdDraws(meshes, placement);

        _crowdPool.Poses[key] = new CrowdPose(
            _currentPoseGlobals.ToArray(),
            _currentRootMotionCorrection,
            meshes,
            placement,
            _crowdDraws);
    }

    private CrowdDraw[] BuildCrowdDraws(
        Mesh[] meshes,
        Matrix4x4[] placement)
    {
        if (!BatchCrowdSubmeshes ||
            placement.Any(matrix => matrix.GetDeterminant() <= 0.000001f))
        {
            return meshes
                .Select((mesh, index) => new CrowdDraw(mesh, placement[index], index))
                .ToArray();
        }

        var groups = new List<List<int>>();

        for (int i = 0; i < meshes.Length; i++)
        {
            Material material = _runtimeMeshes[i].Material;
            List<int>? group = null;

            if (material.BlendMode == BlendMode3D.Opaque)
            {
                foreach (List<int> candidate in groups)
                {
                    if (candidate.Count >= MaxCrowdSubmeshesPerDraw)
                    {
                        continue;
                    }

                    Material existing = _runtimeMeshes[candidate[0]].Material;

                    if (existing.BlendMode == BlendMode3D.Opaque &&
                        MaterialRenderStateComparer.Instance.Equals(existing, material))
                    {
                        group = candidate;
                        break;
                    }
                }
            }

            if (group == null)
            {
                group = new List<int>();
                groups.Add(group);
            }

            group.Add(i);
        }

        var draws = new List<CrowdDraw>(groups.Count);

        foreach (List<int> group in groups)
        {
            if (group.Count == 1)
            {
                int index = group[0];
                draws.Add(new CrowdDraw(meshes[index], placement[index], index));
                continue;
            }

            draws.Add(
                new CrowdDraw(
                    MergeCrowdGeometry(meshes, placement, group),
                    Matrix4x4.Identity,
                    group[0]));
        }

        return draws.ToArray();
    }

    private static Mesh MergeCrowdGeometry(
        Mesh[] meshes,
        Matrix4x4[] placement,
        IReadOnlyList<int> group)
    {
        int floatCount = 0;
        int indexCount = 0;

        foreach (int index in group)
        {
            floatCount += meshes[index].VertexData.Length;
            indexCount += meshes[index].IndexData.Length;
        }

        var vertices = new float[floatCount];
        var indices = new uint[indexCount];

        int vertexOffset = 0;
        int indexOffset = 0;

        foreach (int index in group)
        {
            ReadOnlySpan<float> source = meshes[index].VertexData.Span;
            uint baseVertex = (uint)(vertexOffset / 8);

            Matrix4x4.Invert(placement[index], out Matrix4x4 inversePlacement);
            Matrix4x4 normalMatrix = Matrix4x4.Transpose(inversePlacement);

            for (int vertex = 0; vertex < source.Length; vertex += 8)
            {
                Vector3 position = Vector3.Transform(
                    new Vector3(source[vertex], source[vertex + 1], source[vertex + 2]),
                    placement[index]);

                Vector3 normal = Vector3.TransformNormal(
                    new Vector3(source[vertex + 3], source[vertex + 4], source[vertex + 5]),
                    normalMatrix);

                if (normal.LengthSquared() > 1e-12f)
                {
                    normal = Vector3.Normalize(normal);
                }

                vertices[vertexOffset++] = position.X;
                vertices[vertexOffset++] = position.Y;
                vertices[vertexOffset++] = position.Z;
                vertices[vertexOffset++] = normal.X;
                vertices[vertexOffset++] = normal.Y;
                vertices[vertexOffset++] = normal.Z;
                vertices[vertexOffset++] = source[vertex + 6];
                vertices[vertexOffset++] = source[vertex + 7];
            }

            foreach (uint sourceIndex in meshes[index].IndexData.Span)
            {
                indices[indexOffset++] = sourceIndex + baseVertex;
            }
        }

        return new Mesh(vertices, indices, dynamicVertices: true);
    }

    /// <summary>
    /// Submits the shared pose meshes. Equal mesh/material pairs across
    /// enemies are collapsed into instanced draws by RenderBatcher.
    /// </summary>
    private bool SubmitCrowdBatches(
        RenderContext context,
        Matrix4x4 world,
        Material? modelOverride)
    {
        if (!CrowdEligible || _crowdDraws == null || _crowdDraws.Length == 0)
        {
            ClearCrowdSharedState();
            return false;
        }

        foreach (CrowdDraw draw in _crowdDraws)
        {
            Material material = modelOverride ?? _runtimeMeshes[draw.Slot].Material;

            context.RenderWorld.Submit(
                draw.Mesh,
                material,
                draw.Placement * world,
                ResolveRenderQueue(material),
                true,
                true,
                true);
        }

        return true;
    }

    private void ClearCrowdSharedState()
    {
        bool hadCrowdDraws = _crowdDraws != null;

        _crowdDraws = null;

        foreach (RuntimeSkinnedMesh runtime in _runtimeMeshes)
        {
            runtime.SharedCrowdMesh = null;
        }

        if (hadCrowdDraws)
        {
            // The next pose evaluation must restore private placements and
            // geometry before the normal path renders again.
            _poseDirty = true;
        }
    }

    private void ReleaseCrowdPool()
    {
        if (_crowdPool != null && --_crowdPool.Users <= 0)
        {
            foreach (CrowdPose pose in _crowdPool.Poses.Values)
            {
                foreach (Mesh mesh in pose.Meshes)
                {
                    mesh.Dispose();
                }

                foreach (CrowdDraw draw in pose.Draws)
                {
                    if (Array.IndexOf(pose.Meshes, draw.Mesh) < 0)
                    {
                        draw.Mesh.Dispose();
                    }
                }
            }

            _crowdPool.Poses.Clear();

            if (_crowdModel != null)
            {
                CrowdPools.Remove(_crowdModel);
            }
        }

        _crowdDraws = null;
        _crowdPool = null;
        _crowdModel = null;
        _crowdMaterialSignatureValid = false;
    }

    /// <summary>
    /// Compact diagnostics snapshot used by profiling tests and editor tools.
    /// </summary>
    internal object CaptureCrowdState() => new
    {
        model = _model?.Name,
        users = _crowdPool?.Users ?? 0,
        poses = _crowdPool?.Poses.Count ?? 0,
        hits = _crowdPool?.Hits ?? 0,
        misses = _crowdPool?.Misses ?? 0,
        refused = _crowdPool?.Refused ?? 0
    };
}
