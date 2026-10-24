# ByteEngine weapon workshop
## Build your own guns, pickups, animations, projectiles and HUD

For Modular Weapon Gameplay 3.16.0 · palclone reference setup · 24 October 2026

This tutorial takes you from a model in the Assets panel to a usable world gun. You do not need to write code for the standard setup. Work through the first gun before making variations. The example is a top-down character; FPS differences are called out.

## 1. Understand the pieces

| Piece | Where it belongs | What you edit there |
| --- | --- | --- |
| Weapon Equipment | Player | Camera, aiming mode, character binding, starting weapon and carry limit |
| Weapon Definition | Gun model root | Damage, fire mode, ammunition capacities, recoil, muzzle, projectiles and trails |
| Weapon Instance | Same gun root | Starting/current magazine and reserve ammunition; live weapon state |
| Weapon Pickup | Same gun root for the simple setup | Interaction range, pickup binding and drop clearance |
| Animation Profile | Character asset | Locomotion blendspaces, grip sockets, upper-body layers and named actions |
| Event Sheets | Player, gun or director as appropriate | Input rules, pickup/drop, sounds, animations and HUD updates |
| Weapon Inventory HUD | HUD object | Carried-weapon cards, hotkeys and reserve magazine markers |

**Definition is tuning; Instance is ammunition/state; Equipment is the player's inventory.** A gun does not need to be a child of an inventory object to be collected.

`Self` means the object running that Event Sheet. A player event can usually use Self for equipment. An event on a scene director must explicitly select the player. A gun presentation event should explicitly select its gun and the character to animate.

## 2. Fastest route: duplicate a working gun

1. Save a copy of your scene before editing.
2. Duplicate an existing working world gun, including its components and presentation events. Start with the Pistol for a single-shot weapon or SCAR for an automatic weapon.
3. Rename the object and its Weapon Definition > Display Name.
4. Assign a **unique Slot** on Weapon Definition. Slots are zero based. Two different carried guns cannot occupy the same slot.
5. Keep the imported model root structure intact. To use another model, follow the from-scratch model/root setup below rather than moving mesh children arbitrarily.
6. Set Weapon Instance > Start In Inventory off if this should be found in the world. Check its starting magazine and reserve ammo.
7. Select the appropriate Grip Socket and Locomotion Blend Space. Create a new socket if the new gun needs a different alignment.
8. Choose the new gun's muzzle point, HUD icon, VFX and audio references.
9. Open copied Event Sheets. Check every object reference: duplication must not leave rules pointing at the original gun. Make a separate presentation sheet if editing an asset shared by both would affect the original.
10. Play and complete the acceptance checklist at the end.

Do not duplicate the player's Weapon Equipment for every gun. One equipment component manages multiple weapon instances.

## 3. Prepare the player once

1. Use a character with a working movement controller, camera and Animation Controller before adding weapons.
2. Add **Weapon Equipment** to the player.
3. Set **View Camera** to the gameplay camera.
4. For palclone-style aiming, set **Aim Mode = Top Down Cursor**. The player's native Top Down Controller supplies the cursor aim point. Check its Aim Plane Height against your intended target height. For an FPS, use **View Camera** mode instead.
5. Set **Character Animation Controller** to the player object containing that controller.
6. Choose **Unarmed Blend Space**, or leave it empty to restore the profile default when unarmed.
7. Set **Carry Limit**, for example 2. Zero means unlimited.
8. Turn **Use Built In Fire Input off** when using the Event Sheet input rules below. Enabling both can cause conflicting firing behaviour.
9. **Weapon Root** is optional for world pickups. Use it only to discover preloaded starting rigs beneath a chosen root.

### Choose starting equipment

- **Start empty:** enable Start With No Weapons. Obtain guns through pickup or reward events.
- **Start with one chosen gun:** disable Start With No Weapons and select Default Starting Weapon. This can be a scene gun anywhere, independent of pickup range or parenting.
- **Start with several:** place the preloaded guns under Weapon Root, enable Start In Inventory on the intended instances and use distinct slots. Starting Slot selects the preferred slot; carry limits still apply. An explicitly selected default takes priority.

For predictable single-weapon starts, keep other world guns' Start In Inventory off. A loose world gun is not automatically discovered as starting equipment merely because that box is enabled.

## 4. Make a world gun from scratch

1. Import your FBX or GLB into the game's Assets folder and place an instance in the scene.
2. Select the **imported model root**, not an individual mesh or bone. Give it a useful name, such as `SMG - world gun`.
3. On that root add **Weapon Definition**, **Weapon Instance** and **Weapon Pickup**.
4. On Weapon Pickup, leave **Weapon Rig empty** to use the Definition and Instance on this same object. A separate world pickup and held rig are supported, but add another binding to maintain.
5. Set **Pickup Radius = 2.5** and **Maximum Height Difference = 2** as initial values.
6. Keep **Suspend Physics When Carried** enabled for a gun with world colliders/rigid body. It suspends those while carried and restores their previous enabled states on drop.
7. Set **Drop Ground Clearance**, initially 0.3. Adjust it for the model's origin so the dropped mesh stays above the floor.
8. On Definition assign Display Name, unique Slot, Grip Socket and Locomotion Blend Space. **Preview Character** helps populate the socket/blendspace pickers when the world gun has no player ancestor.
9. On Instance set starting Current Ammo and Reserve Ammo within the capacities on Definition. Disable Start In Inventory for a pickup-only gun.
10. Position it within reach on a walkable floor. Verify Pickup > Binding Error is empty.

Keep the model's imported Import Space hierarchy intact. Equipped placement snaps the gun model root to its character socket. Extra transform wrappers or a second hand offset can make gameplay differ from the socket preview.

## 5. Set gun stats

Edit **Weapon Definition** on the scene gun. These values are authored settings, not hidden weapon-name rules.

| Setting | Meaning |
| --- | --- |
| Trigger Mode | SemiAutomatic for one shot per press; Automatic for held input |
| Magazine Capacity / Reserve Capacity | Maximum magazine rounds / reserve rounds |
| Ammo Per Shot | Ammunition consumed by one trigger shot; usually 1 |
| Damage Per Pellet | Damage for each projectile/pellet, before target armour rules |
| Pellets Per Shot | 1 for a pistol/rifle; multiple for a shotgun |
| Rounds Per Minute | Firing cadence, independent of the animation clip duration |
| Spread Degrees | Pellet/shot spread cone |
| Range | Maximum weapon flight/raycast distance |
| Reload Mode | Magazine or PerRound |
| Reload Seconds / Empty Reload Seconds | Normal/empty reload timing; PerRound uses shell timing |
| Reload Speed Multiplier | Higher values shorten reload time |
| Allow Reload Interrupt | Allows interruption of per-shell loading |
| Auto Reload | Reload empty equipped gun after the last shot cooldown, when reserve remains |
| Equip Seconds | Delay before a newly equipped gun can fire |

Reserve Ammo counts **rounds**, not magazines. Example: a 12-round magazine and 36 reserve rounds represent three reserve magazines. Instance ammo and Definition capacity must agree.

Palclone also has an Event Sheet startup rule enabling Auto Reload. If you disable the component checkbox but it turns back on at Play, change that rule too.

### Starter projectile presets

| Setting | Pistol | SCAR | Shotgun |
| --- | ---: | ---: | ---: |
| Shot Mode | Projectile | Projectile | Projectile |
| Projectile Speed | 75 | 100 | 60 |
| Projectile Radius | 0.022 | 0.022 | 0.018 |
| Projectile Visual Size | 0.06 | 0.06 | 0.045 |
| Projectile Lifetime | 2 | 2 | 2 |
| Maximum Active Projectiles | 48 | 48 | 32 |
| Projectile Trail Length | 2 | 2.6 | 1.4 |
| Trace Width | 0.035 | 0.030 | 0.025 |

Start here and tune through playtesting. A shotgun's multiple pellets consume the projectile budget faster than a pistol. A full budget rejects another shot without spending its ammo.

## 6. Align the hand grip

1. Open the character's Animation Profile and go to **Sockets**.
2. Select the hand bone, usually Hand_R for this character, and add a socket.
3. Name it for the required alignment: PistolGrip, RifleGrip, ShotgunGrip or a new gun-specific name.
4. Select it in the **Editing socket** selector/searchable Sockets list.
5. Set **Preview Asset** to this gun model.
6. Choose a suitable holding animation. Use the animation search to find SH or DH clips.
7. Move, rotate and scale the selected socket until the gun fits the hand. Test more than one pose.
8. Save the profile/changes. On the gun's Definition select this exact **Grip Socket**.
9. Test in Play. Check Weapon Equipment > Weapon Attachment Error if it fails.

SH is single-handed and DH is double-handed in the current imported animation names. These prefixes are clip names, not automatic weapon-type detection. Each gun's blendspace/socket choice supplies that relationship.

## 7. Place the muzzle visually — no bones needed

1. Double-click the gun FBX/GLB in Assets to open the Model window.
2. Click **Add empty point** and name it `Muzzle`.
3. Choose **Model root (no bone)** as its parent for a static gun.
4. Move it to the barrel tip using the gizmo. Orbit with RMB, pan with MMB and zoom with the wheel to inspect from different angles.
5. Attachment points save automatically in model metadata; the source FBX/GLB is not rewritten.
6. Select the scene gun > Weapon Definition. Set **Muzzle Source = Named Model Point** and **Muzzle Point = Muzzle**.
7. Clear **Muzzle Object** if it still refers to a scene Empty: an assigned object takes priority over a named model point.
8. Fire while stationary and moving. The flash and bullet should start at the barrel.

For an instance-specific muzzle, create an Empty **under the gun**, position it visually and choose it in Muzzle Object. It must follow the gun. Named model points are preferable when every instance should share the same barrel placement.

When moving project files manually, retain `.byteengine/ModelSockets` metadata with the model assets. Grip socket and muzzle point are different: the grip attaches the gun to the hand; the muzzle chooses the bullet/flash origin.

## 8. Configure projectiles and trails

1. Choose **Shot Mode = Projectile**.
2. Leave **Projectile Blueprint empty** for the standard bullet. The system creates the projectile and its trail; do not manually add Bullet Trail to every gun or spawn a second bullet in an event.
3. Set speed, radius, visual size, lifetime and the active-projectile budget using the starter table.
4. Enable **Show Shot Trace**. Despite its name, this also controls the projectile trail.
5. Set **Projectile Trail Length** and **Trace Width**.
6. Choose **Trail Core Color** as a bright, nearly white warm colour. Choose **Trail Glow Color** for the outer colour and opacity.
7. Start with **Trail Glow Scale = 3.2**, **Trail Brightness = 1.5** and **Trail Impact Fade Seconds = 0.07**.
8. Leave **Show Camera Aim Ray off** for normal gameplay.
9. Fire past a wall edge and at a distant target. Check the muzzle origin, visible travel and cover blocking.

Damage happens when the bullet arrives. Fast bullets sweep their path to avoid skipping thin cover. Lifetime is also bounded by range/speed. Armour and weak points are checked at impact, so a weak point can close during flight.

The built-in trail uses a bright core and tapered additive glow with depth testing. It uses cached primitive geometry rather than building a particle atlas for every shot. Longer, wider and more opaque trails can obscure targets; tune at the actual gameplay camera distance.

**Trace Seconds is for hitscan streaks.** For projectile appearance use trail length and impact fade. A custom Projectile Blueprint is an advanced option: its own destruction behaviour is retained, so the built-in impact fade does not automatically override it. Establish a working standard bullet first.

## 9. Recoil, accuracy and muzzle VFX

1. Under Definition > Accuracy, set base inaccuracy, moving inaccuracy, bloom per shot, maximum bloom and recovery speed/delay.
2. Under Recoil, set pitch/yaw per shot, maximum recoil and recovery. Begin with small values and increase gradually.
3. Optional: add **Weapon Recoil Visual** on the gun root. Choose a mesh/model **descendant** as Visual Object. It cannot be the same root. Start with Kick Distance 0.025, Kick Tilt 2 degrees and Return Sharpness 25.
4. Do not animate or kick the grip socket itself. Keep alignment and temporary visual kick separate.
5. Enable **Muzzle Flash Enabled** and choose a native `.bvfx` in **Muzzle Flash Effect**. Existing examples are under `Assets/VFX/Gun Shooting Kit`.
6. Adjust Muzzle Flash Size, Muzzle Light Intensity and Muzzle Light Range.
7. Edit the `.bvfx` to change its layers, textures, colour and timing. Reuse the firing system's muzzle binding rather than placing another independent effect.

Current kit artwork has attribution requirements; keep its existing project credits when distributing it. Your own art can replace it without changing the weapon logic.

## 10. Build the input Event Sheet

Create/attach an Event Sheet to the player, or reuse `Assets/Events/Weapons/Weapon Controls.byteevents` if present in your project. Find weapon entries by searching their names in Add condition/action under **Modular Weapon Gameplay**. Native input conditions are under Input.

Each row below is **one event**. All listed conditions are AND conditions unless a nested OR is explicitly desired. Set every equipment action target to the player.

| Event name | Conditions | Action |
| --- | --- | --- |
| E - pick up nearest weapon | Key E pressed; Game input is captured | Pick up weapon; Use nearest eligible pickup on; Equip after pickup on |
| G - drop equipped weapon | Key G pressed; Game input is captured | Drop equipped weapon |
| 1 - equip first carried weapon | Key 1 pressed; Game input is captured | Equip carried weapon by inventory position; position 0 |
| 2 - equip second carried weapon | Key 2 pressed; Game input is captured | Same action; position 1 |
| Hold left mouse - automatic fire | Left mouse held; Game input is captured; Weapon uses automatic trigger; Equipped weapon can fire | Fire equipped weapon |
| Click left mouse - single shot | Left mouse pressed; Game input is captured; Weapon uses single-shot trigger; Equipped weapon can fire | Fire equipped weapon |
| R - reload | Key R pressed; Game input is captured | Reload equipped weapon |

Use **Listen** in a keyboard condition to capture a different key. A captured-input guard prevents gameplay interaction while editor/UI input owns the mouse; it is not a requirement that the gun itself be captured or selected.

For pickup validation, you can add **Can pick up weapon (range and carry limit)** targeting the player with nearest enabled. During debugging, leaving it out lets the pickup action report its failure reason. Do not add a ray-hit requirement to a nearest pickup rule.

**Slot versus carried position:** Definition Slot is the gun's stable identity. Carried position is its current position in the owned inventory. Use carried position for hotkeys when the HUD shows only collected weapons. `Equip weapon slot` is useful when intentionally selecting a specific definition slot.

For a reward gun, use **Give starting or reward weapon**, choose the player equipment and the gun instance, then decide Equip now. The gun must exist in the same scene. This is not a Blueprint-spawning action.

## 11. Make animation follow equipment

1. In the Animation Profile create populated **Unarmed**, **One Hand** and **Two Hand** blendspaces using compatible clips from the same rig.
2. For directional movement use actual local Right/Forward velocity axes and correctly placed forward/back/left/right samples.
3. On each gun choose its **Locomotion Blend Space**. The pistol can choose One Hand; a rifle can choose Two Hand. Pickup/equip switches this automatically. Dropping the last gun restores the equipment's unarmed choice.
4. Add stable aim layers for armed stances. Limit One Hand Aim to the One Hand blendspace and Two Hand Aim to Two Hand.
5. Mask the upper body from the first suitable spine bone. Palclone uses **Spine_01**. Enable Stabilize Torso Rotation where the locomotion hips otherwise twist the weapon.
6. Add a **Weapon Actions** layer above the aim layers with the same upper-body mask.
7. On Actions create named fire, reload and equip actions using the correct SH/DH clips. Route moving fire/reload actions to Weapon Actions rather than FullBody.
8. In gun presentation events use **Weapon fired**, **Reload started**, **Reload completed**, **Reload cancelled** and **Weapon equipped** as the relevant triggers. Target this gun for the condition and the player Animation Controller for Play Named Action.
9. For empty and nonempty reload variants use Current reload started empty where needed. On cancellation stop/cancel the reload action so it cannot continue over firing.
10. Test walking, diagonal strafing and sprinting while firing and reloading. Legs should keep locomotion while arms play their action.

Match Playback To Speed is a fallback for missing sprint strafes. Adjust the profile's playback bounds to avoid frantic cycles; palclone's sprint-strafe cap was tuned separately from forward sprint. Do not speed up every animation globally to fix one direction.

Turn-in-place settings are under **Locomotion > Turn In Place**. Set appropriate clips for each stance and angle/speed thresholds. Movement controller facing options determine whether the character strafes toward the cursor or turns toward movement. A blendspace alone does not decide facing.

## 12. Add shooting and reload sounds

1. Create native Audio Source template objects for shot and reload clips. Use separate sources so a reload can be stopped independently.
2. Choose your audio assets, volume and spatial settings on those objects.
3. Add a gun-specific **Weapon fired** event and play the shot sound using the native one-shot audio action. Use overlapping voices for automatic shots rather than restarting one long sound.
4. On **Reload started**, play the reload source. On **Reload cancelled**, stop it; also check whether completion needs an explicit stop for your chosen clip.
5. Keep the sound timing consistent with actual reload timing. Per-shell reloads may need a different presentation from magazine reloads.
6. Copy the working pattern from `Assets/Events/Weapon Audio and Auto Reload.byteevents`, updating gun/source references rather than replacing all its rules.

There are no automatic sound-clip fields on Weapon Definition in this version. Audio is intentionally wired through editable Event Sheets and Audio Source components. Existing sound objects are under **First Hunt - encounter director**, named after the gun and shot/reload role.

## 13. Build the weapon HUD

### Ammo/name text

1. Create native UI Text objects for ammo, weapon name and optional reload state.
2. Add an **Every update** event, or use **Weapon HUD values changed** for text updates.
3. Add **Show weapon information as text**, targeting player equipment and your destination Text object.
4. Use `{ammo} / {capacity}` for loaded rounds/capacity, `{name}` for the name, `{state}` for RELOADING, or `{ammo} / {reserve}` for loaded/reserve rounds.
5. Set When unarmed to an empty string or your chosen placeholder. Style the actual Text object for fonts, colours and placement.

Other supported placeholders are `{reserve}` and `{reserveClips}`. Reserve clips is reserve rounds divided by magazine capacity and may be fractional. It is not a separate stored magazine inventory.

### Only show owned guns, with reserve markers

1. Add **Weapon Inventory HUD** to a HUD object under your canvas.
2. Choose **Equipment Object** as the player.
3. Create native UI templates: a card **UiWidget**, a hotkey **UiText**, and a reserve **UiWidget**. Bind Card Template, Key Template and Reserve Template.
4. Style their offsets, sizes, card artwork and reserve marker appearance. Keep the template scene objects inactive so they do not display as extra inventory items, but leave the template components enabled and the hotkey Text component Visible. Refresh creates active display objects from their component settings.
5. Assign each gun's **Hud Icon** in Weapon Definition.
6. Set Stack Offset, Hotkey Labels, Selected Color and Stored Color on Weapon Inventory HUD.
7. In an Every update event add **Show carried weapons and reserve magazines**, targeting the HUD object containing Weapon Inventory HUD, not the player.
8. Pick up/drop/switch guns. Only carried weapons should generate cards; the selected weapon is nearest the ammo readout.

Reserve markers are a segmented bar. One full segment represents one magazine's worth of reserve ammunition; fractional reserves partially fill a segment. For a handmade single bar use **Update reserve magazine markers** and select your UiWidget.

## 14. Health, energy, dash and hit feedback

These are separate systems and can be reused without changing a gun.

- **Player health:** add/configure Health on the player. Create a UI bar and Text; in a HUD update event use the health-to-bar/text actions, choosing the player as health owner and the widgets as destinations.
- **Energy:** configure the player's resource component and regeneration. Bind resource-to-bar/text actions to that resource owner. Match the bar maximum to the intended resource range.
- **Dash:** use the native Dash Ability component, configure distance/duration, cooldown and resource cost, and use Key Space pressed plus Dash is ready (energy and cooldown), then Activate dash targeting the player. Copy palclone's working dash rule first, preserving resource and input guards. Style the cooldown/status UI separately.
- **Targets:** add a collider and Health. Check that weapon Hit Mask Bits include the target/cover layers. A rendered mesh alone is not a damage collider.
- **Damage numbers and knockback:** select **Combat feedback - damage numbers and knockback** and edit Combat Hit Feedback. Number colours, size, rise, lifetime and knockback are editable there. Each victim's Health > Knockback Multiplier controls resistance.
- **Weak points:** the boss uses Combat Armour plus a Damage Region linked to the health owner. Hits must reach the exposed region collider. Ordinary guns need no boss-name special cases.

Use **Weapon shot hit** for actual hit presentation and Last Ray Hit data. With projectiles this happens at impact. Use **Weapon fired** for muzzle/sound/shot animation at launch. Do not add another damage action to a normal projectile hit event: the projectile already applies its damage. `Hitscan shot missed` does not detect projectile expiry.

## 15. What is exposed, and what still needs care

| Need | Where to change it |
| --- | --- |
| Fixed damage/fire rate/spread/capacities/reload | Weapon Definition |
| Starting/current ammunition | Weapon Instance; Set magazine and reserve ammo / Add reserve ammo actions |
| Change damage/rate/spread while playing | Set damage, fire rate and spread |
| Change accuracy/recoil while playing | Set weapon accuracy and recoil |
| Change firing simulation | Use projectile / hitscan mode |
| Change bullet speed/length/width/visibility | Set projectile speed and trail |
| Change glow/opacity/scale/impact fade | Set bullet trail appearance |
| Fine settings such as core colour, radius, budget | Definition inspector; these do not all have dedicated named weapon actions |
| Fire/reload/equip/pickup/drop | Named Weapon Equipment / Weapon Pickup actions |
| Hand alignment / locomotion | Character profile and per-gun selections |
| Flash artwork / audio / HUD appearance | VFX assets, Audio Sources and native UI objects |

The reusable projectile code implements flight, collision and cleanup. Your project supplies the values and Event Sheets. Not every public setting has a bespoke action; do not assume a named event action exists just because a property is editable. No extra game-specific code is required for this tutorial's standard guns.

## 16. Troubleshooting

| Symptom | Check in this order |
| --- | --- |
| E does nothing | Gameplay input captured; event targets player equipment; pickup active; correct/self Weapon Rig; horizontal radius; height difference; carry limit; unique slot |
| Cannot tell why pickup fails | Weapon Pickup Binding Error, Weapon Equipment Last Pickup Result, Character Motion Debug HUD (F8 in palclone) |
| Starts with unwanted weapons | Start In Inventory on preloaded guns, Default Starting Weapon, Starting Slot, startup reward events |
| Gun drops below floor/disappears | Pickup binding, Drop Ground Clearance, restored scale and collider/rigid-body state |
| Gun differs from socket preview | Same imported model root, matching Grip Socket, no extra transform wrapper, correct character controller |
| Flash starts at player/wrong barrel | Muzzle Object override, named point/model binding, point at barrel tip, gun hierarchy |
| No trail | Projectile mode, Show Shot Trace, nonzero length/width/opacity, lifetime/range, camera visibility |
| Very thick trails cover targets | Reduce width, glow scale and opacity before adding more effects |
| Gun stops firing under load | Active projectile budget, lifetime/range, ammo and cooldown |
| Walk freezes during fire/reload | FullBody action mask, missing Weapon Actions layer, wrong animation target |
| Rifle shakes while moving | Stable stance aim layer, torso stabilization, upper-body mask root, action crossfades; do not change socket every shot |
| Damage applied twice | Extra damage event in addition to projectile damage |
| HUD shows guns never collected | Static visible cards/templates or old slot rules; use carried inventory refresh |
| Works in editor, export fails | Refresh matching Windows player runtime, enable project runtime plugin, re-export; do not copy only an old executable |

## 17. Final acceptance test

1. Start empty: HUD has no gun cards. E picks up the intended gun.
2. Reach the carry limit and confirm another pickup is rejected clearly.
3. Switch weapons: correct icon, ammo, grip and locomotion stance appear.
4. Fire once: ammo decrements once, sound/flash happen immediately, target damage arrives with the bullet.
5. Fire at thin cover: it blocks the bullet and the enemy behind it is unharmed.
6. Walk, sprint and strafe while firing/reloading: locomotion continues and the gun stays stable.
7. Empty the magazine: automatic reload consumes reserve correctly. Test manual reload and shell interruption if used.
8. Drop and recollect: the gun remains visible, ammo is preserved and HUD cards update.
9. Hit an exposed weak point and then a protected body: feedback differs correctly.
10. Save, reopen and retest. Export only when ready; an existing exported game does not receive scene/plugin changes automatically.

## Further reference

- Gun-Projectiles-and-Trails.md — projectile/trail implementation and tested behaviour.
- Weapon-Locomotion.md — detailed layers, sockets, blendspace timing and diagnostics; some earlier sections describe the older linked-rig workflow, so use this tutorial's self-contained world gun for new setups.
- Model-Attachment-Points.md — model viewer and muzzle metadata.
- Combat-Feedback-and-Audio.md — audio objects, hit feedback and auto-reload rules.
- Combat-HUD-Dash-Recoil.md — HUD, dash and recoil implementation.
- Crownback-Openings.md — armour and exposed back-core authoring.
