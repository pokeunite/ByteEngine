# No Brakes

Replaces the fourth Anchor Relay contract. The scene retains the existing
`Scenes/AnchorRelay.bytescene` path so deployment and project references remain valid.
New independent completion ID: `no-brakes`, reward $1,800.

## Player experience

Deploy directly into a rolling chase, alongside a loaded six-wheel truck. Its
engine provides no drive and its brakes provide no assistance. Gravity,
rolling resistance, collisions and the player's cables control the truck.
Catch its socket while matching speed, manage the load before the broken
bridge, then stop both vehicles inside the recovery yard.

Playtest adjustment (0.18.1): the cargo now starts 64 m before the bridge,
up from 32 m. The player starts at z=114 and the cargo at z=110, with a level
extension above the previous starting area. The upper road is 22 m wide,
up from 16 m; the escape road is 20 m wide and opens to 28 m near the
upper anchor, up from 12 m. The broken bridge gap remains intact.
World coverage grows to 288 m using the same 192-cell coarse mesh budget
and a 1.5 m coarse spacing. The local deforming collision field retains
25 cm samples. Retry reads the scene-authored starting position.

Desert Terrain 0.8.11 fixes the hidden fine-field rut clamp and pressure scale.
Ordinary tyres now depress actual vertices, with raised shoulders and matching
collision. Game contact depth is tuned to 0.16 m with a 0.18 m maximum;
packed ground remains firmer than loose sand. Cargo wheels now stamp their
own loaded contact tracks. The actual-driving regression measures roughly
0.09 m grooves and renders a screenshot for inspection.

The bridge is a real heightfield gap. An elevated approach leads to a lower
escape road on the right. A parented steel eye on a bridge-side boulder can
redirect the rig; a second eye beside the yard offers a braking anchor.
Neither anchor is a completion checkbox. Early braking, a controlled descent
and improvised cable manoeuvres remain valid solutions. The brief identifies
the emergency and destination without prescribing the winch arrangement.

The follow camera widens with speed and includes the load when nearby.
Existing dust, impact sound and winch feedback remain active. R immediately
restarts both vehicles at speed without a scene reload. Losing the unsecured
truck below the route or overrunning the yard fails the attempt. Secured rolls
and falls can be recovered using the cables instead of failing on a short timer.
Payment requires an upright truck, a connected casualty
cable, both vehicles in the yard and both speeds below 0.8 m/s for 1.2 seconds.
The normal short reward presentation then returns to the garage.

## Technical limits and validation

The truck weighs 1,082 kg. A moving casualty hook is allowed below 30 m/s
only when relative vehicle speed is at most 9 m/s. Other contracts keep their
existing low-speed attachment rule. Cable reach and spool limits remain
12 m and 24 m. Forces act on real rigid bodies; no target steering rails or
delivery teleports are used during play.

The isolated native diagnostic checks rolling attachment, relative-speed
rejection, physical anchor redirection and dual-cable recovery back onto the
escape approach, unattended gap failure, rolling retry,
stationary yard completion, one payment, removal from Available and automatic
garage return. Yard checks deliberately reposition bodies to isolate the
completion gate; they are not an end-to-end human playthrough. Earlier pit
and dual-winch extraction regressions run alongside these checks.

Authoring script: `Tools/DuneCompany/author_no_brakes.py`. It backs up the old
scene and maps before writing the Documents project and never changes player
saves. The replacement uses a fresh completion file, so finishing the old
Anchor Relay does not hide No Brakes. New Game resets both schemas.

Desktop plugin and browser runtime must both be synchronized after source
changes. No game export is performed by this workflow.
