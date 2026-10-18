# V2 SQLite migration reference — GitHub Issue #17

**Synthetic, test-driven reference; not yet wired to the Windows .NET host or customer library.** No customer files are included.

Legacy v0.1.7: catalog.json {summary,assets:[{id,name,kind,ext,rel,full,preview,bytes,changed,resources}]}; covers.json {id:filename}; ui-categories.v1.json {schema:1,categories,assignments}; covers/ original user cover copies. Old asset ID uses SHA256 of lowercased absolute Windows path.

The reference importer:
- default dry-run validates legacy JSON without writing;
- imports into a newly created isolated SQLite destination with copied cover files, byte-preserved source JSON snapshot and hashes;
- maps every legacy ID to one new persistent UUID without opening or moving source models;
- rejects duplicate/orphan/invalid IDs, missing covers/categories and path traversal rather than silently dropping data;
- does not overwrite existing destinations and leaves Beta/Stable separate;
- creates SHA256-verified SQLite/cover recovery snapshots retaining three checkpoints (native restore UI not yet implemented).

Run: `python -m unittest discover -s v2/data/migration -v` using only Python standard library.

Codex must implement the matching C# native storage, user-confirmed drive relink, safe DB connection migration/rollback and actual restore UX on Windows before real customer data can be processed. **Do not mistake the reference script for a released migration tool.**
