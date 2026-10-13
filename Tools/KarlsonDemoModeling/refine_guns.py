import bpy, math, shutil
from pathlib import Path
from mathutils import Vector
out=Path(r'C:/Users/codex/Downloads/byte engine files');src=out/'Karlson_Guns.blend'
backup=out/'Karlson_Guns_initial.blend'
if not backup.exists():shutil.copy2(src,backup)
bpy.ops.wm.open_mainfile(filepath=str(src))
scene=bpy.context.scene
for name,color in [('Body',(.055,.063,.075,1)),('Slide',(.105,.12,.14,1)),('Steel',(.16,.18,.21,1))]:
    mat=bpy.data.materials[name];mat.diffuse_color=color;mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=color
for name,energy in [('Key',170),('Fill',85),('Rim',130)]:bpy.data.objects[name].data.energy=energy
camera=scene.camera
def reset(rig):
    rig.animation_data.action=None
    for track in rig.animation_data.nla_tracks:track.mute=True
    for bone in rig.pose.bones:bone.location=(0,0,0);bone.rotation_quaternion=(1,0,0,0);bone.scale=(1,1,1)
    scene.frame_set(1);bpy.context.view_layer.update()
def parts(name):return list(bpy.data.collections[name].objects)
def render(name,view,clip=None,frame=1):
    rig=bpy.data.objects[name+'_Rig'];home=rig.location.copy();rig.location=(0,0,0);reset(rig)
    for other in ['Pistol','Shotgun','Rifle']:
        for obj in parts(other):obj.hide_render=other!=name
    if clip:rig.animation_data.action=bpy.data.actions[name+'_'+clip];scene.frame_set(frame);bpy.context.view_layer.update()
    deps=bpy.context.evaluated_depsgraph_get();verts=[]
    for obj in parts(name):
        if obj.type=='MESH':
            evaluated=obj.evaluated_get(deps);verts.extend([evaluated.matrix_world@Vector(v) for v in evaluated.bound_box])
    low=Vector(tuple(min(v[i] for v in verts) for i in range(3)));high=Vector(tuple(max(v[i] for v in verts) for i in range(3)));target=(low+high)/2
    direction={'three-quarter':Vector((1.4,1.3,.85)),'side':Vector((1.8,0,.05)),'top':Vector((.001,0,2)),'front':Vector((0,2,.05))}[view]
    camera.location=target+direction;camera.rotation_euler=(-direction).to_track_quat('-Z','Y').to_euler()
    inverse=camera.matrix_world.inverted();bpy.context.view_layer.update();inverse=camera.matrix_world.inverted();projected=[inverse@v for v in verts]
    width=max(v.x for v in projected)-min(v.x for v in projected);height=max(v.y for v in projected)-min(v.y for v in projected)
    camera.data.ortho_scale=max(width,height*scene.render.resolution_x/scene.render.resolution_y)*1.22
    scene.render.filepath=str(out/'Karlson Gun Previews'/(name+'_'+(clip+'_'+str(frame) if clip else view)+'.png'));bpy.ops.render.render(write_still=True)
    reset(rig);rig.location=home
for name in ['Pistol','Shotgun','Rifle']:
    rig=bpy.data.objects[name+'_Rig'];home=rig.location.copy();rig.location=(0,0,0);reset(rig)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts(name):obj.select_set(True)
    bpy.context.view_layer.objects.active=rig
    for track in rig.animation_data.nla_tracks:track.mute=False
    bpy.ops.export_scene.gltf(filepath=str(out/('Karlson_'+name+'.glb')),export_format='GLB',use_selection=True,export_animation_mode='NLA_TRACKS',export_force_sampling=True,export_anim_slide_to_zero=True,export_apply=False)
    reset(rig);rig.location=home
    for view in ['three-quarter','top','front','side']:render(name,view)
    for clip,frame in [('Fire',3),('Reload',40),('Reload',72)]:render(name,'three-quarter',clip,frame)
for name in ['Pistol','Shotgun','Rifle']:
    for obj in parts(name):obj.hide_render=False
camera.location=(2.7,2.5,1.8);camera.rotation_euler=(Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=2.3
scene.render.filepath=str(out/'Karlson Gun Previews'/'All_guns.png');bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(src))
print('Refined source, exports and inspection views saved.')
