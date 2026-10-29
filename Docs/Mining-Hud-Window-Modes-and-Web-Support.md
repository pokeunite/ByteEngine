# Mining HUD, window modes and web support

The browser player now statically links `bytebard.karlsondemo` alongside DesertTerrain and DuneCompany. The plugin assembly is retained for reflection-based scene codecs. `Tools/PublishBrowser.ps1` records the supported plugin ID, version and compiled DLL fingerprint, then refreshes the bundled Dist and Debug/Release BrowserRuntime directories. The normal rebuild-and-refresh script installs those runtimes too. Do not fix compatibility errors by manually adding IDs to the capability JSON: the actual plugin must be compiled into WebAssembly.

The installer also prunes obsolete files directly inside the generated browser `_framework` folder after copying, preventing older fingerprinted binaries from accumulating across refreshes. The cleanup is restricted to the requested engine installation and does not delete game content.

## UI changes

- Pause uses a compact 440-by-360 logical-pixel panel. Settings use a 940-by-540 panel with scrollable controls and one Back button; Escape remains a keyboard shortcut.
- Desktop Graphics has an explicit Windowed / Borderless / Fullscreen dropdown. Apply changes the real window and stores the choice with player settings. The reusable `GameWindowDisplay` service is host-owned, so editor Play does not resize the editor. Web builds show Browser for display mode because the browser owns its window/fullscreen controls.
- Raw ore and refined bars use distinct original antialiased icons. The editable SVGs, PNGs and generator are included with the plugin samples/tooling. Component artwork references remain editable.
- Drill health sits immediately above player health at bottom left.
- Defence HUD cards show only inventory ready to place. Shortcut and ready count occupy separate aligned areas. F1–F5 select the configured defence; B cycles selection, left mouse places and right mouse cancels. Keys remain remappable. Alt no longer requests a cursor or interacts with these cards. Zero-ready cards and the All Deployed text are hidden.

## Validation

The native HUD/settings check exercises the actual Back button, all three display-dropdown choices and Apply, Alt remaining inactive, F1 selection, placement on valid ground and hiding the spent slot. A separate test changes the actual Windows game host between all three window modes.

`Tools/TestLinkedMiningBrowser.cjs` boots the published AOT runtime in a hidden browser and mounts only a tiny in-memory test scene containing mining components. It checks component deserialization, simulation and WebGL rendering. This is a runtime test, not a game export. The native `--verify-browser-support` check validates the real project's enabled plugins, compiled DLL fingerprints and scene/blueprint browser capabilities without exporting it.

Plugin version: **3.45.0**. Existing game saves and authored scene content are preserved; the scene changes are the two ore-icon references. A scene backup is kept under the project's `.byteengine/Backups/hud-web-fixes-*` directory.
