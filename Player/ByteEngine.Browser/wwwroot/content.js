export async function loadContent(game, status) {
    const response = await fetch('./web-game.json', { cache: 'no-cache' });
    if (!response.ok) throw new Error('Missing web-game.json. Export the game again.');
    const manifest = await response.json();
    if (![1,2,3].includes(manifest.formatVersion) || !Array.isArray(manifest.files)) throw new Error('Unsupported web package.');
    const audio = new Map();
    if (manifest.formatVersion === 3 && (!Number.isSafeInteger(manifest.packageSize) || manifest.packageSize < 8 || manifest.packageSize > 500*1024*1024)) throw new Error('Invalid package size.');
    const packageBytes = manifest.formatVersion === 3 ? new Uint8Array(manifest.packageSize) : null;
    let index = 0, offset = 0;
    for (const file of manifest.files) {
        if (typeof file.path !== 'string' || file.path.startsWith('/') || file.path.includes('\\') ||
            file.path.split('/').some(p => !p || p === '.' || p === '..')) throw new Error('Unsafe package path.');
        const data = await fetchContent(file,status,++index,manifest.files.length);
        if (packageBytes) {
            if(offset+data.length>packageBytes.length)throw new Error('Package chunks exceed declared size.');
            packageBytes.set(data,offset);offset+=data.length;
        } else if (manifest.formatVersion === 2) {
            if (file.path !== 'Game.bytepak') throw new Error('Unsupported asset package path.');
            await mountPackage(game, data, audio, status);
        } else {
            game.MountFile(file.path, toBase64(data));
            if (file.path.toLowerCase().endsWith('.wav')) audio.set(file.path, data.buffer);
        }
    }
    if(packageBytes){
        if(offset!==packageBytes.length)throw new Error('Incomplete package chunks.');
        if(await digest(packageBytes)!==manifest.packageSha256.toLowerCase())throw new Error('Asset package integrity mismatch.');
        await mountPackage(game,packageBytes,audio,status);
    }
    return { manifest, audio };
}

async function digest(data){return Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',data))).map(n=>n.toString(16).padStart(2,'0')).join('');}
async function fetchContent(file,status,index,count){
    if(!Number.isSafeInteger(file.size)||file.size<0||file.size>500*1024*1024)throw new Error('Invalid content size.');
    const url='./'+file.path.split('/').map(encodeURIComponent).join('/');
    let lastError;
    for(let attempt=1;attempt<=3;attempt++){
        const controller=new AbortController();let timer=setTimeout(()=>controller.abort(),30000);
        try{
            const response=await fetch(url,{signal:controller.signal,cache:attempt>1?'reload':'default'});
            if(!response.ok)throw new Error('HTTP '+response.status);
            const data=new Uint8Array(file.size);let received=0;
            if(response.body){const reader=response.body.getReader();while(true){const {done,value}=await reader.read();if(done)break;if(received+value.length>data.length)throw new Error('Response exceeds declared size');data.set(value,received);received+=value.length;clearTimeout(timer);timer=setTimeout(()=>controller.abort(),30000);status.textContent='Loading '+index+'/'+count+' / '+Math.floor(received/Math.max(1,file.size)*100)+'%'+(attempt>1?' / retry '+attempt:'');}}
            else{const bytes=new Uint8Array(await response.arrayBuffer());if(bytes.length!==data.length)throw new Error('Incomplete download');data.set(bytes);received=bytes.length;}
            if(received!==file.size)throw new Error('Incomplete download: '+received+'/'+file.size+' bytes');
            if(await digest(data)!==file.sha256.toLowerCase())throw new Error('Content integrity mismatch');
            return data;
        }catch(error){lastError=error;controller.abort();if(attempt<3){status.textContent='Retrying download '+index+'/'+count;await new Promise(resolve=>setTimeout(resolve,attempt*500));}}
        finally{clearTimeout(timer);}
    }
    throw new Error('Cannot download '+file.path+' after 3 attempts: '+lastError+'. Please reload to retry.');
}

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
        game.MountFile(path,toBase64(data));
        if(path.toLowerCase().endsWith('.wav'))audio.set(path,data.buffer);
    }
    if(offset!==bytes.length)throw new Error('Unexpected package trailing data.');
}
