const http=require('http'),fs=require('fs'),path=require('path'),{spawn}=require('child_process'),readline=require('readline');
const {chromium}=require(process.env.BYTEENGINE_PLAYWRIGHT||'../.artifacts/network-tools/node_modules/playwright');
(async()=>{
 const server=http.createServer((req,res)=>{res.setHeader('Content-Type','text/javascript');res.end(fs.readFileSync(path.join(__dirname,'../Player/ByteEngine.Browser/wwwroot/net.js')));});
 await new Promise(r=>server.listen(0,'127.0.0.1',r));const port=server.address().port;
 const browser=await chromium.launch({channel:'msedge',headless:true});
 try{
 for(const nativeOffers of [true,false]){
  const child=spawn('dotnet',[path.join(__dirname,'../Tests/ByteEngine.Network.Integration/bin/Release/net9.0/ByteEngine.Network.Integration.dll'),'--bridge',...(nativeOffers?['--offer']:[])],{windowsHide:true,stdio:['pipe','pipe','pipe']});
  let errors='',received=0;child.stderr.on('data',d=>errors+=d);
  const page=await browser.newPage();const pending=[];let ready=false;
  readline.createInterface({input:child.stdout}).on('line',line=>{try{const m=JSON.parse(line);if(m.type==='signal'){const s={kind:m.signal.Kind,content:m.signal.Content,mid:m.signal.Mid};if(ready)page.evaluate(s=>window.link.accept(s),s).catch(e=>errors+=e);else pending.push(s);}else if(m.type==='received')received++;}catch(e){errors+=line;}});
  await page.exposeFunction('signalNative',s=>child.stdin.write(JSON.stringify({Kind:s.kind,Content:s.content,Mid:s.mid})+'\n'));
  await page.goto(`http://127.0.0.1:${port}/`);
  await page.evaluate(async({port,offer})=>{const {ByteNetworkLink}=await import(`http://127.0.0.1:${port}/net.js`);window.echoes=0;window.link=new ByteNetworkLink({onSignal:s=>window.signalNative(s),onMessage:(kind,bytes)=>{if(bytes.length!==36||bytes[0]!==42)throw Error('corrupt echo');window.echoes++;},onDisconnect:r=>window.error=r});window.timer=setInterval(()=>window.link.poll(),2);await window.link.start(offer);},{port,offer:!nativeOffers});ready=true;
  for(const s of pending)await page.evaluate(s=>window.link.accept(s),s);
  try{
   await page.waitForFunction(()=>window.link.connected,null,{timeout:20000});
   await page.evaluate(()=>{const b=new Uint8Array(36);b[0]=42;if(!window.link.send(0,b)||!window.link.send(1,b))throw Error('send failed');});
   await page.waitForFunction(()=>window.echoes===2,null,{timeout:10000});
   if(received!==2||errors)throw Error(`Native received=${received}, errors=${errors}`);
   console.log(`PASS actual native/browser WebRTC ${nativeOffers?'native':'browser'} offer: reliable+unreliable binary echo`);
  }finally{await page.close();child.stdin.write('quit\n');await new Promise(r=>{child.once('exit',r);setTimeout(()=>{child.kill();r();},2000).unref();});if(child.exitCode!==0)throw Error('Native process shutdown failed: '+child.exitCode);}
 }
 }finally{await browser.close();await new Promise(r=>server.close(r));}
})().catch(e=>{console.error(e);process.exitCode=1;});
