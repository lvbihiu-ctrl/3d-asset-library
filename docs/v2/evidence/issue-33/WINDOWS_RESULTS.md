# Issue #33 Windows Alpha verification

Verified on Windows 11 x64 on 2026-10-19 (Asia/Shanghai).
Implementation starts at `93aaaaf` on `v2/33-alpha-real-library`, based on
`a65b8ca` from UI PR #34. This document accompanies the final corrective commit.

## Implemented behavior

The native folder picker selects one library root. C# recursively reads
`.c4d`, `.fbx`, and `.obj` files and associates same-directory, same-basename
PNG/JPG/JPEG/WebP/BMP images. The UI shows real filenames, relative folders,
format filters, text search, file details, and locate-by-native-asset-ID.
Missing covers have an explicit placeholder. Model and texture files are never
opened for writing. IDs are deterministic path hashes for this read-only
session, not persistent database IDs; choose a folder again after restarting.

Folder/model/error counts are reported during scanning. An import request
remains pending until native completion, avoiding the previous five-minute
page/native-session divergence. Cover images use an unmapped local HTTPS
origin: WebView2 static virtual-host mapping otherwise bypasses the dynamic
resource handler. Requests require the exact local origin and a 64-hex ID that
resolves in the current native session. No page-supplied file paths or commands
are accepted.

## Local checks

- SDK 10.0.402; Release, win-x64, .NET self-contained.
- .NET unit tests: 37 passed, 0 failed, including folder progress and cover
  origin/path validation.
- Edge/Playwright desktop and preview UI: 15 passed, 0 failed.
- Existing JavaScript update-flow contract: 7 passed, 0 failed. These tests do
  not mean native V2 updating is implemented.
- Clean ZIP extraction: published EXE launched and passed search, category,
  theme, layout, card selection, and scrolling checks in real WebView2.

| Real WebView2 integration input | Models | Files before/after | Changed/added | Exit | Wall time |
| --- | ---: | ---: | ---: | ---: | ---: |
| Distinct synthetic OBJ files, one same-name PNG | 100 | 101 / 101 | 0 / 0 | 0 | 17.29 s |
| Distinct synthetic OBJ files, one same-name PNG | 1000 | 1001 / 1001 | 0 / 0 | 0 | 5.47 s |
| User-authorized local directory | 2 C4D | 36 / 36 | 0 / 0 | 0 | 7.80 s |

All source files were compared by SHA256 before and after each integration run.
The authorized directory has no matching same-name covers; no private files,
names, paths, or screenshots from that directory are included in the artifact.
Synthetic records are different files, not valid rendered C4D/OBJ content.
Times include EXE startup and UI checks; the 100-item run additionally captures
three screenshots. They are not scanner-throughput benchmarks.

The integration harness clicks Import and Locate in real WebView2, verifies
the native/result counts, searches a real filename, and verifies matched cover
images have loaded with positive natural width. Test-mode Locate validates the
native catalog ID and file existence, then records the resolved ID instead of
opening Explorer in CI. Normal launches use the production Explorer locator.

## Actual screenshots

Screenshots were captured from the Windows EXE's WebView2 control after reading
the synthetic on-disk test library. They are not browser-preview mockups. PNG
IHDR dimensions were independently read and match the labels.

| Screenshot | Actual pixels | SHA256 |
| --- | --- | --- |
| `webview2-1016x689.png` | 1016 x 689 | `E3BAE0389E157883CF476F94D0CC1EEEDFA1FCB2DCC335530EBAFCC9F281C48C` |
| `webview2-1920x1080.png` | 1920 x 1080 | `A1893265400C3B80AFF81BF681745A59EF77651B6B42FB0D4843C7CF351B743D` |
| `webview2-dark-1016x689.png` | 1016 x 689 | `6E3A1DD44029C41CD5ED35E299513FBA198F634651B15E4293D51C62714BD66E` |

## Local delivery

- Directory: `.artifacts/issue-33-delivery/`
- ZIP: `3D-Asset-Library-V2-Alpha-Issue33-win-x64.zip`
- Size: 52,026,168 bytes.
- ZIP SHA256: `1D42D833A6B58EA7112A60FD7BAD799C058024EDEC20C0405B61EE30AA55FF63`
- Apphost EXE SHA256: `7BD034E25A9D1AC12C24F61203C62F77D44B582E75EBA691528E32A8BB04FE1B`
- Application DLL SHA256: `A2BC307F8C670F6A68B0F55E8F49FC9299D812D7F9EC7ADAF50E79378B423B8F`

The EXE is a .NET apphost. Its hash alone does not identify changes in the
application DLL or web files; verify/distribute the whole ZIP. Extract the
whole ZIP and double-click `AssetLibrary.V2.exe`. An installed WebView2
Evergreen Runtime is required; .NET SDK, Node, and Python are not required.
CI independently builds its own ZIP, screenshots, and test logs; CI artifact
hashes need not equal this local ZIP.

## Acceptance still outstanding

- Manual native folder-dialog interaction and actual Explorer selection of
  the original file: Windows app control returned `Computer Use was not
  approved to use v2`. Automated ID/message validation must not be reported
  as proof of Explorer selection. Keep the PR Draft pending that check and
  user visual acceptance.
- No physical disconnected-drive acceptance or slow/removable-disk benchmark
  was performed. File-existence guards are implemented, but are not evidence
  of that hardware scenario.
- SQLite, incremental/persistent indexing, virtual classifications, Ctrl+V
  cover editing, image-similarity search, native updating, legacy migration,
  and C4D FileDrop/Shift are not implemented in this slice. Issue #15 remains
  open. This is a read-only browsing Alpha, not a full production workflow.
- PR #34 remains a Draft dependency awaiting visual approval. Do not merge
  this stacked branch ahead of it without reviewing the resulting diff.

Stable `desktop-updates/native/app-v0.1.7.ps1` and
`desktop-updates/native/latest.json` are unchanged. Other worktrees and user
data remain outside this change.
