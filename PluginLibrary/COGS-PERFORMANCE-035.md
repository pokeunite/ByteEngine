# Cog mesh and workshop performance — 0.3.5

Installed in Goblin Scraper. Restart ByteEngine to reload the plugin and models.

Cogs use matched involute tooth profiles: 12 teeth for medium, 24 for large, pitch radii .542 / 1.084 m. Native build visuals apply tooth phase offsets; physics retains those offsets in play. Mesh detection requires parallel same-facing axes, coplanar discs within .04 m and centre separation within .065 m of the pitch-radius sum. Rotation is transferred by an angular gear constraint, not simulated tooth contacts. The large follower rotates at half input speed. Positions outside those limits do not transfer motion. Gear envelopes may overlap when these rules confirm a valid mesh.

Useful supported mechanisms: rigid sweep arms mounted to the moving output; opposite rotating outputs; same-direction three-cog trains; slower rotating weapon platforms. Hold F. Independent wheel joints do not yet support a keyed gear-to-wheel shaft: a vehicle gearbox is not complete. Impact effectiveness and torque capacity are not rated by these tests.

Build mode now caches catalogue arrays and occupied connectors between edits, indexes connector proximity spatially, avoids a duplicate idle preview pass, and only displays connector markers on the hovered part. Moving a branch still ignores its own connectors correctly. Workshop shadows use 1024 instead of 2048. Yard geometry omits the covered concrete under the raised build pad so it is not shaded twice.

Measurements on this machine: 81 blocks / 1126 connectors: original occupancy audit 450.05 ms, indexed audit 20.00 ms, identical results. These are edit-time audits; idle mode now reuses their results. Native 9-block scene update averages .795 ms. Synchronized 1280x720 rendering plus update averages 16.96 ms / 58.9 FPS, excluding editor panels. This is not a guarantee of 60 FPS; rendering remains the main cost and higher resolutions need further profiling. Earlier native runs varied from 17.41 to 23.39 ms; do not treat the runs as a controlled GPU before/after comparison.

Validation: release build clean; rejected separated/non-coplanar gears; tooth phase algebra; rotated equal cog pair; three-cog train; actual beam-mounted reducer 2:1; sweep arm output; native model import, placement, brace endpoint interaction, undo/redo, disk save/reload, toolbar tools, multiple HUD resolutions, and build/play/build transition.

Original models/catalogues/plugin backed up at Documents/Goblin Scraper/Backups/cogs-performance-035. Editable cog Blender files and exports are in Downloads/parts/refined/matched-cogs-035. Current plugin and this note are saved in Downloads/ByteEngine Plugins and repository PluginLibrary. Updated diagrams remain in Downloads/Goblin Scraper - Gear Blueprints/GEAR-WORKSHOP.html.
