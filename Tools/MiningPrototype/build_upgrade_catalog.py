import json,csv,re,html
from pathlib import Path
root=Path('artifacts/zombillionaire-upgrade-audit')
records=json.loads((root/'authored-upgrades.json').read_text(encoding='utf-8'))
registry=next(r['data']['Stats'] for r in records if r['class']=='SkillStatRegistry')
stats={s['Id']:s for s in registry}
trees=[r['data'] for r in records if r['class']=='SkillTreeAsset']
nodes=[dict(n,Tree=t['TreeName'],HiddenTree=t['HiddenTab']) for t in trees for n in t['Nodes']]
byid={n['Id']:n for n in nodes};assert len(nodes)==len(byid)
for n in nodes:
    assert len(n['RankCosts'])==n['MaxRank']
    assert all(p in byid for p in n['Prereqs'])
    assert all(e['Stat'] in stats for e in n['Effects'])
    n['PrerequisiteNames']=[byid[p]['Name'] for p in n['Prereqs']]
    n['DemoLocked']=min(n['RankCosts'] or [0])>=100000000
    n['Effects']=[dict(e,StatName=stats[e['Stat']]['Name'],Description=stats[e['Stat']]['Description'],Operation=['Add','PercentAdd','Flag'][e['Op']]) for e in n['Effects']]
    n.pop('Icon',None);n.pop('Tint',None);n.pop('IconManual',None);n.pop('Scale',None);n.pop('LocKey',None)
press=next(r['data'] for r in records if r['class']=='PressConfig')
press={k:v for k,v in press.items() if k.endswith('Ladder')}
shop=next(r['data']['Items'] for r in records if r['class']=='ShopConfig')
shop=[{k:v for k,v in i.items() if k in ['Id','NameEnglish','Price','Effect','Amount','MaxBuys','TaughtAction']} for i in shop]
catalog={'scope':'Installed ZOMBILLIONAIRE Demo authored skill trees, ammo-press ladders and shop. Reference facts only; not imported gameplay or proprietary assets.', 'trees':[{'name':t['TreeName'],'hidden':t['HiddenTab'],'nodes':len(t['Nodes'])} for t in trees], 'nodes':nodes,'stats':registry,'pressLadders':press,'shop':shop,'unparsedAncillaryConfigs':json.loads((root/'parse-failures.json').read_text())}
out=Path('Docs/Reference');out.mkdir(exist_ok=True)
(out/'Zombillionaire-Upgrade-Catalog.json').write_text(json.dumps(catalog,indent=2,ensure_ascii=False),encoding='utf-8')
def effects(n):return '; '.join(f"{e['StatName']}: {e['ValuePerRank']:g}{'%' if e['Operation']=='PercentAdd' else ''} / rank ({e['Operation']})" for e in n['Effects']) or 'No serialized gameplay effect'
currencies=['Dollars','Chips','Wrench']
with (out/'Zombillionaire-Upgrade-Catalog.csv').open('w',encoding='utf-8-sig',newline='') as f:
    w=csv.writer(f);w.writerow(['Id','Tree','Name','Ranks','Currency','Rank costs','Effects','Prerequisites','Demo locked'])
    for n in nodes:w.writerow([n['Id'],n['Tree'],n['Name'],n['MaxRank'],currencies[n['Currency']],'/'.join(map(str,n['RankCosts'])),effects(n),'; '.join(n['PrerequisiteNames']),n['DemoLocked']])
esc=html.escape
rows=''.join(f"<tr><td>{i+1}</td><td>{esc(n['Name'])}</td><td>{n['MaxRank']}</td><td>{currencies[n['Currency']]}</td><td>{' / '.join(map(str,n['RankCosts']))}</td><td>{esc(effects(n))}</td><td>{esc(', '.join(n['PrerequisiteNames']) or 'Root')}</td><td>{'Demo locked' if n['DemoLocked'] else 'Available'}</td></tr>" for i,n in enumerate(nodes))
pressrows=''.join(f"<tr><td>{esc(name)}</td><td>{i}</td><td>{', '.join(f'{k}: {v:g}' for k,v in level.items() if k!='Price')}</td><td>{level.get('Price',0)}</td></tr>" for name,levels in press.items() for i,level in enumerate(levels))
page=f'''<!doctype html><html><head><meta charset="utf-8"><title>Upgrade reference catalog</title><style>body{{background:#131914;color:#eee8cc;font:16px system-ui;margin:32px;line-height:1.5}}h1{{color:#f0ca59}}p{{max-width:1000px}}input{{position:sticky;top:10px;background:#273528;color:white;border:1px solid #64795b;padding:14px;width:min(650px,90%);font:inherit}}table{{border-collapse:collapse;width:100%;margin-top:22px;font-size:14px}}td,th{{text-align:left;padding:12px;border-bottom:1px solid #3b4838;vertical-align:top}}th{{background:#263326}}tr:nth-child(even){{background:#1c241d}}small{{color:#b0bda2}}</style></head><body><h1>ZOMBILLIONAIRE Demo — upgrade reference</h1><p><b>{len(nodes)} authored nodes · {sum(n['MaxRank'] for n in nodes)} rank purchases · {len(registry)} stat definitions · {sum(len(v) for v in press.values())} press ladder rows.</b></p><p>Read-only inspection of the installed demo. This is a reference catalog, not upgrades already installed in our game. Duplicate names are separate nodes with separate prerequisites. Costs are authored base prices; runtime balance modifiers can alter them. Placeholder/demo-locked nodes are identified, including entries with no gameplay effect.</p><p>Only Combat and one Economy node are populated in this installed build; Survival is empty. Other releases may differ. Airdrop, atomic-bomb and wave-map ancillary configurations could not be fully decoded; no values have been guessed.</p><input id="search" placeholder="Search upgrade, effect, prerequisite or currency…" aria-label="Search upgrades"><table id="catalog"><thead><tr><th>#</th><th>Upgrade</th><th>Ranks</th><th>Currency</th><th>Cost per rank</th><th>Effect per rank</th><th>Requires all</th><th>Status</th></tr></thead><tbody>{rows}</tbody></table><h2>Ammo press progression</h2><p>Level zero is the starting machine; later rows are purchased levels. Separate ladders for cycle rate, hopper capacity and payload across three ammunition families.</p><table><thead><tr><th>Track</th><th>Level</th><th>Value</th><th>Price</th></tr></thead><tbody>{pressrows}</tbody></table><h2>Shop</h2><pre>{esc(json.dumps(shop,indent=2))}</pre><script>document.querySelector('#search').addEventListener('input',e=>{{let q=e.target.value.toLowerCase();document.querySelectorAll('#catalog tbody tr').forEach(r=>r.hidden=!r.textContent.toLowerCase().includes(q));}});</script></body></html>'''
(out/'Zombillionaire-Upgrade-Catalog.html').write_text(page,encoding='utf-8')
print(len(nodes),'nodes',sum(n['MaxRank'] for n in nodes),'ranks',len(registry),'stats',sum(len(v) for v in press.values()),'press rows',len(shop),'shop items')
