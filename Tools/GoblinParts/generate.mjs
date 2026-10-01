import * as THREE from 'three';
import { GLTFExporter } from 'three/addons/exporters/GLTFExporter.js';
import fs from 'node:fs/promises';
import path from 'node:path';

// Original modular scrap-machine kit. One unit is one metre; Y is up.
// The origin of each GLB is its intended attachment/placement pivot.
globalThis.FileReader ??= class {
  readAsArrayBuffer(blob) {
    blob.arrayBuffer().then(value => { this.result = value; this.onloadend?.(); });
  }
  readAsDataURL(blob) {
    blob.arrayBuffer().then(value => {
      this.result = `data:${blob.type};base64,${Buffer.from(value).toString('base64')}`;
      this.onloadend?.();
    });
  }
};

const out = process.argv[2];
if (!out) throw new Error('Usage: node generate.mjs <output-directory>');
await fs.mkdir(out, { recursive: true });
const mats = {
  iron: new THREE.MeshStandardMaterial({ color: 0x42484a, metalness: .78, roughness: .63 }),
  edge: new THREE.MeshStandardMaterial({ color: 0x777a72, metalness: .8, roughness: .56 }),
  rust: new THREE.MeshStandardMaterial({ color: 0x884b2f, metalness: .55, roughness: .84 }),
  green: new THREE.MeshStandardMaterial({ color: 0x4d6334, metalness: .48, roughness: .73 }),
  brass: new THREE.MeshStandardMaterial({ color: 0xb58a45, metalness: .74, roughness: .5 }),
  rubber: new THREE.MeshStandardMaterial({ color: 0x232525, metalness: .08, roughness: .94 }),
  wood: new THREE.MeshStandardMaterial({ color: 0x674b32, metalness: .02, roughness: .88 }),
  red: new THREE.MeshStandardMaterial({ color: 0xa3452b, metalness: .55, roughness: .67 })
};
const catalog = [];
const g = () => new THREE.Group();
function box(root, name, size, pos, mat='iron', rot=[0,0,0]) {
  const m = new THREE.Mesh(new THREE.BoxGeometry(...size), mats[mat]);
  m.name=name; m.position.set(...pos); m.rotation.set(...rot); root.add(m); return m;
}
function cyl(root, name, radius, depth, pos, mat='iron', axis='y', sides=20) {
  const m = new THREE.Mesh(new THREE.CylinderGeometry(radius,radius,depth,sides),mats[mat]);
  m.name=name; m.position.set(...pos);
  if(axis==='x')m.rotation.z=Math.PI/2;
  if(axis==='z')m.rotation.x=Math.PI/2;
  root.add(m); return m;
}
function torus(root,name,radius,tube,pos,mat='iron',axis='z'){
  const m=new THREE.Mesh(new THREE.TorusGeometry(radius,tube,8,32),mats[mat]);
  m.name=name;m.position.set(...pos);
  if(axis==='x')m.rotation.y=Math.PI/2;
  if(axis==='y')m.rotation.x=Math.PI/2;
  root.add(m);return m;
}
function bolt(root,pos,axis='y'){
  cyl(root,'raised bolt',.045,.025,pos,'brass',axis,8);
}
function part(name,description,build,connections=[]){
  const root=g();root.name=name;build(root);
  catalog.push({file:`${name}.glb`,description,connections,unit:'metre',pivot:'local origin',meshes:root.children.length});
  return root;
}
const parts=[];
parts.push(part('scrap_frame_2x1','Main rectangular vehicle frame, 2 m long.',r=>{
  for(const x of [-.42,.42])box(r,'long box rail',[.12,.16,2],[x,0,0],'iron');
  for(const z of [-.94,0,.94])box(r,'cross member',[.96,.14,.12],[0,0,z],'green');
  for(const x of [-.42,.42])for(const z of [-.94,.94])bolt(r,[x,.095,z]);
},['wheel/axle at ±X','armor above Y']));
parts.push(part('scrap_beam_1m','One-metre square structural beam.',r=>{
  box(r,'scrap square beam',[.18,.18,1],[0,0,0],'green');
  for(const z of [-.42,.42])box(r,'riveted collar',[.24,.24,.12],[0,0,z],'iron');
  for(const z of [-.42,.42])bolt(r,[0,.13,z]);
},['ends at Z ±0.5']));
parts.push(part('scrap_beam_2m','Two-metre long rail for chassis extensions.',r=>{
  box(r,'long square beam',[.18,.18,2],[0,0,0],'green');
  for(const z of [-.88,0,.88])box(r,'metal binding',[.24,.24,.1],[0,0,z],'iron');
},['ends at Z ±1']));
parts.push(part('scrap_corner_joint','Heavy L bracket for beams and armor.',r=>{
  box(r,'horizontal flange',[.78,.12,.18],[.3,0,0],'iron');
  box(r,'vertical flange',[.18,.72,.18],[0,.3,0],'iron');
  for(const p of [[.1,.08,0],[.5,.08,0],[.0,.48,0]])bolt(r,p);
},['inner corner at origin']));
function wheel(name,radius,depth){return part(name,`${radius*2} m diameter scrap wheel.`,r=>{
  cyl(r,'solid rubber tread',radius,depth,[0,0,0],'rubber','x',24);
  cyl(r,'steel side disc',radius*.77,depth+.025,[0,0,0],'iron','x',24);
  for(const x of [-depth/2-.022,depth/2+.022]){
    cyl(r,'rim face',radius*.68,.025,[x,0,0],'rust','x',24);
    torus(r,'raised rim ring',radius*.55,.035,[x,0,0],'edge','x');
    cyl(r,'gold hub',radius*.19,.07,[x,0,0],'brass','x',12);
  }
  for(let i=0;i<12;i++){
    const a=i*Math.PI/6;
    const m=box(r,'chunky tread lug',[depth+.02,.055,.15],[0,Math.sin(a)*radius,Math.cos(a)*radius],'rubber',[a,0,0]);
    m.name=`tread lug ${i+1}`;
  }
},['rotates around local X']);}
parts.push(wheel('scrap_wheel_large',.55,.28));
parts.push(wheel('scrap_wheel_small',.35,.23));
parts.push(part('scrap_axle_2m','Axle shaft with two reinforced hub collars.',r=>{
  cyl(r,'axle shaft',.085,2,[0,0,0],'edge','x',16);
  for(const x of [-.85,.85]){cyl(r,'wheel stop',.18,.16,[x,0,0],'iron','x',16);cyl(r,'bearing',.12,.19,[x,0,0],'brass','x',16);}
},['wheel hubs at X ±0.85','rotates around X']));
parts.push(part('scrap_steering_pivot','Vertical steering swivel between frame and axle.',r=>{
  box(r,'upper bracket',[.6,.12,.6],[0,.27,0],'green');
  box(r,'lower fork',[.55,.12,.45],[0,-.3,0],'iron');
  cyl(r,'pivot spindle',.13,.62,[0,0,0],'brass','y',16);
  torus(r,'turntable ring',.22,.045,[0,.02,0],'rust','y');
  for(const x of [-.22,.22])bolt(r,[x,.35,0]);
},['frame on +Y','axle on -Y','turns around Y']));
parts.push(part('scrap_suspension_piston','Compact spring-damper visual module.',r=>{
  cyl(r,'outer piston',.15,.65,[0,.1,0],'iron','y',16);
  cyl(r,'sliding ram',.085,.62,[0,-.35,0],'edge','y',16);
  for(let i=0;i<6;i++)torus(r,'spring coil',.19,.025,[0,-.44+i*.16,0],'rust','y');
  for(const y of [-.69,.48])cyl(r,'mount eye',.17,.09,[0,y,0],'brass','z',16);
},['upper +Y','lower -Y']));
parts.push(part('scrap_engine_block','Chunky goblin engine with exhaust and flywheel.',r=>{
  box(r,'engine case',[.85,.65,.8],[0,0,0],'green');
  box(r,'top cover',[.74,.13,.68],[0,.38,0],'iron');
  for(const x of [-.25,.25])for(const z of [-.22,.22])bolt(r,[x,.46,z]);
  cyl(r,'flywheel',.29,.13,[.49,0,0],'rust','x',20);
  for(const z of [-.22,.22])cyl(r,'exhaust stub',.085,.55,[-.22,.54,z],'edge');
},['frame underneath -Y','power shaft at +X']));
parts.push(part('scrap_armor_plate','Riveted 1 x 1 metre sheet-metal armor.',r=>{
  box(r,'painted plate',[1,.075,1],[0,0,0],'green');
  for(const x of [-.42,.42])for(const z of [-.42,.42])bolt(r,[x,.06,z]);
  for(const x of [-.46,.46])box(r,'edge rib',[.045,.14,1],[x,.025,0],'iron');
},['flat in XZ','front face +Y']));
parts.push(part('scrap_ram_wedge','Front wedge for pushing or striking.',r=>{
  const shape=new THREE.Shape();shape.moveTo(-.65,0);shape.lineTo(.65,0);shape.lineTo(0,.6);shape.closePath();
  const geo=new THREE.ExtrudeGeometry(shape,{depth:.68,bevelEnabled:false});
  const m=new THREE.Mesh(geo,mats.iron);m.name='solid ram wedge';m.rotation.x=-Math.PI/2;m.position.set(0,-.25,-.32);r.add(m);
  box(r,'reinforcement bar',[1.25,.14,.15],[0,-.14,.36],'rust');
},['rear at Z +0.35','nose at Z -0.35']));
parts.push(part('scrap_saw_disc','Spiked rotating cutter disc.',r=>{
  cyl(r,'cutter plate',.48,.085,[0,0,0],'edge','x',24);
  cyl(r,'hub',.16,.17,[0,0,0],'rust','x',16);
  for(let i=0;i<12;i++){
    const a=i*Math.PI/6;
    box(r,`tooth ${i+1}`,[.13,.12,.18],[0,Math.sin(a)*.48,Math.cos(a)*.48],'iron',[a,0,0]);
  }
},['shaft at origin along X']));
parts.push(part('scrap_weapon_mount','Pivot base for gun, harpoon, or cutter.',r=>{
  cyl(r,'round base',.42,.12,[0,0,0],'iron','y',20);
  cyl(r,'turntable',.31,.11,[0,.1,0],'brass','y',20);
  for(const x of [-.21,.21])box(r,'weapon cradle',[.13,.38,.68],[x,.34,0],'green');
  for(const z of [-.22,.22])cyl(r,'trunnion',.07,.55,[0,.39,z],'edge','x',12);
},['vehicle below -Y','weapon above +Y','yaw around Y']));
parts.push(part('scrap_cab_shell','Open goblin driver cage and dash.',r=>{
  box(r,'floor',[1.1,.1,1.25],[0,-.45,0],'iron');
  for(const x of [-.48,.48])for(const z of [-.53,.53])box(r,'roll-cage post',[.09,.94,.09],[x,.07,z],'rust');
  for(const x of [-.48,.48])box(r,'roof side rail',[.09,.08,1.15],[x,.57,0],'rust');
  box(r,'dashboard',[.96,.25,.12],[0,.02,-.48],'green');
  cyl(r,'steering rim',.22,.05,[0,.19,-.39],'brass','z',20);
},['base Y -0.5','forward -Z']));

const exporter=new GLTFExporter();
for(let i=0;i<parts.length;i++){
  const data=await exporter.parseAsync(parts[i],{binary:true,onlyVisible:true});
  await fs.writeFile(path.join(out,catalog[i].file),Buffer.from(data));
}
await fs.writeFile(path.join(out,'parts-catalog.json'),JSON.stringify(catalog,null,2));
await fs.writeFile(path.join(out,'README.txt'),`Goblin Scrapwar modular vehicle kit\n\nOriginal Three.js-generated assets, not extracted from Besiege.\nEach GLB uses metres, Y-up, and a local attachment pivot.\nThe visible mechanical pieces are geometry only: configure colliders, joints, motors, and gameplay behavior in ByteEngine.\n\nParts: ${catalog.map(x=>x.file).join(', ')}\n`);
console.log(`Wrote ${parts.length} GLB parts to ${out}`);
