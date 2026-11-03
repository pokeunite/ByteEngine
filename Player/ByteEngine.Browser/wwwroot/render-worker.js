import {BrowserRenderer} from './renderer.js';
import {decodeUiPacket} from './frame-wire.js';
import {packedFrameReader} from './packed-frame.js';
let renderer,world,overlay;
function hardware(gl){const ext=gl.getExtension('WEBGL_debug_renderer_info');return {renderer:gl.getParameter(ext?ext.UNMASKED_RENDERER_WEBGL:gl.RENDERER),error:gl.getError(),limits:{maxTextureSize:gl.getParameter(gl.MAX_TEXTURE_SIZE),maxVertexAttributes:gl.getParameter(gl.MAX_VERTEX_ATTRIBS),maxTextureUnits:gl.getParameter(gl.MAX_TEXTURE_IMAGE_UNITS)},extensions:gl.getSupportedExtensions()};}
// Messages are FIFO. Each frame owns its copied buffers until GL has consumed
// them. No frame is dropped: resource uploads may only occur in that frame.
onmessage=e=>{const m=e.data;try{
    if(m.kind==='probe'){const canvas=new OffscreenCanvas(1,1),gl=canvas.getContext('webgl2');if(!gl)throw Error('Worker WebGL 2 unavailable');const info=hardware(gl);gl.getExtension('WEBGL_lose_context')?.loseContext();postMessage({kind:'probe',hardware:info});}
    else if(m.kind==='init'){
        world=m.world;overlay=m.overlay;
        world.addEventListener('webglcontextlost',e=>{e.preventDefault();postMessage({kind:'error',message:'Graphics context lost. Reload this page.'});});
        renderer=new BrowserRenderer(world,overlay);postMessage({kind:'ready',maximumSkinningBones:renderer.maximumSkinningBones,hardware:hardware(renderer.gl)});
    }else if(m.kind==='frame'){
        for(const [canvas,w,h] of [[world,m.size.worldWidth,m.size.worldHeight],[overlay,m.size.uiWidth,m.size.uiHeight]]){if(canvas.width!==w)canvas.width=w;if(canvas.height!==h)canvas.height=h;}
        const frame=m.frame;renderer.readBuffer=packedFrameReader(m.packed,m.ranges);frame.ui=decodeUiPacket(renderer.readBuffer(frame.uiBuffer),frame.uiCount);
        const start=performance.now();renderer.draw(frame,m.width,m.height);const finish=performance.now();
        let comparison;
        if(m.compare){
            const gl=renderer.gl,width=world.width,height=world.height,a=new Uint8Array(width*height*4),b=new Uint8Array(a.length);
            gl.readPixels(0,0,width,height,gl.RGBA,gl.UNSIGNED_BYTE,a);
            const transparent=frame.draws.filter(d=>{const material=d.material===undefined?d:renderer.materials.get(d.material);return material?.unlit&&(material.blend==='Additive'||material.blend==='AlphaBlend');});
            renderer.draw({...frame,draws:frame.draws.filter(d=>!transparent.includes(d))},m.width,m.height);gl.readPixels(0,0,width,height,gl.RGBA,gl.UNSIGNED_BYTE,b);renderer.draw(frame,m.width,m.height);
            let changedPixels=0;for(let i=0;i<a.length;i+=4)if(Math.abs(a[i]-b[i])+Math.abs(a[i+1]-b[i+1])+Math.abs(a[i+2]-b[i+2])>3)changedPixels++;
            comparison={draws:transparent.length,changedPixels};
        }
        postMessage({kind:'frame',id:m.id,finishMs:performance.timeOrigin+finish,renderMs:finish-start,profile:renderer.profile,comparison,diagnostics:m.report?renderer.diagnostics(m.deep):undefined,packed:m.packed},[m.packed]);
    }else if(m.kind==='report')postMessage({kind:'report',diagnostics:renderer.diagnostics(m.deep),error:renderer.gl.getError()});
}catch(error){postMessage({kind:'error',message:String(error.stack||error)});}};
