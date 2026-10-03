"""Visual correction pass on the saved editable source; no regeneration of geometry."""
import bpy,sys,math,json,struct,ast
import numpy as np
from pathlib import Path
from mathutils import Vector,Quaternion
OUT=Path(sys.argv[sys.argv.index('--')+1]); source=OUT/'GoblinScraper_RefinedParts.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
SCENE=bpy.context.scene
catalog=json.loads((OUT/'parts-catalog.json').read_text())
# Reuse the generator's texture function, with subtler rust instead of a camouflage-like mask.
script=Path(__file__).with_name('build_refined.py').read_text(encoding='utf-8-sig')
tree=ast.parse(script)
fn=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='texture')
code=ast.get_source_segment(script,fn).replace('rust>.63','rust>.70').replace('(rust>.59)&(rust<.625)','(rust>.67)&(rust<.695)')
code=code.replace('[.28,.115,.052]','[.19,.075,.030]')
exec(code,globals())
colors={'green':((.235,.285,.095),'paint'),'red':((.39,.085,.04),'paint'),
        'iron':((.15,.17,.18),'metal'),'steel':((.34,.36,.36),'steel'),
        'rust':((.28,.105,.042),'metal'),'brass':((.46,.32,.125),'metal'),
        'rubber':((.05,.057,.052),'rubber'),'wood':((.23,.115,.047),'wood'),'black':((.025,.03,.032),'metal')}
for key,(color,kind) in colors.items():
    material=bpy.data.materials.get('Goblin_'+key)
    if material is None:
        material=bpy.data.materials['Goblin_green'].copy(); material.name='Goblin_'+key
    material.use_fake_user=True
    albedo,rough=texture(key,color,kind)
    bs=material.node_tree.nodes.get('Principled BSDF')
    for link in list(material.node_tree.links):
        if link.to_node==bs and link.to_socket.name=='Base Color': link.from_node.image=albedo
        if link.to_node==bs and link.to_socket.name=='Roughness': link.from_node.image=rough
    pixels=np.empty(albedo.size[0]*albedo.size[1]*4,dtype=np.float32); albedo.pixels.foreach_get(pixels)
    a=pixels.reshape(albedo.size[1],albedo.size[0],4)
    h=a[:,:,:3].mean(axis=2)
    dx=(np.roll(h,-1,axis=1)-np.roll(h,1,axis=1))*1.8
    dy=(np.roll(h,-1,axis=0)-np.roll(h,1,axis=0))*1.8
    norm=np.stack((-dx,-dy,np.ones_like(dx)),axis=-1); norm/=np.sqrt((norm*norm).sum(axis=-1))[:,:,None]
    n=np.ones_like(a); n[:,:,:3]=norm*.5+.5
    img=bpy.data.images.new(key+'_normal',albedo.size[0],albedo.size[1],alpha=True)
    img.colorspace_settings.name='Non-Color'; img.pixels.foreach_set(n.ravel())
    img.filepath_raw=str(OUT/'textures'/(key+'_normal.png')); img.file_format='PNG'; img.save(); img.pack()
    tex=material.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=img
    normal=material.node_tree.nodes.new('ShaderNodeNormalMap'); normal.inputs['Strength'].default_value=.5
    material.node_tree.links.new(tex.outputs['Color'],normal.inputs['Color']); material.node_tree.links.new(normal.outputs['Normal'],bs.inputs['Normal'])

# Find connected primitive surfaces. Rubber and timber bevels must keep their own materials.
corrected=0
for part in catalog['parts']:
    col=bpy.data.collections[part['label']]
    for obj in col.objects:
        if obj.type!='MESH': continue
        data=obj.data; n=len(data.vertices); parent=list(range(n))
        def root(i):
            while parent[i]!=i: parent[i]=parent[parent[i]]; i=parent[i]
            return i
        def union(a,b):
            ra=root(a); rb=root(b)
            if ra!=rb: parent[rb]=ra
        for edge in data.edges: union(edge.vertices[0],edge.vertices[1])
        categories={}
        for poly in data.polygons:
            mat=obj.material_slots[poly.material_index].material
            if mat and mat.name in ('Goblin_rubber','Goblin_wood'):
                categories[root(poly.vertices[0])]=poly.material_index
        for poly in data.polygons:
            mat=obj.material_slots[poly.material_index].material
            target=categories.get(root(poly.vertices[0]))
            if mat and mat.name=='Goblin_steel' and target is not None:
                poly.material_index=target; corrected+=1
print('NON_METAL_BEVEL_FACES_CORRECTED',corrected,flush=True)

# Match the reference's vertical upper/lower jaw silhouette. Rear mounting axis remains unchanged.
grip=bpy.data.objects['scrap_grabber_jaws']; grip.rotation_mode='QUATERNION'; grip.rotation_quaternion=Quaternion((0,1,0),math.pi/2)

studio=bpy.data.collections['_Preview Studio']
for obj in studio.objects:
    if obj.type=='FONT': obj.hide_render=True
camera=bpy.data.objects['Part Preview Camera']; ground=bpy.data.objects['Studio Ground']
SCENE.camera=camera; SCENE.render.resolution_x=512; SCENE.render.resolution_y=512

def readglb(path):
    b=path.read_bytes(); length,kind=struct.unpack_from('<II',b,12)
    return json.loads(b[20:20+length].decode())

def pose_reset(rig):
    rig.animation_data.action=None
    for track in rig.animation_data.nla_tracks: track.mute=True
    for pb in rig.pose.bones: pb.location=(0,0,0); pb.rotation_quaternion=Quaternion(); pb.scale=(1,1,1)
    SCENE.frame_set(1); bpy.context.view_layer.update()

def export(col,rig,file,red=False):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in col.objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=rig
    swaps=[]
    if red:
        for obj in col.objects:
            if obj.type=='MESH':
                for slot in obj.material_slots:
                    if slot.material==bpy.data.materials['Goblin_green']: swaps.append(slot); slot.material=bpy.data.materials['Goblin_red']
    root=bpy.data.objects[file]; root['faction']='red' if red else 'green'
    for track in rig.animation_data.nla_tracks: track.mute=False
    path=OUT/('red_faction' if red else '')/(file+'.glb')
    bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,
        export_yup=True,export_skins=True,export_animations=True,export_animation_mode='NLA_TRACKS',
        export_force_sampling=True,export_frame_range=False,export_extra_animations=False,
        export_extras=True,export_cameras=False,export_lights=False)
    for slot in swaps: slot.material=bpy.data.materials['Goblin_green']
    root['faction']='green'; pose_reset(rig)
    return readglb(path)

parts=[]
for part in catalog['parts']:
    file=Path(part['file']).stem; root=bpy.data.objects[file]; col=bpy.data.collections[part['label']]
    rig=next(o for o in col.objects if o.type=='ARMATURE'); meshes=[o for o in col.objects if o.type=='MESH']
    saved=root.location.copy(); root.location=(0,0,0); pose_reset(rig)
    for p in catalog['parts']: bpy.data.collections[p['label']].hide_render=p is not part
    doc=export(col,rig,file); export(col,rig,file,True)
    if len(doc.get('animations',[]))!=len(part['animations']): raise RuntimeError('Clip count changed '+file)
    coords=[o.matrix_world@Vector(v) for o in meshes for v in o.bound_box]
    low=Vector([min(v[i] for v in coords) for i in range(3)]); high=Vector([max(v[i] for v in coords) for i in range(3)])
    part['bounds_blender_z_up']=[list(low),list(high)]
    part['normal_maps']=True
    size=max(high-low); center=(low+high)*.5
    camera.location=center+Vector((1.4,1.8,1.25))*max(size,1)
    camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler(); camera.data.ortho_scale=max(size*1.62,.85)
    ground.location.z=low.z-.012
    SCENE.render.filepath=str(OUT/'previews'/(file+'.png')); bpy.ops.render.render(write_still=True)
    # Refresh rendered motion for the four review mechanisms after the material corrections.
    if file in ('scrap_crushing_drum','scrap_auger_drill','scrap_grabber_jaws','scrap_track_pod'):
        action=bpy.data.actions[part['animations'][0]['name']]; rig.animation_data.action=action
        end=int(round(part['animations'][0]['duration_seconds']*30))+1
        SCENE.render.resolution_x=256; SCENE.render.resolution_y=256
        for i,f in enumerate(np.linspace(1,end,9)[:-1]):
            SCENE.frame_set(int(f)); SCENE.render.filepath=str(OUT/'previews'/'motion_frames'/f'{file}_{i:02d}.png')
            bpy.ops.render.render(write_still=True)
        SCENE.render.resolution_x=512; SCENE.render.resolution_y=512; pose_reset(rig)
    root.location=saved
    print('REFINED_REVIEW',file,flush=True)

for part in catalog['parts']: bpy.data.collections[part['label']].hide_render=False
for obj in studio.objects:
    if obj.type=='FONT': obj.hide_render=False
ground.location.z=-.08; camera.location=(30,17,34); target=Vector((9,-10,1))
camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler(); camera.data.ortho_scale=39
SCENE.render.resolution_x=1920; SCENE.render.resolution_y=1440
SCENE.render.filepath=str(OUT/'previews'/'whole-library.png'); bpy.ops.render.render(write_still=True)
catalog['materials']='Embedded base colour, roughness/metallic and tangent normal textures; connected rubber/timber bevels retain nonmetal surfaces.'
(OUT/'parts-catalog.json').write_text(json.dumps(catalog,indent=2),encoding='utf-8')
# Remove superseded unreferenced texture data; packed current textures remain in the source.
for image in list(bpy.data.images):
    if image.users==0 and image.name not in ('Render Result','Viewer Node'): bpy.data.images.remove(image)
bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True)
print('VISUAL_CORRECTION_COMPLETE',flush=True)


