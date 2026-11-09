"""Run through Blender MCP; keep the user's existing scene intact."""
exec(open('C:/Users/codex/.codex/worktrees/52af/ByteEngine/Tools/MiningPrototype/BuildResearchModels.py').read())
PREFIX='MortarRound';C=bpy.data.collections.get('RD_MortarRound')
if C:
 for o in list(C.objects):bpy.data.objects.remove(o,do_unlink=True)
 bpy.data.collections.remove(C)
C=bpy.data.collections.new('RD_MortarRound');S.collection.children.link(C);PARTS=[];BONES={};bone('Root',(0,0,0),None)
# Twelve-sided lathed ogive, tapered neck and broad yellow band.
levels=[(.19,.045),(.25,.065),(.31,.095),(.39,.135),(.49,.155),(.59,.154),(.68,.14),(.76,.105),(.82,.06),(.85,.032)]
v=[];f=[];n=12
for z,r in levels:
 for i in range(n):a=math.tau*i/n;v.append((r*math.cos(a),r*math.sin(a),z))
for j in range(len(levels)-1):
 for i in range(n):f.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
f.extend([tuple(range(n-1,-1,-1)),tuple(range((len(levels)-1)*n,len(levels)*n))]);me=bpy.data.meshes.new('Faceted ogive');me.from_pydata(v,[],f);me.update();o=bpy.data.objects.new('Mortar body',me);C.objects.link(o);finish(o,'continuous faceted body','dark',bevel=0)
cylinder('broad identification band',(0,0,.585),.158,.12,'yellow',n=12,bevel=.002)
cylinder('tail boom',(0,0,.16),.035,.24,'steel',n=12)
cylinder('tail collar',(0,0,.205),.052,.07,'dark',n=12)
ring('tail base ring',(0,0,.047),.053,.028,.035,'steel',n=12)
for i in range(6):
 a=math.tau*i/6
 fin=prism('stabilizing fin',[(.032,.04),(.135,.012),(.135,.12),(.065,.205),(.032,.205)],1,-.009,.009,'yellow',bevel=.003)
 from mathutils import Matrix
 rot=Matrix.Rotation(a,4,'Z')
 for vert in fin.data.vertices:vert.co=rot@vert.co
 bolt((.052*math.cos(a),.052*math.sin(a),.21),axis='Z',r=.009)
cylinder('silver nose fuze',(0,0,.855),.037,.04,'steel',n=8,bevel=.004)
lamp('nose status',(0,-.032,.852),(.028,.008,.009))
box('side service bezel',(0,-.151,.435),(.07,.018,.12),'black',bevel=.007)
for i in range(3):lamp('service status',(0,-.164,.405+i*.027),(.03,.009,.012))
for x in (-.043,.043):
 for z in (.39,.48):bolt((x,-.151,z),r=.007)
for z,r in ((.30,.095),(.70,.133)):
 ring('segmented body seam',(0,0,z),r+.003,r-.006,.007,'steel',n=12)
rig=rig_asset();rig.animation_data_create();rig['reference']='Mortar-Round-Reference.png'
for o in C.objects:o.select_set(False)
print(export('MortarRound'))
print(preview('MortarRound'))
