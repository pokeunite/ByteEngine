"""Correct the visible material path: open chamber port -> continuous belt -> bulk bin."""
import bpy,math
from pathlib import Path
from mathutils import Vector,Matrix
BASE=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine');__file__=str(BASE/'Tools/MiningPrototype/build_machines.py')
exec(compile(Path(__file__).read_text().split('def fittings')[0],__file__,'exec'))
OUT=BASE/'artifacts/reference-refinery';scene=bpy.data.scenes['Refinery Reference'];bpy.context.window.scene=scene;scene.frame_set(1)
collection=bpy.data.collections['Reference Refinery'];bpy.context.view_layer.active_layer_collection=bpy.context.view_layer.layer_collection.children[collection.name]
rig=bpy.data.objects['RefineryRig'];mats={k:bpy.data.materials.get('Refinery '+k)for k in palette};parts=[]
# Remove the unconnected short roller platform; preserve the chamber and skip.
for o in list(collection.objects):
 if o.type=='MESH' and o.name.startswith(('Output side rail','Output roller','Roller end bearing','Output drive casing','Output motor cap','Reference conveyor ingot','OutputStart anchor','OutputEnd anchor')):bpy.data.objects.remove(o,do_unlink=True)
# A real hole through the right end plate, not a dark decal on a solid surface.
plate=next(o for o in collection.objects if o.name.startswith('Chamber end plate') and sum(v.co.x for v in o.data.vertices)/len(o.data.vertices)>0)
bpy.ops.mesh.primitive_cube_add(size=1,location=(1.18,-.35,1.58));cutter=bpy.context.object;cutter.scale=(.8,.86,.48);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
boolean=plate.modifiers.new('Actual ingot discharge aperture','BOOLEAN');boolean.operation='DIFFERENCE';boolean.solver='EXACT';boolean.object=cutter
bpy.context.view_layer.objects.active=plate;bpy.ops.object.modifier_apply(modifier=boolean.name);bpy.data.objects.remove(cutter,do_unlink=True)
plate.vertex_groups['Root'].add(list(range(len(plate.data.vertices))),1,'REPLACE')
# A short dark rectangular tunnel and yellow steel jambs make the opening legible.
for y in [-.81,.11]:box('Discharge port side jamb',(1.25,y,1.58),(.22,.09,.55),'yellow',bevel=.02)
for z in [1.30,1.86]:box('Discharge port lintel',(1.25,-.35,z),(.22,1.01,.09),'yellow',bevel=.02)
box('Discharge tunnel roof',(.94,-.35,1.80),(.64,.83,.055),'dark',bevel=.006)
for y in [-.76,.06]:box('Discharge tunnel inner wall',(.95,y,1.58),(.64,.04,.42),'dark',bevel=.005)
box('Discharge tunnel floor',(.96,-.35,1.34),(.63,.82,.055),'dark',bevel=.005)
# Belt top is continuous from inside the port to beyond the collection-bin rim.
rear=1.02;front=2.82;zc=1.38;radius=.12;length=front-rear;perimeter=2*length+math.tau*radius;spec={}
for y in [-.81,.11]:
 box('Discharge conveyor beam',((rear+front)/2,y,zc),(length+.14,.13,.22),'steel',bevel=.02)
 box('Discharge conveyor guide',((rear+front)/2,y,zc+.16),(length+.12,.06,.08),'yellow',bevel=.012)
 for x in [1.40,2.16,2.75]:box('Discharge guide clamp',(x,y,zc+.09),(.09,.16,.26),'dark',bevel=.018)
for x,key in [(rear,'BeltInletRoller'),(front,'BeltOutletRoller')]:
 cyl('Discharge end roller',(x,-.35,zc),radius,1.04,'steel',key,'Y',12);spec[key]=((x,-.35,zc),(x,-.05,zc))
 for y in [-.91,.21]:cyl('Discharge bearing',(x,y,zc),.145,.08,'ingot',axis='Y',vertices=8)
box('Conveyor drive gearbox',(2.25,-.99,1.32),(.42,.24,.35),'yellow',bevel=.04)
cyl('Conveyor drive motor',(2.25,-1.18,1.32),.14,.24,'dark',axis='Y',vertices=10)
# The outlet cantilever is braced back to the refinery frame, clear of ingot fall.
for y in [-.84,.14]:beam('Conveyor cantilever brace',(1.27,y,.76),(2.40,y,1.25),.055,'steel')
def path(s):
 s%=perimeter
 if s<length:return(rear+s,zc+radius,0)
 s-=length
 if s<math.pi*radius:
  a=s/radius;return(front+radius*math.sin(a),zc+radius*math.cos(a),a)
 s-=math.pi*radius
 if s<length:return(front-s,zc-radius,math.pi)
 a=(s-length)/radius;return(rear-radius*math.sin(a),zc-radius*math.cos(a),math.pi+a)
for i in range(26):
 x,z,a=path(i*perimeter/26);key='RefinerySlat%02d'%i
 o=box('Refinery belt slat %02d'%i,(x,-.35,z),(perimeter/26*.92,.82,.035),'black',key,.008)
 for v in o.data.vertices:v.co=Vector((x,-.35,z))+Matrix.Rotation(a,4,'Y')@(v.co-Vector((x,-.35,z)))
 spec[key]=((x,-.35,z),(x,-.35,z+.12))
for key,p in [('OutputStart',(1.28,-.35,1.57)),('OutputEnd',(2.84,-.35,1.57))]:
 spec[key]=(p,(p[0],p[1],p[2]+.12));box(key+' anchor bracket',(p[0],.05,1.23),(.065,.08,.04),'dark',key,.005)
bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
for key,(a,b) in spec.items():
 bone=rig.data.edit_bones.get(key) or rig.data.edit_bones.new(key);bone.head=a;bone.tail=b;bone.parent=rig.data.edit_bones['Root']
bpy.ops.object.mode_set(mode='OBJECT')
for o in parts:mod=o.modifiers.new('Rigid mechanical part','ARMATURE');mod.object=rig;o.parent=rig
for key in spec:
 p=rig.pose.bones[key];p.rotation_mode='XYZ';rest=Vector(spec[key][0])
 for f in range(1,92):
  t=(f-1)/90;p.location=(0,0,0);p.rotation_euler=(0,0,0)
  if key in ['BeltInletRoller','BeltOutletRoller']:p.rotation_euler[1]=math.tau*t*6
  if key.startswith('RefinerySlat'):
   i=int(key[-2:]);distance=i*perimeter/26+t*perimeter;x,z,a=path(distance);a+=math.floor(distance/perimeter)*math.tau;_,_,a0=path(i*perimeter/26)
   p.location=(x-rest.x,z-rest.z,0);p.rotation_euler[2]=-(a-a0) # local Z = -world Y
  p.keyframe_insert(data_path='location',frame=f);p.keyframe_insert(data_path='rotation_euler',frame=f)
for layer in rig.animation_data.action.layers:
 for strip in layer.strips:
  for bag in getattr(strip,'channelbags',[]):
   for curve in bag.fcurves:
    for pt in curve.keyframe_points:pt.interpolation='LINEAR'
scene.frame_set(1)
# Source-only ingots show the intended path and are omitted from the game GLB.
for x in [1.48,2.30]:
 o=box('Reference conveyor ingot',(x,-.35,1.60),(.36,.25,.15),'ingot',bevel=.035)
scene.camera.location=(7,-10,6.8);scene.camera.rotation_euler=(Vector((.45,-.65,1.6))-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.camera.data.ortho_scale=8.3
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Refinery.blend'))
bpy.ops.object.select_all(action='DESELECT');copies=[]
for o in list(collection.objects):
 if o.type!='MESH' or o.name.startswith('Reference '):continue
 c=o.copy();c.data=o.data.copy();collection.objects.link(c);c.select_set(True);copies.append(c)
bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join();body=bpy.context.object;body.name='RefineryRuntimeMesh';rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.gltf(filepath=str(OUT/'Refinery.glb'),export_format='GLB',use_selection=True,export_animations=True,export_force_sampling=True,export_animation_mode='ACTIONS')
bpy.data.objects.remove(body,do_unlink=True)
result={'correction':'Actual chamber opening and tunnel connected to 1.8m animated segmented conveyor over the bulk bin','bones':len(rig.data.bones)}
