import bpy
from mathutils import Vector
C=bpy.app.driver_namespace['cyber'];scene=bpy.context.scene;tube=C['Gun'].add_arms.__globals__['tube']
for g in C['guns']:
 g.rest()
 for side in ['Right','Left']:
  wrist=g.contacts[side]['wrist'];palm=g.contacts[side]['palm'];fore=side+'Forearm';hand=side+'Hand'
  points=[wrist,wrist.lerp(palm,.30),wrist.lerp(palm,.65),wrist.lerp(palm,.92)]
  weights=[{fore:.5,hand:.5},{hand:1},{hand:1},{hand:1}]
  obj=tube(g,side+' connected glove wrist',points,[.025,.023,.019,.015],weights,'Glove',12,.82);obj,vertex_weights=g.skinned.pop()
  names=set(k for w in vertex_weights for k in w);groups={name:obj.vertex_groups.new(name=name) for name in names}
  for i,weight in enumerate(vertex_weights):
   for name,value in weight.items():groups[name].add([i],value,'REPLACE')
  obj.parent=g.rig
  target=next(m for m in g.meshes if m.data.materials[0]==C['MATS']['Glove']);bpy.ops.object.select_all(action='DESELECT');target.select_set(True);obj.select_set(True);bpy.context.view_layer.objects.active=target;bpy.ops.object.join()
 g.rest()
result='Continuous skinned glove-to-cuff bridges added to both wrists on all three rigs'
