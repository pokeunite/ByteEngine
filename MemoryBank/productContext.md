# Product context

Updated: 2026-11-13

## Why this exists

ByteEngine is a solo/small-team engine built to ship playable 3D games without
absorbing the cost of a general-purpose engine. The editor exists to close the
loop between authoring and testing: open a project, press Play, and get the same
gameplay on desktop and in a browser tab.

## Problems it solves

- **Asset churn breaks scenes in other engines.** ByteEngine keeps GUIDs and
  sub-asset keys stable across reimport, so art iteration does not invalidate
  scenes, blueprints or plugin references.
- **Web ports are a rewrite.** Gameplay is C# compiled once; the browser host
  consumes the same frame contract through a JS/WebGL2 renderer.
- **Vehicle/construction gameplay.** Physics has a general rigid-body solver plus
  a separate specialised construction solver; see `Docs/PHYSICS-BACKEND-BOUNDARY.md`.
- **Iteration speed.** Plugins rebuild and hot-reload without recompiling the
  engine or the editor.

## Who uses it

- The owner (single developer) plus an agent: the same person authors engine
  systems here and game content in separate project folders.
- The current game projects are Dune Company, the FPS template, and the Karlson
  demos. Plugin sources for shipped games live in `Plugins/`.

## UX goals

- Editor start is the Project Browser, not an empty scene.
- Everything authored is inspectable in the editor: models, skeletons, sockets,
  variables, graphs, plugin packages.
- Play mode is isolated and disposable; Stop never corrupts the scene.
- Failures are legible: crash reports, browser crash reporting, validation
  diagnostics, and benchmark output under `.artifacts/`.

## What "done" looks like for a feature

1. Engine behavior lives in `Engine/ByteEngine.Core` and is reachable from the
   editor and player.
2. The browser path works or degrades explicitly (no silent desktop-only code).
3. A regression test or benchmark guards the hot path.
4. The relevant `Docs/` note is updated when behavior changes materially.
