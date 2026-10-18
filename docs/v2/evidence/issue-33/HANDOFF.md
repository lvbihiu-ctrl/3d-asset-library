# Issue #33 continuation handoff

Updated 2026-10-19, Asia/Shanghai.

## Worktree and branch

- `D:\Codex\3D-Asset-Library-V2-Issue33`
- `v2/33-alpha-real-library`, based on UI PR #34 branch `v2/32-ui-polish`.
- Core implementation commit: `93aaaaf`; follow-up fixes and evidence are in
  the commit containing this document. Use Git for the exact latest SHA.
- Target: Draft PR to `v2-dev`; do not merge PR #34 or #33 without the stated
  visual/manual acceptance. PR #34 is open, Draft, mergeable, all checks green
  at the 2026-10-19 inspection.

## Ready to try

Extract `.artifacts/issue-33-delivery/3D-Asset-Library-V2-Alpha-Issue33-win-x64.zip`
and start `AssetLibrary.V2.exe`. Select a folder, browse real model names and
same-name covers, search, select details, and use Locate. The startup screen
still clearly labels its sample catalog until a real directory is selected.
Re-select a folder after restart: this Alpha does not persist its real index.

Evidence: `docs/v2/evidence/issue-33/WINDOWS_RESULTS.md`. Final .NET tests 37/37,
UI tests 15/15, update-flow contract 7/7. On-disk 100/1000 fixture and authorized
directory runs passed with zero source hash changes. Actual cover decoding is
checked; separate cover origin fixes a WebView2 virtual mapping interception
bug caught by inspecting the real screenshot.

## Next gate

Desktop UI control was denied with `Computer Use was not approved to use v2`.
Manually verify native folder choice and Explorer selecting the correct file;
the automated locator records the native resolved ID without launching the
shell. Review the optimized UI visually. Until then keep the PR Draft and do
not mark all of Issue #33 accepted.

Do not reconnect the temporary local v0.1.7 update demo to the stable channel.
It only launches a local V2 build and is not a verified GitHub upgrade path.
The demo is outside the repository and must not be published as an updater.
V2 cover paste, image search, migration, and one-click updating are subsequent
work; no claim of full v0.1.7 feature parity.

## Preserve

- Do not modify main or released v0.1.7 scripts/manifests.
- Do not upload authorized models, textures, covers, indexes, or screenshots.
- Keep #15 C4D drag work and all other pre-existing worktrees unchanged.
- Core implementation is one model file per record for this issue; grouping,
  wider formats, persistent IDs, and classification belong to later work.
- Earlier model-availability notes dated 2026-10-18 are historical snapshots,
  not evidence of the model used after subsequent user model switches.
