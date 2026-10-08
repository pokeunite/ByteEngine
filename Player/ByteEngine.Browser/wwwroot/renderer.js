export class BrowserRenderer {
    constructor(canvas, overlay) {
        const gl=this.gl=canvas.getContext('webgl2',{antialias:true,alpha:false});
        if(!gl) throw new Error('WebGL 2 is unavailable on this browser/device.');
        this.ui=overlay.getContext('2d');this.meshes=new Map();this.textures=new Map();
        this.scratch=document.createElement('canvas');
        const compile=(type,source)=>{const shader=gl.createShader(type);gl.shaderSource(shader,source);
            gl.compileShader(shader);if(!gl.getShaderParameter(shader,gl.COMPILE_STATUS))throw new Error(gl.getShaderInfoLog(shader));return shader;};
        const program=this.program=gl.createProgram();
        gl.attachShader(program,compile(gl.VERTEX_SHADER,`#version 300 es
        layout(location=0) in vec3 position;layout(location=1) in vec3 normal;layout(location=2) in vec2 uv;
        uniform mat4 vp,model;out vec3 n,world;out vec2 texcoord;
        uniform sampler2D heightField;uniform bool hasHeight;uniform vec4 heightUv;uniform float heightRange,heightBias;uniform vec2 heightSize;
        float surfaceHeight(vec2 coord){vec2 rg=textureLod(heightField,clamp(coord,vec2(0.0),vec2(1.0)),0.0).rg;return dot(rg,vec2(65280.0,255.0))/65535.0*heightRange+heightBias;}
        void main(){vec3 pos=position,N=normal;if(hasHeight){vec2 coord=uv*heightUv.xy+heightUv.zw;vec2 step=1.0/vec2(textureSize(heightField,0));pos.y+=surfaceHeight(coord);vec2 gradient=vec2(surfaceHeight(coord+vec2(step.x,0.0))-surfaceHeight(coord-vec2(step.x,0.0)),surfaceHeight(coord+vec2(0.0,step.y))-surfaceHeight(coord-vec2(0.0,step.y)))/(2.0*step*heightSize);N=normalize(vec3(-gradient.x,1.0,-gradient.y));}vec4 p=model*vec4(pos,1.0);world=p.xyz;n=mat3(transpose(inverse(model)))*N;
        texcoord=uv;gl_Position=vp*p;gl_Position.z=2.0*gl_Position.z-gl_Position.w;}`));
        gl.attachShader(program,compile(gl.FRAGMENT_SHADER,`#version 300 es
        precision highp float;
        in vec3 n,world;in vec2 texcoord;out vec4 fragment;
        uniform vec4 color;uniform vec3 eye,emission;uniform vec2 tiling,offset;
        uniform sampler2D tex0,tex1,tex2,tex3,tex4,tex5,tex6;
        uniform bool has0,has1,has2,has3,has4,has5,has6,unlit,srgb,directX,packed;
        uniform float metallic,roughness,ao,normalStrength,cutoff,ambient;
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
            vec2 uv=texcoord*tiling+offset;vec4 base=color*(has0?texture(tex0,uv):vec4(1.0));
            if(cutoff>=0.0&&base.a<cutoff)discard;if(srgb)base.rgb=pow(max(base.rgb,vec3(0.0)),vec3(2.2));
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
            outColor+=emission*(has6?texture(tex6,uv).rgb:vec3(1.0));
            if(srgb)outColor=pow(max(outColor,vec3(0.0)),vec3(1.0/2.2));fragment=vec4(outColor,base.a);
        }`));
        gl.linkProgram(program);if(!gl.getProgramParameter(program,gl.LINK_STATUS))throw new Error(gl.getProgramInfoLog(program));
        this.locations=new Map();
    }
    location(name){if(!this.locations.has(name))this.locations.set(name,this.gl.getUniformLocation(this.program,name));return this.locations.get(name);}
    draw(data,width,height){
        const gl=this.gl;
        for(const id of data.deadMeshes){const m=this.meshes.get(id);if(m){gl.deleteVertexArray(m.vao);gl.deleteBuffer(m.vbo);gl.deleteBuffer(m.ibo);this.meshes.delete(id);}}
        for(const id of data.deadTextures){const t=this.textures.get(id);if(t){gl.deleteTexture(t.gpu);this.textures.delete(id);}}
        for(const upload of data.uploads){
            let m=this.meshes.get(upload.id);
            if(!m){m={vao:gl.createVertexArray(),vbo:gl.createBuffer(),ibo:gl.createBuffer()};this.meshes.set(upload.id,m);}
            gl.bindVertexArray(m.vao);gl.bindBuffer(gl.ARRAY_BUFFER,m.vbo);
            gl.bufferData(gl.ARRAY_BUFFER,new Float32Array(Uint8Array.from(atob(upload.vertices),c=>c.charCodeAt(0)).buffer),gl.DYNAMIC_DRAW);
            gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,m.ibo);gl.bufferData(gl.ELEMENT_ARRAY_BUFFER,new Uint32Array(Uint8Array.from(atob(upload.indices),c=>c.charCodeAt(0)).buffer),gl.STATIC_DRAW);
            for(const [index,count,offset] of [[0,3,0],[1,3,12],[2,2,24]]){
                gl.enableVertexAttribArray(index);gl.vertexAttribPointer(index,count,gl.FLOAT,false,32,offset);
            }m.count=atob(upload.indices).length/4;
        }
        for(const upload of data.textures){
            let t=this.textures.get(upload.id);
            if(!t){t={gpu:gl.createTexture(),image:document.createElement('canvas')};this.textures.set(upload.id,t);}
            const decoded=atob(upload.pixels),bytes=new Uint8Array(decoded.length);
            for(let i=0;i<bytes.length;i++)bytes[i]=decoded.charCodeAt(i);
            gl.bindTexture(gl.TEXTURE_2D,t.gpu);gl.pixelStorei(gl.UNPACK_ALIGNMENT,1);
            if(upload.allocate)gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA8,upload.width,upload.height,0,gl.RGBA,gl.UNSIGNED_BYTE,null);if(upload.partial)gl.texSubImage2D(gl.TEXTURE_2D,0,upload.x,upload.y,upload.w,upload.h,gl.RGBA,gl.UNSIGNED_BYTE,bytes);else gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA8,upload.width,upload.height,0,gl.RGBA,gl.UNSIGNED_BYTE,bytes);
            gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,upload.nearest?gl.NEAREST:gl.LINEAR);
            gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.LINEAR);
            gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.REPEAT);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.REPEAT);
            if(!upload.partial||upload.allocate){t.image.width=upload.width;t.image.height=upload.height;}t.nearest=upload.nearest;
            t.image.getContext('2d').putImageData(new ImageData(new Uint8ClampedArray(bytes.buffer),upload.w,upload.h),upload.x,upload.y);
        }
        gl.viewport(0,0,width,height);gl.depthMask(true);gl.clearColor(...data.background,1);
        gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT);gl.useProgram(this.program);
        gl.uniformMatrix4fv(this.location('vp'),false,data.vp);gl.uniform3fv(this.location('eye'),data.eye);
        gl.uniform1f(this.location('ambient'),data.ambient);
        gl.uniform1i(this.location('lightCount'),data.lights.length);gl.uniform1i(this.location('pointCount'),data.points.length);
        if(data.lights.length){gl.uniform3fv(this.location('lightDirection[0]'),data.lights.flatMap(l=>l.direction));
            gl.uniform3fv(this.location('lightColor[0]'),data.lights.flatMap(l=>l.color));}
        if(data.points.length){gl.uniform3fv(this.location('pointPosition[0]'),data.points.flatMap(l=>l.position));
            gl.uniform3fv(this.location('pointColor[0]'),data.points.flatMap(l=>l.color));gl.uniform1fv(this.location('pointRange[0]'),data.points.map(l=>l.range));}
        for(const draw of data.draws){
            const m=this.meshes.get(draw.id);gl.bindVertexArray(m.vao);
            gl.uniformMatrix4fv(this.location('model'),false,draw.model);gl.uniform4fv(this.location('color'),draw.color);
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
            gl.drawElements(gl.TRIANGLES,m.count,gl.UNSIGNED_INT,0);
        }
        const ui=this.ui;ui.clearRect(0,0,width,height);
        for(const c of data.ui){
            ui.globalAlpha=c.color[3];
            if(c.kind==='quad'){ui.fillStyle=`rgb(${c.color[0]*255},${c.color[1]*255},${c.color[2]*255})`;ui.fillRect(c.x,c.y,c.w,c.h);}
            else {
                const t=this.textures.get(c.texture);if(!t)continue;
                ui.imageSmoothingEnabled=!t.nearest;
                if(c.color[0]===1&&c.color[1]===1&&c.color[2]===1)ui.drawImage(t.image,c.sx,c.sy,c.sw,c.sh,c.x,c.y,c.w,c.h);
                else{
                    const s=this.scratch;s.width=Math.max(1,c.sw);s.height=Math.max(1,c.sh);
                    const ctx=s.getContext('2d');ctx.drawImage(t.image,c.sx,c.sy,c.sw,c.sh,0,0,s.width,s.height);
                    ctx.globalCompositeOperation='multiply';ctx.fillStyle=`rgb(${c.color[0]*255},${c.color[1]*255},${c.color[2]*255})`;ctx.fillRect(0,0,s.width,s.height);
                    ctx.globalCompositeOperation='destination-in';ctx.drawImage(t.image,c.sx,c.sy,c.sw,c.sh,0,0,s.width,s.height);
                    ui.drawImage(s,c.x,c.y,c.w,c.h);
                }
            }
        }ui.globalAlpha=1;
    }
}
