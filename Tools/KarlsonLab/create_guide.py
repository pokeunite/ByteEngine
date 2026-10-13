from pathlib import Path
import json
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, Image
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.colors import HexColor, white
from reportlab.lib.enums import TA_LEFT
from pypdf import PdfReader

root=Path('C:/Users/codex/Documents/KarlsonByte')
out=root/'Design/Karlson Byte Lab - Engine Integration.pdf'
styles=getSampleStyleSheet()
styles.add(ParagraphStyle(name='TitleLab',fontName='Helvetica-Bold',fontSize=27,leading=32,textColor=HexColor('#102a3a'),spaceAfter=15))
styles.add(ParagraphStyle(name='SubLab',fontSize=11,leading=16,textColor=HexColor('#187693'),spaceAfter=13))
styles.add(ParagraphStyle(name='BodyLab',fontSize=10,leading=14,spaceAfter=9,textColor=HexColor('#263e4b')))
styles.add(ParagraphStyle(name='SmallLab',fontSize=8,leading=11,spaceAfter=5,textColor=HexColor('#263e4b')))
styles['Heading2'].textColor=HexColor('#187693')
story=[]
def p(text,style='BodyLab'): return Paragraph(text,styles[style])
def add(text,style='BodyLab'): story.append(p(text,style))
def table(rows,widths):
    t=Table([[p(c,'SmallLab') for c in row] for row in rows],colWidths=widths,hAlign='LEFT')
    t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),HexColor('#e1f2f6')),('VALIGN',(0,0),(-1,-1),'TOP'),('LEFTPADDING',(0,0),(-1,-1),8),('RIGHTPADDING',(0,0),(-1,-1),8),('TOPPADDING',(0,0),(-1,-1),8),('BOTTOMPADDING',(0,0),(-1,-1),8),('LINEBELOW',(0,0),(-1,-1),.4,HexColor('#ccdce4'))]))
    story.append(t);story.append(Spacer(1,10))
def page(title): story.append(PageBreak());add(title,'TitleLab')
def footer(canvas,doc):
    canvas.setStrokeColor(HexColor('#ccdce4'));canvas.line(42,37,553,37)
    canvas.setFont('Helvetica',8);canvas.setFillColor(HexColor('#526c7a'))
    canvas.drawString(42,24,'BYTEENGINE / KARLSON LAB - 12 OCTOBER 2026');canvas.drawRightString(553,24,str(doc.page))

add('KARLSON / BYTE LAB','TitleLab')
add('Playable free-prototype mechanics study and reusable engine integration','SubLab')
story.append(Image(str(root/'Preview/weapon-equipped.png'),width=510,height=287))
story.append(Spacer(1,12))
add('A desktop ByteEngine project with four original editable courses: movement, wall running, grapple traversal, and a mixed milk run. It recreates the central loop of the supplied free Karlson prototype using newly written native systems and original primitive art/audio. It is not a reconstruction of the unreleased commercial campaign or the original level layouts.')
add('Open and play','Heading2')
add('Run <b>Open in ByteEngine.cmd</b> in Documents/KarlsonByte, or open <b>KarlsonByte.byteproject</b> with the rebuilt Debug editor. Press Play, click Game View, and select a course. A level waits for mouse capture before its timer and physics begin.')
add('WASD move; mouse look; Space jump/wall jump; Shift sprint; C or Ctrl crouch/slide; RMB grapple; E pick up or grab/release; LMB shoot; Q throw gun; F melee/throw prop; Tab slow motion; R retry; Escape pause; M music. The menu and pause screen expose sensitivity, music and shake settings.','SmallLab')

page('Engine features added')
add('The core contains mechanisms useful across games. The plugin supplies Karlson-specific policy, tuning, presentation and course rules. Existing defaults remain at normal simulation speed.')
table([['Core feature','Purpose and editable surface'],
['HitscanEmitter3D','Bounded ray shooting, fire cooldown, damage, physical impulse, hit event and layer mask. Serialized native component; combat.fireHitscan event action. Input, ammo and equipment remain outside the component.'],
['LaunchVolume3D','Native trigger launches a character motor or dynamic rigidbody. Configurable velocity, additive behavior, horizontal preservation for characters and reentry delay. Used by authored launch-pad blueprints.'],
['ResourceMeter3D','Bounded resource with spending, restoration, change notification and initialization. Drives focus without making slow motion a required core behavior. resource.spend / resource.restore actions.'],
['Scene simulation time','Runtime scene SimulationTimeScale and SimulationTime; Time.UnscaledDeltaTime for UI and timers. Scene-local scope restores host timing and fixed-step timing after update. Slow motion and pause reuse native physics.'],
['HealthComponent.Revive','Resets health and the once-per-life death event. health.revive event action. Available for reuse; this game retries by reloading its authored scene.']], [137,373])
add('Core source','Heading2')
add('C:/Users/codex/ByteEngine/Engine/ByteEngine.Core<br/>Gameplay/HitscanEmitter3D.cs, LaunchVolume3D.cs, ResourceMeter3D.cs, HealthComponent.cs<br/>Time.cs; Scene/Scene.cs; Serialization/ComponentSerializer.cs and HitscanEmitter3DCodec.cs; VisualLogic/VisualLogicRegistry.PhysicsCombat.cs','SmallLab')
add('All three new components are exposed in the editor Add Component menu. Hitscan has an explicit layer-mask codec so scene roundtrips preserve filtering. Existing native CharacterController3D, Rigidbody3D, colliders, CableRenderer3D, cameras, UI, audio, lifetime effects, scene loading and saves provide the rest of the foundation.')

page('Plugin and authored content')
add('Canonical source: <b>C:/Users/codex/ByteEngine/Plugins/Karlson</b><br/>Plugin ID: bytebard.karlson; version 1.0.0. Built package in engine PluginLibrary and artifacts/plugins; synchronized copies in the project Plugins, PluginLibrary and PluginSource/Karlson.','SmallLab')
table([['Plugin component','Responsibility'],
['ParkourPlayer3D','Acceleration and momentum, crouch clearance, slide, wall rays and wall jump, spring/reeling grapple, look/FOV/roll, interaction and prop grab. Supplies velocity intent to the existing native capsule motor.'],
['KarlsonWeapon3D','Physical pickup/equip/drop/throw, ammo, recoil and short-lived shot traces; delegates shooting to the core emitter. Readable held-weapon color and interpolation reset on equip/drop.'],
['KarlsonEnemy3D','Staggered line-of-sight sensing, pursuit, escalating shot accuracy, health response, physical tumbling death and weapon drop. Simple direct pursuit, not navigation around complex mazes.'],
['KarlsonProp3D','Grab/throw impact damage, breakable glass, explosive barrel and six temporary fragments. Radial blast uses native overlap queries; blast currently has no wall-occlusion test.'],
['KarlsonDirector3D','Menu, pause, retry, results, focus time scaling, timers/medals, cached HUD/audio, settings and persistent best times. Context-aware codec supplies project save scope.'],
['MilkGoal3D / CourseGate3D','Objective proximity and gate motion expose state to authored event modules. The modules decide completion and opening.']], [135,375])
add('Editable engine assets','Heading2')
add('Scenes: MainMenu, Course01 Orientation, Course02 Wall Run, Course03 The Gap, Course04 Milk Run. Nine native blueprints cover player, two weapon variants, sentry, milk, barrel, crate, glass and launch pad. Eleven generated audio cues include music. UI is authored native canvas/text/widgets.')
add('<b>MilkCompletion.byteevents:</b> goalReached + triggerOnce -&gt; completeCourse.<br/><b>CombatGate.byteevents:</b> enemiesCleared + triggerOnce -&gt; openGate.<br/>The second weapon asset is named Scattergun for authoring compatibility, but implements a slower, stronger single-ray impact rifle. It does not simulate pellet spread.','SmallLab')
add('No skeletal animation profile is required by the primitive models. Camera bob/roll/recoil and enemy death motion are procedural. The reusable animation system was not changed for this project.','SmallLab')

page('Validation and practical limits')
v=json.loads((root/'Preview/validation.json').read_text())
r=json.loads((root/'Preview/render-validation.json').read_text())
add(f'<b>{v["checks"]} gameplay/serialization checks passed.</b> Separate package loading succeeds for all five scenes with 102-145 authored objects per scene. Native OpenGL ran {r["nativeFrames"]} frames and produced eight 1280 x 720 screenshots without capture errors.')
table([['Evidence','What it proves'],
['Native input and physics','Grounding, movement, jump, collision crouch, momentum slide, side-wall detection, outward wall jump, grapple attachment/pull/release and a complete gap crossing onto the raised landing without teleporting the crossing.'],
['Combat and events','E pickup and prop grab/throw; actual LMB shot breaks glass; stationary player can die from enemy fire; enemy motor switches to tumbling body and drops gun; barrel damages nearby enemy; fragments expire; authored gate and milk event actions run.'],
['Engine state','Scene roundtrips, remapped blueprint camera IDs, persisted hitscan mask, resource bounds, launch trigger, pause freezing physics, unscaled UI timing, host time-scope restoration, health revival and best-time reload.']], [116,394])
add('The objective tests place the player near milk to isolate event wiring. They do not claim full end-to-end playthroughs of all four courses. Wall-running is checked from a prepared gap position. The grapple crossing is an actual movement sequence. Native screenshots validate presentation in the engine runtime harness; packaged scene loading is a separate check.','SmallLab')
add('Performance choices','Heading2')
add('Fixed simulation with four catch-up steps; cached UI, sound, player and enemy references; LOS sensing every 0.12 seconds with enemy staggering; HUD text refresh at 20 Hz; six-piece debris with two-second lifetime; short-lived shot trails and scene object caps. Primitive shared geometry keeps assets small. No broad performance benchmark has been completed.')
add(f'GPU: {r["gpu"]}. One short render run sampled approximately {r["meanUpdateMs"]:.3f} ms mean CPU scene-update time. This excludes a complete frame budget, does not represent heavy combat, and is a diagnostic rather than a frame-rate promise.','SmallLab')
add('Rebuild and check','Heading2')
add('From the engine repository: dotnet build Plugins/Karlson/Karlson.Plugin.csproj; dotnet run --project Tools/KarlsonLab/KarlsonLab.csproj -- --validate; --package-check; --smoke. Rebuild the Debug editor for the new core components. The authoring tool --refresh-content regenerates these original lab scenes, blueprints and audio; use it only when deliberately replacing generated content.','SmallLab')
add('Desktop is the validated target. Browser gameplay needs the new plugin statically linked into the browser runtime and a fresh build; this task did not publish or export a game. Art remains a primitive prototype; no skeletal ragdoll, commercial story, original levels or original assets are included.','SmallLab')

page('Research and project map')
add('Research scope','Heading2')
add('Official game pages and the user-supplied free prototype code informed the mechanics. Developer videos were located, but streaming could not be accessed in this session; no claim is made that they were watched. All ByteEngine implementations and procedural assets here are newly authored.')
for label,url in [('Dani - free Karlson prototype','https://danidev.itch.io/karlson'),('Official commercial game description','https://store.steampowered.com/app/1228610/KARLSON/'),('Developer video index','https://danidev.netlify.app/videos/'),('Making a Game Because He Said I Couldn\'t - video reference','https://www.youtube.com/watch?v=yqmiuPF9YEU'),('Developer AI video - reference','https://www.youtube.com/watch?v=kowCrRPqRMM')]:
    add(f'<b>{label}</b><br/><link href="{url}" color="#187693">{url}</link>','SmallLab')
add('Local study source: D:/ue4 sources/decompile game code/Karlson. Prior classification: D:/ue4 sources/decompile game code/Engine System Review/ByteEngine Component and Plugin Review.pdf. The present lab implements a focused subset and tests its engine integration.','SmallLab')
add('Where to work','Heading2')
table([['Location','Use'],['Documents/KarlsonByte/Scenes','Edit courses and menu as normal engine scenes.'],['Documents/KarlsonByte/Assets/Blueprints','Tune reusable player, weapons, enemy and physical props.'],['Documents/KarlsonByte/Assets/Events','Edit objective and gate rules in the event editor.'],['ByteEngine/Plugins/Karlson','Canonical plugin implementation. Rebuild, then synchronize package and source into the project.'],['ByteEngine/Engine/ByteEngine.Core','Reusable engine components, timing and event actions.'],['ByteEngine/Tools/KarlsonLab','Reproducible authoring, package checks, physics validation and native screenshot harness.'],['Documents/KarlsonByte/Preview','Gameplay check report, native render diagnostics and screenshots.']], [238,272])
add('Suggested next engine experiment','Heading2')
add('After hands-on movement tuning, extract a policy-free grapple or interaction component only if a second game needs the same contract. Add explosion occlusion and a proper skeletal death profile when the game uses complex indoor cover or rigged characters. Keep course progression and Karlson movement tuning in the plugin.')

doc=SimpleDocTemplate(str(out),pagesize=(595.28,841.89),leftMargin=42,rightMargin=42,topMargin=40,bottomMargin=49,title='Karlson Byte Lab - Engine Integration',author='ByteEngine Lab')
doc.build(story,onFirstPage=footer,onLaterPages=footer)
reader=PdfReader(out)
assert len(reader.pages)==5, f'Unexpected overflow: {len(reader.pages)} pages'
print(f'Created {out} - {len(reader.pages)} pages')
