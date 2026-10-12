# Anchor Relay and attached rock eyes

Dune Company 0.17.0 adds a fourth contract: Anchor Relay, $1200. Scene: Documents/DuneCompany/Scenes/AnchorRelay.bytescene. A zigzag canyon descends from the depot to the casualty; the recovery follows three rock anchor stations around the switchbacks. Loose sand, rising return grades and banks constrain the pull angle. Briefing describes the job and secure transfers without specifying a solved vehicle design.

Completion requires three different fixed stations while a casualty cable remains connected and the casualty is within 14m of the station. Delivery also retains the extracted, upright, connected and stationary depot gates. Reward pays once, then returns to garage after 2.5 seconds. Independent history relay-progress-v1.json is reset by New Game.

Fixed the prior anchor move bug: the authored eye name was prefixed "Contract landscape /", so the exact-name migration moved only the rock. Eyes now parent to their rocks with normalized local transforms and compensated display scale. Moving the root carries both visual and actual cable target. Nearby clutter is cleared from a 15m-radius turning area and the firm upper platform widened without changing the deep floor.

Authoring: Tools/DuneCompany/author_anchor_relay.py, backs up scenes before writing. Four-row menu fits with 32px spacing. Existing player saves and blueprint library preserved. Canonical plugin, project PluginSource and package copies synchronized. Browser runtime refreshed separately; no game export created.

Validation: native build; repaired deep-pit physical descent/ascent; parent transform propagation; physical cable pulls at all three relay stations; independent completion, $1200 payment once and automatic garage return; four available rows and canyon screenshots inspected. Relay station tests reposition between stations to isolate each pull; they do not represent a full human playthrough of the canyon.
