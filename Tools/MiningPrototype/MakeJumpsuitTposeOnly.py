"""Standalone textured T-pose mesh. No gameplay, skeleton or animation edits."""
import bpy,math,pathlib,json,bmesh
from mathutils import Vector,Quaternion,Matrix
folder=pathlib.Path('C:/Users/codex/Documents/HazardPay Character Tpose');folder.mkdir(parents=True,exist_ok=True)
before=set(bpy.data.objects);bpy.ops.import_scene.gltf(filepath='C:/Users/codex/Downloads/jumpsuit_low_poly.glb');parts=[o for o in set(bpy.data.objects)-before if o.type=='MESH']
scale=178/(4.640323638916016-.02583656646311283)
def smooth(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
for o in parts:
 world=o.matrix_world.copy();o.parent=None
 name=o.name
 for v in o.data.vertices:
  p=world@v.co;p=Vector((p.x*scale,(p.z-.02583656646311283)*scale,-p.y*scale));x=abs(p.x);y=p.y;sign=1 if p.x>0 else -1
  if y>75 and y<155:
   if 'DETAILS' in name and y<112:w=0
   elif y<111:w=smooth(21,23.5,x)
   elif y<137:w=smooth(17.5,21,x)
   else:w=smooth(12,23,x)*(1-smooth(150,157,y))
   if w:
    pivot=Vector((sign*18.8,147.5,0));q=Vector((sign*.16,-1,0)).rotation_difference(Vector((sign,0,0)));q=Quaternion().slerp(q,w);p=pivot+q@(p-pivot)
  v.co=(p.x*.01,-p.z*.01,p.y*.01)
 o.matrix_world=Matrix.Identity(4)
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();body=bpy.context.object;body.name='Jumpsuit T Pose - original textures'
bm=bmesh.new();bm.from_mesh(body.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0001);bm.to_mesh(body.data);bm.free()
s=bpy.context.scene
for o in list(s.objects):
 if o!=body and o.type not in ['CAMERA','LIGHT']:bpy.data.objects.remove(o,do_unlink=True)
for name,position,power,size in [('Key',(3,-4,5),600,4),('Fill',(-3,-2,3),350,3),('Rim',(1,3,4),650,3)]:
 light=bpy.data.lights.new(name,'AREA');light.energy=power;light.shape='DISK';light.size=size;o=bpy.data.objects.new(name,light);s.collection.objects.link(o);o.location=position;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
if not s.camera:
 data=bpy.data.cameras.new('Tpose camera');s.camera=bpy.data.objects.new('Tpose camera',data);s.collection.objects.link(s.camera)
cam=s.camera;cam.location=(0,-5,1);cam.rotation_euler=(Vector((0,0,.9))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=2.1
s.render.resolution_x=1100;s.render.resolution_y=1100;s.render.resolution_percentage=100;s.render.filepath=str(folder/'Tpose-preview.png')
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);bpy.context.view_layer.objects.active=body
bpy.ops.wm.save_as_mainfile(filepath=str(folder/'Jumpsuit-Tpose.blend'))
bpy.ops.export_scene.gltf(filepath=str(folder/'Jumpsuit-Tpose.glb'),export_format='GLB',use_selection=True,export_animations=False)
bpy.ops.render.render(write_still=True)
(folder/'CREDITS.txt').write_text('JUMPSUIT Low Poly by DanlyVostok (@1799danly)\nhttps://sketchfab.com/3d-models/jumpsuit-low-poly-ddbd830d325b40ceb3829149de146d63\nCC BY 4.0: https://creativecommons.org/licenses/by/4.0/\nModified: repositioned arms into T-pose. Original textures retained.\n')
print(json.dumps({'blend':str(folder/'Jumpsuit-Tpose.blend'),'glb':str(folder/'Jumpsuit-Tpose.glb'),'vertices':len(body.data.vertices)}))
