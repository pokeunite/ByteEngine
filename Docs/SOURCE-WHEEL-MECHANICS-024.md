# Goblin wheel and steering mechanics — 0.2.4

The 0.2.3 trace supplied on 3 October still showed differential steering and separate simulated wheel hubs. This update replaces those mechanics using the installed Besiege wheel/steering scripts and Rigidbody prefab data as references.

## Implemented behavior

- One wheel rigid body has a bearing directly to the supporting frame or steering output. The extra hub body and its weld are removed.
- Small/large powered wheels target 10.472/10.193 rad/s, with source smoothing rates 16/8. W/S controls motors. Releasing input auto-brakes the powered axle.
- A/D does not alter fixed wheel motor targets. Steering blocks accumulate angle at 100 degrees/second, stop at ±40 degrees, and return at 60 degrees/second.
- Controls and physics run at a fixed 60 Hz regardless of rendering frequency. Solver settings and bounded spring/motor forces are calibrated for Bepu, rather than copying Unity numeric drive coefficients directly.
- Source Rigidbody mass ratios and drag values are included in the plugin as embedded resources. Existing per-project reference sidecars can override them. Wheel friction is 0.6; the previously requested Shift drift control reduces grip.
- Measured source wheel collider bounds determine radius, width and axle offset. Bepu uses a smooth cylinder for rolling contact. The source polygon hull produced ground-contact chatter and front-axle traction loss in Bepu, so its geometry is not used as a rolling collision shape. Visible meshes are unchanged.
- Broken wheels lose both bearing and motor and follow their detached physical body. Shared support bodies receive a blast impulse once.
- Round balloon lift now uses its source prefab strength 28, 0.6-second startup ramp and normalized game force units, preserving lift after mass corrections.

## Verification

`--contraption`: small/large front/rear/all-powered layouts at 30/60/120 FPS; forward, reverse, auto-braking and restarting a sleeping machine; identical behavior with/without A/D on fixed axles; loaded steering, gear transfer; wheel damage detachment; passive wheels; projectile motion; flight lift; all 65 non-master parts can attach and simulate; save/load and delete.

`--recorded-build`: the seven-part machine in the latest supplied trace and the earlier nine-part steering machine at 60/45 FPS. Straight travel over four seconds was 7.6–10.1 m, sideways drift below 0.02 m. Maximum recorded axle wobble was 0.088 degrees, hub separation 0.00081 m, rigid mount rotation 0.056 degrees.

`--construction-debug`: opt-in recording, frozen copy, clear/resume snapshots and bounded trace memory.

`--goblin-vehicle ... --free`: native input/rendering test, visible master follows physics, all included models import, mouse connector placement, undo/delete, wheel and hinge machines, both recorded fixtures, and return to build mode. Screenshots/logs are under `output/goblin-scraper/standard-contraptions` and `output/video-wheel`.

## Source reference and limits

Inspected installed scripts: `CogMotorControllerHinge.cs`, `FreeWheel.cs`, `SteeringWheel.cs`, `SetJointWheel.cs`, `BalloonController.cs`, plus wheel collider and Rigidbody prefab data from `C:/Besiege/Besiege_Data`. Decompiled local reference files are in `.artifacts/besiege-full-source`.

This is a wheel/steering correction, not a claim that all Besiege features are ported. Other included blocks retain prototype implementations; their finite simulation tests do not establish exact functionality parity. Unity/PhysX and Bepu have different constraints/contact solvers. Per-block key remapping, full configuration menus, source damage/material systems and many other block-specific behaviors still require separate implementation and validation.

Installed package: `C:/Users/codex/Documents/Goblin Scraper/Plugins/bytebard.goblinscrapper.byteplugin`. Previous package backup: `Backups/before-source-mechanics-024/bytebard.goblinscrapper.byteplugin` in that project. Restart the editor to load 0.2.4; the console snapshot prints the plugin version.
