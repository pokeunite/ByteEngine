# ByteEngine native plugins

A plugin is trusted local C# code. Core and Editor have no compile reference to a game plugin. A project with no plugins is a normal engine project.

## Build and import

Build `Plugins/SpaceScraper/SpaceScraper.Plugin.csproj` or `Plugins/GoblinScrapper/GoblinScrapper.Plugin.csproj` with ordinary `dotnet build -c Release`. The shared `Plugins/BytePlugin.targets` MSBuild target writes `artifacts/plugins/SpaceScraper.byteplugin` or `GoblinScrapper.byteplugin` automatically. No installer script is required.

A `.byteplugin` is a ZIP with a root `plugin.json`, its main DLL and genuine private dependency DLLs. Goblin includes BepuPhysics and BepuUtilities. Never package ByteEngine.Core.dll: the running host supplies Core. Use a Core ProjectReference with `Private="false"`. For additional private libraries, add `BytePluginDependencyName` items containing their output DLL filenames before importing the shared target. The packaged files are chosen explicitly, not by copying the entire host output.

Import through Project Settings → Plugins → Import Plugin, then reopen the project. Enable, disable and remove also require reopening; V1 does not hot-reload. The same operations are exposed through MCP `be_plugin` (`list`, `import`, `enable`, `disable`, `remove`). First open the project with `be_project`. After a package operation, call `be_project/open` again before authoring its components. MCP has no game export tool.

## Manifest

```json
{
  "schemaVersion": 1,
  "id": "example.myplugin",
  "name": "My Plugin",
  "version": "1.0.0",
  "assembly": "MyPlugin.dll",
  "entryPoint": "MyPlugin.Entry",
  "editor": true,
  "runtime": true,
  "minimumEngineVersion": "0.7.0",
  "dependencies": []
}
```

`maximumEngineVersion` is optional. Dependencies use `{ "id": "example.support", "minimumVersion": "1.0.0" }`. IDs must be unique; versions use .NET numeric Version format. Dependencies load before their dependents. Missing, disabled, incompatible, failed or cyclic dependencies produce load diagnostics. Declared dependency plugin assemblies are shared between their plugin contexts so referenced public types retain identity. Private libraries belonging to each plugin stay in that plugin's context. Core always comes from the host.

## Components and events

Implement `IByteEnginePlugin.Register(ByteEnginePluginContext context)`. The Space plugin is the small complete example. `RegisterSimpleComponent<T>("ComponentName", metadata)` handles public read/write supported properties: bool, int, float, double, string, Guid, vectors, enums and AssetReference. Their JSON keys use camel case; their codec IDs are namespaced with the plugin ID. For nested data or collections, implement `IComponentCodec` and call `RegisterComponent(codec, metadata)` instead. Metadata enters the native Add Component and Inspector path through a supported registry method, without private-field reflection.

Register Event Sheet conditions/actions through `RegisterCondition` / `RegisterAction`. Use IDs prefixed by the plugin ID. Optional `Arguments` metadata supplies display names, value types and independent default values. The native Event Sheet editor and MCP use this metadata. `VisualLogicRegistry.CreateDefault()` includes currently registered plugin definitions in both editor and runtime. The Space example has a Build Mode Enabled condition and Set Build Mode action with an editable Enabled argument.

Add old serialized names with `RegisterSerializedAlias(oldName, canonicalCodecName)`. Goblin maps `VehicleBuilder3D` to `bytebard.goblinscrapper.VehicleBuilder3D`. MCP component edits and removal accept short runtime names, full runtime names, canonical codec IDs and registered old aliases. Components always save using their canonical codec ID.

## Storage and unavailable plugins

The original package remains at `<Project>/Plugins/<id>.byteplugin`. Extraction goes into the disposable SHA-256-addressed `<Project>/.byteengine/PluginCache/<id>-<hash>` cache. Enable state is a separate adjacent `.byteplugin.state.json`, never a modification to the package. Legacy exploded plugin folders and `plugin.json.disabled` are still recognized. Package validation rejects traversal/rooted paths, duplicate paths, symlinks and excessive file counts or sizes.

An unavailable component becomes an inactive `MissingComponent`. It retains the full original ComponentData: Type, Enabled and a deep copy of Properties. Saving while disabled preserves that data; reopening with the codec available restores it. The Inspector labels the missing type. Unknown Event Sheet IDs and arguments survive load/save too. A missing plugin is not silently replaced with a generic gameplay component.

## Export and limitations

Windows export includes enabled runtime plugins and their packages; disabled and editor-only plugins are excluded. The self-contained player loads plugins before deserializing startup scenes. Web export explicitly rejects enabled managed runtime plugins; no WASM plugin implementation is provided.

V1 assemblies are non-collectible and can remain mapped until process exit. Reopening removes old registrations, not loaded CLR images. Locked disposable cache DLLs can remain on Windows; temporary game assets are still cleaned individually. This system is not a sandbox, marketplace or hot-reload framework. One project is active per MCP process. Per-project plugin state must be released in Shutdown.

## Verification

Run `ByteEngine.Tests --plugins artifacts/plugins/SpaceScraper.byteplugin` for focused acceptance tests. Run the MCP `--self-test` with `BYTEENGINE_TEST_PLUGIN` pointing at Space's package for actual import → placement → save/reopen → disable/save → enable/reopen checks. Optional `BYTEENGINE_TEST_GOBLIN_PLUGIN` and `BYTEENGINE_TEST_GOBLIN_CATALOG` exercise private Bepu loading without a host plugin reference. `--windows-export <published-player-directory>` with the Space environment variable verifies native plugin content, exclusions and web blocking.
