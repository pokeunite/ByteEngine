using System.Numerics;
using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Core.Graphics.ThreeD;

public sealed class Renderer3D : IDisposable
{
    private readonly Dictionary<PrimitiveMeshType, Mesh> _primitives =
        new();

    private Shader3D? _shader;

    private ShadowShader3D? _shadowShader;

    private ShadowMap3D? _shadowMap;

    internal void Initialize()
    {
        _shader ??=
            new Shader3D();

        _shadowShader ??=
            new ShadowShader3D();

        _shadowMap ??=
            new ShadowMap3D();
    }

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

    public DirectionalShadowPassResult RenderDirectionalShadowMap(
        IReadOnlyList<RenderSubmission> submissions,
        RenderView3D view,
        RenderLighting3D lighting)
    {
        ArgumentNullException.ThrowIfNull(
            submissions);

        ArgumentNullException.ThrowIfNull(
            lighting);

        int shadowLightIndex =
            lighting.FindShadowDirectionalLightIndex();

        if (shadowLightIndex <
                0 ||
            shadowLightIndex >=
                lighting.DirectionalLightCount)
        {
            return
                DirectionalShadowPassResult.None;
        }

        RenderDirectionalLight3D light =
            lighting.DirectionalLights[
                shadowLightIndex];

        RenderSubmission[] casters =
            submissions
                .Where(
                    submission =>
                        submission.CastShadows &&
                        submission.Queue !=
                            RenderQueue3D.Overlay &&
                        submission.Material.BlendMode is
                            BlendMode3D.Opaque or
                            BlendMode3D.Cutout)
                .ToArray();

        if (casters.Length ==
            0)
        {
            return
                DirectionalShadowPassResult.None;
        }

        Initialize();

        Matrix4x4 lightViewProjection =
            BuildDirectionalShadowMatrix(
                view,
                light);

        int previousFramebuffer =
            _shadowMap!.Begin(
                light.ShadowResolution);

        int drawCalls =
            0;

        try
        {
            GL.Enable(
                EnableCap.DepthTest);

            GL.DepthMask(
                true);

            GL.Disable(
                EnableCap.Blend);

            /*
             * Existing ByteEngine FBX content can contain mixed winding.
             * Keep shadow rendering double-sided for compatibility and use
             * depth bias instead of face culling to control acne.
             */
            GL.Disable(
                EnableCap.CullFace);

            GL.PolygonMode(
                MaterialFace.FrontAndBack,
                OpenTK.Graphics.OpenGL4.PolygonMode.Fill);

            _shadowShader!.Use();

            _shadowShader.SetMatrix(
                "uLightViewProjection",
                lightViewProjection);

            foreach (RenderSubmission submission
                     in casters)
            {
                _shadowShader.SetMatrix(
                    "uModel",
                    submission.ModelMatrix);

                bool alphaCutout =
                    submission.Material.BlendMode ==
                    BlendMode3D.Cutout &&
                    submission.Material.MainTexture !=
                    null;

                if (alphaCutout)
                {
                    submission.Material.MainTexture!.Bind(
                        0);

                    _shadowShader.SetInt(
                        "uTexture",
                        0);

                    _shadowShader.SetInt(
                        "uUseTexture",
                        1);

                    _shadowShader.SetFloat(
                        "uAlphaCutoff",
                        Math.Clamp(
                            submission.Material.AlphaCutoff,
                            0.0f,
                            1.0f));
                }
                else
                {
                    _shadowShader.SetInt(
                        "uUseTexture",
                        0);

                    _shadowShader.SetFloat(
                        "uAlphaCutoff",
                        0.0f);
                }

                submission.Mesh.Bind();

                GL.DrawElements(
                    BeginMode.Triangles,
                    submission.Mesh.IndexCount,
                    DrawElementsType.UnsignedInt,
                    0);

                drawCalls++;
            }

            GL.BindVertexArray(
                0);
        }
        finally
        {
            _shadowMap.End(
                previousFramebuffer,
                view.TargetWidth,
                view.TargetHeight);

            RestoreBaselineState();
        }

        return
            new DirectionalShadowPassResult(
                new RenderDirectionalShadow3D(
                    shadowLightIndex,
                    lightViewProjection,
                    _shadowMap.DepthTextureId,
                    _shadowMap.Resolution,
                    light.ShadowBias,
                    light.ShadowStrength),
                drawCalls);
    }

    public void Draw(
        Mesh mesh,
        Material material,
        Matrix4x4 model,
        Matrix4x4 view,
        Matrix4x4 projection,
        RenderLighting3D lighting,
        RenderDirectionalShadow3D? directionalShadow,
        bool receiveShadows)
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

            UploadDirectionalShadow(
                directionalShadow,
                receiveShadows);

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
            GL.ActiveTexture(
                TextureUnit.Texture0);

            RestoreBaselineState();
        }
    }

    public void Draw(
        Mesh mesh,
        Material material,
        Matrix4x4 model,
        Matrix4x4 view,
        Matrix4x4 projection,
        RenderLighting3D lighting)
    {
        Draw(
            mesh,
            material,
            model,
            view,
            projection,
            lighting,
            null,
            false);
    }

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
                ambientIntensity),
            null,
            false);
    }

    private void UploadDirectionalShadow(
        RenderDirectionalShadow3D? directionalShadow,
        bool receiveShadows)
    {
        if (!directionalShadow.HasValue ||
            !receiveShadows)
        {
            _shader!.SetInt(
                "uUseShadowMap",
                0);

            _shader.SetInt(
                "uReceiveShadows",
                receiveShadows
                    ? 1
                    : 0);

            _shader.SetInt(
                "uShadowLightIndex",
                -1);

            _shader.SetFloat(
                "uShadowStrength",
                0.0f);

            return;
        }

        RenderDirectionalShadow3D shadow =
            directionalShadow.Value;

        GL.ActiveTexture(
            TextureUnit.Texture2);

        GL.BindTexture(
            TextureTarget.Texture2D,
            shadow.DepthTextureId);

        _shader!.SetInt(
            "uShadowMap",
            2);

        _shader.SetInt(
            "uUseShadowMap",
            1);

        _shader.SetInt(
            "uReceiveShadows",
            1);

        _shader.SetInt(
            "uShadowLightIndex",
            shadow.DirectionalLightIndex);

        _shader.SetMatrix(
            "uLightViewProjection",
            shadow.LightViewProjection);

        _shader.SetFloat(
            "uShadowBias",
            shadow.Bias);

        _shader.SetFloat(
            "uShadowStrength",
            shadow.Strength);

        float texel =
            1.0f /
            Math.Max(
                shadow.Resolution,
                1);

        _shader.SetVector2(
            "uShadowTexelSize",
            new Vector2(
                texel,
                texel));
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

    private static Matrix4x4 BuildDirectionalShadowMatrix(
        RenderView3D view,
        RenderDirectionalLight3D light)
    {
        float distance =
            Math.Clamp(
                light.ShadowDistance,
                5.0f,
                500.0f);

        Vector3 cameraForward =
            -Vector3.UnitZ;

        if (Matrix4x4.Invert(
                view.ViewMatrix,
                out Matrix4x4 inverseView))
        {
            Vector3 transformedForward =
                Vector3.TransformNormal(
                    -Vector3.UnitZ,
                    inverseView);

            if (transformedForward.LengthSquared() >
                0.000001f)
            {
                cameraForward =
                    Vector3.Normalize(
                        transformedForward);
            }
        }

        Vector3 direction =
            light.Direction.LengthSquared() >
            0.000001f
                ? Vector3.Normalize(
                    light.Direction)
                : new Vector3(
                    0.0f,
                    -1.0f,
                    0.0f);

        Vector3 center =
            view.CameraPosition +
            cameraForward *
            (
                distance *
                0.35f
            );

        Vector3 lightPosition =
            center -
            direction *
            (
                distance *
                1.5f
            );

        Vector3 up =
            MathF.Abs(
                Vector3.Dot(
                    direction,
                    Vector3.UnitY)) >
            0.95f
                ? Vector3.UnitZ
                : Vector3.UnitY;

        Matrix4x4 lightView =
            Matrix4x4.CreateLookAt(
                lightPosition,
                center,
                up);

        Matrix4x4 lightProjection =
            Matrix4x4.CreateOrthographic(
                distance *
                2.0f,
                distance *
                2.0f,
                0.1f,
                distance *
                4.0f);

        return
            lightView *
            lightProjection;
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

        _shadowShader?.Dispose();

        _shadowShader =
            null;

        _shadowMap?.Dispose();

        _shadowMap =
            null;
    }
}
