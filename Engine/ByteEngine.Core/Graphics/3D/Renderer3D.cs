using System.Numerics;
using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Core.Graphics.ThreeD;

public sealed class Renderer3D : IDisposable
{
    private readonly Dictionary<PrimitiveMeshType, Mesh> _primitives =
        new();

    private Shader3D? _shader;

    internal void Initialize() =>
        _shader ??=
            new Shader3D();

    public Mesh GetPrimitive(
        PrimitiveMeshType type)
    {
        if (!_primitives.TryGetValue(
                type,
                out Mesh? mesh))
        {
            mesh =
                PrimitiveMesh.Create(
                    type);

            _primitives[type] =
                mesh;
        }

        return mesh;
    }

    /// <summary>
    /// v0.9-c multi-light draw path.
    /// </summary>
    public void Draw(
        Mesh mesh,
        Material material,
        Matrix4x4 model,
        Matrix4x4 view,
        Matrix4x4 projection,
        RenderLighting3D lighting)
    {
        ArgumentNullException.ThrowIfNull(
            mesh);

        ArgumentNullException.ThrowIfNull(
            material);

        ArgumentNullException.ThrowIfNull(
            lighting);

        Initialize();

        ApplyRenderState(
            material);

        try
        {
            _shader!.Use();

            _shader.SetMatrix(
                "uModel",
                model);

            _shader.SetMatrix(
                "uView",
                view);

            _shader.SetMatrix(
                "uProjection",
                projection);

            _shader.SetVector4(
                "uBaseColor",
                material.BaseColor);

            _shader.SetFloat(
                "uMetallic",
                Math.Clamp(
                    material.Metallic,
                    0.0f,
                    1.0f));

            _shader.SetFloat(
                "uRoughness",
                Math.Clamp(
                    material.Roughness,
                    0.04f,
                    1.0f));

            _shader.SetFloat(
                "uAlphaCutoff",
                material.BlendMode ==
                    BlendMode3D.Cutout
                    ? Math.Clamp(
                        material.AlphaCutoff,
                        0.0f,
                        1.0f)
                    : 0.0f);

            if (Matrix4x4.Invert(
                    view,
                    out Matrix4x4 inverseView))
            {
                _shader.SetVector3(
                    "uCameraPosition",
                    inverseView.Translation);
            }
            else
            {
                _shader.SetVector3(
                    "uCameraPosition",
                    Vector3.Zero);
            }

            UploadLighting(
                lighting);

            if (material.MainTexture !=
                null)
            {
                material.MainTexture.Bind(
                    0);

                _shader.SetInt(
                    "uTexture",
                    0);

                _shader.SetInt(
                    "uUseTexture",
                    1);
            }
            else
            {
                _shader.SetInt(
                    "uUseTexture",
                    0);
            }

            if (material.NormalTexture !=
                null)
            {
                material.NormalTexture.Bind(
                    1);

                _shader.SetInt(
                    "uNormalTexture",
                    1);

                _shader.SetInt(
                    "uUseNormalTexture",
                    1);
            }
            else
            {
                _shader.SetInt(
                    "uUseNormalTexture",
                    0);
            }

            mesh.Bind();

            GL.DrawElements(
                BeginMode.Triangles,
                mesh.IndexCount,
                DrawElementsType.UnsignedInt,
                0);

            GL.BindVertexArray(
                0);
        }
        finally
        {
            RestoreBaselineState();
        }
    }

    /// <summary>
    /// Compatibility overload retained for code written before v0.9-c.
    /// </summary>
    public void Draw(
        Mesh mesh,
        Material material,
        Matrix4x4 model,
        Matrix4x4 view,
        Matrix4x4 projection,
        Vector3 lightDirection,
        Vector3 lightColor,
        float intensity,
        float ambientIntensity)
    {
        Draw(
            mesh,
            material,
            model,
            view,
            projection,
            RenderLighting3D.FromLegacy(
                lightDirection,
                lightColor,
                intensity,
                ambientIntensity));
    }

    private void UploadLighting(
        RenderLighting3D lighting)
    {
        int directionalCount =
            Math.Min(
                lighting.DirectionalLightCount,
                RenderLighting3D.MaxDirectionalLights);

        int pointCount =
            Math.Min(
                lighting.PointLightCount,
                RenderLighting3D.MaxPointLights);

        _shader!.SetInt(
            "uDirectionalLightCount",
            directionalCount);

        _shader.SetInt(
            "uPointLightCount",
            pointCount);

        _shader.SetFloat(
            "uAmbientIntensity",
            lighting.AmbientIntensity);

        for (int index =
                 0;
             index <
             directionalCount;
             index++)
        {
            RenderDirectionalLight3D light =
                lighting.DirectionalLights[index];

            _shader.SetVector3(
                $"uDirectionalLightDirections[{index}]",
                light.Direction);

            _shader.SetVector3(
                $"uDirectionalLightColors[{index}]",
                light.Color);

            _shader.SetFloat(
                $"uDirectionalLightIntensities[{index}]",
                Math.Max(
                    0.0f,
                    light.Intensity));
        }

        for (int index =
                 0;
             index <
             pointCount;
             index++)
        {
            RenderPointLight3D light =
                lighting.PointLights[index];

            _shader.SetVector3(
                $"uPointLightPositions[{index}]",
                light.Position);

            _shader.SetVector3(
                $"uPointLightColors[{index}]",
                light.Color);

            _shader.SetFloat(
                $"uPointLightIntensities[{index}]",
                Math.Max(
                    0.0f,
                    light.Intensity));

            _shader.SetFloat(
                $"uPointLightRanges[{index}]",
                Math.Max(
                    0.01f,
                    light.Range));
        }
    }

    private static void ApplyRenderState(
        Material material)
    {
        if (material.DepthTest)
        {
            GL.Enable(
                EnableCap.DepthTest);
        }
        else
        {
            GL.Disable(
                EnableCap.DepthTest);
        }

        GL.DepthMask(
            material.ResolveDepthWrite());

        switch (material.BlendMode)
        {
            case BlendMode3D.AlphaBlend:
                GL.Enable(
                    EnableCap.Blend);

                GL.BlendEquation(
                    BlendEquationMode.FuncAdd);

                GL.BlendFunc(
                    BlendingFactor.SrcAlpha,
                    BlendingFactor.OneMinusSrcAlpha);
                break;

            case BlendMode3D.Additive:
                GL.Enable(
                    EnableCap.Blend);

                GL.BlendEquation(
                    BlendEquationMode.FuncAdd);

                GL.BlendFunc(
                    BlendingFactor.SrcAlpha,
                    BlendingFactor.One);
                break;

            default:
                GL.Disable(
                    EnableCap.Blend);
                break;
        }

        switch (material.CullMode)
        {
            case CullMode3D.Back:
                GL.Enable(
                    EnableCap.CullFace);

                GL.CullFace(
                    CullFaceMode.Back);
                break;

            case CullMode3D.Front:
                GL.Enable(
                    EnableCap.CullFace);

                GL.CullFace(
                    CullFaceMode.Front);
                break;

            default:
                GL.Disable(
                    EnableCap.CullFace);
                break;
        }

        GL.FrontFace(
            material.FrontFace ==
                FrontFaceWinding3D.Clockwise
                ? FrontFaceDirection.Cw
                : FrontFaceDirection.Ccw);

        GL.PolygonMode(
            MaterialFace.FrontAndBack,
            material.PolygonMode ==
                PolygonMode3D.Wireframe
                ? OpenTK.Graphics.OpenGL4.PolygonMode.Line
                : OpenTK.Graphics.OpenGL4.PolygonMode.Fill);
    }

    private static void RestoreBaselineState()
    {
        GL.PolygonMode(
            MaterialFace.FrontAndBack,
            OpenTK.Graphics.OpenGL4.PolygonMode.Fill);

        GL.DepthMask(
            true);

        GL.Enable(
            EnableCap.DepthTest);

        GL.Disable(
            EnableCap.Blend);

        GL.Disable(
            EnableCap.CullFace);

        GL.FrontFace(
            FrontFaceDirection.Ccw);
    }

    public void Dispose()
    {
        foreach (Mesh mesh
                 in _primitives.Values)
        {
            mesh.Dispose();
        }

        _primitives.Clear();

        _shader?.Dispose();

        _shader =
            null;
    }
}
