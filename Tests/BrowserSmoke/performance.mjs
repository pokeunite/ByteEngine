import {readFileSync} from 'node:fs';
import assert from 'node:assert/strict';
let clock=0;globalThis.performance={now:()=>clock};globalThis.devicePixelRatio=1;globalThis.innerWidth=1280;globalThis.innerHeight=720;globalThis.location={host:'test.local'};
const controls=[];class Element{constructor(){this.style={};this.hidden=false;}append(e){if(e instanceof Element&&e.textContent)controls.push(e);}click(){this.onclick?.();}}
globalThis.document={hidden:false,visibilityState:'visible',body:new Element(),createElement:()=>new Element(),addEventListener(){}};globalThis.window={addEventListener(){}};
const module=await import('data:text/javascript;base64,'+Buffer.from(readFileSync('Player/ByteEngine.Browser/wwwroot/performance.js')).toString('base64'));
const capture=module.createPerformanceCapture();for(let i=0;i<8000;i++){clock+=8;capture.frame({intervalMs:8,managedMs:2,parseMs:.2,uploadMs:0},()=>({scene:'Test',managedMemoryMB:20}));}
let r=capture.report();assert.equal(r.frames.length,7200);assert.equal(r.droppedFrames,800);assert.equal(r.summary.fps,125);
for(let i=0;i<2100;i++)capture.event('marker');r=capture.report();assert.equal(r.events.length,2000);assert(r.droppedEvents>0);
controls.find(e=>e.textContent==='Pause capture').click();clock+=120000;assert.equal(capture.report().frames.length,7200,'Paused capture lost frames');
controls.find(e=>e.textContent==='3D on').click();assert.equal(capture.probes.skipGeometry,true);
controls.find(e=>e.textContent==='New capture').click();assert.equal(capture.report().frames.length,0);
console.log('PASS bounded 60-second capture, percentile summary, bounded events, paused retention, render probe, reset');
