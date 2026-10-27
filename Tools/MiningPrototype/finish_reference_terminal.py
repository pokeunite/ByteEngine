import bpy
from pathlib import Path
from mathutils import Vector
base=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine');scene=bpy.data.scenes['Company Terminal Reference'];bpy.context.window.scene=scene;scene.frame_set(1);collection=bpy.data.collections['Reference Company Terminal'];rig=bpy.data.objects['CompanyTerminalRig']
desk_prefix=('Industrial desktop','Yellow desktop','Splayed yellow desk leg','Desk anchor shoe','Desk diagonal brace','Desk rear crossbar','Main monitor','Upgrade graph','Upgrade node','Display header','Market display','Market chart','Market price','Market current','Keyboard','Trackball','Desktop radio','Radio','Desk auxiliary','Computer','Tower cyan','Main display cable','Market display cable','Station power cable','Interaction marker plate')
for o in collection.objects:
 if o.type!='MESH':continue
 centre=sum((v.co for v in o.data.vertices),Vector())/len(o.data.vertices)
 desk_bolt=o.name.startswith('Terminal hex fastener') and abs(centre.x)<1.9 and centre.z<2.6
 if o.name.startswith(desk_prefix) or desk_bolt:
  for v in o.data.vertices:v.co.x=v.co.x*.90-.20
 if o.name.startswith(('Supply crate','Crate')):
  for v in o.data.vertices:v.co.x+=.27;v.co.y-=.68
bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
for name in ['Antenna','Ticker']:
 bone=rig.data.edit_bones[name];bone.head.x=bone.head.x*.9-.2;bone.tail.x=bone.tail.x*.9-.2
bpy.ops.object.mode_set(mode='OBJECT')
scene.camera.location=(-3.6,-12,3.20);scene.camera.rotation_euler=(Vector((0,0,1.64))-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.camera.data.ortho_scale=7.2;scene.cycles.samples=20
exec(compile((base/'Tools/MiningPrototype/export_reference_terminal.py').read_text(),'export_reference_terminal.py','exec'))
result={'correction':'whole display visible below door roll, desk left of entry, supply crate in clear right nook','desk_right_edge':1.25,'crate_left_edge':1.40}
