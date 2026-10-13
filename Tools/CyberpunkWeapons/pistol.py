import bpy,math
from mathutils import Vector
C=bpy.app.driver_namespace['cyber'];g=C['Gun']('Pistol',(502,120),.0006)
g.trace('Slide chassis',[(258,38),(288,24),(622,25),(656,35),(675,61),(693,102),(674,119),(455,131),(328,119),(267,96)],.045,'Slide','Graphite',bevel=.002)
lower=g.trace('Lower frame',[(272,107),(319,108),(340,128),(420,128),(454,132),(484,132),(559,128),(582,145),(570,188),(547,196),(430,190),(419,166),(395,136),(278,130)],.037)
guard=g.trace('Open trigger guard',[(423,125),(559,126),(577,145),(569,183),(549,194),(440,190),(425,179),(403,140)],.030,'Frame','Graphite',bevel=.0015)
g.cut(guard,[g.yz(p) for p in [(444,140),(546,140),(551,158),(542,179),(449,176),(435,153)]],.05)
g.cut(lower,[g.yz(p) for p in [(444,140),(546,140),(551,158),(542,179),(449,176),(435,153)]],.05)
for side in [-1,1]:
 g.panel('Lower nose armor',[(266,87),(303,85),(337,105),(409,105),(429,124),(414,134),(275,127),(254,111)],side*.0235,'Frame')
 g.panel('Slide forward plate',[(303,37),(423,36),(436,64),(416,77),(358,78),(341,64),(302,61)],side*.0245,'Slide')
 g.panel('Slide central armor',[(419,32),(584,29),(611,45),(601,79),(568,88),(466,78),(445,60)],side*.0245,'Slide')
 g.panel('Rear armor',[(609,36),(653,38),(668,64),(679,85),(662,98),(610,88)],side*.0245,'Slide')
 g.panel('Lower frame stripe',[(439,111),(519,109),(543,96),(566,96),(575,112),(551,125),(457,128)],side*.024,'Frame','Armor')
 g.panel('Grip shell',[(568,130),(617,127),(654,149),(698,278),(681,294),(619,288),(602,259),(581,177)],side*.019,'Frame','Armor')
 g.panel('Grip rubber',[(602,172),(630,160),(648,174),(682,273),(674,281),(632,280),(616,247)],side*.022,'Frame','Rubber')
 g.p_line('Orange nose strip',(297,53),(336,53),.0024,'Slide','Orange',side*.027)
 g.p_line('Cyan status',(551,94),(571,94),.0025,'Frame','Cyan',side*.027)
 g.p_line('Grip cyan',(607,203),(615,225),.0022,'Frame','Cyan',side*.0235)
 # Actual side apertures surrounded by dark bevel strips.
 for i in range(3):g.panel('Slide vent '+str(i),[(579+i*13,45),(587+i*13,44),(596+i*13,76),(587+i*13,76)],side*.027,'Slide','Recess',.001)
 for j in range(9):
  for i in range(3):
   y,z=g.yz((637+i*8+j*2,184+j*10));g.box('Grip texture',(side*.0234,y,z),(.0012,.0022,.0022),'Frame','Graphite',bevel=.0003)
 g.panel('Frame slot',[(411,103),(432,83),(462,85),(449,104)],side*.023,'Frame','Recess',.001)
 g.panel('Forward slot',[(359,92),(390,88),(399,100),(373,108)],side*.024,'Frame','Recess',.001)
 g.panel('Release lever',[(565,113),(583,112),(582,121),(567,124)],side*.028,'Frame','Steel')
# Grip well walls are separate; no solid fill blocks magazine travel.
g.trace('Grip back strap',[(622,132),(637,139),(701,279),(692,290),(675,279)],.027,'Frame','Rubber')
g.trace('Grip front strap',[(574,151),(584,148),(632,281),(622,283)],.027,'Frame','Rubber')
for bone in ['Magazine','ReplacementMagazine']:
 g.trace('Magazine tube',[(592,145),(617,138),(664,285),(637,291)],.022,bone,'Steel',bevel=.0008)
 g.trace('Magazine floor plate',[(629,283),(672,278),(680,297),(643,306),(622,297)],.030,bone,'Graphite',bevel=.0012)
 g.trace('Feed lips',[(591,138),(615,133),(620,143),(594,149)],.024,bone,'Steel',bevel=.0005)
 g.cylinder('Follower',(0,*g.yz((603,142))),.009,.006,bone,'Brass',axis=(0,0,1),sides=8)
 for side in [-1,1]:g.p_line('Magazine base inset',(638,296),(666,288),.0018,bone,'Steel',side*.016)
g.trace('Curved trigger',[(483,130),(496,130),(500,145),(493,164),(482,174),(479,169),(488,151)],.009,'Trigger','Steel',bevel=.0007)
g.ring('Muzzle shroud',(0,*g.yz((258,66))),.015,.010,.014,'Frame','Graphite')
g.ring('Muzzle orange ring',(0,*g.yz((246,66))),.0115,.009,.002,'Frame','Orange')
g.ring('Barrel interior',(0,*g.yz((266,66))),.010,.0075,.023,'Frame','Recess')
g.trace('Front sight',[(286,22),(309,22),(307,32),(279,32)],.012,'Slide','Graphite');g.trace('Rear sight',[(628,20),(658,20),(655,35),(632,35)],.026,'Slide','Graphite')
g.bolts([(299,31),(331,112),(447,39),(625,53),(656,88)],.028,'Slide');g.bolts([(569,145),(636,157),(686,278),(649,292)],.025)
g.add_bone('Frame',(0,0,0));g.add_bone('Slide',(0,0,.028));g.add_bone('Trigger',(0,*g.yz((490,134))));g.add_bone('Magazine',(0,*g.yz((605,145))));g.add_bone('ReplacementMagazine',(0,*g.yz((605,145))));g.add_bone('MuzzleSocket',(0,*g.yz((246,66))))
g.mag_axis=Vector((0,-.30,-.954));g.mag_clear=.18;g.grip=(-.032,-.076,-.073);g.support=(.025,-.074,-.090);g.support_kind='pistol';g.muzzle=(0,*g.yz((246,66)))
C['guns'].append(g);result={'gun':g.name,'parts':len(g.parts)}
