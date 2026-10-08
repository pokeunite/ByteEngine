import { dotnet } from './_framework/dotnet.js';
import { loadContent } from './content.js';
import { BrowserRenderer } from './renderer.js';
import { BrowserAudio } from './audio.js';
const status=document.querySelector('#status'),canvas=document.querySelector('#game'),overlay=document.querySelector('#ui');
const retry=document.querySelector("#retry");retry?.addEventListener("click",()=>location.reload());
let stopped=false;globalThis.byteEnginePerf={begin:performance.now(),frames:[]};
function fail(error){stopped=true;status.dataset.failed='true';status.hidden=false;status.style.pointerEvents='auto';if(retry)retry.hidden=false;status.textContent='ByteEngine browser error\n'+(error?.stack||error);console.error(error);}
window.addEventListener('error',e=>fail(e.error||e.message));
window.addEventListener('unhandledrejection',e=>fail(e.reason));
try{
    const renderer=new BrowserRenderer(canvas,overlay);
    globalThis.byteEngineGraphics=()=>{const gl=renderer.gl,ext=gl.getExtension('WEBGL_debug_renderer_info');return {renderer:gl.getParameter(ext?ext.UNMASKED_RENDERER_WEBGL:gl.RENDERER),error:gl.getError(),meshes:renderer.meshes.size,textures:renderer.textures.size,tintBytes:renderer.tintBytes};};
    canvas.addEventListener('webglcontextlost',e=>{e.preventDefault();fail('Graphics context lost. Reload this page.');});
    const runtimeReady=dotnet.create();
    const gameReady=runtimeReady.then(async runtime=>{const config=runtime.getConfig();const exports=await runtime.getAssemblyExports(config.mainAssemblyName);await runtime.runMain();return exports.BrowserGame;});
    const contentReady=loadContent(gameReady,status);
    const [runtime,game,content]=await Promise.all([runtimeReady,gameReady,contentReady]);
    renderer.readBuffer=index=>game.ReadBuffer(index);
    globalThis.byteEngineDebug=()=>JSON.parse(game.Diagnostics());
    const {manifest,audio:files}=content;byteEnginePerf.unpackEnd=performance.now();
    document.title=manifest.name||'ByteEngine Game';
    const audio=new BrowserAudio(files,id=>game.AudioEnded(id));
    await audio.decode();
    status.textContent=(manifest.demo?'Backend verification â€” W/A/S/D moves the cube.\n':'')+'Click to start';
    await new Promise(resolve=>{
        const start=()=>{canvas.focus();audio.activate().then(resolve).catch(fail);};
        canvas.addEventListener('pointerdown',start,{once:true});
        status.addEventListener('pointerdown',start,{once:true});
    });
    let saveKey=null,lastSave='';
    if(!manifest.demo){saveKey=game.SaveKey();try{const stored=localStorage.getItem(saveKey);if(stored)game.RestoreSaves(stored);}catch(error){console.warn('Browser save restore unavailable',error);}}
    status.textContent="Preparing workshop…";
    await new Promise(resolve=>requestAnimationFrame(()=>setTimeout(resolve,0)));
    const startMs=performance.now();game.Start(!!manifest.demo);byteEnginePerf.startMs=performance.now()-startMs;
    const persist=()=>{if(!saveKey)return;try{const data=game.SaveData();if(data!==lastSave){localStorage.setItem(saveKey,data);lastSave=data;}}catch(error){console.warn('Browser save persistence unavailable',error);}};
    setInterval(persist,5000);window.addEventListener('pagehide',persist);
    status.hidden=true;status.style.pointerEvents="none";
    const keys=new Set(),buttons=new Set(),keyPulses=new Set(),buttonPulses=new Set();
    let pointer=[.5,.5],delta=[0,0],wheel=0,previous=performance.now();
    const names={ArrowUp:'Up',ArrowDown:'Down',ArrowLeft:'Left',ArrowRight:'Right',Space:'Space',
        ShiftLeft:'LeftShift',ShiftRight:'RightShift',ControlLeft:'LeftControl',ControlRight:'RightControl',
        AltLeft:'LeftAlt',AltRight:'RightAlt',Escape:'Escape',Enter:'Enter',Tab:'Tab',
        Backspace:'Backspace',Delete:'Delete',Insert:'Insert',Home:'Home',End:'End',PageUp:'PageUp',PageDown:'PageDown'};
    function keyName(code){return names[code]||(code.startsWith('Key')?code.slice(3):code.startsWith('Digit')?'D'+code.slice(5):/^F\d+$/.test(code)?code:null);}
    canvas.addEventListener('keydown',e=>{
        const k=keyName(e.code);if(k){keys.add(k);if(!e.repeat)keyPulses.add(k);e.preventDefault();}
        if(e.code==='Escape')document.exitPointerLock();
    });
    window.addEventListener('keyup',e=>{const k=keyName(e.code);if(k)keys.delete(k);});
    function release(){keys.clear();buttons.clear();keyPulses.clear();buttonPulses.clear();delta=[0,0];wheel=0;previous=performance.now();}
    canvas.addEventListener('blur',release);document.addEventListener('visibilitychange',release);
    canvas.addEventListener('pointerdown',e=>{
        canvas.focus();audio.activate().catch(fail);
        const b=['Left','Middle','Right'][e.button];if(b){buttons.add(b);buttonPulses.add(b);}
        if(game.WantsPointerLock()&&document.pointerLockElement!==canvas)canvas.requestPointerLock()?.catch(()=>{});
    });
    window.addEventListener('pointerup',e=>buttons.delete(['Left','Middle','Right'][e.button]));
    canvas.addEventListener('contextmenu',e=>e.preventDefault());
    canvas.addEventListener('wheel',e=>{wheel-=Math.sign(e.deltaY);e.preventDefault();},{passive:false});
    canvas.addEventListener('pointermove',e=>{
        const rect=canvas.getBoundingClientRect();
        pointer=document.pointerLockElement===canvas?[.5,.5]:[(e.clientX-rect.left)/rect.width,(e.clientY-rect.top)/rect.height];
        delta[0]+=e.movementX;delta[1]+=e.movementY;
    });
    function frame(now){
        if(stopped)return;
        try{
            // Keep the browser framebuffer within the game's 720p budget on high-DPI displays.
            const scale=Math.min(devicePixelRatio,1280/Math.max(1,canvas.clientWidth),720/Math.max(1,canvas.clientHeight));
            const width=Math.max(1,Math.round(canvas.clientWidth*scale)),height=Math.max(1,Math.round(canvas.clientHeight*scale));
            if(canvas.width!==width||canvas.height!==height){canvas.width=overlay.width=width;canvas.height=overlay.height=height;}
            const pad=Array.from(navigator.getGamepads?.()||[]).find(p=>p?.connected);
            const gamepad=pad?JSON.stringify({axes:Array.from(pad.axes),buttons:pad.buttons.map(b=>b.value)}):'';
            const frameStart=performance.now(),intervalMs=now-previous;const data=JSON.parse(game.Frame(document.hidden?0:Math.max(0,Math.min(.1,(now-previous)/1000)),width,height,
                [...new Set([...keys,...keyPulses])].join(','),[...new Set([...buttons,...buttonPulses])].join(','),pointer[0],pointer[1],delta[0],delta[1],wheel,
                document.activeElement===canvas&&!document.hidden,document.pointerLockElement===canvas,gamepad));
            const managedEnd=performance.now();previous=now;delta=[0,0];wheel=0;keyPulses.clear();buttonPulses.clear();
            if(document.pointerLockElement===canvas&&!game.WantsPointerLock())document.exitPointerLock();
            globalThis.byteEngineLastFrame={pendingTextures:data.pendingTextures,draws:data.draws.length};
            audio.commands(data.audio);renderer.draw(data,width,height);
            const renderEnd=performance.now();
            byteEnginePerf.frames.push({frameMs:renderEnd-frameStart,managedMs:managedEnd-frameStart,renderMs:renderEnd-managedEnd,intervalMs,loopMs:data.loopMs,draws:data.draws.length,pending:data.pendingTextures});
            if(byteEnginePerf.frames.length>600)byteEnginePerf.frames.shift();
            if(data.pendingTextures>64){status.hidden=false;status.textContent='Preparing graphics... '+data.pendingTextures+' uploads';}else status.hidden=true;
            requestAnimationFrame(frame);
        }catch(error){fail(error);}
    }
    // Initial capture is requested from the next click (browser user-gesture requirement).
    requestAnimationFrame(frame);
}catch(error){fail(error);}
