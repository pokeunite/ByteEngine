export async function loadContent(game, status) {
    const response = await fetch('./web-game.json', { cache: 'no-cache' });
    if (!response.ok) throw new Error('Missing web-game.json. Export the game again.');
    const manifest = await response.json();
    if (![1,2].includes(manifest.formatVersion) || !Array.isArray(manifest.files)) throw new Error('Unsupported web package.');
    const audio = new Map();
    let index = 0;
    for (const file of manifest.files) {
        if (typeof file.path !== 'string' || file.path.startsWith('/') || file.path.includes('\\') ||
            file.path.split('/').some(p => !p || p === '.' || p === '..')) throw new Error('Unsafe package path.');
        status.textContent = 'Loading ' + (++index) + '/' + manifest.files.length + ': ' + file.path;
        const url = './' + file.path.split('/').map(encodeURIComponent).join('/');
        const result = await fetch(url);
        if (!result.ok) throw new Error('Missing content: ' + file.path);
        const data = new Uint8Array(await result.arrayBuffer());
        if (data.length !== file.size) throw new Error('Content size mismatch: ' + file.path);
        const hash = Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', data)))
            .map(n => n.toString(16).padStart(2,'0')).join('');
        if (hash !== file.sha256.toLowerCase()) throw new Error('Content integrity mismatch: ' + file.path);
        if (manifest.formatVersion === 2) {
            if (file.path !== 'Game.bytepak') throw new Error('Unsupported asset package path.');
            await mountPackage(game, data, audio, status);
            continue;
        }
        game.MountFile(file.path, toBase64(data));
        if (file.path.toLowerCase().endsWith('.wav')) audio.set(file.path, data.buffer);
    }
    return { manifest, audio };
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
        const data=new Uint8Array(await new Response(stream).arrayBuffer());
        if(data.length!==length)throw new Error('Asset size mismatch: '+path);
        const actual=new Uint8Array(await crypto.subtle.digest('SHA-256',data));
        if(actual.some((value,i)=>value!==expected[i]))throw new Error('Corrupted asset: '+path);
        game.MountFile(path,toBase64(data));
        if(path.toLowerCase().endsWith('.wav'))audio.set(path,data.buffer);
    }
    if(offset!==bytes.length)throw new Error('Unexpected package trailing data.');
}
