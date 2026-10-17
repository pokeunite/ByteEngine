const fs=require('fs'),assert=require('assert');
(async()=>{
 const root='Player/ByteEngine.Browser/wwwroot/';
 const {qualityProfiles}=await import('data:text/javascript;base64,'+fs.readFileSync(root+'shared-render.js').toString('base64'));
 const source=fs.readFileSync(root+'graphics.js','utf8').replace("import {qualityProfiles} from './shared-render.js';",'const qualityProfiles='+JSON.stringify(qualityProfiles)+';');
 const {BrowserPresentation}=await import('data:text/javascript;base64,'+Buffer.from(source).toString('base64'));
 for(const quality of ['Fast','Balanced','High']){
  const p=new BrowserPresentation();let size;
  for(let i=0;i<2000;i++){size=p.dimensions(1280,720,1,quality);p.sample(40,10000+i*17,'Auto');}
  assert.equal(size.worldWidth,1280);assert.equal(size.worldHeight,720);assert.equal(size.uiWidth,1280);
 }
 const p=new BrowserPresentation();let size;
 for(let i=0;i<2000;i++){size=p.dimensions(1920,1080,1,'Balanced');p.sample(40,10000+i*17,'Auto');}
 assert.equal(size.worldWidth,1280);assert.equal(size.worldHeight,720);assert.equal(size.uiWidth,1920);
 size=p.dimensions(800,450,1,'Balanced');assert.equal(size.worldWidth,800);assert.equal(size.uiWidth,800);
 size=p.dimensions(1920,1080,1,'Balanced','Native');assert.equal(size.worldWidth,1920);
 size=p.dimensions(1280,720,1,'Fast','50%');assert.equal(size.worldWidth,640);
 size=p.dimensions(1280,720,1,'High','Native',512);assert(size.worldWidth<=512&&size.uiWidth<=512);
 console.log('PASS Auto clarity floor, large-window adaptation, UI independence, explicit resolution and GPU limits.');
})().catch(e=>{console.error(e);process.exitCode=1;});
