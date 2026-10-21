# Top-down movement and camera

Native adaptation of the MIT-licensed EugeneVorobyev/godot-duckov-controller, revision 4b4f7e4dab1e7a8793db56e03a73a881966b696c. License: Engine/ByteEngine.Core/ThirdPartyNotices/DuckovController.txt.

## Setup

1. Player root: add **Character Controller**, **Capsule Collider** and **Top Down Controller**. Disable **Player Input** if already present. The motor owns speed, acceleration, deceleration, gravity and collision. Use one movement input owner.
2. Separate camera object: add **Camera 3D**, enable **Active Game Camera**, add **Top Down Camera**, pick the player in **Target**. Disable any previous camera driver controlling this camera.
3. On the player's **Top Down Controller**, pick this camera in **View Camera**. An empty camera reference uses the scene's active camera.
4. Keep a solid floor collider. Click inside Game View to activate controls. The pointer remains visible for aiming; Escape releases input.

The project's Move input action supplies WASD/gamepad movement. **Camera Relative Movement** aligns movement with the camera; disable it to match the original Duckov project's world-space axes. **Facing** selects cursor, movement or independent rotation. **Turn Sharpness** controls facing response. **Aim Plane Height** is relative to player Y. Jumping is opt-in using the Jump input action.

## Camera controls

Default framing uses an orthographic camera at 45-degree yaw and 55-degree pitch. For true isometric framing use orthographic, yaw 45, pitch 35.26439 (also available as an event-sheet action). Perspective is optional. The rig manages Camera 3D projection/view width while enabled; Field Of View remains on Camera 3D.

**Follow Sharpness** and **Look Ahead Sharpness** use exponential smoothing independent of frame rate; zero snaps. Look-ahead shifts in camera space toward the visible cursor, has a center dead zone and clamps at its maximum distance. It returns toward center when input is blocked or the cursor leaves the view. Wheel zoom has minimum/maximum limits. Optional XZ bounds constrain the camera focus, not the whole visible rectangle. Camera runs after movement/physics to avoid ordering jitter.

## Event sheets

**Top Down / Controller**: enable input, set facing, choose camera-relative movement, test valid cursor aim, store aim world position in a named variable. Use existing Character Controller actions for speed/jump.

**Top Down / Camera**: select follow target, enable look-ahead and its distance, set zoom, snap to target and select true isometric framing. Pick the camera object as action target when the event sheet is on the player.

Related rows should share one sheet, e.g. Player Movement. Speed and animation remain normal authorable engine systems. No project-specific script is required. This update does not add pathfinding/click-to-move, wall transparency or automatic camera obstruction fading.
