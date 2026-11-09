"""Review scene only. Uses the decoration kit and current game's actual machine models."""
import bpy,math,random,json
from pathlib import Path
from mathutils import Vector
W=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine');P=Path('C:/Users/codex/Documents/palclone/Assets/Models/Mining');OUT=W/'artifacts/camp-decorations'
random.seed(27)
S=bpy.data.scenes.new('Hazard Pay Decorated Camp REVIEW');bpy.context.window.scene=S
S['purpose']='REVIEW ONLY: no game layout changed';S.render.engine='BLENDER_EEVEE';S.render.resolution_x=1600;S.render.resolution_y=1000;S.render.resolution_percentage=100;S.render.image_settings.file_format='PNG';S.render.film_transparent=False
S.world=bpy.data.worlds.new('HPD sunset world');S.world.use_nodes=True;S.world.node_tree.nodes['Background'].inputs[0].default_value=(.19,.15,.28,1);S.world.node_tree.nodes['Background'].inputs[1].default_value=.5;S.view_settings.view_transform='AgX';S.view_settings.look='AgX - Medium High Contrast'
def inst(name,p,angle=0,scale=1):
 col=bpy.data.collections['HPD '+name];o=bpy.data.objects.new(name+' placement',None);o.instance_type='COLLECTION';o.instance_collection=col;S.collection.objects.link(o);o.location=p;o.rotation_euler.z=math.radians(angle);o.scale=(scale,)*3;return o
def geom(name,p,size,material,shape='box'):
 if shape=='box':bpy.ops.mesh.primitive_cube_add(size=1,location=p)
 else:bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1,location=p)
 o=bpy.context.object;o.name=name;o.scale=size;o.data.materials.append(bpy.data.materials['HPD '+material]);return o
def model(name,file,p,width,angle=0):
 before=set(bpy.data.objects);bpy.ops.import_scene.gltf(filepath=str(P/file));objects=list(set(bpy.data.objects)-before)
 col=bpy.data.collections.new('Camp machine - '+name);S.collection.children.link(col);root=bpy.data.objects.new(name+' assembly',None);col.objects.link(root)
 for o in objects:
  for c in list(o.users_collection):c.objects.unlink(o)
  col.objects.link(o)
 for o in objects:
  if o.parent not in objects:o.parent=root
 bpy.context.view_layer.update();points=[o.matrix_world@Vector(c) for o in objects if o.type=='MESH' for c in o.bound_box]
 low=Vector(tuple(min(p[i] for p in points) for i in range(3)));high=Vector(tuple(max(p[i] for p in points) for i in range(3)));factor=width/(high.x-low.x);root.scale=(factor,)*3;root.rotation_euler.z=math.radians(angle)
 center=Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z))*factor;center.rotate(root.rotation_euler);root.location=Vector(p)-center
 for o in objects:
  if o.animation_data:o.animation_data.action=None
 return root
geom('Orange sandy camp floor',(0,0,-.16),(58,100,.30),'orange sand')
# Use modular floor panels only in the working camp, leaving the battlefield open.
for x in range(-9,10):
 for y in range(-4,7):inst('15-walkway-floor-panel',(x*1.97,y*1.97,-.27))
model('Massive mining drill',Path('MiningDrill-Massive.glb'),(-11,8,0),8.5)
model('Refinery A',Path('Refinery.glb'),(-1,9,0),5.5)
model('Refinery B',Path('Refinery.glb'),(6,9,0),5.5)
model('Ammo press',Path('AmmoPress.glb'),(12.5,7,0),3.6)
model('Company tent terminal',Path('CompanyTerminal.glb'),(17,10,0),5.5)
model('Field research station',Path('Research/ResearchStation.glb'),(17,1,0),2.4)
inst('05-fabric-shade-canopy',(-18,-1,0));inst('02-spare-auger-rack',(-18,5,0));inst('03-pipe-storage-rack',(-18,2.5,0));inst('04-gas-cylinder-rack',(-15.5,-1,0))
inst('09-workshop-tool-trolley',(14.5,-.5,0),-15);inst('10-power-cable-reel',(21,4,0),-55);inst('11-portable-generator',(21,0,0),-25);inst('12-communications-mast',(21,12,0))
for name,p,angle in [('06-reinforced-cargo-crate',(-19,-2.4,0),12),('07-small-supply-cases',(-17,-1.7,0),-10),('07-small-supply-cases',(-19,-5.2,0),20),('06-reinforced-cargo-crate',(14,11,0),0),('06-reinforced-cargo-crate',(19,6.5,0),80),('06-reinforced-cargo-crate',(-5.5,12,0),-12)]:
 inst('08-cargo-pallet',p,angle);inst(name,(p[0],p[1],.36),angle)
for p in [(-19,-5.5,0),(-15,5,0),(-6,10.5,0),(10,4.4,0),(19,-2,0)]:inst('01-tripod-work-light',p,15,1.2)
for p,angle in [((18.7,1,0),-25),((18.5,3,0),45),((14.6,3.5,0),90),((-10.5,5.5,0),60)]:inst('13-ground-power-cable',p,angle,1.4)
for p in [(15.4,2.3,0),(-12,5.5,0)]:inst('14-cable-protector-ramp',p)
# Existing defences give the preview scale and orientation context; not new game placements.
defence=P/'Defences'
if not defence.exists():defence=P/'Defense'
for file in P.rglob('*.glb'):
 if file.stem.lower() in ('sentry','turret','sentryturret'):
  for x in (-12,0,12):model('Front sentry',file.relative_to(P),(x,-7.5,0),1.65,0)
  break
for x in (-13.8,-10.4,-1.7,1.7,10.3,13.7):model('Front barricade',Path('Research/FieldBarricade.glb'),(x,-9,0),2.4)
# Back and side cliffs enclose the usable site, with a wide front-facing enemy approach.
for i in range(26):
 x=-27+i*2.2;height=random.uniform(4.8,8.8);geom('Back canyon boulder',(x,18+random.uniform(-1,1),height*.43),(random.uniform(1.8,3),random.uniform(2,3.8),height*.58),'purple canyon','rock')
for side in (-1,1):
 for i in range(15):
  y=14-i*3.1;height=random.uniform(3.5,7);geom('Side canyon boulder',(side*(26+random.uniform(-.8,.8)),y,height*.44),(random.uniform(1.7,2.8),random.uniform(2,3.5),height*.60),'purple canyon','rock')
for i in range(35):
 x=random.choice((-1,1))*random.uniform(23,26);y=random.uniform(-30,17);geom('Small edge rubble',(x,y,.18),(random.uniform(.2,.7),random.uniform(.2,.7),random.uniform(.2,.6)),'purple canyon','rock')
def aim(o,target):o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.light_add(type='SUN',location=(0,-10,30));sun=bpy.context.object;sun.name='Warm sunset sun';sun.data.energy=2.0;sun.data.angle=.2;sun.data.color=(1,.76,.48);sun.rotation_euler=(math.radians(27),math.radians(-22),math.radians(-35))
bpy.ops.object.light_add(type='AREA',location=(-5,-15,24));fill=bpy.context.object;fill.name='Soft sky fill';fill.data.energy=4000;fill.data.shape='DISK';fill.data.size=25;fill.data.color=(.62,.76,1);aim(fill,(0,4,0))
for label,eye,target,ortho in [('Camp overview',(35,-46,33),(0,3,1),61),('Camp entrance eye level',(3,-23,3.5),(0,6,2),0),('Workshop detail',(-26,-13,12),(-15,2,1),20)]:
 bpy.ops.object.camera_add(location=eye);cam=bpy.context.object;cam.name=label;aim(cam,target);cam.data.lens=30;cam.data.clip_end=400
 if ortho:cam.data.type='ORTHO';cam.data.ortho_scale=ortho
 if label=='Camp overview':S.camera=cam
S.render.filepath=str(OUT/'Decorated-Camp-Overview.png')
# Small assembly correction pass: join all visible supports and stacked case contact points.
for o in bpy.data.collections['HPD 05-fabric-shade-canopy'].objects:
 if 'canopy upright' in o.name:o.scale.z*=2.50/2.40;o.location.z+=.05
for o in bpy.data.collections['HPD 07-small-supply-cases'].objects:
 if o.location.z>=1.35:o.location.z-=.126
 elif o.location.z>=.76:o.location.z-=.065
# Connected U-bracket for the twin lamp heads.
col=bpy.data.collections['HPD 01-tripod-work-light']
for x in (-.68,.68):
 bpy.ops.mesh.primitive_cube_add(size=1,location=(x,0,2.27));o=bpy.context.object;o.name='HPD floodlight yoke vertical';o.dimensions=(.06,.07,.34);o.data.materials.append(bpy.data.materials['HPD silver'])
 for c in list(o.users_collection):c.objects.unlink(o)
 col.objects.link(o)
for area in bpy.context.screen.areas:
 if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA';area.spaces.active.shading.type='MATERIAL'
result={'scene':S.name,'decorative_assets':15,'camp_objects':len(S.objects),'render':S.render.filepath,'review_only':True}
print(json.dumps(result))
