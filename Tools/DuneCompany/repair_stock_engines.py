"""Repair stock belt geometry and expose visible animated pulley details in Blender."""
import bpy,bmesh,math
from mathutils import Vector
from pathlib import Path
ROOT=Path('C:/Users/codex/ByteEngine'); OUT=ROOT/'.artifacts/stock-engine-repair';OUT.mkdir(parents=True,exist_ok=True)
SOURCE=Path('C:/Users/codex/Downloads/trailmaker concept models/models/blend')
for part,name in [(23,'compact_engine'),(24,'heavy_engine')]:
    stem=f'{part:02}_{name}'
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE/(stem+'.blend')))
    fixed=bpy.data.objects['Fixed_Body_Geometry']
    if part==24:
        bm=bmesh.new();bm.from_mesh(fixed.data);left=set(bm.verts);removed=0
        while left:
            island=set();stack=[next(iter(left))]
            while stack:
                v=stack.pop()
                if v in island:continue
                island.add(v);stack.extend(e.other_vert(v) for e in v.link_edges if e.other_vert(v) not in island)
            left-=island;points=[fixed.matrix_world@v.co for v in island]
            if len(island)==56 and max(p.y for p in points)<-.49 and min(p.z for p in points)>.18:
                bmesh.ops.delete(bm,geom=list(island),context='VERTS');removed+=1
        assert removed==1,'Original polygonal belt was not found'
        bm.to_mesh(fixed.data);bm.free()
        rubber=bpy.data.materials.new('Engine belt rubber');rubber.diffuse_color=(.018,.016,.013,1);rubber.use_nodes=True
        rubber.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=rubber.diffuse_color
        rubber.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.8
        theta=math.asin((.15-.09)/.35)
        outline=[]
        for center,r,start,end,count in [(.69,.09,theta,math.pi-theta,40),(.34,.15,math.pi-theta,math.tau+theta,64)]:
            for i in range(count+1):
                a=start+(end-start)*i/count
                outline.append((r*math.cos(a),center+r*math.sin(a),a))
        verts=[]
        for x,z,a in outline:
            for y,dr in [(-.527,0),(-.497,0),(-.497,-.008),(-.527,-.008)]:verts.append((x+dr*math.cos(a),y,z+dr*math.sin(a)))
        faces=[];n=len(outline)
        for i in range(n):
            for k in range(4):faces.append((i*4+k,((i+1)%n)*4+k,((i+1)%n)*4+(k+1)%4,i*4+(k+1)%4))
        mesh=bpy.data.meshes.new('Continuous rubber belt mesh');mesh.from_pydata(verts,[],faces);mesh.update()
        belt=bpy.data.objects.new('Engine_Belt',mesh);bpy.context.collection.objects.link(belt);belt.data.materials.append(rubber);belt.parent=bpy.data.objects['Fixed_Body']
        for face in mesh.polygons:face.use_smooth=True
    orange=bpy.data.materials.new('Pulley timing orange');orange.diffuse_color=(.75,.19,.025,1);orange.use_nodes=True;orange.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=orange.diffuse_color
    pulley=bpy.data.objects['Moving_EnginePulley']
    for i in range(3):
        a=i*math.tau/3
        bpy.ops.mesh.primitive_cube_add(size=1)
        marker=bpy.context.object;marker.name='Pulley timing mark';marker.parent=pulley;marker.location=(.105*math.cos(a),-.055,.105*math.sin(a));marker.dimensions=(.026,.008,.015);marker.data.materials.append(orange)
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in bpy.context.scene.objects:
        if not o.name.startswith('STUDIO_'):o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(OUT/(stem+'.glb')),export_format='GLB',use_selection=True,export_animations=False,export_yup=True,export_materials='EXPORT')
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(stem+'.blend')))
    scene=bpy.context.scene;scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100;scene.render.film_transparent=True
    for o in scene.objects:
        if o.name.startswith('STUDIO_') and o.type=='MESH':o.hide_render=True
    if scene.render.engine=='CYCLES':scene.cycles.samples=24
    scene.render.filepath=str(OUT/(stem+'.png'));bpy.ops.render.render(write_still=True)
