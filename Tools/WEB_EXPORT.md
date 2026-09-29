# Browser export development

## Export your game

1. Save the project, scene, Blueprints and other assets. Stop Play mode.
2. In the editor choose **File > Export Web Game (itch.io)...**.
3. Choose the saved startup scene and an output folder outside your project Assets/Scenes folders.
4. Export. The result is a new dated folder containing a **ZIP** and a **site** folder.
5. Upload the ZIP to an itch.io project whose kind is **HTML**. Enable **This file will be played in the browser**. Choose an embed size or fullscreen.
6. Test the uploaded page privately before publishing. Click to start (required for audio). Click inside the game to capture the mouse for FPS/TPS; Escape releases it.

The ZIP has index.html at its root. Do not zip an extra enclosing folder. It is a real project export, not the earlier cube demo. No desktop installer is needed.

For local tests, serve the site folder through an HTTP server, for example `python -m http.server 8000 --directory "<site folder>"`, then open http://localhost:8000. Opening index.html directly from disk does not work.

## What runs

The browser uses .NET 9 WebAssembly and the existing Core scene/component lifecycle, managed physics/raycasts, controllers, animation, sockets, Event Sheets and UI behavior. Models are cooked on Windows during export, so browsers do not need Assimp or other native import libraries. Asset GUIDs, skeletons, animation data, importer settings and model sidecars are preserved.

The WebGL 2 backend supports textured static and CPU-skinned meshes, basic lit PBR materials, cutout/transparency, directional and point lights, and browser anti-aliasing. The UI backend uses the engine's font layout and atlas, with text, images, quads, anchors and wrapping. Audio sources use Web Audio (WAV assets); mouse, keyboard and gamepads are forwarded to shared input.

## Current limits — reduced graphics

This is the first browser backend, **not Windows renderer parity**:
- Flat sky background instead of HDR sky/IBL.
- No browser shadow maps, desktop postprocessing, or foliage wind rendering.
- Desktop SpriteRenderer/ArenaGameManager scenes/Blueprints are rejected.
- Native plugins, custom desktop-only components and native API calls are not portable.
- Desktop WebGL 2 browsers are the current target. No mobile/touch control preset.
- Fonts have the same glyph coverage limitations as Core's current font atlas; localization data does not imply every writing system is supported.
- Browser memory limits are lower than desktop. All packaged content is loaded into virtual storage, so keep projects reasonably small.

Exporter checks itch.io's current archive limits: 1,000 files, 500 MiB total extracted size, 200 MiB per file and 240-character relative paths. Case collisions are rejected because hosting filenames are case-sensitive. No threads/SharedArrayBuffer are used; cross-origin isolation is not required.

Unsupported graphics may look different. Test your own gameplay, scripts, camera, raycasts, attachments and UI on the hosted page. Automated fixtures do not guarantee every project/component is browser-compatible.

## Developer verification

Install the .NET 9 wasm-tools workload once:

    dotnet workload install wasm-tools

Refresh the bundled browser runtime (also called by the editor publish script):

    powershell -ExecutionPolicy Bypass -File Tools/PublishBrowser.ps1

Managed portability checks and real skinned-model export fixture:

    dotnet run --project Tests/ByteEngine.Browser.Tests -c Release -- --export Player/ByteEngine.Browser/bin/Release/net9.0/publish/wwwroot

The output prints WEB_TEST_SITE. With Node, Playwright and Microsoft Edge available, run the headless engine check:

    node Tools/TestBrowserRuntime.cjs "<WEB_TEST_SITE>"

BYTEENGINE_PLAYWRIGHT can specify an installed Playwright module path. This tests code directly: it does not control the editor or perform the user's manual game tests.

The separate Tools/PublishBrowserPrototype.ps1 still creates the clearly labelled isolated cube demo for backend verification only.

## Manual release check

On your private itch.io page:
- Startup scene loads without a visible error.
- Move, jump, aim and fire; mouse capture/release works.
- Animated meshes and socket-attached weapons follow correctly.
- Raycasts, hit/damage and Event Sheet conditions/actions behave correctly.
- Text, custom fonts, images, UI buttons and localization display correctly.
- Audio begins after activation and stops/loops correctly.
- Resize, fullscreen, tab away/back; no stuck input.
- Check a second desktop browser/device before publishing.

References: [itch.io HTML5 hosting](https://itch.io/docs/creators/html5), [Microsoft browser WebAssembly guidance](https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/wasm-browser-app?view=aspnetcore-9.0).
