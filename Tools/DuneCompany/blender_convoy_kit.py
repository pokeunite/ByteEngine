"""Original editable convoy skins and mission props, executed inside Blender."""
from pathlib import Path
exec(compile(Path('C:/Users/codex/ByteEngine/Tools/DuneCompany/blender_race_kit.py').read_text(), 'race-kit', 'exec'),globals())
for collection in list(bpy.data.collections):
 if collection.name.startswith(('Asset / convoy-','Asset / salvage-sign')):
  for obj in list(collection.objects):bpy.data.objects.remove(obj,do_unlink=True)
  bpy.data.collections.remove(collection)

def bolts(x,y,z,width):
 for side in [-1,1]:
  for k in range(5):cylinder('Hex armour fastener',(x+side*width,y+k*.35,z),.025,.025,1,6)

def build_convoy(name):
 PARTS.clear()
 if name=='convoy-cargo':
  # Blender -Y forward becomes engine -Z; shell fits the six-wheel salvage rig.
  cube('Armoured cargo deck',(0,1.7,.64),(1.45,3.65,.10),1,.035)
  for x in [-.75,.75]:
   cube('Cargo side rail',(x,1.8,.94),(.06,3.5,.50),0,.02)
   for y in [.3,1.2,2.1,3.0]:cube('Side reinforcing rib',(x,y,.98),(.09,.10,.60),2,.025)
   for y in [.15,1.05,2.0,2.95]:cylinder('Rail bolt',(x,y,1.21),.025,.03,1,6)
  cube('Rear tailgate',(0,3.52,1),(1.5,.07,.58),0,.03)
  for x in [-.5,.5]:cube('Tail lamp',(x,3.57,.76),(.18,.045,.10),3,.018)
  for y in [.7,1.8,2.9]:
   cube('Locked salvage crate',(0,y,1.10),(1.17,.95,.77),0,.06)
   cube('Raised lid',(0,y,1.52),(1.20,.97,.08),2,.02)
   for x in [-.38,.38]:cube('Orange cargo strap',(x,y,1.57),(.07,1,.018),3,.004)
   for x in [-.52,.52]:cube('Crate corner',(x,y,1.11),(.075,1,.8),1,.015)
  # Open bonnet leaves the real engine readable instead of burying it in the skin.
  for x in [-.68,.68]:
   cube('Cab armour cheek',(x,-.35,.95),(.07,1.2,.65),2,.035)
   beam('Cab roll hoop',(x,-.72,.72),(x,-.72,1.75),.075,1)
  beam('Cab overhead bar',(-.68,-.72,1.75),(.68,-.72,1.75),.075,1)
  cube('Front bumper',(0,-1.05,.4),(1.65,.14,.13),1,.035)
  for x in [-.53,.53]:cylinder('Round headlamp',(x,-1.12,.72),.095,.06,6,24,(0,-1,0))
  cube('Front orange identifier',(0,-1.14,.40),(.32,.015,.14),3,.004)
  for x in [-.78,.78]:
   for y in [-.55,1.3,3.1]:cube('Wheel arch top',(x,y,.55),(.40,.75,.055),2,.022)
  text('Salvage side marking','SALVAGE',(0,3.57,1.15),.17,6)
 elif name=='convoy-turret':
  cylinder('Turret pivot',(0,0,.10),.36,.2,1,32)
  cylinder('Rotating bearing',(0,0,.23),.29,.075,2,32)
  cube('Armoured receiver',(0,0,.50),(.52,.64,.42),0,.05)
  cube('Orange receiver band',(0,-.18,.73),(.18,.30,.018),3,.004)
  for x in [-.16,.16]:
   cylinder('Gun cooling sleeve',(x,-.57,.51),.07,.65,1,24,(0,1,0))
   cylinder('Gun muzzle',(x,-.96,.51),.085,.12,1,24,(0,1,0))
   for k in range(4):torus('Cooling fin',(x,-.40-k*.10,.51),.07,.014,2,(0,1,0))
  cube('Ammo box',(.39,.16,.48),(.23,.35,.34),2,.035)
  for side in [-1,1]:cube('Shield cheek',(side*.42,-.04,.52),(.09,.65,.6),0,.025)
  for x in [-.4,.4]:
   for y in [-.25,.25]:cylinder('Shield bolt',(x,y,.85),.026,.028,1,6)
 elif name=='convoy-barrier':
  cube('Heavy salvage roadblock',(0,0,.6),(3.8,.65,1.2),2,.09)
  for x in [-1.45,-.75,0,.75,1.45]:
   o=cube('Hazard stripe',(x,-.34,.65),(.20,.03,1),3,.008);o.rotation_euler.y=.3
  for x in [-1.5,1.5]:
   beam('Barricade support',(x,-.55,.05),(x,.5,1.15),.09,1)
   cube('Foot plate',(x,0,.04),(.5,1.2,.08),1,.02)
 elif name=='salvage-sign':
  for x in [-1,1]:beam('Extraction sign leg',(x,0,0),(x,0,3),.12,1)
  cube('Extraction sign panel',(0,0,2.65),(3.2,.12,.75),0,.06)
  cube('Orange sign header',(0,-.07,2.98),(3.15,.016,.075),3,.004)
  text('Extraction lettering','EXTRACTION',(0,-.075,2.58),.32,6)
  back=text('Extraction reverse lettering','EXTRACTION',(0,.075,2.58),.32,6);back.rotation_euler.z=math.pi
  for x in [-1.52,1.52]:cylinder('Reflective marker',(x,-.085,2.67),.07,.015,6,16,(0,1,0))
 else:raise ValueError(name)
 for obj in PARTS:
  bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
  bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
  for vertex in obj.data.vertices:vertex.co.y=-vertex.co.y
  import bmesh
  mesh=bmesh.new();mesh.from_mesh(obj.data);bmesh.ops.recalc_face_normals(mesh,faces=list(mesh.faces));mesh.to_mesh(obj.data);mesh.free()
 return export(name)

result=[]
for name in ['convoy-cargo','convoy-turret','convoy-barrier','salvage-sign']:result.append(build_convoy(name))
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Art/RaceKit/DuneCompany-Convoy.blend'))
