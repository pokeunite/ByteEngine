using System.Numerics;
using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// Preprocesses an equirectangular environment texture into the lighting data
/// used by ByteEngine's standard PBR shader:
///
/// 1. HDR/LDR equirectangular texture -> linear HDR cubemap
/// 2. diffuse irradiance cubemap
/// 3. GGX prefiltered specular cubemap across roughness mip levels
/// 4. split-sum BRDF integration LUT
///
/// The expensive work runs only when the source texture changes. Shader or
/// framebuffer failures are contained here so an unsupported GPU/path cannot
/// prevent the editor from opening.
/// </summary>
internal sealed class EnvironmentIbl3D : IDisposable
{
    private const int SourceCubeResolution = 512;
    private const int IrradianceResolution = 32;
    private const int PrefilterResolution = 128;
    private const int PrefilterMipCount = 5;
    private const int BrdfResolution = 256;

    private Texture2D? _sourceTexture;
    private int _sourceContentVersion = -1;
    private Texture2D? _failedSourceTexture;
    private int _failedSourceContentVersion = -1;

    private int _captureFramebuffer;
    private int _captureRenderbuffer;
    private int _environmentCube;
    private int _irradianceCube;
    private int _prefilterCube;
    private int _brdfLut;
    private int _fullscreenVertexArray;

    private Shader? _equirectangularShader;
    private Shader? _irradianceShader;
    private Shader? _prefilterShader;
    private Shader? _brdfShader;

    public bool IsReady =>
        _environmentCube != 0 &&
        _irradianceCube != 0 &&
        _prefilterCube != 0 &&
        _brdfLut != 0;

    public float MaxReflectionLod =>
        PrefilterMipCount - 1;

    public bool IsReadyFor(Texture2D texture) =>
        IsReady &&
        ReferenceEquals(
            texture,
            _sourceTexture) &&
        texture.ContentVersion ==
            _sourceContentVersion;

    public bool TryPrepare(
        Texture2D sourceTexture,
        Mesh cube,
        int targetWidth,
        int targetHeight)
    {
        ArgumentNullException.ThrowIfNull(sourceTexture);
        ArgumentNullException.ThrowIfNull(cube);

        if (IsReadyFor(sourceTexture))
        {
            return true;
        }

        if (ReferenceEquals(
                sourceTexture,
                _failedSourceTexture) &&
            sourceTexture.ContentVersion ==
                _failedSourceContentVersion)
        {
            return false;
        }

        GL.GetInteger(
            GetPName.FramebufferBinding,
            out int previousFramebuffer);

        try
        {
            EnsureShaders();
            EnsureCaptureObjects();

            DestroyProcessedTextures();

            BuildEnvironmentCube(
                sourceTexture,
                cube);

            BuildIrradianceCube(
                cube);

            BuildPrefilterCube(
                cube);

            BuildBrdfLut();

            _sourceTexture =
                sourceTexture;

            _sourceContentVersion =
                sourceTexture.ContentVersion;

            _failedSourceTexture =
                null;

            _failedSourceContentVersion =
                -1;

            return true;
        }
        catch (Exception exception)
        {
            DestroyProcessedTextures();

            _sourceTexture =
                null;

            _sourceContentVersion =
                -1;

            _failedSourceTexture =
                sourceTexture;

            _failedSourceContentVersion =
                sourceTexture.ContentVersion;

            Console.Error.WriteLine(
                "ByteEngine environment IBL preprocessing failed. " +
                "The editor will continue without processed IBL for this texture.");

            Console.Error.WriteLine(
                exception);

            return false;
        }
        finally
        {
            GL.BindFramebuffer(
                FramebufferTarget.Framebuffer,
                previousFramebuffer);

            GL.BindRenderbuffer(
                RenderbufferTarget.Renderbuffer,
                0);

            GL.BindVertexArray(
                0);

            GL.ActiveTexture(
                TextureUnit.Texture0);

            GL.BindTexture(
                TextureTarget.Texture2D,
                0);

            GL.BindTexture(
                TextureTarget.TextureCubeMap,
                0);

            GL.Viewport(
                0,
                0,
                Math.Max(
                    targetWidth,
                    1),
                Math.Max(
                    targetHeight,
                    1));
        }
    }

    public void BindIrradiance(
        int textureSlot)
    {
        BindCube(
            _irradianceCube,
            textureSlot);
    }

    public void BindPrefilter(
        int textureSlot)
    {
        BindCube(
            _prefilterCube,
            textureSlot);
    }

    public void BindBrdfLut(
        int textureSlot)
    {
        if (_brdfLut ==
            0)
        {
            return;
        }

        GL.ActiveTexture(
            (TextureUnit)(
                (int)TextureUnit.Texture0 +
                textureSlot));

        GL.BindTexture(
            TextureTarget.Texture2D,
            _brdfLut);
    }

    private static void BindCube(
        int texture,
        int textureSlot)
    {
        if (texture ==
            0)
        {
            return;
        }

        GL.ActiveTexture(
            (TextureUnit)(
                (int)TextureUnit.Texture0 +
                textureSlot));

        GL.BindTexture(
            TextureTarget.TextureCubeMap,
            texture);
    }

    private void EnsureShaders()
    {
        _equirectangularShader ??=
            new Shader(
                CubeVertexSource,
                EquirectangularFragmentSource);

        _irradianceShader ??=
            new Shader(
                CubeVertexSource,
                IrradianceFragmentSource);

        _prefilterShader ??=
            new Shader(
                CubeVertexSource,
                PrefilterFragmentSource);

        _brdfShader ??=
            new Shader(
                FullscreenTriangleVertexSource,
                BrdfFragmentSource);
    }

    private void EnsureCaptureObjects()
    {
        if (_captureFramebuffer ==
            0)
        {
            _captureFramebuffer =
                GL.GenFramebuffer();
        }

        if (_captureRenderbuffer ==
            0)
        {
            _captureRenderbuffer =
                GL.GenRenderbuffer();
        }

        if (_fullscreenVertexArray ==
            0)
        {
            _fullscreenVertexArray =
                GL.GenVertexArray();
        }
    }

    private void BuildEnvironmentCube(
        Texture2D sourceTexture,
        Mesh cube)
    {
        _environmentCube =
            CreateCubeTexture(
                SourceCubeResolution,
                mipmapped: true,
                maxMipLevel: CalculateMipCount(
                    SourceCubeResolution) -
                    1);

        ConfigureCaptureTarget(
            SourceCubeResolution);

        Matrix4x4 projection =
            CreateCaptureProjection();

        Matrix4x4[] views =
            CreateCaptureViews();

        _equirectangularShader!.Use();

        SetMatrix(
            _equirectangularShader,
            "uProjection",
            projection);

        sourceTexture.Bind(
            0);

        _equirectangularShader.SetInt(
            "uEnvironmentMap",
            0);

        _equirectangularShader.SetInt(
            "uSourceIsHdr",
            sourceTexture.IsHdr
                ? 1
                : 0);

        GL.Disable(
            EnableCap.CullFace);

        GL.Enable(
            EnableCap.DepthTest);

        GL.DepthMask(
            true);

        cube.Bind();

        for (int faceIndex = 0;
             faceIndex < 6;
             faceIndex++)
        {
            SetMatrix(
                _equirectangularShader,
                "uView",
                views[faceIndex]);

            AttachCubeFace(
                _environmentCube,
                faceIndex,
                0);

            GL.Clear(
                ClearBufferMask.ColorBufferBit |
                ClearBufferMask.DepthBufferBit);

            GL.DrawElements(
                BeginMode.Triangles,
                cube.IndexCount,
                DrawElementsType.UnsignedInt,
                0);
        }

        GL.BindTexture(
            TextureTarget.TextureCubeMap,
            _environmentCube);

        GL.GenerateMipmap(
            GenerateMipmapTarget.TextureCubeMap);

        GL.BindTexture(
            TextureTarget.TextureCubeMap,
            0);

        GL.BindVertexArray(
            0);
    }

    private void BuildIrradianceCube(
        Mesh cube)
    {
        _irradianceCube =
            CreateCubeTexture(
                IrradianceResolution,
                mipmapped: false,
                maxMipLevel: 0);

        ConfigureCaptureTarget(
            IrradianceResolution);

        Matrix4x4 projection =
            CreateCaptureProjection();

        Matrix4x4[] views =
            CreateCaptureViews();

        _irradianceShader!.Use();

        SetMatrix(
            _irradianceShader,
            "uProjection",
            projection);

        GL.ActiveTexture(
            TextureUnit.Texture0);

        GL.BindTexture(
            TextureTarget.TextureCubeMap,
            _environmentCube);

        _irradianceShader.SetInt(
            "uEnvironmentMap",
            0);

        cube.Bind();

        for (int faceIndex = 0;
             faceIndex < 6;
             faceIndex++)
        {
            SetMatrix(
                _irradianceShader,
                "uView",
                views[faceIndex]);

            AttachCubeFace(
                _irradianceCube,
                faceIndex,
                0);

            GL.Clear(
                ClearBufferMask.ColorBufferBit |
                ClearBufferMask.DepthBufferBit);

            GL.DrawElements(
                BeginMode.Triangles,
                cube.IndexCount,
                DrawElementsType.UnsignedInt,
                0);
        }

        GL.BindVertexArray(
            0);
    }

    private void BuildPrefilterCube(
        Mesh cube)
    {
        _prefilterCube =
            CreateCubeTexture(
                PrefilterResolution,
                mipmapped: true,
                maxMipLevel: PrefilterMipCount -
                    1,
                allocateAllMipLevels: true,
                explicitMipCount: PrefilterMipCount);

        Matrix4x4 projection =
            CreateCaptureProjection();

        Matrix4x4[] views =
            CreateCaptureViews();

        _prefilterShader!.Use();

        SetMatrix(
            _prefilterShader,
            "uProjection",
            projection);

        GL.ActiveTexture(
            TextureUnit.Texture0);

        GL.BindTexture(
            TextureTarget.TextureCubeMap,
            _environmentCube);

        _prefilterShader.SetInt(
            "uEnvironmentMap",
            0);

        _prefilterShader.SetFloat(
            "uEnvironmentResolution",
            SourceCubeResolution);

        cube.Bind();

        for (int mip = 0;
             mip < PrefilterMipCount;
             mip++)
        {
            int mipResolution =
                Math.Max(
                    1,
                    PrefilterResolution >>
                    mip);

            ConfigureCaptureTarget(
                mipResolution);

            float roughness =
                PrefilterMipCount >
                    1
                    ? (float)mip /
                      (PrefilterMipCount -
                       1)
                    : 0.0f;

            _prefilterShader.SetFloat(
                "uRoughness",
                roughness);

            for (int faceIndex = 0;
                 faceIndex < 6;
                 faceIndex++)
            {
                SetMatrix(
                    _prefilterShader,
                    "uView",
                    views[faceIndex]);

                AttachCubeFace(
                    _prefilterCube,
                    faceIndex,
                    mip);

                GL.Clear(
                    ClearBufferMask.ColorBufferBit |
                    ClearBufferMask.DepthBufferBit);

                GL.DrawElements(
                    BeginMode.Triangles,
                    cube.IndexCount,
                    DrawElementsType.UnsignedInt,
                    0);
            }
        }

        GL.BindVertexArray(
            0);
    }

    private void BuildBrdfLut()
    {
        _brdfLut =
            GL.GenTexture();

        GL.BindTexture(
            TextureTarget.Texture2D,
            _brdfLut);

        GL.TexImage2D(
            TextureTarget.Texture2D,
            0,
            PixelInternalFormat.Rgba16f,
            BrdfResolution,
            BrdfResolution,
            0,
            PixelFormat.Rgba,
            PixelType.Float,
            IntPtr.Zero);

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureWrapS,
            (int)TextureWrapMode.ClampToEdge);

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureWrapT,
            (int)TextureWrapMode.ClampToEdge);

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter,
            (int)TextureMinFilter.Linear);

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter,
            (int)TextureMagFilter.Linear);

        GL.BindFramebuffer(
            FramebufferTarget.Framebuffer,
            _captureFramebuffer);

        GL.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D,
            _brdfLut,
            0);

        GL.DrawBuffer(
            DrawBufferMode.ColorAttachment0);

        EnsureFramebufferComplete(
            "BRDF LUT");

        GL.Viewport(
            0,
            0,
            BrdfResolution,
            BrdfResolution);

        GL.Disable(
            EnableCap.DepthTest);

        GL.DepthMask(
            false);

        GL.Disable(
            EnableCap.CullFace);

        _brdfShader!.Use();

        GL.BindVertexArray(
            _fullscreenVertexArray);

        GL.Clear(
            ClearBufferMask.ColorBufferBit);

        GL.DrawArrays(
            PrimitiveType.Triangles,
            0,
            3);

        GL.BindVertexArray(
            0);

        GL.DepthMask(
            true);
    }

    private int CreateCubeTexture(
        int resolution,
        bool mipmapped,
        int maxMipLevel,
        bool allocateAllMipLevels = false,
        int explicitMipCount = 1)
    {
        int texture =
            GL.GenTexture();

        GL.BindTexture(
            TextureTarget.TextureCubeMap,
            texture);

        int mipCount =
            allocateAllMipLevels
                ? explicitMipCount
                : 1;

        for (int mip = 0;
             mip < mipCount;
             mip++)
        {
            int mipResolution =
                Math.Max(
                    1,
                    resolution >>
                    mip);

            for (int faceIndex = 0;
                 faceIndex < 6;
                 faceIndex++)
            {
                TextureTarget face =
                    (TextureTarget)(
                        (int)TextureTarget.TextureCubeMapPositiveX +
                        faceIndex);

                GL.TexImage2D(
                    face,
                    mip,
                    PixelInternalFormat.Rgba16f,
                    mipResolution,
                    mipResolution,
                    0,
                    PixelFormat.Rgba,
                    PixelType.Float,
                    IntPtr.Zero);
            }
        }

        GL.TexParameter(
            TextureTarget.TextureCubeMap,
            TextureParameterName.TextureWrapS,
            (int)TextureWrapMode.ClampToEdge);

        GL.TexParameter(
            TextureTarget.TextureCubeMap,
            TextureParameterName.TextureWrapT,
            (int)TextureWrapMode.ClampToEdge);

        GL.TexParameter(
            TextureTarget.TextureCubeMap,
            TextureParameterName.TextureWrapR,
            (int)TextureWrapMode.ClampToEdge);

        GL.TexParameter(
            TextureTarget.TextureCubeMap,
            TextureParameterName.TextureMinFilter,
            (int)(
                mipmapped
                    ? TextureMinFilter.LinearMipmapLinear
                    : TextureMinFilter.Linear));

        GL.TexParameter(
            TextureTarget.TextureCubeMap,
            TextureParameterName.TextureMagFilter,
            (int)TextureMagFilter.Linear);

        if (mipmapped)
        {
            GL.TexParameter(
                TextureTarget.TextureCubeMap,
                TextureParameterName.TextureMaxLevel,
                maxMipLevel);
        }

        GL.BindTexture(
            TextureTarget.TextureCubeMap,
            0);

        return texture;
    }

    private void ConfigureCaptureTarget(
        int resolution)
    {
        GL.BindFramebuffer(
            FramebufferTarget.Framebuffer,
            _captureFramebuffer);

        GL.BindRenderbuffer(
            RenderbufferTarget.Renderbuffer,
            _captureRenderbuffer);

        GL.RenderbufferStorage(
            RenderbufferTarget.Renderbuffer,
            RenderbufferStorage.DepthComponent24,
            resolution,
            resolution);

        GL.FramebufferRenderbuffer(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.DepthAttachment,
            RenderbufferTarget.Renderbuffer,
            _captureRenderbuffer);

        GL.DrawBuffer(
            DrawBufferMode.ColorAttachment0);

        GL.Viewport(
            0,
            0,
            resolution,
            resolution);
    }

    private void AttachCubeFace(
        int cubeTexture,
        int faceIndex,
        int mipLevel)
    {
        TextureTarget face =
            (TextureTarget)(
                (int)TextureTarget.TextureCubeMapPositiveX +
                faceIndex);

        GL.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.ColorAttachment0,
            face,
            cubeTexture,
            mipLevel);

        EnsureFramebufferComplete(
            "environment cubemap");
    }

    private static void EnsureFramebufferComplete(
        string label)
    {
        FramebufferErrorCode status =
            GL.CheckFramebufferStatus(
                FramebufferTarget.Framebuffer);

        if (status !=
            FramebufferErrorCode.FramebufferComplete)
        {
            throw new InvalidOperationException(
                $"{label} framebuffer is incomplete: {status}");
        }
    }

    private static Matrix4x4 CreateCaptureProjection() =>
        Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI *
            0.5f,
            1.0f,
            0.1f,
            10.0f);

    private static Matrix4x4[] CreateCaptureViews() =>
        new[]
        {
            Matrix4x4.CreateLookAt(
                Vector3.Zero,
                Vector3.UnitX,
                -Vector3.UnitY),

            Matrix4x4.CreateLookAt(
                Vector3.Zero,
                -Vector3.UnitX,
                -Vector3.UnitY),

            Matrix4x4.CreateLookAt(
                Vector3.Zero,
                Vector3.UnitY,
                Vector3.UnitZ),

            Matrix4x4.CreateLookAt(
                Vector3.Zero,
                -Vector3.UnitY,
                -Vector3.UnitZ),

            Matrix4x4.CreateLookAt(
                Vector3.Zero,
                Vector3.UnitZ,
                -Vector3.UnitY),

            Matrix4x4.CreateLookAt(
                Vector3.Zero,
                -Vector3.UnitZ,
                -Vector3.UnitY)
        };

    private static void SetMatrix(
        Shader shader,
        string name,
        Matrix4x4 value)
    {
        shader.SetMatrix4(
            name,
            new OpenTK.Mathematics.Matrix4(
                value.M11,
                value.M12,
                value.M13,
                value.M14,
                value.M21,
                value.M22,
                value.M23,
                value.M24,
                value.M31,
                value.M32,
                value.M33,
                value.M34,
                value.M41,
                value.M42,
                value.M43,
                value.M44));
    }

    private static int CalculateMipCount(
        int resolution)
    {
        int levels =
            1;

        int value =
            Math.Max(
                resolution,
                1);

        while (value >
            1)
        {
            value >>=
                1;

            levels++;
        }

        return levels;
    }

    private void DestroyProcessedTextures()
    {
        DeleteTexture(
            ref _environmentCube);

        DeleteTexture(
            ref _irradianceCube);

        DeleteTexture(
            ref _prefilterCube);

        DeleteTexture(
            ref _brdfLut);
    }

    private static void DeleteTexture(
        ref int texture)
    {
        if (texture ==
            0)
        {
            return;
        }

        GL.DeleteTexture(
            texture);

        texture =
            0;
    }

    public void Dispose()
    {
        DestroyProcessedTextures();

        _equirectangularShader?.Dispose();
        _equirectangularShader = null;

        _irradianceShader?.Dispose();
        _irradianceShader = null;

        _prefilterShader?.Dispose();
        _prefilterShader = null;

        _brdfShader?.Dispose();
        _brdfShader = null;

        if (_fullscreenVertexArray !=
            0)
        {
            GL.DeleteVertexArray(
                _fullscreenVertexArray);

            _fullscreenVertexArray =
                0;
        }

        if (_captureRenderbuffer !=
            0)
        {
            GL.DeleteRenderbuffer(
                _captureRenderbuffer);

            _captureRenderbuffer =
                0;
        }

        if (_captureFramebuffer !=
            0)
        {
            GL.DeleteFramebuffer(
                _captureFramebuffer);

            _captureFramebuffer =
                0;
        }

        _sourceTexture =
            null;

        _failedSourceTexture =
            null;
    }

    private const string CubeVertexSource =
        """
        #version 330 core

        layout(location=0) in vec3 aPosition;

        uniform mat4 uProjection;
        uniform mat4 uView;

        out vec3 vLocalPosition;

        void main()
        {
            vLocalPosition=
                aPosition;

            gl_Position=
                uProjection*
                uView*
                vec4(
                    aPosition,
                    1.0);
        }
        """;

    private const string EquirectangularFragmentSource =
        """
        #version 330 core

        in vec3 vLocalPosition;

        out vec4 FragColor;

        uniform sampler2D uEnvironmentMap;
        uniform int uSourceIsHdr;

        const float PI=
            3.14159265359;

        vec2 directionToEquirectangularUv(
            vec3 direction)
        {
            vec3 d=
                normalize(
                    direction);

            float longitude=
                atan(
                    d.z,
                    d.x);

            float latitude=
                asin(
                    clamp(
                        d.y,
                        -1.0,
                        1.0));

            return
                vec2(
                    longitude/
                        (2.0*PI)+
                        0.5,
                    0.5-
                        latitude/
                        PI);
        }

        void main()
        {
            vec3 color=
                texture(
                    uEnvironmentMap,
                    directionToEquirectangularUv(
                        vLocalPosition)).rgb;

            if(uSourceIsHdr==0)
            {
                color=
                    pow(
                        max(
                            color,
                            vec3(0.0)),
                        vec3(2.2));
            }

            FragColor=
                vec4(
                    color,
                    1.0);
        }
        """;

    private const string IrradianceFragmentSource =
        """
        #version 330 core

        in vec3 vLocalPosition;

        out vec4 FragColor;

        uniform samplerCube uEnvironmentMap;

        const float PI=
            3.14159265359;

        void main()
        {
            vec3 normal=
                normalize(
                    vLocalPosition);

            vec3 irradiance=
                vec3(0.0);

            vec3 up=
                abs(normal.y)>
                    0.999
                    ? vec3(
                        0.0,
                        0.0,
                        1.0)
                    : vec3(
                        0.0,
                        1.0,
                        0.0);

            vec3 right=
                normalize(
                    cross(
                        up,
                        normal));

            up=
                normalize(
                    cross(
                        normal,
                        right));

            float sampleDelta=
                0.10;

            float sampleCount=
                0.0;

            for(
                float phi=0.0;
                phi<2.0*PI;
                phi+=sampleDelta)
            {
                for(
                    float theta=0.0;
                    theta<0.5*PI;
                    theta+=sampleDelta)
                {
                    vec3 tangentSample=
                        vec3(
                            sin(theta)*
                                cos(phi),
                            sin(theta)*
                                sin(phi),
                            cos(theta));

                    vec3 sampleVector=
                        tangentSample.x*
                            right+
                        tangentSample.y*
                            up+
                        tangentSample.z*
                            normal;

                    irradiance+=
                        texture(
                            uEnvironmentMap,
                            sampleVector).rgb*
                        cos(theta)*
                        sin(theta);

                    sampleCount+=
                        1.0;
                }
            }

            irradiance=
                PI*
                irradiance/
                max(
                    sampleCount,
                    1.0);

            FragColor=
                vec4(
                    irradiance,
                    1.0);
        }
        """;

    private const string PrefilterFragmentSource =
        """
        #version 330 core

        in vec3 vLocalPosition;

        out vec4 FragColor;

        uniform samplerCube uEnvironmentMap;
        uniform float uRoughness;
        uniform float uEnvironmentResolution;

        const float PI=
            3.14159265359;

        float radicalInverseVdc(
            uint bits)
        {
            bits=
                (bits<<16u)|
                (bits>>16u);

            bits=
                (
                    (bits&
                        0x55555555u)<<
                    1u
                )|
                (
                    (bits&
                        0xAAAAAAAAu)>>
                    1u
                );

            bits=
                (
                    (bits&
                        0x33333333u)<<
                    2u
                )|
                (
                    (bits&
                        0xCCCCCCCCu)>>
                    2u
                );

            bits=
                (
                    (bits&
                        0x0F0F0F0Fu)<<
                    4u
                )|
                (
                    (bits&
                        0xF0F0F0F0u)>>
                    4u
                );

            bits=
                (
                    (bits&
                        0x00FF00FFu)<<
                    8u
                )|
                (
                    (bits&
                        0xFF00FF00u)>>
                    8u
                );

            return
                float(bits)*
                2.3283064365386963e-10;
        }

        vec2 hammersley(
            uint index,
            uint count)
        {
            return
                vec2(
                    float(index)/
                        float(count),
                    radicalInverseVdc(
                        index));
        }

        float distributionGgx(
            vec3 normal,
            vec3 halfway,
            float roughness)
        {
            float a=
                roughness*
                roughness;

            float a2=
                a*
                a;

            float nDotH=
                max(
                    dot(
                        normal,
                        halfway),
                    0.0);

            float nDotH2=
                nDotH*
                nDotH;

            float denominator=
                nDotH2*
                (a2-1.0)+
                1.0;

            return
                a2/
                max(
                    PI*
                    denominator*
                    denominator,
                    0.000001);
        }

        vec3 importanceSampleGgx(
            vec2 xi,
            vec3 normal,
            float roughness)
        {
            float a=
                roughness*
                roughness;

            float phi=
                2.0*
                PI*
                xi.x;

            float cosineTheta=
                sqrt(
                    (1.0-xi.y)/
                    (
                        1.0+
                        (a*a-1.0)*
                        xi.y
                    ));

            float sineTheta=
                sqrt(
                    max(
                        1.0-
                        cosineTheta*
                        cosineTheta,
                        0.0));

            vec3 halfway=
                vec3(
                    cos(phi)*
                        sineTheta,
                    sin(phi)*
                        sineTheta,
                    cosineTheta);

            vec3 up=
                abs(normal.z)<
                    0.999
                    ? vec3(
                        0.0,
                        0.0,
                        1.0)
                    : vec3(
                        1.0,
                        0.0,
                        0.0);

            vec3 tangent=
                normalize(
                    cross(
                        up,
                        normal));

            vec3 bitangent=
                cross(
                    normal,
                    tangent);

            return
                normalize(
                    tangent*
                        halfway.x+
                    bitangent*
                        halfway.y+
                    normal*
                        halfway.z);
        }

        void main()
        {
            vec3 normal=
                normalize(
                    vLocalPosition);

            vec3 reflection=
                normal;

            vec3 viewDirection=
                reflection;

            const uint sampleCount=
                256u;

            vec3 prefilteredColor=
                vec3(0.0);

            float totalWeight=
                0.0;

            for(
                uint index=0u;
                index<sampleCount;
                index++)
            {
                vec2 xi=
                    hammersley(
                        index,
                        sampleCount);

                vec3 halfway=
                    importanceSampleGgx(
                        xi,
                        normal,
                        max(
                            uRoughness,
                            0.001));

                vec3 lightDirection=
                    normalize(
                        2.0*
                        dot(
                            viewDirection,
                            halfway)*
                        halfway-
                        viewDirection);

                float nDotL=
                    max(
                        dot(
                            normal,
                            lightDirection),
                        0.0);

                if(nDotL<=0.0)
                {
                    continue;
                }

                float nDotH=
                    max(
                        dot(
                            normal,
                            halfway),
                        0.0);

                float hDotV=
                    max(
                        dot(
                            halfway,
                            viewDirection),
                        0.0);

                float distribution=
                    distributionGgx(
                        normal,
                        halfway,
                        max(
                            uRoughness,
                            0.001));

                float pdf=
                    distribution*
                    nDotH/
                    max(
                        4.0*
                        hDotV,
                        0.0001)+
                    0.0001;

                float texelSolidAngle=
                    4.0*
                    PI/
                    (
                        6.0*
                        uEnvironmentResolution*
                        uEnvironmentResolution
                    );

                float sampleSolidAngle=
                    1.0/
                    (
                        float(sampleCount)*
                        pdf+
                        0.0001
                    );

                float mipLevel=
                    uRoughness<=
                        0.001
                        ? 0.0
                        : 0.5*
                          log2(
                              sampleSolidAngle/
                              texelSolidAngle);

                prefilteredColor+=
                    textureLod(
                        uEnvironmentMap,
                        lightDirection,
                        max(
                            mipLevel,
                            0.0)).rgb*
                    nDotL;

                totalWeight+=
                    nDotL;
            }

            prefilteredColor/=
                max(
                    totalWeight,
                    0.0001);

            FragColor=
                vec4(
                    prefilteredColor,
                    1.0);
        }
        """;

    private const string FullscreenTriangleVertexSource =
        """
        #version 330 core

        out vec2 vUv;

        void main()
        {
            vec2 position=
                vec2(
                    float(
                        (gl_VertexID<<1)&2),
                    float(
                        gl_VertexID&2));

            vUv=
                position;

            gl_Position=
                vec4(
                    position*
                        2.0-
                        1.0,
                    0.0,
                    1.0);
        }
        """;

    private const string BrdfFragmentSource =
        """
        #version 330 core

        in vec2 vUv;

        out vec4 FragColor;

        const float PI=
            3.14159265359;

        float radicalInverseVdc(
            uint bits)
        {
            bits=
                (bits<<16u)|
                (bits>>16u);

            bits=
                (
                    (bits&
                        0x55555555u)<<
                    1u
                )|
                (
                    (bits&
                        0xAAAAAAAAu)>>
                    1u
                );

            bits=
                (
                    (bits&
                        0x33333333u)<<
                    2u
                )|
                (
                    (bits&
                        0xCCCCCCCCu)>>
                    2u
                );

            bits=
                (
                    (bits&
                        0x0F0F0F0Fu)<<
                    4u
                )|
                (
                    (bits&
                        0xF0F0F0F0u)>>
                    4u
                );

            bits=
                (
                    (bits&
                        0x00FF00FFu)<<
                    8u
                )|
                (
                    (bits&
                        0xFF00FF00u)>>
                    8u
                );

            return
                float(bits)*
                2.3283064365386963e-10;
        }

        vec2 hammersley(
            uint index,
            uint count)
        {
            return
                vec2(
                    float(index)/
                        float(count),
                    radicalInverseVdc(
                        index));
        }

        vec3 importanceSampleGgx(
            vec2 xi,
            vec3 normal,
            float roughness)
        {
            float a=
                roughness*
                roughness;

            float phi=
                2.0*
                PI*
                xi.x;

            float cosineTheta=
                sqrt(
                    (1.0-xi.y)/
                    (
                        1.0+
                        (a*a-1.0)*
                        xi.y
                    ));

            float sineTheta=
                sqrt(
                    max(
                        1.0-
                        cosineTheta*
                        cosineTheta,
                        0.0));

            vec3 halfway=
                vec3(
                    cos(phi)*
                        sineTheta,
                    sin(phi)*
                        sineTheta,
                    cosineTheta);

            vec3 up=
                abs(normal.z)<
                    0.999
                    ? vec3(
                        0.0,
                        0.0,
                        1.0)
                    : vec3(
                        1.0,
                        0.0,
                        0.0);

            vec3 tangent=
                normalize(
                    cross(
                        up,
                        normal));

            vec3 bitangent=
                cross(
                    normal,
                    tangent);

            return
                normalize(
                    tangent*
                        halfway.x+
                    bitangent*
                        halfway.y+
                    normal*
                        halfway.z);
        }

        float geometrySchlickGgx(
            float nDotV,
            float roughness)
        {
            float k=
                (
                    roughness*
                    roughness
                )/
                2.0;

            return
                nDotV/
                max(
                    nDotV*
                        (1.0-k)+
                    k,
                    0.0001);
        }

        float geometrySmith(
            vec3 normal,
            vec3 viewDirection,
            vec3 lightDirection,
            float roughness)
        {
            float nDotV=
                max(
                    dot(
                        normal,
                        viewDirection),
                    0.0);

            float nDotL=
                max(
                    dot(
                        normal,
                        lightDirection),
                    0.0);

            return
                geometrySchlickGgx(
                    nDotV,
                    roughness)*
                geometrySchlickGgx(
                    nDotL,
                    roughness);
        }

        vec2 integrateBrdf(
            float nDotV,
            float roughness)
        {
            vec3 viewDirection=
                vec3(
                    sqrt(
                        max(
                            1.0-
                            nDotV*
                            nDotV,
                            0.0)),
                    0.0,
                    nDotV);

            vec3 normal=
                vec3(
                    0.0,
                    0.0,
                    1.0);

            float scale=
                0.0;

            float bias=
                0.0;

            const uint sampleCount=
                256u;

            for(
                uint index=0u;
                index<sampleCount;
                index++)
            {
                vec2 xi=
                    hammersley(
                        index,
                        sampleCount);

                vec3 halfway=
                    importanceSampleGgx(
                        xi,
                        normal,
                        max(
                            roughness,
                            0.001));

                vec3 lightDirection=
                    normalize(
                        2.0*
                        dot(
                            viewDirection,
                            halfway)*
                        halfway-
                        viewDirection);

                float nDotL=
                    max(
                        lightDirection.z,
                        0.0);

                float nDotH=
                    max(
                        halfway.z,
                        0.0);

                float vDotH=
                    max(
                        dot(
                            viewDirection,
                            halfway),
                        0.0);

                if(nDotL<=0.0)
                {
                    continue;
                }

                float geometry=
                    geometrySmith(
                        normal,
                        viewDirection,
                        lightDirection,
                        roughness);

                float visibility=
                    geometry*
                    vDotH/
                    max(
                        nDotH*
                        nDotV,
                        0.0001);

                float fresnel=
                    pow(
                        1.0-
                        vDotH,
                        5.0);

                scale+=
                    (1.0-fresnel)*
                    visibility;

                bias+=
                    fresnel*
                    visibility;
            }

            return
                vec2(
                    scale,
                    bias)/
                float(sampleCount);
        }

        void main()
        {
            vec2 integrated=
                integrateBrdf(
                    clamp(
                        vUv.x,
                        0.0,
                        1.0),
                    clamp(
                        vUv.y,
                        0.0,
                        1.0));

            FragColor=
                vec4(
                    integrated,
                    0.0,
                    1.0);
        }
        """;
}
