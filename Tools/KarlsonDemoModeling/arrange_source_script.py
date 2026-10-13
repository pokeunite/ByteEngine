from pathlib import Path
p=Path('Tools/KarlsonDemoModeling/final_exports_v2.py');s=p.read_text().replace("for rig in rigs:\n rig.animation_data.action=None","for rig in rigs:\n rig.location=(0,0,0)\n rig.animation_data.action=None")
a=s.index('bpy.ops.wm.save_as_mainfile')
s=s[:a]+'''# Arrange the editable source for a useful first opening, after exporting at origin.
from mathutils import Vector
for i,rig in enumerate(rigs):
 rig.location.x=(i-1)*.65
 for child in rig.children:child.hide_render=False
 if rig.name.startswith('Shotgun'):rig.pose.bones['Shell'].scale=(.001,.001,.001)
scene.frame_set(1);camera=scene.camera;camera.data.type='ORTHO';camera.data.ortho_scale=2.2
camera.location=(2.4,2.5,1.6);camera.rotation_euler=(Vector((0,.05,-.02))-camera.location).to_track_quat('-Z','Y').to_euler()
''' + s[a:];p.write_text(s)
