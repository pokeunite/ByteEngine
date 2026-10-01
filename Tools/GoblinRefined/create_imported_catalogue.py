from pathlib import Path
import json,html
project=Path(r'C:/Users/codex/Documents/Goblin Scraper');catalog=json.loads((project/'Assets/ContraptionParts/parts-catalog.json').read_text())
text='''# Goblin Scraper — standard contraption prototype

The active set contains the starting block and 65 buildable standard/flight parts. The previous refined set is archived under Backups/before-standard-contraptions. Rope/brace/winch blocks, sensors, cameras, meters, timers, logic gates, pin, grip pad, square balloon and bouncy pad are excluded. Sea propulsion and space blocks are excluded.

Start with one protected block. Place short/long beams on its faces and continue from beam connectors. Parts are reusable, not limited to vehicle slots. Available faces belong to your placed structure. Matching spare connectors also weld when simulation starts, allowing additional structural connections. Geometry clearance still applies.

## Build controls

- Click an empty connector to place the selected part.
- R rotates; Alt+R rotates 15 degrees. F flips; Tab selects the part's attachment face.
- M moves a pointed-at branch; C copies the pointed-at part.
- ERASE or X selects deletion; Delete removes the selected part. Removing a support leaves disconnected pieces in place.
- Ctrl+Z / Ctrl+Y undo and redo. Save/Load preserve the part graph.
- B starts/stops simulation. No engine, cab or predefined chassis is required.

## Simulation controls

- W/S: powered wheel torque and flying-block thrust.
- A/D: differential wheel steering and steering joints/fins. A turning block is optional for a simple skid-steering machine.
- Space: powered-wheel brake. Shift reduces wheel grip for drifting.
- F: cannons/crossbow fire, bombs/grenades detonate, rockets launch. Hold F for rotating motors, saws, drills, flames, water and suction.
- G: actuator extension/contraction, grabber close/release, decoupler release, bomb-holder payload release, parachute deployment.
- R: reset simulation. B restores the untouched build pose.

Passive wheels, hinges, swivels, ball joints and sliders move through their joints. Powered parts apply torque; they do not move the entire vehicle directly. Children attached to a moving output connect to that output's physical body. Gears close enough to mesh exchange rotational motion. Propellers must attach to a rotating output; their angular speed supplies thrust. Wings supply simplified lift, balloons supply buoyancy, and deployed parachutes increase drag. Rotate a flying block to choose its thrust direction; mounting it on top points thrust upward.

Cannons and crossbows launch colliding projectiles with recoil. Sharp moving parts and flames can damage block connections, and explosions can detach parts. Water pushes, suction pulls, and grabbers can hold nearby detached blocks. These actions affect contraption bodies; infantry combat is not included in this pass.

## Current prototype limits

Physics uses box/cylinder/sphere proxies rather than detailed mesh collisions. Aerodynamics, weapon damage, gear engagement, grip and joint strengths need gameplay tuning. Controls are currently shared by each part type; individual key bindings and an editable per-part settings panel are not implemented. Scaling block currently behaves as a linear extender. Grabbers capture detached contraption blocks, not arbitrary scenery. This is an original prototype inspired by the construction workflow, not a complete reproduction of Besiege's systems.

## Verification

All 66 GLB models imported in the native renderer. All 65 palette parts attached and simulated without invalid poses. A beam-built four-wheel machine drove and turned through wheel torque without an engine, cab or steering block. Save/load, deletion, undo, piston travel, flying-block lift, balloon lift, simulation reset and UI rendering at 720p, 1080p, 16:10 and ultrawide were checked.
'''
(project/'Docs').mkdir(exist_ok=True);(project/'Docs/CONTRAPTION-PROTOTYPE.md').write_text(text,encoding='utf-8')
controls={2:'W/S drive; A/D differential; Space brake',46:'W/S drive; A/D differential; Space brake',14:'W/S thrust in the mounted rotor direction',13:'A/D steering',28:'A/D steering',79:'A/D steering',95:'A/D steering',9:'G contract',12:'G extend',18:'G extend',4:'G disconnect',30:'G release payload',27:'G grab/release detached blocks',77:'G grab/release detached blocks',11:'F fire',53:'F fire',61:'F fire',23:'F detonate / ground impact',54:'F detonate',59:'F launch',17:'Hold F rotate/cut',22:'Hold F rotate output',39:'Hold F drive cog',48:'Hold F rotate/cut',21:'Hold F flame',56:'Hold F water push',62:'Hold F suction',97:'G deploy/retract'}
cards=[]
for p in catalog['parts']:
 image=(project/'Assets/GarageUI/cutouts'/ (Path(p['file']).stem+'.png')).as_uri()
 cards.append(f'<article data-group="{p["group"]}"><img src="{image}"><h2>{html.escape(p["label"])}</h2><small>{p["group"]} · {p["mass"]} kg</small><p>{html.escape(p["description"])}</p><b>{controls.get(p["reference_id"],"Passive physical part")}</b></article>')
page='''<!doctype html><meta charset="utf-8"><title>Goblin Scraper — imported parts</title><style>body{background:#141612;color:#dfded2;font:16px system-ui;margin:35px}h1{color:#d2a13c}nav button{background:#302c1e;color:#efdcad;border:1px solid #766031;padding:12px;margin:4px;cursor:pointer}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(235px,1fr));gap:18px}article{background:#21231d;border:1px solid #414136;padding:18px}img{width:100%;height:170px;object-fit:contain}h2{font-size:19px}small{color:#b8a477}p{line-height:1.5}b{color:#d2a13c;font-size:14px}</style><h1>GOBLIN SCRAPER / IMPORTED PARTS</h1><p>65 buildable standard and flight parts + one starting block. Only this selected set is active in the game.</p><p>Build your structure from beams. Powered parts provide motion; moving output faces carry their attached branches. B simulates; X erases; Ctrl+Z restores.</p><nav>'''+''.join(f'<button onclick="filter(\'{g}\')">{g}</button>' for g in ['All','Structure','Drive','Weapons','Mechanics','Flight'])+'</nav><main>'+''.join(cards)+'''</main><p>Prototype: simplified collisions, aerodynamics and damage. Shared controls; per-part key bindings and settings are future work.</p><script>function filter(g){document.querySelectorAll('article').forEach(a=>a.hidden=g!=='All'&&a.dataset.group!==g)}</script>'''
(project/'Docs/IMPORTED-PARTS.html').write_text(page,encoding='utf-8');Path(r'C:/Users/codex/Downloads/parts/refined/IMPORTED-PARTS.html').write_text(page,encoding='utf-8')
print('GUIDE_AND_SELECTED_CATALOGUE_SAVED')
