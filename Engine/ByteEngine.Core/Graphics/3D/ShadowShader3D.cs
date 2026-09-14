using System.Numerics;

namespace ByteEngine.Core.Graphics.ThreeD;

internal sealed class ShadowShader3D : IDisposable
{
    private readonly Shader _shader =
        new(
            VertexSource,
            FragmentSource);

    public void Use() =>
        _shader.Use();

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

    public void SetFloat(
        string name,
        float value) =>
        _shader.SetFloat(
            name,
            value);

    public void SetInt(
        string name,
        int value) =>
        _shader.SetInt(
            name,
            value);

    public void Dispose() =>
        _shader.Dispose();

    private const string VertexSource =
        """
        #version 330 core

        layout(location=0) in vec3 aPosition;
        layout(location=2) in vec2 aUV;

        uniform mat4 uModel;
        uniform mat4 uLightViewProjection;

        out vec2 vUV;

        void main()
        {
            vUV=aUV;
            gl_Position=
                uLightViewProjection*
                uModel*
                vec4(aPosition,1.0);
        }
        """;

    private const string FragmentSource =
        """
        #version 330 core

        in vec2 vUV;

        uniform sampler2D uTexture;
        uniform int uUseTexture;
        uniform float uAlphaCutoff;

        void main()
        {
            if(uUseTexture==1)
            {
                float alpha=
                    texture(uTexture,vUV).a;

                if(alpha<uAlphaCutoff)
                    discard;
            }
        }
        """;
}
