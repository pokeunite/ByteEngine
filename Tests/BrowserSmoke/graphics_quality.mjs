import assert from 'node:assert/strict';
import {BrowserPresentation} from '../../Player/ByteEngine.Browser/wwwroot/graphics.js';
const p=new BrowserPresentation();
let size=p.dimensions(1920,1080,2,'Fast','Auto');
assert.equal(size.worldWidth,1280);assert.equal(size.uiWidth,3840);
size=p.dimensions(1920,1080,2,'Balanced','Auto');assert.equal(size.worldWidth,1920);assert.equal(size.uiWidth,3840);
size=p.dimensions(1920,1080,2,'High','Auto');assert.equal(size.worldWidth,2560);
size=p.dimensions(1920,1080,2,'High','Native');assert.equal(size.worldWidth,3840);
size=p.dimensions(1920,1080,2,'High','50%');assert.equal(size.worldWidth,1920);assert.equal(size.uiWidth,3840);
p.dimensions(1920,1080,2,'Balanced','Auto');
for(let i=0;i<180;i++)p.sample(30,6000,'Auto');assert.equal(p.factor,.9);
size=p.dimensions(1920,1080,2,'Balanced','Auto');assert.equal(size.worldWidth,1728);assert.equal(size.uiWidth,3840);
for(let i=0;i<180;i++)p.sample(5,12000,'Auto');assert.ok(Math.abs(p.factor-.95)<1e-9);
for(let i=0;i<500;i++){p.sample(null,18000,'Auto');p.sample(90,18000,'Auto',1);p.sample(90,18000,'Native');}
assert.ok(Math.abs(p.factor-.95)<1e-9);
for(const [w,h,dpr] of [[800,600,1],[2560,1080,1.25],[3840,2160,2]]){
 size=p.dimensions(w,h,dpr,'High','Auto',4096);assert.ok(size.worldWidth<=4096&&size.uiWidth<=4096);assert.ok(Math.abs(size.worldWidth/size.worldHeight-w/h)<.005);
}
console.log('PASS independent sharp UI, quality budgets, explicit resolution, GPU adaptation, upload/unsupported-timer safeguards and aspect ratios');
