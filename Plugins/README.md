# First-class game plugins

- GoblinScrapper: existing block construction, physical vehicle simulation, workshop UI, serialization and game-only asset tools.
- SpaceScraper: original small SpaceBuilder3D component, serializable properties and example Event Sheet definitions. This is not a completed Space game.
- BytePlugin.targets: automatic C#/MSBuild packaging for both.

See `../Docs/PLUGIN-AUTHORING.md` for manifest, registration, import, cache, persistence and export details.

The old `plugin/` smoke source and its stale Lib snapshot were preserved under `../Backups/SpaceScraper-smoke-before-migration`; they are not active solution projects. Unrelated `Example/` files were preserved. Game-only tools moved from Tools/GoblinRefined and Tools/GoblinParts into GoblinScrapper/Tools. Older one-off patch scripts record historic paths and are retained as development history; use build_reference_v3.py and finish_reference_normals.py for the current model pipeline.
