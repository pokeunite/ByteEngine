import bpy
C=bpy.app.driver_namespace['cyber'];scene=bpy.context.scene;rows=[]
for g in C['guns']:
 for clip,action in g.actions.items():
  g.rest();g.rig.animation_data.action=action
  worst=(0,None,None,None)
  for f in range(1,int(action.frame_range[1])+1,3):
   scene.frame_set(f);bpy.context.view_layer.update()
   for side in ['Right','Left']:
    hand=g.rig.pose.bones[side+'Hand'].matrix.translation;target=g.rig.pose.bones[side+'WristControl'].matrix.translation;err=(hand-target).length
    if err>worst[0]:worst=(err,f,side,list(target))
  rows.append({'gun':g.name,'clip':clip,'worst':worst})
 g.rest()
result=rows
