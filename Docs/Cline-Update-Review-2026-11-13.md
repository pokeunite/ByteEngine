# Cline update review — 2026-11-13

## Scope and identity

Source routing selects main. Review starts from main `d91cd2a` and secondary
branch `codex/event-sheet-ux-and-engine-updates` at `e128b7f`. The final committed
patch plus these heads identifies the reviewed source. Branches remain separate;
this review does not merge gameplay, networking or rendering between them.
Raw logs are under `.artifacts/cline-review-20261113` in each checkout.

## Main findings resolved

- **Navigation region lifetime:** cached regions could remain usable while
  inactive, disabled or detached. Reading a detached component's owner throws.
  Retain its owning object, check membership/activation, and clear the path when
  the selected region changes, even if bake versions match.
- **Physics candidate lifetime:** contacts and joints can move colliders into
  new overlaps during solver iterations. Reuse now checks world transforms and
  refreshes bounds/candidates after changes.
- **Fixed-step timing:** restore the configured tick delta before fixed callbacks.
  A 20 ms test protects against callbacks seeing the default 1/60 interval.
- **Crowd quantization:** remove the proposed rounded-frame modulo. Playback
  already wraps looping time; modulo corrupted one-shot endings and fractional
  clip endings. Retain immutable cached geometry uploads.
- **Documentation:** correct cache/performance claims and branch-specific GPU
  capabilities; remove the README's duplicate version literal.

Retained A* scratch and private returned paths reduce query allocation. Searches
on one grid/mesh must remain serialized. Repath jitter spreads retries and
changed-destination requests, not continuous successful-path replans.

## Secondary branch findings resolved

- Keep adaptive occlusion at its previous opt-in default until a performance
  gate passes. Packed HDR resolve is behind `EnablePackedHdrResolve`, default
  false; packed-format image/driver compatibility remains unverified.
- Fix the measurement runner: `Reps` now means repetitions per mode, and the
  multi-mode schedule returns one mode rather than an array per launch.
- Retain opt-in phase buckets, shadow cap, allocation counters and signaling
  diagnostics. These are not measured FPS improvements.

## Checks

Main Release test build passes. Run `dotnet run --no-build -c Release --project
Tests/ByteEngine.Tests -- <flag>` after the current build with:
`--engine-optimization`, `--roadmap-physics`, `--roadmap-integration`,
`--web-cpu-regressions`. The default suite also checks editor, serialization and
component registry coverage. New cases cover inactive/removed navigation regions,
solver-created trigger overlaps and custom fixed callback timing.

Secondary Release test build passes. Flags: `--ai-query`, `--post-contexts`,
`--render-pipeline`, `--native-skinning`. The skinning fixture checks 300-bone
changed poses against CPU main/directional/six-face point-shadow pixels. These
are automated graphics checks, not a manual visual game review.

`dotnet run -c Release --project Tests/ByteEngine.Network.Tests` passes codec,
session, four-peer and lobby recovery groups. The corresponding
`Tests/ByteEngine.Network.Integration` command passes three local native WebRTC
checks. Neither validates overseas networks or TURN provisioning.

Harness dry runs pass two modes with eight repetitions each (16 total), and
three modes with four repetitions each (12 total). This checks scheduling, not
FPS. No real repeated A/B sweep ran; previous single-run timings remain noisy.
The paired bootstrap also passes a synthetic, known 10% effect. Synthetic data
validates the statistics implementation and is not engine speedup evidence.

Initial secondary builds hit generated-file access errors. The successful build
uses `-m:1 -nr:false -p:UseSharedCompilation=false`, avoiding reused build/compiler
workers from a different permission context. Nullable/analyzer warnings remain;
successful builds have zero compiler errors.

## Outstanding

- Choose a tested integration direction; main has no multiplayer implementation.
- Run an asset-ready repeated A/B gate with build identity, quality and actual
  raster dimensions recorded. Stress-scene loading remains a documented issue.
- Validate packed HDR framebuffer/format fallback and image parity before enabling.
- Exercise signaling and ICE on both actual overseas networks; an authenticated
  TURN relay may still be needed. Local tests do not establish its necessity.
- Keep the separate `PublicPvpLobby` repository and local companion test outside
  engine commits until their ownership is decided.

No game export, runtime publication, deployment or plugin gameplay-source change
is part of this review. Git publication is the requested delivery.
