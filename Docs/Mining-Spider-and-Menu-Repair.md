# Spider and menu repair

The spider blueprints and their attack sub-behaviours referenced obsolete AI asset GUIDs. The saved project now uses the existing metadata GUIDs, preserving the authored trees. Native checks verify both roles move from all five distant spawn lanes.

Pause, audio, controls and graphics use the yellow/charcoal concept layout, with no game-name branding or slogan. Controls scroll using the wheel or scrollbar. Jumping is disabled on the player controller; dropping remains disabled on equipment. Defence shortcuts F1–F5 select sentry, rotary gun, mortar, mine and supply drop; these are remappable. B cycles defence selection, Alt exposes clickable defence icons, Tab rearranges camp, left mouse places, right mouse cancels.

The Windows player starts fullscreen by default. Continue is shown only for readable campaign progress. New exports receive a fresh runtime save identity; re-opening the same build retains that build's progress. Existing saves are preserved. A real Windows host test confirms fullscreen and no Continue on a fresh identity, without exporting a game.

Plugin version: 3.43.0. Current desktop editor, player runtime, project plugin package and source are synchronized. No game export was performed. Re-export with the updated editor to receive these changes in a standalone build.

The machine-screen images in `Concepts/AlienMining/MachineMenus-Yellow` are new design concepts, not implemented machine interfaces. Pause/settings are implemented. Full generation prompts and tool provenance are in that folder's `PROMPTS.json`.
