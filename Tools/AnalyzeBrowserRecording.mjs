// Read-only analysis of a ByteEngine performance report.
import fs from 'node:fs';
const file=process.argv[2];
if(!file)throw new Error('Usage: node Tools/AnalyzeBrowserRecording.mjs <report.json>');
const r=JSON.parse(fs.readFileSync(file,'utf8'));
const vals=(a,key)=>a.map(x=>x?.[key]).filter(Number.isFinite).sort((a,b)=>a-b);
const q=(a,p)=>a.length?a[Math.min(a.length-1,Math.floor(a.length*p))]:null;
const stat=(a,key)=>{const v=vals(a,key);if(!v.length)return null;return{n:v.length,mean:+(v.reduce((s,x)=>s+x,0)/v.length).toFixed(3),p50:+q(v,.5).toFixed(3),p95:+q(v,.95).toFixed(3),p99:+q(v,.99).toFixed(3),max:+v.at(-1).toFixed(3)}};
const keys=Object.fromEntries(Object.entries(r).map(([k,v])=>[k,Array.isArray(v)?{array:v.length,first:v[0]&&Object.keys(v[0]).slice(0,20)}:typeof v]));
const frames=r.frames??r.capture?.frames??[];
const samples=r.samples??r.capture?.samples??[];
const events=r.events??r.capture?.events??[];
if(frames.length===0){console.log(JSON.stringify({file,keys},null,2));process.exit(0);}
const around=(t,width=1000)=>events.filter(e=>Math.abs((e.t??e.startMs??0)-t)<=width).reduce((m,e)=>(m[e.kind]=(m[e.kind]||0)+1,m),{});
const metrics=['frameMs','managedMs','parseMs','renderMs','intervalMs','loopMs','inputMs','updateStepMs','renderPreparationMs','renderAllocatedBytes','gpuMs','cpu3DMs','uiCpuMs','draws','triangles','instances','materialChanges','uiCommands','uiBytes','payloadChars'];
const phases=['UpdateMs','PhysicsMs','LateUpdateMs','AnimationMs','AllocatedBytes','PhysicsTicks','BroadPhaseCandidates'];
const scenePhases={};for(const k of phases)scenePhases[k]=stat(frames.map(f=>f.sceneMetrics),k);
const frameAllocation=stat(frames,'managedMs');
const long=frames.filter(f=>f.intervalMs>50).slice(-40).map(f=>({id:f.id,intervalMs:f.intervalMs,frameMs:f.frameMs,managedMs:f.managedMs,updateStepMs:f.updateStepMs,renderPreparationMs:f.renderPreparationMs,renderMs:f.renderMs,gpuMs:f.gpuMs,uiCpuMs:f.uiCpuMs,draws:f.draws,triangles:f.triangles,textureUploadBytes:f.textureUploadBytes,meshUploadBytes:f.meshUploadBytes,context:around(f.t??f.startT,500)}));
const eventSummary={};for(const e of events){const k=e.kind;const x=eventSummary[k]??={count:0,durationMs:0,maxMs:0,bytes:0,textures:0};x.count++;if(Number.isFinite(e.durationMs)){x.durationMs+=e.durationMs;x.maxMs=Math.max(x.maxMs,e.durationMs);}if(Number.isFinite(e.textureBytes))x.bytes+=e.textureBytes;if(Array.isArray(e.allocations)){x.textures+=e.allocations.length;for(const a of e.allocations)x.bytes+=(a.width||0)*(a.height||0)*4;}}
const textureUploads=events.filter(e=>e.kind==='graphics-upload').flatMap(e=>e.allocations??[]).filter(a=>a.source==='dynamic/embedded texture');
const uploadBySize={};for(const a of textureUploads){const k=a.width+'x'+a.height;uploadBySize[k]=(uploadBySize[k]||0)+1;}
const lastSample=samples.at(-1);
const sceneStart=events.find(e=>e.kind==='scene-start')?.t??0;
const firstReady=events.find(e=>e.kind==='first-ready-frame')?.t??sceneStart;
const epoch=frames[0]?.t??0;
const bins=[];for(let t=0;t<Math.max(120000,...frames.map(f=>f.t??0));t+=10000){const fs=frames.filter(f=>(f.t??0)>=t&&(f.t??0)<t+10000);if(!fs.length)continue;const iv=vals(fs,'intervalMs');const gp=vals(fs.map(f=>({v:f.gpuMs})),'v');const ev=events.filter(e=>(e.t??e.startMs??0)>=t&&(e.t??e.startMs??0)<t+10000);bins.push({fromSec:t/1000,frames:fs.length,fps:+(1000/(iv.reduce((s,x)=>s+x,0)/iv.length)).toFixed(1),intervalP95:+q(iv,.95).toFixed(1),managed:+(vals(fs,'managedMs').reduce((s,x)=>s+x,0)/fs.length).toFixed(1),prep:+(vals(fs,'renderPreparationMs').reduce((s,x)=>s+x,0)/fs.length).toFixed(1),update:+(vals(fs,'updateStepMs').reduce((s,x)=>s+x,0)/fs.length).toFixed(1),gpu:+(gp.reduce((s,x)=>s+x,0)/gp.length).toFixed(1),ui:+(vals(fs,'uiCpuMs').reduce((s,x)=>s+x,0)/fs.length).toFixed(1),allocKB:Math.round(vals(fs.map(f=>({v:f.renderAllocatedBytes})),'v').reduce((s,x)=>s+x,0)/fs.length/1024),uploads:ev.filter(e=>e.kind==='graphics-upload').length,uploadMB:+(ev.filter(e=>e.kind==='graphics-upload').flatMap(e=>e.allocations??[]).reduce((s,a)=>s+(a.width||0)*(a.height||0)*4,0)/1048576).toFixed(1),longTasks:ev.filter(e=>e.kind==='long-task').length,longTaskMs:Math.round(ev.filter(e=>e.kind==='long-task').reduce((s,e)=>s+(e.durationMs||0),0)),pendingMax:Math.max(0,...fs.map(f=>f.pending??0))});}
const after=Math.max(sceneStart,firstReady),steady=frames.filter(f=>(f.t??0)>=after+5000);
const steadyStats={fromSeconds:after/1000,...Object.fromEntries(metrics.map(k=>[k,stat(steady,k)])),scenePhases:Object.fromEntries(phases.map(k=>[k,stat(steady.map(f=>f.sceneMetrics),k)]))};
const sourceCounts={};for(const a of events.filter(e=>e.kind==='graphics-upload').flatMap(e=>e.allocations??[])){const k=a.source??'(unknown)';sourceCounts[k]??={count:0,mb:0};sourceCounts[k].count++;sourceCounts[k].mb+=(a.width||0)*(a.height||0)*4/1048576;}
const longEvents=events.filter(e=>e.kind==='long-animation-frame'||e.kind==='long-task').sort((a,b)=>(b.durationMs??0)-(a.durationMs??0)).slice(0,20).map(e=>({kind:e.kind,tSec:+((e.t??e.startMs??0)/1000).toFixed(2),durationMs:e.durationMs,blockingMs:e.blockingMs,scripts:(e.scripts??[]).slice(0,5)}));
const gpuUnique=[];const seenGpu=new Set();for(const f of frames){if(Number.isFinite(f.gpuRenderFrame)&&!seenGpu.has(f.gpuRenderFrame)){seenGpu.add(f.gpuRenderFrame);if(Number.isFinite(f.gpuMs))gpuUnique.push({v:f.gpuMs});}}
const uploadFrames=frames.filter(f=>(f.textureUploadBytes??0)>0);
const plainFrames=frames.filter(f=>(f.textureUploadBytes??0)===0);
const pendingFrames=frames.filter(f=>(f.pending??0)>0);
const latency=events.filter(e=>e.kind==='input-latency');
const latencyStats={durationMs:stat(latency,'durationMs'),processingDelayMs:stat(latency,'processingDelayMs')};
const intervalBuckets={};for(const f of frames){const n=f.intervalMs??0;const k=n<=20?'<=20':n<=34?'<=34':n<=50?'<=50':n<=100?'<=100':n<=200?'<=200':'>200';intervalBuckets[k]=(intervalBuckets[k]||0)+1;}
const memory={managedStart:samples[0]?.core?.memory?.managedBytes,managedEnd:samples.at(-1)?.core?.memory?.managedBytes,managedPeak:Math.max(...samples.map(s=>s.core?.memory?.managedBytes??0)),jsUsedStart:samples[0]?.jsHeap?.usedBytes,jsUsedEnd:samples.at(-1)?.jsHeap?.usedBytes,jsUsedPeak:Math.max(...samples.map(s=>s.jsHeap?.usedBytes??0)),gcStart:samples[0]?.core?.memory,gcEnd:samples.at(-1)?.core?.memory};
const keyTotals={frames:frames.length,captureSpanSec:+((frames.at(-1)?.t??0)/1000).toFixed(1),sceneStartSec:+(sceneStart/1000).toFixed(2),firstReadySec:+(firstReady/1000).toFixed(2),gpuUniqueSamples:gpuUnique.length,intervalBuckets,memory,latencyStats,uploadFrames:{n:uploadFrames.length,frameMs:stat(uploadFrames,'frameMs'),managedMs:stat(uploadFrames,'managedMs'),intervalMs:stat(uploadFrames,'intervalMs'),renderAllocatedBytes:stat(uploadFrames,'renderAllocatedBytes')},plainFrames:{n:plainFrames.length,frameMs:stat(plainFrames,'frameMs'),managedMs:stat(plainFrames,'managedMs'),intervalMs:stat(plainFrames,'intervalMs'),renderAllocatedBytes:stat(plainFrames,'renderAllocatedBytes')},pendingFrames:{n:pendingFrames.length,frameMs:stat(pendingFrames,'frameMs'),managedMs:stat(pendingFrames,'managedMs')},gpuUnique:stat(gpuUnique,'v')};
const out={file,keyTotals,summary:r.summary,frames:{...Object.fromEntries(metrics.map(k=>[k,stat(frames,k)])),scenePhases},bins,eventSummary,sourceCounts,uploadBySize,longEvents,lastSample:lastSample?{t:lastSample.t,core:lastSample.core,graphics:lastSample.browser?.graphics}:null};
console.log(JSON.stringify(out,null,2));
