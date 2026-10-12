import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
const {decodeUiPacket}=await import('data:text/javascript;base64,'+readFileSync('Player/ByteEngine.Browser/wwwroot/frame-wire.js').toString('base64'));
const bytes=new Uint8Array(4*56),view=new DataView(bytes.buffer);
for(let i=0;i<4;i++){
    view.setInt32(i*56,i,true);view.setInt32(i*56+4,17000001,true);
    for(let j=0;j<12;j++)view.setFloat32(i*56+8+j*4,j-2.25,true);
}
const commands=decodeUiPacket(bytes,4);
assert.deepEqual(commands.map(c=>c.kind),['quad','image','clip','unclip']);
assert.equal(commands[1].texture,17000001);assert.equal(commands[1].x,-2.25);
assert.equal(commands[1].sh,4.75);assert.deepEqual(commands[1].color,[5.75,6.75,7.75,8.75]);
const first=commands[0],color=first.color;
decodeUiPacket(bytes,4,commands);assert.equal(commands[0],first);assert.equal(commands[0].color,color);
const padded=new Uint8Array(bytes.length+7);padded.set(bytes,7);
assert.equal(decodeUiPacket(padded.subarray(7),4)[1].texture,17000001);
assert.throws(()=>decodeUiPacket(bytes,3),/Invalid/);
assert.throws(()=>decodeUiPacket(bytes,-1),/Invalid/);
view.setInt32(0,4,true);assert.throws(()=>decodeUiPacket(bytes,4),/Unknown/);
decodeUiPacket(new Uint8Array(),0,commands);assert.equal(commands.length,0);
console.log('PASS Binary UI coordinates, colors, command order, exact integer IDs, reuse, subarray offsets and malformed packet rejection');

const {BrowserRenderer}=await import('data:text/javascript;base64,'+readFileSync('Player/ByteEngine.Browser/wwwroot/renderer.js').toString('base64'));
const calls=[];
const gl=new Proxy({}, {get:(_,name)=>/^[A-Z_0-9]+$/.test(name)?0:(...args)=>{calls.push([name,...args]);return name==='getUniformLocation'?args[1]:undefined;}});
const renderer=Object.assign(Object.create(BrowserRenderer.prototype),{gl,ui:{clearRect(){},globalAlpha:1},materials:new Map(),meshes:new Map([[7,{vao:{},count:3}]]),textures:new Map(),tints:new Map(),tintBytes:0,locations:new Map(),profile:{},gpuTimer:null});
const material={material:4,color:[1,.5,.25,1],metallic:0,roughness:.7,ao:1,normalStrength:1,cutoff:-1,channels:[0,1,2],emission:[0,0,0],tiling:[1,1],offset:[0,0],heightUv:[0,0,1,1],heightSize:[1,1],heightRange:0,heightBias:0,depth:true,write:true,blend:'Opaque',cull:'None',front:'CounterClockwise'};
const frame={materials:[material],deadMaterials:[],deadMeshes:[],deadTextures:[],uploads:[],textures:[],background:[0,0,0],vp:[],eye:[0,0,0],lights:[],points:[],draws:[{id:7,material:4,model:[11],instances:[]},{id:7,material:4,model:[22],instances:[]}],ui:[]};
renderer.draw(frame,100,100);
assert.deepEqual(calls.filter(c=>c[0]==='uniformMatrix4fv'&&c[1]==='model').map(c=>c[3]),[[11],[22]]);
assert.equal(calls.filter(c=>c[0]==='uniform1f'&&c[1]==='roughness').length,1,'Shared material must bind once for consecutive draws');
calls.length=0;frame.materials=[];renderer.draw(frame,100,100);assert.equal(renderer.materials.get(4),material);
frame.materials=[{...material,color:[0,1,0,1]}];renderer.draw(frame,100,100);assert.deepEqual(renderer.materials.get(4).color,[0,1,0,1]);
frame.draws=[];frame.materials=[];frame.deadMaterials=[4];renderer.draw(frame,100,100);assert.equal(renderer.materials.size,0);
frame.draws=[{id:7,material:4,model:[],instances:[]}];assert.throws(()=>renderer.draw(frame,100,100),/Missing browser material/);
console.log('PASS Material reuse, independent model transforms, state binding, changed colors, retirement and missing-reference rejection');
