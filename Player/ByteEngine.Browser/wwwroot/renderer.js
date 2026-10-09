export class BrowserRenderer {
    constructor(canvas, overlay) {
        const gl=this.gl=canvas.getContext('webgl2',{antialias:true,alpha:false});
        if(!gl) throw new Error('WebGL 2 is unavailable on this browser/device.');
        this.gpuTimer=gl.getExtension('EXT_disjoint_timer_query_webgl2');this.gpuQueries=[];this.profile={gpuMs:null,cpu3DMs:0,uiCpuMs:0,drawCalls:0};
        this.ui=overlay.getContext('2d');this.meshes=new Map();this.textures=new Map();
        this.tints=new Map();this.tintBytes=0;
        const compile=(type,source)=>{const shader=gl.createShader(type);gl.shaderSource(shader,source);
            gl.compileShader(shader);if(!gl.getShaderParameter(shader,gl.COMPILE_STATUS))throw new Error(gl.getShaderInfoLog(shader));return shader;};
        const program=this.program=gl.createProgram();
        gl.attachShader(program,compile(gl.VERTEX_SHADER,`#version 300 es
        layout(location=0) in vec3 position;layout(location=1) in vec3 normal;layout(location=2) in vec2 uv;
        layout(location=5) in mat4 instanceModel;uniform bool instanced;uniform mat4 vp,model;out vec3 n,world;out vec2 texcoord;
        uniform sampler2D heightField;uniform bool hasHeight;uniform vec4 heightUv;uniform float heightRange,heightBias;uniform vec2 heightSize;
        float surfaceHeight(vec2 coord){vec2 rg=textureLod(heightField,clamp(coord,vec2(0.0),vec2(1.0)),0.0).rg;return dot(rg,vec2(65280.0,255.0))/65535.0*heightRange+heightBias;}
        void main(){vec3 pos=position,N=normal;if(hasHeight){vec2 coord=uv*heightUv.xy+heightUv.zw;vec2 step=1.0/vec2(textureSize(heightField,0));pos.y+=surfaceHeight(coord);vec2 gradient=vec2(surfaceHeight(coord+vec2(step.x,0.0))-surfaceHeight(coord-vec2(step.x,0.0)),surfaceHeight(coord+vec2(0.0,step.y))-surfaceHeight(coord-vec2(0.0,step.y)))/(2.0*step*heightSize);N=normalize(vec3(-gradient.x,1.0,-gradient.y));}mat4 M=instanced?instanceModel:model;vec4 p=M*vec4(pos,1.0);world=p.xyz;n=mat3(transpose(inverse(M)))*N;
        texcoord=uv;gl_Position=vp*p;gl_Position.z=2.0*gl_Position.z-gl_Position.w;}`));
        gl.attachShader(program,compile(gl.FRAGMENT_SHADER,`#version 300 es
        precision highp float;
        in vec3 n,world;in vec2 texcoord;out vec4 fragment;
        uniform vec4 color;uniform vec3 eye,emission;uniform vec2 tiling,offset;
        uniform sampler2D tex0,tex1,tex2,tex3,tex4,tex5,tex6;
        uniform bool has0,has1,has2,has3,has4,has5,has6,unlit,srgb,directX,packed;
        uniform float metallic,roughness,ao,normalStrength,cutoff,ambient,exposure;
        uniform ivec3 channels;uniform int lightCount,pointCount;
        uniform vec3 lightDirection[4],lightColor[4],pointPosition[8],pointColor[8];uniform float pointRange[8];
        const float PI=3.14159265359;
        float channel(vec4 v,int i){return i<0?1.0:v[clamp(i,0,3)];}
        vec3 illuminate(vec3 base,vec3 N,vec3 V,vec3 L,vec3 radiance,float metal,float rough){
            vec3 H=normalize(V+L);float nv=max(dot(N,V),0.001),nl=max(dot(N,L),0.0),nh=max(dot(N,H),0.0);
            float a=rough*rough,a2=a*a,d=nh*nh*(a2-1.0)+1.0,D=a2/max(PI*d*d,0.001);
            float k=(rough+1.0)*(rough+1.0)/8.0,G=nv/(nv*(1.0-k)+k)*nl/(nl*(1.0-k)+k);
            vec3 F=mix(vec3(.04),base,metal);F=F+(1.0-F)*pow(1.0-max(dot(H,V),0.0),5.0);
            return ((1.0-F)*(1.0-metal)*base/PI+D*G*F/max(4.0*nv*nl,.001))*radiance*nl;
        }
        void main(){
            vec2 uv=texcoord*tiling+offset;vec4 sampleColor=has0?texture(tex0,uv):vec4(1.0);if(srgb&&has0)sampleColor.rgb=pow(max(sampleColor.rgb,vec3(0.0)),vec3(2.2));vec4 base=color*sampleColor;
            if(cutoff>=0.0&&base.a<cutoff)discard;
            vec3 N=normalize(n);if(has1){
                vec3 map=texture(tex1,uv).xyz*2.0-1.0;if(directX)map.y=-map.y;map.xy*=normalStrength;
                vec3 dp1=dFdx(world),dp2=dFdy(world);vec2 du1=dFdx(uv),du2=dFdy(uv);
                vec3 T=dp1*du2.y-dp2*du1.y,B=-dp1*du2.x+dp2*du1.x;
                float det=du1.x*du2.y-du1.y*du2.x;
                if(abs(det)>0.000001){T=normalize(T*sign(det));B=normalize(B*sign(det));N=normalize(mat3(T,B,N)*map);}
            }
            if(!gl_FrontFacing)N=-N;
            float metal=metallic*(has2?texture(tex2,uv).r:1.0),rough=roughness*(has3?texture(tex3,uv).r:1.0);
            float occlusion=has4?texture(tex4,uv).r:1.0;
            if(packed&&has5){vec4 p=texture(tex5,uv);occlusion=channel(p,channels.x);rough=roughness*channel(p,channels.y);metal=metallic*channel(p,channels.z);}
            rough=clamp(rough,.08,1.0);metal=clamp(metal,0.0,1.0);
            vec3 outColor=base.rgb;if(!unlit){
                outColor=base.rgb*ambient*mix(1.0,occlusion,clamp(ao,0.0,1.0));vec3 V=normalize(eye-world);
                for(int i=0;i<4;i++){if(i>=lightCount)break;outColor+=illuminate(base.rgb,N,V,normalize(-lightDirection[i]),lightColor[i],metal,rough);}
                for(int i=0;i<8;i++){if(i>=pointCount)break;vec3 diff=pointPosition[i]-world;float distance=length(diff),range=max(pointRange[i],.001);
                    float attenuation=pow(clamp(1.0-distance/range,0.0,1.0),2.0);
                    outColor+=illuminate(base.rgb,N,V,normalize(diff),pointColor[i]*attenuation,metal,rough);}
            }
            vec3 emissionSample=has6?texture(tex6,uv).rgb:vec3(1.0);if(srgb&&has6)emissionSample=pow(max(emissionSample,vec3(0.0)),vec3(2.2));outColor+=emission*emissionSample;
            vec3 x=max(outColor,vec3(0.0))*max(exposure,0.0);x=clamp((x*(2.51*x+.03))/(x*(2.43*x+.59)+.14),0.0,1.0);fragment=vec4(pow(x,vec3(1.0/2.2)),base.a);
        }`));
        gl.linkProgram(program);if(!gl.getProgramParameter(program,gl.LINK_STATUS))throw new Error(gl.getProgramInfoLog(program));
        const sky=this.skyProgram=gl.createProgram();this.skyVao=gl.createVertexArray();
        gl.attachShader(sky,compile(gl.VERTEX_SHADER,`#version 300 es
        out vec2 screen;void main(){vec2 p=vec2((gl_VertexID<<1)&2,gl_VertexID&2);screen=p*2.0-1.0;gl_Position=vec4(screen,1.0,1.0);}`));
        gl.attachShader(sky,compile(gl.FRAGMENT_SHADER,`#version 300 es
        precision highp float;in vec2 screen;out vec4 fragment;
        uniform mat4 inverseVp;uniform vec3 eye,zenith,horizon,ground;uniform sampler2D environment;
        uniform bool hasMap,hdr;uniform float rotation,intensity,exposure,sharpness;
        void main(){vec4 p=inverseVp*vec4(screen,1.0,1.0);vec3 d=normalize(p.xyz/p.w-eye);vec3 col;
         if(hasMap){d=vec3(cos(rotation)*d.x-sin(rotation)*d.z,d.y,sin(rotation)*d.x+cos(rotation)*d.z);vec2 uv=vec2(atan(d.z,d.x)/6.2831853+.5,clamp(.5-asin(clamp(d.y,-1.0,1.0))/3.14159265,.001,.999));col=texture(environment,uv).rgb;if(!hdr)col=pow(max(col,vec3(0.0)),vec3(2.2));}
         else col=mix(horizon,d.y>=0.0?zenith:ground,pow(abs(d.y),max(.01,sharpness)));
         vec3 x=max(col*intensity*exposure,vec3(0.0));x=clamp((x*(2.51*x+.03))/(x*(2.43*x+.59)+.14),0.0,1.0);fragment=vec4(pow(x,vec3(1.0/2.2)),1.0);
        }`));
        gl.linkProgram(sky);if(!gl.getProgramParameter(sky,gl.LINK_STATUS))throw new Error(gl.getProgramInfoLog(sky));
        this.skyLocations=Object.fromEntries(['inverseVp','eye','zenith','horizon','ground','environment','hasMap','hdr','rotation','intensity','exposure','sharpness'].map(n=>[n,gl.getUniformLocation(sky,n)]));
        this.locations=new Map();
    }
    deleteTint(key){const c=this.tints.get(key);if(c){this.tintBytes-=c.width*c.height*4;this.tints.delete(key);}}
    location(name){if(!this.locations.has(name))this.locations.set(name,this.gl.getUniformLocation(this.program,name));return this.locations.get(name);}
    bytes(value){return typeof value==='number'?this.readBuffer(value):Uint8Array.from(atob(value),c=>c.charCodeAt(0));}
    draw(data,width,height){
        const gl=this.gl,uploadStart=performance.now();let meshBytes=0,textureBytes=0;
        for(const id of data.deadMeshes){const m=this.meshes.get(id);if(m){gl.deleteVertexArray(m.vao);gl.deleteBuffer(m.vbo);gl.deleteBuffer(m.ibo);this.meshes.delete(id);}}
        for(const id of data.deadTextures){const t=this.textures.get(id);if(t){gl.deleteTexture(t.gpu);this.textures.delete(id);for(const key of this.tints.keys())if(key.startsWith(id+':'))this.deleteTint(key);}}
        for(const upload of data.uploads){
            let m=this.meshes.get(upload.id);
            if(!m){m={vao:gl.createVertexArray(),vbo:gl.createBuffer(),ibo:gl.createBuffer()};this.meshes.set(upload.id,m);}
            const vertexBytes=this.bytes(upload.vertices),indexBytes=this.bytes(upload.indices);meshBytes+=vertexBytes.byteLength+indexBytes.byteLength;
            gl.bindVertexArray(m.vao);gl.bindBuffer(gl.ARRAY_BUFFER,m.vbo);
            gl.bufferData(gl.ARRAY_BUFFER,new Float32Array(vertexBytes.buffer,vertexBytes.byteOffset,vertexBytes.byteLength/4),gl.DYNAMIC_DRAW);
            gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,m.ibo);gl.bufferData(gl.ELEMENT_ARRAY_BUFFER,new Uint32Array(indexBytes.buffer,indexBytes.byteOffset,indexBytes.byteLength/4),gl.STATIC_DRAW);
            for(const [index,count,offset] of [[0,3,0],[1,3,12],[2,2,24]]){
                gl.enableVertexAttribArray(index);gl.vertexAttribPointer(index,count,gl.FLOAT,false,32,offset);
            }m.count=indexBytes.byteLength/4;
        }
        for(const upload of data.textures){
            for(const key of this.tints.keys())if(key.startsWith(upload.id+':'))this.deleteTint(key);
            let t=this.textures.get(upload.id);
            if(!t){t={gpu:gl.createTexture(),image:document.createElement('canvas')};this.textures.set(upload.id,t);}
            const raw=this.bytes(upload.pixels),bytes=upload.hdr?new Float32Array(raw.buffer,raw.byteOffset,raw.byteLength/4):raw;const format=upload.hdr?gl.RGBA16F:gl.RGBA8,type=upload.hdr?gl.FLOAT:gl.UNSIGNED_BYTE;
            textureBytes+=raw.byteLength;
            gl.bindTexture(gl.TEXTURE_2D,t.gpu);gl.pixelStorei(gl.UNPACK_ALIGNMENT,1);
            if(upload.allocate)gl.texImage2D(gl.TEXTURE_2D,0,format,upload.width,upload.height,0,gl.RGBA,type,null);if(upload.partial)gl.texSubImage2D(gl.TEXTURE_2D,0,upload.x,upload.y,upload.w,upload.h,gl.RGBA,type,bytes);else gl.texImage2D(gl.TEXTURE_2D,0,format,upload.width,upload.height,0,gl.RGBA,type,bytes);
            gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,upload.nearest?gl.NEAREST:gl.LINEAR);
            gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.LINEAR);
            gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.REPEAT);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.REPEAT);
            if(!upload.partial||upload.allocate){t.image.width=upload.width;t.image.height=upload.height;}t.nearest=upload.nearest;
            if(!upload.hdr)t.image.getContext('2d').putImageData(new ImageData(new Uint8ClampedArray(bytes.buffer),upload.w,upload.h),upload.x,upload.y);
        }
        Object.assign(this.profile,{uploadMs:performance.now()-uploadStart,meshUploadBytes:meshBytes,textureUploadBytes:textureBytes,meshUploads:data.uploads.length,textureUploads:data.textures.length,residentMeshes:this.meshes.size,residentTextures:this.textures.size,tintBytes:this.tintBytes});
        gl.viewport(0,0,width,height);gl.depthMask(true);gl.clearColor(...data.background,1);
        gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT);
        if(data.sky){const sky=data.sky,u=this.skyLocations;gl.useProgram(this.skyProgram);gl.bindVertexArray(this.skyVao);gl.disable(gl.DEPTH_TEST);gl.disable(gl.CULL_FACE);gl.disable(gl.BLEND);gl.depthMask(false);gl.uniformMatrix4fv(u.inverseVp,false,sky.inverseVp);gl.uniform3fv(u.eye,data.eye);
         for(const name of ['zenith','horizon','ground'])gl.uniform3fv(u[name],sky[name]);for(const name of ['rotation','intensity','sharpness'])gl.uniform1f(u[name],sky[name]);gl.uniform1f(u.exposure,data.exposure??1);gl.uniform1i(u.hdr,sky.hdr?1:0);gl.uniform1i(u.hasMap,sky.texture&&this.textures.has(sky.texture)?1:0);gl.activeTexture(gl.TEXTURE0);gl.bindTexture(gl.TEXTURE_2D,this.textures.get(sky.texture)?.gpu||null);gl.uniform1i(u.environment,0);gl.drawArrays(gl.TRIANGLES,0,3);}
        gl.useProgram(this.program);gl.uniform1f(this.location('exposure'),data.exposure??1);
        gl.uniformMatrix4fv(this.location('vp'),false,data.vp);gl.uniform3fv(this.location('eye'),data.eye);
        gl.uniform1f(this.location('ambient'),data.ambient);
        gl.uniform1i(this.location('lightCount'),data.lights.length);gl.uniform1i(this.location('pointCount'),data.points.length);
        if(data.lights.length){gl.uniform3fv(this.location('lightDirection[0]'),data.lights.flatMap(l=>l.direction));
            gl.uniform3fv(this.location('lightColor[0]'),data.lights.flatMap(l=>l.color));}
        if(data.points.length){gl.uniform3fv(this.location('pointPosition[0]'),data.points.flatMap(l=>l.position));
            gl.uniform3fv(this.location('pointColor[0]'),data.points.flatMap(l=>l.color));gl.uniform1fv(this.location('pointRange[0]'),data.points.map(l=>l.range));}
        let previousMaterial=null;
        const geometryStart=performance.now();let gpuQuery=null;
        if(this.gpuTimer){
            for(let i=this.gpuQueries.length-1;i>=0;i--){const query=this.gpuQueries[i];if(!gl.getQueryParameter(query,gl.QUERY_RESULT_AVAILABLE))continue;
                this.profile.gpuMs=gl.getParameter(this.gpuTimer.GPU_DISJOINT_EXT)?null:gl.getQueryParameter(query,gl.QUERY_RESULT)/1e6;gl.deleteQuery(query);this.gpuQueries.splice(i,1);}
            if(this.gpuQueries.length<3){gpuQuery=gl.createQuery();gl.beginQuery(this.gpuTimer.TIME_ELAPSED_EXT,gpuQuery);}
        }
        for(const draw of data.draws){
            const m=this.meshes.get(draw.id);gl.bindVertexArray(m.vao);
            const instanceCount=(draw.instances?.length??0)/16;
            gl.uniform1i(this.location('instanced'),instanceCount>1?1:0);
            if(instanceCount>1){
                this.instanceBuffer??=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,this.instanceBuffer);
                gl.bufferData(gl.ARRAY_BUFFER,new Float32Array(draw.instances),gl.STREAM_DRAW);
                for(let column=0;column<4;column++){const location=5+column;gl.enableVertexAttribArray(location);gl.vertexAttribPointer(location,4,gl.FLOAT,false,64,column*16);gl.vertexAttribDivisor(location,1);}
            }
            gl.uniformMatrix4fv(this.location('model'),false,draw.model);gl.uniform4fv(this.location('color'),draw.color);
            const material=[draw.metallic,draw.roughness,draw.ao,draw.normalStrength,draw.cutoff,draw.unlit,draw.srgb,draw.directX,draw.packed,draw.channels,draw.emission,draw.tiling,draw.offset,draw.heightField,draw.heightUv,draw.heightSize,draw.heightRange,draw.heightBias,draw.texture,draw.normal,draw.metallicTexture,draw.roughnessTexture,draw.aoTexture,draw.packedTexture,draw.emissionTexture,draw.depth,draw.write,draw.blend,draw.cull,draw.front].join('|');
            if(material!==previousMaterial){previousMaterial=material;
            for(const name of ['metallic','roughness','ao','normalStrength','cutoff'])gl.uniform1f(this.location(name),draw[name]);
            for(const name of ['unlit','srgb','directX','packed'])gl.uniform1i(this.location(name),draw[name]?1:0);
            gl.uniform3iv(this.location('channels'),draw.channels);gl.uniform3fv(this.location('emission'),draw.emission);
            gl.uniform2fv(this.location('tiling'),draw.tiling);gl.uniform2fv(this.location('offset'),draw.offset);
            gl.activeTexture(gl.TEXTURE7);gl.bindTexture(gl.TEXTURE_2D,this.textures.get(draw.heightField)?.gpu||null);gl.uniform1i(this.location('heightField'),7);gl.uniform1i(this.location('hasHeight'),draw.heightField?1:0);gl.uniform4fv(this.location('heightUv'),draw.heightUv);gl.uniform2fv(this.location('heightSize'),draw.heightSize);gl.uniform1f(this.location('heightRange'),draw.heightRange);gl.uniform1f(this.location('heightBias'),draw.heightBias);
            const maps=[draw.texture,draw.normal,draw.metallicTexture,draw.roughnessTexture,draw.aoTexture,draw.packedTexture,draw.emissionTexture];
            for(let i=0;i<maps.length;i++){
                gl.activeTexture(gl.TEXTURE0+i);gl.bindTexture(gl.TEXTURE_2D,this.textures.get(maps[i])?.gpu||null);
                gl.uniform1i(this.location('tex'+i),i);gl.uniform1i(this.location('has'+i),maps[i]?1:0);
            }
            draw.depth?gl.enable(gl.DEPTH_TEST):gl.disable(gl.DEPTH_TEST);gl.depthMask(draw.write);
            if(draw.blend==='AlphaBlend'||draw.blend==='Additive'){
                gl.enable(gl.BLEND);gl.blendFunc(gl.SRC_ALPHA,draw.blend==='Additive'?gl.ONE:gl.ONE_MINUS_SRC_ALPHA);
            }else gl.disable(gl.BLEND);
            if(draw.cull==='None')gl.disable(gl.CULL_FACE);else{gl.enable(gl.CULL_FACE);gl.cullFace(draw.cull==='Front'?gl.FRONT:gl.BACK);}
            gl.frontFace(draw.front==='Clockwise'?gl.CW:gl.CCW);
            }
            if(instanceCount>1){gl.drawElementsInstanced(gl.TRIANGLES,m.count,gl.UNSIGNED_INT,0,instanceCount);for(let location=5;location<9;location++){gl.disableVertexAttribArray(location);gl.vertexAttribDivisor(location,0);}}
            else gl.drawElements(gl.TRIANGLES,m.count,gl.UNSIGNED_INT,0);
        }
        if(gpuQuery){gl.endQuery(this.gpuTimer.TIME_ELAPSED_EXT);this.gpuQueries.push(gpuQuery);}
        this.profile.cpu3DMs=performance.now()-geometryStart;this.profile.drawCalls=data.draws.length;
        const uiStart=performance.now();
        const ui=this.ui;ui.clearRect(0,0,width,height);
        let clipped=false;
        for(const c of data.ui){
            if(c.kind==='clip'||c.kind==='unclip'){
                if(clipped){ui.restore();clipped=false;}
                if(c.kind==='clip'){ui.save();ui.beginPath();ui.rect(c.x,c.y,c.w,c.h);ui.clip();clipped=true;}continue;
            }
            ui.globalAlpha=c.color[3];
            if(c.kind==='quad'){ui.fillStyle=`rgb(${c.color[0]*255},${c.color[1]*255},${c.color[2]*255})`;ui.fillRect(c.x,c.y,c.w,c.h);}
            else {
                const t=this.textures.get(c.texture);if(!t)continue;
                ui.imageSmoothingEnabled=!t.nearest;
                if(c.color[0]===1&&c.color[1]===1&&c.color[2]===1)ui.drawImage(t.image,c.sx,c.sy,c.sw,c.sh,c.x,c.y,c.w,c.h);
                else{
                    const key=c.texture+':'+c.color.slice(0,3).join(',');let tinted=this.tints.get(key);
                    if(!tinted){
                        tinted=document.createElement('canvas');tinted.width=t.image.width;tinted.height=t.image.height;
                        const ctx=tinted.getContext('2d');ctx.drawImage(t.image,0,0);ctx.globalCompositeOperation='multiply';ctx.fillStyle=`rgb(${c.color[0]*255},${c.color[1]*255},${c.color[2]*255})`;ctx.fillRect(0,0,tinted.width,tinted.height);ctx.globalCompositeOperation='destination-in';ctx.drawImage(t.image,0,0);
                        const bytes=tinted.width*tinted.height*4;
                        while(this.tints.size&&(this.tints.size>=32||this.tintBytes+bytes>64*1024*1024))this.deleteTint(this.tints.keys().next().value);
                        if(bytes<=64*1024*1024){this.tints.set(key,tinted);this.tintBytes+=bytes;}
                    }
                    ui.drawImage(tinted,c.sx,c.sy,c.sw,c.sh,c.x,c.y,c.w,c.h);
                }
            }
        }if(clipped)ui.restore();ui.globalAlpha=1;
        this.profile.uiCpuMs=performance.now()-uiStart;globalThis.byteEngineRenderProfile={...this.profile,viewport:'Game',gpuScope:'WebGL geometry; Canvas UI CPU only'};
    }
}
