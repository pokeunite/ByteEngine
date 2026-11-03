const http=require('http'),fs=require('fs'),path=require('path'),assert=require('assert/strict');
const {chromium}=require(process.env.BYTEENGINE_PLAYWRIGHT||'playwright');
const root=path.resolve('Player/ByteEngine.Browser/wwwroot');
const html=`<!doctype html><canvas id="world"></canvas><canvas id="ui"></canvas><script type="module">
import {createBrowserRenderer} from './render-worker-client.js';
try{
 const world=document.querySelector('#world'),ui=document.querySelector('#ui'),mode=new URLSearchParams(location.search).get('mode');
 let transferred=0;const original=world.transferControlToOffscreen.bind(world);world.transferControlToOffscreen=()=>{transferred++;return original();};
 if(mode==='unsupported')world.transferControlToOffscreen=undefined;
 if(mode==='probe-error')window.Worker=class{postMessage(){queueMicrotask(()=>this.onmessage({data:{kind:'error',message:'Simulated unavailable worker graphics'}}));}terminate(){}};
 const renderer=await createBrowserRenderer(world,ui);renderer.setSize({worldWidth:320,worldHeight:180,uiWidth:640,uiHeight:360});
 const report=renderer.isThreaded?await renderer.report():{error:renderer.gl.getError(),diagnostics:renderer.diagnostics()};
 window.result={threaded:renderer.isThreaded,transferred,error:report.error};
}catch(e){window.failure=String(e.stack||e);}
</script>`;
const server=http.createServer((req,res)=>{if(req.url.startsWith('/test?')){res.setHeader('Content-Type','text/html');return res.end(html);}const file=path.resolve(root,'.'+req.url);if(!file.startsWith(root+path.sep)){res.writeHead(403);return res.end();}fs.readFile(file,(e,b)=>{res.statusCode=e?404:200;res.setHeader('Content-Type','application/javascript');res.end(e?'Missing':b);});});
(async()=>{let browser;try{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));browser=await chromium.launch({channel:'chrome',headless:true});
 for(const mode of ['supported','unsupported','probe-error']){const page=await browser.newPage();await page.goto('http://127.0.0.1:'+server.address().port+'/test?mode='+mode);await page.waitForFunction(()=>window.result||window.failure,null,{timeout:90000});const result=await page.evaluate(()=>window.result||{failure:window.failure});assert(!result.failure,result.failure);assert.equal(result.error,0);assert.equal(result.threaded,mode==='supported');assert.equal(result.transferred,mode==='supported'?1:0);console.log('PASS worker compatibility '+mode+': '+JSON.stringify(result));await page.close();}
 }catch(e){console.error(e);process.exitCode=1;}finally{await browser?.close();server.close();}})();
