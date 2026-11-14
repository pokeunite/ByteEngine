const fs=require('fs'),assert=require('assert');
(async()=>{
 const source=fs.readFileSync('Player/ByteEngine.Browser/wwwroot/frame-wire.js');
 const {decodeDrawPacket}=await import('data:text/javascript;base64,'+source.toString('base64'));
 const sourceBytes=fs.readFileSync(process.argv[2]);const bytes=new Uint8Array(sourceBytes.length);bytes.set(sourceBytes);
 const output=decodeDrawPacket(bytes);assert.equal(output.length,2);assert.equal(output[0].id,7);assert.equal(output[0].material,12);
 assert.equal(output[0].instances.length,32);assert.equal(output[0].shadowInstances.length,16);assert.equal(output[0].model[12],3);assert.equal(output[0].water,null);
 assert.equal(output[1].water.depthTexture,23);assert.equal(output[1].water.size[0],5);assert(Math.abs(output[1].water.column[2]-.8)<1e-6);assert.equal(output[1].receiveShadows,false);
 const reused=decodeDrawPacket(bytes,output);assert.strictEqual(reused,output);assert.equal(reused[1].water.reflections,true);
 for(const length of [0,7,bytes.length-1])assert.throws(()=>decodeDrawPacket(bytes.slice(0,length)));
 const bad=bytes.slice();new DataView(bad.buffer).setInt32(20,1,true);assert.throws(()=>decodeDrawPacket(bad));
 assert.throws(()=>decodeDrawPacket(new Uint8Array(bytes.length+1)));
 const empty=new Uint8Array(8);new DataView(empty.buffer).setInt32(0,0x31445742,true);assert.equal(decodeDrawPacket(empty,output).length,0);
 console.log('PASS Cross-language binary draw decoding and malformed-buffer rejection.');
})().catch(e=>{console.error(e);process.exitCode=1;});
