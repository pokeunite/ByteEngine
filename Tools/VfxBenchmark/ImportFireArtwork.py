"""Import selected, locally downloaded Vefects artwork into a private benchmark.

Requires Pillow and numpy. Does not download, execute Unity code, or bundle art
with ByteEngine. Supply the publisher's unitypackage and destination project.
"""
import io
import sys
import tarfile
from pathlib import Path
import numpy as np
from PIL import Image

package, project = map(Path, sys.argv[1:3])
target = project / 'Assets' / 'Textures'
target.mkdir(parents=True, exist_ok=True)
selected = {
    'Lite_FB_3x3_RGBC_fireSwirl_01.tga': 'Charge.png',
    'Lite_T_VFX_Fireball_Projectile_01_Color.tga': 'Projectile.png',
    'Lite_FB_4x5_RGBC_fireBurst_03.tga': 'Impact.png',
}
# Read in archive order once; never extract archive paths to the filesystem.
with tarfile.open(package) as archive:
    members = {m.name: archive.extractfile(m).read() for m in archive
               if m.isfile() and m.name.endswith(('/pathname', '/asset'))}
for key, data in members.items():
    if not key.endswith('/pathname'):
        continue
    source = data.decode('utf-8').strip()
    name = Path(source).name
    if name not in selected:
        continue
    image = Image.open(io.BytesIO(members[key.rsplit('/', 1)[0] + '/asset'])).convert('RGBA')
    if 'RGBC' in name:
        masks = np.asarray(image, dtype=np.float32) / 255
        # Same nested channel interpretation as the source's unlit shader.
        color = np.full((*masks.shape[:2], 3), [0.24, 0.025, 0.012], dtype=np.float32)
        for channel, tint in [(2, [0.85, 0.07, 0.012]), (1, [1, 0.38, 0.025]), (0, [1, 0.91, 0.48])]:
            mask = masks[:, :, channel:channel+1]
            color = color * (1-mask) + np.array(tint) * mask
        rgba = np.concatenate((color, masks[:, :, 3:4]), axis=2)
        image = Image.fromarray(np.uint8(np.clip(rgba, 0, 1)*255))
    image.save(target / selected[name])
    print(f'{name} -> {selected[name]} ({image.width}x{image.height})')
(project / 'ARTWORK-SOURCE.md').write_text('''# Artwork provenance

Fire textures adapted from Vefects – Free URP Flipbook VFX Bundle Lite.
Downloaded from https://vefects.itch.io/free-urp-flipbook-vfx-bundle-lite
Original package: 2026_Vefects_URP_Flipbook_VFX_Bundle_Lite_6000_0_23.unitypackage.
ByteEngine effects, event sheets and scene setup are separate authored assets.
The publisher's textures are third-party artwork, not original ByteEngine art.
Keep this project private. Do not redistribute the raw textures as an engine
asset library. Confirm the publisher's applicable license before shipping.
''', encoding='utf-8')
