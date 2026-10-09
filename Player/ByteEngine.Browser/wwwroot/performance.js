export function createPerformanceCapture() {
    const epoch=performance.now(), capacity=7200, eventCapacity=2000;
    let frames=new Array(capacity), cursor=0, count=0, events=[], droppedFrames=0, droppedEvents=0, recording=true, lastSample=0, frozenAt=0, lastScene='', diagnostics=null, hardware=null;
    const probes={skipGeometry:false,skipUi:false};
    const time=()=>performance.now()-epoch;
    function event(kind,detail={}) { if(!recording)return; if(events.length>=eventCapacity){events.shift();droppedEvents++;}events.push({t:time(),kind,...detail}); }
    function recent(){const out=[];for(let i=0;i<count;i++){const f=frames[(cursor-count+i+capacity)%capacity];if(f.t>=(recording?time():frozenAt)-60000)out.push(f);}return out;}
    function summary(list){const values=list.filter(f=>f.visible&&f.intervalMs>0).map(f=>f.intervalMs).sort((a,b)=>a-b);const p=q=>values.length?values[Math.min(values.length-1,Math.floor((values.length-1)*q))]:null;return {frames:list.length,visibleFrames:values.length,fps:values.length?1000/(values.reduce((a,b)=>a+b,0)/values.length):null,p50Ms:p(.5),p95Ms:p(.95),p99Ms:p(.99),hitchesOver50Ms:values.filter(n=>n>50).length};}
    const panel=document.createElement('aside');panel.id='byteengine-capture';panel.hidden=true;
    panel.style.cssText='position:fixed;z-index:100;left:16px;bottom:16px;max-width:calc(100vw - 64px);padding:14px;background:#101815f5;color:#efeee5;border:1px solid #d69b39;font:13px system-ui;box-shadow:0 4px 20px #0008';
    const title=document.createElement('strong');title.textContent='Performance capture · F8 to hide';panel.append(title);
    const stats=document.createElement('p');stats.textContent='Recording the last 60 seconds';panel.append(stats);
    const controls=document.createElement('div');controls.style.cssText='display:flex;gap:8px;flex-wrap:wrap';panel.append(controls);
    const button=(text,action)=>{const b=document.createElement('button');b.textContent=text;b.style.cssText='padding:7px 10px;background:#28332d;color:#fff;border:1px solid #67766b;cursor:pointer';b.onclick=action;controls.append(b);return b;};
    function report(){const list=recent();return {format:'ByteEngine-performance',version:1,createdAt:new Date().toISOString(),windowSeconds:60,recording,droppedFrames,droppedEvents,summary:summary(list),hardware,environment:{userAgent:navigator.userAgent,cores:navigator.hardwareConcurrency,deviceMemoryGiB:navigator.deviceMemory??null,dpr:devicePixelRatio,viewport:[innerWidth,innerHeight],host:location.host,visibility:document.visibilityState},probes:{...probes},diagnostics,frames:list,events:[...events],notes:['GPU time is asynchronous geometry time only; null means unsupported or disjoint.','managedMs includes WASM simulation and frame serialization; parseMs is measured separately.','No save files, frame payloads, pixels or account data are included.']};}
    function save(){event('report-saved');const blob=new Blob([JSON.stringify(report())],{type:'application/json'}),url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download='ByteEngine-performance-'+new Date().toISOString().replace(/[:.]/g,'-')+'.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),30000);}
    const toggle=button('Pause capture',()=>{frozenAt=time();recording=!recording;toggle.textContent=recording?'Pause capture':'Resume capture';if(recording)event('capture-resumed');});
    button('New capture',()=>{frames=new Array(capacity);cursor=count=droppedFrames=droppedEvents=0;events=[];lastScene='';recording=true;toggle.textContent='Pause capture';event('capture-start');});
    button('Mark hitch',()=>event('user-marker',{label:'Observed hitch'}));button('Save report',save);
    for(const [key,label] of [['skipGeometry','3D'],['skipUi','UI']]){const b=button(label+' on',()=>{probes[key]=!probes[key];b.textContent=label+(probes[key]?' off':' on');event('render-probe',{...probes});});}
    document.body.append(panel);
    window.addEventListener('keydown',e=>{if(e.code==='F8'){e.preventDefault();e.stopImmediatePropagation();panel.hidden=!panel.hidden;}},true);
    document.addEventListener('visibilitychange',()=>event('visibility',{state:document.visibilityState}));
    try{new PerformanceObserver(list=>{for(const item of list.getEntries())event('long-task',{startMs:item.startTime-epoch,durationMs:item.duration});}).observe({type:'longtask',buffered:true});}catch{}
    const api={event,probes,report,save,setHardware(value){hardware=value;},frame(value,readDiagnostics){
        if(!recording)return;const now=performance.now();
        if(now-lastSample>=250){lastSample=now;try{diagnostics=readDiagnostics();if(diagnostics.scene!==lastScene){event('scene',{name:diagnostics.scene,building:diagnostics.building});lastScene=diagnostics.scene;}}catch(error){event('diagnostic-error',{message:String(error)});}}
        frames[cursor]={t:time(),visible:!document.hidden,...value,diagnostics};cursor=(cursor+1)%capacity;if(count<capacity)count++;else droppedFrames++;
        if(!panel.hidden&&now-lastSample<10){const s=summary(recent());stats.textContent=(recording?'Recording':'Paused')+' · '+(s.fps?.toFixed(1)??'—')+' FPS · p95 '+(s.p95Ms?.toFixed(1)??'—')+' ms · '+s.hitchesOver50Ms+' hitches';}
    }};
    globalThis.byteEngineCapture=api;event('capture-start');return api;
}
