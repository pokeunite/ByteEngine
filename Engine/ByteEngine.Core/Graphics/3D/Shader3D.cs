using System.Numerics;

namespace ByteEngine.Core.Graphics.ThreeD;

internal sealed class Shader3D : IDisposable
{
    private readonly Shader _shader = new(VertexSource, FragmentSource);

    public void Use() => _shader.Use();

    public void SetMatrix(string name, Matrix4x4 value)
    {
        _shader.SetMatrix4(name, new OpenTK.Mathematics.Matrix4(
            value.M11, value.M12, value.M13, value.M14,
            value.M21, value.M22, value.M23, value.M24,
            value.M31, value.M32, value.M33, value.M34,
            value.M41, value.M42, value.M43, value.M44));
    }

    public void SetVector3(string name, Vector3 value) => _shader.SetVector3(name, value);
    public void SetVector4(string name, Vector4 value) => _shader.SetVector4(name, value);
    public void SetFloat(string name, float value) => _shader.SetFloat(name, value);
    public void SetInt(string name, int value) => _shader.SetInt(name, value);
    public void Dispose() => _shader.Dispose();

    private const string VertexSource = """
        #version 330 core
        layout(location=0) in vec3 aPosition;
        layout(location=1) in vec3 aNormal;
        layout(location=2) in vec2 aUV;
        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;
        out vec3 vNormal;
        out vec2 vUV;
        void main()
        {
            vNormal=mat3(transpose(inverse(uModel)))*aNormal;
            vUV=aUV;
            gl_Position=uProjection*uView*uModel*vec4(aPosition,1.0);
        }
        """;

    private const string FragmentSource = """
        #version 330 core
        in vec3 vNormal;
        in vec2 vUV;
        out vec4 FragColor;
        uniform vec4 uBaseColor;
        uniform vec3 uLightDirection;
        uniform vec3 uLightColor;
        uniform float uLightIntensity;
        uniform float uAmbientIntensity;
        uniform sampler2D uTexture;
        uniform int uUseTexture;
        void main()
        {
            vec4 base=uBaseColor*(uUseTexture==1?texture(uTexture,vUV):vec4(1.0));
            float d=max(dot(normalize(vNormal),normalize(-uLightDirection)),0.0);
            vec3 linearColor=base.rgb*(vec3(uAmbientIntensity)+uLightColor*d*uLightIntensity);
            vec3 displayColor=pow(clamp(linearColor,vec3(0.0),vec3(1.0)),vec3(1.0/2.2));
            FragColor=vec4(displayColor,base.a);
        }
        """;
}
