# Starter projects and exported game content

Choose **New Project → Last Stand** in the Project Browser, enter a name and
location, then create the project. The editor opens an independent editable copy.
Press Play and click Game View. WASD moves, the mouse looks, and left mouse fires.
The project contains the shotgun, zombie waves, HUD, medkits and ragdoll behavior.
`START-HERE.txt` points to the Blueprints and Event Sheets that implement them.

Last Stand is distributed as a compressed local starter package of approximately
10 MiB. It works offline. Music, development logs, temporary files, and unused
height/OpenGL-normal maps are excluded. Model animation and socket sidecars,
asset GUID metadata, project inputs and classification settings are preserved.
Creating a starter never modifies the original Last Stand project.

The Project Browser displays an educational asset notice, and every copy includes
`ASSET-NOTICE.txt`. Third-party assets belong to their original creators.
Educational use does not override their licenses. Check the original license and
attribution requirements before reuse or redistribution. The music is excluded.
The notice is also copied to game exports when it exists at the project root.

The 3D Starter and ByteArena explicitly enable built-in primitives for their
generated renderers, including the projectile Blueprint.

## Game exports

New Windows and web exports store project assets, scenes and runtime model
metadata in **Game.bytepak**, instead of loose `Content/Assets` folders. The
package uses a versioned binary layout, per-file zlib compression and SHA-256
integrity checks. The runtime rejects unsafe paths, duplicate entries, unsupported
versions and damaged content. Existing loose-content builds remain supported.

Windows creates a unique temporary content directory for the running game because
native model importers require file paths. The directory is removed on normal
shutdown; a forced termination or crash may leave it behind. Browser games unpack
into the WebAssembly runtime's virtual filesystem using browser decompression.

This is packaging, not encryption or DRM. It removes casually browsable original
files from the exported game folder, but someone who can run a game can recover
its content. Original editable assets remain in the authoring project. Packing
also does not change the rights granted by an asset's license.

## Updating the starter

After saving and closing the source project, run:

```powershell
./Tools/PrepareLastStandStarter.ps1 -ProjectFile 'C:\path\LAST STAND.byteproject'
```

The tool prepares the reduced package, checks installation and scene loading,
then updates `Editor/ByteEngine.Editor/Resources/Starters/LastStand.bytepak`.
Refresh the editor distribution afterward with the normal packaging workflow.
