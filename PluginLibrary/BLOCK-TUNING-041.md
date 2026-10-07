# Block tuning and land palette — 0.4.1

Restart the editor and reopen Goblin Scraper to load the package.

Click the wrench on the top toolbar, then click a placed block. Drag its value bars or click its switches. Adjustments take effect when you next deploy. Escape or X closes tuning. Reset block restores defaults. Apply to same type copies this block's settings to all identical blocks, including mirrored wheels. Copying a tuned block also retains its settings.

Wheel settings: speed multiplier, drive torque, response, grip, auto brake and reversed direction.
Steering: angle limit, turn speed, return speed, return-to-centre and reversed input.
Suspension: spring stiffness and damping.
Piston: travel, speed and force. Travel is limited to the current model's physical stroke.
Powered cogs, motor, saw and drill: rotation speed, torque and reversed direction.
Free wheels, caster and skate wheels: grip.
Cannon: projectile speed, with recoil scaling accordingly.
Passive structure and free swivel have no adjustable settings. Their purpose remains physical connections and free articulation.

For a faster first test, set a powered wheel to speed 2x, torque 300–400 Nm, grip 0.8, response 1.5x, then Apply to same type. Keep direction at its default on both sides: mounting orientation already handles mirrored wheels. Higher speed without enough torque may not accelerate a heavy machine; very high grip can increase tipping.

A physics reference vehicle reached 7 m/s at speed 1x and 14 m/s at speed 2x, both with 400 Nm torque. Actual results depend on your construction. Defaults remain compatible with earlier machines.

Tuning supports undo/redo and machine save/reload. Snapshot format is now version 7; existing v4–v6 builds still load. Older plugin versions cannot load new v7 saves.

The game enables JamLandPalette. Flight and the deferred parts are hidden in its tray, while their models and logic remain installed for existing builds. Retained exceptions: round armour, log, all three cogs, caster, skate wheel, powered/free large wheels and free swivel. Armour plates now appear under Structure. Disable JamLandPalette on VehicleBuilder3D to restore the full tray.

Native event actions: Open Block Tuning (block id) and Set Block Tuning Value (block id, setting key, numeric value). Example keys: speed, torque, grip, autoBrake, reverse, angle, steerSpeed, stiffness, damping, travel, linearSpeed, force, shotPower. Only compatible settings are accepted; finite values are clamped to safe supported ranges.

Reference inspection: local Besiege CogMotorControllerHinge, SteeringWheel and BlockMapper. Their per-block mapper interaction informed the design; our panel and Bepu settings are independently implemented. Extracted Besiege code is not included in this package.

Validated: native wrench selection, slider interaction, applying to matching blocks, undo and disk reload; actual speed change in physics. Plugin builds with zero errors/warnings.
