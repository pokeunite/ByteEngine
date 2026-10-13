import bpy
from mathutils import Vector
C=bpy.app.driver_namespace['cyber'];rows=[]
for g in C['guns']:
 g.rest()
 for mesh in g.meshes:
  names={group.index:group.name for group in mesh.vertex_groups}
  for v in mesh.data.vertices:
   candidates=[(names[group.group],group.weight) for group in v.groups if names.get(group.group) in ['RightUpperArm','RightForearm','LeftUpperArm','LeftForearm']]
   if not candidates:continue
   bone=max(candidates,key=lambda pair:pair[1])[0];a,b,parent,connected=g.bones[bone];axis=b-a;t=(v.co-a).dot(axis)/axis.length_squared;center=a+axis*t;v.co=center+(v.co-center)*.74
  mesh.data.update()
for name,color in [('Glove',(.065,.080,.095,1)),('Sleeve',(.12,.14,.16,1)),('Seam',(.024,.031,.038,1))]:
 m=C['MATS'][name];m.diffuse_color=color;m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=color
result='Sleeve volume reduced to hand scale; glove/sleeve values balanced for engine readability'
