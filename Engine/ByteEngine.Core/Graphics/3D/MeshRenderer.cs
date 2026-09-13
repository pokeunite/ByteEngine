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

    public Material Material { get; set; } =
        new();

    public bool Visible { get; set; } =
        true;

    public bool FrustumCulling { get; set; } =
        true;

    /// <summary>
    /// Automatically maps opaque/cutout materials to the opaque queue and
    /// blended/additive materials to the transparent queue.
    /// </summary>
    public bool AutomaticRenderQueue { get; set; } =
        true;

    /// <summary>
    /// Manual queue used when AutomaticRenderQueue is disabled.
    /// </summary>
    public RenderQueue3D RenderQueue { get; set; } =
        RenderQueue3D.Opaque;

    /*
     * Material authoring proxies.
     *
     * These live on MeshRenderer as well as Material so ByteEngine's existing
     * reflection-driven Inspector can edit the standard material immediately.
     */

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

    /// <summary>
    /// None is double-sided rendering.
    /// </summary>
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

        Mesh mesh =
            Mesh ??
            context.Renderer3D.GetPrimitive(
                Primitive);

        context.RenderWorld.Submit(
            mesh,
            Material,
            Transform.WorldMatrix,
            ResolveRenderQueue(),
            FrustumCulling);
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
