import bpy,json
from pathlib import Path
C=bpy.app.driver_namespace['cyber'];scene=bpy.context.scene
for i,g in enumerate(C['guns']):
 g.rest();g.rig.location=((i-1)*1.25,0,0)
 for obj in g.meshes:obj.hide_render=False
 bpy.context.view_layer.update()
# Pack reference so the editable source can be moved without a missing image.
for image in bpy.data.images:
 if image.name.startswith('Cyberpunk Weapons'):image.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(C['OUT']/'Cyberpunk_Weapons_v3.blend'))
result={'saved':str(C['OUT']/'Cyberpunk_Weapons_v3.blend'),'active_scene':scene.name}
