# Commit-message test fixtures — fleet pre-flight hook

Canonical pass/fail examples for `.husky/commit-msg-fleet-preflight.sh`.

The hook runs after stock commitlint succeeds, catching the **top-5 fleet-specific error classes** that the `@commitlint/config-conventional` config cannot diagnose well — or whose error messages confused agents enough to require 3+ re-cycles on signal-bridge#24, flight-deck#29, etc.

## How to use

Each section below has:

- **PASS examples** — commit messages that should produce no findings
- **FAIL examples** — commit messages that should trigger that rule
- **Edge cases** — messages where the rule could plausibly false-positive

Hand-run any fixture against the local hook:

```sh
echo 'commit message here' > /tmp/test.msg
.husky/commit-msg-fleet-preflight.sh /tmp/test.msg
echo "exit=$?"
```

Three modes via env var:

- `FLEET_PREFLIGHT_MODE=warn` (default) — warnings to stderr, always exit 0
- `FLEET_PREFLIGHT_MODE=block` — warnings to stderr, exit 1 if anything fires
- `FLEET_PREFLIGHT_MODE=off` — no-op

## Rule reference

| ID | Name | Rationale (cerebrum / buglog source) |
|---|---|---|
| R1 | W#NN workstream-shorthand in body | Engineer 3-cycle trap on signal-bridge#24 + flight-deck#29 (cerebrum 2026-05-19) |
| R2 | Bare `<Word>:` at body line start | Footer parser greedy on bare-Word: tokens (cerebrum 2026-05-19) |
| R3 | `<word>#<digit>` inline (cross-repo PR refs) | commitlint footer-leading-blank trap (cerebrum 2026-05-18) |
| R4 | Body line >100 chars | Fleet target stricter than wagoid 120 (PRs #277, #286, #298 hit) |
| R5 | `new <Entity> {` fixture init | Cross-cluster `required` property cascade (bug-191) |

## R1 — W#NN workstream-shorthand

### PASS example R1-P1: workstream cited without `#`

```
feat(blocks-financial-ar): tenant-key IInvoiceRepository contract

Per ADR 0092 Step 1 the blocks-financial-ar invoice repository contract
gains a tenant-scoping marker. Implementation routes through the EF
Core query interceptor; no API change at the call site.

This advances workstream 60 substrate without touching W23.3 surface.
```

(`workstream 60` and `W23.3` are both safe — only `W#NN` is the trap.)

### FAIL example R1-F1: `W#74` in body

```
feat(field): payload extension

This implements W#74 cohort-2 alongside W#60 substrate.
```

Expected hook output:

```
[fleet-preflight] warn [R1-w-shorthand] body line 1 contains 'W#NN' workstream-shorthand: 'This implements W#74 cohort-2 alongside W#60 substrate.'
  -> R1 fix: use 'W60' / 'W23.3' / 'workstream 60' (no '#') in commit bodies
```

### Edge case R1-E1: `W#` in a code block (subject to in-code exemption — currently NOT exempted)

The rule flags `W#NN` regardless of whether it sits inside a ``` fenced block. This is intentional: commitlint's footer parser does not honor markdown fencing either, so the bug reproduces inside code blocks.

## R2 — Bare `<Word>:` at body line start

### PASS example R2-P1: prose phrasing

```
feat(web): WCAG 2.2 AA audit

For accessibility we addressed scope= attribute on table headers
and ensured ARIA live regions on async loading states.

The PR adds nine unit tests covering the relevant section.
```

### PASS example R2-P2: canonical-trailer footer

```
fix(web): rebind PaymentHistoryComponent

Refs: shipyard#42
Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

(`Refs:` and `Co-Authored-By:` are canonical trailers; the hook whitelists them.)

### FAIL example R2-F1: bare `Accessibility:` + `Note:` at line start

```
feat(web): accessibility audit

Accessibility: addressed scope= attribute on table headers.
Note: this defers ARIA live regions to Phase 2.
9 unit tests cover the WCAG 2.2 AA section.
```

Expected:

```
[fleet-preflight] warn [R2-bare-word-trailer] body line 1 starts with 'Accessibility:' — wagoid parses this as a footer token
[fleet-preflight] warn [R2-bare-word-trailer] body line 2 starts with 'Note:' — wagoid parses this as a footer token
```

(Line 3 starts with `9` not a `Word:` token, so it doesn't fire R2.)

### Edge case R2-E1: file-path-like line start

A line like `src/foo.rs:` would also fire R2 because the token matches `^[A-Z][a-z]+:`. Mitigation: convert to a markdown code reference (`` `src/foo.rs` ``) which doesn't start with an uppercase letter on its own line. Currently accepted false-positive risk in warn-only canary.

## R3 — `<word>#<digit>` inline references

### PASS example R3-P1: prose phrasing

```
fix(web): align with sibling shipyard substrate

The substrate landed in the sibling shipyard PR last week. This
commit consumes the new interface.
```

### PASS example R3-P2: `Refs:` trailer

```
feat(payments): align with shipyard substrate

This commit consumes the new IPaymentRepository contract from the
sibling shipyard PR.

Refs: shipyard#42
```

(Body has no `<word>#<digit>`; the trailing `Refs:` line is recognized as a canonical-trailer and excluded.)

### PASS example R3-P3: GitHub URL

```
fix(web): see GitHub issue

See https://github.com/Harborline-Software/shipyard/issues/42 for context.
```

(URLs containing `://` are exempt.)

### FAIL example R3-F1: inline `shipyard#42` and `coordination#6`

```
feat(payments): align with shipyard#42 substrate

The substrate landed in shipyard#42 last week. This commit consumes the
new interface from coordination#6.
```

Expected:

```
[fleet-preflight] warn [R3-repo-hash] body line 1 contains '<word>#<digit>' ref ('The substrate landed in shipyard#42 last week. ...')
[fleet-preflight] warn [R3-repo-hash] body line 2 contains '<word>#<digit>' ref ('new interface from coordination#6.')
```

### Edge case R3-E1: `W#NN` only

A line containing ONLY `W#NN` style refs (no `shipyard#42` etc.) is reported by R1 but NOT R3 — the two rules are explicitly de-duplicated so an author isn't told the same thing twice. If the line contains BOTH `W#74` and `shipyard#42`, both rules fire.

## R4 — Body line >100 chars

### PASS example R4-P1: hard-wrapped at ~85

```
fix(financial-ar): rebind PaymentHistoryComponent

The PaymentHistoryComponent now resolves IInvoiceRepository directly
rather than going through the legacy cross-cluster aggregator. This
removes a 12-row N+1 in the typical small-portfolio render.
```

### FAIL example R4-F1: 210-char paragraph line

```
feat(web): add comprehensive payment posting

This commit introduces the full DefaultPaymentPostingService implementation which routes through the cross-cluster event bus to keep AR and AP ledgers strictly in lockstep without any cross-cluster query joins.
```

Expected:

```
[fleet-preflight] warn [R4-body-long] body line 1 is 210 chars (>100): 'This commit introduces the full DefaultPaymentPostingService...'
```

### Edge case R4-E1: fenced code block

Lines inside a markdown ` ```sh ` ... ` ``` ` block are exempt — they're frequently long shell commands or test fixtures. The hook tracks the in-code state across lines via awk.

## R5 — `new <Entity> {` cross-cluster fixture init

### PASS example R5-P1: prose description, no entity-init token

```
test(financial-ar): add InvoicesEndpointTests

The new test suite seeds via CreateInvoiceAsync factory method to
preserve TenantId default across non-Draft invoices.
```

### FAIL example R5-F1: `new Invoice {` in body

```
test(financial-ar): add InvoicesEndpointTests

This adds new Invoice { Number = "INV-001", TenantId = TenantId.New() }
fixtures across the lifecycle states.
```

Expected (info-level, not warn):

```
[fleet-preflight] info [R5-fixture-init] body line 1 mentions 'This adds new Invoice { Number = "INV-001", TenantId = TenantId.New() }'; cross-cluster fixture-breakage risk if the entity recently gained a 'required' property (see bug-191)
```

### Edge case R5-E1: not actually adding a fixture

The rule heuristically flags ANY `new <Entity> {` mention in the body, including narrative descriptions of changes (where the author cites the fixture pattern but isn't adding any). This is accepted false-positive risk in warn-only canary; the rule is permanently `info` severity, not `warn`, so it never blocks even in block-mode.

## Canary metrics — what to measure (Phase 1 gate)

After the canary period the gate review checks:

1. Total commits in canary window (`git log --since=YYYY-MM-DD --oneline | wc -l`)
2. Commits where the hook fired (grep for `\[fleet-preflight\]` in stderr captures, OR a Phase 1.5 telemetry hook that writes a `.fleet-preflight-history` file per repo)
3. False-positive rate (commits where the hook fired but the message was actually fine)
4. CI failure rate for the prevented categories (commitlint footer trap occurrences in `gh pr list --state open --json statusCheckRollup` for the shipyard repo)

**Promotion gate to block-mode:** if 1 week of canary shows ≥3 catches AND ≤1 false positive AND no developer complaints, promote to `FLEET_PREFLIGHT_MODE=block` default for shipyard. Then roll the hook to sunfish + signal-bridge.

## Bypass mechanisms

Authors who hit a false positive can:

1. **Recommended:** rephrase per the hook's `-> RN fix:` guidance
2. `FLEET_PREFLIGHT_MODE=warn git commit ...` — advisory only, even after block-mode promotion
3. `git commit --no-verify` — universal husky bypass; skips ALL hooks (commitlint + preflight + pre-commit)
4. `FLEET_PREFLIGHT_MODE=off git commit ...` — disables fleet preflight only; commitlint still runs

## Rollback

To disable the fleet preflight without removing the script:

```sh
echo "FLEET_PREFLIGHT_MODE=off" >> shipyard/.env  # or per-shell export
```

To remove entirely:

```sh
rm shipyard/.husky/commit-msg-fleet-preflight.sh
# also revert the dispatch block in .husky/commit-msg
```

Existing husky + commitlint chain continues to work without the preflight.

## Reference

- UPF audit: `coordination/inbox/admiral-status-2026-05-20T12-45Z-upf-pr-error-monitor-audit.md`
- Cerebrum entries: 2026-05-19 W#NN trap; 2026-05-19 bare-`Word:` enumeration; 2026-05-18 commitlint footer-parser traps
- Bug references: bug-191 (cross-cluster fixture cascade)
- Companion detection layer: `coordination/qm-daemon.py check_ci_failures()`
