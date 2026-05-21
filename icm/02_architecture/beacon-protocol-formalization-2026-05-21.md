# Beacon protocol formalization audit (2026-05-21)

**Authored by:** ONR (V4 batch item #6)
**Requester:** Admiral (per `admiral-directive-2026-05-21T14-05Z` item #6)
**Authored at:** 2026-05-21T14-15Z
**Status:** research / proposal (ratification + actual `fleet-conventions.md` edit pending Admiral routing; ONR doesn't author the parent governance file per ADR-authoring scope precedent set in `admiral-ruling-2026-05-21T14-10Z`)

---

## Scope

Audit inbox beacon prefix usage; identify new types since fleet-conventions §"Beacon naming" was last updated (post-2026-05-17 restructure); propose amendments to the beacon-naming table.

Per V4 directive item #6 — coordinate with QM V3 #8 addendum (beacon-naming validator allowlist).

---

## Inventory — all unique beacon prefixes (inbox scan 2026-05-21T14:15Z)

```bash
ls coordination/inbox/ | sed 's/-[0-9]\{4\}-.*//' | sort -u
```

Returns 34 unique prefixes:

### Admiral (8 distinct)

| Prefix | In current fleet-conventions table? | First observed | Purpose |
|---|---|---|---|
| `admiral-attest-*` | NO — NEW | 2026-05-20 (cohort-2 PR 0d dual-GREEN attest) | Admiral self-attest signal that a council requirement / amendment is satisfied without re-dispatching council |
| `admiral-broadcast-*` | NO — NEW | 2026-05-17 | Fleet-wide announcements (heartbeat protocol launch; round-N queue filed; restructure events) |
| `admiral-directive-*` | YES | 2026-05-17 | Direct work assignments to a specific role (canonical type) |
| `admiral-directive-amendment-*` | NO — NEW (derived) | 2026-05-18 | Mid-flight amendments to a previously-issued directive |
| `admiral-question-*` | NO — NEW (rare) | (sparse) | Admiral asks CIC; mirror of `<role>-question-*` pattern at the Admiral layer |
| `admiral-revival-context-*` | NO — NEW | 2026-05-21 (engineer bridge endpoint pair) | Context dump for a session that lost continuity (recovery primer) |
| `admiral-revival-context-amendment-*` | NO — NEW (derived) | 2026-05-21 | Revisions to a revival-context beacon |
| `admiral-ruling-*` | YES | 2026-05-17 | Decisions / dispositions on agent questions or council outputs |
| `admiral-ruling-amendment-*` | NO — NEW (derived) | (sparse) | Revisions to a prior ruling |
| `admiral-status-*` | YES | 2026-05-17 | Admiral's own work-state updates |
| `admiral-tracking-*` | NO — NEW | 2026-05-21 (cross-tenant audit emission retrofit) | Cross-cutting forward-watched work item; not yet a directive (Engineer picks up when bandwidth allows) |

### Other roles (per current fleet-conventions; all canonical)

| Role | Prefixes (observed) | New since fleet-conventions? |
|---|---|---|
| Engineer | `engineer-question`, `engineer-status` | `engineer-idle` + `engineer-council-request` are documented but NOT observed in current inbox (may be archived) |
| FED | `fed-idle`, `fed-question`, `fed-status` | none |
| ONR | `onr-question`, `onr-status` | `onr-idle` documented in agent file but NOT observed (would-be value-add; flagged below) |
| PAO | `pao-question`, `pao-status` | `pao-directive` documented for PAO→Yeoman; NOT observed in inbox (likely uses different storage OR rare) |
| Yeoman | `yeoman-question`, `yeoman-status` | none |
| QM | `qm-status`, `qm-question`, `qm-resumed`, `qm-daemon-status` | `qm-daemon-status` NEW (automated daemon hourly findings); `qm-resumed` NEW or rare (session-revival signal); `qm-idle` documented but not observed |
| po-mac | `po-mac-idle`, `po-mac-question`, `po-mac-status` | none |
| po-win | `po-win-idle`, `po-win-question`, `po-win-status` | none |
| Council | `council-verdict-*` | YES — canonical SPOT-CHECK output |

---

## Gaps + recommendations

### Admiral side — 7 new types to document

Proposed amendment to fleet-conventions §"Beacon naming" Admiral row:

```
| Admiral | `admiral-directive-*`, `admiral-directive-amendment-*`, `admiral-status-*`, `admiral-ruling-*`, `admiral-ruling-amendment-*`, `admiral-broadcast-*`, `admiral-attest-*`, `admiral-tracking-*`, `admiral-revival-context-*`, `admiral-revival-context-amendment-*`, `admiral-question-*` (rare; mirror of `<role>-question-*` for Admiral→CIC) |
```

### Per-type definitions (proposed for fleet-conventions §"Beacon naming" inline OR a new §"Admiral beacon type semantics"):

#### `admiral-attest-*`
**Purpose:** Admiral self-attests that a council requirement / amendment is satisfied without re-dispatching the council. Used after an amendment lands on a council-flagged PR; Admiral verifies + closes the loop.
**When to use:** instead of a new `council-verdict-*` (which requires dispatch); cheaper signal.
**Frontmatter shape:** `re: <prior-council-verdict-beacon>`; `verdict: GREEN-via-admiral-self-attest`.

#### `admiral-broadcast-*`
**Purpose:** Fleet-wide announcement; no single addressee.
**When to use:** protocol launches; structural changes; heartbeat ping waves.
**Frontmatter shape:** `to: fleet` (or `to: all-agents`).

#### `admiral-tracking-*`
**Purpose:** Cross-cutting forward-watched work item that has NOT been formally dispatched as a directive. Engineer (or other role) can pick up when bandwidth allows.
**When to use:** when a council verdict surfaces a follow-on retrofit / hardening item that doesn't block the current PR but should ship eventually.
**Frontmatter shape:** `priority: medium (fleet-wide hardening; not blocking)` + `scope: <Bridge handler families>` + `target: <ADR/protocol reference>`.

#### `admiral-revival-context-*` + `admiral-revival-context-amendment-*`
**Purpose:** Context dump for a session that lost continuity (timeout / API hiccup / crash). Provides a fresh session with enough context to resume work without re-discovering state.
**When to use:** after Admiral observes a session-loss in another agent; before that agent's next session-start.
**Frontmatter shape:** `to: <agent>` + `priority: high (session-revival)` + body has full context + open work items.

#### `admiral-question-*`
**Purpose:** Rare; Admiral asks CIC directly. Mirror of `<role>-question-*` for Admiral→CIC.
**When to use:** when Admiral needs CIC ratification on a decision that exceeds Admiral's authority.
**Frontmatter shape:** `to: cic` + `priority: <varies>`.

#### `admiral-ruling-amendment-*`
**Purpose:** Revision to a prior `admiral-ruling-*` when new context arrives.
**When to use:** rare; amendment shape preserves audit trail (vs editing the original ruling).

### QM side — 2 new types to document

#### `qm-daemon-status-*`
**Purpose:** Automated QM daemon hourly findings beacon. NOT a manual QM status; emits from the QM daemon (`coordination/qm-daemon.py`).
**When to use:** automated; per-cycle.
**Frontmatter shape:** `type: status` + `from: qm-daemon`.

#### `qm-resumed-*`
**Purpose:** QM session-revival signal (analogous to other roles' `<role>-resumed-*`).
**When to use:** after QM session restart.

### ONR side — proposed addition (not yet observed)

#### `onr-idle-*` (proposed; not yet used)
**Purpose:** ONR queue-cleared signal triggering next-batch dispatch from Admiral.
**Current pattern:** ONR uses `onr-status-*-research-queue-vN-cleared-fed-idle.md` as a status (filename contains "fed-idle" but it's the wrong agent token — should be `onr-idle`).
**Recommendation:** introduce `onr-idle-*` for explicit idle signaling; alternatively keep the current pattern with `-onr-idle.md` suffix instead of `-fed-idle.md`.

---

## Coordination with QM V3 #8 addendum

V4 directive references QM V3 #8 addendum as a "beacon-naming validator allowlist." ONR has NOT seen V3 #8 addendum directly but the implication is QM is adding regex / allowlist enforcement that requires the prefix list to be canonical.

**Recommendation:** Admiral coordinates with QM to ensure:
1. The amendments proposed here land in fleet-conventions.md (Admiral's authoring)
2. QM's allowlist mirrors the updated table
3. New prefixes added in future require fleet-conventions amendment BEFORE allowlist update (governance precedence)

---

## Cross-reference to existing infrastructure

### Heartbeat protocol (already documented)

Heartbeats live at `coordination/heartbeats/<agent>.md` per-cycle overwrite per the agent-definition heartbeat protocol. Distinct from inbox beacons; not subject to this audit.

### Monitor protocol

Monitor events are NOT beacon files; they're filesystem events feeding into the agent's session. Outside this audit.

### Per-ship CLAUDE.md beacon documentation

Each ship repo's CLAUDE.md may have ship-specific beacon protocols (e.g., book repo has `.pao-inbox/`). This audit does not extend to per-ship inboxes; fleet-conventions §"Beacon naming" is fleet-level only.

---

## Proposed amendment PR (for Admiral to apply)

**File:** `/Users/christopherwood/Projects/Harborline-Software/.claude/rules/fleet-conventions.md`

**Section:** §"Beacon naming (post-2026-05-17 restructure)"

**Amendment shape:**

1. Replace the Admiral row in the table with the 11-prefix list (above)
2. Replace the QM row with the 4-prefix list (`qm-status`, `qm-question`, `qm-idle`, `qm-resumed`, `qm-daemon-status`)
3. Add an `onr-idle-*` entry to the ONR row (or keep status with `-onr-idle.md` suffix convention)
4. Add a new §"Admiral beacon type semantics" subsection with the per-type definitions above (~70 lines)
5. Optional: §"Beacon-naming validator coordination" subsection coordinating with QM V3 #8 addendum

**Total amendment scope:** ~80-100 lines added to fleet-conventions.md.

---

## ONR's read

ONR's research output here (this document) is the SCAFFOLD per the ADR-authoring precedent (Admiral takes the parent-governance-file edit; ONR provides the research). Admiral applies the amendment to `fleet-conventions.md` directly.

If Admiral prefers ONR to file an amendment PR DIRECTLY against the parent `.claude/rules/fleet-conventions.md` — file a follow-on directive specifying. ONR can author the actual edit (it's text/structure, not policy) if Admiral confirms scope.

---

## Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T14-05Z-onr-v4-batch-adrs-cohorts-and-cleanup.md` item #6
2. `coordination/inbox/admiral-ruling-2026-05-21T14-10Z-onr-v4-adr-authoring-option-c.md` — scope precedent for governance-file authoring
3. Inbox scan `ls coordination/inbox/ | sed 's/-[0-9]\{4\}-.*//' | sort -u` 2026-05-21T14:15Z — 34 unique prefixes
4. `/Users/christopherwood/Projects/Harborline-Software/.claude/rules/fleet-conventions.md` §"Beacon naming" lines 142-158 (current state)
5. `coordination/inbox/admiral-tracking-2026-05-21T08-00Z-cross-tenant-audit-emission-bridge-handler-retrofit.md` — example of `admiral-tracking-*` usage
6. `coordination/inbox/admiral-revival-context-2026-05-21T02-35Z-engineer-bridge-endpoint-pair.md` — example of `admiral-revival-context-*` usage
7. `coordination/inbox/admiral-attest-*` examples (cohort-2 PR 0d dual-GREEN)

---

## What ONR does next

V4 #6 deliverable complete (this scaffold). Files `onr-status-*-v4-item-6-beacon-protocol-research-complete.md` referencing this doc + the proposed amendment scope. Proceeds to V4 #4 (Pattern catalog deep-cleanup audit) per Admiral resequencing.

— ONR, 2026-05-21T14:15Z
