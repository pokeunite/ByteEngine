import bpy,json
from pathlib import Path
from mathutils import Vector
S=bpy.data.scenes['Hazard Pay Decorated Camp REVIEW'];bpy.context.window.scene=S
P=Path('C:/Users/codex/Documents/palclone/SourceArt/MiningMachines');OUT=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/camp-decorations')
spec=[('Massive mining drill','MiningDrill-Massive.blend','Reference Mining Drill',(-11,8,0),8.5),('Refinery A','Refinery.blend','Reference Refinery',(-1,9,0),5.5),('Refinery B','Refinery.blend','Reference Refinery',(6,9,0),5.5),('Ammo press','AmmoPress.blend','Reference Ammo Press',(12.5,7,0),3.6),('Company tent terminal','CompanyTerminal.blend','Reference Company Terminal',(17,10,0),5.5)]
cache={};report=[]
for name,file,source,position,width in spec:
 for c in S.collection.children:
  if c.name=='Camp machine - '+name:c.hide_render=True;c.hide_viewport=True
 if (file,source) not in cache:
  with bpy.data.libraries.load(str(P/file),link=False) as (src,dst):dst.collections=[source]
  col=dst.collections[0];col.name='Native review '+source;S.collection.children.link(col);S.frame_set(1);bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get()
  points=[o.evaluated_get(deps).matrix_world@Vector(corner) for o in col.all_objects if o.type=='MESH' and not o.hide_render for corner in o.evaluated_get(deps).bound_box]
  low=Vector(tuple(min(p[i] for p in points) for i in range(3)));high=Vector(tuple(max(p[i] for p in points) for i in range(3)));col.instance_offset=Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z));cache[file,source]=(col,high.x-low.x);S.collection.children.unlink(col)
 col,size=cache[file,source];o=bpy.data.objects.new('Native '+name,None);o.instance_type='COLLECTION';o.instance_collection=col;S.collection.objects.link(o);o.location=position;o.scale=(width/size,)*3;report.append({'name':name,'source_width':size,'scale':width/size})
S.render.filepath=str(OUT/'Decorated-Camp-Overview.png');bpy.ops.render.render(write_still=True);print(json.dumps(report))
