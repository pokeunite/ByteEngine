import {readFileSync,readdirSync,statSync} from 'node:fs';
import {join} from 'node:path';
import {createHash} from 'node:crypto';
const root=process.argv[2]||'Dist/ByteEngine/BrowserRuntime',framework=join(root,'_framework');
const boot=JSON.parse(readFileSync(join(framework,'blazor.boot.json'),'utf8'));
let checked=0;
for(const category of Object.values(boot.resources)){
    if(!category||typeof category!=='object')continue;
    for(const [name,hash] of Object.entries(category)){
        if(typeof hash!=='string'||!hash.startsWith('sha256-'))continue;
        if(name.includes('/')||name.includes('\\'))throw Error('Unexpected runtime resource path');
        const actual='sha256-'+createHash('sha256').update(readFileSync(join(framework,name))).digest('base64');
        if(actual!==hash)throw Error('Runtime integrity mismatch: '+name);
        checked++;
    }
}
if(checked<3)throw Error('No runtime resource hashes checked');
const current=new Set(Object.keys(boot.resources.fingerprinting));
for(const name of readdirSync(framework)){
    if(/\.(br|gz|symbols)$/.test(name))throw Error('Unneeded runtime sidecar: '+name);
    if(/\.[a-z0-9]{10}\.(wasm|js|dat)$/.test(name)&&!current.has(name))throw Error('Obsolete runtime binary: '+name);
}
const bytes=readdirSync(framework).reduce((sum,name)=>sum+statSync(join(framework,name)).size,0);
console.log(`PASS ${checked} boot resources match SHA-256; current runtime ${(bytes/1048576).toFixed(1)} MiB`);
