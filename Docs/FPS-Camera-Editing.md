# Adjusting the FPS template

Working project: `C:/Users/codex/Documents/fps template`.

The [Unity FPS sample camera settings](https://github.com/Unity-Technologies/FPSSample/blob/master/Assets/Scripts/Game/Systems/PlayerCamera/PlayerCameraSettings.cs) store camera position, rotation and FOV separately from [character presentation](https://github.com/Unity-Technologies/FPSSample/blob/master/Assets/Scripts/Game/Modules/Character/Components/CharacterPresentationSetup.cs). ByteEngine likewise keeps the eye and the camera-child arm alignment separate. No Unity assets or source were incorporated.

## Edit and preview

1. Stop Play and open `Assets/Blueprints/FPSController.byteblueprint`.
2. In the Blueprint toolbar, open **More (...) → Game Camera View**. This renders the authored game camera; inspector edits remain available.
3. Select the player root and open its **First Person Camera → FPS framing** section. **Eye position** changes the camera attachment and **View FOV** changes the viewing width. The template uses `(0, 1.65, 0)` and 80° FOV.
4. Adjust **Arms position** in that same section; there is no need to select the Model child. Current offset: `(0, −.27, −.10)`. The controls edit the referenced camera and its direct child named Model, support undo, and are disabled during Play so temporary runtime values cannot be mistaken for saved framing.
5. Save the Blueprint, then save the scene. Blueprint saves propagate to the open scene while preserving instance overrides; inspect/update overrides if a placed player differs.

Arm axes: X moves sideways; Y up/down; increasing Z toward zero brings the arms closer and crops more of the upper arms. Decreasing Z moves them farther away and reveals more of the full model. Keep scale at 1. Adjust in small increments while watching Game Camera View. Arms remain camera children so the screen composition follows look.

The previous placement showed shoulder ends. Native previews were used to move the arms closer, then tune vertical placement so hands and forearms remain visible with shoulder ends below the frame. Camera height/FOV and mouse look were retained. The Blueprint, placed scene and instance baseline all use the new arm placement. Backups: `.byteengine/backups/fps-arm-framing-20261010-011414`.

This is a hold pose awaiting a weapon. Weapon art/animation is a separate authoring step; clipping upper arms out of frame does not create a weapon or animation.
