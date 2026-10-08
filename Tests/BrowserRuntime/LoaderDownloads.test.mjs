import {readFileSync} from 'node:fs';
import {deflateSync} from 'node:zlib';
import {createHash} from 'node:crypto';
const {loadContent}=await import('data:text/javascript;base64,'+readFileSync('Player/ByteEngine.Browser/wwwroot/content.js').toString('base64'));
const hash=b=>createHash('sha256').update(b).digest('hex');
const path=Buffer.from('Content/Game.byteproject'),raw=Buffer.from('{}'),compressed=deflateSync(raw);
const int=Buffer.alloc(4);int.writeInt32LE(1);const len=Buffer.alloc(8);len.writeBigInt64LE(BigInt(raw.length));const clen=Buffer.alloc(8);clen.writeBigInt64LE(BigInt(compressed.length));
const pak=Buffer.concat([Buffer.from('BYTEPAK2'),int,Buffer.from([path.length]),path,len,Buffer.from(hash(raw),'hex'),clen,compressed]);
const parts=[pak.subarray(0,50),pak.subarray(50)];const manifest={formatVersion:3,packageSize:pak.length,packageSha256:hash(pak),files:parts.map((data,i)=>({path:'Game.bytepak.'+i+'.bin',size:data.length,sha256:hash(data)}))};
let requests={},mounts=[];globalThis.fetch=async url=>{if(url.includes('web-game.json'))return Response.json(manifest);const i=url.includes('.0.bin')?0:1;requests[i]=(requests[i]||0)+1;if(requests[i]===1)return new Response(i===0?parts[i].subarray(0,5):Buffer.alloc(parts[i].length));return new Response(parts[i]);};
await loadContent({MountFile:(p,d)=>mounts.push([p,atob(d)])},{textContent:''});if(requests[0]!==2||requests[1]!==2||mounts[0][1]!=='{}')throw Error('Retries/reassembly/mount failed');console.log('PASS Truncated body retries, corrupt chunk retries, verified reassembly and mount');
requests={};globalThis.fetch=async url=>{if(url.includes('web-game.json'))return Response.json({...manifest,formatVersion:2,files:[{path:'Game.bytepak',size:pak.length,sha256:hash(pak)}]});return new Response(pak);};await loadContent({MountFile:()=>{}},{textContent:''});console.log('PASS Legacy format 2 still loads');
mounts=[];await loadContent({MountBytes:(p,d)=>mounts.push([p,new TextDecoder().decode(d)]),MountFile:()=>{throw Error('Binary mount must not use base64');}},{textContent:''});if(mounts.length!==1||mounts[0][1]!=='{}')throw Error('Binary mount failed');console.log('PASS Binary mounting avoids base64 and preserves bytes');
let failures=0;globalThis.fetch=async url=>{if(url.includes('web-game.json'))return Response.json(manifest);failures++;throw new TypeError('Failed to fetch');};try{await loadContent({MountFile:()=>{throw Error('Should not mount');}},{textContent:''});throw Error('Failure accepted');}catch(e){if(!String(e).includes('after 4 attempts')||(failures<4||failures>manifest.files.length*4))throw e;}console.log('PASS Failed downloads stop after four attempts with actionable error');

requests={};let ranged=false;globalThis.fetch=async (url,options)=>{
 if(url.includes('web-game.json'))return Response.json(manifest);
 const i=url.includes('.0.bin')?0:1;requests[i]=(requests[i]||0)+1;
 if(i===0&&requests[i]===1)return new Response(new ReadableStream({start(c){c.enqueue(parts[0].subarray(0,5));setTimeout(()=>c.error(new Error('connection interrupted')),10);}}));
 if(i===0){if(options.headers?.Range!=='bytes=5-')throw Error('Lost partial response');ranged=true;return new Response(parts[0].subarray(5),{status:206,headers:{'Content-Range':`bytes 5-${parts[0].length-1}/${parts[0].length}`}});}
 return new Response(parts[i]);
};await loadContent({MountBytes:()=>{}},{textContent:''});if(!ranged)throw Error('No resumed download');console.log('PASS Interrupted body resumes with verified Range response');
const stored=new Map();globalThis.caches={open:async()=>({match:async key=>stored.get(typeof key==='string'?key:key.url)?.clone(),put:async(key,value)=>stored.set(key,value.clone()),keys:async()=>[...stored.keys()],delete:async key=>stored.delete(typeof key==='string'?key:key.url)})};
let contentRequests=0;globalThis.fetch=async url=>{if(url.includes('web-game.json'))return Response.json(manifest);contentRequests++;return new Response(url.includes('.0.bin')?parts[0]:parts[1]);};
await loadContent({MountBytes:()=>{}},{textContent:''});await loadContent({MountBytes:()=>{}},{textContent:''});if(contentRequests!==2)throw Error('Verified cache did not survive reload');
const firstKey=[...stored.keys()][0];stored.set(firstKey,new Response(Buffer.from('corrupt')));await loadContent({MountBytes:()=>{}},{textContent:''});if(contentRequests!==3)throw Error('Corrupt cache was accepted');
console.log('PASS Cached chunks survive reload; corrupt cached data is discarded and downloaded again');
globalThis.caches={open:async()=>{throw Error('Storage blocked');}};await loadContent({MountBytes:()=>{}},{textContent:''});console.log('PASS Restricted browser storage falls back to network');delete globalThis.caches;
let ready=false,downloadedBeforeRuntime=false;const delayedGame=new Promise(resolve=>setTimeout(()=>{ready=true;resolve({MountBytes:()=>{}});},100));globalThis.fetch=async url=>{if(url.includes('web-game.json'))return Response.json(manifest);if(!ready)downloadedBeforeRuntime=true;return new Response(url.includes('.0.bin')?parts[0]:parts[1]);};await loadContent(delayedGame,{textContent:''});if(!downloadedBeforeRuntime)throw Error('Data download waits for runtime initialization');console.log('PASS Runtime initialization and content downloads overlap');
