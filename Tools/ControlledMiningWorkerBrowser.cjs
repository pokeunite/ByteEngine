// Isolated experiment: compare the same scene with sequential and worker GL submission.
const fs=require('fs'),http=require('http'),path=require('path'),crypto=require('crypto');
const {chromium}=require(process.env.BYTEENGINE_PLAYWRIGHT||'playwright');
const [site,runtime,override,out]=process.argv.slice(2).map(p=>path.resolve(p));fs.mkdirSync(out,{recursive:true});
const warmup=Number(process.env.BYTEENGINE_WARMUP||600),measured=Number(process.env.BYTEENGINE_MEASURED||300);
const production=process.env.BYTEENGINE_PRODUCTION_RENDERER==='1';
const boot=JSON.parse(fs.readFileSync(path.join(runtime,'_framework/blazor.boot.json'))),native=Object.keys(boot.resources.fingerprinting).find(n=>/^dotnet\.native.*\.wasm$/.test(n));
const runtimeSha256=crypto.createHash('sha256').update(fs.readFileSync(path.join(runtime,'_framework',native))).digest('hex').toUpperCase();
const html=`<!doctype html><style>body{margin:0}canvas{position:absolute;inset:0;width:1280px;height:720px}#ui{pointer-events:none}</style><canvas id="game" width="1280" height="720"></canvas><canvas id="ui" width="1280" height="720"></canvas><div id="status"></div><script type="module">
import {dotnet} from './_framework/dotnet.js';import {loadContent} from './content.js';import {BrowserRenderer} from './renderer.js';import {${production?'createBrowserRenderer':'createThreadedRenderer'}} from './render-worker-client.js';import {createFrameReader} from './frame-buffers.js';import {decodeUiPacket} from './frame-wire.js';import {BrowserAudio} from './audio.js';
try{
const rt=await dotnet.create();await rt.runMain();const game=(await rt.getAssemblyExports(rt.getConfig().mainAssemblyName)).BrowserGame;
const content=await loadContent(game,document.querySelector('#status'));game.Start(false);
const params=new URLSearchParams(location.search),count=+params.get('count'),threaded=params.get('threaded')==='1';
game.ConfigurePresentation(JSON.stringify({quality:'High',worldResolution:'Native'}));game.ConfigureBenchmark(count,'baseline');game.CaptureProfileComponents(false);
const world=document.querySelector('#game'),ui=document.querySelector('#ui');const renderer=threaded?await ${production?'createBrowserRenderer':'createThreadedRenderer'}(world,ui):new BrowserRenderer(world,ui);
game.ConfigureSkinning(renderer.maximumSkinningBones);const transfers=createFrameReader(rt,game);renderer.readBuffer=i=>transfers.read(i);
const audio=new BrowserAudio(content.audio,id=>game.AudioEnded(id));await audio.activate();
let index=0,scheduled=false,finishing=false;const rows=[],completed=[];const channel=new MessageChannel();
if(threaded&&${production}){renderer.oncompleted=frame=>completed.push(frame);renderer.setSize({worldWidth:1280,worldHeight:720,uiWidth:1280,uiHeight:720});}
function schedule(){if(!scheduled&&!finishing){scheduled=true;channel.port2.postMessage(0);}}
channel.port1.onmessage=()=>{scheduled=false;tick();};if(threaded)renderer.onavailable=schedule;
async function finish(){try{
finishing=true;if(threaded)await renderer.idle();const frames=threaded&&!${production}?renderer.completed:completed;
const byId=new Map(frames.map(f=>[f.id,f]));const selected=rows.filter(r=>r.id>${warmup}&&r.id<=${warmup+measured});
for(const row of selected){const f=byId.get(row.id),previous=byId.get(row.id-1);row.intervalMs=f.finishMs-previous.finishMs;row.renderMs=f.renderMs;row.profile=f.profile;}
const report=threaded?await renderer.report():{diagnostics:renderer.diagnostics(false),error:renderer.gl.getError()};
const gl=threaded?null:renderer.gl,e=gl?.getExtension('WEBGL_debug_renderer_info');
window.result={count,threaded,hardware:threaded?renderer.hardware.renderer:gl.getParameter(e?e.UNMASKED_RENDERER_WEBGL:gl.RENDERER),rows:selected,details:JSON.parse(game.PerformanceDetails()),report};
channel.port1.close();channel.port2.close();
}catch(e){window.failure=String(e.stack||e);}}
function tick(){try{
if(threaded&&!renderer.canAcceptFrame)return;
const start=performance.now(),frame=JSON.parse(game.Frame(.016,1280,720,'','',.5,.5,0,0,0,true,false,''));if(game.LastFrameError())throw Error(game.LastFrameError());transfers.beginFrame(frame);
const managedEnd=performance.now();audio.commands(frame.audio);if(!threaded)frame.ui=decodeUiPacket(transfers.read(frame.uiBuffer),frame.uiCount);
const drawStart=performance.now();renderer.draw(frame,1280,720);const end=performance.now();const id=++index;
rows.push({id,frameMs:end-start,managedMs:managedEnd-start,enqueueMs:end-drawStart,renderPreparationMs:frame.renderPreparationMs,sceneMetrics:frame.sceneMetrics});
if(!threaded)completed.push({id,finishMs:performance.timeOrigin+end,renderMs:end-drawStart,profile:{...renderer.profile}});
if(index===${warmup+measured})game.CaptureProfileComponents(true);
if(index<${warmup+measured+30})schedule();else finish();
}catch(e){finishing=true;window.failure=String(e.stack||e);}}
schedule();
}catch(e){window.failure=String(e.stack||e);}
</script>`;
const server=http.createServer((req,res)=>{
 const name=decodeURIComponent(req.url.split('?')[0]);if(name==='/bench.html'){res.setHeader('Content-Type','text/html');return res.end(html);}if(name==='/favicon.ico'){res.writeHead(204);return res.end();}
 const base=name.startsWith('/Game.bytepak')||name==='/web-game.json'?site:fs.existsSync(path.resolve(override,'.'+name))?override:runtime;
 const file=path.resolve(base,'.'+name);if(!file.startsWith(base+path.sep)){res.writeHead(403);return res.end();}
 res.setHeader('Content-Type',({'.js':'application/javascript','.json':'application/json','.wasm':'application/wasm'})[path.extname(file)]||'application/octet-stream');fs.readFile(file,(e,b)=>{res.statusCode=e?404:200;res.end(e?'Missing':b);});
});
(async()=>{let browser;try{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));browser=await chromium.launch({channel:'chrome',headless:true,args:['--autoplay-policy=no-user-gesture-required']});
 const cases=JSON.parse(process.env.BYTEENGINE_CASES||'[[100,false],[100,true]]');
 for(const [count,threaded] of cases){const page=await browser.newPage({viewport:{width:1280,height:720}});page.on('pageerror',e=>console.error(String(e)));await page.goto('http://127.0.0.1:'+server.address().port+'/bench.html?count='+count+'&threaded='+(threaded?1:0));await page.waitForFunction(()=>window.result||window.failure,null,{timeout:300000});const result=await page.evaluate(()=>window.result||{failure:window.failure});Object.assign(result,{runtimeSha256,capturedAt:new Date().toISOString(),benchmark:{warmup,measured,quality:'High',worldResolution:'Native',scheduling:'bounded MessageChannel'}});const name=count+(threaded?'-worker':'-sequential');fs.writeFileSync(path.join(out,name+'.json'),JSON.stringify(result,null,2));if(result.failure)throw Error(result.failure);const mean=k=>result.rows.reduce((s,r)=>s+r[k],0)/result.rows.length;console.log(JSON.stringify({count,threaded,presentedFps:1000/mean('intervalMs'),frameMs:mean('frameMs'),managedMs:mean('managedMs'),renderMs:mean('renderMs'),enqueueMs:mean('enqueueMs'),glError:result.report.error}));await page.screenshot({path:path.join(out,name+'.png')});await page.close();}
}catch(e){console.error(e);process.exitCode=1;}finally{await browser?.close();server.close();}})();
