# Native performance — verdict on the 2026-11-09 follow-up, and the next tranche

Third document in the series. Supersedes the priority order in
`Docs/Native-Performance-Verdict-2026-11-09.md`; the measurement protocol in that document still stands
and the follow-up implemented most of it.

Files: `Docs/Native-Performance-Brief.md` (original), `Docs/Native-Performance-Verdict-2026-11-09.md`
(prior verdict), this document (what is left and why).

---

## 1. Verdict on the follow-up

**The result is valid and the method is sound. It is also roughly one third of the available win.**

Verified directly from `.artifacts/native-upgrade-03/native-frame-summary.json`:

| Field | Value |
| --- | --- |
| Baseline / candidate GPU timeline | 30.8784 → 24.0216 ms |
| Paired median improvement | **22.363 %**, CI 21.8977–22.7593 % |
| Control A / B CI | 0.0008–0.7901 % / −0.1021–1.2366 % |
| Epoch medians | 19.99 %, 22.95 %, 23.32 % |
| Gate | pre-registered, `passed: true` |

Pre-registered gate, AAAA/BBBB controls, seeded ABBA/BAAB quartets, block bootstrap over quartets, and the
earlier failed gate (`native-upgrade-02`) retained rather than relabelled. The effect sits ~20× outside
control noise. The refusals in the report are also correct: paused-render GPU, not FPS; the two stage
experiments are not additive; the absolute targets in the previous verdict are still unmet.

**Scope caveat that must stay attached to the number:** one immutable frame replayed 3 200 times, at
1.275 MP (1506×847 of 1920×1080, render scale 0.7844683), paused, no animation, VFX, physics or camera
motion. Shadow caches, post targets, instance streams and uniform caches are maximally warm in that
workload. In motion the same code will land worse than 24 ms. Treat 22 % as "GPU render cost at fixed
raster", never as "fps in game".

---

## 2. Where the remaining 24 ms goes

| Stage | Now (ms) | ms/MPx | Status |
| --- | ---: | ---: | --- |
| Main geometry | 10.77 | 8.4 | variant covers only untextured materials |
| Post | 5.91 | 4.6 | resolve is correct; AA pass is bandwidth-bound |
| Directional shadows | ~3.50 | — | **untouched in this update** |
| Sky | 0.80 | 0.63 | fine |
| UI | 1.25 | 0.98 | measured, unexamined |
| Env + overhead | ~1.80 | | |
| **Total** | **24.02** | | ≈42 fps GPU-bound |

The follow-up's own flat-fragment ablation (`13.37824 → 4.85776 ms`) is the most valuable number in the
report: **8.5 ms, 63 % of the geometry stage, is material shading** — not geometry, not draw calls, not
overdraw. The accepted solid-lit variant captured 2.57 ms of that 8.5 ms. The rest is still being paid by
every textured draw.

---

## 3. Leak 1 — the shader variant excludes every textured material (≈2.5–4 ms)

`Renderer3D.cs:662-665`:

```csharp
private static bool SolidLitEligible(Material m) => m.Shading == MaterialShadingMode.Lit &&
    !m.WaterSurface && !m.WaterVolume && !m.SandSurface && m.HeightFieldTexture == null && m.WetLevels.W <= 0 &&
    m.MainTexture == null && m.NormalTexture == null && m.MetallicTexture == null && m.RoughnessTexture == null &&
    m.AmbientOcclusionTexture == null && m.PackedPbrTexture == null && !m.EmissionEnabled;
```

Any material with an albedo map is excluded, so terrain sand, character skins, textured props and rails all
still run the generic program: `Shader3D.cs` is 1 493 lines with **15 `uniform sampler2D`, 15 `uUse*`
branch sites and ~41 branch sites overall**. The specialization mechanism itself is proven — three
captures were bit-identical to the generic shader — so this is a coverage problem, not a safety problem.

**Change:** replace the boolean variant with a small variant table.

| Variant | Keeps | Typical users |
| --- | --- | --- |
| `OpaqueBase` | albedo, normal, ORM/metal-roughness, IBL, shadows, fog, palette, skinning | most props, rails, characters |
| `OpaqueCutout` | `OpaqueBase` + alpha test | foliage, fences, grates |
| `Special` | generic program (heightfield, sand, water, wetness, emission) | terrain, ocean, emissive machines |
| `Unlit` | unlit path | decals, emissive UI-world props |

Implementation notes:
1. Convert `SolidLitSource`'s `string.Replace` surgery to `#define` prepends. The existing
   `DIAGNOSTIC_FLAT` path already does this (`Shader3D.cs:11`), and the current replacement breaks
   silently if any shader line's whitespace changes.
2. Select via a cached `MaterialVariant` enum computed from material state (invalidate on material
   change), not per-draw string or boolean evaluation.
3. Compile all variants during `Renderer3D.Initialize()` warmup, and assert the set at startup.
4. Add the missing test: an eligible material must never bind the generic program, a special material must
   never bind `OpaqueBase`, and a live material edit flips variant on the next draw.

**Gate:** geometry ≤ 7.5 ms at 1.275 MP; per-variant captures bit-identical to the generic program on the
same material; `--render-pipeline` and the 24 randomised image comparisons green; material-fallback test
still passes.

---

## 4. Leak 2 — the AA pass is memory-bound on the RGBA16F resolve (≈2.3 ms)

The two-pass restructure is correct (`PostProcess3D.cs:26-42` allocates the resolved target,
`:150-175` runs the resolve then a separate AA program). What remains is bandwidth, not ALU:

> 13 taps × 8 B/px × 1.275 MP ≈ **133 MB of texture reads per frame**, plus the resolve write (10 MB)

On a Vega 10 (shared dual-channel DDR4, ~30–40 GB/s effective) that is 3.3–4.4 ms for the AA pass alone —
which is exactly the 5.9 ms observed. The rejected RGBA8 experiment cut that by 45.8 %, confirming the
diagnosis.

**Change (keeps HDR, halves the bytes):**
1. Use **R11G11B10F** (4 B/px, HDR, no banding) for the resolved target instead of RGBA16F. Expected post
   ≈ 3.5 ms.
2. Tier the tap count: 5 taps at `Balanced`, 9 at `High`, 13 at `Ultra`. Expected post ≈ 2.9–3.1 ms at
   `Balanced` with slightly softer edges.
3. Keep `uResolveOnly`/`uResolvedInput` structure as-is; only the target format and the tap loop change.
4. Do not ship RGBA8 unless a dark-scene dither/banding capture passes — the visual argument for 16F was
   sound, and R11G11B10F answers it at half the bandwidth.

**Gate:** `PostGpuMs` ≤ 3.5 ms (≤ 3.1 ms with tiered taps) at 1.275 MP; RGB MAE ≤ 2/255 and changed-pixel
fraction ≤ 3 % against the current path; no shimmer across three panning frames; pixel-art and
AA-disabled paths untouched.

---

## 5. Leak 3 — shadows were not addressed (≈2 ms)

3.5 ms is ~4.2 MP of depth rendering (2048²) every frame, and it is the largest untouched stage. The
projection-stability idea is a dead end for the reason the follow-up already demonstrated: a camera-fitted
light matrix changes with movement, so a matrix-equality cache cannot hit in play.

**Change, in order:**
1. Tier the resolution: `Balanced` 1024², `High` 1536², `Ultra` 2048² — one clamped value at shadow-light
   construction. Roughly halves the frame at `Balanced`.
2. Cull casters by shadow frustum and `ShadowDistance` only, plus a screen-size threshold at `Balanced`.
3. Static/dynamic split: static casters into a fixed world-space light-space depth layer rebuilt only when
   a static caster's transform version or the light changes; dynamic casters (characters, machines,
   vehicles) per frame into a second depth map with the *same* light matrix; shadow shader samples
   `max(static, dynamic)`. Camera movement then costs nothing for the static layer, and the 33-draw pass
   becomes a 6–10-draw pass.

**Gate:** ≤ 1.5 ms at `Balanced`, ≤ 2.5 ms at `Ultra`; caster counts and pass GPU recorded per tier; no
shadow crawl or shimmer in the panning capture; shadow-only and static-cache fixtures still pass.


---

## 6. Corrections to the previous verdict

**The depth prepass is dead — that was my error, and the follow-up disproved it correctly.**
`13.45968 → 14.07184 ms` (4.55 % slower). The reasoning: opaque draws are already sorted front-to-back,
`EnableStateBatching` (`Renderer3D.cs:656,676-680`) removes redundant state, and early-Z rejects occluded
fragments *before* the fragment shader for non-discarding shaders. A prepass only pays when sorting is
broken or alpha-test coverage is large. Keep `DiagnosticDepthPrepass` (`Renderer3D.cs:667`) disabled and do
not resurrect it without a new measurement on a different scene class (heavy foliage/cutout interior).

**The material texture cache is not a lever.** It executes ~4 times per frame in this game. Keeping it
opt-in (`EnableMaterialTextureCaching = false`, `Renderer3D.cs:669`) is correct; do not spend more time on
GL-call dedupe until CPU submission becomes the wall (§7.3).

**Material-affinity ordering stays off natively.** It would break front-to-back early-Z, which this
fill-bound scene depends on.

---

## 7. What the current benchmark still cannot tell you

### 7.1 Static-frame bias (fix before the next claim)

Replaying one frame 3 200 times means identical GPU state every iteration: shadow maps, post targets,
instance streams and bindings all hit warm paths. Live play re-renders shadow casters as the camera moves
and runs animation and physics. Expect live gains **smaller in absolute ms** than 22 % until measured.

**Required next harness: a motion route.** Record a 10-second real gameplay route once (camera path plus a
scripted enemy spawn at ~2 s and a weapon fire at ~5 s), store it as a frame-indexed track, replay it with
fixed `1/60` steps, `JobRunner` disabled for the harness, audio off, asset-ready gate, seamless loop. Run
the same ABBA/BAAB quartet protocol with the same pre-registered gates.

### 7.2 The tail is unresolved

GPU p95 moved 42.06 → 33.70 ms; spikes survive and nothing attributes them. Add a spike report: the 20
slowest frames per epoch with pass breakdown, plus counters for shader compiles, buffer growth,
framebuffer/target reallocation and GC. A good median with a 34 ms tail is visible hitching.

### 7.3 CPU submission is the next wall and is unexplained

8.15 ms to submit ~143 draws is **57 µs per draw**. `Shader` already caches unchanged values, so the cost
is volume: roughly 40–50 `glUniform*` calls per draw with changed values (model, normal matrix, material
block) plus texture/sampler setup. Once GPU ≤ 16 ms:
1. Per-frame UBO: camera, projection, inverse, lights, fog, IBL, exposure — one bind per frame instead of
   ~40 uploads per draw.
2. Per-material UBO: base colour, PBR scalars, UV transform, heightfield/water blocks.
3. Re-measure; if still > 4 ms at 143 draws, audit driver calls with the existing counters.

### 7.4 UI and frame intervals

UI at 1.25 ms (≈1 ms/MP) is high for a HUD: check glyph draw count, blended quad coverage and font-atlas
mip state in `Renderer2D`. Record **frame interval p50/p99 with vsync off** alongside the GPU timeline; the
GPU number alone cannot express smoothness.


---

## 8. Housekeeping before the next handoff

1. **Browser runtime is stale relative to Core.** `RenderWorld`, `Renderer2D`, `PostEffects3D` and
   `PostProcess3D` are in the wasm dependency graph. Run `Refresh-ByteEngine-BrowserRuntime.ps1`, confirm
   `browser-runtime-integrity.json`, and re-run the browser test scripts before the next web export —
   AGENTS.md requires Dist and the editor copies to stay synchronized.
2. **`SolidLitSource` string surgery** must become `#define`-based with a startup assertion, or the variant
   can silently stop folding features after an unrelated shader edit.
3. **Published Windows build**: keep it, but its release note should read "GPU render cost at fixed raster
   −22 % (paused replay)", not "performance update", until the motion route confirms it.
4. Keep retaining failed gates and their artefacts (`native-upgrade-02` did it right).

---

## 9. Gates for the next update

| # | Gate | Measurement |
| --- | --- | --- |
| 1 | Geometry ≤ 7.5 ms at 1.275 MP | motion route, per-variant captures bit-identical |
| 2 | Post ≤ 3.5 ms, ≤ 3.1 ms with tiered taps | motion route, MAE ≤ 2/255, changed ≤ 3 % |
| 3 | Shadows ≤ 1.5 ms at `Balanced` | motion route, per-tier caster counts |
| 4 | GPU total ≤ 16 ms at 1.275 MP, p95 ≤ 22 ms | motion route, ABBA quartets + controls |
| 5 | Frame interval p99 not worse than −5 % | vsync off, 600 frames per epoch |
| 6 | Zero shader compiles after warmup | spike counters |
| 7 | Browser checks green on the refreshed runtime | `TestBrowser*` scripts + integrity |
| 8 | No visual regression | five-scene screenshot matrix + `--render-pipeline` |

Expected after 1–3: **~16 ms ≈ 62 fps** at the current 0.784 scale (from ~42 fps now). With tight AA taps
and partial UI work: ~14–15 ms ≈ 67–71 fps.

---

## 10. Resolution policy (decide after the next update, not now)

At 0.784 scale the game renders 1.275 MP for a 1920×1080 output. Two ways to spend the next tranche:

| Policy | Result at ~14 ms GPU | Trade |
| --- | --- | --- |
| Keep 0.784 | ~70 fps plus tail headroom for combat | image stays slightly soft |
| Raise to ~0.9 | 1.32× pixels ≈ 18.5 ms ≈ 54 fps | sharper, consumes the gain |
| Native 1.0 | 1.9× pixels ≈ 27 ms ≈ 37 fps | not reachable this tranche |

Recommendation: hold 0.784 until the **combat** route measures GPU ≤ 12 ms, then raise in steps
(0.82 → 0.87) while watching p99. Resolution is already pulled; do not use it as the primary lever.

---

## 11. Do not

- Do not retry the depth prepass (§6).
- Do not ship more GL-call dedupe before GPU ≤ 16 ms; it is not the wall.
- Do not enable the material texture cache by default without a resolved A/B in the motion route.
- Do not lower render scale below 0.784 to buy frames.
- Do not accept a variant scheme that relies on `string.Replace` of shader source.
- Do not claim FPS from a paused replay in notes, docs or store copy.

---

## 12. Execution order

1. Motion-route harness + spike attribution + frame-interval capture (§7.1, §7.2, §7.4) — half a week.
2. Variant table for textured/cutout materials (§3) — the largest GPU win left.
3. R11G11B10F resolve + tiered AA taps (§4).
4. Shadow resolution tier, then static/dynamic split (§5).
5. Re-run the motion route against gates §9; publish only if 1–8 pass.
6. Then, and only then, CPU UBO work (§7.3) and the resolution policy decision (§10).

