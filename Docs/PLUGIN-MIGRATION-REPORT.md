# Native plugins and Goblin parts — local completion report

Completed in the current local ByteEngine checkout on 2026-10-03. No older repository files were restored. No commits, pushes or deployments were performed.

1. **Plugin host changes.** Core owns project-local loading, registration, dependency ordering, engine compatibility checks, failure containment and reverse shutdown. Core is shared with the host; declared plugin dependency assemblies retain shared type identity, and private libraries load in the plugin context. The editor uses a supported metadata bridge. Its Plugins tab shows package errors, load state, dependencies, compatibility and reopen requirements. There are no per-frame filesystem scans.

2. **Package/cache architecture.** `.byteplugin` is the canonical ZIP package stored in a project's Plugins folder. Extraction uses `.byteengine/PluginCache/<id>-<SHA256>`; enable state is adjacent and separate. Traversal, rooted/duplicate paths, symlinks, oversized entries and duplicate plugin IDs are rejected. Legacy exploded packages and disabled manifests still work. Ordinary dotnet builds automatically package both game plugins through `Plugins/BytePlugin.targets`; no plugin installer scripts were introduced.

3. **Manifest format.** Schema 1 contains id, name, numeric version, assembly, optional entryPoint, editor/runtime flags, optional minimum/maximum engine version and optional dependency IDs/minimum versions. The shipped manifests require engine 0.7.0. Invalid or incompatible packages produce actionable diagnostics.

4. **Missing components.** `MissingComponent` is inactive while retaining a deep copy of the entire original ComponentData Type, Enabled and Properties. Save/reload while disabled preserves that state; re-enabling and reopening restores the registered component. Failed deserialization also retains the original data. The native Inspector identifies the missing type.

5. **Event Sheet extensions.** Plugins register namespaced conditions/actions through the supported context API. Default registries include them in editor, runtime and MCP. Optional argument metadata supplies editable labels, types and independent default values. Space includes Build Mode Enabled and Set Build Mode with an Enabled argument. Unknown nodes keep their IDs and arguments when saved without the plugin.

6. **Goblin code removed from Core.** VehicleAssembly/catalog, VehicleBuilder3D and its construction/workshop/UI partials, VehicleBuilder3DCodec, VehicleBuildLayout, VehicleDriveMotion and ContraptionPhysicsWorld moved into Goblin's plugin. Core retains generic construction foundations. Bepu moved from Core's package references into Goblin's dependencies. Game-specific asset tools moved under the plugin. The original Space smoke folder and stale Lib snapshot were preserved in `Backups/SpaceScraper-smoke-before-migration`; unrelated Example files remain untouched.

7. **Goblin plugin location.** [Plugins/GoblinScrapper](C:/Users/codex/ByteEngine/Plugins/GoblinScrapper/GoblinScrapper.Plugin.csproj), including Construction and Tools.

8. **Space plugin location.** [Plugins/SpaceScraper](C:/Users/codex/ByteEngine/Plugins/SpaceScraper/SpaceScraper.Plugin.csproj). It preserves BuildRadius, MaxParts and StartInBuildMode and adds the small Event Sheet example. It does not invent a full Space game.

9. **Goblin artifact.** [GoblinScrapper.byteplugin](C:/Users/codex/ByteEngine/artifacts/plugins/GoblinScrapper.byteplugin). Contains plugin.json, GoblinScrapper.Plugin.dll, BepuPhysics.dll and BepuUtilities.dll; no Core DLL. Installed in the current Goblin project as `Plugins/bytebard.goblinscrapper.byteplugin`.

10. **Space artifact.** [SpaceScraper.byteplugin](C:/Users/codex/ByteEngine/artifacts/plugins/SpaceScraper.byteplugin). Contains plugin.json and SpaceScraper.Plugin.dll; no Core DLL.

11. **Serialization compatibility.** `VehicleBuilder3D` remains a registered alias for `bytebard.goblinscrapper.VehicleBuilder3D`; the existing Goblin startup scene loads. MCP resolves short/full runtime names, canonical IDs and registered aliases when editing/removing components. New contraption saves record a geometry revision separately from catalog schema version. Older connected builds resnap through the retained old catalog, preserving their part IDs, connection names and rotation choices. Clearances are still validated; substantially overlapping builds can need editing.

12. **Builds and tests.** Full solution Release build passed with zero warnings/errors. Both plugin Release artifacts were verified. All 22 focused plugin checks passed. The published MCP self-test passed import → placement → canonical editing/removal → save/reopen → disable/save → re-enable/reopen, plus cold private Bepu loading without a host plugin reference. Real JSON-RPC stdio verified nine tools, live be_plugin, the canonical Goblin component and legacy scene validation; MCP has no export tool. Real Windows export and self-contained executable validation passed, including plugin state and disabled/editor-only exclusion. Web export explicitly blocks managed runtime plugins. All 66 GLBs imported through ByteEngine: 111,658 vertices, 29 usable clips, embedded PBR textures and normalized weights. All 65 buildable parts attached/simulated; wheel torque, hinge connection/steering/travel stops, saved-machine migration, deletion, piston travel, projectiles and flight lift passed. A socket-built beam stand also verified powered/free cog meshing after rotating the whole machine: approximately -25 and +26 rad/s, with opposite rotation. Gear radii now follow the new model axes and meshing uses actual world axes. The real native game loop moved the visible master and wheels, loaded all assets, restored the build pose and rendered hinge builds at multiple UI sizes. The broad existing regression suite **fails** at `Character grounds inside finite collider bounds`; the same assertion reproduces against the preserved pre-migration Core DLL. Its controller, physics and test source are unchanged by this task.

13. **Remaining limitations.** V1 requires reopening after plugin mutations; assemblies are non-collectible. Windows can retain mapped cache DLLs until exit, while temporary assets are cleaned individually. Web/WASM plugins, hot reload and a marketplace are not implemented. Space is an authoring example. Contraption physics still uses simplified shapes, aerodynamics, damage, shared controls and approximate joints; it is not exact Besiege simulation. Narrow or unbalanced machines can tip. Per-part editable bindings/settings remain future work. The new model geometry is derived from the user's installed Besiege game, with Goblin materials/rigs/sockets; it is not original geometry, and no redistribution licence for that geometry has been supplied.

## Requested checklist

| Check | Result |
| --- | --- |
| ByteEngine zero-plugin operation | PASS |
| Space Scraper import/load | PASS |
| Space Scraper serialization | PASS |
| Missing plugin data preservation | PASS |
| Event Sheet plugin conditions/actions | PASS |
| Goblin Scrapper plugin migration | PASS |
| Existing Goblin serialization compatibility | PASS |
| Windows plugin export | PASS |
| Web unsupported-plugin handling | PASS |
| No Goblin compile dependency in Core | PASS |
| No Space Scraper compile dependency in Core | PASS |

The wider character-controller regression mentioned above remains FAIL; it is not included in the plugin acceptance checklist.

## Installed parts and handoff files

- Current project: `C:/Users/codex/Documents/Goblin Scraper/Goblin Scraper.byteproject`.
- The current game has 65 standard/flight parts plus one protected master block. Previously excluded rope, sensor/camera, meter/logic/timer/pin/grip, square balloon/bouncy pad and sea/space blocks remain excluded.
- Parts source and editable Blender project: `C:/Users/codex/Downloads/parts/refined/standard-flight-v3/Goblin_Standard_Flight_v3.blend`.
- [Latest parts workshop](C:/Users/codex/Downloads/parts/refined/standard-flight-v3/PARTS-WORKSHOP.html). A stable shortcut HTML is also at `parts/refined/PARTS-WORKSHOP.html`.
- Previous active assets were backed up to `C:/Users/codex/Documents/Goblin Scraper/Backups/before-reference-v3-20261003-020703`. Existing asset GUID sidecars were retained.
- Refreshed editor: `C:/Users/codex/ByteEngine/Dist/ByteEngine-Refined/ByteEngine.Editor.exe`; final player runtime is bundled beside it.
- [Updated MCP zip](C:/Users/codex/Downloads/ByteEngine-MCP.zip); the previous archive is `ByteEngine-MCP-before-plugins.zip`. This zip contains the tested server, plugin packages, authoring docs and server/Core source, not the Besiege-derived model geometry. Reconnect an existing MCP client to refresh its tool list.
- [Plugin authoring guide](C:/Users/codex/ByteEngine/Docs/PLUGIN-AUTHORING.md).

Reference research used the [Besiege block icon guide](https://steamcommunity.com/sharedfiles/filedetails/?id=2400348420), [flight tutorial](https://steamcommunity.com/sharedfiles/filedetails/?id=884004229), and the installed game's prefab/mesh data. Steering joint conventions were checked against Bepu's [TwistLimit source](https://raw.githubusercontent.com/bepu/bepuphysics2/master/BepuPhysics/Constraints/TwistLimit.cs).