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
/// v0.11-C2 keeps locomotion clips in-place by measuring root travel only
/// AFTER the complete imported hierarchy has been evaluated into model space.
/// This avoids FBX local-axis ambiguity and keeps CharacterController3D as the
/// sole authority for player/world movement.
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
     * C6 socket foundation: keep the exact blended model-space pose used for
     * skinning so attachments can query a bone without independently sampling
     * animation. This guarantees sockets follow cross-fades and root-motion
     * compensation exactly as the visible mesh does.
     */
    private Matrix4x4[] _currentPoseGlobals = Array.Empty<Matrix4x4>();
    private Matrix4x4 _currentRootMotionCorrection = Matrix4x4.Identity;

    /*
     * Root-motion sampling is performed in MODEL SPACE, not in any individual
     * FBX/glTF node's local axes. The chain starts at the skeleton root bone
     * and walks upward through Armature/model conversion nodes so imported
     * coordinate-system rotations are already accounted for before horizontal
     * travel is identified.
     */
    private int _rootMotionNodeIndex = -1;
    private int[] _rootMotionChainIndices = Array.Empty<int>();

    private readonly Dictionary<ImportedAnimation, RootMotionRange>
        _rootMotionRanges = new();

    private Vector3 _modelSpaceRootTravel;

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

    public ModelAsset? ResolvedModel
    {
        get
        {
            if (!_resolved) ResolveRuntimeResources();
            return _model;
        }
    }

    public int SkinnedMeshCount =>
        _runtimeMeshes.Count;

    public IReadOnlyList<string> AnimationNames =>
        _model?.Animations
            .Select(animation => animation.Name)
            .ToArray()
        ?? Array.Empty<string>();

    /// <summary>
    /// Names of bones available for runtime sockets/attachments. Resolves the
    /// model lazily so editor/runtime tooling can query this before Play begins.
    /// </summary>
    public IReadOnlyList<string> BoneNames
    {
        get
        {
            if (!_resolved)
            {
                ResolveRuntimeResources();
            }

            return _skeleton?.Bones
                .Select(bone => bone.Name)
                .ToArray()
                ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Returns the selected bone in the same corrected model space used by the
    /// visible skinned mesh. Cross-fades and in-place root-motion compensation
    /// are therefore already included.
    /// </summary>
    public bool TryGetBoneModelMatrix(
        string boneName,
        out Matrix4x4 matrix)
    {
        matrix = Matrix4x4.Identity;

        if (string.IsNullOrWhiteSpace(boneName))
        {
            return false;
        }

        if (!_resolved &&
            !ResolveRuntimeResources())
        {
            return false;
        }

        if (_poseDirty)
        {
            UpdatePoseAndMeshes();
        }

        if (_skeleton == null ||
            _currentPoseGlobals.Length == 0)
        {
            return false;
        }

        int boneIndex =
            _skeleton.Bones.FindIndex(
                bone =>
                    string.Equals(
                        bone.Name,
                        boneName,
                        StringComparison.Ordinal));

        if (boneIndex < 0)
        {
            boneIndex =
                _skeleton.Bones.FindIndex(
                    bone =>
                        string.Equals(
                            bone.Name,
                            boneName,
                            StringComparison.OrdinalIgnoreCase));
        }

        if (boneIndex < 0 ||
            boneIndex >= _boneNodeIndices.Length)
        {
            return false;
        }

        int nodeIndex =
            _boneNodeIndices[boneIndex];

        if (nodeIndex < 0 ||
            nodeIndex >= _currentPoseGlobals.Length)
        {
            return false;
        }

        matrix =
            _currentPoseGlobals[nodeIndex] *
            _currentRootMotionCorrection;

        return true;
    }

    /// <summary>
    /// Returns the selected bone in world space, including the animated model's
    /// live scene transform. Intended for weapons, VFX and other attachments.
    /// </summary>
    public bool TryGetBoneWorldMatrix(
        string boneName,
        out Matrix4x4 matrix)
    {
        if (!TryGetBoneModelMatrix(
                boneName,
                out Matrix4x4 modelMatrix))
        {
            matrix = Matrix4x4.Identity;
            return false;
        }

        matrix =
            modelMatrix *
            Transform.WorldMatrix;

        return true;
    }

    /// <summary>
    /// Root node currently used to measure locomotion travel. Exposed for
    /// runtime diagnostics; authoring does not need to set this manually.
    /// </summary>
    public string RootMotionNodeName =>
        _rootMotionNodeIndex >= 0 &&
        _rootMotionNodeIndex < _nodes.Length
            ? _nodes[_rootMotionNodeIndex].Name
            : string.Empty;

    /// <summary>
    /// Horizontal root travel removed from the current rendered pose, measured
    /// after the complete imported parent chain has been evaluated.
    /// </summary>
    public Vector3 ModelSpaceRootTravel =>
        _modelSpaceRootTravel;

    /// <summary>
    /// Returns bounds for the currently deformed/skinned meshes in model
    /// space. This is primarily useful to editor tools such as animation
    /// previews that need to frame what is actually visible rather than raw
    /// importer hierarchy bounds.
    ///
    /// The method does not alter animation state. It only ensures the current
    /// pose is resolved, then combines each runtime mesh's already-maintained
    /// dynamic LocalBounds with its mesh-to-model transform.
    /// </summary>
    public bool TryGetCurrentModelBounds(
        out BoundingBox3D bounds)
    {
        if (!_resolved)
        {
            ResolveRuntimeResources();
        }

        if (_poseDirty)
        {
            UpdatePoseAndMeshes();
        }

        Vector3 minimum =
            new(
                float.PositiveInfinity,
                float.PositiveInfinity,
                float.PositiveInfinity);

        Vector3 maximum =
            new(
                float.NegativeInfinity,
                float.NegativeInfinity,
                float.NegativeInfinity);

        bool found =
            false;

        foreach (RuntimeSkinnedMesh runtime
                 in _runtimeMeshes)
        {
            BoundingBox3D meshBounds =
                runtime.Mesh.LocalBounds.Transform(
                    runtime.MeshToModelMatrix);

            if (!meshBounds.IsValid)
            {
                continue;
            }

            minimum =
                Vector3.Min(
                    minimum,
                    meshBounds.Minimum);

            maximum =
                Vector3.Max(
                    maximum,
                    meshBounds.Maximum);

            found =
                true;
        }

        bounds =
            found
                ? new BoundingBox3D(
                    minimum,
                    maximum)
                : BoundingBox3D.Empty;

        return
            found &&
            bounds.IsValid;
    }

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

        Matrix4x4 liveWorldMatrix =
            Transform.WorldMatrix;

        foreach (RuntimeSkinnedMesh runtime in _runtimeMeshes)
        {
            /*
             * Animation caches only mesh/model-space placement. The scene
             * transform is intentionally read here, at render time, so player
             * movement can never be hidden by a stale animation matrix.
             */
            context.RenderWorld.Submit(
                runtime.Mesh,
                runtime.Material,
                runtime.MeshToModelMatrix *
                liveWorldMatrix,
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

    /// <summary>
    /// Samples the active clip at an explicit editor/runtime time without
    /// advancing playback or dispatching AnimationController events.
    /// </summary>
    public bool Seek(float time)
    {
        if (_currentAnimation == null || !float.IsFinite(time))
        {
            return false;
        }

        _currentTime =
            Math.Clamp(
                time,
                0.0f,
                Math.Max(_currentAnimation.Duration, 0.0f));

        _previousAnimation = null;
        _previousTime = 0.0f;
        _transitionElapsed = 0.0f;
        _activeTransitionDuration = 0.0f;
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

        _rootMotionNodeIndex =
            -1;

        /*
         * Prefer a true skeleton root bone. Its GLOBAL/model-space position
         * naturally includes animation on any non-bone Armature/model nodes
         * above it, which is exactly what the old local-axis fix missed.
         */
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

            if (nodeIndex >= 0 &&
                nodeIndex < _nodes.Length)
            {
                _rootMotionNodeIndex =
                    nodeIndex;

                break;
            }
        }

        /*
         * Malformed/importer-specific skeletons can occasionally have no bone
         * flagged as a root. A valid mapped bone is still better than silently
         * disabling compensation.
         */
        if (_rootMotionNodeIndex < 0)
        {
            _rootMotionNodeIndex =
                _boneNodeIndices.FirstOrDefault(
                    nodeIndex =>
                        nodeIndex >= 0 &&
                        nodeIndex < _nodes.Length,
                    -1);
        }

        var rootMotionChain =
            new List<int>();

        var rootMotionVisited =
            new HashSet<int>();

        int currentRootNode =
            _rootMotionNodeIndex;

        while (currentRootNode >= 0 &&
               currentRootNode < _nodes.Length &&
               rootMotionVisited.Add(currentRootNode))
        {
            /*
             * Stored root -> parent -> grandparent. With System.Numerics
             * row-vector composition this evaluates as:
             * rootLocal * parentLocal * grandParentLocal ...
             */
            rootMotionChain.Add(
                currentRootNode);

            currentRootNode =
                _parentIndices[currentRootNode];
        }

        _rootMotionChainIndices =
            rootMotionChain.ToArray();

        _rootMotionRanges.Clear();
        _modelSpaceRootTravel =
            Vector3.Zero;
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

        Matrix4x4 rootMotionCorrection =
            BuildModelSpaceRootMotionCorrection();

        _currentPoseGlobals = globals;
        _currentRootMotionCorrection = rootMotionCorrection;

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

            /*
             * Keep the animation result in model space. World placement is
             * applied live in OnRender().
             *
             * The correction is applied AFTER meshGlobal, so it is measured in
             * the same model-space axes as the fully evaluated skeleton root.
             * This fixes FBX clips whose "forward" translation lives on local Y
             * or another rotated importer axis.
             */
            runtime.MeshToModelMatrix =
                meshGlobal *
                rootMotionCorrection;
        }

        // Followers consume the same completed pose as the skinned mesh.
        // This also covers a clip change or Seek outside Scene.UpdateInternal.
        SkeletalAttachmentService.UpdateForRenderer(this);
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
                PoseToMatrix(
                    current);
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
    /// Produces an in-place locomotion correction from the complete imported
    /// hierarchy. The old C1 fix removed local X/Z from guessed nodes; that is
    /// incorrect for FBX files where an Armature/conversion parent rotates the
    /// animation axes.
    ///
    /// Here the skeleton root is evaluated all the way into model space first.
    /// Only travel along the clip's net horizontal locomotion direction is
    /// removed, preserving vertical movement and side-to-side body sway.
    /// </summary>
    private Matrix4x4 BuildModelSpaceRootMotionCorrection()
    {
        _modelSpaceRootTravel =
            Vector3.Zero;

        if (_currentAnimation == null ||
            _rootMotionChainIndices.Length == 0)
        {
            return Matrix4x4.Identity;
        }

        Vector3 currentTravel =
            CalculateClipRootTravel(
                _currentAnimation,
                _currentTime);

        float blend =
            _previousAnimation == null ||
            _activeTransitionDuration <= 0.000001f
                ? 1.0f
                : Math.Clamp(
                    _transitionElapsed /
                    _activeTransitionDuration,
                    0.0f,
                    1.0f);

        if (_previousAnimation != null &&
            blend < 1.0f)
        {
            Vector3 previousTravel =
                CalculateClipRootTravel(
                    _previousAnimation,
                    _previousTime);

            _modelSpaceRootTravel =
                Vector3.Lerp(
                    previousTravel,
                    currentTravel,
                    blend);
        }
        else
        {
            _modelSpaceRootTravel =
                currentTravel;
        }

        return Matrix4x4.CreateTranslation(
            -_modelSpaceRootTravel.X,
            0.0f,
            -_modelSpaceRootTravel.Z);
    }

    private Vector3 CalculateClipRootTravel(
        ImportedAnimation animation,
        float time)
    {
        if (animation.Duration <= 0.000001f ||
            _rootMotionChainIndices.Length == 0)
        {
            return Vector3.Zero;
        }

        RootMotionRange range =
            GetRootMotionRange(
                animation);

        Vector3 netTravel =
            range.End -
            range.Start;

        netTravel.Y =
            0.0f;

        float netLengthSquared =
            netTravel.LengthSquared();

        if (netLengthSquared <= 0.0000001f)
        {
            /*
             * Already in-place (or no meaningful horizontal root travel).
             */
            return Vector3.Zero;
        }

        Vector3 direction =
            netTravel /
            MathF.Sqrt(netLengthSquared);

        Vector3 current =
            SampleRootModelPosition(
                animation,
                time);

        Vector3 displacement =
            current -
            range.Start;

        displacement.Y =
            0.0f;

        /*
         * Remove progress ALONG the locomotion path but retain perpendicular
         * movement such as hip/torso sway.
         */
        float distanceAlongTravel =
            Vector3.Dot(
                displacement,
                direction);

        return direction *
               distanceAlongTravel;
    }

    private RootMotionRange GetRootMotionRange(
        ImportedAnimation animation)
    {
        if (_rootMotionRanges.TryGetValue(
                animation,
                out RootMotionRange cached))
        {
            return cached;
        }

        RootMotionRange range =
            new(
                SampleRootModelPosition(
                    animation,
                    0.0f),
                SampleRootModelPosition(
                    animation,
                    Math.Max(
                        animation.Duration,
                        0.0f)));

        _rootMotionRanges[animation] =
            range;

        return range;
    }

    private Vector3 SampleRootModelPosition(
        ImportedAnimation animation,
        float time)
    {
        Matrix4x4 global =
            Matrix4x4.Identity;

        foreach (int nodeIndex
                 in _rootMotionChainIndices)
        {
            PoseTransform pose =
                SampleNode(
                    nodeIndex,
                    animation,
                    time);

            global *=
                PoseToMatrix(
                    pose);
        }

        return global.Translation;
    }

    private static Matrix4x4 PoseToMatrix(
        PoseTransform pose) =>
        Matrix4x4.CreateScale(pose.Scale) *
        Matrix4x4.CreateFromQuaternion(pose.Rotation) *
        Matrix4x4.CreateTranslation(pose.Position);

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
        _currentPoseGlobals = Array.Empty<Matrix4x4>();
        _currentRootMotionCorrection = Matrix4x4.Identity;
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

        public Matrix4x4 MeshToModelMatrix { get; set; } =
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

    private readonly record struct RootMotionRange(
        Vector3 Start,
        Vector3 End);

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
