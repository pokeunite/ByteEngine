# Browser texture lifetime fix — 7 November 2026

Maintained source: `C:/Users/codex/.codex/worktrees/52af/ByteEngine`. No game export or
upload is part of this work; only the statically linked browser runtime is refreshed.

## Reported symptom

Hazard Pay, exported web build on itch.io, build `bootSha256 9B2FC6…`, plugins
DesertTerrain 0.8.11 / KarlsonDemo 3.78.0 / DuneCompany 0.27.0. After roughly six
minutes of play the frame aborted:

```
Browser frame failed during scene update and rendering in 'Main' (1280x720):
System.InvalidOperationException: Browser texture has no CPU pixels.
   at BrowserFrameSink.Texture(Texture2D texture)
   at BrowserFrameSink.EmitBatches(IReadOnlyList`1 batches, Int32 pass)
   at ByteEngine.Core.Graphics.ThreeD.RenderWorld.Execute(RenderContext context)
```

The recording showed the exception immediately after a scene restart, during VFX-heavy
combat, and every later frame failed the same way.

## Root cause

`VfxAtlas.Acquire` inserted a newly built atlas into the retained cache and then called
`Trim` **before** taking the lease:

```csharp
entry=new Entry {Texture=texture,Bytes=...}; Cache[key]=entry; CacheBytes+=entry.Bytes;
Trim(Environment.TickCount64);   // new entry still has Users==0 and LastUsed==0
entry.Users++;entry.LastUsed=Environment.TickCount64;
```

Once the retained cache passed its 32 MiB budget (default layers are 2 MiB atlases,
8 MiB for flipbooks, and every distinct sprite/colour/curve key is its own entry), `Trim`
selected idle entries by `LastUsed` ascending. A brand-new entry has `Users == 0` and
`LastUsed == 0`, so it sorted first: the atlas that `VfxPlayer` was about to render was
disposed and evicted, and `b.Material.MainTexture` kept pointing at it.

`Texture2D` in the browser runtime is CPU-only: `Dispose` releases the pixel array.
`BrowserFrameSink.Texture` therefore found a content version it had never uploaded and no
pixels to upload from, and threw. On desktop the GL upload had already happened, so the
same bug was invisible there.

## Fixes

- `Engine/ByteEngine.Core/Vfx/VfxAtlas.cs`
  - The lease is taken (and `LastUsed` stamped) *before* `Trim`, so an atlas that is about
    to render can never be evicted by its own insertion.
  - `Trim` only ever evicts unleased idle entries, through a single `Evict` helper that
    removes the cache entry before disposing the texture, and it skips all LINQ work when
    the cache is under budget and nothing has gone idle.
- `Player/ByteEngine.Browser/Program.cs`
  - `BrowserFrameSink.Texture` no longer throws for a texture whose CPU pixels are gone.
    A resident copy keeps its worker texture; a texture that was never uploaded degrades
    to untextured for that draw and logs one bounded diagnostic naming the owner
    (`vfx:` / `font:` labels, file name or "generated texture").
  - Deferred uploads that cannot be copied (texture disposed, or reloaded to a different
    size between staging and the drain) now invalidate the resident version so the next
    request re-stages the whole image instead of leaving a stale or blank copy.
- `Engine/ByteEngine.Core/Graphics/Texture2D.cs`
  - Added `IsDisposed` so renderers and tests can distinguish "released" from "no pixels
    uploaded yet".
- `Tests/ByteEngine.Tests/VfxRenderTests.cs`
  - New retained-atlas check: five 8 MiB atlases are leased concurrently (past the 32 MiB
    budget) and every lease must still reference a live texture, plus a cache-reuse check.

Other latent hazards found while tracing this (unchanged here, now non-fatal because of
the sink guard): `WaterDepthField3D.OnDestroy/Rebuild` and `DesertTerrain3D.OnStop/
OnDestroy` dispose textures that their materials can still reference.

## Verification

- `dotnet build Engine/ByteEngine.Core` and `dotnet build Player/ByteEngine.Browser -c
  Release`: succeed.
- `ByteEngine.Tests --vfx-render`: passes, including the new retained-atlas leasing check
  ("VFX offscreen GL: … curve atlas sharing and retained atlas leasing passed.").
- Negative control: restoring the pre-fix `Trim`-before-lease ordering makes the new check
  fail with `Leased VFX atlas 3 was disposed by retained-cache trimming.`, so the test
  covers the reported defect rather than the code path only.
