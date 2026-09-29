import { dotnet } from './_framework/dotnet.js';
import { loadContent } from './content.js';
import { BrowserRenderer } from './renderer.js';
import { BrowserAudio } from './audio.js';
const status=document.querySelector('#status'),canvas=document.querySelector('#game'),overlay=document.querySelector('#ui');
let stopped=false;
function fail(error){stopped=true;status.hidden=false;status.textContent='ByteEngine browser error\n'+(error?.stack||error);console.error(error);}
window.addEventListener('error',e=>fail(e.error||e.message));
window.addEventListener('unhandledrejection',e=>fail(e.reason));
try{
    const renderer=new BrowserRenderer(canvas,overlay);
    canvas.addEventListener('webglcontextlost',e=>{e.preventDefault();fail('Graphics context lost. Reload this page.');});
    const runtime=await dotnet.create(),config=runtime.getConfig();
    const exports=await runtime.getAssemblyExports(config.mainAssemblyName);
    await runtime.runMain();
    const game=exports.BrowserGame;
    const {manifest,audio:files}=await loadContent(game,status);
    document.title=manifest.name||'ByteEngine Game';
    const audio=new BrowserAudio(files,id=>game.AudioEnded(id));
    await audio.decode();
    status.textContent=(manifest.demo?'Backend verification — W/A/S/D moves the cube.\n':'')+'Click to start';
    await new Promise(resolve=>{
        const start=()=>{canvas.focus();audio.activate().then(resolve).catch(fail);};
        canvas.addEventListener('pointerdown',start,{once:true});
        status.addEventListener('pointerdown',start,{once:true});
    });
    game.Start(!!manifest.demo);
    status.hidden=true;
    const keys=new Set(),buttons=new Set();
    let pointer=[.5,.5],delta=[0,0],wheel=0,previous=performance.now();
    const names={ArrowUp:'Up',ArrowDown:'Down',ArrowLeft:'Left',ArrowRight:'Right',Space:'Space',
        ShiftLeft:'LeftShift',ShiftRight:'RightShift',ControlLeft:'LeftControl',ControlRight:'RightControl',
        AltLeft:'LeftAlt',AltRight:'RightAlt',Escape:'Escape',Enter:'Enter',Tab:'Tab',
        Backspace:'Backspace',Delete:'Delete',Insert:'Insert',Home:'Home',End:'End',PageUp:'PageUp',PageDown:'PageDown'};
    function keyName(code){return names[code]||(code.startsWith('Key')?code.slice(3):code.startsWith('Digit')?'D'+code.slice(5):/^F\d+$/.test(code)?code:null);}
    canvas.addEventListener('keydown',e=>{
        const k=keyName(e.code);if(k){keys.add(k);e.preventDefault();}
        if(e.code==='Escape')document.exitPointerLock();
    });
    window.addEventListener('keyup',e=>{const k=keyName(e.code);if(k)keys.delete(k);});
    function release(){keys.clear();buttons.clear();delta=[0,0];wheel=0;previous=performance.now();}
    canvas.addEventListener('blur',release);document.addEventListener('visibilitychange',release);
    canvas.addEventListener('pointerdown',e=>{
        canvas.focus();audio.activate().catch(fail);
        const b=['Left','Middle','Right'][e.button];if(b)buttons.add(b);
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
            const width=Math.max(1,Math.round(canvas.clientWidth*devicePixelRatio)),height=Math.max(1,Math.round(canvas.clientHeight*devicePixelRatio));
            if(canvas.width!==width||canvas.height!==height){canvas.width=overlay.width=width;canvas.height=overlay.height=height;}
            const pad=Array.from(navigator.getGamepads?.()||[]).find(p=>p?.connected);
            const gamepad=pad?JSON.stringify({axes:Array.from(pad.axes),buttons:pad.buttons.map(b=>b.value)}):'';
            const data=JSON.parse(game.Frame(document.hidden?0:(now-previous)/1000,width,height,
                [...keys].join(','),[...buttons].join(','),pointer[0],pointer[1],delta[0],delta[1],wheel,
                document.activeElement===canvas&&!document.hidden,document.pointerLockElement===canvas,gamepad));
            previous=now;delta=[0,0];wheel=0;
            if(document.pointerLockElement===canvas&&!game.WantsPointerLock())document.exitPointerLock();
            audio.commands(data.audio);renderer.draw(data,width,height);
            requestAnimationFrame(frame);
        }catch(error){fail(error);}
    }
    // Initial capture is requested from the next click (browser user-gesture requirement).
    requestAnimationFrame(frame);
}catch(error){fail(error);}
