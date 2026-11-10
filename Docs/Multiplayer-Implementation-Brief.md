# Peer-to-peer multiplayer — implementation brief

Target: `C:/Users/codex/.codex/worktrees/52af/ByteEngine` (the maintained checkout that builds the
browser runtime and exports games). Written for an implementing agent. Ordered by phase; each phase is
independently shippable and testable.

Related docs: `Docs/Engine-Pipeline-Fixes-2026-11-05.md`,
`Docs/Browser-Texture-Lifetime-Fix-2026-11-07.md`.

---

## 1. Decision summary

Build **host-authoritative peer-to-peer with peer-owned player avatars**:

- One peer is the **host**. It runs the authoritative world (AI, enemies, spawns, economy, physics
  results, interactions) and replicates that state.
- Every peer owns **its own player avatar** and sends its transform at the network rate. Receivers
  interpolate remote avatars. This gives zero input latency where it matters and removes prediction,
  rollback and input replay from v1.
- Transport: **WebRTC data channels between browser peers**; **UDP between native peers** (editor
  Play mode, Windows player, LAN). One interface, three backends (WebRTC, UDP, loopback for tests).
- No dedicated server, no relay service, no accounts.

### Explicitly rejected for v1 (do not build)

| Option | Why not |
| --- | --- |
| Deterministic lockstep (all peers simulate, exchange inputs) | The engine is not deterministic: `PhysicsWorld3D` broadphase/solver ordering, `JobRunner.For`/`Parallel.For` pose capture in `Scene`, float differences between wasm and desktop, distance-sampled animation budgets. Lockstep would mean rewriting physics and scheduling, then constraining every future feature. |
| Rollback netcode | Needs the same determinism plus a snapshottable world. Only justified for fighting games. |
| Full snapshot of every object every tick | Bandwidth and GC cost. Use opt-in components, dirty masks and rate tiers (§7). |
| Per-frame JSON for network payloads | The engine already ships binary frame buffers (`FrameTransferBuffers` + `bufferViews` + `PackedFramePool`). Reuse them. |
| Client prediction + reconciliation in v1 | Unnecessary once avatars are peer-owned. Add later only if a PvP mode demands it. |

### v1 scope

2–8 peers, co-op (PvE or shared-world PvP-lite), one host, same scene on all peers, no mid-session
scene change other than a host-driven reload, no host migration (host leaving ends the session with a
clear message).

### Non-goals (v2+)

Dedicated servers, matchmaking, accounts, anti-cheat beyond host-side validation, replay/spectator,
host migration, desktop WebRTC, voice chat, full interest management.

---

## 2. Architecture

```
            ┌──────────────────────── host peer ──────────────────────────┐
            │ NetworkSession (Core)                                        │
 local      │  ├─ authority: AI, spawns, damage, interactions, world state │
 input ───► │  ├─ replicates: NetworkTransform (world) @10 Hz, vars, events│
            │  └─ owns: its own avatar (peer-owned transform @20 Hz)       │
            └──────┬──────────────────────────────────────────────┬──────┘
                   │ state (unreliable) + events (reliable)        │
            ┌──────▼──────── client peer ──────────────┐           │
            │  ├─ interpolates host objects (~100 ms)  │   ...     │
            │  ├─ owns its avatar, sends transform 20 Hz│          │
            │  └─ sends gameplay intents as events      │          │
            └──────────────────────────────────────────┘           │
                transport: WebRTC data channel / UDP / loopback ◄──┘
```

Two logical channels per peer link, mirroring WebRTC's model and implemented by every backend:

| Channel | Delivery | Payload |
| --- | --- | --- |
| `State` | unreliable, unordered, dropped when backed up | transforms, variables, ping |
| `Events` | reliable, ordered | join/leave, spawn/despawn, damage, interact, UI/audio cues, RPC |

Core owns simulation and protocol logic; hosts own transport. This mirrors the existing
`IPortableAudio` / `PortableAudio.Backend` pattern, so the browser player and the desktop app install a
backend the same way they install audio.

---

## 3. Transport layer

### 3.1 Core contract — `Engine/ByteEngine.Core/Net/PortableNetwork.cs`

```csharp
public enum NetworkChannel { State, Events }

public sealed record NetworkPeerInfo(int PeerId, string Name, bool IsHost);

/// <summary>Host-owned transport. Desktop UDP and browser WebRTC both implement it.
/// All callbacks fire on the engine owner thread, never from a socket or JS callback.</summary>
public interface IPortableNetwork
{
    bool IsRunning { get; }
    int LocalPeerId { get; }                    // host is always 0
    IReadOnlyList<NetworkPeerInfo> Peers { get; }

    void StartHost(int port, string localName);
    void StartClient(string inviteCode, string localName);
    void Shutdown();

    bool Send(int peerId, NetworkChannel channel, ReadOnlySpan<byte> payload); // false = backpressure
    void Poll();                                // drain inbound; called once per frame before simulation
    NetworkStats Stats { get; }

    event Action<int, string>? PeerConnected;                 // peerId, remote name
    event Action<int>? PeerDisconnected;
    event Action<int, NetworkChannel, byte[]>? Message;        // payload is a copy the caller may keep
    event Action<string>? SessionError;                        // human-readable reason
}

public static class PortableNetwork { public static IPortableNetwork? Backend { get; set; } }
```

Backend rules:

- Never raise callbacks from a socket/JS callback thread. Queue, then drain in `Poll()`.
- `Send` never blocks and does not allocate for payloads under 1 KB (reuse a scratch buffer).
- Inbound payloads handed to `Message` are copies the session may retain.
- `Shutdown` is idempotent and safe when not running.

### 3.2 Native — `Engine/ByteEngine.Core/Net/UdpNetwork.cs`

- `UdpClient` bound to `0.0.0.0:<port>` (host) or an ephemeral port (client). No new NuGet packages.
- Per-datagram header: `u32 magic 'BEN1' | u8 channel | u8 flags | u16 seq | u32 ack | u32 ackBits`.
- Reliability only for `Events`: sequence + ack + 32-bit ack bitfield; resend with a 20 ms RTO floor,
  give up after 5 s and drop the peer. `State` is fire-and-forget; drop when more than 32 datagrams
  are queued for a peer.
- `Events` ordering: release in sequence order, hold at most 64 out-of-order datagrams.
- Max datagram payload 1200 bytes (MTU-safe). The session layer fragments anything larger (§5.4).
- Invite code = base64 of `{ v, hostLanIp, port, nonce }`. Document that internet play on desktop
  needs port forwarding; a relay is out of scope.
- Install next to the audio backend in `ByteEngineApplication` startup and dispose on close.

### 3.3 Browser — `Player/ByteEngine.Browser/wwwroot/net.js` + `BrowserNetwork.cs`

`net.js` (ES module, same style as `audio.js`): one `RTCPeerConnection` per link, two data channels —
`state` (`ordered:false, maxRetransmits:0`) and `events` (`ordered:true`).

- Host: `createOffer()`, wait for ICE gathering to complete, export an invite code. Client: paste code,
  `setRemoteDescription`, `createAnswer()`, export an answer code.
- Invite code: `BEN1.<base64url(deflate-raw(json))>` with json
  `{ v, kind:'offer'|'answer', sdp, ice:[{c,f,u}], app, scene }`; plain base64 fallback when
  `CompressionStream` is missing. Codes land around 600–1500 chars.
- Signaling: **manual copy/paste by default** — the only thing a static itch.io host gives you.
  Two extension points, both required:
  1. `window.ByteEngineSignaling = { send(code), onCode(cb) }` — when a page injects this (tests, or a
     developer's lobby page), codes are exchanged automatically and no UI appears.
  2. Optional `signalingUrl` (WebSocket) on the `NetworkSession` component: host registers a 6-char
     room code, clients join by code. Do not build a signaling server in this repo; document the three
     JSON messages (`host`, `join`, `code`).
- ICE servers: default `stun:stun.l.google.com:19302` and `stun:stun1.l.google.com:19302`, overridable.
  TURN only when the user supplies URLs. On ICE failure report: "both peers appear to be behind
  symmetric NAT; configure a TURN server".
- Browser facts already true here: the game runs in an itch.io iframe over HTTPS (secure context,
  `RTCPeerConnection` available) and `crossOriginIsolated` is false, so never require
  `SharedArrayBuffer` or worker threads for networking.
- .NET bridge (`BrowserNetwork : IPortableNetwork`):
  - inbound — JS calls a new export `BrowserGame.NetworkReceive(int peerId, string channel, byte[] data)`
    using the same `[JSMarshalAs<JSType.Array<JSType.Number>>]` shape as `MountBytes`; it copies and
    enqueues.
  - outbound — packets accumulate in `BrowserFrameSink` as **frame buffer views** (§5.4), the same path
    draw packets and textures already use, so no base64 is spent.
  - install — `PortableNetwork.Backend = Network;` in `BrowserGame.Start()`, next to
    `PortableAudio.Backend = Audio;`.
- JS must copy packet bytes out of the WASM view immediately (`new Uint8Array(view)`); the heap view
  expires at the next `Frame()` call, exactly as `PackedFramePool` documents.

### 3.4 Loopback — `Engine/ByteEngine.Core/Net/LoopbackNetwork.cs`

In-process transport that wires N logical peers with configurable latency, jitter and loss. Used by
`Tests/ByteEngine.Tests` for headless protocol tests and by the Playwright test when a real ICE
handshake is undesirable.


---

## 4. Public API for gameplay code

All `public sealed` types in `Engine/ByteEngine.Core/Net/`, registered for serialization per §9.4.

```csharp
public static class NetworkSession            // created by the first NetworkIdentity in the scene
{
    public static bool IsActive { get; }
    public static bool IsHost { get; }
    public static int LocalPeerId { get; }
    public static double TickRate { get; }    // default 20
    public static NetworkStats Stats { get; }

    public static event Action<NetworkIdentity>? OwnedObjectSpawned;
    public static event Action<NetworkIdentity>? OwnedObjectDespawned;
    public static event Action<int, bool>? PeerJoined;   // peerId, isLocal
    public static event Action<int>? PeerLeft;
    public static event Action<string>? SessionEnded;    // reason

    public static void Host(NetworkSettings settings, string playerName);
    public static void Join(string inviteCode, string playerName);
    public static void Leave();

    public static GameObject? FindObject(uint networkId);
    public static bool TryGetNetworkId(GameObject gameObject, out uint networkId);
}

public sealed class NetworkIdentity : Component      // opt-in replication root
{
    public int OwnerPeerId { get; }                  // 0 = host, -1 = host-owned world object
    public bool IsOwner { get; }
    public bool IsLocalPlayer { get; }
    public uint NetworkId { get; }
    public bool IsPlayerAvatar { get; set; }
    public NetworkAuthority Authority { get; set; }  // OwnerControlled | HostControlled
}

public sealed class NetworkTransform : Component
{
    public float SendRate { get; set; } = 20;        // Hz; host-owned world objects default 10
    public float PositionThreshold { get; set; } = 0.01f;
    public float RotationThreshold { get; set; } = 0.5f;  // degrees
    public NetworkInterpolation Interpolation { get; set; } = NetworkInterpolation.Interpolate;
}

public sealed class NetworkAnimator : Component      // compact visual state for remote avatars
{ public byte SpeedPercent { get; set; } public bool Grounded { get; set; } public float AimPitch { get; set; } }

public sealed class NetworkBehavior : Component      // optional convenience base
{
    protected bool IsOwner { get; }
    protected bool IsHost { get; }
    protected void SendEvent(NetworkEvent evt, NetworkTarget target = NetworkTarget.Host);
    protected virtual void OnNetworkEvent(NetworkEvent evt) { }
    protected virtual void OnNetworkSpawn() { }
    protected virtual void OnNetworkDespawn() { }
}
```

`NetworkSession` is driven by `PortableGameLoop`/`ByteEngineApplication`, not by scenes, so it survives
scene loads. `NetworkEvent` carries `int Id` (a per-game constant), `NetworkTarget`
(`Host | Others | All | Peer(id)`), `bool Reliable` (default true) and a payload written with a pooled
`NetworkWriter` (`WriteByte/Short/Int/Float/Vector3/String`, 7-bit length prefix). Host handlers get the
sender id and must validate ownership, range and cooldown before applying anything.

---


## 5. Protocol

### 5.1 Framing

Every message: `u8 type | u8 version (=1) | payload`. Little-endian throughout, written with
`System.Buffers.Binary.BinaryPrimitives` (already used in `BrowserFrameSink`). No JSON on the wire.

| Type | Name | Channel | Direction |
| --- | --- | --- | --- |
| 1 | `Hello` | Events | client → host |
| 2 | `Welcome` | Events | host → client |
| 3 | `Reject` | Events | host → client |
| 4 | `Manifest` | Events | host → client |
| 5 | `Snapshot` | State | host → all |
| 6 | `OwnState` | State | peer → all |
| 7 | `Event` | Events | any → any |
| 8 | `Ping` / `Pong` | State | both |
| 9 | `Bye` | Events | any → any |

### 5.2 Handshake (join sequence)

1. Client `Hello { protocolVersion, engineIdentity, pluginFingerprint, sceneHash, playerName }` — the
   identity strings come from the values already exported into the runtime
   (`browser-source-identity.json`, `browser-plugin-builds.json`, scene path + file hash).
2. Host compares versions, plugin fingerprints and scene hash. Mismatch → `Reject { code, message }`
   with a specific, user-facing reason ("both players need the same game build").
3. Host allocates a `PeerId` (1..8, monotonic), then sends `Welcome { peerId, hostTick, sceneTime,
   seed }` followed by `Manifest` (see 5.3) and one full `Snapshot` with `keyframe=1`.
4. Host raises `PeerJoined`, spawns (or reveals) that peer's avatar with `OwnerPeerId = peerId` and
   `IsPlayerAvatar = true`, and broadcasts `Event(spawn)` for it.
5. Client applies the manifest, loads the scene if it is not already loaded, then applies the keyframe
   snapshot **without interpolation** before its first frame with networked objects visible.

### 5.3 `Manifest`

`u16 count` × `{ u32 netId, u8 guid[16], u8 flags }` for every `NetworkIdentity` in the scene, plus the
host's mapping for runtime spawns. Purpose: deterministic `netId` derivation with a repair path.

**netId derivation (both peers compute the same value):**
`netId = FNV1a32(gameObject.Id.ToByteArray())`, forced non-zero and `< 0x8000_0000`.
Runtime-spawned objects use `0x8000_0000 | counter` allocated by the host. If the manifest disagrees
with a locally computed id for the same Guid (hash collision), the host's value wins and the local
mapping is corrected — collisions are handled, not ignored.

### 5.4 Snapshots and fragmentation

```
Snapshot:  u8 type|u8 ver | u32 serverTick | u8 flags(keyframe) | u16 recordCount | records...
record:    u32 netId | u16 mask | payload
mask bits: 0 position, 1 rotation, 2 velocity, 3 anim/flags, 4 variables, 5 scale
position   3 × f32            (12 B)          rotation  4 × i16 normalized quaternion (8 B)
velocity   3 × i16 @ 1/128    (6 B)           anim      u8 flags + u8 speed + i16 pitch (4 B)
variables  u8 count × { u8 id, i32 value }      scale     3 × i16 @ 1/1000 (6 B, optional)
```

- `keyframe=1` records carry every field of every object (send on join, after a 2 s silence, and every
  5 s as a safety net).
- Only changed fields are set (threshold per `NetworkTransform`); an unchanged object is omitted.
- WebRTC: keep one channel message ≤ 8 KB. UDP: fragment at 1100-byte payloads with
  `u16 fragmentIndex/u16 fragmentCount` in the message header of channel `State`, reassemble with a
  4 MB cap and a 2 s timeout. Never fragment `Events` larger than 4 KB — split game-level payloads
  instead.
- `OwnState` reuses the single-record layout with `netId` omitted (the link identifies the owner).

### 5.5 Tick model

- `NetworkSession` keeps its own accumulator driven from the engine update (no new thread), default
  **20 Hz** send, **60 Hz** interpolation sampling.
- Snapshot application and interpolation run in `OnLateUpdate` order so camera follow and
  `SkeletalAttachmentService` see the final pose for the frame.
- `PortableGameLoop.Tick` already clamps `Time.Update(Math.Min(seconds, .1))`; when the tab is
  suspended for longer than 1 s, treat the next snapshot as a keyframe and reset the interpolation
  buffer (never interpolate across a multi-second gap).
- Host time authority: clients keep `serverTick` + a smoothed RTT/2 offset; nothing else in the game
  uses wall-clock time for gameplay decisions.

### 5.6 Bandwidth and CPU budget (acceptance criteria)

| Metric | Budget |
| --- | --- |
| Upload per peer, 60 networked objects, 4 players | ≤ 20 KB/s average, ≤ 45 KB/s peak |
| Snapshot CPU (build + serialize) | ≤ 1.0 ms per 20 Hz tick on the Vega 10 class target |
| Interpolation CPU | ≤ 0.3 ms per frame with 8 remote avatars |
| Steady-state allocations | 0 bytes per frame and per tick (pooled writers/buffers, like `FrameTransferBuffers`) |
| Join payload | ≤ 64 KB (manifest + keyframe for 200 objects) |

Rate tiers: player avatars 20 Hz; host-owned objects 10 Hz; objects beyond `FarDistance` (default 60 m)
at 5 Hz; beyond `HideDistance` (default 120 m) frozen and sent only in keyframes.



---

## 6. Replication rules

### 6.1 Authority matrix

| Object | Authority | Sends | Receives |
| --- | --- | --- | --- |
| Player avatar | owning peer | `OwnState` 20 Hz | host-validated corrections |
| Enemies, vehicles, machines, doors, pickups | host | `Snapshot` 10 Hz | — |
| Runtime spawns (projectiles with gameplay effect, loot) | host | `Event(spawn)` + snapshots | — |
| Purely local VFX, audio, UI, camera shake | nobody | — | triggered by `Event` cues |

Host-side validation of peer-owned avatars, kept cheap: reject a position update when the implied speed
exceeds `maxSpeed × dt × 1.35` for two consecutive ticks, or when the avatar moves further than 6 m in
one tick. On rejection send `Event(avatar-correction)` with the authoritative transform; the owner snaps
without playing teleport VFX. That is the whole anti-cheat story for v1.

### 6.2 Body handling on receivers

Remote bodies must never be simulated locally. On spawn of a non-owned identity, and on any
`Rigidbody3D` attached to it, set `BodyType = RigidbodyBodyType3D.Kinematic` — the enum in
`Engine/ByteEngine.Core/Physics/RigidbodyBodyType3D.cs` already documents it as infinite-mass, moved
explicitly through its Transform, and still pushing dynamic bodies. Never leave a replicated object
`Dynamic`. Disable `PlayerController3D`, `TopDownController3D`, `ThirdPersonCamera3D`,
`FirstPersonCamera3D` and any input-reading controller on non-owned avatars, and make sure only the
local player's `Camera3D` is the active game camera.

### 6.3 Interpolation

`NetworkTransform` keeps a small snapshot ring (3 entries) per object and renders about 100 ms in the
past (`delay = 2 / sendRate`, clamped 60–150 ms, raised by measured jitter):

- `Interpolate` — `Vector3.Lerp` position, `Quaternion.Slerp` rotation between the two snapshots that
  bracket the render time. Default for avatars.
- `Extrapolate` — hold the last velocity for at most 150 ms, then freeze. Fast projectiles only.
- `Snap` — teleports, respawns, keyframes.
- Write with `Transform.LocalPosition` / `Transform.LocalRotation` in `OnLateUpdate` so camera follow
  and `SkeletalAttachmentService` read the final pose. Read `Transform.WorldPosition` only for distance
  tiers, never for the write, so sockets/vehicles compose correctly.
- Skip objects beyond `HideDistance` (default 120 m) and request a keyframe on re-entry through
  `Event(request-state)`.

### 6.4 Spawn and despawn

- Scene-authored identities exist on both peers after scene load; only their state travels. They join
  the manifest at connect and after every host scene load.
- Runtime spawn: `Event(spawn) { netId, prefabGuid, pos, rot, ownerPeerId, flags }`. Receivers
  instantiate through the existing `BlueprintRuntimeFactory.Spawn(...)`
  (`Engine/ByteEngine.Core/Runtime/BlueprintRuntimeFactory.cs`); a missing prefab logs once and is
  ignored, never thrown mid-frame.
- Despawn: `Event(despawn) { netId, reason }` → `Scene.DestroyGameObject` on receivers. Pooled VFX and
  audio never get identities; replay them from `Event` cues.
- Scene reload: host broadcasts `Event(scene-load) { path, nonce }`; clients reload through their
  existing `SceneFactory`, then re-handshake (fresh manifest + keyframe). A client that fails to load
  within 15 s is disconnected with a reason.

### 6.5 Variables

Replicate only what remote peers must see: health, ammo, held tool, door/machine/wave state. Author
them as a per-game public enum, stored on the `NetworkBehavior` as `u8 id → i32`; more than 8 variables
on one object means the design is wrong — use an `Event` instead.

### 6.6 Outbound path inside the browser sink

`BrowserFrameSink.Finish` already builds `bufferViews` from `FrameTransferBuffers`. Add a packet list:

```csharp
// BrowserFrame.cs
public List<BrowserNetPacket> net { get; set; } = [];   // { peerId, channel, buffer, length, offset }
```

`BrowserNetwork.Flush(sink)` appends queued packets through the same `FrameTransferBuffers.Copy(...)`
path used by draw packets, and `main.js` forwards them right after `transfers.beginFrame(data)`:

```js
for (const p of data.net) net.send(p.peerId, p.channel, read(p.buffer));   // copy inside net.send
```

This keeps per-frame JSON tiny and spends no base64 on gameplay traffic.

---

## 7. Events, cues and trust

- Reliable events carry a per-sender `u16 sequence`; receivers drop duplicates so a retried damage or
  purchase event never applies twice.
- **Damage, kills, loot and economy are host-only.** Clients send intents (`Event(fire)`,
  `Event(interact)`); the host resolves hits with its own physics/raycasts and broadcasts results
  (`Event(damage)`, `Event(hit-vfx)`, `Event(audio-cue)`). Clients never mutate shared state locally and
  expect it to persist.
- Host-side limits per peer: 40 events/s and 8 KB/s of event payload. Exceeding either logs and drops;
  never disconnect on the first burst.
- Never trust client netIds, positions or amounts. Validate ownership, existence and authored range.
- Log one compact `NET` line per connect/disconnect/reject (peer ids, build identity, reason). Keep it
  off the per-frame path.

---

## 8. Integration points — exact files

### 8.1 New Core files (`Engine/ByteEngine.Core/Net/`)

| File | Contents |
| --- | --- |
| `PortableNetwork.cs` | `IPortableNetwork`, `NetworkChannel`, `NetworkPeerInfo`, `PortableNetwork.Backend` |
| `NetworkSession.cs` | static session facade, tick accumulator, ownership registry, join/leave, validation |
| `NetworkProtocol.cs` | message ids, pooled `NetworkReader`/`NetworkWriter`, FNV1a, quaternion/velocity packing |
| `NetworkSnapshot.cs` | record writing/reading, keyframe + dirty mask, rate tiers |
| `NetworkIdentity.cs`, `NetworkTransform.cs`, `NetworkAnimator.cs`, `NetworkBehavior.cs` | component API (§4) |
| `NetworkSettings.cs` | serializable settings (port, tickRate, stunServers[], signalingUrl, maxPlayers, tiers) |
| `UdpNetwork.cs`, `LoopbackNetwork.cs` | native transports |
| `NetworkStats.cs` | counters for the perf overlay |

Conventions: file-scoped namespaces, `sealed` classes, nullable enabled, no LINQ in per-frame paths,
`ArrayPool`/pooled writers, dense statement style matching `Scene`/`RenderWorld`, XML docs only where the
intent is non-obvious.

### 8.2 Host wiring

- `Engine/ByteEngine.Core/Runtime/PortableGameLoop.cs` — run `PortableNetwork.Backend?.Poll()` before
  simulation and `NetworkSession.Tick(seconds)` after `InputActions.Update(...)`; dispose the session in
  `Dispose()` before `_renderer3D.Dispose()`.
- `Engine/ByteEngine.Core/ByteEngineApplication.cs` — desktop editor/player: create `UdpNetwork`, assign
  `PortableNetwork.Backend` next to audio init, shut it down on close. No session in editor edit mode.
- `Player/ByteEngine.Browser/Program.cs` — `PortableNetwork.Backend = Network;` in `Start()`; add
  `[JSExport] NetworkReceive(int peerId, string channel, byte[] data)`; call `NetworkSession.Tick` from
  `Frame(...)` and `Network.Flush(Sink)` where `Sink.Finish` builds buffers. Keep it inside the existing
  `phase="scene update and rendering"` try/catch.
- `Player/ByteEngine.Browser/BrowserFrame.cs` — add `net` (§6.6).
- `Player/ByteEngine.Browser/BrowserNetwork.cs` — `IPortableNetwork` over `net.js` (queue + `Poll`).
- `Player/ByteEngine.Browser/wwwroot/net.js` — WebRTC, invite codes, optional signaling hook.
- `Player/ByteEngine.Browser/wwwroot/main.js` — import `net.js`, wire `ByteEngineSignaling`, forward
  `data.net`, call `game.NetworkReceive(...)`.

### 8.3 Editor and serialization

- `Engine/ByteEngine.Core/Serialization/ComponentSerializer.cs` — register the components next to the
  other `Register(new ReflectionPluginComponentCodec<T>("Name"))` lines (~line 352) so scenes save and
  load them: `NetworkIdentity`, `NetworkTransform`, `NetworkAnimator`, `NetworkSettings`.
- `Editor/ByteEngine.Editor/ComponentMetadataRegistry.cs` — new `"Network"` category appended to
  `CategoryOrder`, entries with `RequiresComponents` (`NetworkTransform` → `NetworkIdentity`) and property
  metadata for send rate, thresholds, interpolation and authority.
- Update any category filter lists that enumerate `CategoryOrder` (`HierarchyPanel`, inspector).
- Do **not** touch the `.byteproject` format or `GameExportPanel` in v1: settings live on the scene
  component.

### 8.4 Build / runtime plumbing (easy to miss)

- `Tools/PublishBrowser.ps1` — add `net.js` to the required-runtime file list (integrity manifest and the
  Dist/editor copies are generated automatically, but the presence check is explicit).
- `Engine/ByteEngine.Core/Runtime/WebGamePackageExporter.cs` — no change needed; it copies the whole
  runtime tree and revalidates `browser-runtime-integrity.json`. Re-run the export smoke test.
- Any Core change requires republishing the browser runtime
  (`Refresh-ByteEngine-BrowserRuntime.ps1` or `Tools/PublishBrowser.ps1 -Configuration Release`): the web
  build is statically linked wasm, so gameplay changes do nothing until the runtime is refreshed.
- Plugin authors use the same components; rebuild plugins into the runtime with the normal publish flow
  and bump the plugin version per `AGENTS.md`.


---

## 9. Phased plan with acceptance criteria

Ship each phase green before starting the next.

### Phase 1 — Transport core (no gameplay)
`PortableNetwork`, `LoopbackNetwork`, `UdpNetwork`, `NetworkProtocol`, `NetworkStats`.
**Done when** `Tests/ByteEngine.Tests --network-transport` runs two loopback peers plus one UDP pair on
127.0.0.1 and proves: ordered reliable delivery under 20 % injected loss, unreliable state dropped when
backed up, 1200-byte datagram limit respected, a 40 KB event fragmented and reassembled, and zero
allocations on the steady-state send path.

### Phase 2 — Session and handshake
`NetworkSession`, `Hello/Welcome/Reject/Manifest`, peer registry, `Ping/Pong`, disconnect reasons,
build and scene fingerprint checks.
**Done when** `--network-session` proves: join accepted with matching fingerprints; rejected with a
specific reason on engine/plugin/scene mismatch; manifest netIds equal `FNV1a32(guid)` on both sides;
host leaving fires `SessionEnded("host left")` on every client.

### Phase 3 — Transform replication (the visible milestone)
`NetworkIdentity`, `NetworkTransform`, snapshot encode/apply, interpolation, rate tiers, kinematic body
handling, `OwnState` for peer-owned avatars.
**Done when** a loopback test over a synthetic 60-object scene shows: remote positions converge under
1 cm error, avatar round trip ≤ 1 tick at zero added latency, bandwidth and CPU inside §5.6, and a 2 s
freeze produces a keyframe resync instead of a rubber-band sweep. Manual check in editor Play mode: two
windows on one machine, host plus join by LAN code, both avatars move smoothly and physics does not
fight the replicated bodies.

### Phase 4 — Browser transport
`net.js`, `BrowserNetwork`, `NetworkReceive`, `BrowserFrame.net`, `main.js` wiring, publish-script entry.
**Done when** `Tools/TestBrowserNetworking.cjs` (Playwright, same shape as `Tools/TestBrowserRuntime.cjs`)
opens two pages of the built runtime, connects them through an injected `ByteEngineSignaling` shim, moves
a peer-owned avatar, and asserts: connected within 5 s, remote transform converges, no console errors,
`NetworkStats` non-zero on both sides, `payloadChars` grew by less than 4 KB, and
`browser-runtime-integrity.json` still validates (59+ resources).

### Phase 5 — Events, spawns, host validation
`NetworkEvent` bus with sequence dedupe, spawn/despawn, avatar correction, per-peer rate limits,
VFX/audio cues.
**Done when** a headless test proves a duplicated event applies once, rate limits drop instead of
disconnecting, a teleporting avatar is corrected on the second violating tick, and 100 runtime spawns
round-trip with no orphan identities. The browser test extends Phase 4 with a fire intent resolved on
the host.

### Phase 6 — Editor authoring and diagnostics
Component metadata and inspector fields; scene validation warnings ("NetworkTransform without
NetworkIdentity", "player avatar without NetworkTransform", "two NetworkSettings"); a `net` block in
`BrowserFrameSink.CaptureStats()` rendered by `wwwroot/performance.js` (peers, RTT, up/down KB/s, tick
drift, drops).
**Done when** the overlay shows live numbers in an exported build and a mis-authored scene fails loudly
in the editor instead of at runtime.

### Phase 7 — Game integration (Hazard Pay, game-side work)
Gate singletons by `NetworkSession.IsHost` (AI, waves, economy, refinery production, spawning), spawn one
avatar per peer, route damage/loot/interaction through host events, disable remote cameras and
controllers, add host/join UI with invite codes from the main menu.
**Done when** two players complete a mining run on itch.io over the internet (STUN path), the host can
pause/leave cleanly, and a late joiner receives current world state without economy or wave desync.


---

## 10. Tests and instrumentation

| Test | Where | Covers |
| --- | --- | --- |
| `--network-transport` | `Tests/ByteEngine.Tests` | both transports, loss/order, fragmentation, allocations |
| `--network-session` | `Tests/ByteEngine.Tests` | handshake, rejects, manifest, host-leave |
| `--network-replication` | `Tests/ByteEngine.Tests` | snapshot encode/apply, interpolation error, keyframe resync, avatar correction, spawn/despawn |
| `Tools/TestBrowserNetworking.cjs` | Playwright (`chromium`, `channel:'msedge'`, swiftshader args, as the sibling tests do) | real wasm build, two pages, WebRTC via signaling shim, budget checks |
| `Tests/ByteEngine.Browser.Tests` | wasm console (`VfxSerializationChecks.cs` style) | protocol codec round-trip |
| Perf | `BrowserFrameSink.CaptureStats` + `wwwroot/performance.js` | §5.6 budgets visible in exported builds |

Instrumentation from the first phase onward: bytes and packets up/down, resends, drops, RTT (min/avg/max),
tick drift, corrections, peer count. Expose as `NetworkStats`, surface in the `NET` overlay block.

---

## 11. Risks and rules

| Risk | Mitigation |
| --- | --- |
| NAT traversal fails for some pairs | Clear error text, configurable TURN, manual code path always available |
| Snapshot bandwidth creeping over budget | Dirty masks, rate tiers, keyframe interval; assert budgets in tests so regressions fail |
| Radius-LOD flicker | Hysteresis (enter 60 m, leave 66 m) |
| Interpolation fighting physics | Replicated bodies are `Kinematic`; only the owner drives its avatar |
| WASM heap view expiry | Copy packet bytes immediately in `net.js`, as `PackedFramePool` does |
| Tab suspended / huge delta | Treat the next snapshot as a keyframe, reset interpolation, never extrapolate across the gap |
| Host pause (Escape menu) | Pause stops snapshots; clients show "host paused" and hold the last pose |
| Scene-load desync | Manifest + keyframe after every host load, 15 s timeout, explicit reject reason |
| Cheating in co-op | Host validates speed, teleports and rates: "trust friends, validate anyway" |
| Reflection/AOT surprises | Register every new component explicitly in `ComponentSerializer`; no reflection over unseen types in wasm |

**Do not:** add a network thread; call GL, physics or scene mutation from transport callbacks; use
`DateTime.Now` for gameplay timing; send JSON per tick; replicate pooled VFX/audio objects; mark
replicated bodies `Dynamic`; introduce attributes that need runtime reflection; change the
`.byteproject` format in v1.

### Definition of done (v1)

1. Two browser peers on different machines connect through an invite code and play the same scene.
2. A third peer joins mid-run and sees the current world within 2 s of the keyframe.
3. Host leaving ends clients gracefully; a client leaving frees its avatar and AI targets.
4. All four acceptance entry points pass, including the Playwright two-page test.
5. §5.6 budgets are asserted and the `NET` overlay shows live numbers.
6. The browser runtime is republished, `browser-runtime-integrity.json` validates, and one real
   two-player session is verified on the itch.io build.

---

## 12. Open decisions (answer before Phase 4)

1. **Peer cap** — 4 (safe on the current wasm memory budget) or 8 (needs the bandwidth tiers tightened)?
2. **PvP or co-op only** — PvP later demands host-authoritative avatar movement with prediction
   (§1 rejected list), which changes Phase 3; co-op keeps peer-owned avatars.
3. **TURN or not** — without a TURN server, roughly 10–15 % of internet pairs cannot connect. Decide
   whether to pay for one before Phase 7.
