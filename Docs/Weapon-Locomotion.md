# Weapon-driven character locomotion

Modular Weapon Gameplay 3.10.1 includes optional character locomotion bindings, per-weapon hand sockets and top-down aiming. Existing FPS weapons keep their previous behaviour when these fields are empty and Aim Mode remains View Camera.

1. In the character Animation Profile, author populated blendspaces such as `Unarmed`, `One Hand` and `Two Hand`. Use the imported character's actual clips. A directional space can use `Right` / `Forward` velocity; a speed-only space can use `Speed`.
2. On the player, add **Weapon Equipment**. Pick **Character Animation Controller**, **View Camera**, and **Weapon Root**. Put weapon rigs below that root. Disable built-in fire input when an event sheet handles firing.
3. On each rig, add **Weapon Definition** and **Weapon Instance**. Choose **Locomotion Blend Space** from the profile dropdown. Use distinct inventory slots. Leave this field empty to use the equipment's unarmed selection/profile default.
4. For world pickups, add **Weapon Pickup** to a separate world object and link its Weapon Rig. Call the existing pickup action from your interaction event; set Equip after pickup to control whether collection immediately changes stance.

Equip applies the chosen stance. An inventory-only pickup leaves the current stance intact. Dropping a selected weapon switches to the remaining weapon, or restores unarmed locomotion when empty. Fire/reload/equip actions still belong in event sheets. Full-body actions temporarily suppress the locomotion blendspace; jumping/falling keep the profile's airborne animation.

**Unarmed Blend Space** is optional. Empty returns to the profile/graph default. **Locomotion Binding Error** reports missing/empty blendspaces; invalid choices fall back to the profile default rather than retaining the previous gun's stance. Dropdowns resolve the equipment binding through parent objects, so keep rigs beneath Weapon Root on the player.

Event sheets can call **Animation → Set Locomotion Blendspace** directly, or **Modular Weapon Gameplay → Weapon Equipment → Apply equipped weapon locomotion** after editing component values at runtime. Selection never rewrites the shared profile asset. Weapon muzzle, damage, ammunition, fire mode, projectiles, inventory capacity and pickups continue to use the existing reusable systems.

No extra game-specific scripts or animation copies are required. Weapon meshes still need a hand/socket attachment and suitable offsets; selecting a stance does not automatically place a model in a hand. Author one animation profile per compatible character rig.

For top-down projects, set Weapon Equipment's **Aim Mode** to **Top Down Cursor**. It launches from the equipped weapon muzzle toward the native Top Down Controller's Aim World Point; without a cursor point it uses the player's forward direction. Set the controller's **Aim Plane Height** to the intended target height. View Camera remains the default for existing FPS projects.

Blendspace timing uses duration compensation, so a long idle reference does not slow short walk/run clips. This compensation is independent of the profile's authored Playback Speed.

## Per-weapon hand alignment

On each Weapon Definition, select **Grip Socket** from the character's model socket list. Palclone uses **PistolGrip**, **RifleGrip** and **ShotgunGrip**. Edit each socket in Socket Preview with the corresponding gun as its alignment asset. The animation dropdown now has a search field; type `SH`, `DH`, `walk`, or a clip-name fragment.

The equipped object is the imported gun model root, attached with location/rotation/scale snapped to its selected socket. Its imported `Import Space` node remains directly below that root. Do not add a second offset/rotation/scale wrapper below the socket: Socket Preview shows the original imported asset, so additional gameplay transforms would create a mismatch. Edit the socket for hand alignment instead. Empty Grip Socket preserves legacy parenting. **Weapon Attachment Error** reports a missing socket. Event sheets can call **Apply equipped weapon socket** to refresh an edited binding at runtime.

In keyboard conditions, click **Listen**, then press the desired key. Escape cancels. The key dropdown remains available, including for binding Escape itself. Key listening works in detached event-sheet windows and prevents captured Delete/F2 from also deleting or renaming the selection.

## Socket editing and moving attacks

Use the socket editor's **Editing socket** dropdown or searchable **Sockets** list. Transform fields have separate edit identities for each socket, and changing selection resets the gizmo's drag state. Only the selected socket's handle is shown by default; **Show other socket handles** is optional. Switching sockets does not change any stored socket values or preview asset.

Named actions support **Body Mask** on the Animation Profile's Actions tab. **FullBody** keeps the previous whole-body override. **UpperBody**, with **Mask Root Bone** set to the character's first spine bone, plays an independently timed action over the normal locomotion pose. An authored animation layer can now own this mask; Palclone uses the layer setup described below. Blend In/Out and Playback Speed continue to control the action. Profiles without a mask remain compatible and default to FullBody.


## Authored upper-body layers

Palclone now uses the profile's **Layers** stack, in this order:

1. **One Hand Aim** — fixed SH standing aim pose, active in `One Hand` locomotion.
2. **Two Hand Aim** — fixed DH standing aim pose, active in `Two Hand` locomotion.
3. **Weapon Actions** — receives the four SH/DH Fire and Reload actions, timed independently of walking.

All three layers mask `Spine_01` and its descendants. **Stabilize Torso Rotation** compensates the walking pelvis rotation at the mask root, keeping the authored weapon facing while the legs and hips animate. It does not freeze the legs or change socket transforms.

On **Actions**, select **Animation Layer: Weapon Actions**. The layer owns the mask and blend mode; the action owns its clip, speed, blend times, and interruption rules. Existing **Play Named Action** event-sheet actions still trigger firing/reload. Enable/Disable Layer and Set Layer Weight remain available in event sheets. A misspelled/missing layer binding fails instead of silently playing a whole-body action.

Layer **Playback** can follow LocomotionPhase (legacy), run Independently at Layer Speed, or hold a FixedPose at Pose Time. **Active In Blendspaces** limits a layer to selected locomotion sets; empty means all. Defaults preserve existing projects. Action clips on a layer always use the action's own clock; they do not repeat at walking cadence. In Additive mode, action deltas are measured from that action clip's first frame.

Use a stable aim layer underneath short firing clips so returning from a shot does not return the arms to the running arm-swing pose. Repeated shots crossfade from the displayed action pose without fading the arms back to locomotion between shots. Empty Animation Layer keeps the legacy per-action body-mask path for existing profiles.


## World weapons without inventory parenting

Weapon Root is optional and is only used for discovering preloaded starting gear. A pickup can now reference a Weapon Instance anywhere in the same scene. Range checks, carry limits, ownership and unique inventory slots still apply.

For a single world gun, add **Weapon Definition**, **Weapon Instance**, and **Weapon Pickup** on the gun's model root. Leave Pickup > Weapon Rig empty to use itself. Set its Grip Socket and Locomotion Blend Space, and keep weapon presentation events on that same object. Pickup attaches the model root to the collecting character. Drop detaches it, restores its world scale/orientation, places it above the ground and preserves ammunition. An equipped self-pickup does not hide its own gun. Suspend Physics When Carried disables its world colliders and rigid body during carry, then restores their previous enabled states on drop.

Separate world mesh / held rig arrangements remain supported. In that case, pick any scene rig in Pickup > Weapon Rig. **Preview Character** on Weapon Definition supplies the profile for authoring socket/blendspace dropdowns when the rig has no player ancestor. Empty can infer a single scene character; actual equip always uses the collecting equipment's Character Animation Controller.

## Movement recording HUD and turning

Palclone's player now has **Character Motion Debug HUD**. F8 toggles it. CYAN marks the actual cursor/world aim; GREEN marks actual horizontal velocity; GOLD marks character facing. The panel reports cursor coordinates, local right/forward velocity, classified movement direction, animation state, real blendspace clip contributions, action and layer weights. The base clip is labelled separately because its name alone does not describe a blendspace. Font size, panel position, colors and vectors are component settings. Event sheets can show/hide the HUD through **Character Debug**.

In **Animation Profile > Locomotion > Turn In Place**, configure angle thresholds, maximum movement speed, fades and left/right/180 clips for each stance. Palclone uses SHNA turns for Unarmed, SH turns for One Hand and DH turns for Two Hand. Native cursor facing continues to own actor rotation; turn clip speed follows actual yaw speed and authored root yaw is removed to prevent double rotation. Walking, running, jumping, changing stance, reversing the turn or a whole-body action cancels the current turn. Masked upper-body fire/reload may continue above turning legs. Event sheets can enable/disable turn-in-place and check whether a turn is active.


## Continuous turns and pickup diagnostics

Turn-in-place now accumulates actual stationary body rotation as well as checking large instantaneous aim errors. Slow, continuous cursor sweeps therefore trigger foot turns even when the controller tracks the cursor closely. Minimum Turn Angle defaults to 15 degrees; minimum/maximum clip speeds are exposed next to the turn settings to keep the pose progressing visibly.

Palclone's three world guns now each own their Weapon Definition, Weapon Instance, Weapon Pickup, presentation events and imported model. There are no separate hidden weapon-rig references to go missing. Select the world gun to edit its stats. Grip sockets still belong to the character and are preserved. The model's import-space node sits directly below the weapon root, matching socket preview during equip. World scale/orientation are restored on drop.

Pickup Radius measures horizontal distance; Maximum Height Difference independently prevents collecting through floors. Weapon Pickup exposes Binding Error and collection-status diagnostics. The movement HUD also reports input capture, the configured interaction key (E by default, with a brief press indicator), current inventory and nearest pickup name/distance/status. `Ready`, `Out of range`, `Carry limit reached`, and missing bindings are visible. Gameplay still goes through the editable E/G event sheets; the HUD never performs a pickup.

Plugin components can contribute diagnostic text by implementing the native `ICharacterDebugSource` interface. The core HUD does not depend on weapon plugin types.
