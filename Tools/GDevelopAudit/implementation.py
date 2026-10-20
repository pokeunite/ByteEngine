from pathlib import Path
import json,html

registry=json.loads(Path('artifacts/event-sheet-check/byteengine-events.json').read_text(encoding='utf-8-sig'))
areas=[
('Object selection sets','pick.','Object / Selection','Pick all, nearest or random tagged objects. In the object picker, use Picked objects and enter the selection set name (Selection by default). Conditions filter the set; actions run for each remaining object.','Native target-based actions support selections. Plugin actions with their own object resolution may require explicit targets.'),
('Arrays and structure variables','collection.','Variables / Collections','Create arrays/structures; append, read, replace, remove, count, deep-copy and check children. Choose Array or Structure in variable settings; Edit opens a JSON editor with explicit Apply.','Indices are zero based. Nested values are copied, not shared references.'),
('Persistent values','storage.','Storage / Values','Save, load, check and delete values by key. Saved data uses the project/build save location.','Desktop uses atomic JSON files; browser uses the existing browser save persistence. Check existence before loading an optional key.'),
('Non-blocking Wait','time.wait|time.cancelWaits','Flow / Timing','Wait suspends the remaining action chain and resumes it later, including wired branch paths. Cancel pending waits cancels waits in this sheet instance.','Wait uses game time and is cancelled on runtime reset/scene teardown. Do not put an unguarded Wait in an every-frame rule unless repeated delayed actions are intended. Limit: 1024 pending waits per sheet.'),
('General transform and value tweens','tween.','Tween','Animate position, rotation, scale, number variables and camera FOV. Named tweens support easing, pause, resume, remove, running and completion tests.','Starting the same named tween replaces it. Use Trigger once or an input event to start it rather than restarting it every update.'),
('Collision lifecycle','collision.|trigger.','Physics / Contacts','Collision and trigger enter/stay/exit expose ContactObjectId, ContactPoint and ContactNormal on Self.','Uses the native physics contact snapshot. Trigger and solid collision states are kept separate.'),
('NOT condition blocks','logic.not','Logic','Add NOT and nest conditions inside it, like AND/OR. With an object selection, NOT keeps the complement.','An empty block is false. OR evaluates branches independently and unions passing selections.'),
('Camera controls','camera.','Camera','Activate a camera, change FOV or orthographic zoom, center on an object with offset, and clamp its position.','Use a continuous rule for following. These controls operate on native Camera3D components.'),
('Timer pause and inspection','time.pauseTimer|time.resumeTimer|time.readTimer|time.timerPaused','Time / Timers','Pause/resume a named timer and read elapsed time or paused status.','Timers belong to the event-sheet instance; paused timers do not advance.'),
('JSON data conversion','data.','Variables / JSON','Parse ordinary JSON into a variable and serialize collection data back to JSON text.','Arrays, objects, strings, numbers, booleans and null are supported. Vectors serialize as numeric arrays.'),
('Object relationships','links.','Object / Relationships','Link/unlink objects, choose one-way or two-way links, clear relationships, check links and pick linked objects.','Gameplay relationships do not change transform parenting. Links persist as object variables.'),
('General inventory','inventory.','Inventory','Add/remove/count items, set stack limits, test capacity, equip/unequip named slots and clear inventories.','Inventory state is a structure variable in Self, Scene or Global scope. Equip requires an owned item. Removing the last item clears its equipment slots.'),
('Pathfinding commands','navigation.','Navigation','Move a native Navigation Agent to a destination, stop it and inspect idle/following/arrived/unreachable/missing-region status.','Attach and configure a native Navigation Region and Agent. Dynamic rigidbodies remain controlled by physics.'),
('Dialogue and choices','dialogue.','Dialogue','Load a dialogue tree, start/advance/stop, choose a branch, read the current line and inspect running/ended/awaiting-choice state.','Dialogue drives state, not a fixed UI. Use your own labels and buttons. State lives in a variable and can be saved.'),
('Named save slots','save.','Storage / Save Slots','Save/restore/list/delete named scene snapshots, including scene/object variables and globals. Writes/loads expose pending, finished, succeeded and failed states and readable errors. Restores occur at a safe scene boundary.','Scene capture/apply runs on the game thread; desktop disk work runs asynchronously. Browser virtual-file work completes through the same operation lifecycle. This is native serialized scene state, not a snapshot of pending waits, active HTTP requests or decoder state. Slot names: letters, digits, underscore or hyphen, up to 80 characters.'),
('Touch lifecycle and IDs','touch.','Input / Touch','Test started/held/ended/cancelled touches and read stable IDs, normalized positions and active count.','Browser pointer events feed touch/pen input; cancellation handles focus loss. Desktop hosts can feed TouchInput explicitly; native Windows touch-device integration is not added.'),
('Visibility and opacity','renderer.','Renderer / Object Appearance','Show/hide native renderers without disabling the object, and set sprite/mesh/skinned-material opacity.','Opacity is a renderer-local override; it does not modify shared material assets. Choose a skinned material slot where needed.'),
('Audio seek and playback status','audio.seek|audio.isPaused|audio.isStopped|audio.hasFinished|audio.readPosition|audio.readDuration|audio.setBus|audio.setBusVolume|audio.readBusVolume|audio.duckBus|audio.stopBus','Audio / Playback and Mixer','Seek, read playback position/duration and test paused/stopped/naturally-finished state. Route sources to Master/Music/Sfx/Ui buses; set/read bus volume, duck a bus or stop its scene sources.','Native buffered and WAV/Ogg streaming paths support seeking; browser audio has matching adapters. Finished is a retained state until another play/stop, not a one-frame trigger.'),
('Object distance and direction','object.distanceBelow|object.copyDistance|object.copyDirection','Object / Spatial','Compare distance and copy distance or normalized direction between two objects.','Direction is zero when both positions coincide.'),
('HTTP requests','network.send|network.cancel|network.remove|network.finished|network.succeeded|network.failed|network.readResponse','Network / HTTP','Send named GET/POST/PUT/PATCH/DELETE requests, cancel/remove them, test results and read a response structure.','Asynchronous; response changes are published during scene update. Limits: 16 concurrent requests, 256 retained results, 4 MiB response, 30-second timeout. Browser CORS rules apply.'),
('External URL opening','network.openUrl','Network','Open an HTTP/HTTPS page in the system browser or a browser tab.','Browser popup rules apply; invoke from a player interaction. Other URL schemes are rejected.'),
('Video controls','video.','Video / Video Player component','Attach Video Player, set a project-relative Source under Assets, and use play/pause/stop/seek/playing/paused/ended/error/position controls. Screen Position and Screen Size are normalized to the viewport.','Windows uses bounded Media Foundation decoding, up to 3840 x 2160, and needs a separate Audio Source for sound (enable Use Companion Audio). Browser uses HTML video with embedded sound. Codec availability differs; errors are exposed to events. No embedded Windows audio decoding or texture/world-surface video is added.'),
('Reusable event functions','function.','Flow / Functions','Select an event header. Give it a Function name and apply parameter-default JSON in Details. It becomes callable and no longer runs each update. Call event function accepts named parameters; Read function parameter and Set function return value exchange data.','Functions are synchronous, local to a sheet instance, and limited to 16 nested calls. Wait is rejected inside a function. Defaults define parameter types; null allows any type.'),
]
def matches(identifier,pattern):
    return any(identifier.startswith(p) if p.endswith('.') else identifier==p for p in pattern.split('|'))
rows=[]
for title,pattern,menu,usage,limits in areas:
    entries=[dict(kind=kind,**e) for kind in ('conditions','actions') for e in registry[kind] if matches(e['Id'],pattern)]
    assert entries, 'No registered tools found for '+title
    rows.append(dict(title=title,menu=menu,usage=usage,limits=limits,entries=entries))
e=html.escape
cards=''.join(f'<section><h2>{n:02d} · {e(row["title"])}</h2><p class="where">{e(row["menu"])}</p><p>{e(row["usage"])}</p><p class="limits">{e(row["limits"])}</p><details><summary>Registered conditions and actions ({len(row["entries"])})</summary><ul>'+''.join(f'<li><b>{"Condition" if x["kind"]=="conditions" else "Action"}</b> — {e(x["DisplayName"])} <code>{e(x["Id"])}</code></li>' for x in row['entries'])+'</ul></details></section>' for n,row in enumerate(rows,1))
page='''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>ByteEngine · Added event tools</title><style>body{font:17px/1.55 system-ui;background:#10171d;color:#e4edf1;margin:0}main{max-width:1040px;margin:auto;padding:48px 24px}h1{font-size:42px;line-height:1.1}h2{font-size:24px}section{background:#1b252e;padding:24px;margin:18px 0;border-radius:12px;border-left:4px solid #61bdd6}.where{color:#8bd2e5}.limits{font-size:15px;color:#b6c4cd}code{color:#f2bf6e;font-size:13px}li{margin:8px 0}.note{padding:18px;background:#293027;border-radius:10px}a{color:#8bd2e5}@media print{body{color:#111;background:white}section{break-inside:avoid;background:#fff;border:1px solid #ccc}.limits,.where{color:#333}code{color:#333}}</style><main><p>BYTEENGINE / EVENT TOOLKIT / 20 OCTOBER 2026</p><h1>Added event tools</h1><p>Native engine additions covering the 23 areas identified in the GDevelop audit. This is a practical feature guide, not a claim of complete GDevelop API parity.</p><p class="note">Existing event files remain compatible. No game export was created. Each section explains where to find the tools and the limits of this implementation.</p>'''+cards+'''<section><h2>Dialogue format</h2><pre>{
  "start": "hello",
  "nodes": {
    "hello": {
      "speaker": "Guide",
      "text": "Take the reward?",
      "choices": [{"text": "Yes", "next": "reward"}]
    },
    "reward": {"speaker": "Guide", "text": "Good luck."}
  }
}</pre><p>A node without a next destination ends the dialogue. Choice indices start at zero.</p></section><section><h2>Function example</h2><p>Name an event GiveReward. Apply defaults <code>{"amount":5}</code>. In its actions, read amount into a variable and use it in your reward logic. Call GiveReward with <code>{"amount":10}</code> from another event.</p></section></main></html>'''
out=Path('artifacts/gdevelop-audit');out.mkdir(parents=True,exist_ok=True)
(out/'Event toolkit - added features.html').write_text(page,encoding='utf-8')
(out/'implementation-status.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
text='BYTEENGINE - ADDED EVENT TOOLS\n20 October 2026\n\n'
for n,row in enumerate(rows,1):text+=f'{n:02d}. {row["title"]}\nFind: {row["menu"]}\n{row["usage"]}\nLimits: {row["limits"]}\n\n'
Path('Docs/Event toolkit implementation.txt').write_text(text,encoding='utf-8')
(out/'Event toolkit - added features.txt').write_text(text,encoding='utf-8')
audit=out/'ByteEngine conditions and actions audit.html'
if audit.exists():
    original=audit.read_text(encoding='utf-8')
    if 'byteengine-implementation-note' not in original:
        note='<aside id="byteengine-implementation-note" style="padding:20px;border:2px solid #75c9a2;border-radius:10px;background:#21332c"><b>Implementation update — 20 October 2026</b><p>This page preserves the original gap audit. Native tools have since been added across the 23 areas. See the <a href="Event%20toolkit%20-%20added%20features.html">added-features guide</a> for current controls and implementation limits.</p></aside>'
        original=original.replace('<main>','<main>'+note,1)
        audit.write_text(original,encoding='utf-8')
print(f'{len(rows)} areas documented; registry now has {len(registry["conditions"])} conditions and {len(registry["actions"])} actions.')
