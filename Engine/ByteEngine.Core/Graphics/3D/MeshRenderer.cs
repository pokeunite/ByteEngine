using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics.ThreeD;

public sealed class MeshRenderer : Component
{
    public Mesh? Mesh { get; set; }

    public ModelMeshReference? MeshReference { get; set; }

    public ModelMaterialReference? MaterialReference { get; set; }

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

        context.RenderWorld.Submit(
            mesh,
            Material,
            Transform.WorldMatrix,
            ResolveRenderQueue(),
            FrustumCulling,
            CastShadows,
            ReceiveShadows);
    }

    private RenderQueue3D ResolveRenderQueue()
    {
        if (!AutomaticRenderQueue)
        {
            return RenderQueue;
        }

        return Material.BlendMode switch
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
