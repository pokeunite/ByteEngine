# Construction debug

Open Console → Debug and tick **Debug Building / Contraptions** before building or driving. Run a short test: place beams and wheels, drive straight, steer both directions, brake, return to Build. Untick the checkbox to freeze the trace, then use **Copy Debug** or **Save Debug…**. Clear Debug resets the buffer and requests fresh build/physics snapshots if recording remains enabled.

Recorded: full authored build and catalog revision; selection/hover/socket/twist/placement rejection; accepted placement and branch move; removal/detachment; undo/redo; save/load and failures; mode changes; firing/mechanism use/damage; body/collider/axis/pivot/mass setup; input, speed, root uprightness; every part's pose, velocity, attachment gap and joint presence; wheel RPM, axle misalignment in degrees, hub drift, lateral speed and friction; moving output transforms. GroundNear is a height estimate. GroundContactReported and contactDepth record solver contacts reported that step; sleeping bodies may not report a fresh contact. MountAngleDeg measures a rigid mount�s rotation relative to its authored frame orientation.

Wheel wobble and hub displacement measure physical hinge compliance; flags mark axle misalignment above 2 degrees or attachment/hub separation above 0.025 m. Plugin 0.2.6 uses direct wheel-to-support bearings. A/D drives steering components only; fixed powered wheels respond to W/S and auto-brake on release. No automatic differential steering is mixed into wheel motors.

Opt-in tracing samples physics at 4 Hz, retains bounded event and sample queues, and does no expensive sampling when disabled. Units: metres, seconds, radians (wobble explicitly degrees); wheel speed in RPM.

The old running editor cannot be overwritten while Windows has its binaries open. Close it, then launch the updated debug editor at C:/Users/codex/ByteEngine/Dist/ByteEngine-BuildDebug/ByteEngine.Editor.exe and open the Goblin Scraper project. Updated project plugin is version 0.2.6 and requires the existing Core diagnostic API.
