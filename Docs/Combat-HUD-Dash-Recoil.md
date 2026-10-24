# Combat HUD, dash and weapon feel

Palclone uses the native UiCanvas, UiWidget, UiText, UiAnimator, Health and Resource Meter components. All visual elements are separate scene objects under `HUD canvas`. No game-specific C# HUD/controller was added.

The bottom-center HUD has mirrored red HP and cyan energy bars with an original bag emblem. Five ability slots sit above; Dash is functional and the other four are locked placeholders. Weapon cards at the lower right indicate ownership and selection, with separate name, magazine/reserve ammunition and reload text. Artwork lives in the project at `Assets/UI/CombatHUD`; original reusable artwork and its generator live in `Plugins/KarlsonDemo/Samples/Hud`.

`Assets/Events/Combat HUD and Dash.byteevents` owns HUD bindings, Space activation, energy regeneration and trail emission. Open that sheet to change policy or input. In this top-down project Space replaces jumping; Top Down Controller > Allow Jump is disabled to prevent conflicting actions. Existing E/G, weapon selection and mouse controls are retained. F8 still toggles the optional movement diagnostic overlay, which starts hidden.

## Native dash

Add **Dash Ability** beside Character Controller and Resource Meter. The component exposes distance, duration, cooldown, energy cost, resource owner, direction mode, grounding requirement and regeneration delay. Defaults authored in palclone are 4.2m / 0.18s, 25 energy, 1.25s cooldown, 0.8s regeneration delay. An Event Sheet restores 14 energy per second after the delay. The motor sweeps collisions and retains gravity; ending a dash returns directly to ordinary movement speed rather than coasting at dash speed.

Event conditions: Dash Is Ready, Character Is Dashing, Energy Regeneration Delay Has Ended. Actions: Activate Dash, Cancel Dash, Configure Dash, Update Dash Cooldown And Ready Status. Read-only component fields include cooldown seconds/fraction, activation count, availability reason and last activation result. Dash does not grant hidden invulnerability.

The editable `Assets/VFX/Dash Slipstream.bvfx` uses a bounded 64-particle budget. Events start emission once per dash and stop it while allowing existing particles to fade. Icon pulse, cooldown fill and energy spending reflect actual ability state.

## Weapon accuracy and recoil

Select each world gun's **Weapon Definition** to tune Accuracy and Recoil categories. Base Inaccuracy and movement penalty combine with existing pellet spread. Bloom grows per accepted shot and recovers independently. Pitch/yaw recoil affects subsequent shot trajectories and has a configurable cap/recovery rate. Defaults of zero preserve older projects. Palclone's three weapons have distinct initial tuning.

**Weapon Recoil Visual** applies a small kick to a chosen imported model child. The grip socket and character animation stay untouched. Kick distance is in physical metres, with editable tilt and return sharpness. It is optional and separate from ballistic recoil.

Event Sheets expose Set Weapon Accuracy And Recoil, formatted weapon HUD text, slot icon/selection updates and readouts for inaccuracy, bloom and recoil angles. Weapon Definition > HUD Icon can be replaced with another texture. The ammo format accepts `{name}`, `{ammo}`, `{reserve}`, `{capacity}`, `{state}` and a configurable unarmed fallback.

## UI enhancements

Progress bars accept an Image Reference and crop the texture as values change, preserving bevels and artwork proportions. Fill Direction supports left/right and up/down depletion. Existing rectangular bars remain compatible. Widget image, colors, size, order and anchors remain editable. The HUD scales with a 1280x720 reference canvas and safe-area insets.


## Carried inventory HUD (weapon plugin 3.12)

The palclone canvas uses **Weapon Inventory HUD**. Its three template pickers point to inactive native UI objects: **HUD weapon-card-template**, **HUD weapon-key-template**, and **HUD reserve-template**. Edit their size, position, fonts and colors in the normal UI inspector. The canvas component exposes stack offset, selected/stored tint and comma-separated hotkey labels. The Event Sheet **Combat HUD and Dash → HUD - carried weapons and reserve magazines** calls **Refresh carried weapon HUD** every update. This binding creates native cards only for owned weapons and removes cards after drops. The selected weapon appears nearest the ammo readout. Each gun's **Weapon Definition → Hud Icon** supplies its card artwork. This works for new guns without adding named-gun HUD rules.

Number keys use **Equip carried weapon by inventory position** (zero based), so the label follows current carried order, not a particular gun type. If you add more keys, update Weapon Controls and the HUD component's Hotkey Labels together. Empty inventory displays no cards or weapon/ammo text.

The ammo label format is **{ammo} / {capacity}**. Reserve markers show reserve ammunition in magazine equivalents: one lit marker is one magazine, with partial reserve magazines partly filled. Empty markers show remaining reserve capacity. Per-round reload weapons also use magazine equivalents. Infinite reserve lights all markers. The native **SegmentedBar** widget exposes marker count (0 = automatic), angle, length, thickness, filled and empty colors. Automatic marker count is capped at 64 for rendering cost. The separate **Update reserve magazine markers** action can bind another native UI bar to the equipped or chosen gun. **Write formatted weapon HUD text** also supports **{reserve}** (rounds) and **{reserveClips}** (fractional magazines).

### Default starting weapon

On the player, open **Weapon Equipment → Starting Equipment**. Turn **Start With No Weapons** off and select any scene gun in **Default Starting Weapon**. The gun need not be a child of inventory or within pickup range. Carry Limit still applies. Default selection takes precedence over legacy Starting Slot. To start empty, turn Start With No Weapons on; the chosen gun stays available in the world. To use the older child inventory loadout, leave the default picker empty and use each rig's Start In Inventory flag.

For a fully event-authored loadout, keep Start With No Weapons on and use **Give starting or reward weapon** with **On start**. The action accepts a scene gun picker and Equip Now, bypasses pickup distance, and respects ownership, carry limits and unique definition slots. Use **Pick up weapon** for proximity-based collection, **Drop equipped weapon** for drops, and **Set weapon carry limit** to change capacity. Default and granted world guns restore their world scale and suspended pickup physics when dropped.
