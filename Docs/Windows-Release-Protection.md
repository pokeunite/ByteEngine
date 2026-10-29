# Windows release protection: current audit and proposal

## Inspected build

`Hazard_Pay-Windows-20261029-093419-d1c6e8` contains a small native launcher,
`Game.bytepak`, metadata and a self-contained .NET player in `Runtime`.
`ByteEngine.Core.dll` and `ByteEngine.Player.dll` contain managed executable code.
They can be inspected with decompilers. Most other DLLs are .NET/OpenGL/audio/
model-import dependencies. `System.Windows.Forms.Design.Editors.dll` is a
framework dependency, not the ByteEngine editor.

The initial runtime inventory found no loose C# source, PDB files, ByteEngine
editor/assistant assemblies or key/certificate files. This is an inventory
check, not a guarantee that arbitrary authored game data cannot contain secrets.

## Recommended approach

1. Keep the current functional Windows export while adding a repeatable release
   audit for accidental source, symbols, editor assemblies and credentials.
2. Introduce an optional bundled release player after testing package lookup,
   native-library loading, plugin loading and clean-machine startup. The current
   exporter validates loose Core build identity and the native launcher starts
   `Runtime/ByteEngine.Player.exe`; both paths need a coherent format update.
3. Consider compatible managed-code obfuscation only with explicit reflection,
   serializer, event/component ID and plugin API preservation. Renaming reflected
   properties indiscriminately would break authored scenes and saves.
4. Sign the Windows executable and verify a signed content manifest to detect
   modified distributions. This raises the bar; an offline attacker controlling
   the executable can still patch the checks.
5. For stronger reverse-engineering resistance, investigate a Native AOT player
   with generated component codecs and statically linked runtime plugins. This
   is a separate engineering task: the current dynamic plugin/reflection design
   cannot safely be converted by toggling one publish setting.

Single-file deployment embeds managed assemblies but is not encryption or an
anti-extraction guarantee; native files may still be extracted for loading.
Native AOT produces machine code but remains reverse-engineerable. Never ship
service/API secrets in a client, including inside an encrypted package whose
key is included with that same client.

No export format or supplied build is modified by this proposal. The existing
export should retain its complete Runtime folder until a new format is tested.

## Primary references

* [Microsoft: single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
* [Microsoft: Native AOT limitations](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)

Use `Tools/AuditWindowsRelease.ps1 -BuildDirectory <existing build>` for a
nonmutating inventory check. Keep its report with release validation evidence.
