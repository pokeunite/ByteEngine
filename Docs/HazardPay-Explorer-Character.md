# Hazard Pay explorer character

The original yellow/charcoal explorer is saved in the game project, outside the engine repository:

- `Assets/Characters/HazardPayExplorer.glb`: import-ready asset with the supplied source skeleton and animations.
- `SourceArt/HazardPayExplorer/HazardPayExplorer.blend`: editable rest-pose source.
- `SourceArt/HazardPayExplorer/HazardPayExplorer-AnimationPreview.blend`: idle animation selected; press Play in Blender.
- `SourceArt/HazardPayExplorer/animation-validation.json`: weight, rest-pose and sampled-pose checks.
- `SourceArt/HazardPayExplorer/export-validation.json`: exact animation and inverse-bind verification.

The character uses 51 source bones and all 76 source animation tracks. The supplied SH/DH bone names, hierarchy, rest transforms and animation samples are preserved. Geometry is newly authored and manually weighted; it is not the supplied Synty character mesh. Source animation licensing still applies. The existing player scene has not been replaced.

Choose clips through the rig's Action editor/NLA tracks. The original import is preserved in its own hidden reference collection. Do not apply transforms to the armature or change its rest pose to resize the character; scale the instance instead. Weapon sockets and SH/DH profiles can use the same bone names, but visual hand alignment should be checked on the new armor.

`PreserveExplorerAnimation.py` combines the Blender-authored mesh with the original animation and bind data. It refuses to overwrite the imported asset. Optional Blender material extensions are removed for the engine's core PBR import path. It does not copy the original mesh into the delivered asset.

Validation: no unweighted vertices or unknown bone groups; 16 key clips sampled at three points; native ByteEngine import confirms 51 bones, 76 clips and 10 mesh parts. These checks establish import/rig compatibility, not a complete in-game combat playtest.
