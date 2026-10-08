export async function loadContent(game, status) {
    const mountedGame=Promise.resolve(game);
    const cache=await openContentCache();
    const response = await fetch('./web-game.json', { cache: 'no-cache' });
    if (!response.ok) throw new Error('Missing web-game.json. Export the game again.');
    const manifest = await response.json();
    if (![1,2,3].includes(manifest.formatVersion) || !Array.isArray(manifest.files)) throw new Error('Unsupported web package.');
    const audio = new Map();
    if (manifest.formatVersion === 3 && (!Number.isSafeInteger(manifest.packageSize) || manifest.packageSize < 8 || manifest.packageSize > 500*1024*1024)) throw new Error('Invalid package size.');
    const packageBytes = manifest.formatVersion === 3 ? new Uint8Array(manifest.packageSize) : null;
    let index = 0, offset = 0;
    const progress=new Map();
    const update=(path,loaded)=>{if(status.dataset?.failed)return;progress.set(path,loaded);const total=manifest.files.reduce((sum,file)=>sum+file.size,0),done=[...progress.values()].reduce((sum,n)=>sum+n,0);status.textContent=`Downloading game / ${Math.floor(done/Math.max(1,total)*100)}% / ${(done/1048576).toFixed(1)} MB of ${(total/1048576).toFixed(1)} MB`;};
    for(const file of manifest.files)if(typeof file.path !== 'string' || file.path.startsWith('/') || file.path.includes('\\') || file.path.includes(':') || file.path.split('/').some(p=>!p||p==='.'||p==='..'))throw new Error('Unsafe package path.');
    const pending=new Map();
    const prefetch=i=>{if(i<manifest.files.length){const task=fetchContent(manifest.files[i],status,i+1,manifest.files.length,cache,update);task.catch(()=>{});pending.set(i,task);}};
    if(packageBytes)for(let i=0;i<Math.min(3,manifest.files.length);i++)prefetch(i);
    for (const file of manifest.files) {
        if (typeof file.path !== 'string' || file.path.startsWith('/') || file.path.includes('\\') ||
            file.path.split('/').some(p => !p || p === '.' || p === '..')) throw new Error('Unsafe package path.');
        const data = packageBytes?await pending.get(index):await fetchContent(file,status,index+1,manifest.files.length,cache,update);
        if(packageBytes){pending.delete(index);prefetch(index+3);}index++;
        if (packageBytes) {
            if(offset+data.length>packageBytes.length)throw new Error('Package chunks exceed declared size.');
            packageBytes.set(data,offset);offset+=data.length;
        } else if (manifest.formatVersion === 2) {
            if (file.path !== 'Game.bytepak') throw new Error('Unsupported asset package path.');
            await mountPackage(await mountedGame, data, audio, status);
        } else {
            mountFile(await mountedGame,file.path,data);
            if (file.path.toLowerCase().endsWith('.wav')) audio.set(file.path, data.buffer);
        }
    }
    if(packageBytes){
        if(offset!==packageBytes.length)throw new Error('Incomplete package chunks.');
        if(await digest(packageBytes)!==manifest.packageSha256.toLowerCase())throw new Error('Asset package integrity mismatch.');
        await mountPackage(await mountedGame,packageBytes,audio,status);
    }
    return { manifest, audio };
}

async function digest(data){return Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',data))).map(n=>n.toString(16).padStart(2,'0')).join('');}
// Inspired by Godot's tracked streaming preloader: runtime/data downloads overlap,
// progress is aggregated, and network attempts do not restart the complete startup.
async function openContentCache(){try{return globalThis.caches?await caches.open('byteengine-verified-content-v1'):null;}catch{return null;}}
function cacheKey(file){return new URL('./.byteengine-cache/'+file.sha256.toLowerCase(),globalThis.location?.href||'https://byteengine.invalid/').href;}
async function cachedContent(cache,file){
    if(!cache)return null;
    try{const response=await cache.match(cacheKey(file));if(!response)return null;const data=new Uint8Array(await response.arrayBuffer());if(data.length===file.size&&await digest(data)===file.sha256.toLowerCase())return data;await cache.delete(cacheKey(file));}catch{}
    return null;
}
async function rememberContent(cache,file,data){
    if(!cache)return;
    try{
        await cache.put(cacheKey(file),new Response(data,{headers:{'X-ByteEngine-Size':String(data.length),'X-ByteEngine-Cached':String(Date.now())}}));
        const entries=await Promise.all((await cache.keys()).map(async key=>{const response=await cache.match(key);return {key,size:Number(response?.headers.get('X-ByteEngine-Size')||0),age:Number(response?.headers.get('X-ByteEngine-Cached')||0)};}));
        let total=entries.reduce((sum,e)=>sum+e.size,0);for(const entry of entries.sort((a,b)=>a.age-b.age)){if(total<=256*1048576)break;await cache.delete(entry.key);total-=entry.size;}
    }catch{} // Storage restrictions/quota must never prevent playing.
}
async function fetchContent(file,status,index,count,cache,update){
    if(!Number.isSafeInteger(file.size)||file.size<0||file.size>500*1024*1024||!/^[0-9a-f]{64}$/i.test(file.sha256))throw new Error('Invalid content metadata.');
    const stored=await cachedContent(cache,file);if(stored){update(file.path,stored.length);return stored;}
    const url='./'+file.path.split('/').map(encodeURIComponent).join('/');
    const data=new Uint8Array(file.size);let received=0,lastError;
    for(let attempt=1;attempt<=4;attempt++){
        const controller=new AbortController();let timer;
        const watch=()=>{clearTimeout(timer);timer=setTimeout(()=>{if(globalThis.document?.hidden){watch();return;}controller.abort(new Error('No download progress for 120 seconds'));},120000);};
        try{
            watch();
            const requested=received;
            const response=await fetch(url,{signal:controller.signal,cache:attempt>1?'reload':'default',headers:requested?{Range:`bytes=${requested}-`}:undefined});
            if(!response.ok)throw new Error('HTTP '+response.status);
            if(requested&&response.status===206){const match=/^bytes (\d+)-(\d+)\/(\d+)$/.exec(response.headers.get('Content-Range')||'');if(!match||Number(match[1])!==requested||Number(match[3])!==file.size)throw new Error('Invalid resumed response');}
            else received=0; // Servers which ignore Range return the complete file.
            watch();
            if(response.body){const reader=response.body.getReader();try{while(true){const {done,value}=await reader.read();if(done)break;if(received+value.length>data.length)throw new Error('Response exceeds declared size');data.set(value,received);received+=value.length;watch();update(file.path,received);}}finally{reader.releaseLock();}}
            else{const bytes=new Uint8Array(await response.arrayBuffer());if(received+bytes.length>data.length)throw new Error('Response exceeds declared size');data.set(bytes,received);received+=bytes.length;update(file.path,received);}
            if(received!==file.size)throw new Error('Incomplete download: '+received+'/'+file.size+' bytes');
            if(await digest(data)!==file.sha256.toLowerCase()){received=0;throw new Error('Content integrity mismatch');}
            await rememberContent(cache,file,data);return data;
        }catch(error){lastError=error;controller.abort();if(received===file.size)received=0;if(attempt<4){if(!status.dataset?.failed)status.textContent=`Reconnecting download ${index}/${count} / keeping completed downloads`;await new Promise(resolve=>setTimeout(resolve,1000));}}
        finally{clearTimeout(timer);}
    }
    throw new Error('Cannot download '+file.path+' after 4 attempts: '+lastError+'. Reload to resume verified downloads.');
}

function mountFile(game,path,data){if(game.MountBytes)game.MountBytes(path,data);else game.MountFile(path,toBase64(data));}

function toBase64(data) {
    const chunks = [];
    for (let at=0; at<data.length; at+=32768) chunks.push(String.fromCharCode(...data.subarray(at,at+32768)));
    return btoa(chunks.join(''));
}

async function mountPackage(game, bytes, audio, status) {
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    const decoder = new TextDecoder('utf-8', { fatal: true });
    let offset = 0;
    function take(length) {
        if (!Number.isSafeInteger(length) || length < 0 || offset + length > bytes.length) throw new Error('Truncated asset package.');
        const data = bytes.subarray(offset, offset + length); offset += length; return data;
    }
    function integer() { const data=take(4); return new DataView(data.buffer,data.byteOffset,4).getInt32(0,true); }
    function size() {
        const data=take(8), value=new DataView(data.buffer,data.byteOffset,8).getBigInt64(0,true);
        if(value<0n || value>1073741824n)throw new Error('Invalid asset package size.');
        return Number(value);
    }
    function pathString() {
        let length=0, shift=0;
        for(let i=0;i<5;i++) {
            const value=take(1)[0]; length += (value & 127) * 2**shift;
            if(!(value & 128)) {
                if(length>4096)throw new Error('Asset package path too long.');
                return decoder.decode(take(length));
            }
            shift+=7;
        }
        throw new Error('Invalid asset package path.');
    }
    if(decoder.decode(take(8))!=='BYTEPAK2')throw new Error('Unsupported ByteEngine asset package.');
    const count=integer(), seen=new Set();
    if(count<1 || count>100000)throw new Error('Invalid asset package entry count.');
    let total=0;
    for(let index=0;index<count;index++) {
        const path=pathString();
        if(path.startsWith('/') || path.includes('\\') || path.includes(':') ||
            path.split('/').some(part=>!part || part==='.' || part==='..') || seen.has(path.toLowerCase()))
            throw new Error('Unsafe or duplicate asset package path.');
        seen.add(path.toLowerCase());
        const length=size(), expected=take(32), compressed=take(size());
        total+=length;if(total>4294967296)throw new Error('Asset package too large.');
        status.textContent='Unpacking assets '+(index+1)+'/'+count;
        const stream=new Blob([compressed]).stream().pipeThrough(new DecompressionStream('deflate'));
        let data;try{data=length===0&&compressed.length===0?new Uint8Array():new Uint8Array(await new Response(stream).arrayBuffer());}catch(error){throw new Error("Unpacking "+path+": "+error);}
        if(data.length!==length)throw new Error('Asset size mismatch: '+path);
        const actual=new Uint8Array(await crypto.subtle.digest('SHA-256',data));
        if(actual.some((value,i)=>value!==expected[i]))throw new Error('Corrupted asset: '+path);
        mountFile(game,path,data);
        if(path.toLowerCase().endsWith('.wav'))audio.set(path,data.buffer);
    }
    if(offset!==bytes.length)throw new Error('Unexpected package trailing data.');
}
