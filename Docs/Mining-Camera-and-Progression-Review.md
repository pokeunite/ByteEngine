# Third-person camera and business progression review

## Evidence and camera reference

Reviewed the two recordings dated 2026-10-25, 21:23 and 21:26. The authored Camera Boom still had **Invert Vertical Look** enabled after migration from the previous controller. The procedural aim rotated an imported spine about bone-local axes. These are separate camera-input and character-pose problems.

Reference reviewed: [GDQuest's MIT-licensed third-person controller](https://github.com/gdquest-demos/godot-4-3d-third-person-controller), pinned at `b3bd6e81084f568be8aa44a69a0c2b1e52e806b3`. In particular, `player/camera_controller.gd` separates the control pivot, spring arm, shoulder framing, camera ray target and character. Its Godot code is not a drop-in ByteEngine component. We retained ByteEngine's native collision-aware boom and applied this separation; no reference art was imported.

Palclone now uses conventional vertical look, pitch limits -35 to 55 degrees, a 2.8 m normal arm and a 2.1 m aimed arm. RMB tightens the view. The Animation Profile's Procedural section offers **Aim around model axes (imported rigs)**, enabled for Synty. This converts the model-space rotation back through the current posed parent rather than assuming the spine's local X axis is horizontal.

The weapon and reticle share `GetThirdPersonAimTarget`. A centre-camera ray finds the aiming point, a muzzle ray checks intervening cover, and shots converge from the muzzle to that point. The reticle projects that same point through the rendered camera; orange indicates an obstruction between barrel and intended target. Spread, recoil and projectile travel still apply. A reticle is an aim point, not a promise that every inaccurate shot hits it.

## What was found in the installed demo

Read-only inspection of the locally installed Unity managed assembly confirmed these structural mechanisms:

| System | Observed structure | Original adaptation for our game |
|---|---|---|
| Permanent research | Prerequisites, multiple ranks, per-rank prices, flat/percentage/flag effects; several currencies | Keep a smaller company tree, add functional milestones and preserve stable save IDs |
| Ammo press | Separate authored ladders for cycle rate, hopper size and rounds per stroke; different ammunition families | Independent cycle speed, hopper and payload upgrades |
| Inventory | Limited ammunition shelves; capability unlocks and capacity improvements | Make press production matter by removing unconditional wave refills |
| Progression | Weapon/defence unlocks alongside statistical improvements | Establish weapon and automation milestones before expanding content |
| Market | Dedicated market, storage and sale systems, with upgrade modifiers | Keep our existing simulated commodity market and atomic sale quote |

This is an architectural comparison, not a claim to have recovered the demo's exact authored balance tables or all runtime behaviour. Decompiled material stays in ignored inspection artifacts. No demo code or assets were copied into our implementation.

## Playable upgrade changes

The existing IDs, prices, prerequisites and purchased ownership remain. These nodes now also provide:

| Node | Actual effect |
|---|---|
| Company charter | Three extra rounds per magazine, plus its existing damage bonus |
| Quick-change feed | Reload duration divided by 1.35, plus its existing damage bonus |
| Extended feed | Six extra rounds per magazine, plus its existing damage bonus |
| Auto refinery | Starts the next available batch between waves; retains the yield bonus |
| Dash capacitor | 40% shorter dash cooldown, plus 25 energy capacity |
| Supply conveyor | Delivers produced ammo to the equipped weapon between waves; retains production bonus |

Existing owners receive their node's added capability. Changes use captured base values so repeated purchases/application do not compound accidentally. Magazine upgrades increase capacity; they do not create free ammunition.

The press's payload upgrade in this scene increases output by a whole additional base payload per level (1x, 2x, 3x...). Speed and hopper remain independent. Current/next speed previews include permanent production bonuses. `FreeAmmoEveryOperation` is exposed on Mining Operation, defaults off, and can be enabled deliberately for another game mode. Starting ammunition remains authored on the weapon. The press supplies subsequent rounds.

Automation and all numeric bonuses are authored in the workshop's `ResearchDefinitionsJson`. Existing Event Sheet purchase actions still execute the same research and machine APIs. No new hidden input shortcuts are used.

## Verification

Run weapon/event regression tests and the native `--verify-third-person` and `--verify-ui` checks from Tools/MiningPrototype. The camera verification includes normal mouse-up direction and projection of aim points through the actual render-view matrix at -35, 0, 35 and 55 degrees, as well as muzzle-converged hitscan/projectiles, camera collision and modal input ownership. Capture normal, `--look-up`, and `--look-down` views for visual inspection. Test tools disable progression saving; authored updates create a scene/profile backup.

These changes establish a more meaningful first progression pass. Longer-term economy pacing and enemy pressure still require player testing rather than copying the demo's prices.
