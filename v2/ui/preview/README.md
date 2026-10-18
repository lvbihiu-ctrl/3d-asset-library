# V2 Synthetic HTML UI preview — Issue #18

**Browser-only visual and interaction reference.** Maintains the previously user-approved HTML/CSS layout, replaces all private commercial covers and labels with locally generated SVG illustrations. No real model files or user content are included.

- Responsive masonry 4-column concept with compact/large layout, light/dark theme, text filtering, synthetic categories + source folders, details, local-only multi-select.
- Preview uses existing `v2/ui/update/bridge.js` and doesn't let browser-only checks pretend a successful Windows update. C4D card is intentionally disabled without a trusted indexed model and native FileDrop.
- Real native actions are `{id,action,payload}`, request assetId, and require Windows host to authorize the asset and never trust HTML paths.
- `?synthetic=100` or `?synthetic=1000` generates deterministic synthetic stress cards.
- Seven synthetic browser smoke tests: `python -m unittest discover -s v2/ui/preview -v`, after installing Python Playwright and Chromium.
- Only `v2-dev` preview, **not** a functioning Windows EXE, C4D importer, persisted catalog, signed updater or finished native integration. Codex Issue #14 will embed trusted HTML in WebView2, #15 will test true FileDrop, and #19 will connect model data.
