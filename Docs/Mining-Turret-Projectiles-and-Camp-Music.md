# Turret projectiles and camp music (3.40.0)

Sentry and repeater turrets now launch swept bullets. Damage occurs on impact, with the same bright-core, tapered-glow Bullet Trail used by the pistol. Cover blocks flight and piercing research continues through successive enemies. Bullets have a range lifetime and a per-turret count limit; they are cleaned up when their turret stops.

## Editing

Select the player and open Company Defence Planner to set Defence Projectile Speed (75), Defence Projectile Radius (0.025), Defence Tracer Length (2), and Defence Tracer Width (0.035) for newly placed and restored turrets. A placed Company Defence also exposes its own Projectiles section. Existing fire-rate, damage, range and piercing upgrades still apply.

On Mining company operation > Mining Effects, Mine Explosion Scale is 1.8 and Mortar Explosion Scale is 2.3. These increase visuals without changing damage radius, force, particle count or sprite resolution. Change the gameplay blast radius separately in the planner if desired.

Mining company operation > Mining Audio > Music contains Camp Music, Camp Music Volume (0.38), Music Fade Seconds (1.5), and Music Enabled. Music is non-spatial and uses the Music mixer bus. PYNCHON loops during Preparation, fades out during drilling, failure or a roaming-enemy encounter, then returns when the camp is safe. No battle track is assigned yet.

Event Sheet actions: Set camp music volume; Set mine and mortar explosion sizes; Set turret projectile speed and tracer. Normal component-property actions can edit other settings.

## Music source

PYNCHON by James Gargette (cinameng), CC0: https://opengameart.org/content/pynchon
Original MP3 retained in SourceArt/Camp Music. Runtime asset is stereo 48 kHz 16-bit PCM WAV, with transparent linear gain to 0.8 peak. No compression applied.

## Verification

503 weapon/gameplay regression checks passed. Native project verification additionally checks actual camp playback and Music-bus routing, moderate volume, saved explosion scales, roaming-combat and drilling fades, delayed bullet damage, flight movement, one impact per victim, thin cover, piercing upgrades, projectile bounds and expiry, scene shutdown and unchanged scene/progress files.

Only the project and editor plugin packages were updated. Existing standalone exports are unchanged.
