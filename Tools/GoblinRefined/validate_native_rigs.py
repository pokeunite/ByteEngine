import bpy,sys,json,math
from pathlib import Path
from mathutils import Vector
out=Path(sys.argv[sys.argv.index('--')+1]); bpy.ops.wm.open_mainfile(filepath=str(out/'GoblinScraper_RefinedParts.blend'))
catalog=json.loads((out/'parts-catalog.json').read_text()); scene=bpy.context.scene
results=[]; socket_count=0
for part in catalog['parts']:
    file=Path(part['file']).stem; col=bpy.data.collections[part['label']]
    rig=next(o for o in col.objects if o.type=='ARMATURE'); root=bpy.data.objects[file]
    rig.animation_data.action=None
    for track in rig.animation_data.nla_tracks: track.mute=True
    for pb in rig.pose.bones: pb.location=(0,0,0); pb.rotation_quaternion=(1,0,0,0); pb.scale=(1,1,1)
    scene.frame_set(1); bpy.context.view_layer.update()
    for socket in part['sockets']:
        obj=bpy.data.objects[socket['name']] if socket['name'] in col.objects else next(o for o in col.objects if o.name.startswith(socket['name']))
        v=root.matrix_world.inverted()@obj.matrix_world.translation
        actual=Vector((v.x,v.z,-v.y)); expected=Vector(socket['position'])
        if (actual-expected).length>1e-4: raise RuntimeError(f'Socket position changed: {file} {socket["name"]}: {tuple(actual)} vs {tuple(expected)}')
        bone=socket.get('animation_bone','Root')
        if bone!='Root' and (obj.parent!=rig or obj.parent_type!='BONE' or obj.parent_bone!=bone):
            raise RuntimeError('Unbound moving socket '+file+' '+socket['name'])
        socket_count+=1
    for clip in part['animations']:
        action=bpy.data.actions[clip['name']]; rig.animation_data.action=action
        end=int(round(clip['duration_seconds']*30))+1
        def matrices(f):
            scene.frame_set(f); bpy.context.view_layer.update()
            return [tuple(v for row in pb.matrix for v in row) for pb in rig.pose.bones]
        first=matrices(1); samples=[matrices(max(2,int(1+(end-1)*t))) for t in [.22,.47,.71]]
        diff=max(abs(a-b) for sample in samples for left,right in zip(first,sample) for a,b in zip(left,right))
        if diff<1e-4: raise RuntimeError('Static animation clip '+clip['name'])
        last=matrices(end)
        loop_error=max(abs(a-b) for left,right in zip(first,last) for a,b in zip(left,right))
        if clip['loop'] and loop_error>1e-3: raise RuntimeError('Nonseamless looping clip '+clip['name']+': '+str(loop_error))
        results.append({'clip':clip['name'],'evaluated_motion':True,'loop':clip['loop'],'loop_matrix_error':loop_error})
    rig.animation_data.action=None
    for pb in rig.pose.bones: pb.location=(0,0,0); pb.rotation_quaternion=(1,0,0,0); pb.scale=(1,1,1)
summary={'verification':'Blender 5 evaluated poses and socket transforms','clips':results,'socket_count':socket_count,'all_passed':True}
(out/'animation-validation.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
print('RIG_SOCKET_VALIDATION_PASS',len(results),'animated clips,',socket_count,'socket transforms',flush=True)
