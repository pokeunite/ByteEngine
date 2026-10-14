# Browser crash reports

The tester supplied only three dotnet.js stack locations from itch build 19716987. These locations belong to the runtime bridge; no exception name, message, gameplay action or managed call site was supplied. The specific fault is not diagnosed or verified fixed.

Updated behavior:
- Frame exceptions preserve their full managed stack, scene, dimensions and failure phase in a separately readable export, so runtime bridge formatting cannot discard the only useful details.
- The error screen offers Save error report and Retry loading, with technical details expandable instead of covering the screen with a stack dump.
- Failure events remain recorded even if the tester paused performance capture; existing recent scene/performance data remains in the downloadable JSON.
- Duplicate global error notifications do not recreate the error UI.
- Audio completion ignores malformed voice IDs instead of throwing a Guid parse exception across the browser bridge.

Checks: Release browser build; browser crash-report regression covering paused capture, scene context, managed message, download control and duplicate errors; JavaScript syntax checks.

Next useful reproduction: send the downloaded JSON and the action before the crash. Diagnose the actual exception, reproduce it in a private fixture, add a targeted regression, then rebuild the runtime. Do not treat swallowing all gameplay exceptions or just hiding the error screen as a fix.

Updating BrowserRuntime does not alter an already uploaded itch game. A fresh game export and upload are required for testers to receive this reporting change; no game export is authorized by this work.
