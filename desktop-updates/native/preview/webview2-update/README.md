# Preview: Native Update Button Contract

Only an **isolated browser-side update state controller**; not a completed Windows updater or production UI.

Run: `node --test desktop-updates/native/preview/webview2-update/bridge.test.cjs`.

The test-first cycle was performed locally: initially the test failed because `bridge.js` did not exist; once the minimal controller was implemented, 7/7 tests passed.

For the user-approved HTML candidate, include `bridge.js` as a local script and use `window.NativeUpdateFlow.createUpdateFlow({callNative,confirmInstall})` to control `checkForUpdates` and `installUpdate` button states. `callNative` must be wired to the **real** Windows WebView2 native host; a mock is never a valid production update service.

A trustworthy host response to `checkForUpdates` looks like:
```json
{"verified":true,"currentVersion":"0.1.7","latestVersion":"0.1.8","available":true}
```
The `verified` flag must only be set by the Windows native updater *after* GitHub Raw anti-cache request, Contents API fallback when needed, origin and pinned-commit validation. For `installUpdate`, only reply `{"restarting":true}` after verifying the update package hash, backup and scheduling restart. Unknown, unavailable, malformed or unverified states must not offer installation.

**Not tested:** WebView2 wiring, .NET runtime/host delivery, native SHA256 download/rollback, C4D FileDrop or Windows actual restart. No published v0.1.7 code or manifest is modified. No customer renders or models are included.

Next TDD seam: a Windows native host which implements these messages using the existing v0.1.7 update rules, with a Windows-only integration test and explicit dependency delivery/restart plan.
