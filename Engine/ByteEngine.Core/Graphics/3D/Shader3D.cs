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
        out vec3 vWorldPosition;
        out vec2 vUV;
        void main()
        {
            vNormal=mat3(transpose(inverse(uModel)))*aNormal;
            vWorldPosition=(uModel*vec4(aPosition,1.0)).xyz;
            vUV=aUV;
            gl_Position=uProjection*uView*uModel*vec4(aPosition,1.0);
        }
        """;

    private const string FragmentSource = """
        #version 330 core
        in vec3 vNormal;
        in vec3 vWorldPosition;
        in vec2 vUV;
        out vec4 FragColor;
        uniform vec4 uBaseColor;
        uniform vec3 uLightDirection;
        uniform vec3 uLightColor;
        uniform float uLightIntensity;
        uniform float uAmbientIntensity;
        uniform float uMetallic;
        uniform float uRoughness;
        uniform vec3 uCameraPosition;
        uniform sampler2D uTexture;
        uniform sampler2D uNormalTexture;
        uniform int uUseTexture;
        uniform int uUseNormalTexture;

        const float PI=3.14159265359;

        vec3 getNormal()
        {
            vec3 n=normalize(vNormal);
            if(uUseNormalTexture==0)return n;
            vec3 tangentNormal=texture(uNormalTexture,vUV).xyz*2.0-1.0;
            vec3 q1=dFdx(vWorldPosition);
            vec3 q2=dFdy(vWorldPosition);
            vec2 st1=dFdx(vUV);
            vec2 st2=dFdy(vUV);
            vec3 tangent=normalize(q1*st2.t-q2*st1.t);
            vec3 bitangent=normalize(-q1*st2.s+q2*st1.s);
            return normalize(mat3(tangent,bitangent,n)*tangentNormal);
        }

        float distributionGGX(vec3 n,vec3 h,float roughness)
        {
            float a=roughness*roughness;
            float a2=a*a;
            float nDotH=max(dot(n,h),0.0);
            float denominator=nDotH*nDotH*(a2-1.0)+1.0;
            return a2/max(PI*denominator*denominator,0.0001);
        }

        float geometrySchlickGGX(float nDotV,float roughness)
        {
            float r=roughness+1.0;
            float k=(r*r)/8.0;
            return nDotV/max(nDotV*(1.0-k)+k,0.0001);
        }

        vec3 fresnelSchlick(float cosine,vec3 f0)
        {
            return f0+(1.0-f0)*pow(clamp(1.0-cosine,0.0,1.0),5.0);
        }

        void main()
        {
            vec4 base=uBaseColor*(uUseTexture==1?texture(uTexture,vUV):vec4(1.0));
            vec3 n=getNormal();
            vec3 v=normalize(uCameraPosition-vWorldPosition);
            vec3 l=normalize(-uLightDirection);
            vec3 h=normalize(v+l);
            float nDotL=max(dot(n,l),0.0);
            float nDotV=max(dot(n,v),0.0);
            vec3 f0=mix(vec3(0.04),base.rgb,uMetallic);
            vec3 f=fresnelSchlick(max(dot(h,v),0.0),f0);
            float d=distributionGGX(n,h,uRoughness);
            float g=geometrySchlickGGX(nDotV,uRoughness)*geometrySchlickGGX(nDotL,uRoughness);
            vec3 specular=(d*g*f)/max(4.0*nDotV*nDotL,0.0001);
            vec3 diffuse=(1.0-f)*(1.0-uMetallic)*base.rgb/PI;
            vec3 direct=(diffuse+specular)*uLightColor*uLightIntensity*nDotL;
            vec3 linearColor=base.rgb*uAmbientIntensity+direct;
            vec3 displayColor=pow(clamp(linearColor,vec3(0.0),vec3(1.0)),vec3(1.0/2.2));
            FragColor=vec4(displayColor,base.a);
        }
        """;
}
