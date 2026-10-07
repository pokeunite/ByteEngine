# Goblin Scraper: manual editor control (0.4.2)

Reopen the project and Scenes/Main.bytescene to load the new saved scene. If an older blank scene is still open, do not save that old scene over this one. The installed plugin is 0.4.2.

## Working in the scene

The hierarchy now contains real, saved objects before Play:

- WORKSHOP - editable environment: ground, workbenches, banners and props.
- BATTLEFIELD - editable level: obstacles, cover, boundaries and course markers.
- GOBLINS - duplicate or move these spawns: 18 saved red goblin models. Move them to place enemies. Duplicate a spawn to add more; keep its GoblinEnemySpawn boolean variable true. The builder uses active spawns in name order, limited by BattlefieldEnemyCount (maximum 24). Give duplicates unique names.
- Toolbar, part palette and HUD objects: select their UiWidget/UiText components to edit position, size, colours and text. The tuning popup is saved but inactive; enable it temporarily to inspect its layout, then disable it again.
- Starting block - editor preview: visible in Edit mode; live construction replaces this preview in Play.

Move and scale the native level objects and save the scene normally. Solid obstacles and the ground need a non-trigger BoxCollider3D; its dimensions and the object's rotation/scale are used by vehicle physics. Adding unsupported collider types will not create vehicle obstacles.

Concrete and wood now use saved .bmat materials. Edit those material assets normally; their texture references persist after reload.

Keep GoblinAuthoringKey variables unchanged: they bind gameplay to the saved HUD and scene objects. Reparenting and changing layouts are supported. Renaming a toolbar button also requires changing its target name in the UI event sheet.

## Editable gameplay controls

Select the builder object and its VehicleBuilder3D component. Inspector settings include BattleDuration, EnemyMoveSpeed, EnemyAttackDamage, EnemyAttackInterval, BattlefieldEnemyCount and the existing construction/tuning options. Keep UseAuthoredScene enabled and UseBuiltInToolbarActions disabled for this authored scene.

Assets/Events/GoblinControls.byteevents contains your existing keyboard controls.
Assets/Events/GoblinWorkshopUI.byteevents contains the 13 toolbar button actions: deploy/build, undo, redo, save, load, place, move, rotate, change face, copy, erase, recover and tune. Both are attached through the builder's EventModuleComponent. Open the native event editor to change the rules or actions.

The parts palette, socket placement, tuning sliders, battle counters, vehicle physics, live contraption, shots and ragdolls still have plugin behavior. This is an editable authored level and UI, not a conversion of the entire simulation into event sheets. Replacing the goblin mesh/rig also requires matching animation and ragdoll assets.

## Steering and blocks

Steering block and steering hinge are available in the land palette again. Hinge input compensates for an inverted turning axis. The block's Reverse direction tuning option can still deliberately reverse a particular hinge.

## Backups and plugin source

Old runtime-only scene and 0.4.1 package: project Backups/authored-scene-042.
Editable source: C:/Users/codex/ByteEngine/Plugins/GoblinScrapper.
Reusable package: C:/Users/codex/ByteEngine/PluginLibrary/GoblinScrapper.byteplugin and Downloads/ByteEngine Plugins.

Validated: native edit-mode render; layout edit/save/reload; no duplicate HUD in Play; editable toolbar events; driving on saved collider ground; enemy spawn reset; steering forward/reverse/counter-steering with normal and inverted axes. Build: zero errors or warnings.
