import fs from 'node:fs';import assert from 'node:assert/strict';
const {BrowserPresentation}=await import('data:text/javascript;base64,'+fs.readFileSync('Player/ByteEngine.Browser/wwwroot/graphics.js').toString('base64'));
const p=new BrowserPresentation();
let d=p.dimensions(640,360,1.5,'Fast','Auto',16384);assert.deepEqual([d.worldWidth,d.worldHeight,d.uiWidth,d.uiHeight],[960,540,960,540]);
d=p.dimensions(1920,1080,1.5,'Fast','Auto',16384);assert.deepEqual([d.worldWidth,d.worldHeight,d.uiWidth,d.uiHeight],[1280,720,1920,1080]);
d=p.dimensions(3840,2160,2,'Fast','Native',16384);assert.deepEqual([d.worldWidth,d.worldHeight,d.uiWidth,d.uiHeight],[7680,4320,1920,1080]);
p.dimensions(1920,1080,1.5,'Fast','Auto');for(let i=0;i<8;i++)p.sample(80,2000+i*100,'Auto');assert.equal(p.factor,.75);
for(let i=0;i<8;i++)p.sample(5,8000+i*100,'Auto');assert.equal(p.factor,.8);
const prior=p.factor;for(const resolution of ['Native','720p','1080p','75%','50%'])p.sample(200,20000,resolution);assert.equal(p.factor,prior);
for(const value of [null,NaN,Infinity,-1,0])p.sample(value,30000,'Auto');assert.equal(p.factor,prior);
for(let i=0;i<20;i++)p.sample(200,30000,'Auto',1);assert.equal(p.factor,prior);
console.log('PASS fullscreen world/UI budgets, explicit Native resolution, responsive Auto scaling and unavailable/startup GPU guards');

const fixed=new BrowserPresentation();let initial=fixed.dimensions(1920,1080,2);assert.deepEqual([initial.worldWidth,initial.worldHeight],[1280,720]);for(let i=0;i<100;i++)fixed.sample(100,2000+i*100,'720p');assert.deepEqual(fixed.dimensions(1920,1080,2),initial);console.log('PASS default fixed 720p remains stable under sustained GPU pressure');

const crisp=new BrowserPresentation(),balanced=crisp.dimensions(1920,1080,1.5,'Balanced','Auto',16384);assert.deepEqual([balanced.uiWidth,balanced.uiHeight],[1920,1080]);
const floor=new BrowserPresentation();for(let i=0;i<40;i++){const size=floor.dimensions(3840,2160,2,'Balanced','Auto',16384);floor.sample(100,5000+i*1100,'Auto',0,i,size.worldWidth,size.worldHeight);}assert.equal(floor.factor,.5);
console.log('PASS Balanced UI avoids supersampling and Auto preserves a 50% readability floor');
