# Interactive sand and Console diagnostics

Installed plugin version 0.8.5 and rebuilt Dist editor/player runtime. Live vehicle saves and scenes preserved.

## Console > Debug

- Debug Performance / Frame Pacing: CPU render submission, delayed nonblocking full-frame GPU timestamps, presentation waits, allocated bytes, collection counts and GC pause time. Vehicle phase samples include physics, camera, UI, feedback, contacts, vertical velocity and fixed steps.
- Debug Interactive Sand: active window, window revision, cached tiles, successful stamps, modified nodes, deepest rut, shader and imprint-texture state.
- Untick to freeze; Copy Debug or Save Debug copies the complete bounded trace. Clear Debug clears both.
- Console live text is capped at 24KB and refreshed twice per second. Hidden/collapsed console content is skipped. The prior full-trace string and editable buffer were recreated every display frame. This removes a potential diagnostic-induced slowdown; it does not prove the remaining FPS problem is solved.

## Reference video

Reviewed the supplied 105-second interactive-desert video. Its target is persistent surface deformation with raised shoulders and smoother ripples inside the pressure track. Dust is not the requested effect.

Sand contact now approaches a pressure-controlled trough, retains displaced shoulders, and suppresses ripple shading inside the disturbed area. ContactRutDepth is a saved terrain setting, default 0.065m, with bounded rut depth. Rendering and collision still share the local heightfield. This is not individual granular-particle simulation.

interactive-sand-surface.png is a dust-free controlled circular pressure test using the game's terrain renderer, not a screenshot of a player driving in a circle. The real 21-block saved vehicle was separately driven and checked.

## Verification and remaining performance issue

Console trace fields, opt-in freeze, full Copy/Save sections, bounded storage and clear regressions pass. Terrain/heightmap/wheel-rut and vehicle-physics regressions pass. Native saved-vehicle driving, surface capture and OpenGL checks pass.

The new trace captures genuine GPU and physics spikes as well as startup costs. Native test rendering includes an explicit GPU wait for measurement; the editor/player diagnostics use asynchronous timestamps without forced waits. Stable 60 FPS is not verified. Next investigation should use Console Performance and Interactive Sand together to correlate spikes with sand-window changes and GPU render time.

Source and test logs are saved in this folder and in the engine repo Designs/InteractiveSand.

Final exported Windows build: Builds/Windows/Dune_Company-Windows-20261007-000801-579829. Packed terrain/collision/driving validation passed; executable smoke test exited 0.

Resume: collect Console Debug Performance / Frame Pacing and Interactive Sand together during a real editor playtest; investigate correlation of GPU/physics spikes with window revision changes. No claim that the remaining FPS issue is resolved.
