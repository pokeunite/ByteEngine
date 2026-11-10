# Native performance — verdict on the 2026-11-09 investigation, and the corrected plan

Supersedes the priority order in `Docs/Native-Performance-Brief.md`. Source of truth for what to do
next. Written after re-reading the maintained checkout (`52af`) and the numbers in
`ByteEngine-Performance-Findings-2026-11-09.md`.

---

## 1. Verdict

**The investigation is honest and mostly factually correct, but its conclusion is wrong.**

- Correct: several items in the original brief were already implemented in the maintained engine. I
  re-verified three of them directly (§2). That is a real flaw in the brief, not in Codex's work.
- Correct and important: the cache/Cache-off comparison does not support a performance claim in either
  direction, and the 3 % gate failure is not evidence of a regression (§3).
- **Wrong:** "the brief's largest proposed opportunities already existed". The two largest costs are in
  the investigation's own profile and neither was addressed: the **final post pass** (~9.8 ms GPU) and
  the **uber-shader main pass** (~12.6 ms GPU for 326 k triangles). Those are not pre-existing work —
  they are the actual disease.

Cost attribution, taken from the report's own 1080p capture at a **1506×847 world raster (1.275 MP)**:

| Stage | GPU ms | ms per megapixel | Read |
| --- | ---: | ---: | --- |
| Sky | 0.80 | 0.63 | fine |
| Directional shadows | 3.51 | — (≈4.2 MP shadow map) | resolution/caster bound, not matrix churn |
| Main geometry | 12.61 | **9.9** | 3–5× worse than a sane lit pass ⇒ shader/overdraw bound, not draw-call bound |
| UI | 1.25 | 0.98 | high for a HUD; worth one look |
| Post | 9.80 | **7.7** | 6–8× worse than tonemap+FXAA should cost |
| **Total** | **~27** | | ≈37 fps, which matches what was observed |

Two facts kill the "not much left to win" reading: 326 k triangles across 109 draws cannot cost 12.6 ms
on any GPU from the last decade unless the *fragments* are expensive, and a tonemap+AA pass cannot cost
9.8 ms unless it is doing far more work per pixel than a tonemap+AA pass should. Both are measurable, and
both are fixable without lowering resolution or touching art content.

---

## 2. Brief items Codex correctly retired (do not spend more time here)

| Old item | Verified reality | Action |
| --- | --- | --- |
| P1 "uniforms re-upload every draw" | `Graphics/Shader.cs:17-29` already caches int/float/vector/matrix values per shader and skips identical uploads | **Dropped.** Codex right. |
| P4 "point shadows unbounded" | Point-shadow pass measured 0.005 ms CPU / 0.0003 ms GPU in this scene | **Dropped for this game.** Keep the quality cap, nothing more. |
| P5 "SMAA to FXAA" | `PostProcess3D.cs:296-328` is already an FXAA-style edge filter | **Dropped as written** — but see W1, the real problem is what each tap *does*. |
| P3 "no fixed extent / no snapping" | `Renderer3D.BuildDirectionalShadowMatrix` already clamps `ShadowDistance` and snaps; removing the camera-equality gate was a valid cleanup | **Rewritten** as W3 (a different fix, §4). |
| P2 "texture bind churn" | Real but tiny in this game: the sampler path executes ~4×/frame | **Park.** Keep the cache (harmless, correct), do not expect FPS from it. |
| P7 (from the original brief) "enable material affinity natively" | Would break front-to-back early-Z ordering, which matters *more* in a fill-bound scene | **Cancel.** Original brief was wrong here. |
| P6 "editor preview double render" | Scene view already freezes during Play when Game view is visible | **Keep the resolution cap**, drop the double-render rationale. |

Anti-pattern to stop: further GL-call micro-optimisation. At ~143 draws per frame the CPU side is not
the wall; the report's CPU p50 of 14–18 ms includes profiler overhead and needs separating before any CPU
claim is made (§5).

---

## 3. Why the failed comparison is not evidence of anything

Read the numbers in the report again:

- The change under test skips a handful of redundant GL binds. The report's own counter says the
  material-sampling path executes **4 times per frame** (3 with the cache). A code path that runs four
  times per frame cannot double GPU time from 20.5 ms to 39.7 ms.
- The 720p pair therefore measured **machine state**, not the cache: an iGPU (Vega 10 shares memory and
  power budget with the CPU) under sequential single runs with no clock, thermal or background control.
  The 1080p pair, taken the same way, differs by 1.5 % GPU / 6 % CPU — i.e. noise.
- Allocation columns differ by 62 % between the two 720p runs (16.1 KB vs 26.2 KB per frame). Two runs of
  the same code path with the same scene should not differ that much. The workload was not equivalent.
- Both runs already contained every other landed change, so the delta is "cache on vs off", not
  "optimised vs original".

Conclusion: the gate failure means **the harness cannot resolve 3 % changes on this machine**. That is a
tooling defect, not a result. Neither "regression" nor "improvement" is established, and the manual
statement in the report is the correct one.

Do not revert the cache on the strength of this. Do not claim it either. Fix the harness (§5) and re-run
A/B/A/B before judging it.


---

## 4. The wins that were missed (do these, in this order)

### W1 — The final post pass does ~13 full tonemaps per pixel (expected: 9.8 → 2.5–3.5 ms)

`PostProcess3D.cs:279-293` defines `displaySample`, which per call does: scene fetch (+AO fetch when AO is
on, +bloom fetch when bloom is on), exposure multiply, warmth tint, ACES filmic curve,
**`pow(x, 1/2.2)` gamma**, saturation mix and contrast. `smoothEdges` (`:296-328`) calls it **4 times** for
the base cross, then on any detected edge another **2 + 2** for narrow/wide and **4** for the axial term —
up to **13 calls, ~39 texture fetches and 13 ACES+gamma chains per pixel**, in the single
full-resolution pass that writes the window.

That is where the 9.8 ms lives. Two fixes, in ascending cost:

**W1a (do first, shader-only, ~half a day).** Anti-aliasing decisions need *luma*, not final graded
colour. Replace the neighbour samples with a cheap luma probe:

```glsl
float L(vec2 uv){return dot(clamp(aces(max(texture(uSceneTexture,uv).rgb,0.0)*uExposure),0.0,1.0),
                               vec3(.2126,.7152,.0722));}
```

No AO fetch, no bloom fetch, no `pow`, no saturation, no contrast. Keep the centre colour from
`displaySample` for the final blend and keep the existing thresholds so the image stays visually
identical (verify with the screenshot matrix). Neighbour cost drops from ~1 tonemap + 3 fetches to
~1 fetch + 2 dot products.

**W1b (proper, if W1a is not good enough).** Split into two passes: (1) resolve HDR → LDR once per pixel
(`displaySample` exactly once) into an RGBA8 target; (2) FXAA on that LDR target with 5–13 single-fetch
taps, no tonemap. Needs one extra target in `RuntimePostProcessTarget3D` and one extra draw.

Gate: `PostGpuMs` ≤ 3.5 ms at 1.275 MP; screenshot diff of five reference scenes (day, water,
sand/terrain, night with point lights, UI-heavy) within tolerance; no edge shimmer over 3 frames of a
panning camera.

### W2 — The main pass is fragment-bound: uber-shader plus no depth prepass (12.6 → 5–7 ms)

`Shader3D.cs` is **1 493 lines with 15 sampler uniforms and ~41 branch sites** (`uWaterSurface`,
`uWaterVolume`, `uUseHeightField`, `uSandSurface`, `uUseNormalTexture`, dissolve, wetness, triplanar sand,
imprint, palette …). Every fragment pays those branches and samplers, and there is **no depth prepass**
anywhere in the 3D renderer, so every overlapped fragment (terrain behind foliage behind props behind
water) runs the whole shader before being depth-rejected. At 1.27 MP that is the 12.6 ms.

Order of work, each independently measurable with the ablation matrix (§5):

1. **Depth prepass** for opaque geometry: same VAO, position-only variant, colour writes off, `GL_LESS`,
   then the main opaque pass with `GL_LEQUAL`. Gate: main geometry GPU ≥ 25 % lower; no z-fighting on the
   rail/terrain-seam/decal fixtures.
2. **Shader variants** instead of one mega program: `OpaqueBase` (albedo/normal/ORM/emission/heightfield),
   `Water` (surface/volume/depth/reflection), `FoliageCutout` (alpha test + wind), `Unlit`. Select with a
   cached enum on `Material`, not a per-draw string compare. Gate: another ≥ 25 %; all variants compiled
   during startup warmup; `--render-pipeline` plus the 24 randomised image comparisons stay green.
3. Only then consider clusters, foliage instancing or LODs. Those are system projects; the profile says
   fragments are the wall.

One experiment confirms the diagnosis: render the same camera with every material swapped for a trivial
unlit shader. If geometry falls from 12.6 ms to ~2 ms, W2 is real and worth weeks. If it barely moves, the
cost is overdraw and W2.1 alone is the answer.


### W3 — Shadows are resolution/caster bound; "stability" was the wrong lever (3.5 → 1.0–1.5 ms)

The report is right that quantising the projection changed nothing. With a *camera-fitted* light matrix,
movement beyond one texel produces a new matrix, so a matrix-equality cache can never hit during play.
Chasing stability is a dead end; the cost is ~4.2 MP of depth rendering (2048²) every frame.

Cheapest first:
1. **Tier the resolution**: `Balanced` 1024², `High` 1536², `Ultra` 2048². One clamped value at the shadow
   light construction site; roughly a 2–4× cut at Balanced.
2. **Cull casters** by shadow frustum and `ShadowDistance` only, and drop casters below a screen-size
   threshold in Balanced.
3. **Static/dynamic split** (the real fix): static casters render into a fixed world-space light-space
   depth layer, rebuilt only when a static caster's transform version or the light changes; dynamic
   casters (characters, machines, vehicles) render each frame into a second small depth map with the
   *same* light matrix; the shader samples `max(static, dynamic)`. Camera movement then costs nothing for
   the static layer, and a 33-draw pass becomes a 6–10-draw pass.

Gate: directional shadows ≤ 1.5 ms at Balanced, ≤ 2.5 ms at Ultra; per-tier caster counts recorded; no
shadow crawl or shimmer in the screenshot check.

### W4 — Cheaper items once W1–W3 are in

| Item | Why | Gate |
| --- | --- | --- |
| UI 1.25 ms GPU | Check glyph count per frame, blended quad coverage and font-atlas mip state in `Renderer2D` | ≤ 0.6 ms at 1080p |
| Software occlusion outdoors | 1.06 ms CPU with **0** occluded meshes in this outdoor view | Adaptive: keep indoors, disable after N frames with zero culls; keep the toggle |
| Shadow duplicates | 33 shadow draws for 634 visible, many the same terrain chunks | Answered by W3.3 |
| Whole-frame allocations | Report measures main thread only, and noisily | ≤ 32 KB/frame including workers, 0 GC0 over 60 s |


---

## 5. The minimum capture that can actually settle this

The current harness cannot resolve 3 %. Replace it with this, and only then judge changes.

**Determinism**
- Fixed scene state: load `Main`, teleport to a fixed spot, disable wave spawning, fixed seed.
- Fixed camera: a recorded 10-second path (position + look) replayed by the harness, never live input.
- Fixed timestep: drive the frame with a constant `1/60`; never variable delta.
- Disable parallelism for the bench (`JobRunner` threshold to `int.MaxValue`) so physics and pose ordering
  are stable between runs. Document it as a measurement mode, not a shipping mode.
- Audio disabled, editor closed, nothing else running, `GL_RENDERER` string logged in every row.

**Noise control (the part that failed)**
- Warm up 60 s, or until the last 60 frames' p50 varies < 2 %; then measure 600 frames.
- Run **A/B/A/B/A/B** — three alternating pairs inside one process launch — and compare median-of-pairs.
  Never compare two sequential processes (that is what produced the 20.5 → 39.7 ms pair).
- Include a **control run** (same build twice) and record its spread as the noise floor. No delta inside
  that floor counts, regardless of the 3 % gate.
- Record run wall time and the p50 of the first vs last 100 frames as a thermal/clock proxy.

**Every row must carry** (the report's biggest harness defect): quality tier, `RenderScale`, **world
raster W×H**, output W×H, DPI, vsync, post on/off, shadows on/off, AO on/off, bloom on/off, edge filter
on/off, profiler on/off, commit, and per-pass GPU ms (sky, directional shadow, point shadow, geometry,
independent UI, post subpasses). Without the raster column "1080p" is meaningless — the observed 0.784
scale means the real workload was 1.275 MP.

**Ablation matrix** — one scene, one camera, same run:

| # | Variant | Question answered |
| --- | --- | --- |
| 0 | Baseline | reference |
| 1 | Post off | true post cost |
| 2 | Edge filter off (tonemap only) | W1 share |
| 3 | AO off / bloom off separately | AO vs bloom share |
| 4 | Shadows off | W3 total |
| 5 | Shadow resolution halved | resolution vs caster cost |
| 6 | Transparent/water hidden | overdraw share |
| 7 | All materials → unlit shader | uber-shader ALU share (W2 confirmation) |
| 8 | World raster 0.78 → 1.0 | fill-rate slope in ms/MP |
| 9 | Depth prepass prototype | W2.1 value |

Row 8 is the most informative single experiment: if geometry ms scales with pixels the pass is
fill-bound (W1/W2 answer it); if it stays flat it is vertex/draw-bound and the plan changes.

---

## 6. What to do with the landed changes

| Change | Keep? | Note |
| --- | --- | --- |
| Material texture/sampler dedupe | Keep, **default off behind the flag** until §5 resolves it | Correct and harmless, but it executes ~4×/frame here; a 93 % GPU swing cannot come from it and must be explained before shipping it enabled |
| Directional cache camera-equality removal | Keep | Valid simplification |
| Game View 1280×720 cap + Match window | Keep, report as UX | Changes pixels, not renderer speed |
| Detached window VSync follows preference | Keep | |
| `Renderer2D` projection dedupe, indexed water check | Keep | |
| Benchmark harness + comparator | Keep, then rewrite per §5 | The 3 % gate is fine; the experimental design is not |
| Hierarchy search work | Keep | Unrelated, no perf risk |
| Reverted shadow projection experiment | Correctly reverted | Do W3 instead |
| Published Windows build | Note in release notes | Do not describe it as a performance release |

---

## 7. Expected result

| Stage | Now | After W1–W3 |
| --- | ---: | ---: |
| Post | 9.8 ms | 2.5–3.5 ms |
| Main geometry | 12.6 ms | 5–7 ms |
| Directional shadows | 3.5 ms | 1.0–1.5 ms |
| Sky + UI | 2.1 ms | 1.5 ms |
| **GPU total** | **~28 ms** | **~10–13 ms** |

That is roughly 75–100 fps at the current internal resolution on the reference iGPU, with no art changes
and no further resolution drop — versus ~37 fps now. The CPU side then becomes the next wall, which is the
right place to be.

Order of execution: **§5 harness (2–3 days) → W1a → W2.1 → W3.1 → W1b/W2.2/W3.3**, each landing with its
own ablation rows and screenshot gate.

