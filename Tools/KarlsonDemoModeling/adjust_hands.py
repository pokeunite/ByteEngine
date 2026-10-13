from pathlib import Path
p=Path('Tools/KarlsonDemoModeling/rebuild_v2.py');s=p.read_text()
a=s.index('def hand(');b=s.index('\ndef rig(',a)
s=s[:a]+'''def hand(gun,bone,center,support=False):
 x,y,z=center
 if bone=='RightHand':
  # Palm alongside the grip; four fingers wrap around its front, thumb across the back.
  gun.box(bone+' palm',(x-.009,y,z),(.022,.042,.078),bone,'Glove',bevel=.006)
  gun.box(bone+' wrist',(x-.002,y-.035,z-.044),(.034,.04,.038),bone,'Grip',angle=-20,bevel=.005)
  gun.box(bone+' forearm',(x+.008,y-.082,z-.068),(.046,.07,.046),bone,'Body',angle=-25,bevel=.006)
  for i in range(4):
   fz=z+.029-i*.017
   gun.box(bone+' knuckle'+str(i),(x-.013,y+.022,fz),(.025,.019,.015),bone,'Glove',bevel=.004)
   gun.box(bone+' finger'+str(i),(.001,y+.026,fz),(.041,.016,.014),bone,'Glove',bevel=.004)
   gun.box(bone+' fingertip'+str(i),(-.018,y+.014,fz),(.015,.022,.014),bone,'Glove',bevel=.003)
  gun.box(bone+' thumb',(.016,y-.014,z+.036),(.043,.021,.021),bone,'Glove',bevel=.005)
 else:
  gun.box(bone+' palm',(x,y,z),(.035,.065,.022),bone,'Glove',bevel=.006)
  gun.box(bone+' wrist',(x-.018,y-.040,z-.015),(.032,.035,.034),bone,'Grip',bevel=.005)
  gun.box(bone+' forearm',(x-.033,y-.085,z-.042),(.043,.07,.043),bone,'Body',angle=-25,bevel=.006)
  for i in range(4):
   fy=y+.022-i*.015
   gun.box(bone+' knuckle'+str(i),(x+.015,fy,z+.015),(.02,.012,.025),bone,'Glove',bevel=.003)
   gun.box(bone+' finger'+str(i),(x+.032,fy,z+.025),(.025,.012,.017),bone,'Glove',bevel=.003)
  gun.box(bone+' thumb',(x-.008,y+.03,z+.022),(.021,.032,.02),bone,'Glove',bevel=.004)
''' +s[b:]
s=s.replace("(-.036,-.15),(-.078,-.154),",'')
s=s.replace("'LeftHand':(-.065,.075,-.065)","'LeftHand':(-.035,-.055,-.125)")
s=s.replace("hand(p,'LeftHand',(-.065,.075,-.065))","hand(p,'LeftHand',(-.035,-.055,-.125))")
s=s.replace("(.064,-.12,-.09) if self.name=='Pistol' else (.064,-.16,-.05)","(.015,-.005,-.015) if self.name=='Pistol' else (.03,-.16,-.10)")
s=s.replace("hand(s,'LeftHand',(-.057,.25,-.035))","hand(s,'LeftHand',(-.025,.25,-.05))")
s=s.replace("hand(r,'LeftHand',(-.063,.20,-.05))","hand(r,'LeftHand',(-.025,.20,-.058))")
p.write_text(s)
