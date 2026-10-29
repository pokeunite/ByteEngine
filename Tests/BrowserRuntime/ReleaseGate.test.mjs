import test from 'node:test';
import assert from 'node:assert/strict';
import {assessCapture} from '../../Tools/ValidateBrowserCapture.mjs';

const smooth=()=>({perf:Array.from({length:60},()=>({intervalMs:1000/60,managedMs:8,renderMs:4,pending:0})),frame:{pendingTextures:0},runtimeErrors:[]});
test('steady 60 FPS capture passes',()=>assert.equal(assessCapture(smooth()).passed,true));
test('a game that runs at eight FPS fails release validation',()=>{
    const capture=smooth();for(const f of capture.perf)f.intervalMs=125;
    const result=assessCapture(capture);assert.equal(result.passed,false);assert.equal(result.measured.fps,8);
});
test('a fast game with errors still fails',()=>{
    const capture=smooth();capture.runtimeErrors.push('Shader compilation failed');assert.equal(assessCapture(capture).passed,false);
});
test('caught main-loop errors are rejected even without an unhandled page error',()=>{
    const capture=smooth();capture.capture={events:[{kind:'error',message:'Frame failed during scene update'}]};
    assert.equal(assessCapture(capture).passed,false);
});
test('startup uploads and incomplete samples cannot establish performance',()=>{
    const capture=smooth();capture.perf.length=10;capture.perf[0].pending=1;
    const result=assessCapture(capture);assert.equal(result.passed,false);assert.ok(result.errors.length>=2);
});
