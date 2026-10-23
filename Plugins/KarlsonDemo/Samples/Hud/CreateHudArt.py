"""Original, deterministic vector-style HUD artwork. Pillow is only an authoring dependency."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import math

OUT=Path(__file__).parent/'Art'
OUT.mkdir(parents=True,exist_ok=True)
S=3
def canvas(w,h):return Image.new('RGBA',(w*S,h*S))
def poly(im,points,fill,outline=None,width=1):
    ImageDraw.Draw(im).polygon([(int(x*S),int(y*S)) for x,y in points],fill=fill)
    if outline:ImageDraw.Draw(im).line([(int(x*S),int(y*S)) for x,y in points+[points[0]]],fill=outline,width=width*S,joint='curve')
def rect(im,box,fill,outline=None,width=1):ImageDraw.Draw(im).rectangle(tuple(int(v*S) for v in box),fill=fill,outline=outline,width=width*S)
def line(im,points,fill,width=1):
    overlay=Image.new('RGBA',im.size)
    ImageDraw.Draw(overlay).line([(int(x*S),int(y*S)) for x,y in points],fill=fill,width=width*S,joint='curve')
    im.alpha_composite(overlay)
def save(im,name):im.resize((im.width//S,im.height//S),Image.Resampling.LANCZOS).save(OUT/(name+'.png'))
def bar(name,color,reverse=False):
    frame=canvas(280,32);p=[(2,17),(17,4),(269,4),(278,15),(266,28),(18,28)]
    poly(frame,p,(12,18,22,240),(63,78,83,255));poly(frame,[(13,16),(23,8),(265,8),(272,15),(263,24),(23,24)],(3,10,14,255),(22,34,39,255))
    line(frame,[(21,5),(266,5)],(130,151,153,130));line(frame,[(22,27),(264,27)],(2,5,7,255),2)
    fill=canvas(280,32);mask=canvas(280,32);poly(mask,[(14,16),(24,10),(263,10),(269,15),(261,22),(24,22)],(255,255,255,255))
    draw=ImageDraw.Draw(fill)
    for y in range(9*S,23*S):
        t=(y/S-9)/14;b=.68+.4*math.sin(t*math.pi)
        draw.line((0,y,280*S,y),fill=tuple(min(255,int(v*b)) for v in color)+(255,))
    fill.putalpha(mask.getchannel('A'));line(fill,[(25,11),(261,11)],tuple(min(255,int(v*1.25+25)) for v in color)+(180,))
    for x in range(38,260,23):line(fill,[(x,13),(x-5,20)],(255,255,255,18))
    if reverse:frame=frame.transpose(Image.Transpose.FLIP_LEFT_RIGHT);fill=fill.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    save(frame,name+'-frame');save(fill,name+'-fill')
bar('health',(212,55,32),True);bar('energy',(24,198,220))

im=canvas(74,80)
poly(im,[(37,1),(73,39),(37,79),(1,39)],(7,12,17,245),(75,91,98,255),2)
poly(im,[(37,8),(65,39),(37,71),(9,39)],(24,33,40,255),(129,145,146,220))
poly(im,[(37,17),(56,39),(37,60),(18,39)],(10,18,23,255),(45,61,69,255))
poly(im,[(26,32),(48,32),(51,51),(23,51)],(189,197,180,255),(28,36,39,255),2)
poly(im,[(25,32),(49,32),(46,40),(28,40)],(220,224,201,255),(63,74,73,255))
line(im,[(31,31),(31,26),(43,26),(43,31)],(204,217,202,255),2)
rect(im,(34,36,40,42),(14,34,39,255));rect(im,(36,37,38,40),(66,222,220,255))
line(im,[(27,46),(32,46)],(101,113,109,255));line(im,[(42,46),(47,46)],(101,113,109,255))
save(im,'bag-emblem')

def slot(locked=False):
    im=canvas(64,64)
    poly(im,[(1,8),(8,1),(56,1),(63,8),(63,56),(56,63),(8,63),(1,56)],(9,15,20,240),(76,91,99,255))
    poly(im,[(5,10),(10,5),(54,5),(59,10),(59,54),(54,59),(10,59),(5,54)],(21,36,43,245),(33,57,66,255))
    for x in range(8,58,7):line(im,[(x,7),(x,58)],(87,135,146,13))
    for y in range(8,58,7):line(im,[(7,y),(58,y)],(87,135,146,13))
    if locked:
        ImageDraw.Draw(im).arc((25*S,23*S,39*S,40*S),180,360,fill=(93,107,112,255),width=3*S)
        rect(im,(23,32,41,47),(69,82,88,255));rect(im,(31,37,33,42),(13,23,29,255))
    else:
        line(im,[(11,2),(54,2)],(235,173,71,255),2)
        poly(im,[(37,13),(49,15),(37,27),(47,29),(22,50),(27,35),(17,34)],(158,244,243,255))
        line(im,[(12,23),(27,23)],(20,143,177,255),2);line(im,[(10,31),(21,31)],(47,197,216,255),2);line(im,[(12,40),(20,40)],(20,143,177,255),2)
    return im
save(slot(),'dash-slot');save(slot(True),'locked-slot')

for gun in ['pistol','rifle','shotgun']:
    im=canvas(170,110)
    poly(im,[(3,67),(119,2),(166,43),(50,108)],(8,15,19,225),(86,105,111,255))
    poly(im,[(11,66),(119,9),(157,43),(49,100)],(21,33,37,150),(36,53,59,255))
    icon=canvas(124,54)
    if gun=='pistol':
        poly(icon,[(26,17),(99,17),(103,25),(67,28),(64,45),(47,45),(44,29),(26,27)],(124,142,120,255),(19,28,28,255),2)
        rect(icon,(31,18,96,23),(190,198,162,255));rect(icon,(48,31,60,42),(65,83,76,255));line(icon,[(70,31),(70,36),(65,36)],(178,171,130,255),2)
    elif gun=='rifle':
        poly(icon,[(8,25),(28,18),(35,23),(88,21),(91,27),(116,27),(116,31),(84,32),(72,34),(69,47),(59,46),(56,33),(45,32),(40,45),(31,45),(31,32),(8,35)],(105,125,91,255),(13,22,21,255),2)
        rect(icon,(40,19,83,26),(159,172,122,255));rect(icon,(51,13,66,18),(41,53,48,255));line(icon,[(44,20),(77,20)],(211,209,158,255))
    else:
        poly(icon,[(8,26),(33,22),(46,24),(99,23),(116,24),(117,29),(78,30),(72,35),(56,35),(52,45),(43,44),(44,34),(33,32),(10,36)],(158,125,83,255),(16,25,25,255),2)
        rect(icon,(45,21,115,25),(172,187,172,255));rect(icon,(74,29,95,33),(93,70,49,255));rect(icon,(53,23,68,29),(78,91,80,255))
    icon=icon.rotate(28,resample=Image.Resampling.BICUBIC,expand=True)
    im.alpha_composite(icon,((im.width-icon.width)//2,(im.height-icon.height)//2))
    line(im,[(52,103),(162,42)],(197,154,83,145),2)
    save(im,'weapon-'+gun)
print('Created HUD art in',OUT)
