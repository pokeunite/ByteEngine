using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics.ThreeD;

public sealed class MeshRenderer : Component
{
    public Mesh? Mesh { get; set; }
    private readonly MeshLodGroup _modelLod=new(){ScreenSize=true};
    public bool AutomaticModelLod {get;set;}=true;

    public ModelMeshReference? MeshReference { get; set; }

    public ModelMaterialReference? MaterialReference { get; set; }

    private AssetReference _materialAssetReference = AssetReference.Empty;
    private Material? _resolvedAssetMaterial;
    private bool _materialAssetLoadFailed;
    private Material? _overrideMaterial;
    public MaterialOverrideState MaterialOverrides { get; } = new();

    public AssetReference MaterialAssetReference
    {
        get => _materialAssetReference;
        set
        {
            _materialAssetReference = value ?? AssetReference.Empty;
            _resolvedAssetMaterial = null;
            _materialAssetLoadFailed = false;
        }
    }

    public PrimitiveMeshType Primitive { get; set; } =
        PrimitiveMeshType.Cube;

    /// <summary>
    /// Explicitly enables the built-in primitive mesh path.
    ///
    /// Generic MeshRenderer components default to false, so an unassigned
    /// renderer does not silently become a cube. The editor's dedicated
    /// Cube/Sphere/Plane creation commands enable this flag.
    /// </summary>
    public bool UsePrimitive { get; set; } =
        false;

    public Material Material { get; set; } =
        new();

    public bool Visible { get; set; } =
        true;

    public bool FrustumCulling { get; set; } =
        true;

    /// <summary>
    /// Includes this renderer in the directional shadow depth pass.
    /// AlphaBlend/Additive materials are skipped by the shadow pass.
    /// </summary>
    public bool CastShadows { get; set; } =
        true;

    /// <summary>
    /// Allows this renderer to sample the active directional shadow map.
    /// </summary>
    public bool ReceiveShadows { get; set; } =
        true;

    public bool AutomaticRenderQueue { get; set; } =
        true;

    public RenderQueue3D RenderQueue { get; set; } =
        RenderQueue3D.Opaque;

    public float Metallic
    {
        get =>
            Material.Metallic;

        set =>
            Material.Metallic =
                Math.Clamp(
                    value,
                    0.0f,
                    1.0f);
    }

    public float Roughness
    {
        get =>
            Material.Roughness;

        set =>
            Material.Roughness =
                Math.Clamp(
                    value,
                    0.04f,
                    1.0f);
    }

    public BlendMode3D BlendMode
    {
        get =>
            Material.BlendMode;

        set =>
            Material.BlendMode =
                value;
    }

    public float AlphaCutoff
    {
        get =>
            Material.AlphaCutoff;

        set =>
            Material.AlphaCutoff =
                Math.Clamp(
                    value,
                    0.0f,
                    1.0f);
    }

    public bool DepthTest
    {
        get =>
            Material.DepthTest;

        set =>
            Material.DepthTest =
                value;
    }

    public DepthWriteMode3D DepthWriteMode
    {
        get =>
            Material.DepthWriteMode;

        set =>
            Material.DepthWriteMode =
                value;
    }

    public CullMode3D CullMode
    {
        get =>
            Material.CullMode;

        set =>
            Material.CullMode =
                value;
    }

    public FrontFaceWinding3D FrontFace
    {
        get =>
            Material.FrontFace;

        set =>
            Material.FrontFace =
                value;
    }

    public PolygonMode3D PolygonMode
    {
        get =>
            Material.PolygonMode;

        set =>
            Material.PolygonMode =
                value;
    }

    protected override void OnRender(
        RenderContext context)
    {
        if (context.RenderWorld.View is {} lodView && !MeshLodGroup.Allows(GameObject,lodView,context.ViewportName)) return;
        if (!Visible ||
            !context.Has3DCamera)
        {
            return;
        }

        Mesh? mesh =
            Mesh;

        if (mesh ==
            null)
        {
            if (!UsePrimitive)
            {
                return;
            }

            mesh =
                context.Renderer3D.GetPrimitive(
                    Primitive);
        }

        if(AutomaticModelLod&&MeshReference!=null&&context.RenderWorld.View is {} view&&AnimationRuntimeAssets.TryGet(out var assets)&&assets!=null)
        {
            float distance=System.Numerics.Vector3.Distance(Transform.WorldPosition,view.CameraPosition);
            float pixels=mesh.LocalBounds.Size.Length()*.5f*System.Numerics.Vector3.Abs(Transform.WorldScale).Length()/MathF.Sqrt(3)*view.ProjectionMatrix.M22*view.TargetHeight/Math.Max(.001f,distance);
            int level=_modelLod.SelectForViewport(context.ViewportName,view,distance,pixels);if(level>0)mesh=assets.GetModelLodMesh(MeshReference,level);
        }
        Material effectiveMaterial = ResolveEffectiveMaterial();
        context.RenderWorld.Submit(
            mesh,
            effectiveMaterial,
            Transform.RenderMatrix,
            ResolveRenderQueue(effectiveMaterial),
            FrustumCulling,
            CastShadows,
            ReceiveShadows);
    }

    private Material ResolveEffectiveMaterial()
    {
        Material source = ModelHierarchyInstance.ResolveMaterialOverride(GameObject) ?? ResolveBaseMaterial();
        if (MaterialOverrides.IsEmpty) return source;
        _overrideMaterial ??= source.Clone();
        AnimationRuntimeAssets.TryGet(out AssetManager? assets);
        MaterialOverrides.Apply(source, _overrideMaterial, assets);
        return _overrideMaterial;
    }

    private Material ResolveBaseMaterial()
    {
        if (_materialAssetReference.IsEmpty) return Material;
        if (_resolvedAssetMaterial != null) return _resolvedAssetMaterial;
        if (_materialAssetLoadFailed ||
            !AnimationRuntimeAssets.TryGet(out AssetManager? assets) || assets == null)
            return Material;
        try
        {
            _resolvedAssetMaterial = assets.LoadMaterial(_materialAssetReference);
            return _resolvedAssetMaterial;
        }
        catch
        {
            _materialAssetLoadFailed = true;
            return Material;
        }
    }

    private RenderQueue3D ResolveRenderQueue(Material effectiveMaterial)
    {
        if (!AutomaticRenderQueue)
        {
            return RenderQueue;
        }

        return effectiveMaterial.BlendMode switch
        {
            BlendMode3D.AlphaBlend =>
                RenderQueue3D.Transparent,

            BlendMode3D.Additive =>
                RenderQueue3D.Transparent,

            _ =>
                RenderQueue3D.Opaque
        };
    }
}
