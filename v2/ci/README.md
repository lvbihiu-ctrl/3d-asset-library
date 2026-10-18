# Public V2 PR path guard (Issue #27)

Check changed filenames in V2 pull requests. Reject released app and updater
files under desktop-updates/native, private data directories, common customer
model/render formats, cover raster images, SQLite databases, installers,
archives and code signing keys. Synthetic SVG and source files are allowed.

Run: python -m unittest discover -s v2/ci -p 'test_*.py' -v

Limits: only filenames, not file contents. It cannot detect secrets inside
code or real user files embedded in SVG. Human review, secret scanning and
separate local-only test fixtures remain essential. Without a required
status-check branch rule, failed CI is advisory rather than merge-blocking.
This workflow never touches the published v0.1.7 update mechanism.
