import assert from 'node:assert/strict';
import {cacheUniforms} from '../Player/ByteEngine.Browser/wwwroot/uniform-cache.js';
let calls=[];const gl={};for(const n of ['uniform1f','uniform1i','uniform2f','uniform3f','uniform4f','uniform2fv','uniform3fv','uniform4fv','uniform3iv','uniformMatrix4fv'])gl[n]=(...a)=>calls.push([n,...a]);
const cache=cacheUniforms(gl),a={},b={};gl.uniform1f(a,1);gl.uniform1f(a,1);assert.equal(calls.length,1);gl.uniform1f(b,1);assert.equal(calls.length,2);gl.uniform1f(a,2);assert.equal(calls.length,3);
const v=new Float32Array([1,2,3]);gl.uniform3fv(a,v);gl.uniform3fv(a,v);assert.equal(calls.length,4);v[0]=9;gl.uniform3fv(a,v);assert.equal(calls.length,5);
cache.enabled=false;gl.uniform1f(a,5);cache.enabled=true;gl.uniform1f(a,2);assert.equal(calls.length,7);
const m=new Float32Array(16);gl.uniformMatrix4fv(b,false,m);gl.uniformMatrix4fv(b,false,m);assert.equal(calls.length,8);m[0]=2;gl.uniformMatrix4fv(b,false,m);assert.equal(calls.length,9);
gl.uniform3fv(a,v,1,2);gl.uniform3fv(a,v,1,2);assert.equal(calls.length,11);gl.uniform1f(null,1);assert.equal(calls.length,11);
console.log('PASS uniform cache: unchanged scalars/vectors/matrices, mutable arrays, distinct program locations, disabled-cache writes, offset overloads and null locations');
