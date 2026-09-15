using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// Final fullscreen presentation pass for a completed 3D scene.
///
/// Scene/material/sky shaders stay in linear HDR. This pass owns the single
/// HDR -> display conversion: exposure, ACES-style tone mapping, gamma and
/// final 8-bit dithering.
///
/// The same class can also perform a straight copy for ByteEngine's legacy 2D
/// display-space path.
/// </summary>
public sealed class PostProcess3D
    : IDisposable
{
    private Shader? _shader;

    private int _vertexArray;

    public void Render(
        int sourceTexture,
        int targetFramebuffer,
        int width,
        int height,
        bool applyToneMapping,
        float exposure)
    {
        if (sourceTexture ==
            0)
        {
            return;
        }

        EnsureResources();

        GL.BindFramebuffer(
            FramebufferTarget.Framebuffer,
            targetFramebuffer);

        GL.Viewport(
            0,
            0,
            Math.Max(
                width,
                1),
            Math.Max(
                height,
                1));

        GL.Disable(
            EnableCap.DepthTest);

        GL.DepthMask(
            false);

        GL.Disable(
            EnableCap.Blend);

        GL.Disable(
            EnableCap.CullFace);

        _shader!.Use();

        GL.ActiveTexture(
            TextureUnit.Texture0);

        GL.BindTexture(
            TextureTarget.Texture2D,
            sourceTexture);

        _shader.SetInt(
            "uSceneTexture",
            0);

        _shader.SetInt(
            "uApplyToneMapping",
            applyToneMapping
                ? 1
                : 0);

        _shader.SetFloat(
            "uExposure",
            Math.Max(
                exposure,
                0.0f));

        GL.BindVertexArray(
            _vertexArray);

        GL.DrawArrays(
            PrimitiveType.Triangles,
            0,
            3);

        GL.BindVertexArray(
            0);

        GL.BindTexture(
            TextureTarget.Texture2D,
            0);

        /*
         * Restore ByteEngine's normal renderer baseline. The caller owns the
         * framebuffer/viewport selected after this pass.
         */
        GL.DepthMask(
            true);

        GL.Enable(
            EnableCap.DepthTest);

        GL.Disable(
            EnableCap.Blend);

        GL.Disable(
            EnableCap.CullFace);
    }

    private void EnsureResources()
    {
        _shader ??=
            new Shader(
                VertexSource,
                FragmentSource);

        if (_vertexArray ==
            0)
        {
            _vertexArray =
                GL.GenVertexArray();
        }
    }

    public void Dispose()
    {
        _shader?.Dispose();
        _shader =
            null;

        if (_vertexArray !=
            0)
        {
            GL.DeleteVertexArray(
                _vertexArray);

            _vertexArray =
                0;
        }
    }

    private const string VertexSource =
        """
        #version 330 core

        out vec2 vUV;

        void main()
        {
            vec2 position;

            if(gl_VertexID==0)
            {
                position=
                    vec2(-1.0,-1.0);
            }
            else if(gl_VertexID==1)
            {
                position=
                    vec2(3.0,-1.0);
            }
            else
            {
                position=
                    vec2(-1.0,3.0);
            }

            vUV=
                position*
                0.5+
                0.5;

            gl_Position=
                vec4(
                    position,
                    0.0,
                    1.0);
        }
        """;

    private const string FragmentSource =
        """
        #version 330 core

        in vec2 vUV;

        out vec4 FragColor;

        uniform sampler2D uSceneTexture;
        uniform int uApplyToneMapping;
        uniform float uExposure;

        vec3 acesFilm(
            vec3 value)
        {
            const float a=
                2.51;

            const float b=
                0.03;

            const float c=
                2.43;

            const float d=
                0.59;

            const float e=
                0.14;

            return
                clamp(
                    (
                        value*
                        (
                            a*
                            value+
                            b
                        )
                    )/
                    (
                        value*
                        (
                            c*
                            value+
                            d
                        )+
                        e
                    ),
                    vec3(0.0),
                    vec3(1.0));
        }

        float screenDither(
            vec2 position)
        {
            return
                fract(
                    52.9829189*
                    fract(
                        dot(
                            position,
                            vec2(
                                0.06711056,
                                0.00583715))))-
                0.5;
        }

        void main()
        {
            vec4 source=
                texture(
                    uSceneTexture,
                    vUV);

            if(uApplyToneMapping==0)
            {
                FragColor=
                    source;

                return;
            }

            vec3 exposed=
                max(
                    source.rgb,
                    vec3(0.0))*
                max(
                    uExposure,
                    0.0);

            vec3 mapped=
                acesFilm(
                    exposed);

            vec3 displayColor=
                pow(
                    mapped,
                    vec3(
                        1.0/
                        2.2));

            /*
             * This is now the only display dither in the 3D pipeline, directly
             * before writing into the final RGBA8 presentation texture.
             */
            displayColor=
                clamp(
                    displayColor+
                    vec3(
                        screenDither(
                            gl_FragCoord.xy)/
                        255.0),
                    vec3(0.0),
                    vec3(1.0));

            FragColor=
                vec4(
                    displayColor,
                    source.a);
        }
        """;
}
