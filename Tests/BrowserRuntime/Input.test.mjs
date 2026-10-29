import {readFileSync} from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';

// Execute the shipped input listeners, rather than a duplicate implementation.
const source=readFileSync(new URL('../../Player/ByteEngine.Browser/wwwroot/main.js',import.meta.url),'utf8');
class Surface extends EventTarget {
    style={}; focus(){document.activeElement=this;}
    requestPointerLock(){document.pointerLockElement=this;document.dispatchEvent(new Event('pointerlockchange'));return Promise.resolve();}
    getBoundingClientRect(){return {left:0,top:0,width:1280,height:720};}
    setPointerCapture(){}
}
const document=new Surface(),canvas=new Surface(),window=new Surface();
document.hidden=false;document.hasFocus=()=>true;
document.exitPointerLock=()=>{document.pointerLockElement=null;document.dispatchEvent(new Event('pointerlockchange'));};
let wants=true;
const context=vm.createContext({canvas,window,document,performance,console,fail:e=>{throw e;},audio:{activate:()=>Promise.resolve()},game:{WantsPointerLock:()=>wants,CancelTouches(){},Touch(){}}});
const block=source.slice(source.indexOf('    const keys=new Set()'),source.indexOf('    const uiCommands=[];'));
vm.runInContext(block+'\nglobalThis.state=()=>({keys:[...keys,...keyPulses],buttons:[...buttons],pauseFromCapture});globalThis.clearPulses=()=>{keyPulses.clear();buttonPulses.clear();pauseFromCapture=false;};',context);
function send(target,type,fields={}){const event=new Event(type,{cancelable:true});Object.assign(event,fields);target.dispatchEvent(event);return event;}
assert(send(canvas,'pointerdown',{pointerType:'touch',pointerId:1,clientX:100,clientY:100}).defaultPrevented,'touch must suppress compatibility mouse clicks');
assert.equal(context.state().buttons.length,0);
send(canvas,'pointerdown',{pointerType:'mouse',button:2});
send(canvas,'mousedown',{button:2});
send(canvas,'mousedown',{button:0});
assert.deepEqual([...context.state().buttons].sort(),['Left','Right']);
send(window,'mouseup',{button:0});assert.deepEqual([...context.state().buttons],['Right']);
send(window,'mouseup',{button:2});assert.equal(context.state().buttons.length,0);
document.exitPointerLock();assert(context.state().keys.includes('Escape'));assert(context.state().pauseFromCapture);
context.clearPulses();canvas.requestPointerLock();wants=false;document.exitPointerLock();
assert(!context.state().keys.includes('Escape'),'menu-requested unlock must not create another Escape');
wants=true;canvas.requestPointerLock();send(canvas,'keydown',{code:'Escape',repeat:false});
assert.equal(context.state().keys.filter(k=>k==='Escape').length,2); // Held + pulse are deduplicated by the frame bridge.
assert(source.includes('document.pointerLockElement===canvas||pauseFromCapture'));
send(canvas,'blur');assert.equal(context.state().keys.length,0);assert.equal(context.state().buttons.length,0);
console.log('PASS shipped browser input: aim/fire chords, independent release, browser Escape, intentional unlock and focus loss');
