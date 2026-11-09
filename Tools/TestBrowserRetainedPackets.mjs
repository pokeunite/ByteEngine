import assert from 'node:assert/strict';
import {RetainedDrawPackets} from '../Player/ByteEngine.Browser/wwwroot/frame-wire.js';
const cache=new RetainedDrawPackets(),packet=new Uint8Array(136),skin=new Uint8Array(64);new Float32Array(packet.buffer)[2]=1;skin[0]=23;
let reads=0;const read=i=>{reads++;return i===0?packet:skin;};
let draws=[{packetSlot:0,dataBuffer:0,skinBuffer:1,skinned:true}];cache.apply(draws,read);assert.equal(reads,2);packet.fill(0);skin.fill(0);
draws=[{packetSlot:0,dataBuffer:-1,skinBuffer:-1,skinned:true}];cache.apply(draws,()=>{throw Error('Unchanged packet read');});assert.equal(draws[0].model[0],1);assert.equal(draws[0].skinPacket[0],23);
cache.apply([{packetSlot:0,dataBuffer:-1,skinBuffer:-1,skinned:false}],read);assert.equal(cache.skins[0],null);cache.apply([],read);assert.equal(cache.packets.length,0);assert.equal(cache.skins.length,0);
assert.throws(()=>cache.apply([{packetSlot:0,dataBuffer:-1,skinned:false}],read),/Missing/);
assert.throws(()=>cache.apply([{packetSlot:100,dataBuffer:0}],read),/Invalid/);
console.log('PASS retained packets: owned lifetime, zero unchanged reads, skin removal, bounded eviction and invalid references');

const {BrowserRenderer}=await import('../Player/ByteEngine.Browser/wwwroot/renderer.js');
let uploads=0,created=0;const renderer=Object.create(BrowserRenderer.prototype);
renderer.gl={ARRAY_BUFFER:1,STREAM_DRAW:2,createBuffer:()=>++created,bindBuffer(){},bufferData(){uploads++;}};
const matrices=new Float32Array(32),command={packetSlot:0,instances:matrices,shadowInstances:matrices};
renderer.bindInstanceStream(command);renderer.bindInstanceStream({...command,instances:new Float32Array(matrices.buffer)});assert.equal(uploads,1);
renderer.bindInstanceStream(command,true);assert.equal(uploads,2);assert.equal(created,2);
renderer.bindInstanceStream({...command,instances:matrices.slice()});assert.equal(uploads,3);
console.log('PASS resident GPU instance streams: unchanged packets skip uploads, changed transforms upload, shadow stream stays independent');

const sparse=new RetainedDrawPackets();sparse.apply([{packetSlot:2,dataBuffer:0,skinned:false}],()=>new Uint8Array(136),3);sparse.apply([{packetSlot:2,dataBuffer:-1,skinned:false}],()=>{throw Error('Subset reread');},3);assert.equal(sparse.packets.length,3);console.log('PASS retained sparse slots survive filtered diagnostic passes');
