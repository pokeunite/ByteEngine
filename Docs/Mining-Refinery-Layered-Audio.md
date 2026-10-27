# Refinery sound pass

Installed in palclone with Modular Weapon Gameplay 3.37.0.

The refinery now has three independently controlled layers:

1. **Processor:** filtered factory machinery recording while a batch is being processed. Uses the existing `Refinery loop` binding and `Machine volume × Refinery volume multiplier`.
2. **Conveyor:** a separate filtered/resampled mechanical crank recording at the centre of the actual belt. Runs during processing and continues while the visible output is travelling, then fades out. `Refinery conveyor loop` and `Refinery conveyor volume` are editable.
3. **Bucket impacts:** three heavier metal/clatter variations, selected with slight pitch variation, at the physical bin. Triggered by `MiningRefineryOutput3D.LandedIngotSequence` when a travelling ingot lands, not by inventory changes, timers or opening the menu. `Refinery ore impact 1–3` and `Refinery ore volume` are editable.

Opening the refinery now uses a short mechanical switch sound instead of the notification chime. Closing any open business panel uses a separate latch click, including Close, Escape and E-to-close through the same Close method. Calling Close on an already closed panel is silent. Approved notification, purchase and weapon recordings are retained.

All bindings are on **Mining Audio**, on the **Mining company operation** scene object. The `Refinery layers`, `Machine loops`, `Mix` and `Interface sounds` property groups hold the settings. Event Sheets' **Play mining sound cue** action includes `RefineryOpen`, `PanelClose` and `RefineryOreImpact`.

Runtime files are in `Assets/Audio/Mining/Refinery`. Provenance is in `SourceAudio/Refinery/SOURCES.json`. These are edits of existing CC0 recordings already in the project: Dominik_W's factory machine, AslakHostaker's crank, RyanKingArt's metal impact, BlondPanda's small metal roll and Nanashi's mechanical trigger. Original recordings are preserved. No additional downloads or attribution obligations were introduced.

`BuildRefineryAudio.py` reproduces the edits and writes a short mixed audition. `MiningPrototype --verify-refinery-audio` tests idle silence, menu cues, independent live loops, conveyor delivery tail, real landing-triggered impacts, idle fades, stock reduction, bounded voices and shutdown cleanup. `--verify-audio` checks the existing audio system and native WAV decoding too.

No game export was produced. Scene and plugin backups are in the project's `.byteengine/Backups` directory.
