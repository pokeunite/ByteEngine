using System.Numerics;

namespace ByteEngine.Core.Graphics.ThreeD;

internal sealed class Shader3D : IDisposable
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

    public void SetVector2(
        string name,
        Vector2 value) =>
        _shader.SetVector2(
            name,
            value);

    public void SetVector3(
        string name,
        Vector3 value) =>
        _shader.SetVector3(
            name,
            value);

    public void SetVector4(
        string name,
        Vector4 value) =>
        _shader.SetVector4(
            name,
            value);

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

    private const string FragmentSource =
        """
        #version 330 core

        #define MAX_DIRECTIONAL_LIGHTS 4
        #define MAX_POINT_LIGHTS 8

        in vec3 vNormal;
        in vec3 vWorldPosition;
        in vec2 vUV;

        out vec4 FragColor;

        uniform vec4 uBaseColor;
        uniform float uMetallic;
        uniform float uRoughness;
        uniform float uAlphaCutoff;
        uniform float uAmbientIntensity;
        uniform vec3 uCameraPosition;

        uniform int uUseEnvironmentIbl;
        uniform samplerCube uIrradianceMap;
        uniform samplerCube uPrefilterMap;
        uniform sampler2D uBrdfLut;
        uniform float uEnvironmentIntensity;
        uniform float uEnvironmentRotationRadians;
        uniform float uMaxReflectionLod;

        uniform int uFogEnabled;
        uniform int uFogMode;
        uniform vec3 uFogColor;
        uniform float uFogStart;
        uniform float uFogEnd;
        uniform float uFogDensity;
        uniform float uFogMaxOpacity;

        uniform sampler2D uTexture;
        uniform sampler2D uNormalTexture;
        uniform int uUseTexture;
        uniform int uUseNormalTexture;

        uniform int uDirectionalLightCount;
        uniform vec3 uDirectionalLightDirections[MAX_DIRECTIONAL_LIGHTS];
        uniform vec3 uDirectionalLightColors[MAX_DIRECTIONAL_LIGHTS];
        uniform float uDirectionalLightIntensities[MAX_DIRECTIONAL_LIGHTS];

        uniform int uPointLightCount;
        uniform vec3 uPointLightPositions[MAX_POINT_LIGHTS];
        uniform vec3 uPointLightColors[MAX_POINT_LIGHTS];
        uniform float uPointLightIntensities[MAX_POINT_LIGHTS];
        uniform float uPointLightRanges[MAX_POINT_LIGHTS];

        uniform sampler2D uShadowMap;
        uniform int uUseShadowMap;
        uniform int uReceiveShadows;
        uniform int uShadowLightIndex;
        uniform mat4 uLightViewProjection;
        uniform float uShadowBias;
        uniform float uShadowStrength;
        uniform float uShadowSoftness;
        uniform vec2 uShadowTexelSize;

        uniform samplerCube uPointShadowMap0;
        uniform samplerCube uPointShadowMap1;
        uniform int uPointShadowLightIndex0;
        uniform int uPointShadowLightIndex1;
        uniform float uPointShadowFarPlane0;
        uniform float uPointShadowFarPlane1;
        uniform float uPointShadowBias0;
        uniform float uPointShadowBias1;
        uniform float uPointShadowStrength0;
        uniform float uPointShadowStrength1;
        uniform float uPointShadowSoftness0;
        uniform float uPointShadowSoftness1;

        const float PI=3.14159265359;

        vec3 getNormal()
        {
            vec3 n=normalize(vNormal);

            if(uUseNormalTexture==0)
                return n;

            vec3 tangentNormal=
                texture(uNormalTexture,vUV).xyz*2.0-1.0;

            vec3 q1=dFdx(vWorldPosition);
            vec3 q2=dFdy(vWorldPosition);
            vec2 st1=dFdx(vUV);
            vec2 st2=dFdy(vUV);

            vec3 tangent=
                normalize(q1*st2.t-q2*st1.t);

            vec3 bitangent=
                normalize(-q1*st2.s+q2*st1.s);

            return
                normalize(
                    mat3(
                        tangent,
                        bitangent,
                        n)*
                    tangentNormal);
        }

        float distributionGGX(
            vec3 n,
            vec3 h,
            float roughness)
        {
            float a=roughness*roughness;
            float a2=a*a;
            float nDotH=max(dot(n,h),0.0);
            float denominator=
                nDotH*nDotH*(a2-1.0)+1.0;

            return
                a2/
                max(
                    PI*denominator*denominator,
                    0.0001);
        }

        float geometrySchlickGGX(
            float nDotV,
            float roughness)
        {
            float r=roughness+1.0;
            float k=(r*r)/8.0;

            return
                nDotV/
                max(
                    nDotV*(1.0-k)+k,
                    0.0001);
        }

        vec3 fresnelSchlick(
            float cosine,
            vec3 f0)
        {
            return
                f0+
                (1.0-f0)*
                pow(
                    clamp(
                        1.0-cosine,
                        0.0,
                        1.0),
                    5.0);
        }

        vec3 evaluatePbrLight(
            vec3 n,
            vec3 v,
            vec3 l,
            vec3 radiance,
            vec3 baseColor,
            float metallic,
            float roughness)
        {
            vec3 h=normalize(v+l);

            float nDotL=max(dot(n,l),0.0);
            float nDotV=max(dot(n,v),0.0);

            if(nDotL<=0.0)
                return vec3(0.0);

            vec3 f0=
                mix(
                    vec3(0.04),
                    baseColor,
                    metallic);

            vec3 f=
                fresnelSchlick(
                    max(dot(h,v),0.0),
                    f0);

            float d=
                distributionGGX(
                    n,
                    h,
                    roughness);

            float g=
                geometrySchlickGGX(
                    nDotV,
                    roughness)*
                geometrySchlickGGX(
                    nDotL,
                    roughness);

            vec3 specular=
                (d*g*f)/
                max(
                    4.0*nDotV*nDotL,
                    0.0001);

            vec3 diffuse=
                (1.0-f)*
                (1.0-metallic)*
                baseColor/
                PI;

            return
                (diffuse+specular)*
                radiance*
                nDotL;
        }

        float samplePointShadow(
            samplerCube shadowMap,
            vec3 fragmentToLight,
            float currentDepth,
            float farPlane,
            float bias,
            float softness,
            float strength)
        {
            float safeFarPlane=max(farPlane,0.0001);
            float radius=
                max(softness,0.0)*
                0.015*
                (0.25+currentDepth/safeFarPlane);

            float shadow=0.0;
            shadow+=currentDepth-bias>texture(shadowMap,fragmentToLight).r*safeFarPlane?1.0:0.0;
            shadow+=currentDepth-bias>texture(shadowMap,fragmentToLight+vec3(radius,0.0,0.0)).r*safeFarPlane?1.0:0.0;
            shadow+=currentDepth-bias>texture(shadowMap,fragmentToLight-vec3(radius,0.0,0.0)).r*safeFarPlane?1.0:0.0;
            shadow+=currentDepth-bias>texture(shadowMap,fragmentToLight+vec3(0.0,radius,0.0)).r*safeFarPlane?1.0:0.0;
            shadow+=currentDepth-bias>texture(shadowMap,fragmentToLight-vec3(0.0,radius,0.0)).r*safeFarPlane?1.0:0.0;
            shadow+=currentDepth-bias>texture(shadowMap,fragmentToLight+vec3(0.0,0.0,radius)).r*safeFarPlane?1.0:0.0;
            shadow+=currentDepth-bias>texture(shadowMap,fragmentToLight-vec3(0.0,0.0,radius)).r*safeFarPlane?1.0:0.0;

            return
                shadow/7.0*
                clamp(strength,0.0,1.0);
        }

        float calculatePointShadow(
            int lightIndex,
            vec3 lightPosition,
            float currentDepth)
        {
            if(uReceiveShadows==0)
                return 0.0;

            vec3 fragmentToLight=
                vWorldPosition-
                lightPosition;

            if(lightIndex==uPointShadowLightIndex0)
            {
                return samplePointShadow(
                    uPointShadowMap0,
                    fragmentToLight,
                    currentDepth,
                    uPointShadowFarPlane0,
                    uPointShadowBias0,
                    uPointShadowSoftness0,
                    uPointShadowStrength0);
            }

            if(lightIndex==uPointShadowLightIndex1)
            {
                return samplePointShadow(
                    uPointShadowMap1,
                    fragmentToLight,
                    currentDepth,
                    uPointShadowFarPlane1,
                    uPointShadowBias1,
                    uPointShadowSoftness1,
                    uPointShadowStrength1);
            }

            return 0.0;
        }

        float calculateDirectionalShadow(
            vec3 normal,
            vec3 surfaceToLight)
        {
            if(
                uUseShadowMap==0 ||
                uReceiveShadows==0)
            {
                return 0.0;
            }

            vec4 lightSpace=
                uLightViewProjection*
                vec4(
                    vWorldPosition,
                    1.0);

            if(abs(lightSpace.w)<0.00001)
                return 0.0;

            vec3 projected=
                lightSpace.xyz/
                lightSpace.w;

            projected=
                projected*0.5+
                0.5;

            if(
                projected.x<0.0 ||
                projected.x>1.0 ||
                projected.y<0.0 ||
                projected.y>1.0 ||
                projected.z<0.0 ||
                projected.z>1.0)
            {
                return 0.0;
            }

            float slope=
                1.0-
                max(
                    dot(
                        normal,
                        surfaceToLight),
                    0.0);

            float bias=
                max(
                    uShadowBias*slope,
                    uShadowBias*0.25);

            float shadow=
                0.0;

            for(
                int x=-1;
                x<=1;
                x++)
            {
                for(
                    int y=-1;
                    y<=1;
                    y++)
                {
                    vec2 offset=
                        vec2(
                            float(x),
                            float(y))*
                        uShadowTexelSize*
                        max(
                            uShadowSoftness,
                            0.0);

                    float storedDepth=
                        texture(
                            uShadowMap,
                            projected.xy+
                            offset).r;

                    shadow+=
                        projected.z-bias>
                        storedDepth
                            ? 1.0
                            : 0.0;
                }
            }

            return
                shadow/
                9.0;
        }

        vec3 rotateEnvironmentDirection(
            vec3 direction)
        {
            float cosine=
                cos(
                    uEnvironmentRotationRadians);

            float sine=
                sin(
                    uEnvironmentRotationRadians);

            vec3 d=
                normalize(
                    direction);

            return
                normalize(
                    vec3(
                        cosine*d.x-
                        sine*d.z,
                        d.y,
                        sine*d.x+
                        cosine*d.z));
        }

        vec3 fresnelSchlickRoughness(
            float cosine,
            vec3 f0,
            float roughness)
        {
            return
                f0+
                (
                    max(
                        vec3(
                            1.0-
                            roughness),
                        f0)-
                    f0
                )*
                pow(
                    clamp(
                        1.0-
                        cosine,
                        0.0,
                        1.0),
                    5.0);
        }

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
            vec4 base=
                uBaseColor*
                (
                    uUseTexture==1
                        ? texture(uTexture,vUV)
                        : vec4(1.0)
                );

            if(
                uAlphaCutoff>0.0 &&
                base.a<uAlphaCutoff)
            {
                discard;
            }

            vec3 n=getNormal();
            vec3 v=normalize(uCameraPosition-vWorldPosition);

            vec3 direct=
                vec3(0.0);

            for(
                int index=0;
                index<MAX_DIRECTIONAL_LIGHTS;
                index++)
            {
                if(index>=uDirectionalLightCount)
                    break;

                vec3 l=
                    normalize(
                        -uDirectionalLightDirections[index]);

                vec3 radiance=
                    uDirectionalLightColors[index]*
                    uDirectionalLightIntensities[index];

                float shadowMultiplier=
                    1.0;

                if(index==uShadowLightIndex)
                {
                    float shadow=
                        calculateDirectionalShadow(
                            n,
                            l);

                    shadowMultiplier=
                        1.0-
                        shadow*
                        uShadowStrength;
                }

                direct+=
                    evaluatePbrLight(
                        n,
                        v,
                        l,
                        radiance,
                        base.rgb,
                        uMetallic,
                        uRoughness)*
                    shadowMultiplier;
            }

            for(
                int index=0;
                index<MAX_POINT_LIGHTS;
                index++)
            {
                if(index>=uPointLightCount)
                    break;

                vec3 toLight=
                    uPointLightPositions[index]-
                    vWorldPosition;

                float distanceToLight=
                    length(toLight);

                float range=
                    max(
                        uPointLightRanges[index],
                        0.0001);

                if(distanceToLight>=range)
                    continue;

                vec3 l=
                    toLight/
                    max(
                        distanceToLight,
                        0.0001);

                float normalizedDistance=
                    distanceToLight/
                    range;

                float rangeFade=
                    clamp(
                        1.0-
                        normalizedDistance*
                        normalizedDistance,
                        0.0,
                        1.0);

                rangeFade*=
                    rangeFade;

                float attenuation=
                    rangeFade/
                    max(
                        1.0+
                        2.0*normalizedDistance+
                        normalizedDistance*
                        normalizedDistance,
                        0.0001);

                vec3 radiance=
                    uPointLightColors[index]*
                    uPointLightIntensities[index]*
                    attenuation;

                float pointShadow=
                    calculatePointShadow(
                        index,
                        uPointLightPositions[index],
                        distanceToLight);

                direct+=
                    evaluatePbrLight(
                        n,
                        v,
                        l,
                        radiance,
                        base.rgb,
                        uMetallic,
                        uRoughness)*
                    (1.0-pointShadow);
            }

            vec3 ambient=
                base.rgb*
                uAmbientIntensity;

            if(uUseEnvironmentIbl==1)
            {
                float nDotV=
                    max(
                        dot(
                            n,
                            v),
                        0.0);

                vec3 f0=
                    mix(
                        vec3(0.04),
                        base.rgb,
                        uMetallic);

                vec3 fresnel=
                    fresnelSchlickRoughness(
                        nDotV,
                        f0,
                        uRoughness);

                vec3 kS=
                    fresnel;

                vec3 kD=
                    (
                        vec3(1.0)-
                        kS
                    )*
                    (
                        1.0-
                        uMetallic
                    );

                vec3 irradiance=
                    texture(
                        uIrradianceMap,
                        rotateEnvironmentDirection(
                            n)).rgb;

                vec3 diffuseEnvironment=
                    irradiance*
                    base.rgb;

                vec3 reflectionDirection=
                    reflect(
                        -v,
                        n);

                vec3 prefilteredEnvironment=
                    textureLod(
                        uPrefilterMap,
                        rotateEnvironmentDirection(
                            reflectionDirection),
                        uRoughness*
                        uMaxReflectionLod).rgb;

                vec2 brdf=
                    texture(
                        uBrdfLut,
                        vec2(
                            nDotV,
                            uRoughness)).rg;

                /*
                 * Very rough reflections should converge toward low-frequency
                 * indirect lighting instead of exposing coarse/deep cubemap
                 * mip structure. Unreal uses the same 0.1 -> 0.3 roughness
                 * transition when mixing reflection captures with its
                 * low-frequency indirect-lighting source.
                 *
                 * ByteEngine does not have lightmaps yet, so use the already
                 * convolved sky irradiance as the low-frequency radiance
                 * source while preserving the split-sum BRDF weighting.
                 */
                float roughReflectionMix=
                    smoothstep(
                        0.10,
                        0.30,
                        uRoughness);

                vec3 specularRadiance=
                    mix(
                        prefilteredEnvironment,
                        irradiance,
                        roughReflectionMix);

                vec3 specularEnvironment=
                    specularRadiance*
                    (
                        fresnel*
                        brdf.x+
                        brdf.y
                    );

                ambient=
                    (
                        kD*
                        diffuseEnvironment+
                        specularEnvironment
                    )*
                    max(
                        uEnvironmentIntensity,
                        0.0);
            }

            vec3 linearColor=
                ambient+
                direct;

            if(uFogEnabled==1)
            {
                float fogDistance=
                    length(
                        uCameraPosition-
                        vWorldPosition);

                float fogFactor=
                    0.0;

                if(uFogMode==0)
                {
                    float fogRange=
                        max(
                            uFogEnd-
                            uFogStart,
                            0.001);

                    fogFactor=
                        clamp(
                            (
                                fogDistance-
                                uFogStart
                            )/
                            fogRange,
                            0.0,
                            1.0);
                }
                else
                {
                    fogFactor=
                        1.0-
                        exp(
                            -max(
                                uFogDensity,
                                0.0)*
                            fogDistance);
                }

                fogFactor=
                    min(
                        fogFactor,
                        clamp(
                            uFogMaxOpacity,
                            0.0,
                            1.0));

                linearColor=
                    mix(
                        linearColor,
                        max(
                            uFogColor,
                            vec3(0.0)),
                        fogFactor);
            }

            vec3 mappedColor=
                acesFilm(
                    max(
                        linearColor,
                        vec3(0.0)));

            vec3 displayColor=
                pow(
                    mappedColor,
                    vec3(1.0/2.2));

            /*
             * The final monitor/display path is normally 8-bit. Add a tiny
             * screen-space dither before that quantization so smooth HDR
             * gradients do not collapse into visible contour bands.
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
                    base.a);
        }
        """;
}
