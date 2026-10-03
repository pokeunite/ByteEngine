# Wheel placement, traction and steering — 0.2.5

## Fixes

The renderer’s physical bone override previously reconstructed the rest joint from the inverse-bind matrix. That matrix may include a mesh-node offset and is not necessarily the joint’s authored model transform. Physical overrides now apply to the actual rest joint global transform. A neutral override preserves the same rendered wheel and hinge bounds as the Build pose.

Standard Build placement now uses authored connector poses directly. It no longer applies an inverse-bind/current-bone delta to an idle hinge’s attached children. Physics continues to attach those children to the actual moving output in Play.

Steering input direction is corrected: D steers both upright hinges right relative to the machine; A steers left. Limits and return behavior are unchanged.

WheelU5’s source friction combine rule is Multiply. Tire/ground contacts now multiply the 0.6 tire coefficient by the garage ground coefficient 1.5, producing 0.9 rather than selecting 0.6. Contact normals and motor forces continue to provide actual propulsion. No vehicle translation is injected.

## Verification

- Native render test: identity physical overrides preserve model bounds within 0.1 mm for small/large powered/free wheels and the steering hinge.
- Native game loop: model imports, mouse placement, delete/undo, single wheel, beam frame, hinge frame, both recorded builds, and return to Build pass.
- Steering regression verifies D points both hinge outputs toward the machine’s right under load.
- Small/large wheels with front, rear or all-powered layouts pass forward/reverse, brake, sleep/restart, and no automatic A/D wheel steering at 30/60/120 FPS.
- Traction regression requires four-powered-wheel builds to reach at least 85% of nominal rolling speed after four seconds. Measured small wheels: vehicle 5.07 m/s vs tire 5.07 m/s. Large wheels: vehicle 7.00 m/s vs tire 7.00 m/s, at all tested frame rates.
- Existing gear, flight, projectile, damage-detachment, save/load, deletion, and finite simulation checks for all included parts pass.

Logs: `output/video-wheel/025-handling.log`, `025-traction-check.log`, `bind-pose-fix-native.log`. Native screenshots: `output/goblin-scraper/standard-contraptions`.

## Installed files

Project plugin is 0.2.5 at `C:/Users/codex/Documents/Goblin Scraper/Plugins/bytebard.goblinscrapper.byteplugin`. Its previous package is in that project’s `Backups/before-pose-traction-025`.

The renderer fix requires the updated engine DLL as well as the plugin. Matching editor builds have been published to `Dist/ByteEngine-BuildDebug`, `Dist/ByteEngine`, and `Dist/ByteEngine-Refined`. Close and reopen the editor before testing. New construction snapshots identify plugin 0.2.5.

These checks validate the covered fixtures, not arbitrary contraptions or complete Besiege functionality parity. Turning tire contact, unrealistic weight distribution and incompatible layouts can still produce physical slip.
