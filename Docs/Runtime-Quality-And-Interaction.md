# Reusable runtime quality and interaction

AudioSettings supplies optional per-title persisted Music and Sfx gains through the shared AudioMixer (Sfx also governs Ui). Scene AudioSource3D gains remain their authored mix; flux-driven water audio therefore respects the player's mixer settings. Use Streaming for long music tracks to avoid loading whole decoded tracks into memory. Empty SettingsKey disables disk persistence. Settings are applied on start and saved only on changes; malformed preference files leave defaults intact and expose LastError.

DockingReceiver3D is a reusable opt-in receiver: carrier and cargo must both enter CaptureCenter/CaptureSize. It switches those bodies to kinematic, eases the carrier to MooringPosition, then moves cargo to CargoDestination through a short unload arc. Captured, Delivered and Progress are read-only event inputs. Scene Event Sheets own cues, success panels, replay and scene navigation. Receiver motion uses fixed simulation time, so pause freezes the entire sequence. Author a destination on a real dock surface; the receiver does not invent a physical dock or crane.

## Rendering

The shared native water shader skips opaque BRDF evaluation, directional/point shadow filtering and diffuse/specular IBL when refraction provides the lit submerged scene. Those results were previously calculated and then overwritten by the optical water branch. The fallback without a refraction capture retains its lighting. Water impact foam also writes triangles directly into its reusable stream, removing the per-face array allocations from active spray rendering. This applies to every Water Surface, Water Flow Path and Water Ocean, without Headwater names or layout tests in the renderer.

Sky Environment's Render Scale is bounded to 0.5-1.0 and saved by both environment codecs. The standalone runtime and editor Game View rasterize 3D into a scaled HDR/depth target, then present it at the requested output size. UI canvas layout, input coordinates and final UI rendering retain output resolution. RenderContext reports separate RasterWidth/RasterHeight for LOD and geometry profiling. The editor Scene view intentionally retains its authoring resolution.

Existing Fast/Balanced/High quality levels now also bound water reflection steps (off/10/20), low-quality refraction filtering and ocean tessellation (96/128/256 maximum segments, capped by the artist's requested Segments). Shallow-water cells, hydraulic time steps, ocean CPU sampling and gate mechanics are independent of these visual settings. Custom retains the existing high-detail water path. Shadow caps continue through GraphicsLook.

Add Graphics Settings (GraphicsSettings3D) for runtime quality and render scale. Visual events can assign Quality and RenderScale. Give SettingsKey a unique game identifier to retain preferences across scene resets and restarts; leave it empty to disable disk persistence. Preferences live under LocalApplicationData/ByteEngine/Graphics. Preserve Post Effects retains the artist's AO/bloom when changing detail presets. Invalid values are bounded and failed preference I/O is reported through LastError.

## Interaction

Add Pointer Target to each inspectable/clickable object. PickCenter/PickSize define a local box transformed with the object. LinkedButton references an existing UI button. HighlightObjects is a semicolon-separated list of visual roots. StatusText describes the present state; ActionText describes the next click; Description is optional help.

Add one Pointer Interaction manager per scene, set CanvasName and FontReference, and keep it before the scene's Event Module object. It chooses the nearest eligible pick box, links world and UI focus, and draws one content-sized context panel. Click empty space to clear selection. SelectTarget(name) permits inspection focus from other interfaces without activating anything. Call RefreshTargets after dynamically adding/removing targets or UI. Read WasActivated in Event Sheets to drive existing commands; the picker contains no puzzle or machinery logic. AllowActivation=false permits inspection while gameplay is paused.

Highlights use renderer-owned material overrides, restoring prior emission overrides on deselection/stop. Shared imported material assets are never edited. Pointer boxes do not add physics collision. This explicit interaction picker is separate from the physics occlusion/raycast system: choose boxes around visible controls, rather than enclosing an entire building. Keyboard/controller focus is shown while navigation is in use; moving the pointer restores mouse targeting.

UiWidget.BlocksWorldPointer prevents interacting with world targets through visible help/settings panels. Buttons/sliders already block the picker. UI pointer blocking also respects this panel flag for normal gameplay input.

Water Flow Indicator draws a few optional arrows using delivered gate flux and actual basin wet depth. Configure a BasinName or a BasinPrefix/BasinCount chain, plus Start/End. Closed/dry routes render no arrows. It neither creates flow nor changes water volume.

## Validation

Headwater's Tools expose interactionqa and roomperformance. Interaction checks cover shared UI/world activation, click holding, clearing selection, focus handoff, paused inspection, render-scale bounds and native serialization. The three-run benchmark renders the complete room at High, 1280 x 720, scale 1.0 and records average FPS plus p95/p99 frame times per phase, with warm-up and scene-load frames excluded. Drivers only operate the normal input variables; they do not teleport cargo or force delivery. The results retain slow frames rather than treating average FPS as a guarantee of every frame meeting its budget.
