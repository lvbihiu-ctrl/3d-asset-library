# ChatGPT ↔ Codex · Existing Repository Collaboration

Repository: https://github.com/lvbihiu-ctrl/3d-asset-library
**Keep using this repository. Do not require a new repository.**
Current repository visibility: **public**. Work on v2-dev will be publicly visible. Never commit client renders, commercial models, textures, paths containing secrets, private data, or user-supplied demonstration images.

## Roles
- **ChatGPT / product architect**: convert confirmed V2 decisions into specifications and acceptance criteria; publish GitHub Issues; review diffs, PRs, CI reports and Windows evidence. Do not falsely claim Windows/C4D tests.
- **Codex / Windows implementer**: pick up one unblocked Issue at a time, create a feature branch based on v2-dev, use TDD, implement native WebView2/C#/SQLite/Velopack work, execute Windows tests, attach sanitized evidence, and open a draft PR targeting v2-dev.
- **User / release approver**: confirm UX and C4D native drag (including Shift merge), migration results and Stable release approval.

## Branches
- `main`: remains the existing v0.1.7 baseline with no automatic release of V2 code; do not edit its legacy update manifest for previews.
- `v2-dev`: integration branch for all V2 work, separate from released client.
- `v2/<issue-number>-<slug>`: one small task branch per GitHub Issue, PR to `v2-dev`.
- **No blind auto-merge into main**. Official Stable requires explicit user approval and test evidence.
- V2 source in this *public* repo is publicly readable. Keeping the source legally unlicensed for redistribution does not make it confidential.

## Task workflow
1. ChatGPT: read V2 specs + GLOSSARY and use `to-tickets` to draft vertical-slice Issues. Every Issue includes external acceptance criteria, blockers and safety boundaries.
2. Codex: select a ready Issue, apply `tdd` then `implement`, include exact commands/results and a Windows test log; no copy-pasted private data.
3. PR review: ChatGPT uses `code-review` for requirements and code quality. Missing tests return to Codex with actionable feedback.
4. Integrate passing PRs to `v2-dev`. Do not claim a UI mock proves native drag, updater or data migration.
5. Stable promotion only after real Windows 11 installation, C4D drag+Shift, SQLite backup/migration, multiple actual OTA upgrades and recovery verification, signing and user approval.
6. Use `handoff` to summarize a task for a fresh agent. Link Issue/PR/spec paths rather than paste entire chats.

## First recommended V2 Issues
1. Native WebView2 FileDrop spike, ordinary C4D drag and Shift merge, synthetic file first (no source writes).
2. Windows installer + Velopack channel proof, upgrade twice and failure rollback in a throwaway test install.
3. Read-only legacy JSON migration adapter → isolated SQLite test DB, mapping verification and 3 recovery points.
4. Real HTML UI + model/catalog bridge (no fake demo-to-model links), viewport and interactions.
5. Legacy updater migration handoff under a separate, signed/tested migration path.

## Safety
- Released v0.1.7, its PS1 script and `desktop-updates/native/latest.json` must not be modified until approved acceptance.
- Original models/textures are read-only; installation and updater never traverse/upload user data.
- Preserve full existing `.local-data` before any local migration, never run destructive fixes without explicit approval.
- Use `docs/agents/issue-tracker.md` as issue tracker configuration; existing AGENTS.md remains authoritative.
