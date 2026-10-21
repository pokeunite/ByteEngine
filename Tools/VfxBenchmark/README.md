# Native fire attack benchmark

The benchmark uses existing native VFX, Blueprint, projectile, health and event
sheet systems. The C# fixture authors assets and records the saved scene; it is
not a gameplay dependency. No third-party artwork is included in this repo.

1. Download the free bundle from the publisher:
   https://vefects.itch.io/free-urp-flipbook-vfx-bundle-lite
2. Run `ImportFireArtwork.py <local-unitypackage> <project-directory>` using
   Python with Pillow and numpy. Use a separate project under Documents.
3. Build `Tests/ByteEngine.Tests` in Release.
4. Run the tests executable with `--fire-attack-benchmark <project-directory>`.
5. Open the generated `.byteproject`, press Play; R restarts the scene.

Three native `.bvfx` files expose charge, projectile and impact layers. The caster
event sheet starts the charge and launches the projectile after its timer. The
target event sheet reacts to real projectile damage. The capture reloads the
saved scene and verifies launch, collision and impact before reporting success.

The packed RGB masks are converted to ordinary RGBA with the source shader's
nested colour mapping. This does not implement the source shader's distortion,
erosion or complete Unity rendering. The artwork remains the publisher's work;
do not redistribute it as part of ByteEngine's stock asset library.

Preview captures are correctness/visual evidence, not a hardware FPS benchmark.
