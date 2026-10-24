# Crownback openings and focused HUD

The fight now tests **bait → evade → reposition → shoot the exposed back core**. Boss health remains 900; it was not increased to stretch the fight.

## Damage rules

- The body takes **4%** of normal damage. A 25-damage pistol shot removes 1 HP and shows a small grey number, a brief grey flash and a metallic impact cue.
- A missed charge or obstacle-stopped charge creates a **1.65-second recovery**. Slam landings also create recovery. A charge that hits the player does not expose the core.
- Only the separate gold target on the boss's **back** bypasses armour during an opening. A 25-damage pistol shot hitting it removes 50 HP, with a gold number, stronger impact cue and gold body flash.
- Body/front hits remain armoured while the core is exposed. This is not a whole-body damage multiplier.
- The core disappears and its collider stops participating as soon as recovery closes. Reset and death also close it.

The rear target is positioned at the normal gun-aim height so it can be hit with the existing top-down aiming system. It follows the boss's orientation, so dodging past a charge naturally puts the player nearer its back.

## Attack language and enrage

Charges show a red ground strip along the locked direction. Slams retain a marked landing area. Missed attacks create the gold-core opportunity. Existing adaptive charge, leap and feint choices remain.

Below **35% health**, the boss turns a stronger red and its attacks/cooldowns speed up by **1.25x**. Recovery remains useful. The feint's separate follow-up warning keeps its normal escape timing. Landing points still lock before the leap; no invisible tracking was added.

The 60–120-second boss length is still a playtest target, not a measured result. First check whether players recognise armour, bait attacks and aim at the opening. Perfect aiming and different weapons will produce different completion times.

## Editing without code

Select **Crownback - Boss 11**:

- **Armour and Weak Point** controls armour reduction, core damage multiplier, linked weak-point object, enrage threshold/speed, hit sounds, flash duration and status text/labels.
- **Simple Enemy AI** controls recovery duration, attack timings, the visible charge path and the existing adaptive attack behaviour. Recovery Damage Multiplier is 1 on this boss; the separate core owns its damage bonus.
- **Health** remains 900.

The child **Crownback back core - shoot when gold** contains its editable box collider, gold visual, light and **Damage Region** pointing to the boss's health. Move/scale this child to reposition the target. Keep it outside the rear body surface so shots can reach it.

Event Sheets expose **Weak point is exposed** and **Set weak point exposure**, alongside existing phase and learned-attack conditions. Hitscan weapons and native projectiles both route region hits to the health owner. Generic direct damage remains an armoured body hit.

The weapon plugin is **3.15.0**. The editor and bundled Windows player runtime are synchronized. Re-export to include this new scene and plugin in a standalone game; an earlier exported Game.bytepak is unchanged.

## HUD

One short objective appears at a time. Locked ability slots and their labels are hidden; the usable dash is centred above health/energy. Health, energy, carried weapons and ammunition remain. The boss's short status explicitly distinguishes armour, an exposed core and enrage.

Press **F1** to show/hide the enemy colour key and controls. The help toggle and objective wording are authored in `Assets/Events/First Hunt.byteevents`. Status text is linked through the armour component, with editable labels.

## Sound sources

The two short hit cues are trimmed derivatives of the existing CC0 recordings: SpringySpringo's rifle reload and The Free Firearm Sound Library's shotgun recording. See `Weapon-Audio-Sources.md` for creators and source pages. No new restrictive licence was introduced. Clips are in `Assets/Audio/Combat Feedback` and can be replaced independently.

## Verification

Actual weapon-ray tests verify closed-core armour, front/body protection during exposure, 50-damage rear-core hits, restoration on closure, enrage and reset. The full native combat suite passes. A saved-game render/fire check confirms 1-damage armour hits, 50-damage core hits and the new visibility rules. The complete hunt, checkpoint retry and victory flow pass without event warnings. The native Windows player is rebuilt alongside the editor to keep future exports compatible.
