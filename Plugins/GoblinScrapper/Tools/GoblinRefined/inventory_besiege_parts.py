from pathlib import Path
import json,re,html,hashlib,csv
ROOT=Path('.artifacts/besiege-full-source');OUT=Path('output/goblin-scraper/besiege-parts-audit');OUT.mkdir(parents=True,exist_ok=True)
# Explicit mapping to installed behavior classes. Shared behavior is recorded rather than inventing a separate implementation per icon.
DATA='''StartingBlock|Master block|Structure|SourceBlock|core|Protected machine origin; only one root, with no mapped inputs.
DoubleWoodenBlock|Long wooden beam|Structure|BlockBehaviour|beam|Long rigid structural member; attach components on its faces and connectable ends.
Wheel|Powered wheel|Drive|CogMotorControllerHinge|wheel|Motor-driven rolling joint; forward/reverse, speed, acceleration, auto-brake and automatic operation.
MetalBlade|Metal blade|Weapons|BlockBehaviour,CogMotorDamage|blade|Passive sharp collision weapon; damages things it contacts without an activation key.
Decoupler|Decoupler|Mechanics|ShorteningBlock|decoupler|Releases its attachment when activated, separating the connected machinery.
Hinge|Free hinge|Mechanics|HingeBlockBehaviour|hinge|Allows attached bodies to rotate freely about a single hinge axis; has no steering motor.
MetalBall|Metal ball|Weapons|BlockBehaviour|sphere|Heavy rigid spherical payload used for impacts and balancing.
Brace|Brace|Structure|BraceCode|brace|Rigid support between two selected endpoints; reinforces a machine against bending and twisting.
Unused|Unused identifier|Internal|BlockBehaviour|unused|Reserved enum identifier; not a usable part.
Spring|Contractable spring|Mechanics|SpringCode|spring|Connects two endpoints with a tension spring and can contract on activation.
WoodenPanel|Wooden panel|Structure|BlockBehaviour|panel|Flat wooden covering; contributes structural support, collision and mass.
Cannon|Cannon|Weapons|CanonBlock|cannon|Fires a physical cannonball and transfers recoil to its mounting body; adjustable strength.
ScalingBlock|Scaling block|Structure|ScalingBlock|core|Structural block with configurable mass/density and optional mass proportional to scaled volume.
SteeringBlock|Steering block|Mechanics|SteeringWheel|steer|Motorized swivel; rotates its connected assembly within configured limits, optionally returning to center.
FlyingBlock|Powered flying fan|Flight|FlyingController|fan|Powered fan produces directional lift/thrust; speed, reverse, automatic and toggle modes.
SingleWoodenBlock|Short wooden beam|Structure|BlockBehaviour|beam|Short rigid structural member for frames, offsets and attachment branches.
Suspension|Suspension|Mechanics|SuspensionController|suspension|Spring-damped sliding joint: its output travels along one axis while lateral movement is constrained.
CircularSaw|Circular saw|Weapons|CogMotorControllerHinge,CogMotorDamage|saw|Continuously rotating toothed weapon; motor speed/acceleration and sharp contact damage.
Piston|Piston|Mechanics|SliderController|piston|Powered linear actuator extends/retracts its output; activation, toggle and inversion settings.
Swivel|Free swivel|Mechanics|FreeWheel|swivel|Free rotating joint around its central shaft; attached machinery rotates with the output.
Spike|Metal spike|Weapons|BlockBehaviour|spike|Passive pointed contact weapon; no firing or rotation control.
Flamethrower|Flamethrower|Weapons|FlamethrowerController,RepeatFireBlock|flame|Emits an activated flame stream that heats metal and ignites combustible objects; limited fuel.
SpinningBlock|Spinning motor|Mechanics|CogMotorControllerHinge|motor|Motorized shaft rotates its attached output continuously; speed and acceleration settings.
Bomb|Impact bomb|Weapons|ExplodeOnCollideBlock|bomb|Explodes from sufficiently strong contact or damage; radial blast pushes and damages nearby bodies.
ArmorPlateSmall|Small armor plate|Armor|ArmorBlock|armor|Small metal protective surface; passive collision protection with added mass.
Wing|Wing|Flight|PropellorController|wing|Passive aerodynamic surface: lift and drag depend on motion relative to its orientation.
Propeller|Aerodynamic propeller|Flight|PropellorController,RotationalThrustBlockBehaviour|propeller|Passive angled blades create lift/drag when moved or rotated; requires mechanical drive to spin.
Grabber|Grabber|Mechanics|GrabberBlock|grabber|Creates/releases a temporary attachment to nearby objects; automatic and static-only grab modes.
SteeringHinge|Steering hinge|Mechanics|SteeringWheel|steeringhinge|Driven hinge rotates connected bodies around one axis; left/right controls, limits and centering.
ArmorPlateRound|Round armor plate|Armor|ArmorBlock|roundarmor|Circular metal protective plate; passive protection and collision.
BombHolder|Bomb holder|Weapons|JoinOnTriggerBlock|holder|Holds an explosive payload through a contact attachment; release detaches the held object.
FlameBall|Flaming ball|Weapons|FireBallDamage|flameball|Burning spherical payload heats/ignites on contact; can be extinguished and reignited.
ArmorPlateLarge|Large armor plate|Armor|ArmorBlock|armor|Large metal protective surface; greater coverage and mass than a small plate.
Plow|Plow|Weapons|BlockBehaviour|plow|Wide rigid pushing wedge for ramming and clearing objects; passive contact behavior.
WingPanel|Wing panel|Flight|PropellorController|wingpanel|Broad passive aerodynamic panel; converts motion and angle of attack into lift and drag.
Ballast|Ballast|Structure|BallastWeightController|ballast|Adjustable mass used to shift center of gravity and balance a machine.
Boulder|Boulder|Weapons|BoulderController|rock|Heavy throwable impact payload which can fracture under sufficiently strong impact.
HalfPipe|Half pipe|Structure|BlockBehaviour|halfpipe|Curved channel supporting and guiding rolling payloads; passive rigid collision surface.
CogMediumUnpowered|Free medium cog|Drive|CogFreeController|gear|Unpowered toothed rotating joint; receives motion through physical contact with another cog.
CogMediumPowered|Powered medium cog|Drive|CogMotorControllerHinge|gear|Motorized toothed joint drives meshing gears and attached machinery; adjustable motor settings.
WheelUnpowered|Free wheel|Drive|FreeWheel|wheel|Freely rolling wheel without a motor; supplies ground contact and rolls when pushed.
WoodenPole|Wooden pole|Structure|BlockBehaviour|pole|Narrow long structural member for lightweight frames and offsets.
Slider|Free slider|Mechanics|SliderBlock|slider|Passive rail joint allows attached bodies to translate along its axis; has no extension motor.
Balloon|Round balloon|Flight|BalloonController|balloon|Buoyant tethered balloon with configurable buoyancy and string length; balloon and tether can break.
BallJoint|Ball joint|Mechanics|MyJointController|balljoint|Allows connected bodies to rotate about all three axes at one pivot.
RopeWinch|Rope and winch|Mechanics|RopeJoint|winch|Connects two endpoints with a flexible tension-only rope; winch winds/unwinds the rope.
LargeWheel|Large powered wheel|Drive|CogMotorControllerHinge|wheel|Larger motor wheel with forward/reverse, automatic, toggle, speed and auto-brake controls.
Torch|Small torch|Weapons|TorchesController|torch|Persistent small flame source that heats or ignites nearby objects without a firing key.
Drill|Drill|Weapons|CogMotorControllerHinge,CogMotorDamage|drill|Continuously rotating drill shaft; angular motor and sharp contact damage.
GripPad|Grip pad|Drive|BlockBehaviour|grip|High-friction contact pad for feet, supports and grabbing ground; no motor.
SmallWheel|Small caster wheel|Drive|SmallWheel|caster|Small passive rolling support/caster; follows local movement instead of supplying motor power.
CogLargeUnpowered|Free large cog|Drive|CogFreeController|gear|Large unpowered gear rotates from contact forces and transmits movement through meshing teeth.
Unused3|Unused identifier 52|Internal|BlockBehaviour|unused|Reserved enum identifier; not a usable part.
ShrapnelCannon|Shrapnel cannon|Weapons|ShrapnelCannon,CanonBlock|shrapnel|Fires multiple short-lived shrapnel projectiles and transfers recoil to its mount.
Grenade|Remote grenade|Weapons|ExplodeOnCollideBlock|grenade|Activated explosive charge; detonation also responds to damage and configured delays.
SmallPropeller|Small aerodynamic propeller|Flight|PropellorController,RotationalThrustBlockBehaviour|propeller|Small passive pitched propeller; generates lift/drag when mechanically moved or rotated.
WaterCannon|Water cannon|Weapons|WaterCannonController,RepeatFireBlock|water|Activated water jet pushes contacted objects and interacts with heat; hot water can become steam.
Pin|Pin|Mechanics|PinBlock|pin|Pins an attachment to the world until released; visual hiding is configurable.
CameraBlock|Camera block|Automation|FixedCameraBlock|camera|Provides selectable machine-mounted camera views and tracking/first-person camera settings.
Rocket|Explosive rocket|Weapons|TimedRocket|rocket|Timed propelled explosive; launch and detonation behavior with exhaust and recoil interactions.
LargeWheelUnpowered|Large free wheel|Drive|FreeWheel|wheel|Large freely rotating rolling support with no propulsion motor.
Crossbow|Repeating crossbow|Weapons|CrossBowBlock,RepeatFireBlock|crossbow|Repeating bolt weapon with fire-rate/ammunition and hold/toggle controls.
Vacuum|Vacuum|Mechanics|VacuumBlock,VacuumController,RepeatFireBlock|vacuum|Activated suction field pulls nearby bodies along its intake direction.
Log|Log|Structure|LogController|log|Thick long structural log with collision, mass and fracture/burning behavior.
Magnet|Magnet (internal)|Internal|MagnetBlock,MagnetController|magnet|Magnetic attraction implementation exists in code; no numbered palette icon was found in this install.
Sensor|Sensor|Automation|SensorBlock|sensor|Detects objects in a configured volume and emits a mapped output; range, inversion and static filters.
Timer|Timer|Automation|TimerBlock|timer|Emits mapped output after a configured delay; automatic, start/stop, duration and looping options.
Altimeter|Altimeter|Automation|AltimeterBlock|altimeter|Compares altitude to a configurable threshold and emits an output; configurable reference/inversion.
LogicGate|Logic gate|Automation|LogicGate|logic|Combines mapped inputs using a selected Boolean gate and emits mapped output.
Anglometer|Angle meter|Automation|AnglometerBlock|anglometer|Measures angular orientation against a configured reference/threshold and emits a mapped output.
Speedometer|Speed meter|Automation|SpeedometerBlock|speedometer|Measures speed against a configurable threshold and emits a mapped output.
BuildNode|Surface corner node|Internal|BuildNodeBlock|node|Internal construction handle defining a procedural surface corner; not an ordinary functional block.
BuildEdge|Surface edge handle|Internal|BuildEdgeBlock|edge|Internal handle controlling procedural surface edge curvature; not an ordinary palette block.
BuildSurface|Build surface|Armor|BuildSurface,SurfaceVisualController|surface|Procedural three/four-corner surface with adjustable shape, material, aerodynamics and collision.
SqrBalloon|Square balloon|Flight|SqrBalloonController|squareballoon|Square tethered buoyancy block with balloon-specific lift and tether controls.
RopeMeasure|Rope length meter|Automation|RopeMeasure|ropemeter|Two-endpoint distance/length detector; compares measured separation with a configured threshold.
Axle|Axle|Mechanics|MyJointController|axle|Mechanical shaft/support transfers attached motion; passive joint behavior depends on prefab configuration.
MetalJaw|Metal jaw|Weapons|SpringReleaseBlock|jaw|Winds and releases a sprung biting joint; angle, force and automatic reset settings.
Sail|Sail|Water|SailBlock|sail|Cloth sailing surface catches relative wind; sail force depends on area and orientation.
Rudder|Rudder|Water|RudderController|rudder|Directional hydrodynamic surface produces steering force in moving water.
NauticalScrew|Powered nautical screw|Water|NauticalScrew|screw|Motor-driven marine propeller produces forward/reverse propulsion while submerged.
Paddle|Paddle|Water|PaddleBlock|paddle|Powered paddle applies propulsion through its stroke and water contact.
Buoyancy|Buoyancy block|Water|BuoyancyController,BuoyancyDensityController|float|Adjustable-density float creates displacement-based buoyancy when submerged.
BigBarrel|Large barrel|Water|BuoyancyController|barrel|Large barrel-shaped float/payload; buoyancy and mass depend on serialized prefab setup.
Harpoon|Harpoon|Water|HarpoonController|harpoon|Launches a barbed projectile/tether that can attach to a target and pull through its rope.
CornerWoodenBlock|Corner wooden beam|Structure|CurvedBlock|corner|Curved/corner structural beam changes frame direction while supplying rigid attachment faces.
SkateWheel|Skate wheel|Drive|SkateboardWheel|skate|Passive skateboard-style rolling support; rolling axis limits lateral ground movement.
BouncyPad|Bouncy pad|Drive|BouncyPadBlock|bounce|Elastic contact pad rebounds impacts; adjustable spring/contact response.
FlyWheel|Flywheel|Mechanics|FlyWheelBlock|flywheel|Rotating inertial mass stores angular momentum; motor and kinetic response controlled by block settings.
DragBlock|Directional drag block|Flight|DragBlock|drag|Directional drag surface resists motion along its configured orientation; adjustable magnitude.
Booster|Booster|Space|ThrusterBlockBehaviour|booster|Fuel-consuming thruster applies directional thrust with throttle, ignition and fuel-network behavior.
SteeringThruster|Steering thruster|Space|SteeringThrusterBehaviour|steeringthruster|Fuel-powered thruster whose direction can be controlled; thrust and steering act together.
FuelBarrel|Fuel tank|Space|FuelBlockBehaviour,FragileFuelBlockBehaviour|fuel|Stores fuel for connected consumers; contents, transfer and damage behavior belong to the fuel network.
FuelGauge|Fuel gauge|Automation|FuelMeterBehaviour|fuelgauge|Displays/detects fuel state in connected tanks/network and exposes mapped outputs.
GridFin|Grid fin|Space|GridFinBlock|gridfin|Grid-shaped aerodynamic control surface generates directional forces while moving through atmosphere.
SteeringFin|Steering fin|Space|SteeringWheel,PropellorController|steeringfin|Actuated aerodynamic fin combines steering angle with directional aerodynamic force.
FuelLine|Fuel line|Space|FuelLineBehaviour,FuelLine|fuelline|Two-endpoint fuel connector transfers fuel between linked network nodes.
Parachute|Parachute|Space|ParachuteBlock|parachute|Deployable canopy increases drag and slows descent; deployment and reefing depend on settings.
FuelCoupler|Fuel coupler|Space|FuelCouplerBehaviour|fuelcoupler|Fuel-network connector couples fuel supply and supports its configured connection/release behavior.
FuelBarrelBig|Large fuel tank|Space|FuelBlockBehaviour,FragileFuelBlockBehaviour|fuel|Larger fuel reservoir; capacity, mass and fragile behavior come from prefab-specific settings.
SpaceWheel|Space wheel|Space|CogMotorControllerHinge|spacewheel|Motorized specialized wheel; exact terrain/space contact behavior and tuning require prefab verification.
ReactionSteerBlock|Reaction steering block|Space|InertialSteeringBehaviour|reaction|Applies inertial steering torque without requiring ground contact; configurable steering control.
FuelCannon|Fuel cannon|Space|ThrusterBlockBehaviour,CanonBlock|fuelcannon|Fuel-related cannon/thrust weapon identifier; exact firing/consumption pairing requires serialized prefab verification.
'''
rows={r.split('|')[0]:r.split('|')[1:] for r in DATA.strip().splitlines()}
symbols=re.findall(r'^\s*(\w+),?\s*$',(ROOT/'BlockType.cs').read_text(),re.M)
prefabs={r['data']['Info']['ID']:r for r in json.loads((OUT/'level0-prefabs-decoded.json').read_text()) if 'data' in r}
icons=json.loads((OUT/'installed-reference-index.json').read_text());iconmap={int(i['name'].split('_')[0]):i for i in icons}
parts=[]
for id,symbol in enumerate(symbols):
 name,category,classes,shape,effect=rows[symbol];code=[];controls=[];missing=[]
 prefab=prefabs.get(id,{});actual=prefab.get('actual_behavior_class');classes=actual or classes
 # Walk inheritance to collect the mapper declarations actually inherited by this controller.
 pending=classes.split(',');lineage=[]
 while pending:
  cls=pending.pop(0)
  if cls in lineage:continue
  lineage.append(cls);f=ROOT/(cls+'.cs')
  if f.exists():
   m=re.search(r'public (?:abstract )?class '+re.escape(cls)+r'\s*:\s*(\w+)',f.read_text())
   if m and (ROOT/(m[1]+'.cs')).exists():pending.append(m[1])
 classes=','.join(lineage)
 if id==102:effect='Repeating fuel-fed cannon consumes fuel per shot; configurable firing rate, hold/toggle behavior, shot power and recoil.'
 if id==100:effect='Motorized lunar wheel with forward/reverse, speed, acceleration and braking settings; prefab controller is CogMotorControllerHinge.'
 if id==76:effect='Passive rigid axle/support; ignores specified connected colliders and restores collision when the attachment breaks.'
 if id in [5,19,44]:effect += ' Joint degrees of freedom are stored in the serialized mechanical output joint, not a unique motor controller.'
 for cls in classes.split(','):
  f=ROOT/(cls+'.cs')
  if not f.exists():missing.append(cls);continue
  lines=f.read_text().splitlines();code.append({'class':cls,'file':str(f.resolve()),'sha256':hashlib.sha256(f.read_bytes()).hexdigest()})
  for lineNo,line in enumerate(lines,1):
   if re.search(r'\bAdd(Key|Slider|Toggle|Limits|Menu)\(',line):controls.append({'class':cls,'line':lineNo,'declaration':line.strip()})
 file='goblin_'+re.sub(r'(?<!^)(?=[A-Z])','_',symbol).lower()
 visibility='reserved' if shape=='unused' else 'protected-root' if id==0 else 'surface-edit-handle' if id in [71,72] else 'internal/unconfirmed' if id==64 else 'reference-palette'
 animation='Spin' if shape in ['wheel','gear','motor','swivel','fan','saw','drill','propeller','caster','skate','flywheel','screw','spacewheel'] else 'Steer' if shape in ['steer','steeringhinge','rudder','steeringfin','steeringthruster'] else 'Extend' if shape in ['piston','suspension','slider'] else 'Bite' if shape=='jaw' else 'Deploy' if shape=='parachute' else 'Release' if shape in ['decoupler','grabber','holder','pin'] else 'Meter' if category=='Automation' else None
 parts.append({'id':id,'symbol':symbol,'name':name,'category':category,'visibility':visibility,'function':effect,'actual_prefab_behavior_class':actual,'prefab_info':prefab.get('data',{}).get('Info'),'dlc_type':prefab.get('data',{}).get('dlcType'),'serialized_behavior':prefab.get('behavior_data'),'behavior_decode_error':prefab.get('behavior_decode_error'),'physics_components':prefab.get('physics_components',[]),'source_classes':code,'unresolved_class_names':missing,'code_controls':controls,'reference_image':f"reference-icons/{Path(iconmap[id]['file']).name}" if id in iconmap else None,'goblin_file':file+'.glb','shape':shape,'required_animation':animation,'model_status':'not-built','runtime_status':'not-ported','parity_status':'not-verified','tuning_note':'Mapper declarations are code evidence. Serialized prefab values and force/collision behavior must be verified separately.','goblin_design':'Chipped green paint, exposed iron/wood, brass fasteners; preserve the recognizable '+shape+' silhouette. Orange moving/output assembly and visible directional marking; straight descriptive name.'})
(OUT/'besiege-parts-inventory.json').write_text(json.dumps({'source_assembly':r'C:\Besiege\Besiege_Data\Managed\Assembly-CSharp.dll','source_sha256':hashlib.sha256(Path(r'C:\Besiege\Besiege_Data\Managed\Assembly-CSharp.dll').read_bytes()).hexdigest(),'identified':len(parts),'numbered_reference_icons':len(icons),'parts':parts},indent=2),encoding='utf-8')
with (OUT/'besiege-parts-inventory.csv').open('w',newline='',encoding='utf-8-sig') as f:
 writer=csv.writer(f);writer.writerow(['ID','Code identifier','Player name','Category','Visibility','Function','Behavior classes','Reference','Goblin model','Animation','Code mapper declarations','Unresolved class mapping','Runtime parity'])
 for p in parts:writer.writerow([p['id'],p['symbol'],p['name'],p['category'],p['visibility'],p['function'],', '.join(x['class'] for x in p['source_classes']),p['reference_image'],p['goblin_file'],p['required_animation'],'\n'.join(x['declaration'] for x in p['code_controls']),','.join(p['unresolved_class_names']),p['parity_status']])
md=['# Besiege part inventory — installed build','',f"{len(parts)} enum identifiers; {len(icons)} numbered reference thumbnails. Two reserved identifiers, one protected root, one internal/unconfirmed magnet and two procedural editor handles are identified separately. The remaining 97 identifiers have numbered thumbnails.",'','Source: supplied Assembly-CSharp.dll, decompiled using ILSpy 9.1.0.7988. Actual behavior classes were resolved from all 103 serialized prefab records. 102 behavior payloads were decoded; the Rocket payload has a decoding error recorded explicitly. The JSON includes original joint/body/behavior settings and raw mapper declarations, with inherited classes included. Units remain Besiege source units; they are not assumed equal to Goblin units.','', 'The requested full port is not complete. ByteEngine currently has linear rigid-body physics without an angular/joint solver; mechanical, aerodynamic, fluid, fuel and combat parity require engine work and behavioral tests, beyond mesh animation.','', 'Online visual references: [PlayStation part selection screenshot](https://blog.ja.playstation.com/2025/01/20/20250120-besiege/), [block reference gallery](https://steamcommunity.com/sharedfiles/filedetails/?id=2400348420), [official surface-block help](https://playdigious.helpshift.com/hc/en/33-besiege/faq/608-where-is-the-build-surface-block/). Numbered per-part reference pictures were extracted from this installed game and are reference-only; they are not placed in game assets.','', '| ID | Besiege identifier / Goblin name | Function | Source class | Animation |','|---|---|---|---|---|']
for p in parts:md.append(f"| {p['id']} | {p['symbol']} / **{p['name']}** | {p['function']} | {', '.join(c['class'] for c in p['source_classes']) or 'Unresolved'} | {p['required_animation'] or 'Static'} |")
(OUT/'BESIEGE-PARTS.md').write_text('\n'.join(md),encoding='utf-8')
print('Inventory',len(parts),'records. Unresolved classes:',sorted({n for p in parts for n in p['unresolved_class_names']}))
