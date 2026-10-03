# Goblin Scraper: manual project controls

The game now uses an editable native Event Sheet at `Assets/Events/GoblinControls.byteevents`, attached to the builder object through an EventModuleComponent. Select that object and open its Events workspace to change the rules. Plugin version: 0.3.0.

## Controls supplied

- W / S: forward / reverse. A / D: steering input.
- B: switch build / simulation. Space: start simulation in build mode; brake while driving.
- Shift: drift. F: fire or run weapons. G: toggle mechanisms.
- Enter: place preview. R: rotate preview (reset position while driving). F: flip preview in build mode. Tab: next connector.
- Delete: delete selected block. X: delete hovered block. C: copy hovered part. M: move hovered branch. Escape: cancel.
- Left Ctrl + Z / Y: undo / redo. F5: save machine. F9: load machine in build mode.
- The existing mouse palette, placement and orbit camera remain enabled.

## Editing the behavior

Actions and conditions appear under **Goblin / Builder**. The default target is `Self`, so attach the sheet to the object containing VehicleBuilder3D. Other targets accept an object name or `id:<object-guid>`.

The supplied sheet disables built-in keyboard/driving controls to avoid doubled input. Its Always rule resets drive inputs and held weapons first; subsequent rules add throttle, steering, brake, drift and weapon input for this frame. Keep that ordering when editing. Throttle and steering accept -1 to 1. Positive steering is right. Physics stays inside the plugin; events control its inputs and commands.

The component exposes four persisted switches: UseBuiltInControls, UseBuiltInPointerControls, AutomaticCamera and ShowWorkshopHud. Disable pointer controls, camera or HUD when replacing those systems with your own events/UI. Removing the sheet requires re-enabling UseBuiltInControls if you want the original keyboard controls.

Build actions include selecting a part by catalogue model filename, placing or rotating the preview, connector selection, erase, delete, copy, move, cancel, undo, redo, save and load. Place Part at Connector accepts a model filename, parent block ID, parent connector, optional own connector and twist in degrees. The master block is ID 0; connector names come from the part catalogue. Invalid placements are rejected. Mode and readiness conditions let your rules guard these operations.

## Saved copies / resume

`PluginLibrary` in the engine repository and `C:\Users\codex\Downloads\ByteEngine Plugins` contain the Goblin 0.3.0 and Space Scraper 0.1.0 packages, this guide and the editable sheet. Space Scraper is a starter/example plugin. Source lives under `Plugins` in the engine repository.

To import later, use Project Settings > Plugins and import a `.byteplugin` package, then reopen the project. The current Goblin game already has its package and sheet installed. Its scene before this change is backed up under `Backups/before-manual-events-030`. The event sheet is portable; meshes, textures and the part catalogue remain project assets and are not bundled in the code plugin.
