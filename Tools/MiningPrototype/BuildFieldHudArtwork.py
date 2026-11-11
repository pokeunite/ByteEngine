from pathlib import Path
import json
out=Path(__file__).resolve().parents[2]/'artifacts/hud-field-kit'
out.mkdir(parents=True,exist_ok=True)
# Original flat vector artwork, drawn for the accepted Field Systems reference.
icons={
 'credits':'<path fill="#f3c332" d="M12 1 22 6v12l-10 5L2 18V6z"/><path fill="#252524" d="M17 7h-7l-4 4v5l4 3h7v-4h-6v-4h6z"/>',
 'raw-ore':'<path fill="#f3c332" d="m5 17 2-8 4-7 3 8 5 3-2 7-7 2z"/><path fill="none" stroke="#fff1c1" stroke-width="1.5" d="m7 18 4-13 1 10 5 3M3 12 2 18l5 4m13-12 2 7-3 4"/>',
 'refined-ore':'<path fill="#f3c332" d="m2 8 9-5 11 5-10 6zM2 10l9 6v6l-9-6zm11 6 9-5v6l-9 5z"/>',
 'cargo':'<path fill="none" stroke="#f5f2e9" stroke-width="1.8" stroke-linejoin="round" d="m12 2 10 5v11l-10 5-10-5V7zm-10 5 10 5 10-5m-10 5v11M7 4l10 5v5"/>',
 'health':'<path fill="#f5f2e9" d="M9 2h6v7h7v6h-7v7H9v-7H2V9h7z"/>',
 'drill':'<g fill="#f5f2e9"><path d="M5 2h14v3H5zM5 5h3v15H5zm11 0h3v15h-3zM2 20h7v3H2zm13 0h7v3h-7zM10 5h4v13h-4z"/><path d="m8 7 8 2v2l-8-2zm0 5 8 2v2l-8-2zm2 6h4l-2 5z"/></g>',
 'company-goal':'<path fill="none" stroke="#f3c332" stroke-width="2" d="m12 1 8 4 3 7-4 8-7 3-8-4-3-7 4-8z"/><circle cx="12" cy="12" r="5" fill="none" stroke="#f3c332" stroke-width="2"/><circle cx="12" cy="12" r="2" fill="#f3c332"/>',
 'pistol':'<path fill="#f5f2e9" d="M2 5h20v5H12l-1 3H8l-3 9H1l3-11H2zm7 5H6v2h3zM4 3h3v2H4zm14 0h2v2h-2z"/>'}
icons.update({
 'factory':'<g fill="#f5f2e9"><path d="M2 21V10l6 3V8l6 4V6h4v15zM16 2h3v13h-3zM21 1h2v20h-2z"/></g><path stroke="#252524" stroke-width="1.5" d="M5 15v4m5-4v4m5-4v4"/>',
 'ammo':'<path fill="#f5f2e9" d="M2 22V9L5 2l3 7v13zm8 0V9l3-7 3 7v13zm8 0V9l3-7 2 7v13z"/><path stroke="#252524" stroke-width="1.2" d="M2 18h6m2 0h6m2 0h5"/>',
 'clock':'<circle cx="12" cy="14" r="8" fill="none" stroke="#f5f2e9" stroke-width="1.7"/><path fill="#f5f2e9" d="M9 1h6v3H9z"/><path fill="none" stroke="#f5f2e9" stroke-width="1.7" d="M12 7v7l4 2M18 6l2-2"/>',
 'recycle':'<path fill="none" stroke="#f5f2e9" stroke-width="2" d="m7 9 4-7h3l4 7m-1 3 4 7-2 3h-8m-3-1H2l-1-3 4-8"/><path fill="#f5f2e9" d="m5 9 5 1-1-5zm11 3 5-1-3-4zm-5 8-3-4-3 4z"/>',
 'info':'<circle cx="12" cy="12" r="10" fill="none" stroke="#f5f2e9" stroke-width="1.6"/><path fill="#f5f2e9" d="M11 10h2v8h-2zm0-5h2v2h-2z"/>',
 'gear':'<path fill="#f5f2e9" d="M9 1h6l1 4 4-1 3 5-3 3 3 3-3 5-4-1-1 4H9l-1-4-4 1-3-5 3-3-3-3 3-5 4 1z"/><circle cx="12" cy="12" r="4" fill="#202020"/>'})
icons.update({
 'hunter':'<circle cx="12" cy="13" r="5" fill="#f5f2e9"/><circle cx="12" cy="7" r="3" fill="#f5f2e9"/><path fill="none" stroke="#f5f2e9" stroke-width="1.5" d="M9 9 4 3 1 7m8 4L3 8l-2 5m8 1-6 1-2 5m9-4-4 4v4m9-15 5-6 3 4m-8 4 6-3 2 5m-8 1 6 1 2 5m-9-4 4 4v4"/>',
 'rig-raider':'<path fill="#f5f2e9" d="m12 3 6 5-1 9-5 5-5-5-1-9z"/><path fill="none" stroke="#f5f2e9" stroke-width="2" d="m8 7-5-3-2 4m6 3-5 1-1 6m7-3-4 5v4m12-17 5-3 2 4m-6 3 5 1 1 6m-7-3 4 5v4"/><path fill="none" stroke="#252524" stroke-width="1.5" d="m8 10 4 3 4-3m-4 3v6"/>'})
icons.update({
 'support-drone':'<g fill="none" stroke="#f5f2e9" stroke-width="1.4"><path d="M7 7 17 17M17 7 7 17M3 5h7m4 0h7M3 19h7m4 0h7"/><circle cx="5" cy="5" r="4"/><circle cx="19" cy="5" r="4"/><circle cx="5" cy="19" r="4"/><circle cx="19" cy="19" r="4"/></g><path fill="#f5f2e9" d="m9 8 6 0 2 5-5 4-5-4z"/>',
 'support-barricade':'<path fill="#f5f2e9" d="m3 5 18-3 2 18-21 2z"/><path fill="none" stroke="#252524" stroke-width="1" d="m3 16 19-3M5 18l2 3m4-4 2 3m4-4 2 3"/><circle cx="7" cy="10" r="1.5" fill="#252524"/><circle cx="17" cy="8" r="1.5" fill="#252524"/>',
 'support-mine':'<path fill="#f5f2e9" d="M1 15 4 9l8-3 8 3 3 6-1 5-10 3L2 20z"/><path fill="none" stroke="#252524" stroke-width="1" d="m2 15 10 4 10-4M4 11l8 4 8-4"/><path fill="#f5f2e9" d="M8 4h8v4H8z"/>',
 'support-sentry':'<path fill="#f5f2e9" d="M3 4h12v5H3zM15 5h8v2h-8zM6 9h7v5H6z"/><path fill="none" stroke="#f5f2e9" stroke-width="2" d="m10 13-8 9m8-9 2 10m-2-10 11 8M5 2h8"/>',
 'support-mortar':'<path fill="#f5f2e9" d="m6 2 8-1 6 13-8 4zM3 19h18v4H3z"/><path fill="none" stroke="#f5f2e9" stroke-width="2" d="m9 10-5 10m12-7 4 7"/><path fill="none" stroke="#252524" stroke-width="1.3" d="m7 4 7-2"/>',
 'support-station':'<path fill="#f5f2e9" d="M1 21 5 6l7-5 7 5 4 15H1z"/><path fill="#252524" d="m5 20 3-11 4-4 4 4 3 11h-5V10h-4v10z"/>',
 'support-refinery':'<path fill="#f5f2e9" d="M1 22V10h5V2h3v11h3V5h3v8h3V2h3v20z"/><path fill="none" stroke="#252524" stroke-width="1" d="M4 15v5m4-5v5m5-5v5m5-5v5"/>'})
for name,body in icons.items():(out/(name+'.svg')).write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128" viewBox="0 0 24 24">{body}</svg>')
panels={'resource-strip':(560,52),'vitals':(350,84),'ammunition':(230,60),'hint':(194,34),'goal':(210,66),'machine':(480,580),'upgrade-row':(444,62),'price':(98,40),'survey-frame':(1120,640),'site-chip':(205,76)}
for name,(w,h) in panels.items():
 points=f'8,1 {w-9},1 {w-1},9 {w-1},{h-9} {w-9},{h-1} 8,{h-1} 1,{h-9} 1,9'
 (out/(name+'-border.svg')).write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}"><polygon points="{points}" fill="#202020" fill-opacity=".94" stroke="#c3bda6" stroke-width="1.2"/></svg>')
(out/'site-chip-selected.svg').write_text((out/'site-chip-border.svg').read_text().replace('#c3bda6','#f3c332').replace('stroke-width="1.2"','stroke-width="2"'))
(out/'support-knob.svg').write_text('<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128" viewBox="0 0 24 24"><circle cx="12" cy="12" r="11" fill="#f5f2e9"/></svg>')
(out/'manifest.json').write_text(json.dumps({'reference':'Docs/Concepts/HazardPay-UI-Overhaul/01-Field-Systems.png','font':'Inter 4.1; closest verifiable match, generated reference has no embedded font identity','icons':list(icons),'panels':panels,'palette':{'surface':'#202020','border':'#c3bda6','accent':'#f3c332','text':'#f5f2e9'}},indent=2))
print(f'Created {len(icons)} original SVG icons, {len(panels)} clipped-corner panel assets and layout manifest.')
