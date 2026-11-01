import bpy,bmesh,math,pathlib,numpy as np
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
s=bpy.context.scene;body=bpy.data.objects['Reconstructed jumpsuit - connected T pose'];helmet=bpy.data.objects['Reconstructed teal helmet shell'];collection=body.users_collection[0];out=pathlib.Path('C:/Users/codex/Documents/Jumpsuit Reconstruction')
def mat(name,color,rough=.65,metal=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal;return m
fabric=mat('Reconstruction / woven charcoal textile',(.035,.042,.046),.78,.03)
size=256;y,x=np.mgrid[0:size,0:size];rng=np.random.default_rng(21)
weave=.20+.025*np.sin(x*math.tau/8)+.025*np.sin(y*math.tau/8)+.018*np.where((x//8+y//8)%2==0,np.sin(x*math.tau/8),np.sin(y*math.tau/8))+.007*rng.normal(size=(size,size));rgba=np.ones((size,size,4),dtype=np.float32)
for c,scale in enumerate([.92,.97,1]):rgba[:,:,c]=np.clip(weave*scale,0,1)
image=bpy.data.images.new('Authored woven textile base',width=size,height=size);image.pixels.foreach_set(rgba.ravel());image.filepath_raw=str(out/'Rebuilt-fabric-base.png');image.file_format='PNG';image.save();image.pack()
n=fabric.node_tree.nodes.new('ShaderNodeTexImage');n.image=image;n.extension='REPEAT';fabric.node_tree.links.new(n.outputs['Color'],fabric.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
height=(weave-weave.min())/(weave.max()-weave.min());dx=(np.roll(height,-1,1)-np.roll(height,1,1))*.22;dy=(np.roll(height,-1,0)-np.roll(height,1,0))*.22;normal=np.ones((size,size,4),dtype=np.float32);normal[:,:,0]=.5-dx;normal[:,:,1]=.5-dy;normal[:,:,2]=1
im=bpy.data.images.new('Authored weave normal',width=size,height=size,is_data=True);im.pixels.foreach_set(normal.ravel());im.filepath_raw=str(out/'Rebuilt-fabric-normal.png');im.file_format='PNG';im.save();im.pack();tex=fabric.node_tree.nodes.new('ShaderNodeTexImage');tex.image=im;tex.extension='REPEAT';nm=fabric.node_tree.nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.32;fabric.node_tree.links.new(tex.outputs['Color'],nm.inputs['Color']);fabric.node_tree.links.new(nm.outputs['Normal'],fabric.node_tree.nodes.get('Principled BSDF').inputs['Normal'])
boots=mat('Reconstruction / black boot leather',(.021,.026,.029),.61)
body.data.materials[0]=fabric;body.data.materials[1]=boots
# Broaden the waist and soften the exaggerated V silhouette.
for v in body.data.vertices:
 p=v.co
 if .9<p.z<1.25 and abs(p.x)<.20:p.x*=1+.09*math.sin(math.pi*(p.z-.9)/.35)
body.data.update();uv=body.data.uv_layers.active
for p in body.data.polygons:
 axis=max(range(3),key=lambda i:abs(p.normal[i]));axes=(0,2) if axis==1 else (1,2) if axis==0 else (0,1)
 for li in p.loop_indices:
  co=body.data.vertices[body.data.loops[li].vertex_index].co;uv.data[li].uv=(co[axes[0]]*20,co[axes[1]]*20)
teal=mat('Reconstruction / worn teal helmet',(.032,.15,.16),.34,.32);helmet.data.materials[0]=teal
# Small broad finish variation rather than a noisy copied UV patchwork.
nodes=teal.node_tree.nodes;noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=18;noise.inputs['Detail'].default_value=2;ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].color=(.025,.11,.12,1);ramp.color_ramp.elements[1].color=(.07,.23,.23,1);teal.node_tree.links.new(noise.outputs['Fac'],ramp.inputs['Fac']);teal.node_tree.links.new(ramp.outputs['Color'],nodes.get('Principled BSDF').inputs['Base Color'])
gold=mat('Reconstruction / ochre trim',(.58,.38,.07),.72);white=mat('Reconstruction / off-white print',(.67,.70,.66),.75);red=mat('Reconstruction / red insignia',(.48,.035,.025),.65);dark=mat('Reconstruction / black binding',(.012,.017,.019),.8)
body.data.calc_loop_triangles();tree=BVHTree.FromPolygons([v.co for v in body.data.vertices],[tuple(t.vertices)for t in body.data.loop_triangles],all_triangles=True)
def mesh(name,verts,faces,material):
 d=bpy.data.meshes.new(name+' mesh');d.from_pydata(verts,[],faces);d.materials.append(material);d.update();o=bpy.data.objects.new(name,d);collection.objects.link(o);return o
def surface(x,z,back=False):
 start=Vector((x,.5 if back else -.5,z));direction=Vector((0,-1 if back else 1,0));p,n,i,d=tree.ray_cast(start,direction)
 if p is None:return Vector((x,.1 if back else -.1,z))
 return p+n*.0013
def ribbon(name,path,width,material,back=False):
 verts=[]
 for x,z in path:verts.extend([surface(x-width/2,z,back),surface(x+width/2,z,back)])
 return mesh(name,verts,[(i*2,i*2+1,i*2+3,i*2+2)for i in range(len(path)-1)],material)
for sign in [-1,1]:
 path=[(sign*x,z)for x,z in [(.092,1.515),(.097,1.48),(.103,1.44),(.106,1.40),(.107,1.36),(.108,1.32),(.105,1.28),(.10,1.24),(.092,1.20),(.084,1.16),(.081,1.12),(.080,1.08),(.079,1.04),(.079,1.0),(.079,.96),(.082,.92),(.084,.88),(.09,.84),(.106,.80),(.129,.76),(.145,.72),(.16,.68),(.174,.64),(.181,.60),(.176,.57)]]
 ribbon('Continuous yellow front trim',path,.014,gold)
 ribbon('Rear calf trim',[(sign*x,z)for x,z in [(.15,.43),(.158,.40),(.16,.37),(.15,.34),(.136,.31),(.124,.28),(.12,.24),(.12,.20)]],.013,gold,True)
 ribbon('Rear waist stripe',[(sign*x,z)for x,z in [(.045,1.24),(.045,1.20),(.05,1.16),(.08,1.12),(.12,1.08),(.15,1.06)]],.014,gold,True)
 ribbon('Rear seat trim',[(sign*x,z)for x,z in [(.02,.91),(.07,.91),(.11,.89),(.13,.85),(.15,.81),(.17,.77),(.17,.74)]],.013,gold,True)
 # Shoulder-to-upper-arm yellow stripe follows the top surface.
 verts=[]
 for k in range(15):
  x=sign*(.23+k*.0135)
  for y in [-.022,-.008]:
   p,n,i,d=tree.ray_cast(Vector((x,y,1.75)),Vector((0,0,-1)));verts.append(p+n*.0014 if p else Vector((x,y,1.5)))
 mesh('Yellow upper sleeve stripe',verts,[(2*i,2*i+1,2*i+3,2*i+2)for i in range(14)],gold)
 # A narrow circumferential band sits below the upper sleeve stripe.
 verts=[]
 for x in [.405,.425]:
  for j in range(32):
   a=math.tau*j/32;direction=Vector((0,math.cos(a),math.sin(a)));p,n,i,d=tree.ray_cast(Vector((sign*x,0,1.4275))+direction*.20,-direction);verts.append(p+n*.0014 if p else Vector((sign*x,.067*math.cos(a),1.4275+.064*math.sin(a))))
 mesh('Yellow upper-arm band',verts,[(j,(j+1)%32,(j+1)%32+32,j+32)for j in range(32)],gold)
# Printed labels conform to the actual new torso surface.
def label(name,text,location,size,vertical=False,back=False):
 data=bpy.data.curves.new(name,'FONT');data.body=text;data.size=size;data.extrude=0;data.align_x='LEFT';data.materials.append(white);o=bpy.data.objects.new(name,data);collection.objects.link(o);o.location=location
 o.rotation_euler=(Matrix(((0,-1,0),(0,0,-1),(1,0,0))) if vertical else Matrix(((1,0,0),(0,0,-1),(0,1,0)))).to_euler()
 if back:o.rotation_euler=(math.pi/2,0,math.pi)
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH');o=bpy.context.object
 mod=o.modifiers.new('Print follows cloth','SHRINKWRAP');mod.target=body;mod.wrap_method='NEAREST_SURFACEPOINT';mod.offset=.0018;bpy.ops.object.modifier_apply(modifier=mod.name);o.select_set(False);return o
label('Vertical security chest print','SECURITY',(.022,-.17,1.16),.035,True)
label('Small chest designation','SFRSMT',(.046,-.16,1.385),.009)
label('Back designation','SFRSMT',(.040,.16,1.285),.014,False,True)
def chevron(name,z,back=False):
 points=[(-.027,z+.014),(0,z-.010),(.027,z+.014),(.018,z+.020),(0,z+.003),(-.018,z+.020)];return mesh(name,[surface(x,h,back)for x,h in points],[(0,1,4,5),(1,2,3,4)],red)
chevron('Red chest chevron',1.455);chevron('Red back chevron',1.375,True)
# Smooth the helmet silhouette while keeping its newly authored base editable.
sub=helmet.modifiers.new('Helmet finish subdivision','SUBSURF');sub.levels=1;sub.render_levels=1
# New pale rim geometry follows the lower helmet curve.
verts=[]
for band in [0,1]:
 for j in range(48):
  a=math.tau*j/48;z=1.58+.010*math.cos(a)+band*.004;verts.append((.073*math.cos(a),.081*math.sin(a),z))
mesh('Pale helmet lower perimeter',verts,[(j,(j+1)%48,(j+1)%48+48,j+48)for j in range(48)],white)
# Round the protective knee silhouette rather than leaving cuboid blocks.
for o in list(collection.objects):
 if o.name.startswith('Rounded knee guard'):
  o.data.materials[0]=white
  mod=o.modifiers.new('Knee pad finish','SUBSURF');mod.levels=1;mod.render_levels=1
for o in collection.objects:
 if o.type=='MESH' and ('long thigh side panel' in o.name):
  for v in o.data.vertices:
   if v.co.z<0:v.co.x*=.80;v.co.y*=.90
  o.data.update()
s.camera.location=(0,-5,1);s.camera.rotation_euler=(Vector((0,0,.9))-s.camera.location).to_track_quat('-Z','Y').to_euler();s.render.filepath=str(out/'Reconstruction-front-refined.png');bpy.ops.wm.save_as_mainfile(filepath=str(out/'Jumpsuit-Reconstructed.blend'));bpy.ops.render.render(write_still=True)
print('Rebuilt woven materials, continuous trim and fitted labels; geometry remains new.')
