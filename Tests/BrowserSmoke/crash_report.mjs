import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

const elements=[];
class Element {
    constructor(){this.style={};this.children=[];this.dataset={};elements.push(this);}
    append(...children){this.children.push(...children);}
    click(){}
}
globalThis.document={createElement:()=>new Element(),body:new Element(),visibilityState:'visible',hidden:false,addEventListener(){}};
globalThis.window={addEventListener(){}};
Object.defineProperty(globalThis,'navigator',{value:{userAgent:'test',hardwareConcurrency:4},configurable:true});
globalThis.devicePixelRatio=1;globalThis.innerWidth=1280;globalThis.innerHeight=720;
globalThis.location={host:'localhost'};
Object.defineProperty(globalThis,'performance',{value:{now:()=>1000},configurable:true});
const {createPerformanceCapture}=await import('../../Player/ByteEngine.Browser/wwwroot/performance.js');
const capture=createPerformanceCapture();
capture.frame({intervalMs:16},()=>({scene:'Contract transition'}));
elements.find(e=>e.textContent==='Pause capture').onclick();
capture.failure({message:'Managed index error during scene update'});
const report=capture.report();
assert.equal(report.recording,false);
assert.equal(report.diagnostics.scene,'Contract transition');
assert.equal(report.events.at(-1).message,'Managed index error during scene update');
assert.equal(report.events.at(-1).kind,'error');
assert.equal(report.frames.length,1);

const source=fs.readFileSync(new URL('../../Player/ByteEngine.Browser/wwwroot/main.js',import.meta.url),'utf8');
const fail=source.slice(source.indexOf('function fail(error)'),source.indexOf("window.addEventListener('error'"));
const status=new Element(),retry=new Element(),progress=new Element();
const context={stopped:false,readManagedError:()=>"Managed failure in 'Quarry'",capture,status,retry,progress,document,console:{error(){}},error:new Error('dotnet.js:3:6165')};
vm.runInNewContext(fail+';fail(error);fail(error);',context);
assert.equal(context.stopped,true);
assert.equal(status.children.filter(e=>e instanceof Element && e.textContent==='Save error report').length,1);
assert.equal(retry.hidden,false);
assert.match(capture.report().events.at(-1).message,/Managed failure in 'Quarry'/);
assert.match(capture.report().events.at(-1).message,/dotnet.js:3:6165/);
console.log('PASS paused capture retains crash, managed details, recent scene, save action and duplicate-error guard.');
