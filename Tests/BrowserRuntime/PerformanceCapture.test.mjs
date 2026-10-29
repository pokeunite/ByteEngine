import assert from 'node:assert/strict';
import {CaptureRing,summarizeFrames,performanceTrace,createPerformanceCapture} from '../../Player/ByteEngine.Browser/wwwroot/performance.js';
import {summarizeCapture} from '../../Tools/SummarizeWebPerformance.mjs';
const ring=new CaptureRing(2);for(let i=0;i<5;i++)ring.push(i);assert.deepEqual(ring.values(),[3,4]);assert.equal(ring.dropped,3);ring.clear();assert.equal(ring.count,0);
assert.equal(summarizeFrames([{visible:true,intervalMs:50},{visible:false,intervalMs:1000}]).fps,20);
let now=0;
Object.defineProperty(globalThis,'performance',{value:{now:()=>now,memory:{usedJSHeapSize:10,totalJSHeapSize:20,jsHeapSizeLimit:30}},configurable:true});
class Element extends EventTarget{style={};children=[];append(...items){this.children.push(...items);}click(){this.onclick?.();}}
const document=new Element();document.body=new Element();document.createElement=()=>new Element();document.hidden=false;document.visibilityState='visible';
Object.assign(globalThis,{document,window:new Element(),devicePixelRatio:1,innerWidth:1280,innerHeight:720,location:{host:'test',href:'https://test/game?token=secret'}});
Object.defineProperty(globalThis,'navigator',{value:{userAgent:'Test',hardwareConcurrency:4},configurable:true});
class Observer{static supportedEntryTypes=['resource','longtask'];static listeners={};constructor(callback){this.callback=callback;}observe({type}){Observer.listeners[type]=this.callback;}}
globalThis.PerformanceObserver=Observer;
const api=createPerformanceCapture(),profiles=[];
api.configure({build:{test:true},profileComponents:b=>profiles.push(b),readDetails:()=>({profiler:{enabled:api.deep}}),readInventory:()=>({objects:[{name:'fixture'}]})});
api.setHardware({gpuTimer:false});api.setBuildIdentity({bootSha256:'fixture'});
const frame=()=>api.frame({startMs:now-20,frameMs:20,managedMs:10,parseMs:1,audioMs:1,renderMs:8,intervalMs:25,gpuMs:null},()=>({scene:'fixture'}));
now=100;frame();assert.equal(api.report().samples.length,1);assert.equal(api.report().frames[0].sampleId,1);
api.startDeep(1);assert.equal(profiles.at(-1),true);now=700;frame();assert(api.report().frames.at(-1).deep);assert(api.report().inventories.length);
now=1200;api.beforeFrame();assert.equal(profiles.at(-1),false);frame();assert(!api.report().frames.at(-1).deep);
Observer.listeners.resource({getEntries:()=>[{name:'https://test/file?token=secret#private',startTime:10,duration:20,transferSize:50}]});
assert.equal(api.report().events.find(e=>e.kind==='resource').url,'https://test/file');
const trace=performanceTrace(api.report());assert(trace.traceEvents.some(e=>e.name==='WASM simulation + encoding'));
assert(!trace.traceEvents.filter(e=>e.ph==='C').some(e=>'gpuMs' in e.args),'unsupported GPU timing must not become a zero counter');
const panel=document.body.children[0],controls=panel.children.find(e=>e.children.some(c=>c.textContent==='Pause capture'));
controls.children.find(c=>c.textContent==='Pause capture').click();const before=api.report().frames.length;now=5000;frame();assert.equal(api.report().frames.length,before);
api.failure({message:'fixture error'});assert(api.report().events.some(e=>e.kind==='error'));
api.reset();assert.equal(api.report().frames.length,0);assert.equal(api.report().events.length,1);
const fixture={format:'ByteEngine-performance',frames:[{managedMs:40,renderMs:4,gpuMs:null,deep:true}],samples:[{deep:true,core:{profiler:{
 components:[{Component:'Worker',Phase:'Update',Calls:2,TotalMs:30,SlowestMs:20,AllocatedBytes:1000}],
 objects:[{Id:'fixture',Object:'Spider',Component:'Worker',Phase:'Update',Calls:1,TotalMs:20}],
 systems:[{Category:'Navigation.FindPath',Name:'region',Calls:1,TotalMs:10}]}}}],events:[]};
const ranked=summarizeCapture(fixture);assert.equal(ranked.componentCosts[0].meanSampleMs,30);assert.equal(ranked.objectCosts[0].name.startsWith('Spider'),true);
assert(ranked.warnings.some(s=>s.includes('GPU timing unavailable')));assert(ranked.evidence.some(s=>s.includes('Managed')));
const windowed={...fixture,frames:[{id:10,t:100,captured:true,visible:true,intervalMs:40,managedMs:30},{id:20,t:200,captured:false,visible:true,intervalMs:10,managedMs:5}],
 samples:[{...fixture.samples[0],t:50,firstFrameId:1,frameId:5},{...fixture.samples[0],t:150,firstFrameId:5,frameId:15},{...fixture.samples[0],t:180,firstFrameId:16,frameId:18}]};
const scoped=summarizeCapture(windowed);assert.equal(scoped.measurementWindow.samplesAnalyzed,1);assert.equal(scoped.measurementWindow.olderOrPartialSamplesExcluded,2);
assert.equal(scoped.groups.capturedNormal.fps,25);assert.equal(scoped.groups.normal.fps,40);
console.log('PASS bounded recorder, sample links, deep timeout, overhead metadata, pause/error retention, URL redaction, mode summaries and trace export');
