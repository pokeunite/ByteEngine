// Bounded, local-only recordings. Deep instrumentation is opt-in and timed.
export class CaptureRing {
    constructor(capacity){this.capacity=capacity;this.clear();}
    clear(){this.items=new Array(this.capacity);this.cursor=0;this.count=0;this.dropped=0;}
    push(value){this.items[this.cursor]=value;this.cursor=(this.cursor+1)%this.capacity;if(this.count<this.capacity)this.count++;else this.dropped++;}
    values(){return Array.from({length:this.count},(_,i)=>this.items[(this.cursor-this.count+i+this.capacity)%this.capacity]);}
}
export function summarizeFrames(list){
    const values=list.filter(f=>f.visible&&f.intervalMs>0).map(f=>f.intervalMs).sort((a,b)=>a-b);
    const p=q=>values.length?values[Math.floor((values.length-1)*q)]:null;
    return {frames:list.length,visibleFrames:values.length,fps:values.length?1000/(values.reduce((a,b)=>a+b,0)/values.length):null,
        p50Ms:p(.5),p95Ms:p(.95),p99Ms:p(.99),hitchesOver50Ms:values.filter(n=>n>50).length,hitchesOver100Ms:values.filter(n=>n>100).length};
}
export function performanceTrace(report){
    const traceEvents=[{ph:'M',name:'process_name',pid:1,tid:1,args:{name:'ByteEngine web game'}}];
    const span=(name,cat,ts,dur,args,tid=1)=>{if(Number.isFinite(ts)&&Number.isFinite(dur)&&dur>=0)traceEvents.push({name,cat,ph:'X',pid:1,tid,ts:ts*1000,dur:dur*1000,args});};
    for(const f of report.frames){
        const start=f.startT??f.t-f.frameMs;
        span('Frame','frame',start,f.frameMs,{id:f.id,intervalMs:f.intervalMs,sampleId:f.sampleId,deep:f.deep});
        let t=start;for(const [name,key] of [['WASM simulation + encoding','managedMs'],['Frame JSON decode','parseMs'],['Audio commands','audioMs'],['Video update','videoMs'],['Audio position feedback','audioPositionMs'],['Browser rendering','renderMs']]){span(name,'cpu',t,f[key],{});t+=f[key]||0;}
        traceEvents.push({name:'Frame counters',cat:'counters',ph:'C',pid:1,tid:1,ts:start*1000,args:{draws:f.draws||0,triangles:f.triangles||0,...(Number.isFinite(f.gpuMs)?{gpuMs:f.gpuMs}:{}),uploads:f.pending||0}});
    }
    for(const e of report.events){
        if(e.kind==='long-task'||e.kind==='long-animation-frame')span(e.kind,'browser',e.startMs,e.durationMs,{},2);
        else if(e.kind==='resource')span('Resource','network',e.startMs,e.durationMs,{url:e.url,transferBytes:e.transferBytes},3);
        else traceEvents.push({name:e.kind,cat:'recording',ph:'i',s:'t',pid:1,tid:1,ts:e.t*1000,args:e});
    }
    return {traceEvents,displayTimeUnit:'ms',metadata:{...report.build,notes:['GPU values are asynchronous counters, not timestamped spans.','Component/system samples are inclusive aggregates in the companion JSON, not fabricated timeline spans.']}};
}
export function createPerformanceCapture(){
    const epoch=performance.now(),windowSeconds=120;
    const frames=new CaptureRing(18000),events=new CaptureRing(4096),samples=new CaptureRing(512),inventories=new CaptureRing(4);
    const probes={skipGeometry:false,skipUi:false};
    let recording=true,frozenAt=0,lastSample=-Infinity,lastStats=0,serial=0,sampleSerial=0,lastScene='',diagnostics=null,hardware=null,build=null;
    let sources={},deepUntil=0,profileOn=false,lastSamplingMs=0,lastInventory=-Infinity,readBasic=()=>diagnostics??{};
    const time=()=>performance.now()-epoch;
    function event(kind,detail={}){if(recording)events.push({...detail,t:time(),kind});}
    function setProfile(value){if(profileOn===value)return;if(profileOn&&!value&&recording&&sources.readDetails){const start=performance.now();collect(serial,true,true);event('profile-tail',{durationMs:performance.now()-start});}profileOn=value;try{sources.profileComponents?.(value);}catch(e){event('diagnostic-error',{message:String(e)});}}
    function startDeep(seconds=30){deepUntil=performance.now()+Math.max(1,Math.min(30,Number(seconds)||30))*1000;event('deep-start',{seconds:(deepUntil-performance.now())/1000});inventory();beforeFrame();}
    function beforeFrame(){const deep=recording&&performance.now()<deepUntil;setProfile(deep);if(deepUntil&&performance.now()>=deepUntil){deepUntil=0;event('deep-finished');}return deep;}
    function inventory(){if(!sources.readInventory||performance.now()-lastInventory<1000)return;const start=performance.now();lastInventory=start;try{inventories.push({t:time(),data:sources.readInventory()});event('inventory',{durationMs:performance.now()-start});}catch(e){event('diagnostic-error',{message:String(e)});}}
    function collect(id,deep,tail=false){
        const start=performance.now();lastSample=start;
        try{diagnostics=readBasic();const core=sources.readDetails?.()??diagnostics;
            if(tail&&core.profiler?.frames===0)return;
            const jsHeap=performance.memory?{usedBytes:performance.memory.usedJSHeapSize,totalBytes:performance.memory.totalJSHeapSize,limitBytes:performance.memory.jsHeapSizeLimit}:null;
            samples.push({id:++sampleSerial,firstFrameId:Math.max(1,id-Math.max(1,core.profiler?.frames??1)+1),frameId:id,t:time(),deep,core,browser:sources.readBrowser?.()??null,jsHeap});
            if(diagnostics.scene!==lastScene){event('scene',{name:diagnostics.scene});lastScene=diagnostics.scene;if(deep)inventory();}
        }catch(e){event('diagnostic-error',{message:String(e)});}finally{lastSamplingMs=performance.now()-start;}
    }
    const recent=()=>frames.values().filter(f=>f.t>=(recording?time():frozenAt)-windowSeconds*1000);
    const panel=document.createElement('aside');panel.id='byteengine-capture';panel.hidden=true;
    panel.style.cssText='position:fixed;z-index:10000;left:16px;bottom:16px;max-width:calc(100vw - 64px);padding:14px;background:#101411f5;color:#efeee5;border:1px solid #e4b524;font:13px system-ui;box-shadow:0 4px 20px #0008';
    const title=document.createElement('strong');title.textContent='Performance recording · F8 to hide';panel.append(title);
    const stats=document.createElement('p');stats.textContent='Rolling 120-second recording. Reproduce the slowdown, then download.';panel.append(stats);
    const notice=document.createElement('p');notice.textContent='F9: record 30 seconds of detailed gameplay costs. Adds overhead. F8: download afterward. Reports stay on this device.';notice.style.maxWidth='560px';panel.append(notice);
    const controls=document.createElement('div');controls.style.cssText='display:flex;gap:8px;flex-wrap:wrap';panel.append(controls);
    const button=(text,action)=>{const b=document.createElement('button');b.textContent=text;b.style.cssText='padding:7px 10px;background:#28332d;color:#fff;border:1px solid #67766b;cursor:pointer';b.onclick=action;controls.append(b);return b;};
    function report(){const list=recent();return {format:'ByteEngine-performance',version:2,createdAt:new Date().toISOString(),windowSeconds,recording,
        droppedFrames:frames.dropped,droppedEvents:events.dropped,droppedSamples:samples.dropped,droppedInventories:inventories.dropped,
        limits:{frames:frames.capacity,events:events.capacity,samples:samples.capacity,inventories:inventories.capacity,objectRowsPerSample:256,objectRowsTrackedPerWindow:4096,systemRowsPerWindow:1024,inventoryObjects:20000},
            summary:summarizeFrames(list),modeSummaries:{normal:summarizeFrames(list.filter(f=>!f.deep)),deep:summarizeFrames(list.filter(f=>f.deep))},hardware,build,environment:{userAgent:navigator.userAgent,cores:navigator.hardwareConcurrency,deviceMemoryGiB:navigator.deviceMemory??null,
            dpr:devicePixelRatio,viewport:[innerWidth,innerHeight],host:location.host,visibility:document.visibilityState,crossOriginIsolated:globalThis.crossOriginIsolated??false},
        probes:{...probes},diagnostics,frames:list,events:events.values(),samples:samples.values(),inventories:inventories.values(),
        availability:{observers:globalThis.PerformanceObserver?.supportedEntryTypes??[],jsHeap:!!performance.memory,gpuTimer:hardware?.gpuTimer??null},
        notes:['Deep callback/system timings and allocations are inclusive; nested scopes must not be summed as exclusive cost.',
            'Deep mode and diagnostic sampling add overhead; compare against normal recording. captureOverheadMs records recorder work; profile callback overhead is not subtracted.',
            'Deep samples aggregate every profiled frame in their firstFrameId/frameId window, retaining maximum callback times. Transfer cadence adapts to sampling cost (500–3000ms).',
            'GPU timing is asynchronous world/shadow time; null is unavailable/disjoint. Browser UI uses Canvas CPU timing.',
            'GPU VRAM, OS scheduling, temperatures, and arbitrary function stacks are not exposed by ordinary browser pages. Missing measurements are not zero.',
            'No save files, account data, cookies, input text, screenshots or asset contents are collected. Errors and object names may contain project information.',
            'Rings and row counts are bounded; dropped counters and limits disclose truncation. Download before the issue leaves the 120-second frame window.']};}
    async function save(kind='report'){
        try{event('report-saved',{kind});const data=report(),trace=kind==='trace';
            const blob=new Blob([JSON.stringify(trace?performanceTrace(data):data)],{type:'application/json'});
            const url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download='ByteEngine-'+(trace?'trace':'performance')+'-'+new Date().toISOString().replace(/[:.]/g,'-')+'.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),30000);
        }catch(e){notice.textContent='Could not save recording: '+String(e);}
    }
    const toggle=button('Pause capture',()=>{if(recording){setProfile(false);frozenAt=time();recording=false;}else{recording=true;event('capture-resumed');}toggle.textContent=recording?'Pause capture':'Resume capture';});
    function reset(){setProfile(false);for(const ring of [frames,events,samples,inventories])ring.clear();lastSample=lastInventory=-Infinity;serial=sampleSerial=0;lastScene='';diagnostics=null;recording=true;deepUntil=0;toggle.textContent='Pause capture';event('capture-start');}
    button('New capture',reset);button('Mark slowdown',()=>event('user-marker',{label:'Observed slowdown'}));
    button('Deep profile · 30s',()=>startDeep());button('Scene inventory',inventory);button('Download report',()=>save());button('Download trace',()=>save('trace'));
    for(const [key,label] of [['skipGeometry','3D'],['skipUi','UI']]){const b=button(label+' on',()=>{probes[key]=!probes[key];b.textContent=label+(probes[key]?' off':' on');event('render-probe',{...probes});});}
    document.body.append(panel);
    const show=()=>{panel.hidden=!panel.hidden;if(!panel.hidden&&document.pointerLockElement)document.exitPointerLock();};
    window.addEventListener('keydown',e=>{if(e.code==='F8'||e.code==='F9'){e.preventDefault();e.stopImmediatePropagation();if(!e.repeat){if(e.code==='F8')show();else startDeep();}}},true);
    document.addEventListener('visibilitychange',()=>event('visibility',{state:document.visibilityState}));
    const safeUrl=value=>{try{const u=new URL(value,location.href);return u.origin+u.pathname;}catch{return String(value).slice(0,200);}};
    const observe=(type,handler,extra={})=>{try{if(globalThis.PerformanceObserver?.supportedEntryTypes?.includes(type))new PerformanceObserver(list=>{for(const item of list.getEntries())handler(item);}).observe({type,buffered:true,...extra});}catch(e){event('observer-unavailable',{type,message:String(e)});}};
    observe('longtask',e=>event('long-task',{startMs:e.startTime-epoch,durationMs:e.duration,attribution:(e.attribution??[]).map(a=>({name:a.name,containerType:a.containerType}))}));
    observe('long-animation-frame',e=>event('long-animation-frame',{startMs:e.startTime-epoch,durationMs:e.duration,blockingMs:e.blockingDuration,
        scripts:(e.scripts??[]).slice(0,32).map(s=>({source:safeUrl(s.sourceURL),function:s.sourceFunctionName,durationMs:s.duration,forcedStyleMs:s.forcedStyleAndLayoutDuration}))}));
    observe('resource',e=>event('resource',{url:safeUrl(e.name),initiator:e.initiatorType,startMs:e.startTime-epoch,durationMs:e.duration,
        transferBytes:e.transferSize,encodedBytes:e.encodedBodySize,decodedBytes:e.decodedBodySize,protocol:e.nextHopProtocol,status:e.responseStatus??null}));
    observe('event',e=>event('input-latency',{name:e.name,startMs:e.startTime-epoch,durationMs:e.duration,processingDelayMs:e.processingStart-e.startTime,interactionId:e.interactionId}),{durationThreshold:16});
    const api={event,probes,report,save,startDeep,beforeFrame,inventory,reset,get deep(){return profileOn;},
        configure(value){sources=value;build=value.build??build;sources.profileComponents?.(profileOn);},setHardware(value){hardware=value;},setBuildIdentity(value){build={...build,...value};event('build-identity',value);},
        failure(detail){const was=recording;recording=true;event('error',detail);setProfile(false);recording=false;if(was)frozenAt=time();toggle.textContent='Resume capture';},
        frame(value,readDiagnostics){
            if(!recording)return;readBasic=readDiagnostics;const start=performance.now(),deep=value.deep??beforeFrame(),id=++serial;
            const due=start-lastSample>=(deep?Math.min(3000,Math.max(500,lastSamplingMs*20)):1000);
            if(due)collect(id,deep);
            const copy={...value};delete copy.startMs;
            const f={...copy,id,t:time(),startT:Number.isFinite(value.startMs)?value.startMs-epoch:null,visible:!document.hidden,deep,skipGeometry:probes.skipGeometry,skipUi:probes.skipUi,sampleId:sampleSerial,samplingMs:due?lastSamplingMs:0};
            f.captureOverheadMs=performance.now()-start;frames.push(f);
            if(!panel.hidden&&start-lastStats>1000){lastStats=start;const s=summarizeFrames(recent());stats.textContent=(deep?'DEEP PROFILE · ':'Recording · ')+(s.fps?.toFixed(1)??'—')+' FPS · p95 '+(s.p95Ms?.toFixed(1)??'—')+' ms · '+s.hitchesOver50Ms+' slow frames';}
        }};
    globalThis.byteEngineCapture=api;event('capture-start');return api;
}
