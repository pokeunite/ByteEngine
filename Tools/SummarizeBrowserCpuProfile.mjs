import {readFileSync} from 'node:fs';
import {basename} from 'node:path';

const [profileFile,symbolFile]=process.argv.slice(2);
if(!profileFile||!symbolFile)throw Error('Usage: node Tools/SummarizeBrowserCpuProfile.mjs <cpu-profile.json> <matching.symbols>');
const profile=JSON.parse(readFileSync(profileFile,'utf8'));
if(profile.byteEngineRuntimeSha256&&!basename(symbolFile).toUpperCase().startsWith(profile.byteEngineRuntimeSha256.toUpperCase()+'.'))
    throw Error('CPU profile and symbol map belong to different WASM binaries.');
const symbols=new Map(readFileSync(symbolFile,'utf8').split(/\r?\n/).flatMap(line=>{
    const match=/^(\d+):(.*)$/.exec(line);return match?[[Number(match[1]),match[2]]]:[];
}));
if(!symbols.size)throw Error('No WebAssembly function symbols found.');
const nodes=new Map(profile.nodes.map(n=>[n.id,n]));
const parents=new Map();for(const node of profile.nodes)for(const child of node.children??[])parents.set(child,node.id);
const own=new Map(),inclusive=new Map();
function name(id){const raw=nodes.get(id)?.callFrame.functionName??'(unknown)';const match=/wasm-function\[(\d+)\]/.exec(raw);return match?(symbols.get(Number(match[1]))??raw):raw;}
for(let i=0;i<profile.samples.length;i++){
    const id=profile.samples[i],ms=(profile.timeDeltas[i]??0)/1000;
    own.set(name(id),(own.get(name(id))??0)+ms);
    const seen=new Set();for(let at=id;at!==undefined;at=parents.get(at)){
        const method=name(at);if(seen.has(method))continue;seen.add(method);inclusive.set(method,(inclusive.get(method)??0)+ms);
    }
}
const top=map=>[...map].sort((a,b)=>b[1]-a[1]).slice(0,30).map(([method,ms])=>({ms:+ms.toFixed(2),method}));
console.log(JSON.stringify({warning:'Use only the symbol map for the exact native WASM binary captured.',own:top(own),inclusive:top(inclusive)},null,2));
