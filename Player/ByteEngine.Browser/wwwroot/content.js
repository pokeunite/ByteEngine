export async function loadContent(game, status) {
    const response = await fetch('./web-game.json', { cache: 'no-cache' });
    if (!response.ok) throw new Error('Missing web-game.json. Export the game again.');
    const manifest = await response.json();
    if (manifest.formatVersion !== 1 || !Array.isArray(manifest.files)) throw new Error('Unsupported web package.');
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
        const chunks = [];
        for (let at=0; at<data.length; at+=32768) chunks.push(String.fromCharCode(...data.subarray(at,at+32768)));
        game.MountFile(file.path, btoa(chunks.join('')));
        if (file.path.toLowerCase().endsWith('.wav')) audio.set(file.path, data.buffer);
    }
    return { manifest, audio };
}
