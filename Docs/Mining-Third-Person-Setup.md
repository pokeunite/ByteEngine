# Mining prototype: third-person controls

The player now uses a perspective, over-the-shoulder camera rather than the overhead camera. The existing model, weapon sockets, locomotion blendspaces, firing/reloading layers, business progress and machines are retained.

## Controls

- Click the Game view to capture mouse look.
- **WASD:** move relative to camera yaw.
- **Mouse:** turn the view; the armed character faces camera yaw for strafing.
- **Right mouse:** tighter aiming distance and field of view.
- **Left mouse:** existing Event Sheet firing and projectiles.
- **R:** existing reload binding.
- **Space:** unassigned. Dash and its Event Sheet rules are disabled.
- **E:** interact with the nearby machine or tent computer. Panels release the cursor and suspend movement, mouse look; closing restores movement and leaves dash disabled.

## Editing

On **Mannequin**:

1. **Player Controller 3D** owns movement and body facing. The former Top Down Controller is disabled. Armed facing is managed by the combat-view component; turn off Face Camera While Armed to use movement-facing outside aiming.
2. **Camera Boom 3D** owns shoulder framing and solid-obstacle avoidance. Adjust Pivot Height, Shoulder Offset, pitch limits and collision settings here. The gameplay camera must be a child of the player for this native boom.
3. **Third Person Combat View** exposes normal/aim arm lengths (2.8 / 2.1 metres), normal/aim FOV (60 / 48), mouse sensitivities, zoom smoothing, aim Input Map action, crosshair colour/visibility and upper-body pitch control. Its settings drive arm length, sensitivity and FOV during play, so edit these values here rather than only on the camera.
4. **Modular weapon equipment** uses **Aim Mode: ThirdPerson** and the **Third Person Gameplay Camera** reference. A camera-centre ray selects the aim point, then the shot originates at the weapon muzzle. Nearby cover therefore still blocks muzzle-origin shots. Projectiles, trails and recoil remain configured on weapon definitions.

The existing animation profile has procedural upper-body pitch enabled on Spine_03. Its locomotion blendspaces and action masks are preserved. Native turn-in-place support now accepts the third-person controller as well as the older top-down controller. Disable Drive Upper Body Pitch in the combat view if tuning a different model/rig.

The disabled top-down camera component remains available in the saved scene, but it does not compete for camera control. Reverting should restore the backed-up scene and animation profile together.

## Validation and recovery

- 256 existing regression checks passed.
- 28 native third-person checks passed: movement relative to rotated camera, mouse look, aim zoom, grounded directional dash, modal controls, collision pull-in, hitscan and projectile convergence on a crosshair target, and source/save preservation.
- 49 machine/market/map interaction checks passed with the new setup.
- Installed editor play/stop/unload check passed.
- The actual game view was visually inspected for character, camera, crosshair and HUD framing.

Scene and profile backups: `.byteengine/Backups/third-person-*` in palclone. Installed plugin/runtime backups: ByteEngine/UpdateBackups/mining-ui-*. Canonical plugin source version is **3.23.0** and project PluginSource is synchronized. No game export or Git push was performed.

Tooling: `--third-person-author` performs the backed-up scene/profile migration; `--verify-third-person` runs the isolated native checks. The installer updates code packages by default; pass `-IncludeModels` only when intentionally replacing Blender assets.

See [camera and progression review](Mining-Camera-and-Progression-Review.md) for the vertical-look correction, imported-rig aiming and production changes.

Current simplification: turn-in-place playback is disabled on the character, dash is disabled, and the HUD shows health without energy. See Alien-Mining-Upgrade-Port-Plan.md for the complete reference catalog and crash fix.
