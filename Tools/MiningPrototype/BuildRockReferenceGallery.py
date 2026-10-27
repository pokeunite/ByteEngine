"""Build an inspectable HTML catalogue. No image pixels are edited or cropped."""
import json, html
from pathlib import Path

root=Path('Docs/Concepts/AlienMining/RockKit')
data=json.loads((root/'catalog.json').read_text())
groups={'R01':'Cliffs','R02':'Cliffs','R03':'Cliffs','R04':'Cliffs','R05':'Backdrop','R06':'Backdrop','R07':'Backdrop','R08':'Boulders','R09':'Boulders','R10':'Boulders','R11':'Rubble','R12':'Rubble','R13':'Crystal rocks','R14':'Crystal rocks','D01':'Decorations','D02':'Decorations'}
roles={
'R01':('6–12 m high','Continuous rear wall; repeat with overlap and rotation.'),
'R02':('4–8 m high','Side wall height transition; keep its lowest boundary taller than the player can cross.'),
'R03':('4–9 m high','Right-hand enclosing cliffs and angled end caps.'),
'R04':('2–5 m high','Corners and foreground boundary trim; overlap a high wall where required.'),
'R05':('10–25 m high','Distant skyline landmark outside the gameplay boundary.'),
'R06':('20–60 m wide','Distant silhouette behind the forward approach valley.'),
'R07':('15–30 m wide','Far background only; no apparent accessible bridge behind camp.'),
'R08':('0.6–1.4 m high','Individual low cover and near-ground silhouette variation.'),
'R09':('2–4 m wide','Broad low cover cluster in the forward arena.'),
'R10':('1.5–2.5 m high','Upright arena cover; keep routes around both sides.'),
'R11':('0.2–0.7 m high','Small piles near larger formations; generally decorative.'),
'R12':('0.05–0.25 m pieces','Sparse decorative floor scatter; avoid individual collision/pathfinding obstacles.'),
'R13':('4–8 m high','Crystal-bearing variant of the enclosing cliff.'),
'R14':('1–3 m high','Crystal-bearing variant of low boulders near the boundary.'),
'D01':('0.15–0.7 m high','Separate reusable cyan crystal insert; emission is a material setting.'),
'D02':('0.15–0.5 m high','Separate violet foliage tuft at boulder bases.')}
cards=[]
for a in data['assets']:
 a['group']=groups[a['id']];a['suggestedScale'],a['role']=roles[a['id']]
 cards.append(f'''<article class="card" id="{a['id']}" data-group="{a['group']}">
 <button class="preview" onclick="choose('{a['id']}')" aria-label="Inspect {html.escape(a['name'])}"><img loading="lazy" src="{a['file']}" alt="{html.escape(a['name'])}"></button>
 <div class="body"><small>{a['id']} · {a['group']}</small><h2>{html.escape(a['name'])}</h2><p>{html.escape(a['role'])}</p><p class="scale">Modelling target: {a['suggestedScale']}</p><div class="links"><button onclick="choose('{a['id']}')">Locate in reference</button><a href="{a['file']}" target="_blank">Open separate PNG ↗</a></div></div></article>''')
(root/'catalog.json').write_text(json.dumps(data,indent=2),encoding='utf-8')
doc='''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Alien Mining — separated rock kit</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#171922;color:#f2ece1;font:16px/1.5 system-ui,sans-serif}header,main{max-width:1500px;margin:auto;padding:28px}header{padding-bottom:12px}h1{font-size:36px;margin:0 0 10px}h2{font-size:20px;margin:6px 0}p{margin:8px 0;color:#c7c6cf}small,.scale{color:#e7b683}.badge{color:#90d7d8;font-weight:650}nav{display:flex;gap:8px;flex-wrap:wrap;margin:20px 0}button,a{font:inherit}button{cursor:pointer;color:#fff;background:#34333f;border:1px solid #605268;border-radius:6px;padding:8px 12px}button:hover,button.active{background:#635073}a{color:#9cdee2}.source{border:1px solid #615268;border-radius:12px;overflow:hidden;background:#25242e}svg{display:block;width:100%;height:auto}#sourcebox{fill:#efbb5440;stroke:#ffe4a0;stroke-width:4;pointer-events:none}.source-footer{padding:16px 20px;background:#25242e}.grid{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:18px}.card{border:1px solid #4e455a;border-radius:10px;overflow:hidden;background:#24232e}.card.selected{outline:3px solid #edbd7c}.preview{border:0;border-radius:0;display:block;width:100%;padding:10px;aspect-ratio:1.4;background:repeating-conic-gradient(#33313d 0% 25%,#393743 0% 50%) 50%/24px 24px}.preview img{width:100%;height:100%;object-fit:contain}.body{padding:16px}.body p{font-size:14px}.links{display:flex;gap:10px;flex-wrap:wrap;align-items:center;font-size:13px;margin-top:12px}.links button{padding:5px 8px;font-size:13px}.note{background:#282735;border-left:4px solid #d2a978;padding:14px 18px;margin:18px 0}.summary{display:flex;gap:20px;flex-wrap:wrap}.summary span{color:#e6b780}#focus-title{margin:0;font-size:22px}footer{padding:30px 0;color:#aaa7b2;font-size:14px}.hidden{display:none}@media(max-width:1100px){.grid{grid-template-columns:repeat(3,minmax(0,1fr))}}@media(max-width:760px){.grid{grid-template-columns:repeat(2,minmax(0,1fr))}header,main{padding:18px}h1{font-size:28px}}@media(max-width:480px){.grid{grid-template-columns:1fr}}
</style><header><div class="badge">ALIEN MINING · ENVIRONMENT BREAKDOWN</div><h1>Separated rock & cluster references</h1><p>14 rock forms and 2 independent decoration elements, each in its own named image.</p><div class="summary"><span>4 boundary modules</span><span>3 distant landforms</span><span>3 boulder forms</span><span>2 rubble groups</span><span>2 crystal-rock variants</span><span>2 inserts</span></div><div class="note">These are isolated modelling-reference reconstructions from the supplied image, not finished 3D models or exact pixel segmentation. Hidden/overlapping edges are inferred. Source highlights identify approximate example regions. Repeated placements belong to the same reusable form.</div></header>
<main><section class="source"><svg viewBox="0 0 1672 941" role="img" aria-label="Original reference with selected source region"><image href="Reference.png" width="1672" height="941"/><rect id="sourcebox" x="0" y="0" width="0" height="0"/></svg><div class="source-footer"><h2 id="focus-title">Choose a rock below to locate its source</h2><p id="focus-description">The full original image is preserved unchanged.</p></div></section><nav id="filters"></nav><section class="grid">__CARDS__</section><footer>Basalt geometry, cyan crystals and violet foliage stay modular. The rear and both sides must form continuous visible boundaries; the broad front remains the enemy approach. Rock references have sunset lighting: use neutral basalt materials and the existing game lights when modelling, rather than baking the orange highlight into every face. Generation prompts and source coordinates: <a href="catalog.json">catalog.json</a>.</footer></main>
<script>const assets=__DATA__;
function choose(id){const a=assets.find(x=>x.id===id),b=document.getElementById('sourcebox');b.setAttribute('x',a.box[0]);b.setAttribute('y',a.box[1]);b.setAttribute('width',a.box[2]-a.box[0]);b.setAttribute('height',a.box[3]-a.box[1]);document.getElementById('focus-title').textContent=a.id+' — '+a.name;document.getElementById('focus-description').textContent=a.description;document.querySelectorAll('.card').forEach(c=>c.classList.toggle('selected',c.id===id));document.querySelector('.source').scrollIntoView({behavior:'smooth',block:'start'});}
const groups=['All',...new Set(assets.map(a=>a.group))];groups.forEach((group,i)=>{const b=document.createElement('button');b.textContent=group;b.classList.toggle('active',i===0);b.onclick=()=>{document.querySelectorAll('#filters button').forEach(x=>x.classList.toggle('active',x===b));document.querySelectorAll('.card').forEach(c=>c.classList.toggle('hidden',group!=='All'&&c.dataset.group!==group));};document.getElementById('filters').append(b);});
</script></html>'''.replace('__CARDS__',''.join(cards)).replace('__DATA__',json.dumps(data['assets']).replace('</','<\\/'))
(root/'Rock-Kit-References.html').write_text(doc,encoding='utf-8')
print('Gallery written: '+str(root/'Rock-Kit-References.html'))
