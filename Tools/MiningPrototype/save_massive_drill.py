import bpy,shutil,json,uuid
from pathlib import Path
base=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine');out=base/'artifacts/reference-drill';project=Path('C:/Users/codex/Documents/palclone')
source=project/'SourceArt/MiningMachines/MiningDrill-Massive.blend';asset=project/'Assets/Models/Mining/MiningDrill-Massive.glb'
source.parent.mkdir(parents=True,exist_ok=True);asset.parent.mkdir(parents=True,exist_ok=True)
scene=bpy.data.scenes['Drill Reference'];bpy.context.window.scene=scene;scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT');rig=bpy.data.objects['MiningDrillRig'];rig.select_set(True);bpy.context.view_layer.objects.active=rig
# Keep the live Blender session editing the delivered file rather than a work copy.
bpy.ops.wm.save_as_mainfile(filepath=str(source))
shutil.copy2(out/'MiningDrill.glb',asset)
meta=Path(str(asset)+'.meta')
if not meta.exists():meta.write_text(json.dumps({'guid':str(uuid.uuid4()),'type':'Model3D'},indent=2))
shutil.copy2(out/'MiningDrill-preview.png0001.png',source.with_name('MiningDrill-Massive-preview.png'))
shutil.copy2(base/'Docs/Concepts/AlienMining/MiningDrill-approved.png',source.with_name('MiningDrill-Reference.png'))
source.with_suffix('.md').write_text('''# Massive mining drill

Open the Drill Reference scene in the blend file. The refinery and press scenes are preserved separately.

User revision: enlarged rig, much larger collection hopper and a long thick auger.

- Shaft: 12.52 m long, 1.0 m diameter.
- Helical flight: 11.70 m long, 2.90 m outside diameter, about four turns.
- Machinery: approximately 9 m tall above ground, with broad stabilizers and enlarged hydraulic supports.
- Hopper mouth: 4.60 x 3.80 m, with the chute aligned and extending inside the mouth.
- Operate animation: 3-second loop; rotating auger and motor fan, hydraulic carriage and ore gate.
- Runtime export: about 13,500 triangles, five bones, eight material batches. Presentation ground is excluded.

The GLB is available as Assets/Models/Mining/MiningDrill-Massive.glb. It is a separate model asset, allowing scene assignment and scale to be chosen without replacing the earlier drill automatically. The source retains individually editable mesh parts. The single concept view determines the visible design; rear construction and fine surface wear are simplified.
''',encoding='utf8')
result={'blend':str(source),'model_asset':str(asset),'active_scene':scene.name,'auger_length_m':12.52,'auger_outer_diameter_m':2.90,'hopper_opening_m':[4.60,3.80]}
