import bpy,json,math
from pathlib import Path
from mathutils import Vector
out=Path(r'C:/Users/codex/Downloads/byte engine files');bpy.ops.wm.open_mainfile(filepath=str(out/'Karlson_Guns.blend'))
scene=bpy.context.scene;camera=scene.camera;rows=[]
for name in ['Pistol','Shotgun','Rifle']:
    rig=bpy.data.objects[name+'_Rig'];home=rig.location.copy();rig.location=(0,0,0);rig.animation_data.action=None
    for track in rig.animation_data.nla_tracks:track.mute=True
    for bone in rig.pose.bones:bone.location=(0,0,0);bone.rotation_quaternion=(1,0,0,0)
    scene.frame_set(1);bpy.context.view_layer.update();vertices=[];tris=0
    for obj in bpy.data.collections[name].objects:
        if obj.type=='MESH':
            vertices.extend([obj.matrix_world@v.co for v in obj.data.vertices]);obj.data.calc_loop_triangles();tris+=len(obj.data.loop_triangles)
            assert all(len(v.groups)==1 and abs(v.groups[0].weight-1)<1e-6 for v in obj.data.vertices)
    low=Vector(tuple(min(v[i] for v in vertices) for i in range(3)));high=Vector(tuple(max(v[i] for v in vertices) for i in range(3)))
    rows.append({'gun':name,'artistic_dimensions_m':list(high-low),'triangles':tris,'render_assemblies':sum(o.type=='MESH' for o in bpy.data.collections[name].objects),'bones':list(rig.data.bones.keys()),'clips':['Idle','Fire','Reload','Equip'],'reload_scope':'Weapon-only animation; no character hands or shell-loading interaction.'})
    for other in ['Pistol','Shotgun','Rifle']:
        for obj in bpy.data.collections[other].objects:obj.hide_render=other!=name
    target=(low+high)/2;direction=Vector((1.4,1.3,.85));camera.location=target+direction;camera.rotation_euler=(-direction).to_track_quat('-Z','Y').to_euler();bpy.context.view_layer.update();projected=[camera.matrix_world.inverted()@v for v in vertices]
    width=max(v.x for v in projected)-min(v.x for v in projected);height=max(v.y for v in projected)-min(v.y for v in projected);camera.data.ortho_scale=max(width,height*scene.render.resolution_x/scene.render.resolution_y)*1.22
    scene.render.filepath=str(out/'Karlson Gun Previews'/(name+'_three-quarter.png'));bpy.ops.render.render(write_still=True);rig.location=home
for name in ['Pistol','Shotgun','Rifle']:
    for obj in bpy.data.collections[name].objects:obj.hide_render=False
camera.location=(2.7,2.5,1.8);camera.rotation_euler=(Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=2.3
scene.render.filepath=str(out/'Karlson Gun Previews'/'All_guns.png');bpy.ops.render.render(write_still=True)
(out/'Karlson_Guns_validation.json').write_text(json.dumps(rows,indent=2))
print(json.dumps(rows))
