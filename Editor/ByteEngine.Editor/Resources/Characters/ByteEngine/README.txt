ByteEngine animated mannequin starting assets

Mannequin.glb: full body, 146 animation clips/takes, weighted humanoid skeleton.
Mannequin-FPS-Arms.glb: arms only, 103 clips/takes, fixed-root arm skeleton.
These are copies of the All-Available GLBs, renamed to the standard engine
asset names. Both retain the original mannequin mesh, materials and T-pose bind.

UAL_*: Quaternius in-place clips. UAL_RM_*: full-body root-motion variants.
PSX_* and Drillimpact_*: arm/hand actions. Rokoko_*: 15 gun-handling takes,
approximately 11–57 seconds long. Their full sequences are preserved; cut or
blend them into gameplay states as needed. All keys are baked at 30 FPS.

Full-body motion on FPS contains arm extraction with fixed root. FPS-source
motion on the full body leaves the legs in a standing rest pose and can be
used in an upper-body layer. Reference pose clips are also included.
No weapons or magazine/bolt animation tracks are included. Position weapons,
align sights, set movement blending and add animation events in your game.

New starter projects receive these models, inventory and source/license notices
in Assets/Characters/ByteEngine (or their configured asset root). The models
are available in the asset browser, ready to add to scenes. Existing project
copies and edits are preserved. Original Blender models are unchanged.

Keep ANIMATION-CREDITS.txt, both CC license notices and ROKOKO-SOURCE-NOTICE.txt
with redistributed projects. Different source contributions have different
permissions; the entire combined GLB is not blanket CC0 or CC BY.
See animations.json for the complete clip inventory and sources.
