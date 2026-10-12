# ByteEngine: UE5 checklist - simplicity first

Reviewed 11 October 2026. UE5 5.8 branch, Build.version 5.8.4, commit 4dced763583b8fa05d214b133c2c21b73da300d4.

Focused architecture/interface review, not exhaustive parity audit. All checkboxes are future work; existing foundations are acknowledged by section. FIRST = stability and daily workflow; NEXT = reusable capability; LATER = advanced optional work.

## Start here: keep ByteEngine simple

Keep GameObjects, components, prefab-style Byte Blueprints and events. UE5 is a reference for capability and architecture. This is a focused source review and proposed backlog, not a claim of feature parity or a complete Unreal audit.

- [ ] One clear workflow [FIRST] - Build an object with components, save it as a Blueprint, add behavior through events. No separate character Blueprint types or mandatory extra graph editors.
- [ ] Basic settings first [FIRST] - Show the few useful controls by default. Put specialist options under Advanced; offer presets and reset buttons everywhere.
- [ ] One system owns each job [FIRST] - Define who owns transforms, physics, camera, animation and saving. Stop different systems silently fighting over the same object.
- [ ] Safe schema upgrades [FIRST] - Version scenes, Blueprints, components and saves. Migrate old data, preserve unknown components and show actionable errors.
- [ ] Reliable fixed simulation [FIRST] - Build on the existing fixed clock. Migrate remaining simulation callbacks safely; verify identical input at 30, 60 and 144 FPS.
- [ ] Predictable component lifetime [FIRST] - Make creation, activation, destruction, scene changes and deferred events safe. Cancel work when its owner disappears.
- [ ] Portable engine services [FIRST] - Keep platform details behind shared rendering, audio, input, save and loading interfaces. Components should not need desktop/browser forks.
- [ ] Make performance visible [FIRST] - Extend existing captures with loading stages, stalls, memory, asset ownership and object attribution. A slow frame should point to its cause.
- [ ] Prove changes in small scenes [FIRST] - Maintain FPS, TPS, physics, animation, UI and streaming test scenes. Reject changes that break existing game projects.

## Assets, Blueprints and loading

Already present: GUID assets, model import, dependency tracking, background imports, cooked models, prefab variants and override controls. Improve these foundations rather than replacing them.

- [ ] Async loading from request to GPU [FIRST] - Use cancellable asset handles with progress, priority and ownership. Decode off-thread and budget uploads so gameplay does not freeze.
- [ ] Keep shared assets loaded [FIRST] - Cache shared meshes, textures, sounds and shaders across scene changes. Show why each asset remains resident; release unused resources safely.
- [ ] Load only the next scene needs [FIRST] - Cook dependency bundles and preload the next playable area. Avoid unpacking the whole game or reloading the garage after every contract.
- [ ] Portable cooked content [FIRST] - Make export/import settings part of cache keys. Reuse unchanged results; validate plugin versions, dependencies and resource hashes before launch.
- [ ] Texture streaming and compression [NEXT] - Add mip-aware residency and platform texture formats with memory limits. Include browser-compatible fallbacks and explicit color-space handling.
- [ ] Blueprint editing that stays predictable [NEXT] - Strengthen nested prefab overrides, variants, missing references and conflict previews. Show exactly what Apply and Revert will change.
- [ ] Data assets and reusable settings [NEXT] - Give designers typed data tables, curves and shared settings assets. Components and events can reference them without custom code.
- [ ] Dependency and size inspector [NEXT] - Find unused assets, missing references, duplicated content and large files. Show export size before packaging.
- [ ] Safe project upgrades and recovery [NEXT] - Provide autosave recovery, backups, broken-reference repair and plugin update previews. Never silently replace project source with stale copies.

## Gameplay, cameras and events

Already present: character movement, FPS/TPS cameras, action input, event logic and native save storage. Make these dependable and reusable across games.

- [ ] Finished character movement [FIRST] - Harden steps, slopes, moving platforms, crouch clearance and grounding. Expose simple movement presets and visible debug tools.
- [ ] One camera owner [FIRST] - Add camera priority and smooth switching, obstruction handling, shake and recoil. Keep eye and first-person arms editing directly in the FPS component.
- [ ] Input contexts and rebinding [NEXT] - Extend action input with gameplay/menu/vehicle contexts, priorities, hold/tap triggers, remap persistence and device-aware button prompts.
- [ ] Reusable game-session component [NEXT] - Handle player spawning, restart, pause and win/lose rules. Keep it a simple component instead of requiring a hierarchy of game framework classes.
- [ ] Named signals and timers [NEXT] - Add typed object-to-object events, subscriptions, cancellation and reusable timer handles. Avoid checking everything every frame.
- [ ] Event-sheet debugging [NEXT] - Build on current traces with pause, step, breakpoints and live variable inspection. Click an error to open the responsible event or component.
- [ ] Event functions and reusable modules [NEXT] - Support parameters, return values, scoped variables and clear async completion. Share behavior without copying whole event sheets.
- [ ] Abilities and attributes [NEXT] - Offer optional components for health, stamina, costs, cooldowns and status effects. Use simple tags and events; hide networking machinery until needed.
- [ ] General interaction and progress [NEXT] - Promote reusable interaction, objective, reward and tutorial primitives from game plugins. Let each game choose rules and presentation.
- [ ] Save authoring in the editor [NEXT] - Build on GameSaveService with saveable fields, slots, checkpoints, schema migration and New Game reset scope. Show what will be saved.

## Animation and AI

Already present: skeletal animation, profiles, blend spaces, layers, root motion, IK foundations, ragdoll and navigation. Better integration matters as much as more features.

- [ ] Animation import confidence [FIRST] - Validate skeleton compatibility, axes, scale, bind poses and root motion. Preview the result before accepting an import.
- [ ] Simple retargeting workflow [NEXT] - Add a visual rig map, retarget pose and side-by-side preview. Reuse animations across characters without manual file edits.
- [ ] Clear state and action authoring [NEXT] - Improve existing animation profiles with transition previews, interruption rules and event timing. Keep common locomotion editable as a small table.
- [ ] IK and contact polish [NEXT] - Make foot placement, hand targets and look-at easy to author. Add contact locking and debug overlays to diagnose sliding or twisted limbs.
- [ ] Root motion and movement agree [NEXT] - Give one system authority over motion. Support predictable collision, action interruption and optional motion warping to a target.
- [ ] Animation budgets and skinned LOD [NEXT] - Reduce update frequency for distant characters, share compatible work and support skinned mesh LODs. Preserve important gameplay events.
- [ ] Inspectable AI state component [NEXT] - Add states such as Patrol, Chase and Attack with conditions and tasks. Show the active state and reason for each transition.
- [ ] Perception and decision inputs [NEXT] - Provide sight, hearing, memory and shared AI values. Events should react to changes rather than constantly scan the scene.
- [ ] Navigation in moving crowds [NEXT] - Extend existing navigation with avoidance, runtime obstacle updates, async path queries and traversal links. Show failed-path reasons.
- [ ] Advanced animation and crowds [LATER] - Optional pose search/motion matching, richer rig controls and crowd simulation. Add these after ordinary character animation is reliable.

## World building and physics

Already present: terrain, foliage, collision shapes, angular contacts, sleeping, swept tests and joints. The construction vehicle solver remains a separate backend with an explicit ownership boundary.

- [ ] Streamable scene sections [NEXT] - Add editable cells or subscenes with load/unload distances and shared references. Start with a simple streaming grid and clear memory budgets.
- [ ] World layers and distant proxies [NEXT] - Group optional world content into layers; generate distant combined meshes. Preserve editing and runtime identity across streaming.
- [ ] Terrain layers and better authoring [NEXT] - Add non-destructive sculpt/paint layers, splat materials, road tools and erosion presets. Keep brushing responsive and undo reliable.
- [ ] Scatter tools with rules [NEXT] - Paint or generate rocks, plants and props using seed, slope, density and exclusion areas. Preview and regenerate without destroying manual edits.
- [ ] Mesh and collision editing [NEXT] - Offer basic cut, merge, pivot, simplify and collision-generation tools. Complex modeling can stay in Blender.
- [ ] Choose a general physics strategy [FIRST] - Benchmark extending the current solver versus a mature backend. Preserve existing games; do not silently merge it with vehicle physics.
- [ ] Physics reliability suite [NEXT] - Strengthen stacks, fast and rotating collisions, contacts, filtering and queries. Add profiler evidence before changing solver quality defaults.
- [ ] Joint and ragdoll authoring [NEXT] - Provide visible joint limits, motors, break forces and per-bone bodies. Preview behavior without repeatedly launching the game.
- [ ] Reusable vehicle tools [NEXT] - Expose vehicle, suspension, traction and winch capabilities as optional reusable components with remappable inputs and clear diagnostics.
- [ ] Optional specialist simulation [LATER] - Destruction, cloth, water/buoyancy and large-world origin handling. Add only with representative games and explicit platform support.

## Rendering and effects

Already present: PBR, environment lighting, shadows, post effects, static instancing/LOD and VFX. Nanite/Lumen-class features are major research projects, not a few settings to copy.

- [ ] Correct rendering across platforms [FIRST] - Test linear/sRGB textures, normal maps, transparency, sky exposure and tone mapping. Clearly label intentional browser quality reductions.
- [ ] Renderer backend and pass scheduling [NEXT] - Separate graphics API work from game components. Track pass dependencies and resource lifetimes; retain a lightweight browser path.
- [ ] Better lighting tools [NEXT] - Improve shadow stability, bias, filtering and local-light limits. Add lighting previews, baked lighting/probes and reflection captures.
- [ ] Temporal image quality [NEXT] - Add stable anti-aliasing, dynamic resolution and upscaling presets. Validate motion artifacts instead of hiding them behind a quality label.
- [ ] Visibility and geometry budgets [NEXT] - Extend instancing/LOD with occlusion culling, shadow batching and world proxies. Measure draw calls and memory in actual game scenes.
- [ ] Materials people can understand [NEXT] - Keep simple material presets; add optional reusable material functions, instances, decals and an advanced graph with readable errors.
- [ ] Professional atmosphere [NEXT] - Improve sky/cloud/fog controls, exposure zones and post-process blending. Provide a few coherent presets instead of many unrelated sliders.
- [ ] Reusable effects with budgets [NEXT] - Extend VFX with curves, emitters, collision and gameplay signals. Add pooling, preview and performance limits; GPU simulation is optional.
- [ ] Dynamic indirect lighting [LATER] - Prototype a scalable GI/reflection approach with low-end fallbacks. Decide on software/hardware paths after profiling; Lumen itself is not a drop-in.
- [ ] Virtualized high-detail rendering [LATER] - Research mesh/texture virtualization and modern GPU-driven rendering. Nanite-class fidelity and ray tracing belong in an optional desktop tier.

## UI, audio and cinematic tools

Already present: responsive UI containers, themes, focus, native/browser audio buses and music streaming. Keep authoring visual and feedback immediate.

- [ ] Visual UI layout editing [NEXT] - Add drag handles, anchors, constraints, reusable widgets and layout diagnostics. Preview common screen sizes and safe areas.
- [ ] UI state and data binding [NEXT] - Bind text, visibility, progress and selection to component values. Add hover, press, disabled and focus styles with simple transitions.
- [ ] Accessible, localizable menus [NEXT] - Add text localization, font fallbacks, scalable text, contrast options and reliable keyboard/gamepad focus. Test long labels.
- [ ] UI polish components [NEXT] - Provide tooltip, toast, dialog, tutorial callout and loading indicator components. Games choose themes and sounds.
- [ ] Audio mixer and effects [NEXT] - Extend buses with EQ, compression, reverb and routing. Give each effect clear presets and show peak levels.
- [ ] Sound environments [NEXT] - Add attenuation curves, occlusion and room/reverb zones. Keep browser fallbacks and voice/memory budgets explicit.
- [ ] Adaptive sound and music [NEXT] - Support layered engine audio, variation, crossfades and event-driven music cues through components and simple sound recipes.
- [ ] General gameplay timeline [NEXT] - Add camera, transform, animation, audio and event tracks. Extend animation timeline foundations rather than creating disconnected editors.
- [ ] Advanced cinematic controls [LATER] - Optional camera rails, shot blending, recording, video playback and render capture. Film-production tooling stays outside the default workflow.

## Networking, shipping and developer tools

Windows and browser packaging already work, with a capability matrix and automated checks. Multiplayer and additional targets need separate design and validation; an Unreal source download does not supply them automatically.

- [ ] Small multiplayer foundation [NEXT] - Add optional server authority, object identity, spawning, replicated fields and remote events. Start with a simple two-player test game.
- [ ] Responsive networked movement [LATER] - Add snapshot interpolation, prediction, reconciliation and lag compensation. Test loss, latency and bandwidth before exposing advanced knobs.
- [ ] Sessions and online features [LATER] - Offer optional lobby, join/leave, reconnect and save/session integration. Keep platform services behind provider interfaces.
- [ ] Headless player and replay hooks [NEXT] - Support a render-free server/test runner and input/session recording. Do not promise deterministic replay across machines without evidence.
- [ ] Trustworthy export pipeline [FIRST] - Validate startup scenes, plugins, assets and save scope. Produce clean packages, build IDs and useful error reports for every target.
- [ ] Browser-first delivery checks [FIRST] - Test cold/warm cache, retry, offline failure, context loss, memory limits and real hosted startup. Show a loading bar and avoid unnecessary scene reloads.
- [ ] Shader and asset warm-up [NEXT] - Precompile/cook what each target supports and stage uploads. Measure startup separately from first-use gameplay hitches.
- [ ] Editor iteration and source control [NEXT] - Improve safe live preview, play-change inspection, asset diff/merge and reference-aware renaming. Keep Git integration optional.
- [ ] Repeatable performance gates [NEXT] - Compare fixed captures across builds for frame time, allocations, GPU work, memory and loading. Test real hardware as well as software rendering.
- [ ] More platforms and extensions [LATER] - Add platform adapters and stable plugin APIs before Linux/mobile/console/XR targets. Certified SDK access and per-target validation remain separate work.

## Review notes

**UE5 source:** Downloaded a shallow, sparse reference checkout under D:/ue4 sources/UnrealEngine-5.8. This is selected source for review, not a complete build-ready Unreal installation. Existing UE4 files were preserved.

**Pinned version:** Branch 5.8; Engine/Build/Build.version reports 5.8.4. Commit: 4dced763583b8fa05d214b133c2c21b73da300d4. The release branch was not used, so this does not claim to be the newest release.

**UE5 interfaces inspected:** CharacterMovementComponent, PlayerCameraManager, Enhanced Input, AssetManager, StreamableManager, WorldPartition, AnimInstance, IKRig retargeting, PhysicsAsset, NavigationSystem, StateTree, GameplayAbilities, GameplayTags, Niagara, PCG, UMG, SoundSubmix, MovieScene, NetDriver, SaveGame and renderer module dependencies.

**ByteEngine compared:** Current Core and Editor source inventory; fixed clock; action input; FPS camera; animation controller/advanced pose; navigation agent; renderer submissions; save service; existing roadmap implementation and platform-capability notes. Existing features are not assumed absent merely because UE names differ.

**How to use this list:** Work FIRST items before adding advanced tools. NEXT adds broad game-making capability. LATER holds expensive or specialist work. Each item needs authoring, serialization, runtime behavior, debugging and target-specific tests before ticking it.

**Boundary:** This is a game-engine capability roadmap, not a full catalog of Unreal film, CAD, virtual-production or industry integrations. Advanced rendering, large-scale simulation and multiplayer are substantial workstreams. Source review here does not establish performance or implementation completeness.

**Implementation rule:** Study architecture and build ByteEngine-native systems. Keep licensed Unreal source out of the ByteEngine repository and do not paste Unreal implementations into it.

## Sources

- [UE5 source at the reviewed commit](https://github.com/EpicGames/UnrealEngine/tree/4dced763583b8fa05d214b133c2c21b73da300d4)
- [Epic: gameplay framework](https://dev.epicgames.com/documentation/en-us/unreal-engine/gameplay-framework-in-unreal-engine)
- [Epic: World Partition](https://dev.epicgames.com/documentation/en-us/unreal-engine/world-partition-in-unreal-engine)
- [Epic: Lumen technical details](https://dev.epicgames.com/documentation/unreal-engine/lumen-technical-details-in-unreal-engine)
- [Epic: Gameplay Ability System](https://dev.epicgames.com/documentation/unreal-engine/gameplay-ability-system-for-unreal-engine)
- [Epic: Unreal Engine EULA](https://www.unrealengine.com/eula/unreal)