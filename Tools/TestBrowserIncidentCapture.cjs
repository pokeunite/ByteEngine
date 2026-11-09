const fs=require('fs'),path=require('path'),http=require('http'),assert=require('assert/strict');
const {chromium}=require(process.env.BYTEENGINE_PLAYWRIGHT||'playwright');
const server=http.createServer((req,res)=>{if(req.url==='/'){res.setHeader('Content-Type','text/html');return res.end('<body><script type="module">import{createPerformanceCapture}from"/performance.js";window.capture=createPerformanceCapture();</script>');}res.setHeader('Content-Type','application/javascript');res.end(fs.readFileSync(path.resolve('Player/ByteEngine.Browser/wwwroot/performance.js')));});
(async()=>{let browser;try{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));browser=await chromium.launch({channel:'chrome',headless:true});const page=await browser.newPage();await page.goto('http://127.0.0.1:'+server.address().port);await page.waitForFunction(()=>window.capture);
 const result=await page.evaluate(()=>{
  const frame=()=>capture.frame({frameMs:16,intervalMs:16,visible:true},()=>({scene:'fixture'}));
  for(let i=0;i<150;i++)frame();capture.trigger('health-death',{callbackMs:12});for(let i=0;i<120;i++)frame();
  const first=capture.report().incidents[0];
  for(let j=0;j<6;j++){capture.trigger('resize-test');for(let i=0;i<120;i++)frame();}
  const bounded=capture.report();window.dispatchEvent(new Event('resize'));capture.failure({message:'synthetic failure'});const partial=capture.report();
  return{before:first.before.length,after:first.after.length,complete:first.complete,kind:first.triggers[0].kind,callback:first.triggers[0].callbackMs,bounded:bounded.incidents.length,partial:partial.incidents.at(-1),recording:partial.recording};
 });
 assert.equal(result.before,120);assert.equal(result.after,120);assert.equal(result.complete,true);assert.equal(result.kind,'health-death');assert.equal(result.callback,12);assert.equal(result.bounded,4);assert.equal(result.partial.complete,false);assert.equal(result.recording,false);
 console.log('PASS bounded before/after incident windows, death timing, resize trigger and partial failure recording');
 }finally{await browser?.close();server.close();}})().catch(e=>{console.error(e);process.exitCode=1});
