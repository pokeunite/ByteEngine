using System.Numerics;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// Runtime skeletal mesh playback for ByteEngine v0.11-C1.
///
/// The first foundation slice intentionally uses CPU skinning and submits the
/// deformed mesh through ByteEngine's existing stable Mesh/RenderWorld path.
/// Shader3D, Renderer3D, IBL, shadows, physics and audio are not rewritten.
///
/// Locomotion clips are kept in-place for v0.11-C1: accumulated horizontal
/// root translation is removed so animation cannot drag the visual mesh away
/// from the CharacterController3D/camera root.
///
/// Once animation behaviour is stable this component can move skinning to the
/// GPU without changing imported clips, controller logic or authoring data.
/// </summary>
public sealed class SkeletalMeshRenderer : Component
{
    private readonly List<RuntimeSkinnedMesh> _runtimeMeshes = new();

    private ModelAsset? _model;
    private SkeletonAsset? _skeleton;

    private ImportedNode[] _nodes = Array.Empty<ImportedNode>();
    private int[] _parentIndices = Array.Empty<int>();
    private int[] _boneNodeIndices = Array.Empty<int>();

    /*
     * Nodes that can carry locomotion root translation. Until ByteEngine's
     * dedicated Root Motion phase is implemented, horizontal clip travel must
     * be removed from these nodes so CharacterController3D remains the sole
     * owner of world movement.
     */
    private bool[] _rootMotionNodes = Array.Empty<bool>();

    private Vector3[] _basePositions = Array.Empty<Vector3>();
    private Quaternion[] _baseRotations = Array.Empty<Quaternion>();
    private Vector3[] _baseScales = Array.Empty<Vector3>();

    private ImportedAnimation? _currentAnimation;
    private ImportedAnimation? _previousAnimation;

    private float _currentTime;
    private float _previousTime;
    private float _transitionElapsed;
    private float _activeTransitionDuration;

    private bool _currentLoop = true;
    private bool _previousLoop = true;

    private bool _resolved;
    private bool _poseDirty = true;

    public AssetReference Model { get; set; } =
        AssetReference.Empty;

    public string? SkeletonKey { get; set; }

    /// <summary>
    /// Optional material overrides matched to skinned mesh order. Empty entries
    /// use the imported material assigned to the source mesh.
    /// </summary>
    public List<string> MaterialKeys { get; set; } = new();

    public bool Visible { get; set; } = true;

    public string DefaultAnimation { get; set; } = string.Empty;

    public bool PlayOnStart { get; set; }

    public bool Loop { get; set; } = true;

    public float Speed { get; set; } = 1.0f;

    public float TransitionDuration { get; set; } = 0.15f;

    public string CurrentAnimation =>
        _currentAnimation?.Name ?? string.Empty;

    public bool IsPlaying { get; private set; }

    public float PlaybackTime => _currentTime;

    public float Duration =>
        _currentAnimation?.Duration ?? 0.0f;

    public bool ModelLoaded =>
        _resolved && _model != null;

    public int SkinnedMeshCount =>
        _runtimeMeshes.Count;

    public IReadOnlyList<string> AnimationNames =>
        _model?.Animations
            .Select(animation => animation.Name)
            .ToArray()
        ?? Array.Empty<string>();

    protected override void OnStart()
    {
        ResolveRuntimeResources();

        if (!PlayOnStart || _model == null)
        {
            return;
        }

        string clipName =
            !string.IsNullOrWhiteSpace(DefaultAnimation)
                ? DefaultAnimation
                : _model.Animations.FirstOrDefault()?.Name
                  ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(clipName))
        {
            Play(clipName, Loop, 0.0f);
        }
    }

    protected override void OnUpdate()
    {
        if (!_resolved)
        {
            ResolveRuntimeResources();
        }

        if (_currentAnimation == null)
        {
            if (_poseDirty)
            {
                UpdatePoseAndMeshes();
            }

            return;
        }

        float delta =
            Math.Max(
                (float)ByteEngine.Core.Time.DeltaTime,
                0.0f) *
            Math.Max(Speed, 0.0f);

        if (IsPlaying && delta > 0.0f)
        {
            _currentTime =
                AdvanceTime(
                    _currentTime,
                    delta,
                    _currentAnimation.Duration,
                    _currentLoop,
                    out bool finished);

            if (finished)
            {
                IsPlaying = false;
            }

            if (_previousAnimation != null)
            {
                _previousTime =
                    AdvanceTime(
                        _previousTime,
                        delta,
                        _previousAnimation.Duration,
                        _previousLoop,
                        out _);

                _transitionElapsed += delta;

                if (_activeTransitionDuration <= 0.000001f ||
                    _transitionElapsed >= _activeTransitionDuration)
                {
                    _previousAnimation = null;
                    _transitionElapsed = 0.0f;
                    _activeTransitionDuration = 0.0f;
                }
            }

            _poseDirty = true;
        }

        if (_poseDirty)
        {
            UpdatePoseAndMeshes();
        }
    }

    protected override void OnRender(RenderContext context)
    {
        if (!Visible || !context.Has3DCamera)
        {
            return;
        }

        if (!_resolved)
        {
            ResolveRuntimeResources();
        }

        if (_poseDirty)
        {
            UpdatePoseAndMeshes();
        }

        foreach (RuntimeSkinnedMesh runtime in _runtimeMeshes)
        {
            context.RenderWorld.Submit(
                runtime.Mesh,
                runtime.Material,
                runtime.ModelMatrix,
                ResolveRenderQueue(runtime.Material),
                true,
                true,
                true);
        }
    }

    protected override void OnDestroy()
    {
        DisposeRuntimeMeshes();
    }

    /// <summary>
    /// Loads the model/skeleton and creates private dynamic meshes.
    /// Safe to call repeatedly.
    /// </summary>
    public bool ResolveRuntimeResources()
    {
        if (_resolved)
        {
            return _model != null && _runtimeMeshes.Count > 0;
        }

        if (Model.IsEmpty ||
            !AnimationRuntimeAssets.TryGet(out AssetManager? assets) ||
            assets == null)
        {
            return false;
        }

        try
        {
            ModelAsset model = assets.LoadModel(Model);
            SkeletonAsset? skeleton = model.Skeleton;

            _model = model;

            if (skeleton == null)
            {
                _resolved = true;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(SkeletonKey) &&
                !string.Equals(
                    skeleton.Key,
                    SkeletonKey,
                    StringComparison.Ordinal))
            {
                _resolved = true;
                return false;
            }

            _skeleton = skeleton;
            SkeletonKey = skeleton.Key;

            BuildNodeRuntime();
            BuildRuntimeMeshes(assets);

            _resolved = true;
            _poseDirty = true;

            return _runtimeMeshes.Count > 0;
        }
        catch
        {
            _resolved = false;
            return false;
        }
    }

    public bool HasAnimation(string clipName)
    {
        if (!_resolved)
        {
            ResolveRuntimeResources();
        }

        return FindAnimation(clipName) != null;
    }

    public bool Play(
        string clipName,
        bool? loop = null,
        float? transitionDuration = null)
    {
        if (string.IsNullOrWhiteSpace(clipName))
        {
            return false;
        }

        if (!_resolved)
        {
            ResolveRuntimeResources();
        }

        ImportedAnimation? animation =
            FindAnimation(clipName);

        if (animation == null)
        {
            return false;
        }

        bool requestedLoop = loop ?? Loop;

        if (ReferenceEquals(animation, _currentAnimation))
        {
            _currentLoop = requestedLoop;
            Loop = requestedLoop;
            IsPlaying = true;
            return true;
        }

        float blend =
            Math.Max(
                transitionDuration ?? TransitionDuration,
                0.0f);

        if (_currentAnimation != null &&
            blend > 0.000001f)
        {
            _previousAnimation = _currentAnimation;
            _previousTime = _currentTime;
            _previousLoop = _currentLoop;
            _transitionElapsed = 0.0f;
            _activeTransitionDuration = blend;
        }
        else
        {
            _previousAnimation = null;
            _previousTime = 0.0f;
            _transitionElapsed = 0.0f;
            _activeTransitionDuration = 0.0f;
        }

        _currentAnimation = animation;
        _currentTime = 0.0f;
        _currentLoop = requestedLoop;
        Loop = requestedLoop;
        IsPlaying = true;
        _poseDirty = true;

        UpdatePoseAndMeshes();

        return true;
    }

    public void Pause()
    {
        IsPlaying = false;
    }

    public void Resume()
    {
        if (_currentAnimation != null)
        {
            IsPlaying = true;
        }
    }

    public void Stop()
    {
        IsPlaying = false;
        _currentAnimation = null;
        _previousAnimation = null;
        _currentTime = 0.0f;
        _previousTime = 0.0f;
        _transitionElapsed = 0.0f;
        _activeTransitionDuration = 0.0f;
        _poseDirty = true;

        UpdatePoseAndMeshes();
    }

    private ImportedAnimation? FindAnimation(string clipName)
    {
        return
            _model?.Animations.FirstOrDefault(
                animation =>
                    string.Equals(
                        animation.Name,
                        clipName,
                        StringComparison.Ordinal))
            ?? _model?.Animations.FirstOrDefault(
                animation =>
                    string.Equals(
                        animation.Name,
                        clipName,
                        StringComparison.OrdinalIgnoreCase));
    }

    private void BuildNodeRuntime()
    {
        if (_model == null || _skeleton == null)
        {
            return;
        }

        _nodes = _model.Nodes.ToArray();
        _parentIndices = new int[_nodes.Length];
        Array.Fill(_parentIndices, -1);

        var byKey =
            _nodes
                .Select((node, index) => (node.Key, index))
                .ToDictionary(
                    item => item.Key,
                    item => item.index,
                    StringComparer.Ordinal);

        var byName =
            new Dictionary<string, int>(StringComparer.Ordinal);

        var byNameIgnoreCase =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        _basePositions = new Vector3[_nodes.Length];
        _baseRotations = new Quaternion[_nodes.Length];
        _baseScales = new Vector3[_nodes.Length];

        for (int index = 0; index < _nodes.Length; index++)
        {
            ImportedNode node = _nodes[index];

            if (node.ParentKey != null &&
                byKey.TryGetValue(
                    node.ParentKey,
                    out int parentIndex))
            {
                _parentIndices[index] = parentIndex;
            }

            byName.TryAdd(node.Name, index);
            byNameIgnoreCase.TryAdd(node.Name, index);

            if (!Matrix4x4.Decompose(
                    node.LocalTransform,
                    out Vector3 scale,
                    out Quaternion rotation,
                    out Vector3 translation))
            {
                scale = Vector3.One;
                rotation = Quaternion.Identity;
                translation = node.LocalTransform.Translation;
            }

            _basePositions[index] = translation;
            _baseRotations[index] = NormalizeSafe(rotation);
            _baseScales[index] = SanitizeScale(scale);
        }

        _boneNodeIndices =
            new int[_skeleton.Bones.Count];

        for (int boneIndex = 0;
             boneIndex < _skeleton.Bones.Count;
             boneIndex++)
        {
            string boneName =
                _skeleton.Bones[boneIndex].Name;

            _boneNodeIndices[boneIndex] =
                byName.TryGetValue(boneName, out int exact)
                    ? exact
                    : byNameIgnoreCase.TryGetValue(
                        boneName,
                        out int fallback)
                        ? fallback
                        : -1;
        }

        _rootMotionNodes =
            new bool[_nodes.Length];

        for (int boneIndex = 0;
             boneIndex < _skeleton.Bones.Count;
             boneIndex++)
        {
            if (_skeleton.Bones[boneIndex].ParentIndex >= 0)
            {
                continue;
            }

            int nodeIndex =
                _boneNodeIndices[boneIndex];

            /*
             * FBX/glTF exporters do not agree on whether locomotion travel
             * lives on the skeleton root itself or on an Armature/model node
             * above it. Mark the root bone and its ancestor chain.
             */
            var visited =
                new HashSet<int>();

            while (nodeIndex >= 0 &&
                   nodeIndex < _rootMotionNodes.Length &&
                   visited.Add(nodeIndex))
            {
                _rootMotionNodes[nodeIndex] =
                    true;

                nodeIndex =
                    _parentIndices[nodeIndex];
            }
        }
    }

    private void BuildRuntimeMeshes(AssetManager assets)
    {
        DisposeRuntimeMeshes();

        if (_model == null)
        {
            return;
        }

        var meshNodeIndices =
            new Dictionary<string, int>(StringComparer.Ordinal);

        for (int nodeIndex = 0;
             nodeIndex < _nodes.Length;
             nodeIndex++)
        {
            foreach (string meshKey in _nodes[nodeIndex].MeshKeys)
            {
                meshNodeIndices.TryAdd(meshKey, nodeIndex);
            }
        }

        int skinnedIndex = 0;

        foreach (ImportedMesh source in _model.Meshes)
        {
            int vertexCount = source.Vertices.Length / 8;

            if (vertexCount <= 0 ||
                source.JointIndices.Length != vertexCount ||
                source.JointWeights.Length != vertexCount ||
                !meshNodeIndices.TryGetValue(
                    source.Key,
                    out int meshNodeIndex))
            {
                continue;
            }

            string? materialKey =
                skinnedIndex < MaterialKeys.Count &&
                !string.IsNullOrWhiteSpace(MaterialKeys[skinnedIndex])
                    ? MaterialKeys[skinnedIndex]
                    : source.MaterialKey;

            Material material = new();

            if (!string.IsNullOrWhiteSpace(materialKey))
            {
                try
                {
                    material =
                        assets.GetModelMaterial(
                            Model,
                            materialKey);
                }
                catch
                {
                    material = new Material();
                }
            }

            _runtimeMeshes.Add(
                new RuntimeSkinnedMesh(
                    source,
                    meshNodeIndex,
                    new Mesh(
                        (float[])source.Vertices.Clone(),
                        source.Indices,
                        dynamicVertices: true),
                    material,
                    (float[])source.Vertices.Clone()));

            skinnedIndex++;
        }
    }

    private void UpdatePoseAndMeshes()
    {
        _poseDirty = false;

        if (_model == null ||
            _skeleton == null ||
            _runtimeMeshes.Count == 0 ||
            _nodes.Length == 0)
        {
            return;
        }

        Matrix4x4[] locals = BuildLocalPose();
        Matrix4x4[] globals = ComputeGlobals(locals);

        foreach (RuntimeSkinnedMesh runtime in _runtimeMeshes)
        {
            if (runtime.MeshNodeIndex < 0 ||
                runtime.MeshNodeIndex >= globals.Length)
            {
                continue;
            }

            Matrix4x4 meshGlobal =
                globals[runtime.MeshNodeIndex];

            Matrix4x4 inverseMesh =
                Matrix4x4.Invert(
                    meshGlobal,
                    out Matrix4x4 inverse)
                    ? inverse
                    : Matrix4x4.Identity;

            Matrix4x4[] skinMatrices =
                new Matrix4x4[_skeleton.Bones.Count];

            for (int boneIndex = 0;
                 boneIndex < skinMatrices.Length;
                 boneIndex++)
            {
                int nodeIndex =
                    _boneNodeIndices[boneIndex];

                if (nodeIndex < 0 ||
                    nodeIndex >= globals.Length)
                {
                    skinMatrices[boneIndex] =
                        Matrix4x4.Identity;

                    continue;
                }

                /*
                 * System.Numerics/ByteEngine use row-vector composition.
                 * Assimp/glTF inverse-bind matrices are already converted to
                 * that convention by their importers.
                 *
                 * v_local * inverseBind * jointGlobal * inverse(meshGlobal)
                 * gives the skinned vertex back in the mesh node's local
                 * space. The mesh node's current global transform is then used
                 * as the normal RenderWorld model matrix.
                 */
                skinMatrices[boneIndex] =
                    _skeleton.Bones[boneIndex].BindPose *
                    globals[nodeIndex] *
                    inverseMesh;
            }

            SkinMesh(runtime, skinMatrices);

            runtime.ModelMatrix =
                meshGlobal *
                Transform.WorldMatrix;
        }
    }

    private Matrix4x4[] BuildLocalPose()
    {
        Matrix4x4[] result =
            new Matrix4x4[_nodes.Length];

        float blend =
            _previousAnimation == null ||
            _activeTransitionDuration <= 0.000001f
                ? 1.0f
                : Math.Clamp(
                    _transitionElapsed /
                    _activeTransitionDuration,
                    0.0f,
                    1.0f);

        for (int nodeIndex = 0;
             nodeIndex < _nodes.Length;
             nodeIndex++)
        {
            PoseTransform current =
                SampleNode(
                    nodeIndex,
                    _currentAnimation,
                    _currentTime);

            if (_previousAnimation != null &&
                blend < 1.0f)
            {
                PoseTransform previous =
                    SampleNode(
                        nodeIndex,
                        _previousAnimation,
                        _previousTime);

                current =
                    PoseTransform.Lerp(
                        previous,
                        current,
                        blend);
            }

            result[nodeIndex] =
                Matrix4x4.CreateScale(current.Scale) *
                Matrix4x4.CreateFromQuaternion(current.Rotation) *
                Matrix4x4.CreateTranslation(current.Position);
        }

        return result;
    }

    private PoseTransform SampleNode(
        int nodeIndex,
        ImportedAnimation? animation,
        float time)
    {
        Vector3 position = _basePositions[nodeIndex];
        Quaternion rotation = _baseRotations[nodeIndex];
        Vector3 scale = _baseScales[nodeIndex];

        if (animation == null)
        {
            return new PoseTransform(position, rotation, scale);
        }

        ImportedAnimationChannel? channel =
            animation.FindChannel(_nodes[nodeIndex].Name);

        if (channel == null)
        {
            return new PoseTransform(position, rotation, scale);
        }

        position =
            AnimationPoseSampler.Sample(
                channel.Translation,
                time,
                position);

        if (nodeIndex < _rootMotionNodes.Length &&
            _rootMotionNodes[nodeIndex])
        {
            position =
                RemoveAccumulatedRootTranslation(
                    channel.Translation,
                    animation.Duration,
                    time,
                    _basePositions[nodeIndex],
                    position);
        }

        rotation =
            AnimationPoseSampler.Sample(
                channel.Rotation,
                time,
                rotation);

        scale =
            AnimationPoseSampler.Sample(
                channel.Scale,
                time,
                scale);

        return new PoseTransform(position, rotation, scale);
    }

    /// <summary>
    /// Converts root-motion locomotion clips into in-place playback.
    ///
    /// We remove only the accumulated horizontal travel from the start of the
    /// clip to the end of the clip. Local hip/body sway remains, and vertical
    /// motion remains untouched. CharacterController3D continues to own actual
    /// world movement until the dedicated Root Motion phase is implemented.
    /// </summary>
    private static Vector3 RemoveAccumulatedRootTranslation(
        ImportedVectorTrack? track,
        float duration,
        float time,
        Vector3 fallback,
        Vector3 sampled)
    {
        if (track == null ||
            track.Keys.Count < 2 ||
            duration <= 0.000001f)
        {
            return sampled;
        }

        Vector3 start =
            AnimationPoseSampler.Sample(
                track,
                0.0f,
                fallback);

        Vector3 end =
            AnimationPoseSampler.Sample(
                track,
                duration,
                start);

        float normalizedTime =
            Math.Clamp(
                time /
                duration,
                0.0f,
                1.0f);

        Vector3 accumulatedTravel =
            Vector3.Lerp(
                start,
                end,
                normalizedTime) -
            start;

        return
            new Vector3(
                sampled.X -
                    accumulatedTravel.X,
                sampled.Y,
                sampled.Z -
                    accumulatedTravel.Z);
    }

    private Matrix4x4[] ComputeGlobals(
        IReadOnlyList<Matrix4x4> locals)
    {
        Matrix4x4[] globals =
            new Matrix4x4[locals.Count];

        byte[] states = new byte[locals.Count];

        for (int index = 0; index < locals.Count; index++)
        {
            ResolveGlobal(
                index,
                locals,
                globals,
                states);
        }

        return globals;
    }

    private Matrix4x4 ResolveGlobal(
        int index,
        IReadOnlyList<Matrix4x4> locals,
        Matrix4x4[] globals,
        byte[] states)
    {
        if (states[index] == 2)
        {
            return globals[index];
        }

        if (states[index] == 1)
        {
            globals[index] = locals[index];
            states[index] = 2;
            return globals[index];
        }

        states[index] = 1;

        int parent = _parentIndices[index];

        globals[index] =
            parent >= 0 && parent < locals.Count
                ? locals[index] *
                  ResolveGlobal(
                      parent,
                      locals,
                      globals,
                      states)
                : locals[index];

        states[index] = 2;

        return globals[index];
    }

    private static void SkinMesh(
        RuntimeSkinnedMesh runtime,
        IReadOnlyList<Matrix4x4> skinMatrices)
    {
        ImportedMesh source =
            runtime.Source;

        float[] vertices =
            runtime.DeformedVertices;

        int vertexCount =
            vertices.Length /
            8;

        for (int vertexIndex =
                 0;
             vertexIndex <
             vertexCount;
             vertexIndex++)
        {
            int offset =
                vertexIndex *
                8;

            Vector3 sourcePosition =
                new(
                    source.Vertices[offset],
                    source.Vertices[offset + 1],
                    source.Vertices[offset + 2]);

            Vector3 sourceNormal =
                new(
                    source.Vertices[offset + 3],
                    source.Vertices[offset + 4],
                    source.Vertices[offset + 5]);

            Vector4 jointIndices =
                source.JointIndices[vertexIndex];

            Vector4 jointWeights =
                source.JointWeights[vertexIndex];

            Vector3 position =
                Vector3.Zero;

            Vector3 normal =
                Vector3.Zero;

            float totalWeight =
                0.0f;

            ApplyInfluence(
                jointIndices.X,
                jointWeights.X,
                sourcePosition,
                sourceNormal,
                skinMatrices,
                ref position,
                ref normal,
                ref totalWeight);

            ApplyInfluence(
                jointIndices.Y,
                jointWeights.Y,
                sourcePosition,
                sourceNormal,
                skinMatrices,
                ref position,
                ref normal,
                ref totalWeight);

            ApplyInfluence(
                jointIndices.Z,
                jointWeights.Z,
                sourcePosition,
                sourceNormal,
                skinMatrices,
                ref position,
                ref normal,
                ref totalWeight);

            ApplyInfluence(
                jointIndices.W,
                jointWeights.W,
                sourcePosition,
                sourceNormal,
                skinMatrices,
                ref position,
                ref normal,
                ref totalWeight);

            if (totalWeight <=
                0.000001f)
            {
                position =
                    sourcePosition;

                normal =
                    sourceNormal;
            }
            else if (MathF.Abs(
                         totalWeight -
                         1.0f) >
                     0.0001f)
            {
                position /=
                    totalWeight;

                normal /=
                    totalWeight;
            }

            normal =
                normal.LengthSquared() >
                    0.000001f
                    ? Vector3.Normalize(
                        normal)
                    : sourceNormal;

            vertices[offset] =
                position.X;

            vertices[offset + 1] =
                position.Y;

            vertices[offset + 2] =
                position.Z;

            vertices[offset + 3] =
                normal.X;

            vertices[offset + 4] =
                normal.Y;

            vertices[offset + 5] =
                normal.Z;

            /*
             * UVs never change. They already live in DeformedVertices from
             * the one-time source clone created with the runtime mesh.
             */
        }

        runtime.Mesh.UpdateVertices(
            vertices,
            updateBounds: true);
    }

    private static void ApplyInfluence(
        float jointIndexValue,
        float weightValue,
        Vector3 sourcePosition,
        Vector3 sourceNormal,
        IReadOnlyList<Matrix4x4> skinMatrices,
        ref Vector3 position,
        ref Vector3 normal,
        ref float totalWeight)
    {
        float weight =
            Math.Max(
                weightValue,
                0.0f);

        if (weight <=
            0.000001f)
        {
            return;
        }

        int boneIndex =
            (int)MathF.Round(
                jointIndexValue);

        if (boneIndex <
                0 ||
            boneIndex >=
                skinMatrices.Count)
        {
            return;
        }

        Matrix4x4 matrix =
            skinMatrices[boneIndex];

        position +=
            Vector3.Transform(
                sourcePosition,
                matrix) *
            weight;

        normal +=
            Vector3.TransformNormal(
                sourceNormal,
                matrix) *
            weight;

        totalWeight +=
            weight;
    }

    private static float AdvanceTime(
        float current,
        float delta,
        float duration,
        bool loop,
        out bool finished)
    {
        finished = false;

        if (duration <= 0.000001f)
        {
            return 0.0f;
        }

        float next = current + delta;

        if (loop)
        {
            return next % duration;
        }

        if (next >= duration)
        {
            finished = true;
            return duration;
        }

        return next;
    }

    private static Quaternion NormalizeSafe(Quaternion value) =>
        value.LengthSquared() > 0.000001f
            ? Quaternion.Normalize(value)
            : Quaternion.Identity;

    private static Vector3 SanitizeScale(Vector3 value) =>
        new(
            NonZero(value.X),
            NonZero(value.Y),
            NonZero(value.Z));

    private static float NonZero(float value) =>
        MathF.Abs(value) < 0.0001f
            ? MathF.CopySign(
                0.0001f,
                value == 0.0f ? 1.0f : value)
            : value;

    private static RenderQueue3D ResolveRenderQueue(
        Material material) =>
        material.BlendMode switch
        {
            BlendMode3D.AlphaBlend =>
                RenderQueue3D.Transparent,
            BlendMode3D.Additive =>
                RenderQueue3D.Transparent,
            _ =>
                RenderQueue3D.Opaque
        };

    private void DisposeRuntimeMeshes()
    {
        foreach (RuntimeSkinnedMesh runtime in _runtimeMeshes)
        {
            runtime.Mesh.Dispose();
        }

        _runtimeMeshes.Clear();
    }

    private sealed class RuntimeSkinnedMesh
    {
        public ImportedMesh Source { get; }
        public int MeshNodeIndex { get; }
        public Mesh Mesh { get; }
        public Material Material { get; }

        /// <summary>
        /// Reused every frame. Keeping one deformation buffer per runtime mesh
        /// eliminates a full float[] allocation on every animation tick.
        /// </summary>
        public float[] DeformedVertices { get; }

        public Matrix4x4 ModelMatrix { get; set; } =
            Matrix4x4.Identity;

        public RuntimeSkinnedMesh(
            ImportedMesh source,
            int meshNodeIndex,
            Mesh mesh,
            Material material,
            float[] deformedVertices)
        {
            Source = source;
            MeshNodeIndex = meshNodeIndex;
            Mesh = mesh;
            Material = material;
            DeformedVertices = deformedVertices;
        }
    }

    private readonly record struct PoseTransform(
        Vector3 Position,
        Quaternion Rotation,
        Vector3 Scale)
    {
        public static PoseTransform Lerp(
            PoseTransform from,
            PoseTransform to,
            float amount) =>
            new(
                Vector3.Lerp(
                    from.Position,
                    to.Position,
                    amount),
                Quaternion.Slerp(
                    SkeletalMeshRenderer.NormalizeSafe(from.Rotation),
                    SkeletalMeshRenderer.NormalizeSafe(to.Rotation),
                    amount),
                Vector3.Lerp(
                    from.Scale,
                    to.Scale,
                    amount));
    }
}
