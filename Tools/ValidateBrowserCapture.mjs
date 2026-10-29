import {readFileSync, writeFileSync} from 'node:fs';
import {pathToFileURL} from 'node:url';

// Correctness and speed are independent gates. A running game can still fail release validation.
export function assessCapture(capture, {minFps=30, maxP95Ms=50}={}) {
    const errors=[];
    const frames=capture.perf ?? [];
    const intervals=frames.map(f=>f.intervalMs).filter(v=>Number.isFinite(v)&&v>0).sort((a,b)=>a-b);
    if(intervals.length<30)errors.push('At least 30 steady-state frame intervals are required.');
    if(frames.some(f=>f.pending>0))errors.push('Assets were still uploading during the measurement.');
    const mean=intervals.length?intervals.reduce((a,b)=>a+b,0)/intervals.length:null;
    const fps=mean===null?null:1000/mean;
    const p95Ms=intervals.length?intervals[Math.ceil(intervals.length*.95)-1]:null;
    if(fps!==null&&fps<minFps)errors.push(`Frame rate ${fps.toFixed(1)} FPS is below ${minFps} FPS.`);
    if(p95Ms!==null&&p95Ms>maxP95Ms)errors.push(`95th percentile frame interval ${p95Ms.toFixed(1)} ms exceeds ${maxP95Ms} ms.`);
    if((capture.runtimeErrors??[]).length)errors.push(...capture.runtimeErrors.map(e=>'Runtime error: '+e));
    for (const event of capture.capture?.events ?? [])
        if (event.kind === 'error' || event.kind === 'diagnostic-error')
            errors.push('Captured runtime failure: ' + (event.message ?? JSON.stringify(event)));
    if(capture.frame?.pendingTextures>0)errors.push('Texture uploads have not completed.');
    const meanField=key=>{const values=frames.map(f=>f[key]).filter(Number.isFinite);return values.length?values.reduce((a,b)=>a+b,0)/values.length:null;};
    return {passed:errors.length===0, limits:{minFps,maxP95Ms}, measured:{frames:intervals.length,fps,p95Ms,
        managedMs:meanField('managedMs'),renderMs:meanField('renderMs')},errors,
        notes:['This gate validates captured timings and reported errors; it does not replace gameplay or visual comparison.',
            'Capture a foreground, warmed-up game on real hardware without a concurrent compiler running.']};
}

if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href) {
    const file=process.argv[2];
    if(!file)throw Error('Usage: node Tools/ValidateBrowserCapture.mjs <game.json> [minimum-fps]');
    const minFps=process.argv[3]===undefined?30:Number(process.argv[3]);
    if(!Number.isFinite(minFps)||minFps<=0)throw Error('Minimum FPS must be a positive finite number.');
    const report=assessCapture(JSON.parse(readFileSync(file,'utf8')),{minFps});
    writeFileSync(file+'.validation.json',JSON.stringify(report,null,2));
    console.log(JSON.stringify(report,null,2));
    if(!report.passed)process.exitCode=1;
}
