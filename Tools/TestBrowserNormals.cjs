const fs=require('fs'),assert=require('assert');
(async()=>{
 const source=fs.readFileSync('Player/ByteEngine.Browser/wwwroot/renderer.js','utf8');
 const code=source.slice(source.indexOf('export function normalMatrix'),source.indexOf('export class ShadowFrameCache'));
 const {normalMatrix}=await import('data:text/javascript;base64,'+Buffer.from(code).toString('base64'));
 const m=[2,1,0,0,0,3,0,0,0,0,-4,0,5,6,7,1],expected=[.5,0,0,-1/6,1/3,0,0,0,-.25];
 normalMatrix(m).forEach((x,i)=>assert(Math.abs(x-expected[i])<1e-6));
 const projective=[1,0,0,.1,0,1,0,0,0,0,1,0,2,0,0,1];assert(Math.abs(normalMatrix(projective)[0]-1.25)<1e-6);
 for(let k=0;k<200;k++){
  const a=[1+k*.01,.3,.1,0,.2,2,.4,0,.1,.2,-1-k*.005,0,k,2,3,1],n=normalMatrix(a);
  // M-transpose times its inverse-transpose must be identity, including shear/mirroring.
  for(let col=0;col<3;col++)for(let row=0;row<3;row++){
   let v=0;for(let j=0;j<3;j++)v+=a[row*4+j]*n[col*3+j];assert(Math.abs(v-+(row===col))<1e-5);
  }
 }
 console.log('PASS Cached normal matrices preserve nonuniform scale, shear, mirroring and projective transforms.');
})().catch(e=>{console.error(e);process.exitCode=1;});
