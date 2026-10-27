# Fitted mining spiders

Both mining enemy blueprints now use the authored eight-legged models, fitted to Mesh2Motion's 56-bone spider rig in Blender. The original automatic website binding was rejected because its body height and joint positions did not match these models.

## Files

- Editable rig and clips: `C:/Users/codex/Documents/palclone/SourceArt/SpiderModels/SpiderRigged.blend`.
- Original editable models remain in `SpiderKit.blend` beside it.
- Runtime assets: `Assets/Models/Mining/Spiders/PlayerHunter.glb` and `DrillBreaker.glb`.
- Blueprints: `Assets/Blueprints/Mining/Alien Hunter.byteblueprint` and `Alien Raider.byteblueprint`. Existing AI, health, navigation and role selection remain intact.

## What was corrected

The template body was near the floor while our spider bodies are raised. All eight leg chains were fitted to their authored root, coxa, knee, ankle and foot points. Automatic distance weights were replaced: a leg can only be influenced by its own chain, while the shell, armour, eyes and mouth remain rigid. The animation transfer corrects bone axes and preserves alternating foot contacts despite the different proportions.

The source skeleton and five base clips are Mesh2Motion CC0 assets. The user's default-spider Walk export confirmed the same rig hierarchy and bind positions. Credits are included alongside the Blender source.

## Editing in the engine

Open either blueprint and select **Animated Mining Spider**. Model, scale, clip prefix and each individual clip are editable. `Walk speed at one`, minimum/maximum playback speeds, blend time and animation update interval control presentation without changing AI movement or damage.

- **Idle**: stationary.
- **Walk**: actual horizontal movement, with speed adjusted to movement.
- **Jump**: actual Leap/Lunge combat phases, or the event action below. This does not add a new jumping attack to AI that has none.
- **Attack**: committed strikes or actual attack fire. Wind-up does not restart it every frame.
- **Death**: plays once on a detached visual, so health destruction and final-wave cleanup cannot truncate it. Corpses have an editable lifetime and a bounded count (12 by default), with no gameplay collider.

In Event Sheets, search **Animated Mining Spider** for **Play spider animation**, **Spider model and rig are ready**, and **Spider animation state is**. Turn off `Automatic playback` if an Event Sheet should own non-death animation selection completely. Damage and navigation remain controlled by the existing AI behaviour assets.

## Verification

`MiningPrototype --verify-spiders` loads both actual blueprints, imports all five clips, checks live walk/idle switching, non-restarting jump/attack playback, detached death playback and timed corpse removal. `--verify-ambush` checks the actual 50-spider release, navigation, combat and recovery cleanup. Blender geometry checks sample 65 poses per model, verify normalized weights, finite bounded geometry and rigid-shell/shaft length preservation; short flexible joint membranes bend between segments.

No game build was exported. Blueprint and plugin backups are under the project's `.byteengine/Backups` directory.
