# UI Migration — TDD and Rollout Plan (Preview Only)

**Current release:** v0.1.7 WinForms. **Release manifest and EXE updater remain frozen.** No claim of working new EXE until Windows/C4D signoff.

## Accepted UI
Existing local HTML source is the visual basis: PureRef-like auto masonry, **dual virtual category + real folder filtering**, quick asset find as the primary workflow, light default/dark optional. Remove the *three prototype design-choice panels*, any tutorial banner, demo badge, and export-design-choice footer. Demo images are supplied by the user and are **not linked to real model entries**; do not upload them to GitHub.

## The hard technical boundary
`desktop-updates/native/app-v0.1.7.ps1` currently performs a cache-busted GitHub Raw check, Contents API fallback, pinned GitHub-commit PS1-only download, SHA256 verification, old override backup, and restart.
`desktop-updates/native/launcher/Program.cs` resolves and launches versioned PowerShell scripts. A WebView2 host/managed component is **not** automatically installed by this PS1-only update channel.
An HTML mock `checkForUpdates` / `installUpdate` message does *not* implement the native update service. Browser-only Playwright cannot verify C4D FileDrop or Shift merge.

## Small verified slices: Red → Green
1. **Browser→native bridge seam**. Input: `{id, action:'checkForUpdates'}`; result: explicit version, availability, failure reason, and required installation action. Test no-native-host path informs users instead of pretending to install. Input: `installUpdate` only after a verified newer manifest and confirmation. Tests use synthetic fixtures, not real downloads.
2. **Native updater seam**. Freeze current version in each test. Cases: Raw cached old but API new; both channels down → error, not 'latest'; malformed source URL/commit/SHA rejected; SHA mismatch retains prior runtime; backed-up override and launcher restart confirmed on a Windows fixture. Reuse v0.1.7 guarantees rather than reimplementing a looser 'fetch latest' helper.
3. **Model FileDrop seam**. Select one *real* mapped model card → Windows FileDrop path exactly matches actual model. Drag from new HTML host into C4D; test ordinary drop and Shift merge separately on user's Windows C4D. Must not infer from DOM drag event.
4. **Storage seam**. Existing `catalog.json` and `covers.json` remain unchanged; virtual categories live in `ui-categories.v1.json` and are backed up on writes. Offline/disconnected drive gives clear error.
5. **Delivery seam**. Prove an installed v0.1.7 client can upgrade through its *existing button*, including obtaining any required WebView2/.NET host dependencies without dangerous manual file replacement. If impossible with PS1-only channel, design and test an explicit signed native upgrade before editing stable launcher. Do not ship a new update manifest until all Windows tests pass.

## Screenshot and performance gates
- Use `ui-ux-pro-max` plus `webapp-testing` for screenshot diffs against the original HTML at the same viewport/zoom/data; test compact 1016×689 and large 1920×1080.
- Test 100 and 1000 **different real or synthetic** entries, scroll latency, missing thumbnail, keyboard, dark theme.
- Source repo must contain **no user images, model data, textures or generated thumbnail cache**.

## Work progression
- Current commit: only installs development Skills and records the scoped test-driven execution plan.
- Next isolated feature branch: implement seam 1 (native update host contract + a red test) and then the smallest native adapter; tests must run on actual Windows before declaring success.
- There is **no release v0.1.8 from this installation**.
