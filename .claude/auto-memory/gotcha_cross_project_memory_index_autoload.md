---
name: gotcha-cross-project-memory-index-autoload
description: Passively auto-loaded MEMORY.md can be a baseline-sibling project's (harness memory-dir config) — verify recalled repo-specific facts first-party.
metadata:
  type: feedback
---

The passively auto-loaded `MEMORY.md` is Claude Code's native memory feature (a harness/user-level memory-directory config), NOT the project SessionStart hook — `session_context_loader.py` uses git-toplevel and never loads MEMORY.md. That config can point at a DIFFERENT baseline-sharing project, so a recalled fact naming a repo-specific artifact (autoload, file, flag, class) may be the sibling's and FALSE here.

**How to apply:** before a recalled repo-specific fact shapes a design, verify it first-party in THIS repo (Glob/Grep/Read). Active `semantic-search(searchDir=<this repo>)` is trustworthy — the passive index is the contamination vector.

**Verified:** this DraconicWars session passively loaded PushinPotions' `MEMORY.md`; its "Global autoload exists" fact (`gotcha_pp_autoload_dual_registration`, a PP file absent from DW's store) nearly hosted a `ThemeService` on a phantom autoload — DW has ZERO autoloads (`project.godot` has no `[autoload]`; cross-scene state is all `public static class`). Caught by a red-team agent's first-party `project.godot` read.

Related: [[precedent_is_evidence_not_authority]], [[feedback_verify_explore_agent_empirical_claims]].
