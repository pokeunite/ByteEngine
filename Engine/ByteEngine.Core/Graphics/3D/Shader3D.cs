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

    private static readonly string VertexSource =
        """
        #version 330 core
        /*BYTE_WATER_SHARED*/

        layout(location=0) in vec3 aPosition;
        layout(location=1) in vec3 aNormal;
        layout(location=2) in vec2 aUV;

        layout(location=5) in mat4 aInstanceModel;
        uniform int uInstanced;
        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;
        uniform mat4 uNormalMatrix;
        uniform vec4 uWaterStyle;
        uniform vec4 uWetContact0,uWetContact1,uWetContact2,uWetLevels;
        uniform int uWaterSurface;
        uniform int uWaterVolume;
        uniform vec4 uWaterColumn;
        uniform int uWaterDepthEnabled;
        uniform sampler2D uWaterDepthTexture;
        uniform vec4 uWaterDepthRect;
        uniform vec4 uWaterDepthSettings;
        uniform vec4 uWaterOceanWaves;
        uniform vec4 uWaterMotion;
        uniform vec4 uWaterWake;
        uniform vec4 uWaterFlow;
        uniform vec4 uWaterImpact;
        uniform vec2 uWaterSize;
        uniform int uUseHeightField;
        uniform sampler2D uHeightFieldTexture;
        uniform vec4 uHeightFieldUvTransform;
        uniform float uHeightFieldRange;
        uniform float uHeightFieldBias;
        uniform vec2 uHeightFieldWorldSize;
        float surfaceHeight(vec2 uv){vec2 rg=textureLod(uHeightFieldTexture,uv,0.0).rg;return dot(rg,vec2(65280.0,255.0))/65535.0*uHeightFieldRange+uHeightFieldBias;}


        out vec3 vNormal;
        out vec3 vWorldPosition;
        out vec2 vUV;

        void oceanWave(inout vec3 displacement,inout vec3 tx,inout vec3 tz,vec2 q,vec2 d,float amplitude,float wavelength,float phase){
            byteGravityWave(displacement,tx,tz,q,d,amplitude,wavelength,phase,uWaterMotion.x,uWaterFlow.z);
        }
        void main()
        {
            vec3 position=aPosition;vec3 normal=aNormal;
            if(uUseHeightField==1){
                vec2 uv=aUV*uHeightFieldUvTransform.xy+uHeightFieldUvTransform.zw;
                vec2 texel=1.0/vec2(textureSize(uHeightFieldTexture,0));
                position.y+=surfaceHeight(uv);
                vec2 gradient=vec2(surfaceHeight(uv+vec2(texel.x,0))-surfaceHeight(uv-vec2(texel.x,0)),surfaceHeight(uv+vec2(0,texel.y))-surfaceHeight(uv-vec2(0,texel.y)))/(2.0*texel*uHeightFieldWorldSize);
                normal=normalize(vec3(-gradient.x,1.0,-gradient.y));
            }
            mat4 model=uInstanced==1?aInstanceModel:uModel;
            if(uInstanced==1)
            {
                // Cofactors give inverse-transpose direction without a full mat4 inverse
                // or another instance stream. Every fragment consumer normalizes vNormal;
                // determinant magnitude cancels, but its sign must survive mirrored scale.
                mat3 basis=mat3(model);
                vec3 c0=cross(basis[1],basis[2]);
                vec3 c1=cross(basis[2],basis[0]);
                vec3 c2=cross(basis[0],basis[1]);
                float orientation=dot(basis[0],c0)<0.0?-1.0:1.0;
                vNormal=(mat3(c0,c1,c2)*normal)*orientation;
            }
            else vNormal=mat3(uNormalMatrix)*normal;
            vWorldPosition=(model*vec4(position,1.0)).xyz;
            if(uWaterSurface==1){
                float t=uWaterMotion.x;vec2 q=vWorldPosition.xz;
                if(uWaterStyle.w>.5){
                    float shoreAmplitude=1.;
                    if(uWaterDepthEnabled==1){vec2 fieldUv=(q-uWaterDepthRect.xy)/uWaterDepthRect.zw;if(all(greaterThanEqual(fieldUv,vec2(0)))&&all(lessThanEqual(fieldUv,vec2(1)))){vec2 dim=vec2(textureSize(uWaterDepthTexture,0));vec2 texUv=(fieldUv*(dim-1.)+.5)/dim;vec2 packed=textureLod(uWaterDepthTexture,texUv,0.).rg;float bed=mix(uWaterDepthSettings.x,uWaterDepthSettings.y,dot(packed,vec2(65280.,255.))/65535.);shoreAmplitude=smoothstep(0.,max(.01,uWaterDepthSettings.w),uWaterDepthSettings.z-bed);}}
                    vec2 d=length(uWaterOceanWaves.zw)>.001?normalize(uWaterOceanWaves.zw):vec2(1,0);vec3 disp=vec3(0),tx=vec3(1,0,0),tz=vec3(0,0,1);
                    oceanWave(disp,tx,tz,q,d,uWaterOceanWaves.x*shoreAmplitude,uWaterOceanWaves.y,0.);
                    oceanWave(disp,tx,tz,q,vec2(-d.y,d.x),uWaterOceanWaves.x*.52*shoreAmplitude,uWaterOceanWaves.y*.492,1.7);
                    oceanWave(disp,tx,tz,q,normalize(vec2(d.x*.8+d.y*.6,d.y*.8-d.x*.6)),uWaterOceanWaves.x*.21*shoreAmplitude,uWaterOceanWaves.y*.1984,3.1);
                    vWorldPosition+=disp;vNormal=normalize(cross(tz,tx));
                }
                float edge=smoothstep(0.0,0.07,min(min(aUV.x,1.0-aUV.x),min(aUV.y,1.0-aUV.y)));
                float h=(sin(dot(q,vec2(2.6,1.1))-t*1.6)*0.65+sin(dot(q,vec2(-1.4,3.6))+t*1.1)*0.35)*uWaterMotion.z*edge;
                if(abs(normal.y)>0.5)vWorldPosition.y+=h;else vWorldPosition.z+=h;
            }
            vUV=aUV;
            gl_Position=uProjection*uView*vec4(vWorldPosition,1.0);
        }
        """.Replace("/*BYTE_WATER_SHARED*/",PortableWaterShader.Source);

    private static readonly string FragmentSource =
        """
        #version 330 core
        /*BYTE_WATER_SHARED*/

        #define MAX_DIRECTIONAL_LIGHTS 4
        #define MAX_POINT_LIGHTS 8

        in vec3 vNormal;
        in vec3 vWorldPosition;
        in vec2 vUV;

        out vec4 FragColor;

        uniform vec4 uBaseColor;
        uniform float uMetallicFactor;
        uniform float uRoughnessFactor;
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
        uniform sampler2D uMetallicTexture;
        uniform sampler2D uRoughnessTexture;
        uniform sampler2D uAoTexture;
        uniform sampler2D uPackedPbrTexture;
        uniform sampler2D uEmissionTexture;
        uniform int uUseMetallicTexture;
        uniform int uUseRoughnessTexture;
        uniform int uUseAoTexture;
        uniform int uUsePackedPbrTexture;
        uniform int uUseEmissionTexture;
        uniform int uPackedAoChannel;
        uniform int uPackedRoughnessChannel;
        uniform int uPackedMetallicChannel;
        uniform float uAoStrength;
        uniform float uNormalStrength;
        uniform int uWaterRefraction;
        uniform sampler2D uWaterSceneColor;
        uniform sampler2D uWaterSceneDepth;
        uniform mat4 uWaterInverseViewProjection;
        uniform mat4 uWaterViewProjection;
        uniform vec4 uWaterAbsorption;
        uniform vec4 uWaterScattering;
        uniform vec4 uWaterStyle;
        uniform vec4 uWetContact0,uWetContact1,uWetContact2,uWetLevels;
        uniform int uWaterReflections;
        uniform int uWaterDetail;
        uniform vec4 uWaterViewport;
        uniform int uWaterSurface;
        uniform int uWaterVolume;
        uniform vec4 uWaterColumn;
        uniform vec4 uWaterMotion;
        uniform vec4 uWaterWake;
        uniform vec4 uWaterFlow;
        uniform vec4 uWaterImpact;
        uniform vec2 uWaterSize;
        uniform int uUseHeightField;
        uniform sampler2D uHeightFieldTexture;
        uniform vec4 uHeightFieldUvTransform;
        uniform int uSandSurface;
        uniform int uUseSandImprint;
        uniform sampler2D uSandImprintTexture;
        uniform vec2 uSandImprintOrigin;
        uniform float uNormalYSign;
        uniform int uUnlit;
        uniform int uDecodeColorSrgb;
        uniform int uEmissionEnabled;
        uniform vec3 uEmissionColor;
        uniform float uEmissionIntensity;
        uniform vec2 uUvTiling;
        uniform vec2 uUvOffset;

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

        /*
         * ByteEngine v1 material compatibility:
         *
         * Positive UV tiling keeps the original mesh-UV path.
         * Negative X+Y tiling is the persisted World Aligned marker. The
         * absolute X value is the world-units-per-texture-repeat scale.
         *
         * This keeps existing .bmat files backward compatible without a file
         * format bump. The Material editor owns this encoding so users never
         * have to enter negative values themselves.
         */
        bool useWorldAligned()
        {
            return
                uUvTiling.x<0.0 &&
                uUvTiling.y<0.0;
        }

        float worldTextureScale()
        {
            return
                max(
                    abs(uUvTiling.x),
                    0.001);
        }

        vec2 materialUv()
        {
            return
                vUV*uUvTiling+
                uUvOffset;
        }

        vec3 triplanarWeights(
            vec3 normal)
        {
            vec3 weights=
                pow(
                    abs(
                        normalize(
                            normal)),
                    vec3(4.0));

            float total=
                max(
                    weights.x+
                    weights.y+
                    weights.z,
                    0.00001);

            return
                weights/
                total;
        }

        void worldProjectionUvs(
            vec3 normal,
            out vec2 uvX,
            out vec2 uvY,
            out vec2 uvZ)
        {
            vec3 world=
                vWorldPosition/
                worldTextureScale();

            uvX=
                world.zy;

            uvY=
                world.xz;

            uvZ=
                world.xy;

            /*
             * Keep projections on opposite faces consistently oriented rather
             * than mirrored. This follows the same practical triplanar idea as
             * Unreal's WorldAlignedTexture / WorldAlignedNormal functions.
             */
            if(normal.x<0.0)
                uvX.x=-uvX.x;

            if(normal.y<0.0)
                uvY.x=-uvY.x;

            if(normal.z>=0.0)
                uvZ.x=-uvZ.x;

            uvX+=uUvOffset;
            uvY+=uUvOffset;
            uvZ+=uUvOffset;
        }

        vec4 sampleMaterialTexture(
            sampler2D source)
        {
            if(!useWorldAligned())
            {
                return
                    texture(
                        source,
                        materialUv());
            }

            vec3 normal=
                normalize(
                    vNormal);

            vec3 weights=
                triplanarWeights(
                    normal);

            vec2 uvX;
            vec2 uvY;
            vec2 uvZ;

            worldProjectionUvs(
                normal,
                uvX,
                uvY,
                uvZ);

            return
                texture(source,uvX)*weights.x+
                texture(source,uvY)*weights.y+
                texture(source,uvZ)*weights.z;
        }

        float mapChannel(vec4 sampleValue,int channel)
        {
            if(channel==1) return sampleValue.r;
            if(channel==2) return sampleValue.g;
            if(channel==3) return sampleValue.b;
            if(channel==4) return sampleValue.a;
            return 1.0;
        }

        vec3 decodeColor(vec3 value)
        {
            return uDecodeColorSrgb==1
                ? pow(max(value,vec3(0.0)),vec3(2.2))
                : value;
        }

        vec3 blendTriplanarNormal(
            vec3 mappedNormal,
            vec3 surfaceNormal)
        {
            vec3 result;

            result.xy=
                mappedNormal.xy+
                surfaceNormal.xy;

            result.z=
                mappedNormal.z*
                surfaceNormal.z;

            return result;
        }

        vec3 getWorldAlignedNormal(
            vec3 n)
        {
            vec3 weights=
                triplanarWeights(
                    n);

            vec2 uvX;
            vec2 uvY;
            vec2 uvZ;

            worldProjectionUvs(
                n,
                uvX,
                uvY,
                uvZ);

            vec3 tangentNormalX=
                texture(
                    uNormalTexture,
                    uvX).xyz*
                2.0-
                1.0;

            vec3 tangentNormalY=
                texture(
                    uNormalTexture,
                    uvY).xyz*
                2.0-
                1.0;

            vec3 tangentNormalZ=
                texture(
                    uNormalTexture,
                    uvZ).xyz*
                2.0-
                1.0;

            tangentNormalX.y*=uNormalYSign;
            tangentNormalY.y*=uNormalYSign;
            tangentNormalZ.y*=uNormalYSign;

            tangentNormalX.xy*=uNormalStrength;
            tangentNormalY.xy*=uNormalStrength;
            tangentNormalZ.xy*=uNormalStrength;

            /*
             * Match the UV face flips above so tangent-space normals do not
             * become mirrored on negative projections.
             */
            if(n.x<0.0)
                tangentNormalX.x=-tangentNormalX.x;

            if(n.y<0.0)
                tangentNormalY.x=-tangentNormalY.x;

            if(n.z>=0.0)
                tangentNormalZ.x=-tangentNormalZ.x;

            vec3 worldNormalX=
                blendTriplanarNormal(
                    tangentNormalX,
                    n.zyx).zyx;

            vec3 worldNormalY=
                blendTriplanarNormal(
                    tangentNormalY,
                    n.xzy).xzy;

            vec3 worldNormalZ=
                blendTriplanarNormal(
                    tangentNormalZ,
                    n);

            return
                normalize(
                    worldNormalX*weights.x+
                    worldNormalY*weights.y+
                    worldNormalZ*weights.z);
        }

        vec3 getNormal()
        {
            vec3 n=normalize(vNormal);

            if(uUseNormalTexture==0)
                return n;

            if(useWorldAligned())
            {
                return
                    getWorldAlignedNormal(
                        n);
            }

            vec3 tangentNormal=
                texture(uNormalTexture,materialUv()).xyz*2.0-1.0;
            tangentNormal.y*=uNormalYSign;
            tangentNormal.xy*=uNormalStrength;
            tangentNormal=normalize(tangentNormal);

            vec3 q1=dFdx(vWorldPosition);
            vec3 q2=dFdy(vWorldPosition);
            vec2 st1=dFdx(vUV);
            vec2 st2=dFdy(vUV);

            float determinant=st1.s*st2.t-st2.s*st1.t;
            if(abs(determinant)<1e-8) return n;
            vec3 tangent=(q1*st2.t-q2*st1.t)/determinant;
            vec3 originalBitangent=(-q1*st2.s+q2*st1.s)/determinant;
            tangent-=n*dot(n,tangent);
            if(dot(tangent,tangent)<1e-10) return n;
            tangent=normalize(tangent);
            float handedness=dot(cross(n,tangent),originalBitangent)<0.0?-1.0:1.0;
            vec3 bitangent=cross(n,tangent)*handedness;

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

        float waterNoise(vec2 p){return byteWaterNoise(p);}
        // Independent depth-weighted reconstruction: reject foreground neighbours instead
        // of allowing bilinear colour to drag the wall or cargo into the water.
        vec4 waterBackground(vec2 uv,out vec3 point){
            // Smooth open sea needs no costly neighbourhood reconstruction. Keep the
            // full foreground-aware filter at shores, rocks and every puzzle surface.
            if(uWaterStyle.w>.5||uWaterDetail==0){float centerDepth=texture(uWaterSceneDepth,uv).r;vec4 centerWorld=uWaterInverseViewProjection*vec4(uv*2.-1.,centerDepth*2.-1.,1.);point=centerWorld.xyz/centerWorld.w;if((vWorldPosition.y-point.y>1.5||uWaterDetail==0)&&centerDepth>=gl_FragCoord.z-.000002)return vec4(texture(uWaterSceneColor,uv).rgb,1.);}
            ivec2 size=textureSize(uWaterSceneDepth,0);vec2 pixel=clamp(uv*vec2(size)-.5,vec2(0),vec2(size)-1.001);ivec2 basePixel=ivec2(floor(pixel));vec2 f=fract(pixel);
            vec3 color=vec3(0),position=vec3(0);float sum=0.;
            float tolerance=max(.000002,max(abs(dFdx(gl_FragCoord.z)),abs(dFdy(gl_FragCoord.z)))*2.);
            for(int y=0;y<2;y++)for(int x=0;x<2;x++){
                ivec2 p=min(basePixel+ivec2(x,y),size-1);float d=texelFetch(uWaterSceneDepth,p,0).r;
                float w=(x==0?1.-f.x:f.x)*(y==0?1.-f.y:f.y);if(d+tolerance<gl_FragCoord.z)w=0.;
                vec2 tc=(vec2(p)+.5)/vec2(size);vec4 wp=uWaterInverseViewProjection*vec4(tc*2.-1.,d*2.-1.,1.);
                color+=texelFetch(uWaterSceneColor,p,0).rgb*w;position+=wp.xyz/wp.w*w;sum+=w;
            }
            point=sum>.00001?position/sum:vWorldPosition;return vec4(color/max(sum,.00001),sum);
        }
        void main()
        {
            vec4 baseTexture=uUseTexture==1
                ? sampleMaterialTexture(uTexture)
                : vec4(1.0);
            vec4 base=vec4(
                uBaseColor.rgb*decodeColor(baseTexture.rgb),
                uBaseColor.a*baseTexture.a);

            if(
                uAlphaCutoff>0.0 &&
                base.a<uAlphaCutoff)
            {
                discard;
            }

            if(uWaterVolume==1){
                float depth=max(0.0,uWaterColumn.x-vWorldPosition.y);
                float absorption=1.0-exp(-depth*max(0.01,uWaterColumn.z));
                base.rgb=mix(base.rgb*vec3(1.25,1.5,1.55),base.rgb*0.55,absorption);
                base.a=clamp(uWaterColumn.w+(1.0-uWaterColumn.w)*absorption*0.4,0.0,0.98);
            }
            vec4 packedPbrSample=vec4(1.0);
            if(uUsePackedPbrTexture==1)
                packedPbrSample=sampleMaterialTexture(uPackedPbrTexture);
            float uMetallic=clamp(uMetallicFactor*
                (uUsePackedPbrTexture==1
                    ? mapChannel(packedPbrSample,uPackedMetallicChannel)
                    : uUseMetallicTexture==1 ? sampleMaterialTexture(uMetallicTexture).r : 1.0),
                0.0,1.0);
            float uRoughness=clamp(uRoughnessFactor*
                (uUsePackedPbrTexture==1
                    ? mapChannel(packedPbrSample,uPackedRoughnessChannel)
                    : uUseRoughnessTexture==1 ? sampleMaterialTexture(uRoughnessTexture).r : 1.0),
                0.04,1.0);
            float ao=clamp(
                uUsePackedPbrTexture==1 && uPackedAoChannel!=0
                    ? mapChannel(packedPbrSample,uPackedAoChannel)
                    : uUseAoTexture==1 ? sampleMaterialTexture(uAoTexture).r : 1.0,
                0.0,1.0);
            ao=mix(1.0,ao,clamp(uAoStrength,0.0,1.0));
            vec3 emission=vec3(0.0);
            if(uEmissionEnabled==1)
            {
                vec3 emissionMap=uUseEmissionTexture==1
                    ? decodeColor(sampleMaterialTexture(uEmissionTexture).rgb)
                    : vec3(1.0);
                emission=uEmissionColor*emissionMap*
                    max(uEmissionIntensity,0.0);
            }
            if(uUnlit==1)
            {
                FragColor=vec4(max(base.rgb+emission,vec3(0.0)),base.a);
                return;
            }

            if(uWetLevels.w>0.0){
                vec2 q=vWorldPosition.xz;
                float a=step(uWetContact0.x,q.x)*step(q.x,uWetContact0.y)*step(uWetContact0.z,q.y)*step(q.y,uWetContact0.w);
                float b=step(uWetContact1.x,q.x)*step(q.x,uWetContact1.y)*step(uWetContact1.z,q.y)*step(q.y,uWetContact1.w);
                float c=step(uWetContact2.x,q.x)*step(q.x,uWetContact2.y)*step(uWetContact2.z,q.y)*step(q.y,uWetContact2.w);
                float height=max(max(mix(-10000.,uWetLevels.x,a),mix(-10000.,uWetLevels.y,b)),mix(-10000.,uWetLevels.z,c));
                float brokenLine=sin(q.x*13.+q.y*9.)*.015+sin(q.x*4.1-q.y*5.7)*.018;
                float wet=(1.-smoothstep(height-.09,height+.16+brokenLine,vWorldPosition.y))*uWetLevels.w;
                base.rgb*=1.-wet*.32;uRoughness=mix(uRoughness,max(.22,uRoughness*.58),wet);
            }
            vec3 n=getNormal();
            if(uSandSurface==1){
                vec4 sandImprint=vec4(0.5,0.5,0.0,0.0);
                if(uUseSandImprint==1){
                    vec2 imprintUv=(vWorldPosition.xz-uSandImprintOrigin+vec2(0.125))/8.25;
                    sandImprint=texture(uSandImprintTexture,imprintUv);
                }
                if(uUseHeightField==1){
                    vec4 field=texture(uHeightFieldTexture,vUV*uHeightFieldUvTransform.xy+uHeightFieldUvTransform.zw);
                    sandImprint.a=field.a;
                    base.rgb=mix(base.rgb,base.rgb*vec3(0.73,0.70,0.65),field.b*0.65);
                }
                // Pressure smooths ripples in the trough and displaced shoulders.
                // World-space wind ripples. Derivative filtering avoids distant shimmer.
                vec2 wind=normalize(vec2(0.86,0.51));
                float phase=dot(vWorldPosition.xz,wind)*22.0+sin(vWorldPosition.z*0.73)*1.8+sin(vWorldPosition.x*0.31+vWorldPosition.z*0.17)*2.3;
                float fade=1.0-smoothstep(0.45,2.4,fwidth(phase));
                float slope=cos(phase)*0.12*fade*(1.0-sandImprint.a)*pow(abs(normalize(vNormal).y),4.0);
                n=normalize(n-vec3(wind.x*slope,0.0,wind.y*slope));
                float macro=sin(vWorldPosition.x*0.071+sin(vWorldPosition.z*0.043))*sin(vWorldPosition.z*0.059);
                base.rgb*=1.0+macro*0.055;
                uRoughness=max(uRoughness,0.82);
                if(uUseSandImprint==1){
                    vec2 imprintUv=(vWorldPosition.xz-uSandImprintOrigin+vec2(0.125))/8.25;
                    vec4 imprint=sandImprint;
                    n=normalize(n+vec3((imprint.r-0.5)*2.0,0.0,(imprint.g-0.5)*2.0));
                    base.rgb*=1.0-sqrt(imprint.b)*0.34;
                }
            }
            if(uWaterSurface==1){
                vec2 q=uWaterFlow.w>.5?vUV*uWaterSize:vWorldPosition.xz;
                vec2 dir=uWaterFlow.w>.5?vec2(0,1):uWaterFlow.xy;
                float time=uWaterMotion.x;vec2 adv=q-dir*time*uWaterFlow.z;
                float p=dot(adv,vec2(3.1,1.7))+sin(q.y*1.4-time*.55)*.7;
                float r=dot(adv,vec2(-2.3,4.7))+time*.8;
                vec2 slope=vec2(cos(p)*.14-cos(r)*.095,cos(p)*.075+cos(r)*.17);
                slope+=vec2(waterNoise(adv*7.0),waterNoise(adv*7.0+31.0))*.055-.0275;
                vec3 ripple=vec3(-slope.x,0,-slope.y);
                if(uWaterFlow.w>.5){
                    // Path UVs are cross-channel / downstream. Perturb in that local
                    // surface frame, not world X/Z (which reverses or skews inclined runs).
                    vec2 direction=uWaterFlow.xy;
                    vec3 along=normalize(vec3(direction.x,-dot(n.xz,direction)/max(.05,abs(n.y)),direction.y));
                    vec3 across=normalize(cross(n,along));
                    ripple=-across*slope.x-along*slope.y;
                }
                n=normalize(n+ripple*uWaterStyle.x);uRoughness=clamp(uWaterStyle.y,.045,1.);uMetallic=0.0;
            }
            vec3 v=normalize(uCameraPosition-vWorldPosition);

            vec3 linearColor=vec3(0.0);
            // Refractive water supplies its own optical lighting below. Do not evaluate
            // opaque BRDF, shadow PCF and diffuse IBL only to overwrite their result.
            if(uWaterSurface==0 || uWaterRefraction==0){
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

            linearColor=ambient*ao+direct+emission;
            }

            if(uWaterSurface==1){
                // Depth absorption and Schlick Fresnel follow tuxalin/water-shader (MIT); see ThirdParty notices.
                float time=uWaterMotion.x;bool channel=uWaterFlow.w>.5;
                vec2 q=channel?vUV*uWaterSize:vWorldPosition.xz;
                vec2 adv=q-(channel?vec2(0,1):uWaterFlow.xy)*time*uWaterFlow.z;
                float noise=waterNoise(adv*3.2+waterNoise(adv*.8+time*.12));
                float fine=waterNoise(adv*10.0+vec2(time*.15,-time*.11));
                float depth=max(.04,uWaterColumn.x-uWaterColumn.y);vec3 refracted=vec3(0.);vec3 bedWorld=vWorldPosition-vec3(0,depth,0);bool useRefraction=uWaterRefraction==1;
                vec2 screen=(gl_FragCoord.xy-uWaterViewport.xy)/max(uWaterViewport.zw,vec2(1.));
                if(useRefraction){
                    // The undistorted coordinate is already pixel-centred: no neighbourhood filter is needed here.
                    float straightZ=texture(uWaterSceneDepth,screen).r;vec4 straightWorld=uWaterInverseViewProjection*vec4(screen*2.-1.,straightZ*2.-1.,1.);vec3 straightPoint=straightWorld.xyz/straightWorld.w;vec4 straight=vec4(texture(uWaterSceneColor,screen).rgb,1.);
                    float straightDepth=clamp(vWorldPosition.y-straightPoint.y,0.,24.);
                    vec2 offset=n.xz*.011*min(straightDepth,1.5)*smoothstep(0.,.15,straightDepth);
                    vec3 distortedPoint;vec4 distorted=waterBackground(clamp(screen+offset,vec2(.001),vec2(.999)),distortedPoint);
                    bool valid=distorted.a>.0001;refracted=valid?distorted.rgb:straight.rgb;bedWorld=valid?distortedPoint:straightPoint;
                    depth=clamp(vWorldPosition.y-bedWorld.y,0.,24.);
                }
                // Coefficients are reciprocal metres. Both scattering and absorption
                // remove light from the ray; their ratio controls the scattered fraction.
                vec3 sigmaA=max(uWaterAbsorption.rgb,vec3(0)),sigmaS=max(uWaterScattering.rgb,vec3(0));
                vec3 sigmaT=max(sigmaA+sigmaS,vec3(.000001));
                vec3 intoWater=refract(-v,n,1./1.333);float pathLength=min(30.,depth/max(.2,abs(intoWater.y)));
                vec3 transmittance=byteWaterTransmittance(sigmaT,pathLength);
                vec3 sunDirection=uDirectionalLightCount>0?normalize(-uDirectionalLightDirections[0]):vec3(0,1,0);
                float g=clamp(uWaterScattering.w,-.8,.8),mu=dot(-intoWater,sunDirection);
                float phase=byteWaterPhase(g,mu);
                vec3 incident=vec3(.055,.075,.095);
                if(uDirectionalLightCount>0)incident+=uDirectionalLightColors[0]*uDirectionalLightIntensities[0]*phase;
                vec3 scattered=incident*clamp(sigmaS/sigmaT*(1.-transmittance),vec3(0),vec3(1));
                vec3 bottomLight=exp(-sigmaT*depth/max(.35,sunDirection.y));
                vec3 under=(useRefraction?refracted:linearColor*.18)*transmittance*bottomLight+scattered;
                float absorption=1.-exp(-depth*.8);
                // Caustics modulate the lit bottom; they are not white lines painted on the surface.
                vec2 bed=bedWorld.xz+vec2(sin(time*.43+bedWorld.z)*.12,cos(time*.37+bedWorld.x)*.12);
                float c1=sin(bed.x*5.1+sin(bed.y*3.7+time*.65));float c2=sin(bed.y*5.8+sin(bed.x*4.1-time*.53));
                float caustic=pow(max(0.,1.-abs(c1+c2)*1.7),9.)*(.65+.35*waterNoise(bed*.63));
                under+=refracted*transmittance*bottomLight*caustic*max(0.,uWaterAbsorption.w)*.5*exp(-depth*.6)*smoothstep(0.,.08,depth);
                float fresnel=.02037+.97963*pow(1.0-clamp(dot(n,v),0.0,1.0),5.0);
                vec3 reflectionRay=reflect(-v,n);float skyBand=smoothstep(-.15,.85,reflectionRay.y);
                vec3 reflected=mix(vec3(.035,.09,.12),vec3(.42,.58,.63),skyBand);
                if(uUseEnvironmentIbl==1)reflected=textureLod(uPrefilterMap,rotateEnvironmentDirection(reflectionRay),clamp(uWaterStyle.y*8.,.3,4.)).rgb*uEnvironmentIntensity;
                // Bounded screen-space ray marching; off-screen rays retain the environment fallback.
                if(useRefraction&&uWaterReflections==1&&uWaterDetail>0&&reflectionRay.y>0.){
                    vec3 origin=vWorldPosition+n*.06;float previousGap=-1.;
                    for(int step=0;step<20;step++){
                        if(uWaterDetail==1&&step>=10)break;
                        float distanceAlong=.10+float(step)*.26+float(step*step)*.018;
                        vec3 ray=origin+reflectionRay*distanceAlong;vec4 clip=uWaterViewProjection*vec4(ray,1.);
                        if(clip.w<=0.)break;vec2 uv=clip.xy/clip.w*.5+.5;
                        if(any(lessThan(uv,vec2(.002)))||any(greaterThan(uv,vec2(.998))))break;
                        float sampledDepth=texture(uWaterSceneDepth,uv).r;
                        vec4 hit=uWaterInverseViewProjection*vec4(uv*2.-1.,sampledDepth*2.-1.,1.);hit/=hit.w;
                        float gap=length(uCameraPosition-ray)-length(uCameraPosition-hit.xyz);
                        if(sampledDepth<.99999&&gap>0.&&gap<.38&&previousGap<=0.){
                            float edgeFade=smoothstep(0.,.09,min(min(uv.x,1.-uv.x),min(uv.y,1.-uv.y)));
                            reflected=mix(reflected,texture(uWaterSceneColor,uv).rgb,edgeFade*.8);break;
                        }
                        previousGap=gap;
                    }
                }
                linearColor=mix(under,reflected,fresnel);

                if(uDirectionalLightCount>0){float glitter=pow(max(0.,dot(reflectionRay,normalize(-uDirectionalLightDirections[0]))),mix(540.,60.,uWaterStyle.y));linearColor+=uDirectionalLightColors[0]*glitter*1.2;}
                vec2 edgeDistance=min(vUV,vec2(1.)-vUV)*uWaterSize;
                float bank=1.-smoothstep(.015,.11,channel?edgeDistance.x:min(edgeDistance.x,edgeDistance.y));
                vec2 wp=vWorldPosition.xz-uWaterWake.xy;
                float ring=(1.-smoothstep(.04,.22,abs(length(wp/vec2(max(.1,uWaterWake.z*.54),max(.1,uWaterWake.z*.3)))-1.)))*uWaterWake.w;
                vec2 ip=vWorldPosition.xz-uWaterImpact.xy;
                float impact=exp(-dot(ip,ip)/max(.01,uWaterImpact.z*uWaterImpact.z))*uWaterImpact.w;
                float streak=channel?smoothstep(.42,.73,waterNoise(vec2(q.x*8.,q.y*2.1-time*uWaterFlow.z*2.1)))*clamp(abs(uWaterMotion.y)*.18,0.,.65):0.;
                float wash=.5+.5*sin(time*1.4+waterNoise(q*.7)*9.);float shoreline=(1.-smoothstep(.01,max(.04,uWaterStyle.z)*(.5+wash*.5),depth))*smoothstep(.48,.83,fine)*wash*uWaterStyle.w;
                float foam=clamp((shoreline+(useRefraction?(1.-smoothstep(.025,.17,depth))*.2*bank:0.)+bank*.10*clamp(abs(uWaterMotion.y)*.3,0.,1.)+ring*.7+impact*(.45+noise)+streak)*(fine*.7+.3)*uWaterMotion.w,0.,.85);
                linearColor=mix(linearColor,vec3(.72,.84,.81),foam);
                base.a=useRefraction?1.:clamp(.48+absorption*.35+fresnel*.2+foam*.4,.45,.97);
            }
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

            /*
             * Keep the material pass in linear HDR. Exposure, tone mapping,
             * gamma conversion and final display dithering now happen once in
             * PostProcess3D after the full scene has been composed.
             */
            FragColor=
                vec4(
                    max(
                        linearColor,
                        vec3(0.0)),
                    base.a);
        }
        """.Replace("/*BYTE_WATER_SHARED*/",PortableWaterShader.Source);
}
