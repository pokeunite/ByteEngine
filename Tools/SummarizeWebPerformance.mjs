import {readFileSync,writeFileSync} from 'node:fs';
import {gunzipSync} from 'node:zlib';
export function summarizeCapture(report){
    if(report.format!=='ByteEngine-performance'||!Array.isArray(report.frames))throw Error('Not a ByteEngine performance recording.');
    const mean=values=>values.length?values.reduce((a,b)=>a+b,0)/values.length:null;
    const start=report.frames[0]?.t??-Infinity,end=report.frames.at(-1)?.t??Infinity;
    const samples=(report.samples??[]).filter(s=>(!Number.isFinite(s.t)||s.t>=start&&s.t<=end)&&
        (!Number.isFinite(s.firstFrameId)||!Number.isFinite(report.frames[0]?.id)||s.firstFrameId>=report.frames[0].id));
    const phaseKeys=['managedMs','parseMs','audioMs','videoMs','audioPositionMs','renderMs','gpuMs','inputMs','updateStepMs','renderPreparationMs','uploadMs','uiCpuMs','shadowCpuMs','captureOverheadMs','samplingMs'];
    function group(rows){const values=rows.filter(f=>f.visible!==false&&f.intervalMs>0).map(f=>f.intervalMs).sort((a,b)=>a-b);
        return {frames:rows.length,fps:values.length?1000/mean(values):null,p95Ms:values.length?values[Math.floor((values.length-1)*.95)]:null,
            phases:Object.fromEntries(phaseKeys.map(k=>[k,mean(rows.map(f=>f[k]).filter(Number.isFinite))])),
            meanRenderAllocatedBytes:mean(rows.map(f=>f.renderAllocatedBytes).filter(Number.isFinite)),
            meanSceneAllocatedBytes:mean(rows.map(f=>f.sceneMetrics?.AllocatedBytes).filter(Number.isFinite)),
            meanMeshUploadBytes:mean(rows.map(f=>f.meshUploadBytes).filter(Number.isFinite))};}
    const numbers=key=>report.frames.map(f=>f[key]).filter(Number.isFinite);
    const phases=Object.fromEntries(['managedMs','parseMs','audioMs','videoMs','audioPositionMs','renderMs','gpuMs','inputMs','updateStepMs','renderPreparationMs','uploadMs','uiCpuMs','shadowCpuMs','captureOverheadMs','samplingMs'].map(key=>[key,mean(numbers(key))]));
    const components=new Map(),objects=new Map(),systems=new Map();
    function accumulate(map,key,row,frames){const value=map.get(key)||{name:key,samples:0,profiledFrames:0,totalMs:0,calls:0,allocatedBytes:0,slowestMs:0};
        value.samples++;value.profiledFrames+=frames;value.totalMs+=row.TotalMs;value.calls+=row.Calls;value.allocatedBytes+=row.AllocatedBytes||0;value.slowestMs=Math.max(value.slowestMs,row.SlowestMs||0);map.set(key,value);}
    for(const sample of samples){if(!sample.deep)continue;const profiler=sample.core?.profiler;
        const count=Math.max(1,profiler?.frames??1);
        for(const row of profiler?.components??[])accumulate(components,row.Component+' / '+row.Phase,row,count);
        for(const row of profiler?.objects??[])accumulate(objects,row.Object+' ['+row.Id+'] / '+row.Component+' / '+row.Phase,row,count);
        for(const row of profiler?.systems??[])accumulate(systems,row.Category+' / '+row.Name,row,count);
    }
    const top=map=>[...map.values()].sort((a,b)=>b.totalMs-a.totalMs).slice(0,30).map(v=>({...v,meanSampleMs:v.totalMs/v.samples,meanProfiledFrameMs:v.totalMs/v.profiledFrames}));
    const warnings=[];
    if(!(report.samples??[]).some(s=>s.deep))warnings.push('No deep samples: callback/system attribution is unavailable.');
    if(report.frames.some(f=>f.deep))warnings.push('Deep frames include profiling overhead. Compare normal/deep summaries separately.');
    if(!numbers('gpuMs').length)warnings.push('GPU timing unavailable; CPU timings cannot establish a GPU bottleneck.');
    if(report.probes?.skipGeometry||report.probes?.skipUi||report.frames.some(f=>f.skipGeometry||f.skipUi))warnings.push('A rendering diagnostic probe was active; inspect per-frame probe flags before comparing gameplay performance.');
    if(report.droppedFrames||report.droppedEvents||report.droppedSamples)warnings.push('Recording rings wrapped; inspect dropped counters before inferring full-session history.');
    const evidence=[];
    if(phases.managedMs>20)evidence.push('Managed simulation/render preparation/encoding consumes a substantial CPU budget. Use phase and callback samples to narrow it down.');
    if(phases.gpuMs>25)evidence.push('Measured world/shadow GPU work is near or above a 30 FPS budget; inspect resolution, geometry and shadow counts.');
    if(phases.uploadMs>3)evidence.push('Recurring mesh/texture uploads have meaningful CPU submission cost. Inspect upload bytes and asset residency over time.');
    const first=samples[0]?.core?.memory,last=samples.at(-1)?.core?.memory;
    return {summary:report.summary,modeSummaries:report.modeSummaries,build:report.build,environment:report.environment,hardware:report.hardware,
        phases,evidence,warnings,measurementWindow:{startMs:start,endMs:end,samplesAnalyzed:samples.length,olderOrPartialSamplesExcluded:(report.samples?.length??0)-samples.length},
        groups:{normal:group(report.frames.filter(f=>!f.deep)),deep:group(report.frames.filter(f=>f.deep)),
            capturedNormal:group(report.frames.filter(f=>!f.deep&&f.captured&&!f.skipGeometry&&!f.skipUi)),
            capturedDeep:group(report.frames.filter(f=>f.deep&&f.captured&&!f.skipGeometry&&!f.skipUi))},
        componentCosts:top(components),objectCosts:top(objects),systemCosts:top(systems),
        memoryDelta:first&&last?{managedBytes:last.managedBytes-first.managedBytes,totalAllocatedBytes:last.totalAllocatedBytes-first.totalAllocatedBytes,gc0:last.gc0-first.gc0,gc1:last.gc1-first.gc1,gc2:last.gc2-first.gc2}:null,
        events:(report.events??[]).reduce((result,e)=>(result[e.kind]=(result[e.kind]||0)+1,result),{}),
        notes:['Rankings use complete profile windows within the retained frame window; older or partial windows are excluded.',
            'Captured groups mean mouse-locked frames, a useful active-play proxy for this game, not universal gameplay detection.',
            'Rankings sum sampled inclusive costs, not full-session totals or exclusive percentages.','Observations are evidence to investigate, not proof of a unique root cause.']};
}
if(process.argv[1]?.endsWith('SummarizeWebPerformance.mjs')){
    const file=process.argv[2];if(!file)throw Error('Usage: node Tools/SummarizeWebPerformance.mjs <report.json[.gz]> [summary.json]');
    const bytes=readFileSync(file),report=JSON.parse((file.endsWith('.gz')?gunzipSync(bytes):bytes).toString('utf8'));
    const output=JSON.stringify(summarizeCapture(report),null,2);if(process.argv[3])writeFileSync(process.argv[3],output);else console.log(output);
}
