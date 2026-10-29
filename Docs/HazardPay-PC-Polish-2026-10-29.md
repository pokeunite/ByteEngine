# Hazard Pay — PC playtest fixes

## Presentation and sound

* Permanent-upgrade confirmation has a dedicated strip between the map and the
  selected upgrade details. It replaces the navigation hint temporarily, without
  painting over the description, rank or purchase control.
* Camp-arrangement instructions use a smaller upper-left card and a compact
  centered bottom strip. Object nameplates size to their text.
* Menu music uses unscaled time and the already selected PYNCHON camp track.
  Menu hover reuses the terminal selection recording at 22% of the interface
  mix, once on entry rather than every frame. Gain remains editable.

## Shooting and enemy pressure

* Spider body damage regions are separate trigger hitboxes. Size and center are
  editable on the spider animation component, without enlarging the movement
  capsule or blocking the player.
* Weapon aiming sees those regions. Projectiles ignore ordinary pickup volumes,
  which previously could absorb a shot. Solid movement/camera queries continue
  to ignore trigger hitboxes. Existing raycast method signatures remain intact
  for compiled plugins.
* The third-person reticle reflects the same accumulated recoil applied to the
  shot. Pistol spread, movement inaccuracy and bloom are reduced in the authored
  weapon component, retaining visible recoil and editable accuracy settings.
* Raider/hunter per-hit damage is explicit on the operation component. Raider
  damage is 6 before the chosen contract multiplier; raider cooldown is 1.4 s
  and recovery .65 s. Three attack slots limit simultaneous hits. Merely reaching
  operation three does not multiply damage: the original pressure increase came
  from more enemies and the selected contract.

## Machine progression

| Upgrade | First cost | Growth per rank |
| --- | ---: | ---: |
| Press cycle speed | 140 cr | 2.15x |
| Press hopper | 110 cr | 2.15x |
| Press payload | 180 cr | 2.15x |
| Refinery batch | 130 cr | 2.15x |
| Refinery speed | 160 cr | 2.15x |
| Refinery yield | 240 cr | 2.15x |

Press payload adds 20% per level instead of doubling production on the first
cheap purchase. Bucket starts at 60 rounds, gaining 30 per storage rank. Costs,
growth and production remain component properties. Player saves are preserved;
already purchased ranks are not removed. A shallow operation's base 18 ore is
worth 90 cr refined before price variation, making purchases span operations
rather than allowing several immediately.

## Movement investigation

The deterministic sprint probe traversed approximately 90 m in four open lanes
without a stop. Another lane hit `R09_low boulder 0 collision`, a broad box around
the visible rock. Small R09/R10 cover rocks now use their rendered mesh geometry
for collision; cliff boundary boxes are retained. This addresses invisible box
corners, but does not establish the cause of every reported intermittent stop.

## Verification and delivery

Gameplay regressions include damage-only trigger targeting, pickup-volume
pass-through, high-speed projectile impact, idempotent damage setup and price
defaults. Native tests confirmed music while paused and a quiet, nonrepeating
hover cue. Actual Windows screenshots of research purchase feedback and camp
arrangement are under `artifacts/mining-research-ui.png` and `mining-hud.png`.
The scene backup is in `artifacts/pc-polish-backup`. No game export or save reset
was requested or performed. This pass targets Windows; the browser player was
not republished.

## Explorer concept

Built-in ImageGen created `Docs/Art/HazardPay-Explorer-Tpose.png`.
Prompt: original full-body front orthographic space explorer in strict T-pose;
premium polished low-poly game art; practical mustard-yellow and charcoal
industrial protective suit, off-white undersuit, boots/gloves, utility belt,
compact life-support backpack and closed helmet with dark teal visor and subtle
cyan indicators; symmetrical neutral stance; entire figure visible; neutral
light-grey studio background; no text, logos, handheld equipment or scenery.
