# Ammo press sound pass

Installed in palclone with Modular Weapon Gameplay 3.38.0. Original recordings remain intact. The edited sounds use existing CC0 project sources, with provenance in `SourceAudio/Ammo Press/SOURCES.json`.

## Layers

- **Motor:** separate filtered industrial motor bed while ammunition is being produced. Uses `Press loop` and `Machine volume × Press volume multiplier`.
- **Ram:** short mechanical stamp at contact in each animated stroke. Reads the imported model's playback time; the authored `Operate` clip has three strokes, with bottom contact halfway through each stroke. `Press strokes per clip`, `Press stroke contact phase`, `Press ram bone`, clip and volume are editable. Pausing the ram stops stamping; resuming starts a fresh timing observation rather than replaying missed strokes.
- **Conveyor:** separate moving mechanical loop located between the model's belt markers. Continues while the final visible rounds travel after the hopper fills, then fades out.
- **Tray:** three small-metal clink variations, with slight pitch variation, triggered when a visible round reaches the collection tray. Stock generation and collection do not trigger impacts. The old production-time clink is disabled when the new impact binding is assigned, avoiding doubled sound.

Opening the ammo-press menu uses a short mechanical switch click. Close, Escape and E-to-close retain the shared latch cue added in the refinery pass.

All controls are on **Mining Audio** on the **Mining company operation** scene object. Use the **Ammo press layers**, **Machine loops**, **Mix** and **Interface sounds** property groups. Event Sheets' **Play mining sound cue** includes `PressOpen`, `PressStroke` and `PressBucketImpact`.

Runtime audio: `Assets/Audio/Mining/Ammo Press`. `BuildPressAudio.py` reproduces the edits and mixed preview. `MiningPrototype --verify-press-audio` checks live independent voices, animated stroke timing including clip wrap, landing synchronization, storage-full stops, final delivery tail, collection, restart, bounded voices and shutdown. The existing general-audio and refinery-audio checks cover coexistence.

Scene/plugin backups are in `.byteengine/Backups`. No game export was produced.
