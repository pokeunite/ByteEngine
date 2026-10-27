import bpy,math
from pathlib import Path
from mathutils import Vector
base=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine');out=base/'artifacts/reference-terminal';scene=bpy.data.scenes['Company Terminal Reference'];bpy.context.window.scene=scene;scene.frame_set(1);collection=bpy.data.collections['Reference Company Terminal'];rig=bpy.data.objects['CompanyTerminalRig']
for key in ['canvas','lining']:
 m=bpy.data.materials['Terminal '+key];image=bpy.data.images.load(str(out/(key+'.png')),check_existing=True);image.pack();node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=image;node.extension='REPEAT';m.node_tree.links.new(node.outputs['Color'],m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
for o in collection.objects:
 if o.type!='MESH':continue
 if any(m and m.name in ['Terminal canvas','Terminal lining']for m in o.data.materials) and not o.data.uv_layers:
  uv=o.data.uv_layers.new(name='Canvas weave UV')
  for poly in o.data.polygons:
   axis=max(range(3),key=lambda i:abs(poly.normal[i]))
   for li in poly.loop_indices:
    p=o.data.vertices[o.data.loops[li].vertex_index].co
    uv.data[li].uv=((p.y,p.z)if axis==0 else (p.x,p.z)if axis==1 else(p.x,p.y))
 if o.name.startswith(('Worklight backing','Warm interior worklight','Worklight end cap')):
  for v in o.data.vertices:v.co.y-=.40;v.co.z-=.17
# Visible rolled fabric layers at the door ends rather than flat pipe caps.
for x in [-2.315,2.315]:
 for radius in [.145,.105,.062]:
  verts=[];n=16
  for r in [radius,radius-.014]:
   for i in range(n):a=i*math.tau/n;verts.append((x,-1.96+math.cos(a)*r,2.95+math.sin(a)*r))
  faces=[(i,(i+1)%n,(i+1)%n+n,i+n)for i in range(n)]
  data=bpy.data.meshes.new('Rolled canvas layers');data.from_pydata(verts,[],faces);data.materials.append(bpy.data.materials['Terminal black']);o=bpy.data.objects.new('Canvas roll end binding',data);collection.objects.link(o);o.vertex_groups.new(name='Flap').add(list(range(len(data.vertices))),1,'REPLACE');mod=o.modifiers.new('Rigid assembly animation','ARMATURE');mod.object=rig;o.parent=rig
scene.camera.location=(-4.1,-11.7,4.8);scene.camera.rotation_euler=(Vector((0,0,1.64))-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.camera.data.ortho_scale=7.3
scene.render.resolution_x=1150;scene.render.resolution_y=900;scene.cycles.samples=24;scene.cycles.use_denoising=True
bpy.ops.wm.save_as_mainfile(filepath=str(out/'CompanyTerminal.blend'))
result={'corrections':'full monitor visibility, packed woven canvas, visible warm light, layered rolled door ends'}
