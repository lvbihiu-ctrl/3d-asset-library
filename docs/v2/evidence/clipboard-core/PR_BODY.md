## User-visible outcome

The two previously disabled V2 buttons now have native implementations: clipboard cover paste (including Ctrl+V outside editable fields) and dedicated basic dHash image search. Existing local desktop entry has been switched to the tested self-contained V2 with the old stable shortcut kept as a fallback.

## Scope and dependencies

Targets `v2-dev`. Builds on the unmerged UI/read-only Alpha work in #34 and #36; includes that ancestry, not a replacement architecture. This incremental Alpha preserves the current path-derived IDs and uses an independent atomic local catalog/cover cache; SQLite/UUID migration is not being silently redesigned here.

No stable v0.1.7 script or update manifest changed. No private catalog, source model, texture or commercial cover image is included. No automatic rollback or full production OTA claim: unfinished updater work is preserved separately.

## Verification

- Actual Windows .NET 10 self-contained Release build; 45 native tests and 18 UI tests passed.
- Actual published EXE/WebView2 cover-button save, Ctrl+V handler, distinct image matching Top-1, invalid clipboard errors, source immutability, and separate-process restart persistence passed.
- Actual legacy import restored two records and covers; six legacy files and 36 authorized source files hash-identical before/after.
- Actual installed EXE launched from the replaced desktop shortcut; all 282 copied files SHA256-verified. Separate installed-EXE screenshot verification exited 0.
- Screenshot PNG headers validated at 1016x689 and 1920x1080; public images are synthetic only. See [Windows results](WINDOWS_RESULTS.md).
- One intermediate repeat timed out on the second cover shortcut. Subsequent fresh runs passed; exact original cause remains unproven. Added bounded STA retry and diagnostic state; final feature/restart runs both passed. Not concealing the intermittent test result.

## Still not delivered

C4D FileDrop/Shift (#15), editing real virtual categories, broad-format rescan parity and production OTA migration. This PR does not claim full stable feature parity or public production readiness. The software is locally available now; these remaining items must not be mistaken for working capabilities.
