# Mechanical steering correction — 0.2.6

## Evidence

Reviewed the supplied gameplay recording `2026-10-03 19-48-53.mp4` and construction trace from attachment `443eeab3-a188-4b6d-a8b9-2e3b7decd834`.

The front tire axes follow the commanded hinge rotation, with low axle wobble and no significant hub gap. During right input the chassis nevertheless develops positive (left) yaw. One powered rear wheel loses contact, and rear lateral slip exceeds 2 m/s. The earlier test checked unsigned turn magnitude and therefore missed the direction mismatch.

The exact authored assembly is preserved in `Tests/ByteEngine.Tests/TestData/GoblinSteeringUser025.json`.

## Change

Driving wheel torque is bounded at 120 game torque units instead of using the 10,000-unit near-hard velocity drive. Speed targets, smoothing, wheel geometry, hinge targets and friction settings remain intact. This lets physical wheel speeds differ under cornering load rather than forcing the tires to slip at a locked motor speed. Auto-braking still uses the separate 10,000-unit limit.

This is a calibration for ByteEngine/Bepu, not a claim that the source Unity motor force number has been copied literally. No steering rotation or vehicle translation is injected into the chassis.

## Tests

Before the fix, the exact recorded build developed +0.610 radians of left yaw during the right-turn test. It failed the new signed-direction regression.

After the fix, that build develops -1.123 radians (right) for D and +1.126 radians (left) for A. Reverse driving produces the mechanically expected opposite heading changes. Counter-steering reverses the turning direction in all eight combinations of recorded/reference machines, A/D and W/S.

`--steering-direction`: signed chassis yaw, both inputs, forward/reverse and counter-steering. `--contraption` includes this regression alongside existing wheel/hinge, traction, brake/restart, gear, projectile, flight, damage, save/load and deletion tests. Small/large wheels still reach nominal rolling speed in the four-powered-wheel traction fixture.

Native game-loop regression reproduces the user’s machine through actual W/D input, asserts the chassis turns right, then uses W/A and asserts a left turn relative to its previous heading. It also checks return to Build.

Logs: `output/video-wheel/steering-direction-baseline.log`, `026-direction-countersteer.log`, `026-all-checks.log`, `026-native-steering.log`. Screenshots: `output/goblin-scraper/standard-contraptions/user-steering-*-026.png`.

## Installation

Updated project package: `C:/Users/codex/Documents/Goblin Scraper/Plugins/bytebard.goblinscrapper.byteplugin`, version 0.2.6. Previous package is backed up under that project’s `Backups/before-mechanical-steering-026`.

Restart the editor to load the new plugin. The previously installed 0.2.5 renderer fix is still required and unchanged. New debug snapshots distinguish driving torque 120 from auto-braking torque 10,000.

Covered fixtures pass; arbitrary construction layouts still require playtesting. Complete functionality parity with every Besiege block is not established by these checks.
