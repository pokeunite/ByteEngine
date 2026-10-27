# Selected mining audio — 3.28.0

The 18 user-selected recordings are integrated. Gun reload files and their event bindings are retained. No new footsteps are installed: the previously reviewed footstep recording contained unwanted environmental audio.

## Sound bindings

| Gameplay event | Selected source |
|---|---|
| Pistol shot | Weapons_Handun_Shot — unfa |
| Empty trigger | Trigger 02 — Nanashi |
| Press bucket ejection | Small_Bullet_Roll_Soft_03 — BlondPanda |
| Enemy attack vocal | alien growl — pointparkcinema |
| Refinery operation | Machine in a factory — Dominik_W |
| Drill operation | Industrial Machine Drone — kkenny101 |
| Ammo press operation | Turn crank Old Metal Machine — AslakHostaker |
| Rig damage and world impacts | Metal Impact — RyanKingArt |
| Enemy damage | SFX_Impact_Bullet_Hit_Body_1 — PoundSoundUK; three shortened variations |
| Player damage | PlayerHurt2 — mrickey13 |
| Menu selection / purchase | GASP UI Confirm — Rob_Marion |
| Processing, collection and operation completion | GASP UI Notification 1 — Rob_Marion |
| Wave start, defeat and critical player health | GASP UI Alert 2 — Rob_Marion |
| Invalid transaction | Minimalist Sci-Fi UI Error — plasterbrain |
| Completed shipment | Coin — CogFireStudios |
| Critical drill health | Severe Warning Alarm — deleted_user_7709760 |
| Mine / mortar explosion | Grenade Explosion — unfa |
| Mortar launch | Gun / Cannon / Explosion — SamsterBirdies |

Sentries use the selected handgun recording for their own shots. Reloads continue through the original Weapon Audio and Auto Reload Event Sheet, including cancellation behavior.

## Editing

On the company operation object, the Mining Audio component exposes every clip reference, machine/combat/interface/alarm volumes, critical drill threshold and voice budget. Source WAVs are under Assets/Audio/Mining/Selected. The existing pistol shot Audio Source is bound to the new clip; its reload source is not changed.

Automatic Cues drives gameplay-state audio. The Play mining sound cue Event Sheet action can also trigger an individual cue. If replacing automatic behavior with authored events, disable Automatic Cues to avoid duplicate state playback. Direct transaction/weapon/defence cues remain callable separately.

Machine loops are positional and fade down when work stops. Alarms and interface sounds are listener-relative. A bounded reusable pool limits one-shots; duplicate calls in the same instant are suppressed. Damage vocals and long creature sounds are limited to prevent crowd noise overwhelming the player. Creature vocals stop when their owner dies or is removed. Scene shutdown releases observation hooks and stops all voices.

## Source quality and credits

Freesound original downloads required authentication. The publicly available HQ MP3 previews were downloaded and decoded to mono 48 kHz 16-bit PCM WAV. These are preview-derived assets, not lossless original downloads. Changing the container to WAV does not restore compression detail. Raw previews and a manifest are retained under SourceAudio/Freesound Selected for replacement with originals later.

One-shots have outer silence trimmed and conservative peak normalization; declared machine loops have a 20 ms seam blend. The six-second body-hit source was divided into short impact variations rather than replayed in full per bullet.

The body-hit sound is CC BY 4.0 and credits Marcus Dellicompagni (PoundSoundUK), links to the source/licence and identifies edits. The company upgrade map has a Credits button displaying this attribution in-game. The full source list and voluntary CC0 author credits are in Assets/Audio/Mining/Selected/ATTRIBUTION.md. The source manifest records source URLs and hashes. All other selected recordings were listed as CC0.

## Verification

Native checks decode every assigned clip, validate mono PCM format, play every cue, test spam suppression and voice limits, exercise actual damage hooks, critical drill alarm, failure shutdown and scene cleanup. UI checks cover the Credits panel and existing transactions. The authoring tool backs up the scene and verifies that reload WAV hashes remain unchanged. No game export is performed.
