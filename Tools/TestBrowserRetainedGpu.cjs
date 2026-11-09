const fs=require('fs'),http=require('http'),path=require('path'),assert=require('assert/strict');
const {chromium}=require(process.env.BYTEENGINE_PLAYWRIGHT||'playwright');
const root=path.resolve('Player/ByteEngine.Browser/wwwroot');
const html=`<canvas id="g" width="64" height="64"></canvas><canvas id="u" width="64" height="64"></canvas><script type="module">
import {BrowserRenderer} from './renderer.js';
try{
 const canvas=document.querySelector('#g'),ui=document.querySelector('#u'),r=new BrowserRenderer(canvas,ui),gl=r.gl;
 const I=[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1];
 const vertices=new Float32Array([-.4,-.4,0,0,0,1,0,0,.4,-.4,0,0,0,1,1,0,0,.4,0,0,0,1,.5,1]),indices=new Uint32Array([0,1,2]);
 const packet=new Uint8Array(264);new DataView(packet.buffer).setInt32(0,2,true);const f=new Float32Array(packet.buffer,8);f.set(I);f.set(I,32);f.set(I,48);f[44]=-.3;f[60]=.3;
 const buffers=[new Uint8Array(vertices.buffer),new Uint8Array(indices.buffer),packet];let reads=0;r.readBuffer=i=>{reads++;return buffers[i];};
 const material={material:1,color:[.7,.2,.1,1],palette:[],heightUv:[1,1,0,0],heightSize:[1,1],heightRange:0,heightBias:0,normalStrength:1,metallic:0,roughness:.7,ao:1,cutoff:-1,channels:[-1,-1,-1],unlit:true,srgb:false,depth:true,write:true,blend:'Opaque',cull:'None',front:'CounterClockwise',tiling:[1,1],offset:[0,0],emission:[0,0,0],sandOrigin:[0,0]};
 const base={quality:'Fast',vp:I,inverseVp:I,eye:[0,0,2],background:[0,0,0],ambient:1,exposure:1,look:[1,1,0],lights:[],points:[],uploads:[],materials:[],textures:[],deadMaterials:[],deadMeshes:[],deadTextures:[],ui:[],separateShadows:true};
 const command=()=>({id:1,material:1,packetSlot:0,dataBuffer:-1,skinBuffer:-1,skinned:false});
 const image=()=>{const b=new Uint8Array(canvas.width*canvas.height*4);gl.readPixels(0,0,canvas.width,canvas.height,gl.RGBA,gl.UNSIGNED_BYTE,b);return b;};
 r.draw({...base,uploads:[{id:1,vertices:0,indices:1,skin:-1}],materials:[material],draws:[{...command(),dataBuffer:2}]},64,64);const first=image();if(!first.some((v,i)=>i%4!==3&&v>10))throw Error('Blank fixture');
 const originalUpload=gl.bufferData.bind(gl);let uploads=0;gl.bufferData=(...a)=>{uploads++;return originalUpload(...a);};
 reads=0;packet.fill(0);r.draw({...base,draws:[command()]},64,64);const second=image();if(reads||uploads||!first.every((v,i)=>v===second[i]))throw Error('Unchanged frame copied/uploaded or changed pixels: '+reads+'/'+uploads);
 const moved=new Uint8Array(264);new DataView(moved.buffer).setInt32(0,2,true);const mf=new Float32Array(moved.buffer,8);mf.set(I);mf.set(I,32);mf.set(I,48);mf[44]=-.6;mf[60]=.6;buffers[2]=moved;
 r.draw({...base,draws:[{...command(),dataBuffer:2}]},64,64);if(first.every((v,i)=>v===image()[i]))throw Error('Changed transforms did not update');
 canvas.width=128;canvas.height=128;ui.width=128;ui.height=128;r.draw({...base,draws:[command()]},128,128);if(!image().some((v,i)=>i%4!==3&&v>10))throw Error('Resize lost retained geometry');
 r.draw({...base,draws:[{...command(),shadowOnly:true}]},128,128);if(r.profile.drawCalls!==0)throw Error('Shadow-only command entered main pass');
 r.draw({...base,draws:[],deadMeshes:[1]},128,128);if(r.retainedPackets.packets.length||r.instanceStreams.size)throw Error('Retired streams retained');
 if(gl.getError())throw Error('WebGL error');window.result={unchangedReads:0,unchangedUploads:0,pixelParity:true,movement:true,resize:true,shadowIsolation:true,eviction:true};
}catch(e){window.failure=String(e.stack||e);}</script>`;
const server=http.createServer((req,res)=>{if(req.url==='/'){res.setHeader('Content-Type','text/html');return res.end(html);}const file=path.resolve(root,'.'+req.url);if(!file.startsWith(root+path.sep)){res.writeHead(403);return res.end();}fs.readFile(file,(e,b)=>{res.statusCode=e?404:200;res.setHeader('Content-Type','application/javascript');res.end(e?'Missing':b);});});
(async()=>{let browser;try{await new Promise(r=>server.listen(0,'127.0.0.1',r));browser=await chromium.launch({channel:'chrome',headless:true});const page=await browser.newPage();await page.goto('http://127.0.0.1:'+server.address().port);await page.waitForFunction(()=>window.result||window.failure,null,{timeout:90000});const result=await page.evaluate(()=>window.result||{failure:window.failure});assert(!result.failure,result.failure);console.log('PASS retained GPU packets '+JSON.stringify(result));}catch(e){console.error(e);process.exitCode=1;}finally{await browser?.close();server.close();}})();
