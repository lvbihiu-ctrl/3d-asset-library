# V2 UI ↔ Windows updater seam (synthetic, not installed)

State controller for **Issue #18**. Reused from tested isolated updater preview (7 unit cases). Actual WebView2/.NET service implementation and Velopack upgrades belong to **Issue #16** and the Windows Codex side.

- The HTML host may call `createUpdateFlow({callNative,confirmInstall})`.
- `checkForUpdates` must have a real native handler; if missing show error, **never** show 'latest'.
- Only Windows native host with a verified trusted package may return `verified:true` and consistent version numbers.
- Install is separate and requires explicit user confirmation; native service verifies package, schedules restart, and only then returns `{restarting:true}`.
- Failed checks/install show error rather than fake success; package signatures/downgrade protection are native service responsibilities.
- Test this browser controller using `node --test v2/ui/update/bridge.test.cjs` under Node 22.
- This does **not** mean the full new UI, updater, installer or C4D FileDrop works. Do not ship until corresponding Windows test evidence.

Cross-team action contracts (proposed):
- `{id,action:'checkForUpdates'}` → `{verified,currentVersion,latestVersion,available}`
- `{id,action:'installUpdate'}` → `{restarting:true}` *only after signed package verified and restart scheduled*
- `{id,action:'listAssets',payload:{...}}` → paginated sanitized summary without user filesystem reading from JS
- `{id,action:'beginModelDrag',payload:{assetId}}` → native host validates ID and initiates Windows FileDrop; no arbitrary untrusted path input accepted
- `{id,action:'locateAsset',payload:{assetId}}` → resolve valid indexed path, otherwise offline/missing error
- `{id,action:'setCoverFromClipboard',payload:{assetId}}` → only allow image clipboard and user data directory writes
- `{id,action:'searchSimilarClipboard'}` → dHash compare only (no unsupported 'AI recognition' claim)

All actions are **proposals** until both host and UI tests are green. Do not infer code completion from this README.
