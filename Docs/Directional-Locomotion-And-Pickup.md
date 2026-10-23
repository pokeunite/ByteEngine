# Gameplay focus and directional locomotion

Weapon **Game input is active** uses the same gameplay-input gate as movement. In the editor, Play accepts keys while Game View has keyboard focus or captures input and the application is focused. A visible, uncaptured pointer does not disable E/G. Clicking other editor panels disables gameplay keyboard input.

Pickup actions recheck range and carry limits themselves; a separate Can Pick Up condition is optional. **Last Pickup Result** on Weapon Equipment exposes success or the blocking reason for event-driven UI. The diagnostic HUD shows eligible pickups and keeps the last E press visible for three seconds. Interaction remains in the E/G Event Sheet.

For velocity-based 2D locomotion, select **2D blending: Direction and speed** in Animation Profile > Blend Spaces. X/Y remain character-local Right/Forward velocity. Speed interpolates along each authored direction, then neighboring directions blend. Diagonal walking no longer introduces running or idle because a Cartesian triangle cuts across the walking-speed circle. Cartesian remains the default for existing general-purpose blend spaces.

Top Down Controller > **Facing Mode** supports both control styles:

- **Cursor:** always face the mouse; moving east while aiming south uses a sideways animation relative to the character.
- **MovementUntilAiming:** face travel normally; hold the **Aim** action or fire to face the cursor and strafe. Palclone maps Aim to right mouse and Fire to left mouse. Both bindings are editable in Project Settings > Input. **Aim Release Delay** prevents rapid facing-mode changes between shots, and **Face Cursor While Firing** can be disabled. Aim Action and Fire Action are selectable on the component. Event Sheets can also Set Top Down Aiming Request, check Is Facing Cursor, or change Facing Mode.
- **Movement:** always face travel. **Independent:** leave rotation to another system.

Changing the cursor never rotates the WASD travel vector in either main mode. The current pack has forward running but no sideways/backward running takes, so those directions clamp to their fastest authored sample.

Reference: [Unity's official 2D blending guide](https://docs.unity.com/en-us/engine/6000.6/manual/animation-section/animation-mecanim/animation-animator-controller/animation-state-machines/animation-blend-trees/blend-tree-2dblending) distinguishes directional blend spaces with multiple speeds from Cartesian parameter blending. ByteEngine's direction/speed interpolation is an independent implementation.
