# Top-down and isometric controller references

Reviewed 21 October 2026. Research references only; not imported or tested in ByteEngine.

Update: the Duckov reference has now been adapted into native TopDownController3D and TopDownCamera3D components. See [native setup and supported features](Top-Down-Controller.md). The other repositories remain research references.

| Purpose | Repository | Useful features documented by author | Code license |
| --- | --- | --- | --- |
| Top-down action controller and following camera | https://github.com/EugeneVorobyev/godot-duckov-controller | WASD movement, cursor-facing character, character follow, camera shift toward cursor | MIT |
| Isometric character controller and follow camera | https://github.com/EmreDogann/IsometricCharacterController | Keyboard/gamepad input, movement damping, smooth camera follow, obstruction cutout shader, buffered/coyote jumps | MIT |
| Independent top-down strategy camera | https://github.com/EmilHakala/3D-RTS-Camera | Smooth limited zoom, orbit, screen-edge scrolling, keyboard panning, mouse pitch/rotation | MIT |

Recommendation: use the Duckov-style project as the main action movement/camera reference; study the Unity example for isometric movement and obstruction visibility; keep the RTS camera as a separate strategy/editor use case. These are Godot/Unity implementations and need adaptation to ByteEngine, not direct installation. MIT license notices must accompany reused code; bundled third-party art can have separate terms.

ByteEngine adaptation should expose movement speed, acceleration/deceleration, facing mode, camera-relative input, follow smoothing, cursor look-ahead strength/limit, projection, zoom limits, camera bounds and obstruction handling. Gameplay triggers belong in editable event sheets. This is a proposed feature list, not a claim that these features have been implemented or performance-tested.
