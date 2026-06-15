---
name: gotcha-cwd-executor-cannot-reach-vault-doc
description: A cwd-resolving executor (e.g. /update_roadmap) can't operate on a doc outside the repo (Obsidian vault); hand-edit with full recompute.
metadata:
  type: project
---

A slash-command executor that resolves its target file from the working dir / nearest ancestor (e.g. `/update_roadmap` searching for `roadmap.md`) cannot operate on a doc that lives OUTSIDE the repo. DW's `roadmap.md` is in the Obsidian vault, so `/update_roadmap` run from the repo cwd never finds it.

**Why:** the executor searches cwd/ancestors; the vault is a sibling tree, not an ancestor of the repo.

**How to apply:** hand-edit the vault doc, replicating the executor's multi-section recompute. For the roadmap that's Parts-table State + Mermaid node class + derived views (ready / blocked, incl. downstream unblocks) + Revision Log — all kept consistent in one pass.

**Concrete:** 2026-06-15 CBPT completion — marked complete by hand-editing the vault roadmap across all 5 surfaces (/update_roadmap unreachable from the repo).
