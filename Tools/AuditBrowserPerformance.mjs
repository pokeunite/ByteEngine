// Read-only engine audit: no build, export, or gameplay modifications.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const root=process.cwd(), configured=JSON.parse(fs.readFileSync('.byteengine-build-source.json')).SourceRoot;
const installed=path.join(root,'Dist/ByteEngine/BrowserRuntime');
const hash=p=>fs.existsSync(p)?crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex'):null;
const files=['main.js','graphics.js','renderer.js','performance.js','render-worker.js','render-worker-client.js','packed-frame.js'];
const identity=files.map(file=>({file,local:hash(path.join(root,'Player/ByteEngine.Browser/wwwroot',file)),maintained:hash(path.join(configured,'Player/ByteEngine.Browser/wwwroot',file)),installed:hash(path.join(installed,file))}));
const {BrowserPresentation}=await import('data:text/javascript;base64,'+fs.readFileSync(path.join(installed,'graphics.js')).toString('base64'));
const dimensions=[];
for(const [width,height,dpr] of [[640,360,1.5],[1920,1080,1.5],[3840,2160,2]])for(const quality of ['Fast','Balanced','High'])for(const resolution of ['Auto','Native']){
 const p=new BrowserPresentation(),size=p.dimensions(width,height,dpr,quality,resolution,16384);
 dimensions.push({quality,resolution,dpr,...size,pixels:size.worldWidth*size.worldHeight});
}
const stale=new BrowserPresentation();stale.dimensions(1920,1080,1,'Balanced','Auto');
for(let i=0;i<8;i++)stale.sample(80,2000+i*16,'Auto');
const missing=new BrowserPresentation();missing.dimensions(1920,1080,1,'Balanced','Auto');
for(let i=0;i<600;i++)missing.sample(null,2000+i*16,'Auto');
const stat=a=>{a=a.filter(x=>typeof x==='number'&&Number.isFinite(x)).sort((a,b)=>a-b);return a.length?{n:a.length,mean:a.reduce((a,b)=>a+b,0)/a.length,p50:a[Math.floor(a.length*.5)],p95:a[Math.min(a.length-1,Math.floor(a.length*.95))],max:a.at(-1)}:null;};
const historical=[];
for(const stage of ['embedded-Auto','fullscreen-720p','fullscreen-auto','fullscreen-native']){
 const file=path.join(configured,'artifacts/fullscreen-clean-fast-auto',stage+'.json');
 if(!fs.existsSync(file))continue;
 const d=JSON.parse(fs.readFileSync(file)),ids=new Set(d.rows.map(r=>r.id)),frames=d.capture.frames.filter(f=>ids.has(f.renderSubmittedId));
 const unique=new Map();for(const r of d.rows)if(r.profile.gpuRenderFrame!=null)unique.set(r.profile.gpuRenderFrame,r.profile.gpuMs);
 historical.push({file,stage,workerSubmissionFps:d.fps,presentation:d.presentation,rows:d.rows.length,
  uniqueGpuSamples:unique.size,gpuMsUnique:stat([...unique.values()]),gpuAgeFrames:stat(d.rows.map(r=>r.profile.gpuSampleAgeFrames)),
  workerCpuMs:stat(d.rows.map(r=>r.renderMs)),mainManagedMs:stat(frames.map(f=>f.managedMs)),renderPreparationMs:stat(frames.map(f=>f.renderPreparationMs)),
  materialChanges:stat(d.rows.map(r=>r.profile.materialChanges)),drawCalls:stat(d.rows.map(r=>r.profile.drawCalls)),triangles:stat(d.rows.map(r=>r.profile.triangles)),
  meshUploadBytes:stat(d.rows.map(r=>r.profile.meshUploadBytes)),renderer:d.renderer.diagnostics});
}
const output={configuredSource:configured,identity,dimensions,probes:{eightReadsOfSame80msValue:{factor:stale.factor,note:'API accepts eight repetitions as eight samples; call site does not pass gpuRenderFrame.'},missingGpuTimer:{factor:missing.factor}},historical};
const out=path.join(root,'artifacts/browser-engine-audit-2026-11-04');fs.mkdirSync(out,{recursive:true});fs.writeFileSync(path.join(out,'evidence.json'),JSON.stringify(output,null,2));
console.log(JSON.stringify({identity:identity.map(x=>({file:x.file,localMatchesInstalled:x.local===x.installed,maintainedMatchesInstalled:x.maintained===x.installed})),probes:output.probes,historical:historical.map(({renderer,file,...rest})=>rest)},null,2));
