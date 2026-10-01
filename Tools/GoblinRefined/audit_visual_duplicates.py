from pathlib import Path
import json,struct,hashlib,collections,html
root=Path(r'C:\Users\codex\Downloads\parts\refined\besiege-redesign');catalog=json.loads((root/'parts-catalog.json').read_text());groups=collections.defaultdict(list)
for p in catalog['parts']:
 b=(root/p['file']).read_bytes();n=struct.unpack_from('<I',b,12)[0];d=json.loads(b[20:20+n]);buf=b[28+n:];primitives=[]
 for mesh in d['meshes']:
  for pr in mesh['primitives']:
   sig=[]
   for key,a in sorted(pr['attributes'].items()):
    if key not in ['POSITION','NORMAL','TEXCOORD_0']:continue
    ac=d['accessors'][a];v=d['bufferViews'][ac['bufferView']];start=v.get('byteOffset',0)+ac.get('byteOffset',0);dim={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[ac['type']];size={5126:4,5123:2,5125:4,5121:1}[ac['componentType']];sig.append((key,hashlib.sha256(buf[start:start+ac['count']*dim*size]).hexdigest()))
   if 'indices' in pr:
    ac=d['accessors'][pr['indices']];v=d['bufferViews'][ac['bufferView']];start=v.get('byteOffset',0)+ac.get('byteOffset',0);size={5123:2,5125:4,5121:1}[ac['componentType']];sig.append(('indices',hashlib.sha256(buf[start:start+ac['count']*size]).hexdigest()))
   primitives.append(tuple(sig))
 groups[tuple(sorted(primitives))].append(p)
duplicates=[g for g in groups.values() if len(g)>1]
notes=[
('Joints and motors',[4,5,13,19,22,28,44,76,98,101],'The same square case, brass circular joint and output flange dominate. Show a release latch on the decoupler, exposed pin on hinges, ball on the ball joint, plain rigid shaft on the axle, gears on the motor, and a flywheel on reaction steering.'),
('Barrel-shaped weapons and thrusters',[11,21,53,56,62,90,91,102],'Nozzles communicate too little. Use a fire pilot and hoses, a water pump, a wide intake grille, a pellet muzzle, a repeating breech/ammunition feed and a thruster exhaust with a visible steering gimbal.'),
('Instruments',[58,65,66,67,68,69,70,93],'The shared cube and top dial conceal function. Use a camera lens, sensor antenna, clock, vertical altitude scale, logic switches, angle protractor, speed dial and tank sight glass.'),
('Wheels and flywheel',[2,40,46,50,60,86,88,100],'The repeated tire/rim silhouette weakens recognition. A caster needs its swiveling fork, skate wheel a compact mount, flywheel a heavy bare metal disc, space wheel a distinct contact profile; powered hubs should show the motor.'),
('Explosive and ball bodies',[6,23,31,54],'Shared faceted spheres rely on paint or a small wick. Distinguish a plain solid ball, contact bomb, visible burning cage and lever/pin remote grenade.'),
('Tanks and ballast',[35,82,83,92,99],'The barrel silhouettes overlap. Make ballast visibly solid/heavy, buoyancy a sealed float, and fuel vessels show ports, straps and a fill indicator.'),
('Propellers and aquatic blades',[26,55,80,81],'Air and water propulsion need distinct blade pitch, screw housing and paddle forms.'),
('Rocket and harpoon',[59,84],'Both currently read as a projectile at the end of a small barrel. Expose rocket fins/exhaust and a barbed harpoon with its rope spool.'),
('Piston and slider',[18,42],'A powered piston needs its cylinder and seals; a passive slider should expose a rail carriage.'),
('Rope devices and fuel line',[45,75,96],'The fuel line currently inherits the winch silhouette. Show flexible hose and couplings; keep a drum/ratchet for the winch and measuring wheel/counter for the rope meter.')]
md=['# Parts visual duplication audit','',f'Inspected the 98 original Goblin previews used by PARTS-WORKSHOP.html. Browser control could not start; the same saved thumbnails and exported GLB geometry were inspected directly. No models were modified during this audit.','',f'{len(duplicates)} groups / {sum(len(g) for g in duplicates)} models reuse identical exported position, normal, UV and index data. Materials, bones, animation and function are not included in this comparison: these may differ even when the static geometry matches.','', '| Group | Parts sharing geometry |','|---|---|']
for i,g in enumerate(duplicates,1):md.append('| '+str(i)+' | '+'; '.join(f"#{p['reference_id']} {p['label']}" for p in g)+' |')
md+=['','## Additional visual overlap and redesign priorities','']
for title,ids,note in notes:md+=['### '+title,'','IDs: '+', '.join(map(str,ids))+'. '+note,'']
md+=['## Legitimate family resemblance','', 'Long/short beams (#1/#15), wood pole/log (#41/#63), medium/large cogs (#38/#51), armor sizes (#24/#32), and large/small propellers (#26/#55) are intentional size families. Their auto-framed thumbnails obscure the actual size differences. Use a shared scale reference and dimensions in the catalogue rather than treating every size variant as a duplicate. Powered/free wheel variants can share a tire, but their motor and passive hub must be distinguishable.','', 'Highest priority: the instrument family; fuel line versus rope devices; motor/joint/axle silhouettes; piston versus slider; grabber/holder/jaw; cannon/flame/water/vacuum distinctions; space wheel and flywheel. Establish a distinct static silhouette for each function before the next modeling pass.']
(root/'DUPLICATE-AUDIT.md').write_text('\n'.join(md),encoding='utf-8')
e=html.escape;sections=[]
for i,g in enumerate(duplicates,1):
 cards=''.join(f'<figure><img src="previews/{Path(p["file"]).stem}.png"><figcaption>#{p["reference_id"]} {e(p["label"])}</figcaption></figure>' for p in g)
 sections.append(f'<section><h2>Group {i}: shared geometry</h2><div class="row">{cards}</div></section>')
page='''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Goblin parts / Duplicate audit</title><style>body{margin:0;background:#eeeae0;color:#292b23;font:16px/1.5 system-ui}main{max-width:1280px;margin:auto;padding:32px}h1{font-size:32px}h2{font-size:20px}.row{display:flex;gap:16px;flex-wrap:wrap}section{padding:20px;background:#faf9f4;margin:20px 0;border:1px solid #cecec1}figure{margin:0;width:210px}img{width:210px;height:160px;object-fit:contain;background:#dadbd0}figcaption{font-size:14px;margin:8px 0}a{color:#405b31}.note{padding:16px;border-left:4px solid #9d6425;background:#e1d8c7}</style><main><h1>Parts with duplicate designs</h1><p class="note">COUNT models across GROUPS groups share static geometry. Some differ only in materials or animations. No models were changed in this review.</p><p><a href="DUPLICATE-AUDIT.md">Full audit and proposed distinctions</a> · <a href="PARTS-WORKSHOP.html">Full catalogue</a></p>SECTIONS<h2>Broader look-alike families</h2>NOTES<p>Beam lengths, armor sizes, cog sizes and propeller sizes are legitimate variants. Auto-framed thumbnails hide their true size; add a consistent scale reference.</p></main></html>'''
page=page.replace('COUNT',str(sum(len(g) for g in duplicates))).replace('GROUPS',str(len(duplicates))).replace('SECTIONS',''.join(sections)).replace('NOTES',''.join(f'<section><h2>{e(t)}</h2><p>IDs: {e(str(ids))}</p><p>{e(n)}</p></section>' for t,ids,n in notes))
(root/'DUPLICATE-AUDIT.html').write_text(page,encoding='utf-8')
print('Saved audit:',len(duplicates),'groups;',sum(len(g) for g in duplicates),'models')
