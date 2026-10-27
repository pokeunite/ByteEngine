# Mining sound, machine effects and explosion reactions

Installed into palclone with Modular Weapon Gameplay 3.39.0. No game build was exported.

## Sound

The drill now uses an actual pneumatic excavator jackhammer recording, mixed with a restrained motor bed. It loops only during extraction. Source: Joseph SARDIN, **Pneumatic jackhammer #5**, [BigSoundBank](https://bigsoundbank.com/pneumatic-jackhammer-5-s3129.html), CC0. The public MP3 was converted to mono 48 kHz PCM16, trimmed, filtered/resampled, seam-smoothed and gain staged.

Sentry and repeater use separate strengthened firing cues. Mortar launch and explosion cues have more body and independent volume controls. Player gunshot recordings are preserved. On **Mining Audio**, edit **Defence sounds**: `Defence volume`, `Explosion volume`, `Mortar launch volume` and `Defence hearing distance`. One-shot voices remain bounded at 24 by the authored scene.

The company terminal uses short mechanical/digital clicks for opening, selecting research/resource tabs, buying research and starting a shipment. Closing retains the shared latch sound. Approved notification cues remain available for actual notifications.

## VFX assets

Open `Assets/VFX/Mining` in the asset browser. Every `.bvfx` file is an editable native VFX asset:

- **Drill rock dust and chips:** a restrained ground dust plume and small bouncing stone chips during extraction.
- **Refinery exhaust:** light warm process vapour, with small puffs when ingots land.
- **Press compressed air / Press stamp flecks:** light air release, short flecks timed to the ram, and tiny tray puffs.
- **Turret muzzle flash:** bright flash, forward sparks and short after-smoke.
- **Defence tracer:** short depth-tested beam streak from the firing muzzle to the target.
- **Mortar shell trail:** a small smoke trail along the actual shell flight.
- **Mine blast / Mortar impact:** animated explosion, ground pressure ring, trailing soot and hot chips.

The explosion cores use the unmodified **8×8, 64-frame** half-resolution sheets from Sinestesia's [2D Explosion Animations #2](https://opengameart.org/content/2d-explosion-animations-2-frame-by-frame), CC0. The downloaded source pack and source manifest are retained. No explosion image was represented as newly authored artwork.

**Mining Effects Library** on **Mining company operation** holds the VFX references, muzzle/explosion sizes, blast launch/lift and a bounded prewarmed transient pool (40 objects by default). This moves atlas preparation to scene loading and caps effect storms. Edit the assets themselves for particle colour, rate, lifetime, flipbook, smoke and debris.

**Mining Machine Effects** on each model controls the operating VFX reference, bone or object anchor, local offset and size. Drill effects follow the model when camp placement changes. Refinery/press landing puffs follow actual delivery events. Press contact is editable through `Strokes per clip` and `Contact phase`.

## Spider blast physics

Ordinary gunshot kills still play the authored death clip. Mine and mortar explosions apply an outward/upward impulse before damage:

- Surviving spiders enter a lightweight skeletal knockdown. The current attack is cancelled; behaviour, navigation and combat are suspended while physics moves the root. They blend back and resume the previously enabled systems after recovery.
- Fatal blasts detach the visible skin onto a colliding rigid body with angular motion, preserving the reaction even when the enemy root or final wave is removed.
- Native angular-spring bone motion provides leg movement; whole-body rigid-body collision handles trajectory and bouncing. This is bounded lightweight ragdoll physics, not a separate collision body for every leg bone.

On **Animated Mining Spider**, edit `Explosion ragdoll`, `Blast knockdown seconds`, `Blast recovery seconds`, `Maximum blast knockdowns`, `Corpse seconds` and `Maximum corpses`. Defaults limit concurrent knockdowns and corpses to 12. Excess affected enemies still receive damage.

Event Sheets include **Play mining effect at object**, **Knock spider away from explosion**, and **Spider is knocked down by an explosion**, alongside the sound and animation actions. Effect playback and knockdown actions are presentation/physics; use existing damage actions if authoring a new damaging attack.

## Checks

`MiningPrototype --verify-effects` exercises downloaded flipbooks, native particle emission, machine state, drill audio, live knockdown displacement, AI suspension/recovery, fatal ballistic corpses, expiry, overload budgets, terminal cues and scene preservation. Installed-editor checks additionally run actual Play Mode, VFX rendering and spider blast physics against the installed engine assemblies. Existing weapon/event regression tests and audio/defence checks are retained.

Runtime audio is in `Assets/Audio/Mining/Effects Pass`. Provenance is in `SourceArt/Mining Effects Pass/SOURCES.json`. Scene and plugin backups are in `.byteengine/Backups`.
