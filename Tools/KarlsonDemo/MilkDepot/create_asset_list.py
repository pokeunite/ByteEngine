from pathlib import Path
from collections import defaultdict
import json, shutil
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, Image, PageBreak, KeepTogether
from reportlab.lib import colors
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from pypdf import PdfReader

ROOT=Path('C:/Users/codex/Downloads/byte engine files/Milk Depot Environment')
DOCS=Path('C:/Users/codex/Documents/karlson demo/Docs/Level Concepts')
data=json.loads((ROOT/'asset_catalog.json').read_text())
styles=getSampleStyleSheet()
styles.add(ParagraphStyle(name='TinyAsset',fontName='Helvetica',fontSize=8,leading=10,textColor=colors.HexColor('#344452')))
styles.add(ParagraphStyle(name='AssetName',fontName='Helvetica-Bold',fontSize=8.3,leading=10,textColor=colors.HexColor('#172c39')))
styles['Title'].textColor=colors.HexColor('#172c39');styles['Title'].fontSize=27
styles['Heading1'].textColor=colors.HexColor('#cc521b');styles['Heading1'].spaceAfter=12
styles['BodyText'].leading=14;styles['BodyText'].fontSize=10
story=[]
def para(t,style='BodyText'):return Paragraph(t,styles[style])
def image(path,w):
    from PIL import Image as PIL
    with PIL.open(path) as im:ratio=im.height/im.width
    return Image(str(path),width=w,height=w*ratio)
def footer(c,doc):
    c.setStrokeColor(colors.HexColor('#d8dfe2'));c.line(36,36,A4[0]-36,36)
    c.setFont('Helvetica',8);c.setFillColor(colors.HexColor('#52636e'))
    c.drawString(36,23,'MILK DEPOT  |  Modular environment asset inventory  |  Revision 3')
    c.drawRightString(A4[0]-36,23,str(doc.page))

story.extend([para('MILK DEPOT','Title'),para('Environment object list and Blender asset inventory','Heading1')])
story.append(para(f"{data['unique_assets']} unique modeled assets | {data['instances']} placed instances | {data['shared_materials']} shared materials. Built around the generated Karlson demo level concept."))
story.append(Spacer(1,14));story.append(image(ROOT/'Previews/MilkDepot_Courtyard.png',A4[0]-72))
story.append(Spacer(1,12));story.append(para('Scope and deliverables','Heading2'))
story.append(para('Editable Blender source contains the asset library and an assembled reference layout. Individual GLB models and the assembled GLB are staged in the project Assets/Environment/MilkDepot folder. The current playable gun-lab scene has not been replaced.'))
story.append(para('This inventory separates visual models from gameplay authoring. Pickups, doors, checkpoints, illumination and enemies require engine components. The mechanical training dummy is a static target model, not a rigged or animated enemy.'))
story.append(PageBreak())
groups=defaultdict(list)
for a in data['assets']:groups[a['category']].append(a)
for category,assets in groups.items():
    category_heading=para(category,'Heading1')
    rows=[[para('Asset','AssetName'),para('Size / triangles','AssetName'),para('Purpose / construction','AssetName')]]
    for a in assets:
        size=' x '.join(f'{x:g}' for x in a['dimensions_m'])+' m'
        rows.append([para(a['id'].replace('_',' '),'AssetName'),para(f"{size}<br/>{a['triangles']:,} triangles<br/>{a['placements']} placements",'TinyAsset'),para(a['description']+'<br/><b>Collision:</b> '+a['collision'],'TinyAsset')])
    table=Table(rows,colWidths=[125,110,A4[0]-72-235],repeatRows=1,hAlign='LEFT')
    table.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),colors.HexColor('#e5ebed')),('VALIGN',(0,0),(-1,-1),'TOP'),
       ('LEFTPADDING',(0,0),(-1,-1),8),('RIGHTPADDING',(0,0),(-1,-1),8),('TOPPADDING',(0,0),(-1,-1),8),('BOTTOMPADDING',(0,0),(-1,-1),8),
       ('LINEBELOW',(0,0),(-1,0),.7,colors.HexColor('#cc521b')),('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.white,colors.HexColor('#f6f8f9')])]))
    story.append(KeepTogether([category_heading,table,Spacer(1,18)]))
story.append(PageBreak());story.append(para('Assembly and integration notes','Heading1'))
story.append(image(ROOT/'Previews/MilkDepot_Overview.png',A4[0]-72));story.append(Spacer(1,10))
for t in ['Route: safe spawn and armory, jump training, courtyard, close-range corridor, final arena and visible milk exit. An elevated optional route reconnects to the final arena.',
          'The source uses metres and Z-up. GLB exports use the standard Y-up convention. Walking surfaces and architectural pieces share floor-level pivots. Side-mounted props preserve their mounting origin.',
          'Reuse the existing pistol, shotgun and rifle assets. The weapon rack has mounting hooks; the first-person gun models and their reload animation profiles remain in the project.',
          'Use solid box collision for floor foundations and cover. Use compound boxes around door and window openings. Stair collision should use stepped boxes or a controller-compatible ramp; do not use the handrails as the walking collider.',
          'The assembled GLB is a visual reference. Build the playable engine scene from modular pieces so collisions, culling, lighting, pickups, checkpoints and target behavior can be authored and verified separately.',
          'Existing named asset files are static and unanimated. Any moving door or enemy animation is a separate gameplay and rigging task.']:
    story.append(para(t));story.append(Spacer(1,8))
story.append(PageBreak());story.append(para('Close-up asset review','Heading1'))
hero=ROOT/'Previews/MilkDepot_HeroCrate.png'
if hero.exists():story.append(image(hero,A4[0]-120))
story.append(para('Cargo crate: protective frame, recessed cargo panels, carry handles, locking latches, hinges, serial plate, barcode and corner feet. Shared portable PBR maps include base colour, roughness and normal detail.'))
pdf=DOCS/'Milk Depot Asset List.pdf'
SimpleDocTemplate(str(pdf),pagesize=A4,rightMargin=36,leftMargin=36,topMargin=36,bottomMargin=48).build(story,onFirstPage=footer,onLaterPages=footer)
reader=PdfReader(str(pdf));assert all(a['id'].replace('_',' ') in '\n'.join(page.extract_text() for page in reader.pages) for a in data['assets'])
shutil.copy2(pdf,ROOT/pdf.name)
txt=['MILK DEPOT - COMPLETE OBJECT LIST, REVISION 3','',f"{data['unique_assets']} unique models; {data['instances']} assembled placements.",'']
for cat,items in groups.items():
    txt += [cat.upper()]
    for a in items:txt += [f"- {a['id']}: {a['description']} ({a['triangles']:,} triangles, {a['placements']} placements)"]
    txt += ['']
txt += ['Existing animated pistol, shotgun and rifle assets are reused, not replaced.','Training dummy is static. Door, pickup, checkpoint and enemy gameplay remain engine-authored.','Playable scene unchanged. Individual GLBs are staged under Assets/Environment/MilkDepot/Models.']
(DOCS/'Milk Depot Asset List.txt').write_text('\n'.join(txt),encoding='utf-8');(ROOT/'Milk Depot Asset List.txt').write_text('\n'.join(txt),encoding='utf-8')
print('PDF PASS',len(reader.pages),'pages,',len(data['assets']),'listed assets')
