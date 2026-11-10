# Multiplayer review — validation and gap list, 9 November 2026

Review of the P2P networking update in the maintained checkout (`52af`). Written after reading the new
`Engine/ByteEngine.Core/Net/` code, the browser and native transports, the integration points, and running
the network test projects.

---

## 1. What was validated in this review

| Check | Result |
| --- | --- |
| `dotnet build Tests/ByteEngine.Network.Tests -c Release` | **Build succeeded** (only the pre-existing `SkeletalMeshRenderer.cs:1960` nullable warning) |
| `ByteEngine.Network.Tests.exe` (codec + session + four peers) | **3/3 PASS** — codec roundtrip/bounds/wrap/zero-allocation; lossy+jittery authority, prediction, 1 cm convergence, fingerprints, host leave, 40 KB event, MTU/backpressure; four-peer late join, baseline, cap, departure |
| `ByteEngine.Network.Integration.exe` (real transport) | **PASS ×3** — actual ICE, DTLS, SCTP, reliable + unreliable binary channels, three create/connect/send/dispose cycles |
| `dotnet build Player/ByteEngine.Browser -c Release` | compiles through the wasm link stage with no C# errors |
| Integration points | verified wired: `NetworkRuntime.Update` in `ByteEngineApplication.cs:204` and `PortableGameLoop.cs:40`; `CreateLink` factories in `Editor/…/Program.cs:10`, `Player/ByteEngine.Player/Program.cs:11`, `Player/ByteEngine.Browser/Program.cs:100`; `main.js:1,147` drives `network-bridge.js` and `data.net`; `PublishBrowser.ps1:65` ships `net.js` + `network-bridge.js`; components registered in `ComponentSerializer.cs:352-354` and `ComponentMetadataRegistry.cs:45-47` |
| Browser identity plumbing | `WebGamePackageExporter.cs:110-112` writes `.byteengine/network-plugins.json`; `NetworkContentIdentity` reads it on wasm and hashes plugin assemblies natively |
| Notices packaging | `ByteEngine.Core.csproj:10` copies `ThirdPartyNotices/**/*.txt` to output and publish |

**Verdict: the foundation is real, correct in its authority model, and better than the original brief in
one important respect** — it uses one WebRTC transport for both Windows and browsers, so native↔browser
cross-play is a first-class path instead of the UDP/WebRTC split the brief proposed. Host-authoritative
input with client prediction and host-side collision constraint is the right shape for a competitive
4-player game.

What it is *not*: a general replication system. There is no world-object replication, no animation state,
no hit validation. Games must author avatars per slot and encode everything else as events/intents. That is
a legitimate scope decision, but it means "engine has multiplayer" ≠ "game is multiplayer".

---

## 2. Correctness findings (ranked; fix before gameplay integration)

### C1 — Input starvation under jitter (worst fidelity bug)

`NetworkSession.cs:107-113`: the host consumes **at most one** latest input per peer per tick
(`r.Input`, `r.HasInput = false`). If two of a client's input packets arrive inside one 33 ms host tick —
normal at 30 Hz with Wi-Fi jitter — the older movement is silently discarded, so the host advances less
distance than the client predicted. Reconciliation then pulls the client back, repeatedly.

The comment says this prevents "input flooding … from increasing movement speed", but rate-capping and
dropping are different tools. Dropping *inputs* loses intent; the anti-cheat goal only needs a cap on
*applied* inputs per second.

Fix: queue per peer (bounded ring, 8 entries), apply up to 2 per tick, and enforce the anti-speed-hack rule
as "average applied inputs per 1 s ≤ tick rate × 1.25". Gate: at 30 Hz with 60 ms jitter and 10 % loss,
`Corrections` stays under ~1/s and travelled distance matches input distance within 2 %.

### C2 — Local avatar pops to world origin on join (`NetworkRuntime.cs:70,139,87`)

The host seeds `players[0]` at `Vector3.Zero`; a client sets
`PredictedLocal = new(LocalPeerId, 0, Vector3.Zero, Vector3.Zero)` on `Welcome`. `NetworkTransform` renders
the local avatar from `session.PredictedLocal.Position`, so it is drawn at the origin until the first
snapshot arrives — one RTT of a wrong position, visible over the internet. Renderers are also disabled
until `Players` contains the peer, so the avatar appears late *and* misplaced.

Fix: send the authoritative spawn in `Welcome` (it exists in `spawns`), seed `PredictedLocal` from it, and
don't render the local avatar before that seed. Gate: at 150 ms RTT the local avatar's first rendered
position is within 1 cm of the authored spawn, never the origin.

### C3 — No correction smoothing

`ApplySnapshot` (`NetworkSession.cs:176`) replaces `PredictedLocal` with the replayed authoritative state, so
corrections under a metre are applied instantly and jitter reads as micro-stutter even though position
converges.

Fix: keep an error offset and decay it (`error *= exp(-12 * dt)`) below ~0.6 m; hard-snap above that
(teleport/respawn). Gate: 20 % loss still converges to 1 cm, and no per-frame discontinuity above 2 cm in a
recorded position trace.

### C4 — Connection registry leak, no close path (`NetworkRuntime.cs:12-19`)

`NetworkRuntime.links` gains entries in `OpenConnection` and is only cleared by `Stop()`. When a guest
leaves, `NetworkSession.Remove` disposes the link but the runtime dictionary keeps the dead entry, and
`nextId` keeps growing. The lobby lists connections from that dictionary, so departed peers leave phantom
rows, and `Accept(connection, …)` on a stale id acts on a disposed link.

Fix: add `NetworkRuntime.CloseConnection(int)` and call it from `PeerLeft`/`Disconnected`; prune the
dictionary. Gate: four join/leave cycles leave `links.Count == 0` and the lobby lists only live links.

### C5 — Per-snapshot allocations on clients

`ApplySnapshot` allocates a `Dictionary<int, NetworkPlayerState>` plus two `Except().ToArray()` arrays every
snapshot (30 Hz). Small, but it is garbage in a path the engine otherwise keeps allocation-free.

Fix: decode into a preallocated 4-entry array/bitmask, diff with plain loops, reuse `players`. Gate:
steady-state client allocations match idle.


### C6 — Host avatar slot 0 is required but not validated

`ApplySnapshot` rejects any snapshot lacking peer id 0 (`NetworkSession.cs:170`), and `Welcome` assigns
clients 1–3 (`:135`). `NetworkSettings.StartSession` validates range and uniqueness
(`NetworkRuntime.cs:37`) but never that slot 0 exists. A scene with avatars 1–3 and no 0 silently drops
every snapshot on clients, with no error anywhere.

Fix: require a `NetworkIdentity` with `PeerId == 0` in validation, and report it in the editor. Gate: Play
with such a scene shows a clear authoring error instead of a dead session.

### C7 — `ReadInput` is overwritten with movement-only input (`NetworkRuntime.cs:42`)

`NetworkSettings.StartSession` assigns `session.ReadInput` unconditionally to a WASD lambda returning
`Look = Vector2.Zero, Buttons = 0`. A game wanting mouse look, fire or gamepad must reassign `ReadInput`
*after* `StartSession`; anything set before is clobbered. `PlayerInput` already carries `Look`/`Buttons`,
the default simply never fills them.

Fix: assign the default only when `ReadInput` is null, or add `NetworkRuntime.InputProvider`. Gate: an
FPS-style scene sends look/buttons without engine edits, and the demo exercises a button bit.

### C8 — Remote-input suppression covers only two controller types (`NetworkRuntime.cs:73`)

Remote avatars disable `CharacterController3D` and `PlayerController3D` only. `TopDownController3D`,
`FirstPersonCamera3D`, plugin controllers and custom input readers keep running on remote avatars,
consuming local mouse/keyboard and fighting the replicated transform.

Fix: marker interface (`INetworkRemoteSuppressed` / `IInputConsumer`); disable every implementer on
non-owned identities, with the two current types as built-in implementers. Gate: a remote avatar carrying
`TopDownController3D` shows no local input response.

### C9 — No animation state for remote avatars (largest visible gap)

`NetworkPlayerState` carries `Velocity` but nothing consumes it, and remote controllers are disabled, so a
character's animation layer has no speed or grounded signal: remote avatars slide in their initial pose.
Fix: a small animator bridge that writes locomotion speed, grounded and aim pitch from
`NetworkPlayerState.Velocity` (+ one state byte in the snapshot). Gate: each client sees the other avatar's
walk cycle start and stop with movement.

### C10 — 150 ms silence stops movement (`NetworkSession.cs:111`)

`r.Silence > .15` zeroes movement, so one 200 ms Wi-Fi stall freezes that avatar on the host. Raise to
0.35–0.5 s; keep clearing button bits at 150 ms. Gate: a 200 ms induced stall produces no visible freeze.

---

## 3. Validation gaps (tests that would have caught the above)

| # | Test | Catches |
| --- | --- | --- |
| V1 | 30 Hz session with 60 ms jitter + 10 % loss; assert `Corrections/sec` and total travelled distance | C1, C3 |
| V2 | Join at 150 ms RTT; assert the local avatar's first rendered position equals the authored spawn | C2 |
| V3 | Four join/leave cycles; assert `NetworkRuntime.links` empty and lobby rows correct | C4 |
| V4 | Client steady-state allocation count over 60 s | C5 |
| V5 | Scene with slots 1–3 only; assert a clear authoring error | C6 |
| V6 | Scene with `TopDownController3D` on a remote avatar; assert no input response | C8 |
| V7 | Two avatars walking; assert remote animation speed > 0 | C9 |
| V8 | Same scene identity computed on two different install paths/cultures/timezones | false compatibility rejects |
| V9 | Host pauses 30 s (menu open); session must survive | silent mass disconnect |
| V10 | 10-minute, 4-peer soak; assert heap, GC and link counts are flat | leaks |
| V11 | Two physical machines over the internet, and the same inside an itch.io-style iframe | real-world NAT/iframe behaviour |
| V12 | Export a game with a plugin enabled; browser peer must match native peer identity | missing `network-plugins.json` in an older export |

V8, V9 and V10 are the ones most likely to bite in the first real play session. V11 is the only test that
proves the shipping story, and it is currently explicitly untested.


---

## 4. Missing features (functional, for a real game rather than a demo)

| # | Missing | Why it matters | Smallest useful version |
| --- | --- | --- | --- |
| M1 | **World-object replication** — no `NetworkIdentity` for props/enemies/doors, no spawn/despawn, no replicated variables | Every game needs enemies, pickups, machines and doors to agree. Today each must be hand-encoded into `BroadcastEvent` with netids the game invents and validates itself | `NetworkObject` with a host-allocated 16-bit id, `Spawn(prefab, pos, rot)`/`Despawn` events using `BlueprintRuntimeFactory.Spawn`, plus `NetworkVariable<T>` (u8 id → i32/f32) at 10 Hz |
| M2 | **Remote animation state** | See C9 — third-person remotes slide | The animator bridge in C9 |
| M3 | **Hit validation / lag compensation** | Competitive PvP needs host-side rewind or at least a documented tolerance | Host-side raycast against a small position history (200 ms ring) per peer, exposed as `NetworkSession.IsHitValid(...)` |
| M4 | **Room codes / relay UX** | Copying SDP arrays between players is the main barrier to actually playing over the internet | Optional WebSocket room-code exchange (3 JSON messages) or a QR/clipboard one-click flow; TURN config is already supported |
| M5 | **Reconnect** | A dropped Wi-Fi packet burst ending the session is harsh | Client auto-retry loop with a new invite while the host keeps the slot reserved for N seconds |
| M6 | **Network diagnostics in the perf overlay** | Stats live only in the lobby | Add peers/RTT/bytes/corrections to `performance.js` next to the existing GPU/CPU block |
| M7 | **Pause policy** | See V9 — a paused host that stops updating drops everyone after 10 s | Either keep `NetworkRuntime.Update` running while paused (send look/keepalive) or treat pause as host-authoritative and broadcast a paused state |
| M8 | **Non-avatar ownership** (vehicles, turrets, spectating) | Slot-per-avatar does not cover "who drives this vehicle" | `NetworkIdentity.PeerId` settable at runtime with a host-authoritative claim/release event |
| M9 | **Bundled single-executable player** | Native DLLs (datachannel, OpenSSL) must ship with `ByteEngine.Player.exe`; `PublishSingleFile=false` covers the normal player, the bundled variant is unverified | Build the bundled player and assert it can host and join |
| M10 | **Non-Windows guard** | `ByteEngine.Network.Native` is `net9.0` with Windows-only native packages | Throw a clear "networking is Windows-only in this build" instead of a `DllNotFoundException` |

Not missing, do not rebuild: the authority model, the WebRTC transport choice, the compatibility identity
(source-hash instead of MVID was the right call), the codec and its validation, the rate limits, the
event/intent split, and the manual-signaling design.

---

## 5. Priority

**P0 — before any gameplay integration (a day or two total)**
1. C1 input queue (movement fidelity is the whole point of prediction)
2. C2 spawn seed (visible on every join)
3. C6 slot-0 validation and C7 `ReadInput` hook (silent failures for game authors)
4. C4 link pruning + `CloseConnection`
5. V9 pause policy

**P1 — playable-quality**
6. C3 correction smoothing, C10 silence threshold
7. C9 remote animation bridge
8. C8 input-consumer marker
9. C5 allocation cleanup
10. M6 overlay stats, V1/V2/V3/V10 tests

**P2 — product**
11. M1 world-object replication (the big one; scope it as its own feature with its own brief)
12. M4 room codes, M3 hit validation, M5 reconnect, M7/M8, M9/M10 packaging

---

## 6. Bottom line

The transport, authority model, compatibility identity and test discipline are genuinely good — I
re-ran the unit suite (3/3) and the real WebRTC integration (3/3) on the current source and both pass, and
the browser half compiles and is wired correctly end to end. This is a credible foundation.

It is not yet a multiplayer *game framework*: input handling under jitter will visibly rubber-band, every
join starts with a wrong-position pop, remote avatars do not animate, and nothing but avatars replicates.
Those five items — C1, C2, C9, M1, plus the missing real-internet test — are the difference between
"multiplayer exists" and "multiplayer ships".

