# Build clearance and moving-part braces - 0.3.6

Installed plugin: Goblin Scraper/Plugins/bytebard.goblinscrapper.byteplugin. Restart ByteEngine to reload it. Existing block and brace saves remain compatible.

Build mode previously kept the master at 1.25 m even when suspension and long beams put the wheels below ground. This was floor occlusion, not missing wheel geometry. The assembly now rests at a preview height calculated from its lowest connected part, with .11 m ground clearance; part attachment coordinates remain unchanged. The build camera follows the complete assembly's bounds centre while preserving manual orbit/pan. Returning from simulation restores the authored build and this clearance.

Braces previously welded endpoint bodies, locking their orientations and overconstraining moving joints. They now constrain the distance between the actual selected socket positions, using body-local anchors and bounded, damped correction. Endpoints can pivot; bodies retain angular freedom. Endpoint collisions are no longer globally disabled merely because a brace joins them. Visual brace endpoints update after the current physics step so the span does not lag behind moving parts.

A brace still limits motion according to its geometry. A diagonal brace from a moving suspension output to the fixed frame can reduce or prevent suspension travel. Attach to the suspension's fixed Root connector when reinforcing the frame without restricting travel. This is a fixed-length, pivoting-end reinforcement bar; it is not a rigid orientation weld or a sliding linkage.

Validation: clean Release build. Six-second brace tests for contractable spring, suspension, piston, steering block and ball joint, each connected from Moving output to a wooden beam; finite poses, bounded velocity, held span. The axially braced steering block retains approximately .70 rad of relative rotation. Detached beam length, duplicate rejection, snapshot restore and endpoint deletion tests pass. Native suspended-wheel fixture with a brace passed placement, simulation, return to build, authored-layout equality and floor-clearance checks. Existing native toolbar, pointer placement/erase, undo/redo, disk save/reload and HUD tests pass. Gear pair, train and phase regressions also pass.

Previous package: Documents/Goblin Scraper/Backups/brace-clearance-036/previous.byteplugin. Current package copies: repository PluginLibrary and Downloads/ByteEngine Plugins (also zipped). No model import is required for 0.3.6.
