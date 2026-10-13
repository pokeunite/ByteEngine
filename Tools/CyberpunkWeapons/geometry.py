import bpy, math, json
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion
OUT=Path(r'C:/Users/codex/Downloads/byte engine files/Cyberpunk Weapons v3');OUT.mkdir(exist_ok=True)
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.render.fps=60
scene.render.engine='BLENDER_EEVEE_NEXT';scene.render.resolution_x=1280;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.world=bpy.data.worlds.new('Cyberpunk studio');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.22,.25,.29,1);scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.4
MATS={}
for name,col,rough,metal,emission in [('Graphite',(.024,.031,.038,1),.46,.45,0),('Armor',(.56,.56,.52,1),.52,.32,0),('Steel',(.18,.21,.23,1),.32,.7,0),('Rubber',(.01,.014,.019,1),.9,0,0),('Recess',(.003,.005,.008,1),1,0,0),('Glove',(.019,.027,.036,1),.85,0,0),('Sleeve',(.045,.055,.068,1),.95,0,0),('Seam',(.012,.017,.023,1),.95,0,0),('Orange',(.95,.20,.015,1),.4,.2,2),('Cyan',(.01,.55,.70,1),.35,.2,2),('Brass',(.46,.30,.09,1),.35,.7,0),('Shell',(.27,.065,.027,1),.55,.1,0)]:
 m=bpy.data.materials.new('Cyber '+name);m.use_nodes=True;m.diffuse_color=col;bs=m.node_tree.nodes['Principled BSDF'];bs.inputs['Base Color'].default_value=col;bs.inputs['Roughness'].default_value=rough;bs.inputs['Metallic'].default_value=metal
 if emission:bs.inputs['Emission Color'].default_value=col;bs.inputs['Emission Strength'].default_value=emission
 MATS[name]=m
class Gun:
 def __init__(self,name,anchor,pixel_scale):
  self.name=name;self.anchor=anchor;self.pixel_scale=pixel_scale;self.collection=bpy.data.collections.new('Cyber '+name);scene.collection.children.link(self.collection);self.parts=[];self.bones={};self.actions={};self.contacts={}
 def yz(self,point):return ((self.anchor[0]-point[0])*self.pixel_scale,(self.anchor[1]-point[1])*self.pixel_scale)
 def own(self,obj,bone,material):
  for c in list(obj.users_collection):c.objects.unlink(obj)
  self.collection.objects.link(obj);obj.data.materials.append(MATS[material]);obj['assembly']=bone
  bpy.context.view_layer.update();bpy.context.view_layer.objects.active=obj;obj.select_set(True);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);obj.select_set(False);self.parts.append((obj,bone));return obj
 def prism(self,name,profile,width,bone='Frame',mat='Graphite',x=0,bevel=.001):
  bpy.ops.object.select_all(action='DESELECT');n=len(profile);verts=[(xx,y,z) for xx in [x-width/2,x+width/2] for y,z in profile];faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
  data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update();obj=bpy.data.objects.new(self.name+' '+name,data);scene.collection.objects.link(obj);bpy.context.view_layer.update();bpy.context.view_layer.objects.active=obj;obj.select_set(True)
  if bevel:
   mod=obj.modifiers.new('Machined chamfer','BEVEL');mod.width=bevel;mod.segments=1;bpy.ops.object.modifier_apply(modifier=mod.name)
  return self.own(obj,bone,mat)
 def trace(self,name,points,width,bone='Frame',mat='Armor',x=0,bevel=.001):return self.prism(name,[self.yz(p) for p in points],width,bone,mat,x,bevel)
 def box(self,name,center,size,bone='Frame',mat='Graphite',bevel=.001,rotation=None):
  bpy.ops.object.select_all(action='DESELECT');bpy.ops.mesh.primitive_cube_add(size=1,location=center);obj=bpy.context.object;obj.name=self.name+' '+name;obj.dimensions=size
  if rotation:obj.rotation_euler=rotation
  bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
  if bevel:
   mod=obj.modifiers.new('Chamfer','BEVEL');mod.width=bevel;mod.segments=1;bpy.ops.object.modifier_apply(modifier=mod.name)
  return self.own(obj,bone,mat)
 def cylinder(self,name,center,radius,depth,bone='Frame',mat='Steel',axis=(0,1,0),sides=12):
  bpy.ops.object.select_all(action='DESELECT');bpy.ops.mesh.primitive_cylinder_add(vertices=sides,radius=radius,depth=depth,location=center);obj=bpy.context.object;obj.name=self.name+' '+name;obj.rotation_euler=Vector(axis).to_track_quat('Z','Y').to_euler();return self.own(obj,bone,mat)
 def ring(self,name,center,outer,inner,depth,bone='Frame',mat='Steel',sides=12):
  y0=center[1]-depth/2;y1=center[1]+depth/2;verts=[]
  for y,r in [(y0,outer),(y1,outer),(y0,inner),(y1,inner)]:
   verts.extend([(center[0]+r*math.cos(i*math.tau/sides),y,center[2]+r*math.sin(i*math.tau/sides)) for i in range(sides)])
  faces=[]
  for i in range(sides):
   j=(i+1)%sides;faces.extend([(i,j,sides+j,sides+i),(2*sides+j,2*sides+i,3*sides+i,3*sides+j),(sides+i,sides+j,3*sides+j,3*sides+i),(j,i,2*sides+i,2*sides+j)])
  data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update();obj=bpy.data.objects.new(self.name+' '+name,data);scene.collection.objects.link(obj);return self.own(obj,bone,mat)
 def cut(self,obj,profile,width):
  cutter=self.prism('temporary cut',profile,width,bevel=0);self.parts.pop();bpy.context.view_layer.objects.active=obj;mod=obj.modifiers.new('Open panel aperture','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter;bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cutter,do_unlink=True)
 def panel(self,name,points,side,bone='Frame',mat='Armor',width=.003):return self.trace(name,points,width,bone,mat,x=side,bevel=.0008)
 def bolts(self,points,width,bone='Frame'):
  for side in [-1,1]:
   for i,point in enumerate(points):
    y,z=self.yz(point);self.cylinder('Hex screw '+str(i),(side*width,y,z),.0028,.0018,bone,'Steel',axis=(1,0,0),sides=6);self.cylinder('Screw recess '+str(i),(side*(width+.001),y,z),.0012,.002,bone,'Recess',axis=(1,0,0),sides=6)
 def line(self,name,a,b,width,bone='Frame',mat='Orange',side=0):
  a=Vector((side,*a));b=Vector((side,*b));delta=b-a;obj=self.box(name,(a+b)/2,(width,width,delta.length),bone,mat,bevel=width*.24);# orient a fresh mesh about its already baked centre
  rotation=delta.to_track_quat('Z','Y').to_matrix()
  for v in obj.data.vertices:v.co=(a+b)/2+rotation@(v.co-(a+b)/2)
  return obj
 def p_line(self,name,a,b,width,bone='Frame',mat='Orange',side=0):return self.line(name,self.yz(a),self.yz(b),width,bone,mat,side)
 def add_bone(self,name,head,tail=None,parent='WeaponMotion',connected=False):self.bones[name]=(Vector(head),Vector(tail) if tail else Vector(head)+Vector((0,0,.04)),parent,connected)
 def key(self,bone,frame,move=(0,0,0),pitch=0,yaw=0,roll=0,scale=None):
  p=self.rig.pose.bones[bone];basis=self.rig.data.bones[bone].matrix_local.to_3x3();p.location=basis.inverted()@Vector(move);r=Matrix.Rotation(math.radians(pitch),3,'X')@Matrix.Rotation(math.radians(yaw),3,'Z')@Matrix.Rotation(math.radians(roll),3,'Y');p.rotation_quaternion=(basis.inverted()@r@basis).to_quaternion();p.keyframe_insert(data_path='location',frame=frame);p.keyframe_insert(data_path='rotation_quaternion',frame=frame)
  if scale is not None:p.scale=(scale,)*3;p.keyframe_insert(data_path='scale',frame=frame)
 def rest(self):
  self.rig.animation_data.action=None
  for track in self.rig.animation_data.nla_tracks:track.mute=True
  for p in self.rig.pose.bones:p.location=(0,0,0);p.rotation_quaternion=(1,0,0,0);p.scale=(1,1,1)
  for name in ['ReplacementMagazine','Shell']:
   if name in self.rig.pose.bones:self.rig.pose.bones[name].scale=(.001,)*3
  scene.frame_set(1);bpy.context.view_layer.update()
bpy.app.driver_namespace['cyber']={'Gun':Gun,'guns':[],'MATS':MATS,'OUT':OUT}
result='Cyberpunk geometry helpers ready'
