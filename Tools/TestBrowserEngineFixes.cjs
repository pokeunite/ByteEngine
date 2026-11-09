const fs=require('fs'),http=require('http'),path=require('path'),assert=require('assert');
const {chromium}=require(process.env.BYTEENGINE_PLAYWRIGHT||'playwright');
const source=JSON.parse(fs.readFileSync('.byteengine-build-source.json')).SourceRoot;
const root=path.join(source,'Player/ByteEngine.Browser/wwwroot');
const html=`<canvas id="g" width="64" height="64"></canvas><canvas id="u" width="64" height="64"></canvas><script type="module">
import {createBrowserRenderer} from './render-worker-client.js';import {BrowserRenderer} from './renderer.js';import {BrowserPresentation} from './graphics.js';
try{
const r=new BrowserRenderer(document.querySelector('#g'),document.querySelector('#u')),I=[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1];
const pixels=new Uint8Array([255,0,0,255]);r.readBuffer=()=>pixels;
const base={quality:'Fast',vp:I,inverseVp:I,eye:[0,0,2],background:[0,0,0],ambient:1,exposure:1,look:[1,1,0],lights:[],points:[],draws:[],uploads:[],materials:[],deadMaterials:[],deadMeshes:[],deadTextures:[],ui:[]};
const upload=ui=>({id:1,width:1,height:1,x:0,y:0,w:1,h:1,allocate:true,partial:true,nearest:true,hdr:false,pixels:0,ui});
r.draw({...base,textures:[upload(false)]},64,64);if(r.textures.get(1).image!==null)throw Error('World-only texture allocated a Canvas');
r.draw({...base,textures:[upload(true)],ui:[{kind:'image',texture:1,x:0,y:0,w:64,h:64,sx:0,sy:0,sw:1,sh:1,color:[1,1,1,1]}]},64,64);
const color=r.ui.getImageData(32,32,1,1).data;if(color[0]!==255||color[3]!==255)throw Error('UI promotion pixels failed');
const imageCommand=()=>[{kind:'image',texture:1,x:0,y:0,w:64,h:64,sx:0,sy:0,sw:1,sh:1,color:[1,1,1,1]}];
r.draw({...base,textures:[],ui:imageCommand()},64,64);if(r.profile.uiSkips!==1)throw Error('Unchanged UI was redrawn');
pixels.set([0,255,0,255]);r.draw({...base,textures:[upload(true)],ui:imageCommand()},64,64);const updated=r.ui.getImageData(32,32,1,1).data;if(updated[1]!==255||updated[0]!==0)throw Error('UI cache retained stale texture pixels');
r.draw({...base,textures:[],ui:imageCommand()},64,64);if(r.profile.uiSkips!==2)throw Error('Updated UI was not cached');
if(r.gl.getContextAttributes().antialias)throw Error('Unneeded default framebuffer MSAA');
const p=new BrowserPresentation();p.dimensions(1920,1080,1,'Balanced','Auto');for(let i=0;i<20;i++)p.sample(80,2000+i*20,'Auto',0,1,1920,1080);if(p.factor!==1)throw Error('Duplicate GPU sample adapted');
for(let i=2;i<10;i++)p.sample(80,3000+i*20,'Auto',0,i,1920,1080);if(p.factor!==.75)throw Error('Fresh samples did not adapt');
p.dimensions(800,450,1,'Balanced');for(let i=10;i<30;i++)p.sample(80,5000+i*20,'Auto',0,i,1920,1080);if(p.factor!==1)throw Error('Old-size sample adapted');
const world=document.createElement('canvas'),overlay=document.createElement('canvas');document.body.append(world,overlay);const worker=await createBrowserRenderer(world,overlay);worker.setSize({worldWidth:64,worldHeight:64,uiWidth:64,uiHeight:64});worker.readBuffer=()=>new Uint8Array(0);for(let i=0;i<3;i++){worker.draw({...base,textures:[],uiBuffer:0,uiCount:0,bufferViews:[0,0]},64,64);await worker.idle();}const report=await worker.report();if(report.error)throw Error('Worker GPU backpressure failed');worker.close();window.result={workerFrames:3,worldCanvasSkipped:true,uiPromotionPixels:true,duplicateSamplesRejected:true,oldSizeRejected:true,freshSamplesAdapt:true,error:r.gl.getError()};
}catch(e){window.failure=String(e.stack||e);}
</script>`;
const server=http.createServer((req,res)=>{if(req.url==='/'){res.setHeader('Content-Type','text/html');return res.end(html);}const file=path.resolve(root,'.'+req.url);if(!file.startsWith(root+path.sep)){res.writeHead(403);return res.end();}fs.readFile(file,(e,b)=>{res.statusCode=e?404:200;res.setHeader('Content-Type','application/javascript');res.end(e?'Missing':b);});});
(async()=>{let browser;try{await new Promise(r=>server.listen(0,'127.0.0.1',r));browser=await chromium.launch({channel:'chrome',headless:true});const page=await browser.newPage();await page.goto('http://127.0.0.1:'+server.address().port);await page.waitForFunction(()=>window.result||window.failure);const result=await page.evaluate(()=>window.result||{failure:window.failure});assert(!result.failure,result.failure);assert.equal(result.error,0);console.log('PASS '+JSON.stringify(result));}catch(e){console.error(e);process.exitCode=1;}finally{await browser?.close();server.close();}})();
