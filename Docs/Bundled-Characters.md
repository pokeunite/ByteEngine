# Bundled mannequin characters

Every newly created project receives these independent, editable assets:

- `Assets/Characters/ByteEngine/Mannequin.glb`: full-body gray mannequin with T-pose bind, 49 deform joints, 84,160 triangles and 146 clips/takes.
- `Assets/Characters/ByteEngine/Mannequin-FPS-Arms.glb`: matching arms with T-pose bind, 36 deform joints, 36,096 triangles and 103 clips/takes.
- Usage notes, animation inventory, attribution, CC0/CC BY license notices and a separate Rokoko source notice.

Each GLB contains two material sections and normalized skin weights. Preview cameras, lights, reference images, and Blender IK controls are excluded. The editable Blender originals and animated exports are under `C:/Users/codex/Documents/MannequinCharacter`.

Animations are retargeted from Quaternius Universal Animation Library Standard (43 clips, CC0), heyheythere PSX FPS Arms Free (27, CC BY 4.0), and Drillimpact PSX First Person Arms (17 actions plus rest, CC0). The full body includes Quaternius in-place and root-motion variants. FPS includes fixed-root arm extractions. FPS-source clips on the body animate arms with a standing lower-body rest, suitable for upper-body layering. No weapons or weapon mechanism tracks are included. Keep `ANIMATION-CREDITS.txt` and the license notices in distributed projects. See [animation sources](Mannequin-Animation-Sources.md) and the bundle's `animations.json` for details.

At the user's request, the standard engine files now contain the complete All-Available exports, including fifteen Rokoko gun-handling captures. Their author resource page permits personal and commercial animation/game/3D project use. These are embedded as retargeted mannequin example-project assets; the raw source FBX archive is not bundled. `ROKOKO-SOURCE-NOTICE.txt` preserves the author grant, source and terms, and explicitly avoids representing these contributions as CC0/CC BY or as a general standalone-animation redistribution license. The takes run approximately 11–57 seconds and have not been cut into gameplay states. Sketchfab sources remain pending account-access downloads.

`Editor/ByteEngine.Editor/Resources/Characters/ByteEngine` is the canonical engine bundle. The editor project copies it into build and publish output. `BundledCharacterInstaller` installs the resources before the first asset-database scan in `EditorProjectContext.Create`; packaged Last Stand projects use the same installer. Custom project asset directories are respected. Installation validates the source bundle and does not overwrite existing project files or asset metadata.

Existing projects are unchanged when opened. New Clean, 3D Starter, ByteArena, Interactive Sand, and Last Stand projects all receive the models; they are available in the asset browser without being placed in the startup scene.

Regression checks: build `Tests/ByteEngine.Tests` and run it with `--bundled-characters`. The checks cover creation, engine GLB import, all 146/103 clips including fifteen Rokoko takes per model, material sections, skeletons, normalized weights and quaternion keys, source/license copying, packaged starters, missing resources, and preservation of user edits.
