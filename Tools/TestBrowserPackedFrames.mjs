import assert from 'node:assert/strict';
import {PackedFramePool,packedFrameReader} from '../Player/ByteEngine.Browser/wwwroot/packed-frame.js';
const pool=new PackedFramePool(),sources=[new Uint8Array([1,2,3]),new Uint8Array(0),new Uint8Array([4,5,6,7,8])];
const frame={bufferViews:new Array(sources.length*2).fill(0)};
const first=pool.pack(frame,i=>sources[i]);
assert.deepEqual([...first.ranges],[0,3,4,0,4,5]);
sources[0].fill(255); // Simulate reuse/growth of the managed frame heap.
let read=packedFrameReader(first.packed,first.ranges);
assert.deepEqual([...read(0)],[1,2,3]);assert.deepEqual([...read(2)],[4,5,6,7,8]);assert.equal(read(1).length,0);
assert.throws(()=>read(-1));assert.throws(()=>read(3));assert.throws(()=>packedFrameReader(first.packed,new Uint32Array([3,2]))(0));assert.throws(()=>packedFrameReader(first.packed,new Uint32Array([0,100]))(0));
const returned=structuredClone(first,{transfer:[first.packed]});assert.equal(first.packed.byteLength,0);
pool.release(returned.packed);const reused=pool.pack(frame,i=>sources[i]);assert.equal(reused.packed,returned.packed);assert.equal(pool.bytes,0);assert.equal(packedFrameReader(reused.packed,reused.ranges)(0)[0],255);
for(let i=1;i<400;i++)pool.release(new ArrayBuffer(i*4));assert(pool.pool.size<=256);assert(pool.bytes<=pool.limit);
assert.throws(()=>pool.pack({bufferViews:[0]},()=>sources[0]));
console.log('PASS packed frame alignment, ownership, heap reuse, transfer return, invalid bounds and bounded pool');
