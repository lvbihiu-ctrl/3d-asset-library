# V2 Windows desktop interface integration · Issue #32

The executable uses the approved V2 **light-first masonry** layout and keeps
the existing navigation, search, layout switcher, card selection, theme
switcher, and details panel. Issue #32 tightens the typography, spacing,
contrast, focus states, scrollbars, card proportions, and compact-window
layout without introducing a different visual direction.

The canonical interaction implementation lives in `v2/ui/preview/app.js`.
The desktop HTML owns the Windows-oriented visual system and capability-gates
native-only actions. The browser update state controller is taken from
`v2/ui/update/bridge.js`. These files are copied to `Web/index.html`,
`Web/app.js`, and `Web/bridge.js` during publish; no CDN is used.

This is **visual delivery, not a working real model database**. Cards are
synthetic generated SVGs and are explicitly labelled as not connected to the
real asset library. Unsupported native actions are disabled instead of
showing false success feedback. The current native host only supports
`getHostInfo`.

Automated coverage includes the browser UI contract at 1016×689 and
1920×1080, plus a real WebView2 interaction smoke test for search, category,
theme, layout, card selection, and all three scrolling regions. Capture also
produces exact-size light and dark screenshots from the real executable.

## Required evidence

Windows CI pipeline `V2 Windows WebView2 host` must pass for this PR and
upload a self-contained `win-x64` ZIP, before/after screenshots (including
one dark-mode capture), UI test log,
and .NET test report. The real WebView2 screenshots must be exactly 1016×689
and 1920×1080 and show the V2 sidebar, search, masonry grid, and details panel.
Do not claim that C4D FileDrop or SQLite is completed.
Synthetic SVGs only; **do not** commit the customer's screenshot images.
Existing v0.1.7 and main are untouched.

For actual model import and C4D drag: see #15 and #19.
