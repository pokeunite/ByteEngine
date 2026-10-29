import { dotnet } from './_framework/dotnet.js';
import { loadContent } from './content.js';
import { BrowserRenderer } from './renderer.js';
import { decodeUiPacket } from './frame-wire.js';
import { BrowserAudio } from './audio.js';
import { BrowserVideos } from './video.js';
import { createPerformanceCapture } from './performance.js';
import { BrowserPresentation } from './graphics.js';
const capture=createPerformanceCapture();
const status=document.querySelector('#status'),canvas=document.querySelector('#game'),overlay=document.querySelector('#ui');
const progress=document.querySelector('#progress');
status.setProgress=(value,label)=>{if(status.dataset.failed)return;progress.hidden=false;if(value===null)progress.removeAttribute('value');else progress.value=Math.max(0,Math.min(1,value));status.textContent=label;};
const retry=document.querySelector("#retry");retry?.addEventListener("click",()=>location.reload());
let stopped=false;globalThis.byteEnginePerf={begin:performance.now(),frames:[]};
let readManagedError=()=>'';
function fail(error){
    if(stopped)return;
    let managed='';try{managed=readManagedError();}catch{}
    const detail=[managed,String(error?.stack||error)].filter(Boolean).join('\n');
    capture.failure({message:detail.slice(0,16000)});
    stopped=true;progress.hidden=true;status.dataset.failed='true';status.hidden=false;status.style.pointerEvents='auto';
    if(retry)retry.hidden=false;
    status.textContent='The game stopped unexpectedly. Save the error report and send it with what you were doing, then retry.';
    const saveReport=document.createElement('button');saveReport.textContent='Save error report';saveReport.style.cssText='margin:16px;padding:12px 24px;background:#2986c5;color:white;border:0;border-radius:4px;font:inherit;cursor:pointer';saveReport.onclick=()=>capture.save();status.append(document.createElement('br'),saveReport);
    const details=document.createElement('details'),label=document.createElement('summary'),text=document.createElement('pre');label.textContent='Technical details';text.textContent=detail;text.style.cssText='max-height:25vh;overflow:auto;text-align:left;white-space:pre-wrap;font-size:12px';details.append(label,text);status.append(details);console.error(detail);
}
window.addEventListener('error',e=>fail(e.error||e.message));
window.addEventListener('unhandledrejection',e=>fail(e.reason));
try{
    const renderer=new BrowserRenderer(canvas,overlay);
    const presentation=new BrowserPresentation();let graphics={quality:'Balanced',worldResolution:'Auto'};
    globalThis.byteEngineGraphics=()=>{const gl=renderer.gl,ext=gl.getExtension('WEBGL_debug_renderer_info');return {renderer:gl.getParameter(ext?ext.UNMASKED_RENDERER_WEBGL:gl.RENDERER),error:gl.getError(),meshes:renderer.meshes.size,textures:renderer.textures.size,tintBytes:renderer.tintBytes,gpuTimer:!!renderer.gpuTimer,limits:{maxTextureSize:gl.getParameter(gl.MAX_TEXTURE_SIZE),maxVertexAttributes:gl.getParameter(gl.MAX_VERTEX_ATTRIBS),maxTextureUnits:gl.getParameter(gl.MAX_TEXTURE_IMAGE_UNITS)},extensions:gl.getSupportedExtensions()};};
    canvas.addEventListener('webglcontextlost',e=>{e.preventDefault();fail('Graphics context lost. Reload this page.');});
    capture.setHardware(byteEngineGraphics());
    const runtimeReady=dotnet.create().then(runtime=>{capture.event('runtime-ready');return runtime;});
    const gameReady=runtimeReady.then(async runtime=>{const config=runtime.getConfig();const exports=await runtime.getAssemblyExports(config.mainAssemblyName);await runtime.runMain();return exports.BrowserGame;});
    const contentReady=loadContent(gameReady,status);
    const [runtime,game,content]=await Promise.all([runtimeReady,gameReady,contentReady]);
    readManagedError=()=>game.LastFrameError();
    renderer.readBuffer=index=>game.ReadBuffer(index);
    globalThis.byteEngineDebug=()=>JSON.parse(game.Diagnostics());
    const {manifest,audio:files}=content;byteEnginePerf.unpackEnd=performance.now();
    const previewQuality=new URLSearchParams(location.search).get('previewQuality');const exportGraphics={...(manifest.graphics||{})};if(['High','Balanced','Fast'].includes(previewQuality))exportGraphics.quality=previewQuality;game.ConfigurePresentation(JSON.stringify(exportGraphics));
    document.title=manifest.name||'ByteEngine Game';
    const audio=new BrowserAudio(files,id=>game.AudioEnded(id));
    const videos=new BrowserVideos(files,overlay,(id,seconds,paused,ended,error)=>game.VideoState(id,seconds,paused,ended,error));audio.videos=videos;
    capture.configure({profileComponents:value=>game.CaptureProfileComponents(value),readDetails:()=>JSON.parse(game.PerformanceDetails()),
        readInventory:()=>JSON.parse(game.PerformanceInventory()),build:{name:manifest.name,runtime:runtime.getConfig().mainAssemblyName},
        readBrowser:()=>({graphics:renderer.diagnostics(capture.deep),audio:{state:audio.context.state,sources:audio.sources.size,
            playing:[...audio.sources.values()].filter(s=>s.wantsPlay).length,decodedBuffers:audio.bufferBytes.size,
            decodedBytes:[...audio.bufferBytes.values()].reduce((a,b)=>a+b,0),streamingUrls:audio.urls.size,baseLatency:audio.context.baseLatency,outputLatency:audio.context.outputLatency}})});
    Promise.all(['browser-runtime-integrity.json','browser-plugin-builds.json'].map(path=>fetch(path).then(r=>r.ok?r.json():null)))
        .then(([integrity,plugins])=>capture.setBuildIdentity({bootSha256:integrity?.files?.['_framework/blazor.boot.json']??null,plugins})).catch(e=>capture.event('build-identity-unavailable',{message:String(e)}));
    status.setProgress(null,'Preparing audio…');
    const decodeStart=performance.now();await audio.decode();capture.event('audio-ready',{durationMs:performance.now()-decodeStart});
    if(stopped)throw new Error("Startup was interrupted; use Retry loading.");
    progress.hidden=true;
    status.textContent=(manifest.demo?'Backend verification â€” W/A/S/D moves the cube.\n':'')+'Click to start';
    await new Promise(resolve=>{
        const start=()=>{canvas.focus();audio.activate().then(resolve).catch(fail);};
        canvas.addEventListener('pointerdown',start,{once:true});
        status.addEventListener('pointerdown',start,{once:true});
    });
    let saveKey=null,lastSave='';
    if(!manifest.demo){saveKey=game.SaveKey();try{const stored=localStorage.getItem(saveKey);if(stored)game.RestoreSaves(stored);}catch(error){console.warn('Browser save restore unavailable',error);}}
    status.setProgress(null,'Preparing workshop…');
    await new Promise(resolve=>requestAnimationFrame(()=>setTimeout(resolve,0)));
    const startMs=performance.now();game.Start(!!manifest.demo);byteEnginePerf.startMs=performance.now()-startMs;capture.event('scene-start',{durationMs:byteEnginePerf.startMs});
    const persist=()=>{if(!saveKey)return;try{const data=game.SaveData();if(data!==lastSave){localStorage.setItem(saveKey,data);lastSave=data;}}catch(error){console.warn('Browser save persistence unavailable',error);}};
    setInterval(persist,5000);window.addEventListener('pagehide',persist);
    if(stopped)throw new Error("Startup failed; use Retry loading.");
    status.setProgress(null,'Preparing graphics…');
    const keys=new Set(),buttons=new Set(),keyPulses=new Set(),buttonPulses=new Set();
    let pointer=[.5,.5],delta=[0,0],wheel=0,previous=performance.now(),graphicsReady=false,previousPending=-1;
    let wasPointerLocked=false,pauseFromCapture=false;
    const names={ArrowUp:'Up',ArrowDown:'Down',ArrowLeft:'Left',ArrowRight:'Right',Space:'Space',
        ShiftLeft:'LeftShift',ShiftRight:'RightShift',ControlLeft:'LeftControl',ControlRight:'RightControl',
        AltLeft:'LeftAlt',AltRight:'RightAlt',Escape:'Escape',Enter:'Enter',Tab:'Tab',
        Backspace:'Backspace',Delete:'Delete',Insert:'Insert',Home:'Home',End:'End',PageUp:'PageUp',PageDown:'PageDown'};
    function keyName(code){return names[code]||(code.startsWith('Key')?code.slice(3):code.startsWith('Digit')?'D'+code.slice(5):/^F\d+$/.test(code)?code:null);}
    canvas.addEventListener('keydown',e=>{
        const k=keyName(e.code);if(k){keys.add(k);if(!e.repeat)keyPulses.add(k);e.preventDefault();}
        if(e.code==='Escape'){
            if(document.pointerLockElement===canvas)pauseFromCapture=true;
            document.exitPointerLock();
        }
    });
    window.addEventListener('keyup',e=>{const k=keyName(e.code);if(k)keys.delete(k);});
    function release(){game.CancelTouches();keys.clear();buttons.clear();keyPulses.clear();buttonPulses.clear();pauseFromCapture=false;delta=[0,0];wheel=0;previous=performance.now();}
    function touch(e,phase){const r=canvas.getBoundingClientRect();game.Touch(e.pointerId,(e.clientX-r.left)/r.width,(e.clientY-r.top)/r.height,phase);}
    canvas.style.touchAction='none';
    canvas.addEventListener('pointercancel',e=>{if(e.pointerType!=='mouse')touch(e,'Cancelled');});
    canvas.addEventListener('blur',release);document.addEventListener('visibilitychange',release);
    canvas.addEventListener('pointerdown',e=>{
        canvas.focus();audio.activate().catch(fail);
        if(e.pointerType!=='mouse'){canvas.setPointerCapture(e.pointerId);touch(e,'Started');e.preventDefault();return;}
        if(game.WantsPointerLock()&&document.pointerLockElement!==canvas)canvas.requestPointerLock()?.catch(()=>{});
    });
    // Pointer events only signal the first/last held mouse button. Mouse events
    // signal each transition, including left-fire while right-aim is held.
    canvas.addEventListener('mousedown',e=>{const b=['Left','Middle','Right'][e.button];if(b){buttons.add(b);buttonPulses.add(b);}e.preventDefault();});
    window.addEventListener('mouseup',e=>buttons.delete(['Left','Middle','Right'][e.button]));
    window.addEventListener('pointerup',e=>{if(e.pointerType!=='mouse')touch(e,'Ended');});
    document.addEventListener('pointerlockchange',()=>{
        const locked=document.pointerLockElement===canvas;
        // Escape is reserved by the browser while locked and may produce no
        // keydown. Forward one pause press with the capture state it began in.
        if(wasPointerLocked&&!locked&&game.WantsPointerLock()&&!document.hidden&&document.hasFocus()){
            keyPulses.add('Escape');pauseFromCapture=true;buttons.clear();buttonPulses.clear();delta=[0,0];
        }
        wasPointerLocked=locked;
    });
    canvas.addEventListener('contextmenu',e=>e.preventDefault());
    canvas.addEventListener('wheel',e=>{wheel-=Math.sign(e.deltaY);e.preventDefault();},{passive:false});
    canvas.addEventListener('pointermove',e=>{
        if(e.pointerType!=='mouse'){if(e.pressure>0)touch(e,'Held');return;}
        const rect=canvas.getBoundingClientRect();
        pointer=document.pointerLockElement===canvas?[.5,.5]:[(e.clientX-rect.left)/rect.width,(e.clientY-rect.top)/rect.height];
        delta[0]+=e.movementX;delta[1]+=e.movementY;
    });
    const uiCommands=[];
    function frame(now){
        if(stopped){status.hidden=false;return;}
        try{
            const size=presentation.dimensions(canvas.clientWidth,canvas.clientHeight,devicePixelRatio,graphics.quality,graphics.worldResolution,renderer.gl.getParameter(renderer.gl.MAX_TEXTURE_SIZE));
            const {width,height,worldWidth,worldHeight,uiWidth,uiHeight}=size;
            if(canvas.width!==worldWidth||canvas.height!==worldHeight){canvas.width=worldWidth;canvas.height=worldHeight;}
            if(overlay.width!==uiWidth||overlay.height!==uiHeight){overlay.width=uiWidth;overlay.height=uiHeight;}
            const pad=Array.from(navigator.getGamepads?.()||[]).find(p=>p?.connected);
            const gamepad=pad?JSON.stringify({axes:Array.from(pad.axes),buttons:pad.buttons.map(b=>b.value)}):'';
            const deepProfile=capture.beforeFrame();
            const frameStart=performance.now(),intervalMs=now-previous;const payload=game.Frame(document.hidden?0:Math.max(0,Math.min(.1,(now-previous)/1000)),width,height,
                [...new Set([...keys,...keyPulses])].join(','),[...new Set([...buttons,...buttonPulses])].join(','),pointer[0],pointer[1],delta[0],delta[1],wheel,
                document.activeElement===canvas&&!document.hidden,document.pointerLockElement===canvas||pauseFromCapture,gamepad);
            const wasmEnd=performance.now();const data=JSON.parse(payload);
            graphics={quality:data.quality||'Balanced',worldResolution:data.worldResolution||'Auto'};
            if(data.uiBuffer>=0)data.ui=decodeUiPacket(game.ReadBuffer(data.uiBuffer),data.uiCount,uiCommands);
            const managedEnd=performance.now();previous=now;delta=[0,0];wheel=0;keyPulses.clear();buttonPulses.clear();pauseFromCapture=false;
            if(document.pointerLockElement===canvas&&!game.WantsPointerLock())document.exitPointerLock();
            globalThis.byteEngineLastFrame={pendingTextures:data.pendingTextures,draws:data.draws.length,skyTexture:data.sky?.texture||0,hdrSky:data.sky?.hdr||false};
            const audioStart=performance.now();audio.commands(data.audio);const audioEnd=performance.now();
            videos.update();const videoEnd=performance.now();
            for(const [id,state] of audio.sources){const seconds=state.media?state.media.currentTime:state.offset+(state.node?(audio.context.currentTime-state.started)*state.pitch:0);game.AudioPosition(id,seconds);}
            const drawCount=data.draws.length,uiCount=data.ui.length,drawStart=performance.now();
            if(capture.probes.skipGeometry)data.draws=[];if(capture.probes.skipUi)data.ui=[];
            renderer.draw(data,width,height);
            presentation.sample(renderer.profile.gpuMs,now,graphics.worldResolution,data.pendingTextures);
            globalThis.byteEnginePresentation={...size,...graphics,adaptiveScale:presentation.factor};
            const renderEnd=performance.now();
            const allocations=data.textures.filter(t=>t.allocate||!t.partial);
            if(allocations.length||renderer.profile.uploadMs>8)capture.event('graphics-upload',{durationMs:renderer.profile.uploadMs,meshBytes:renderer.profile.meshUploadBytes,textureBytes:renderer.profile.textureUploadBytes,allocations:allocations.map(t=>({id:t.id,source:t.source,width:t.width,height:t.height,hdr:t.hdr}))});
            byteEnginePerf.frames.push({frameMs:renderEnd-frameStart,managedMs:managedEnd-frameStart,renderMs:renderEnd-managedEnd,intervalMs,loopMs:data.loopMs,draws:data.draws.length,pending:data.pendingTextures});
            if(byteEnginePerf.frames.length>600)byteEnginePerf.frames.shift();
            capture.frame({startMs:frameStart,deep:deepProfile,frameMs:renderEnd-frameStart,managedMs:wasmEnd-frameStart,parseMs:managedEnd-wasmEnd,audioMs:audioEnd-audioStart,
                videoMs:videoEnd-audioEnd,audioPositionMs:drawStart-videoEnd,renderMs:renderEnd-drawStart,intervalMs,loopMs:data.loopMs,payloadChars:payload.length,uiBytes:uiCount*56,
                draws:drawCount,uiCommands:uiCount,audioCommands:data.audio.length,pending:data.pendingTextures,width,height,worldWidth,worldHeight,uiWidth,uiHeight,
                directionalLights:data.lights.length,pointLights:data.points.length,shadowResolution:data.shadow?.resolution??0,
                sceneMetrics:data.sceneMetrics,inputMs:data.inputMs,updateStepMs:data.updateStepMs,renderPreparationMs:data.renderPreparationMs,renderAllocatedBytes:data.renderAllocatedBytes,
                focused:document.activeElement===canvas,captured:document.pointerLockElement===canvas,quality:graphics.quality,worldResolution:graphics.worldResolution,adaptiveScale:presentation.factor,...renderer.profile},()=>JSON.parse(game.Diagnostics()));
            if(data.pendingTextures!==previousPending){capture.event('graphics-queue',{pending:data.pendingTextures,reason:graphicsReady?'background asset uploads':'initial graphics'});previousPending=data.pendingTextures;}
            if(!graphicsReady&&data.pendingTextures===0){graphicsReady=true;capture.event('first-ready-frame');}
            // A background upload must not replace gameplay with the startup splash.
            status.hidden=graphicsReady;status.style.pointerEvents="none";
            if(!graphicsReady)status.textContent='Preparing graphics... '+data.pendingTextures+' uploads';
            requestAnimationFrame(frame);
        }catch(error){fail(error);}
    }
    // Initial capture is requested from the next click (browser user-gesture requirement).
    requestAnimationFrame(frame);
}catch(error){fail(error);}
