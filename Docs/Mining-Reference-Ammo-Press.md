# Animated ammo press

The approved yellow industrial press replaces only `AmmoPress.glb` in palclone. Its existing asset GUID is retained. The other machines, weapon tuning and progression saves are preserved.

## Editable source

`C:/Users/codex/Documents/palclone/SourceArt/MiningMachines/AmmoPress.blend`

The Reference Ammo Press collection contains separately editable mechanical parts and AmmoPressRig. The Operate clip is three seconds at 30 fps. The ram strokes three times; the flywheel turns; both end rollers turn; 28 slats circulate around the conveyor. The game export merges the parts into six material batches, retaining rigid bone weights (4,950 triangles, 36 bones).

## Production and collection

The Ammo press station has Mining Machine Animation and Mining Ammo Bucket components. Production runs until stored ammunition reaches capacity; collection frees capacity and resumes production. Rounds travel across the belt, drop into the tray, and become a layered pile. Walking up to the tray transfers ammunition to weapon reserve, subject to the existing reserve limit.

The stock value remains authoritative. Visual rounds represent multiple actual rounds, with representation adjusted for storage upgrades. They do not introduce a second inventory, additional ammunition, rigidbody simulation or per-round scene objects. The pile uses two bounded dynamic mesh batches; no more than 72 rounds are visible with the installed settings.

## Exposed controls

- Model anchors: BeltStart, BeltEnd and BucketFloor. These are imported static marker bones, so the path follows the machine's scene transform. Existing offset fields remain fallback settings for older models.
- Conveyor speed, drop duration, maximum travelling rounds.
- Pile footprint, height, visible round budget, rounds per visual, round size and brass/copper colours.
- Pickup radius and automatic walk-up collection.
- Operate clip and playback speed on Mining Machine Animation. The installed conveyor speed matches the authored belt speed at playback speed 1.
- Refinery Volume Multiplier on Mining Audio: increased from an implicit 0.8 to an exposed 3.0, using the existing Machine Volume of 0.18 (0.54 source gain). Spatial distance attenuation still applies.

## Verification

The native press check loads the actual GLB and clip, verifies marker positions, ram movement, slat translation, wheel rotation, delivery, pile bounds, capacity pause/resume, exact walk-up collection and native refinery loop playback. Tests disable persistence and leave real progress intact.

Model construction is reproducible through build_reference_press.py, followed by refine_reference_press.py. fix_press_loop.py updates the earlier live Blender scene with unwrapped slat rotations and seamless roller turns. InstallReferencePress.ps1 backs up the previous press asset/source; MiningPressAuthor backs up the scene before changing component settings. No game export is performed.

The visible reference is closely recreated; unseen rear construction and exact dimensions are inferred from the single concept image.
