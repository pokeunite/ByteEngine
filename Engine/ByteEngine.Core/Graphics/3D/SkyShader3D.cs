using System.Numerics;
using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// Isolated procedural-sky shader.
///
/// Keeping the sky shader separate from Shader3D prevents world-background
/// changes from destabilizing ByteEngine's validated material/shadow shader.
/// </summary>
internal sealed class SkyShader3D : IDisposable
{
    private readonly Shader _shader =
        new(
            VertexSource,
            FragmentSource);

    internal static RenderEnvironment3D CurrentEnvironment { get; set; } =
        RenderEnvironment3D.Default;

    public void Use()
    {
        _shader.Use();

        RenderEnvironment3D environment =
            CurrentEnvironment;

        bool useEnvironmentMap =
            environment.SkyMode ==
                SkyMode3D.EnvironmentMap &&
            environment.EnvironmentMapTexture !=
                null;

        _shader.SetInt(
            "uSkyMode",
            useEnvironmentMap
                ? 1
                : 0);

        _shader.SetFloat(
            "uEnvironmentIntensity",
            environment.EnvironmentIntensity);

        _shader.SetFloat(
            "uEnvironmentRotationRadians",
            environment.EnvironmentRotationDegrees *
            MathF.PI /
            180.0f);

        _shader.SetInt(
            "uEnvironmentIsHdr",
            useEnvironmentMap &&
            environment.EnvironmentMapTexture!.IsHdr
                ? 1
                : 0);

        if (useEnvironmentMap)
        {
            const int textureSlot =
                5;

            environment.EnvironmentMapTexture!.Bind(
                textureSlot);

            _shader.SetInt(
                "uEnvironmentMap",
                textureSlot);

            GL.ActiveTexture(
                TextureUnit.Texture0);
        }
    }

    public void SetMatrix(
        string name,
        Matrix4x4 value)
    {
        _shader.SetMatrix4(
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

    public void SetVector3(
        string name,
        Vector3 value) =>
        _shader.SetVector3(
            name,
            value);

    public void SetFloat(
        string name,
        float value) =>
        _shader.SetFloat(
            name,
            value);

    public void Dispose() =>
        _shader.Dispose();

    private const string VertexSource =
        """
        #version 330 core

        layout(location=0) in vec3 aPosition;

        uniform mat4 uView;
        uniform mat4 uProjection;

        out vec3 vDirection;

        void main()
        {
            mat4 rotationView=
                mat4(
                    mat3(
                        uView));

            vec4 clipPosition=
                uProjection*
                rotationView*
                vec4(
                    aPosition,
                    1.0);

            /*
             * Force the sky cube to the far plane. Renderer3D draws it with
             * depth writes disabled, so scene geometry remains unaffected.
             */
            gl_Position=
                clipPosition.xyww;

            vDirection=
                aPosition;
        }
        """;

    private const string FragmentSource =
        """
        #version 330 core

        in vec3 vDirection;

        out vec4 FragColor;

        uniform vec3 uZenithColor;
        uniform vec3 uHorizonColor;
        uniform vec3 uGroundColor;
        uniform float uSkyIntensity;
        uniform float uHorizonSharpness;

        uniform int uSkyMode;
        uniform sampler2D uEnvironmentMap;
        uniform float uEnvironmentIntensity;
        uniform float uEnvironmentRotationRadians;
        uniform int uEnvironmentIsHdr;

        void main()
        {
            vec3 direction=
                normalize(
                    vDirection);

            if(uSkyMode==1)
            {
                const float PI=
                    3.14159265359;

                vec3 rotatedDirection=
                    normalize(
                        vec3(
                            cos(uEnvironmentRotationRadians)*
                                direction.x-
                            sin(uEnvironmentRotationRadians)*
                                direction.z,
                            direction.y,
                            sin(uEnvironmentRotationRadians)*
                                direction.x+
                            cos(uEnvironmentRotationRadians)*
                                direction.z));

                float longitude=
                    atan(
                        rotatedDirection.z,
                        rotatedDirection.x);

                float latitude=
                    asin(
                        clamp(
                            rotatedDirection.y,
                            -1.0,
                            1.0));

                vec2 environmentUV=
                    vec2(
                        longitude/
                            (2.0*PI)+
                            0.5,
                        0.5-
                        latitude/
                            PI);

                vec3 environmentColor=
                    texture(
                        uEnvironmentMap,
                        environmentUV).rgb;

                /*
                 * PNG environment maps arrive as display-referred sRGB-like
                 * values. Convert them back toward linear before exposure and
                 * tone mapping. Native .hdr textures are already linear floats.
                 */
                if(uEnvironmentIsHdr==0)
                {
                    environmentColor=
                        pow(
                            max(
                                environmentColor,
                                vec3(0.0)),
                            vec3(2.2));
                }

                environmentColor*=
                    max(
                        uEnvironmentIntensity,
                        0.0);

                vec3 mappedEnvironment=
                    environmentColor/
                    (
                        environmentColor+
                        vec3(1.0)
                    );

                vec3 displayEnvironment=
                    pow(
                        clamp(
                            mappedEnvironment,
                            vec3(0.0),
                            vec3(1.0)),
                        vec3(
                            1.0/
                            2.2));

                FragColor=
                    vec4(
                        displayEnvironment,
                        1.0);

                return;
            }

            float sharpness=
                max(
                    uHorizonSharpness,
                    0.1);

            float upperBlend=
                pow(
                    clamp(
                        direction.y,
                        0.0,
                        1.0),
                    sharpness);

            float lowerBlend=
                pow(
                    clamp(
                        -direction.y,
                        0.0,
                        1.0),
                    sharpness);

            vec3 linearColor=
                direction.y>=0.0
                    ? mix(
                        uHorizonColor,
                        uZenithColor,
                        upperBlend)
                    : mix(
                        uHorizonColor,
                        uGroundColor,
                        lowerBlend);

            linearColor=
                max(
                    linearColor*
                    max(
                        uSkyIntensity,
                        0.0),
                    vec3(0.0));

            /*
             * Lightweight Reinhard mapping keeps HDR-ish sky values usable
             * without introducing a scene-wide post-processing dependency.
             */
            vec3 mapped=
                linearColor/
                (
                    linearColor+
                    vec3(1.0)
                );

            vec3 displayColor=
                pow(
                    clamp(
                        mapped,
                        vec3(0.0),
                        vec3(1.0)),
                    vec3(
                        1.0/
                        2.2));

            FragColor=
                vec4(
                    displayColor,
                    1.0);
        }
        """;
}
