"""Secondary industrial forms and hardware for the second art pass."""
def detail_pass(key):
    if key.startswith('Wall_') and key.endswith('m'):
        w=2 if key=='Wall_2m' else 4
        for sign in [-1,1]:
            y=sign*.18
            box('Panel recessed gasket',(0,y,1.90),(w-.14,.035,1.67),'Dark',.025)
            box('Replaceable concrete inset',(0,y+sign*.023,1.91),(w-.21,.032,1.58),'Concrete',.024)
            box('Lower service bay',(0,y,.57),(w-.14,.035,.52),'Steel',.015)
            box('Lower service panel',(0,y+sign*.025,.59),(w-.24,.020,.38),'Concrete',.018)
            for x in [-w/2+.11,w/2-.11]:
                box('Panel edge extrusion',(x,sign*.213,1.5),(.055,.04,2.70),'Steel',.007)
                for z in [.29,1.45,2.70]:cyl('Captive quarter-turn bolt',(x,sign*.238,z),.024,.009,'Dark',(math.pi/2,0,0),12,.001)
            box('Top light trough',(0,sign*.198,2.81),(w-.22,.07,.085),'Dark',.008)
            box('Diffused light trim',(0,sign*.238,2.81),(w-.28,.018,.026),'Warm',.003)
            box('Panel ID badge',(-w*.30,sign*.23,2.42),(.30,.018,.13),'Steel',.004)
            if sign==-1:text('MD / 07',(-w*.30,-.246,2.42),.057)
    elif key=='Wall_CornerColumn':
        for x in [-.285,.285]:
            box('Corner channel',(x,0,1.5),(.035,.31,2.68),'Steel',.006)
        for z in [.29,2.70]:
            for y in [-.281,.281]:box('Column bolted plate',(0,y,z),(.34,.023,.15),'Steel',.006)
    elif key.startswith('Cover_'):
        h={'Cover_Low':1,'Cover_Tall':1.8,'Cover_Pillar':2.5}[key]
        w=1 if key=='Cover_Pillar' else 2
        box('Cover plinth',(0,0,.055),(w+.09,1.09,.11),'Dark',.02)
        for side in [-1,1]:
            x=side*(w/2+.021)
            box('Replaceable edge armour',(x,0,h*.51),(.045,.87,h*.80),'Steel',.018)
            box('Inset carry recess',(x+side*.028,0,h*.62),(.014,.36,.13),'Dark',.012)
            rod('Carry bar',(x+side*.047,-.125,h*.62),(x+side*.047,.125,h*.62),.017,'Steel')
        for y in [-.552,.552]:
            for x in [-w*.40,w*.40]:
                for z in [.36,h-.18]:cyl('Cover countersunk screw',(x,y,z),.022,.009,'Steel',(math.pi/2,0,0),8,.001)
        for x in [-w*.36,w*.36]:
            box('Top wear strip',(x,0,h+.027),(.09,.80,.012),'Steel',.004)
        box('Cover designation',(0,-.552,h*.67),(.37,.012,.13),'Dark',.006)
        text('MD-BARRIER',(0,-.563,h*.67),.047)
    elif key.startswith('Crate_'):
        w,d,h=([.8,.8,.8] if key=='Crate_Small' else [1.4,1.2,1.2])
        for y in [-d/2-.012,d/2+.012]:
            box('Inset latch receiver',(0,y,h*.75),(.25,.018,.09),'Dark',.01)
            rod('Spring carry handle',(-.085,y-.025,h*.75),(.085,y-.025,h*.75),.018,'Steel')
            for x in [-w*.32,w*.32]:
                box('Latch recess',(x,y,h*.82),(.14,.025,.24),'Dark',.014)
                box('Locking lever',(x,y-.025,h*.82),(.08,.025,.17),'Steel',.009)
                cyl('Latch hinge',(x,y-.045,h*.91),.02,.11,'Steel',(0,math.pi/2,0),20,.001)
                for z in [.12,h-.12]:cyl('Frame fixing',(x,y-.021,z),.023,.015,'Steel',(math.pi/2,0,0),8,.001)
        for x in [-w/2-.006,w/2+.006]:
            box('Side recessed bay',(x,0,h*.49),(.02,d*.63,h*.56),'Dark',.024)
            box('Side cargo panel',(x+(1 if x>0 else -1)*.014,0,h*.49),(.02,d*.54,h*.48),'Orange',.016)
            for yy in [-d*.19,d*.19]:box('Panel reinforcement rib',(x+(1 if x>0 else -1)*.027,yy,h*.49),(.026,.025,h*.45),'Steel',.004)
        box('Shipping data plate',(-w*.22,-d/2-.031,h*.31),(.24,.01,.13),'White',.003)
        text('DEPOT / 07',(-w*.22,-d/2-.039,h*.31),.034,'Dark')
        for i in range(12):box('Barcode',(-w*.31+i*.014,-d/2-.041,h*.23),(.006,.004,.047),'Dark',0)
        for x in [-w*.34,w*.34]:
            for y in [-d*.34,d*.34]:box('Recessed cargo foot',(x,y,.034),(.14,.14,.068),'Dark',.009)
    elif key=='Weapon_Rack':
        for x in [-.73,-.365,0,.365,.73]:
            for z in [.65,.9,1.15,1.4,1.65]:cyl('Mounting-board hole',(x,.133,z),.023,.012,'Steel',(math.pi/2,0,0),12,.001)
        for x in [-.60,0,.60]:
            box('Orange weapon cradle',(x,-.02,.48),(.22,.27,.08),'Orange',.016)
            text(str(1+round((x+.6)/.6)),(x,.128,1.73),.11)
    elif key.startswith('Doorway'):
        for x in [-1.53,1.53]:
            box('Reinforced jamb panel',(x,-.27,1.18),(.30,.13,1.70),'Steel',.025)
            box('Jamb shock cushion',(x,-.345,.72),(.18,.035,.70),'Dark',.014)
            box('Door upright insert',(x,-.35,1.72),(.18,.035,.28),'Orange',.013)
            for z in [.31,1.02,1.88]:cyl('Jamb mounting bolt',(x,-.375,z),.026,.02,'Steel',(math.pi/2,0,0),8,.002)
        box('Threshold',(0,-.01,.014),(3.02,.48,.028),'Steel',.007)
        for x in [-1.2,-.9,-.6,-.3,0,.3,.6,.9,1.2]:box('Threshold drainage slot',(x,-.02,.030),(.025,.31,.007),'Dark',.001)
        box('Door access enclosure',(1.79,-.24,1.39),(.24,.18,.43),'Dark',.018)
        box('Access screen',(1.79,-.34,1.47),(.16,.028,.12),'Cyan',.007)
        for x in [1.75,1.82]:
            for z in [1.30,1.35]:cyl('Keypad key',(x,-.34,z),.015,.01,'White',(math.pi/2,0,0),12,.001)
    elif key.startswith('Stair_'):
        rise=1 if key=='Stair_1m' else 2.5;n=5 if rise==1 else 14;run=n*.30
        for x in [-1.0,1.0]:
            rod('Stair side stringer',(x,-run/2,.05),(x,run/2,rise-.1),.045,'Dark')
            for i in [0,n//2,n-1]:
                y=-run/2+(i+.5)*.3;z=rise*(i+1)/n
                box('Handrail bolted socket',(x,y,z+.025),(.14,.14,.05),'Steel',.004)
                for xx in [-.04,.04]:cyl('Socket bolt',(x+xx,y,z+.053),.012,.011,'Dark',vertices=8,bevel=.001)
    elif key=='Balcony_2m':
        for x in [-.82,.82]:box('Deck supporting joist',(x,0,-.30),(.11,1.9,.32),'Steel',.012)
        box('Recessed fascia seam',(0,-1.046,-.18),(1.82,.025,.026),'Dark',.003)
    elif key.startswith('Railing'):
        for x in [-.92,.92]:
            for xx in [-.055,.055]:
                for yy in [-.055,.055]:cyl('Rail foot anchor',(x+xx,yy,.041),.014,.010,'Dark',vertices=8,bevel=.001)
            cyl('Collar sleeve',(x,0,.65),.041,.085,'Dark')
        box('Rail orange safety plaque',(0,-.025,.75),(.36,.018,.13),'Orange',.005)
        text('CAUTION',(0,-.037,.75),.052,'White')
    elif key=='Depot_Terminal':
        for x in [-.215,.215]:
            box('Terminal frame extrusion',(x,-.229,.72),(.04,.06,.79),'Dark',.007)
            for z in [.30,.67,1.04]:cyl('Terminal screw',(x,-.265,z),.014,.012,'Steel',(math.pi/2,0,0),8,.001)
        box('Terminal intake',(0,-.229,.68),(.29,.03,.15),'Dark',.007)
        for z in [.64,.68,.72]:box('Terminal louvre',(0,-.25,z),(.26,.03,.013),'Steel',.002)
        box('Terminal badge plate',(0,-.238,.36),(.29,.01,.09),'White',.002)
        text('MD / CONTROL',(0,-.245,.36),.031,'Dark')
        cyl('Terminal status lamp',(.175,-.284,1.02),.022,.018,'Cyan',(math.pi/2,0,0),16,.002)
    elif key=='Milk_Exit_Tower':
        for y in [-1.9,0,1.9]:
            for x in [-2.33,2.33]:box('Tower flank structural rib',(x,y,4.1),(.10,.14,7.4),'Steel',.014)
        for z in [2.8,5.4,7.6]:
            for x in [-2.33,2.33]:box('Tower service seam',(x,0,z),(.05,4.25,.035),'Dark',.004)
        for x in [-1.6,1.6]:
            box('Tower face extrusion',(x,-2.38,5.1),(.09,.11,5.0),'Steel',.012)
            for z in [3.0,5.2,7.5]:cyl('Facade fastener',(x,-2.447,z),.045,.025,'Dark',(math.pi/2,0,0),8,.002)
        box('Canopy',(0,-2.67,2.95),(3.0,1.0,.18),'Steel',.025)
        box('Canopy light',(0,-2.8,2.846),(2.6,.10,.018),'Warm',.004)
        text('EXTRACTION  /  06',(0,-2.441,2.56),.19,'White')
        for x in [-1.2,1.2]:
            for y in [.35,.7,1.05]:box('Roof service louvre',(x,y,8.68),(.68,.12,.035),'Dark',.004)
    elif key=='Ammo_Case':
        for x in [-.25,.25]:box('Ammo corner guard',(x,0,.19),(.04,.35,.32),'Steel',.005)
        for i in range(4):box('Case strengthening bead',(-.15+i*.1,0,.44),(.024,.25,.012),'Dark',.003)
    elif key=='Utility_Drum':
        for i in range(20):
            a=i*math.tau/20
            cyl('Lid screw',(math.cos(a)*.26,math.sin(a)*.26,1.123),.010,.008,'Dark',vertices=8,bevel=.001)
    elif key=='Training_Dummy':
        for x in [-.22,.22]:
            cyl('Chest fastener',(x,-.176,1.42),.019,.011,'Steel',(math.pi/2,0,0),8,.001)
        for x in [-.10,0,.10]:box('Chest cooling vent',(x,-.232,1.22),(.034,.014,.048),'Dark',.002)
        for side in [-1,1]:
            rod('Arm actuator',(side*.39,.07,1.33),(side*.49,.065,1.11),.023,'Steel')
            for z in [.34,.49]:box('Shin pad',(side*.16,-.118,z),(.13,.025,.075),'Dark',.008)
