# Browser plugin update propagation

Web gameplay plugins are statically compiled into BrowserRuntime. Project .byteplugin replacements update native gameplay but cannot replace code inside an existing WebAssembly runtime. The bundled runtime dated 19:09 preceded Dune 0.16.1, explaining the visible delivery pad and missing automatic garage return.

Tools/PublishBrowser.ps1 now writes browser-plugin-builds.json with linked plugin versions and binary build provenance. WebGamePackageExporter checks every enabled runtime plugin version before creating an export; absent metadata, missing plugins and different versions stop export with a refresh message. Version checks intentionally do not equate desktop and browser DLL hashes, since target-specific compilation produces different binaries. Increment plugin versions on code updates.

The desktop exporter guard is installed in Dist and Debug editor Core assemblies. Browser runtime is rebuilt and copied to those editor runtime directories. The checked-in AGENTS workflow now requires refreshing the browser runtime on Dune plugin updates. Existing itch uploads remain unchanged; the user must export and upload again. No game export was created by this task.

Validation: missing metadata rejected, matching version accepted, newer project version rejected, existing package chunk boundaries pass. Native Dune recovery checks from the prior task cover the same linked code's invisible delivery zone and timed garage return.
