# Combat HUD sample artwork

Original transparent PNGs and deterministic authoring source, made for ByteEngine. No third-party artwork or font is included.

Use a UiCanvas at 1280x720 with Scale With Screen. Keep the bars, frame images, text, bag emblem and weapon cards as separate objects. Bar fills are UiWidget Progress Bars with an Image Reference. Right To Left can mirror depletion without stretching or recoloring the original asset. FillDirection and the image reference persist through scene save/load.

Weapon Definition > HUD Icon supplies each weapon's card. Event Sheet actions under Modular Weapon Gameplay > Weapon HUD update owned/selected tint and formatted ammo/name/reload text. The text format supports {name}, {ammo}, {reserve}, {capacity}, {state}; unarmed text is configurable.

The native Dash Ability component contains distance, duration, cooldown, energy cost, resource owner, direction and regeneration delay. Event Sheets call Activate Dash; Can Dash checks energy, cooldown, death and grounding. The character motor performs swept motion, retaining gravity/collisions and ending without extra dash-speed coasting. A separate resource regeneration event controls regeneration rate. Dash HUD feedback and VFX are also event-controlled.

Weapon accuracy/recoil use degrees. Base Inaccuracy is added to pellet Spread, Movement Inaccuracy applies while moving, Bloom accumulates per shot and recovers over time, and Pitch/Yaw recoil displace subsequent shots. Zero defaults preserve older projects. Weapon Recoil Visual optionally kicks a selected model child in physical metres, never its grip/socket or character animation. Its distance, tilt and return rate are editable. Live Recoil Angles, Accuracy Bloom and Current Inaccuracy are readable component values.
