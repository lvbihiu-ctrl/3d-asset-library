# Engineering Skills Installation — 3D Asset Library

Upstream: https://github.com/mattpocock/skills
Pinned revision: `49dd158d1076134a641b33efb035946536778336`
License: MIT, copied at `docs/licenses/mattpocock-skills.LICENSE` with copyright notice.

## Installed project-local Skills
- `setup-matt-pocock-skills` — setup and all issue tracker/domain/label templates
- `tdd` — SKILL, tests/mocking docs, agent metadata
- `diagnosing-bugs` — SKILL, agent metadata, HITL script
- `prototype` — SKILL, UI/logic references, agent metadata
- `codebase-design` — SKILL, both detailed design references, agent metadata
- `code-review` — SKILL and agent metadata
- `to-spec` — SKILL and agent metadata
- `implement` — SKILL and agent metadata

All 28 upstream text files in these eight Skill folders are vendored at `.agents/skills/<name>/` unchanged. These are the skills relevant to UI integration, native update testing and safe release; the *rest of the Matt Pocock repo is intentionally not installed*. Existing `frontend-design`, `ui-ux-pro-max` and `webapp-testing` remain intact.

**Project-local installation only.** Pull the project on Windows to make these Skill folders accessible to Codex in that project. It is not a global plugin installation and does not modify the existing Windows EXE. The upstream README also supports a Codex plugin mechanism, but no global Codex plugin was installed from this GitHub connection.

`setup-matt-pocock-skills` is an interactive setup, not auto-run in this change. Its proposal to edit `AGENTS.md` or create GitHub labels requires project review; existing repo rules and glossary are authoritative.

## How we will use these
1. `prototype`: original user HTML/CSS is visual source-of-truth, but no customer-cover photos are pushed to this public repo.
2. `tdd`: red→green tests at externally observable seams (native update bridge, verified updater, genuine C4D drag).
3. `diagnosing-bugs`: reproducible failing checks before altering update behavior; differentiate CDN cache, version check, package verification and restart failures.
4. `codebase-design` and `to-spec`: isolate HTML views, Windows adapter, storage and release channels.
5. `code-review`: compare diff to explicit acceptance criteria before release.
6. `webapp-testing` (preexisting): test rendered HTML; Windows/C4D acceptance must be performed separately.

No production code, updater manifests, launchers or user data are changed by Skill installation.
