# Reusable runtime quality and interaction

TimedChallenge3D latches its clock on a scene boolean (StartVariable), freezes on FinishVariable, and exposes live elapsed time, three/two/one-star tiers, and a separate reveal clock. It uses fixed simulation time, so pause freezes scoring. RecordKey optionally persists best time per title/level; empty keys disable writes for test runs. BestStars is derived from that best time and the current thresholds. Scene Event Sheets own tutorial sequencing, award animations, sounds and replay. PointerTarget3D.PulseHint adds a restrained warm pulse without activating the target; clearing it restores the authored emission overrides.

The shared spatial edge pass also handles thin subpixel features whose diagonal gradients cancel, blending a bounded axial coverage sample only on detected edges. UI is drawn afterwards at native resolution. Mesh UV stretching is an asset issue and should be corrected at source, rather than blurred away by anti-aliasing.

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

## Native loading presentation

LoadingScreenStyle authors a font, palette and semicolon-separated destination-path=title map. The responsive title, gold bar and vector boat are drawn in the final UI pass, outside 3D tone mapping and render scaling. SceneManager.ProgressiveSceneFactory accepts SceneSerializer.LoadSteps, which constructs objects cooperatively on the graphics thread with an approximately 8 ms work budget between frames. Progress measures completed preparation work, not elapsed time; a single expensive asset can still exceed that budget. Scene activation completes the bar, then gameplay begins after the completion frame. No minimum loading delay is imposed. Synchronous SceneFactory remains supported. Failed file loads restore the prior scene. Hosts with external runtime-scene state can use SceneLoaded to update it only on activation.

UiInteractionAudio provides scene-wide button-entry feedback for pointer and keyboard focus, including paused simulation. Set HoverSourceName to a quiet AudioSource3D on the UI bus. Hidden/disabled buttons are excluded; holding focus does not repeat; a short cooldown limits rapid sweeps. PointerInteraction3D.ShowContextPanel=false removes the context label without disabling world picking or highlights.

## Water and machinery polish

WaterSurface3D separates calm normal strength from flow activity, eases foam response using actual received flux, and lets body wakes decay after velocity stops. Buoyant bodies are cached per scene rather than scanned/sorted for every water surface each frame. WaterDynamics3D remains the source of local waves; shader detail does not change conserved water volume or cargo buoyancy.

WetSurfaceContact3D applies transient renderer-owned material overrides to RendererRoots. Up to three BasinNames provide world-space bounds and live heights; Margin covers adjoining stone thickness. With no basins, SeaLevel provides shoreline wetness. The shader darkens and lowers roughness below a slightly broken waterline. Defaults leave unrelated materials unchanged. Overrides are restored on stop; shared imported assets remain intact. These fields participate in material copy/clone and render-state equality so batching cannot mix different wet regions.

BuoyantPulley3D exposes TravelSpeed, IsReady and ReadySequence for visual events. Configure ReadyTravel using real cargo clearance. Use movement, rather than inlet flux, to drive winch audio; stop it at clearance or rest. Cables should continue following physical transforms. DockingReceiver3D adds a small damped contact dip/roll, exposes Unloading and CargoLanded, and holds Delivered until the final landing settle ends. Keep time medals based on the completed sequence.

## Validation

Headwater's Tools expose leveloneqa and roomperformance. Interaction checks cover shared UI/world activation, click holding, clearing selection, focus handoff, paused inspection, render-scale bounds and native serialization. The three-run benchmark renders the complete room at High, 1280 x 720, scale 1.0 and records average FPS plus p95/p99 frame times per phase, with warm-up and scene-load frames excluded. Drivers only operate the normal input variables; they do not teleport cargo or force delivery. The results retain slow frames rather than treating average FPS as a guarantee of every frame meeting its budget.
