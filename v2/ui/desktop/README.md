# V2 Windows desktop interface integration · Issue #30

The executable now uses the approved V2 **light-first masonry** layout rather
than the old V14 HTML prototype. The canonical layout implementation lives in
`v2/ui/preview/app.js`; the desktop-specific HTML document has window-sizing
CSS only. The browser update state controller is taken from
`v2/ui/update/bridge.js`. Paths are copied to `Web/index.html`,
`Web/app.js`, and `Web/bridge.js` during dotnet publish; no CDN.

This is **visual delivery, not a working real model database**. Cards are
synthetic generated SVGs and say they are not mapped to real models. Buttons
for unsupported native operations reject requests; native host only supports
`getHostInfo`. Windows smoke testing checks actual rendered V2 cards, not
just a successful WebView2 handshake.

## Required evidence

Windows CI pipeline `V2 Windows WebView2 host` must pass for this PR and
upload a self-contained `win-x64` ZIP and real WebView2 screenshots at
1016×689 and 1920×1080. Screenshot must show the V2 sidebar + search +
masonry grid. Do not claim that C4D FileDrop or SQLite is completed.
Synthetic SVGs only; **do not** commit the customer's screenshot images.
Existing v0.1.7 and main are untouched.

For actual model import and C4D drag: see #15 and #19.
