import bpy,math
from mathutils import Vector
C=bpy.app.driver_namespace['cyber'];scene=bpy.context.scene
studio=bpy.data.collections.new('Cyberpunk Review Studio');scene.collection.children.link(studio)
camera=bpy.data.objects.new('Cyberpunk review camera',bpy.data.cameras.new('Cyberpunk review camera'));studio.objects.link(camera);scene.camera=camera;camera.data.type='ORTHO'
for name,position,power,size in [('Key',(1.2,1.1,1.8),100,2),('Fill',(-1.3,.6,1.2),70,2),('Rim',(0,-1.8,1.4),120,1.7)]:
 data=bpy.data.lights.new('Cyber '+name,'AREA');data.energy=power;data.size=size;obj=bpy.data.objects.new('Cyber '+name,data);studio.objects.link(obj);obj.location=position;obj.rotation_euler=(-obj.location).to_track_quat('-Z','Y').to_euler()
image=bpy.data.images.load(r'C:/Users/codex/Documents/karlson demo/Assets/Concepts/Cyberpunk Weapons v1.png',check_existing=True)
ref=bpy.data.objects.new('Cyberpunk primary concept reference',None);studio.objects.link(ref);ref.empty_display_type='IMAGE';ref.data=image;ref.hide_render=True;ref.location=(.8,.1,.2);ref.empty_display_size=1.2
bpy.ops.wm.save_as_mainfile(filepath=str(C['OUT']/'Cyberpunk_Weapons_v3.blend'));result='Studio, reference and source saved'
