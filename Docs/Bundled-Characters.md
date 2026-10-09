# Bundled mannequin characters

Every newly created project receives these independent, editable assets:

- `Assets/Characters/ByteEngine/Mannequin.glb`: full-body gray mannequin in T-pose, 49 deform joints, 84,160 triangles.
- `Assets/Characters/ByteEngine/Mannequin-FPS-Arms.glb`: matching arms in T-pose, 36 deform joints, 36,096 triangles, and the `FPS_Ready_Pose` animation.
- `Assets/Characters/ByteEngine/README.txt`: usage notes.

Each GLB contains two material sections and normalized skin weights. Preview cameras, lights, reference images, and Blender IK controls are excluded. The editable Blender originals and previews are included under `MannequinCharacter/` in this repository.

`Editor/ByteEngine.Editor/Resources/Characters/ByteEngine` is the canonical engine bundle. The editor project copies it into build and publish output. `BundledCharacterInstaller` installs the resources before the first asset-database scan in `EditorProjectContext.Create`; packaged Last Stand projects use the same installer. Custom project asset directories are respected. Installation validates the source bundle and does not overwrite existing project files or asset metadata.

Existing projects are unchanged when opened. New Clean, 3D Starter, ByteArena, Interactive Sand, and Last Stand projects all receive the models; they are available in the asset browser without being placed in the startup scene.

Regression checks: build `Tests/ByteEngine.Tests` and run it with `--bundled-characters`. The checks cover creation, engine GLB import, material sections, skeletons, normalized weights, FPS animation, packaged starters, missing resources, and preservation of user edits.
