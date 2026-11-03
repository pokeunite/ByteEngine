const http=require('http'),fs=require('fs'),path=require('path');
const{chromium}=require(process.env.BYTEENGINE_PLAYWRIGHT||'playwright');
const root=path.resolve('Player/ByteEngine.Browser/wwwroot');
const html=`<canvas id="g" width="320" height="180"></canvas><canvas id="u" width="320" height="180"></canvas><script type="module">
import{BrowserRenderer}from'./renderer.js';
try{
const r=new BrowserRenderer(document.querySelector('#g'),document.querySelector('#u')),I=[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1],buffers=[];r.readBuffer=i=>buffers[i];
const bytes=v=>{buffers.push(new Uint8Array(v.buffer));return buffers.length-1};
const vertices=(x,slot)=>new Float32Array([x,-.4,.5,0,0,1,slot,0,x+.7,-.4,.5,0,0,1,slot,0,x,.4,.5,0,0,1,slot,0]);
const a=vertices(-.8,0),b=vertices(.1,1),joined=new Float32Array([...a,...b]);
const upload=(id,v,i)=>({id,vertices:bytes(v),indices:bytes(new Uint32Array(i))});
const material=(material,color,palette=[])=>({material,color,palette,texture:0,normal:0,metallicTexture:0,roughnessTexture:0,aoTexture:0,packedTexture:0,emissionTexture:0,channels:[0,1,2],emission:[0,0,0],tiling:[1,1],offset:[0,0],heightUv:[1,1,0,0],heightSize:[1,1],heightRange:1,heightBias:0,metallic:0,roughness:1,ao:1,normalStrength:1,cutoff:-1,depth:true,write:true,blend:'Opaque',cull:'None',front:'CounterClockwise',unlit:true});
const colors=[[.1,.2,.3,1],[.8,.5,.2,1]],mat=[material(1,colors[0]),material(2,colors[1]),material(3,[1,1,1,1],colors.flat())];
const command=(id,material)=>({id,material,model:I,instances:[],shadowInstances:[],wet:new Float32Array(16)});
const base={quality:'High',worldResolution:'Native',vp:I,inverseVp:I,eye:[0,0,2],background:[0,0,0],ambient:1,exposure:1,look:[1,1,0],fog:[0,0,0,0,1,0,0,0],pixelArt:[0,4,8,.35],lights:[],points:[],sky:null,shadow:null,textures:[],deadMeshes:[],deadTextures:[],deadMaterials:[],ui:[]};
function render(draws,uploads=[],materials=[]){r.draw({...base,draws,uploads,materials},320,180);const pixels=new Uint8Array(320*180*4);r.gl.readPixels(0,0,320,180,r.gl.RGBA,r.gl.UNSIGNED_BYTE,pixels);let hash=2166136261;for(const v of pixels)hash=Math.imul(hash^v,16777619)>>>0;return{hash,error:r.gl.getError()};}
const raw=render([command(1,1),command(2,2)],[upload(1,a,[0,1,2]),upload(2,b,[0,1,2]),upload(3,joined,[0,1,2,3,4,5])],mat);
const batched=render([command(3,3)]),reset=render([command(1,1),command(2,2)]);
const surfaceChecks=[];
if(r.fastSurfaceProgram){
 function compareSurface(name,draws,uploads=[],materials=[],textures=[],eligible=true){
  function paint(enabled,initial){r.fastSurfaceEnabled=enabled;r.draw({...base,draws,uploads:initial?uploads:[],materials:initial?materials:[],textures:initial?textures:[]},320,180);const pixels=new Uint8Array(320*180*4);r.gl.readPixels(0,0,320,180,r.gl.RGBA,r.gl.UNSIGNED_BYTE,pixels);return pixels;}
  const generic=paint(false,true),specialized=paint(true,false);let error=0;for(let i=0;i<generic.length;i++)error=Math.max(error,Math.abs(generic[i]-specialized[i]));surfaceChecks.push({name,maxChannelError:error,eligible:r.profile.surfaceVariant,expectedEligible:eligible,error:glError()});
 }
 function glError(){return r.gl.getError();}
 compareSurface('palette and base colours',[command(3,3)]);
 const tile=(id,pixel)=>({id,width:1,height:1,x:0,y:0,w:1,h:1,allocate:true,partial:false,nearest:true,hdr:false,pixels:bytes(new Uint8Array(pixel))});
 compareSurface('heightfield fallback',[command(1,10)],[],[{...material(10,[.3,.6,.2,1]),heightField:200,heightRange:.2,heightBias:-.1}],[tile(200,[0,128,0,255])],false);
 compareSurface('normal map fallback',[command(1,11)],[],[{...material(11,[.3,.6,.2,1]),normal:201,unlit:false}],[tile(201,[128,128,255,255])],false);
 compareSurface('sand imprint fallback',[command(1,12)],[],[{...material(12,[.3,.6,.2,1]),sandImprint:202,sandOrigin:[0,0]}],[tile(202,[128,128,0,255])],false);
 const water={surface:false,reflections:false,size:[1,1],column:[1,0,0,0],motion:[0,0,0,0],flow:[0,0,0,0],impact:[0,0,0,0],wake:[0,0,0,0],absorption:[0,0,0,0],scattering:[0,0,0,0],style:[0,0,0,0],waves:[0,0,0,0],depthRect:[0,0,1,1],depthSettings:[0,1,0,0],depthTexture:0};
 compareSurface('water fallback',[{...command(1,1),water}],[],[],[],false);
 compareSurface('mixed ordinary, mapped and water draws preserve draw order',[command(1,1),command(2,11),{...command(1,1),water},command(2,2)],[],[],[],false);
 compareSurface('mapped additive effect between ordinary surfaces',[command(1,1),command(2,13),command(1,2)],[],[{...material(13,[.3,.6,.2,.5]),emissionTexture:201,emission:[.2,.3,.1],blend:'Additive',write:false}],[],false);
 compareSurface('return to ordinary surface',[command(3,3)]);
}

const gl=r.gl,boneA=[.8,0,-.15,0,0,1,0,0,.15,0,.8,0,.1,.05,0,1],boneB=[1,0,0,0,0,1.1,0,0,0,0,1,0,-.05,.1,0,1],bones=new Float32Array([...boneA,...boneB]);
const skin=new Float32Array(24),cpu=new Float32Array(a),expected=[];
const transform=(v,m,normal=false)=>[v[0]*m[0]+v[1]*m[4]+v[2]*m[8]+(normal?0:m[12]),v[0]*m[1]+v[1]*m[5]+v[2]*m[9]+(normal?0:m[13]),v[0]*m[2]+v[1]*m[6]+v[2]*m[10]+(normal?0:m[14])];
for(let v=0;v<3;v++){skin.set([0,1,0,0,v===2?0:.25,v===2?0:.75,0,0],v*8);const p=[...a.slice(v*8,v*8+3)],normal=[0,0,1];let q=p,N=normal;if(v!==2){const pa=transform(p,boneA),pb=transform(p,boneB),na=transform(normal,boneA,true),nb=transform(normal,boneB,true);q=pa.map((x,i)=>x*.25+pb[i]*.75);N=na.map((x,i)=>x*.25+nb[i]*.75);}const length=Math.hypot(...N);N=N.map(x=>x/length);cpu.set(q,v*8);cpu.set(N,v*8+3);expected.push({q,N});}
const cpuCommand=command(4,1),gpuCommand={...command(5,1),skinBuffer:bytes(bones)},gpuUpload={...upload(5,a,[0,1,2]),skin:bytes(skin)};
const cpuSkin=render([cpuCommand],[upload(4,cpu,[0,1,2]),gpuUpload]),gpuSkin=render([gpuCommand]);
function linked(original,feedback){const program=gl.createProgram();for(const shader of gl.getAttachedShaders(original))gl.attachShader(program,shader);if(feedback)gl.transformFeedbackVaryings(program,feedback,gl.INTERLEAVED_ATTRIBS);gl.linkProgram(program);if(!gl.getProgramParameter(program,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(program));return program;}
const feedback=linked(r.program,['world','n']);gl.useProgram(feedback);for(const n of ['vp','model'])gl.uniformMatrix4fv(gl.getUniformLocation(feedback,n),false,I);gl.uniform1i(gl.getUniformLocation(feedback,'skinBoneCount'),2);gl.uniformMatrix4fv(gl.getUniformLocation(feedback,'skinBones[0]'),false,bones);gl.bindVertexArray(r.meshes.get(5).vao);
const resultBuffer=gl.createBuffer();gl.bindBuffer(gl.TRANSFORM_FEEDBACK_BUFFER,resultBuffer);gl.bufferData(gl.TRANSFORM_FEEDBACK_BUFFER,18*4,gl.DYNAMIC_READ);gl.bindBufferBase(gl.TRANSFORM_FEEDBACK_BUFFER,0,resultBuffer);gl.enable(gl.RASTERIZER_DISCARD);gl.beginTransformFeedback(gl.TRIANGLES);gl.drawArrays(gl.TRIANGLES,0,3);gl.endTransformFeedback();gl.disable(gl.RASTERIZER_DISCARD);const actual=new Float32Array(18);gl.getBufferSubData(gl.TRANSFORM_FEEDBACK_BUFFER,0,actual);gl.bindBufferBase(gl.TRANSFORM_FEEDBACK_BUFFER,0,null);let maxError=0;for(let v=0;v<3;v++){const N=[...actual.slice(v*6+3,v*6+6)],length=Math.hypot(...N);for(let i=0;i<3;i++){maxError=Math.max(maxError,Math.abs(actual[v*6+i]-expected[v].q[i]),Math.abs(N[i]/length-expected[v].N[i]));}}gl.deleteBuffer(resultBuffer);gl.deleteProgram(feedback);

const shadowFeedback=linked(r.shadowProgram,['gl_Position']);gl.useProgram(shadowFeedback);gl.uniformMatrix4fv(gl.getUniformLocation(shadowFeedback,'vp'),false,I);gl.uniform1i(gl.getUniformLocation(shadowFeedback,'skinBoneCount'),2);gl.uniformMatrix4fv(gl.getUniformLocation(shadowFeedback,'skinBones[0]'),false,bones);gl.bindVertexArray(r.meshes.get(5).vao);const instance=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,instance);gl.bufferData(gl.ARRAY_BUFFER,new Float32Array(I),gl.STATIC_DRAW);for(let column=0;column<4;column++){gl.enableVertexAttribArray(5+column);gl.vertexAttribPointer(5+column,4,gl.FLOAT,false,64,column*16);gl.vertexAttribDivisor(5+column,1);}
const shadowBuffer=gl.createBuffer();gl.bindBuffer(gl.TRANSFORM_FEEDBACK_BUFFER,shadowBuffer);gl.bufferData(gl.TRANSFORM_FEEDBACK_BUFFER,12*4,gl.DYNAMIC_READ);gl.bindBufferBase(gl.TRANSFORM_FEEDBACK_BUFFER,0,shadowBuffer);gl.enable(gl.RASTERIZER_DISCARD);gl.beginTransformFeedback(gl.TRIANGLES);gl.drawArraysInstanced(gl.TRIANGLES,0,3,1);gl.endTransformFeedback();gl.disable(gl.RASTERIZER_DISCARD);const shadowActual=new Float32Array(12);gl.getBufferSubData(gl.TRANSFORM_FEEDBACK_BUFFER,0,shadowActual);gl.bindBufferBase(gl.TRANSFORM_FEEDBACK_BUFFER,0,null);let shadowError=0;for(let v=0;v<3;v++){const expectedClip=[expected[v].q[0],expected[v].q[1],2*expected[v].q[2]-1,1];for(let i=0;i<4;i++)shadowError=Math.max(shadowError,Math.abs(shadowActual[v*4+i]-expectedClip[i]));}gl.deleteBuffer(shadowBuffer);gl.deleteBuffer(instance);gl.deleteProgram(shadowFeedback);
window.result={raw,batched,reset,cpuSkin,gpuSkin,maxError,shadowError,surfaceChecks,error:gl.getError(),maximumSkinningBones:r.maximumSkinningBones,identical:raw.hash===batched.hash&&raw.hash===reset.hash&&cpuSkin.hash===gpuSkin.hash&&maxError<.00001&&shadowError<.00001&&surfaceChecks.every(x=>x.maxChannelError===0&&x.eligible===x.expectedEligible&&!x.error)};

}catch(e){window.failure=String(e.stack||e)}
</script>`;
const server=http.createServer((req,res)=>{if(req.url==='/'){res.setHeader('Content-Type','text/html');return res.end(html);}const file=path.resolve(root,'.'+req.url);if(!file.startsWith(root+path.sep)){res.writeHead(403);return res.end();}fs.readFile(file,(e,b)=>{res.statusCode=e?404:200;res.setHeader('Content-Type','application/javascript');res.end(e?'missing':b);});});
(async()=>{let browser;try{await new Promise(r=>server.listen(0,'127.0.0.1',r));browser=await chromium.launch({channel:'chrome',headless:true});const page=await browser.newPage();await page.goto('http://127.0.0.1:'+server.address().port);await page.waitForFunction(()=>window.result||window.failure);const result=await page.evaluate(()=>window.result||{failure:window.failure});console.log(JSON.stringify(result));if(!result.identical||(result.error||Object.values(result).some(x=>x?.error)))process.exitCode=1;}catch(e){console.error(e);process.exitCode=1;}finally{await browser?.close();server.close();}})();
