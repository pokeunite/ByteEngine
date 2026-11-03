import {BrowserRenderer} from './renderer.js';
import {PackedFramePool} from './packed-frame.js';
function directRenderer(world,overlay){
    const renderer=new BrowserRenderer(world,overlay),gl=renderer.gl,ext=gl.getExtension('WEBGL_debug_renderer_info');
    renderer.isThreaded=false;renderer.canAcceptFrame=true;
    renderer.hardware={renderer:gl.getParameter(ext?ext.UNMASKED_RENDERER_WEBGL:gl.RENDERER),error:gl.getError(),limits:{maxTextureSize:gl.getParameter(gl.MAX_TEXTURE_SIZE),maxVertexAttributes:gl.getParameter(gl.MAX_VERTEX_ATTRIBS),maxTextureUnits:gl.getParameter(gl.MAX_TEXTURE_IMAGE_UNITS)},extensions:gl.getSupportedExtensions()};
    renderer.setSize=size=>{if(world.width!==size.worldWidth)world.width=size.worldWidth;if(world.height!==size.worldHeight)world.height=size.worldHeight;if(overlay.width!==size.uiWidth)overlay.width=size.uiWidth;if(overlay.height!==size.uiHeight)overlay.height=size.uiHeight;};
    return renderer;
}
export async function createBrowserRenderer(world,overlay,{forceMain=false,onError=()=>{}}={}){
    if(forceMain||typeof Worker==='undefined'||!world.transferControlToOffscreen||!overlay.transferControlToOffscreen)return directRenderer(world,overlay);
    let worker;const waits=new Map(),pool=new PackedFramePool();let error=null,inFlight=0,nextId=0,transferred=false,lastFinish=null;
    const result={isThreaded:true,profile:{gpuMs:null},hardware:null,maximumSkinningBones:0,onavailable:null,readBuffer:null,size:null,completedFrames:0,lastCompletion:null,cachedDiagnostics:{},wantDeep:false,
        get canAcceptFrame(){if(error)throw error;return inFlight<2;},
        setSize(size){this.size=size;},
        draw(frame,width,height){
            if(!this.canAcceptFrame)throw Error('Render queue is full');
            const packet=pool.pack(frame,this.readBuffer),id=++nextId,compare=this.comparisonRequested;this.submittedFrameId=id;this.comparisonRequested=false;inFlight++;
            worker.postMessage({kind:'frame',id,frame:{...frame,bufferViews:undefined,ui:undefined,audio:undefined},width,height,size:this.size,compare,report:id===1||id%120===0||this.wantDeep,deep:this.wantDeep,...packet},[packet.packed]);
            this.wantDeep=false;
        },
        diagnostics(deep=false){if(deep)this.wantDeep=true;return {...this.cachedDiagnostics,threaded:true,inFlight,completedFrames:this.completedFrames,lastCompletion:this.lastCompletion,transferPoolBytes:pool.bytes};},
        compareNextFrame(){if(waits.has('comparison'))throw Error('Comparison already pending');this.comparisonRequested=true;return wait('comparison',30000);},
        async idle(){if(!inFlight)return;return wait('idle',30000);},
        async report(deep=false){const promise=wait('report',30000);worker.postMessage({kind:'report',deep});return promise;},
        close(){worker?.terminate();fail(Error('Renderer closed'),false);}
    };
    function wait(key,timeout=15000){return new Promise((resolve,reject)=>{const timer=setTimeout(()=>{waits.delete(key);reject(Error('Renderer timed out: '+key));},timeout);waits.set(key,{resolve:value=>{clearTimeout(timer);resolve(value);},reject:value=>{clearTimeout(timer);reject(value);}});});}
    function fail(value,notify=true){if(error)return;error=value instanceof Error?value:Error(String(value));for(const pending of waits.values())pending.reject(error);waits.clear();if(transferred&&notify)onError(error);}
    try{
        worker=new Worker(new URL('./render-worker.js',import.meta.url),{type:'module'});worker.onerror=e=>fail(e.message);
        worker.onmessage=e=>{const m=e.data;if(m.kind==='error'){fail(m.message);return;}if(m.kind==='frame'){
            inFlight--;pool.release(m.packed);result.profile=m.profile;result.completedFrames++;
            result.lastCompletion={id:m.id,finishMs:m.finishMs,intervalMs:lastFinish===null?null:m.finishMs-lastFinish,renderMs:m.renderMs};lastFinish=m.finishMs;
            result.oncompleted?.({...result.lastCompletion,profile:m.profile});
            if(m.diagnostics)result.cachedDiagnostics=m.diagnostics;
            if(m.comparison){waits.get('comparison')?.resolve(m.comparison);waits.delete('comparison');}
            if(!inFlight){waits.get('idle')?.resolve();waits.delete('idle');}result.onavailable?.();
        }else{waits.get(m.kind)?.resolve(m);waits.delete(m.kind);}};
        const probe=wait('probe');worker.postMessage({kind:'probe'});await probe;
        const a=world.transferControlToOffscreen();transferred=true;const b=overlay.transferControlToOffscreen(),ready=wait('ready',60000);
        result.size={worldWidth:world.width,worldHeight:world.height,uiWidth:overlay.width,uiHeight:overlay.height};
        worker.postMessage({kind:'init',world:a,overlay:b},[a,b]);const config=await ready;result.hardware=config.hardware;result.maximumSkinningBones=config.maximumSkinningBones;return result;
    }catch(error){worker?.terminate();if(transferred)throw error;console.info('Using main-thread graphics:',String(error));return directRenderer(world,overlay);}
}
