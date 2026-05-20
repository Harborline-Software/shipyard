#!/bin/sh
# Fleet pre-flight commit-msg checker — catches the top-5 fleet-specific
# error classes that the stock commitlint config cannot diagnose well
# (or whose error message confused agents enough to require 3+ re-cycles
# on signal-bridge#24, flight-deck#29, etc.).
#
# Background:
#   UPF audit `admiral-status-2026-05-20T12-45Z-upf-pr-error-monitor-audit.md`
#   recommended LAYER-1 prevention over LAYER-4 detection for the
#   recurring fleet-friction PR-CI cycle. This is the prevention layer.
#
# Invocation:
#   Called from `.husky/commit-msg` after stock commitlint succeeds.
#   Receives the same `$1` (path to COMMIT_EDITMSG).
#
# Modes (via FLEET_PREFLIGHT_MODE env var, defaults to `warn`):
#   off    — no-op; skip preflight entirely
#   warn   — print warnings to stderr, ALWAYS exit 0 (commit goes through)
#   block  — print errors to stderr, exit 1 if any rule fires (rejects commit)
#
# Bypass (universal husky):
#   `git commit --no-verify` skips ALL hooks, this one included.
#
# Test fixtures + canonical pass/fail examples:
#   `_shared/engineering/commit-message-test-fixtures.md`

[ "${HUSKY:-1}" = "0" ] && exit 0

# Read mode early so explicit `off` short-circuits before any work.
FLEET_PREFLIGHT_MODE=${FLEET_PREFLIGHT_MODE:-warn}
if [ "$FLEET_PREFLIGHT_MODE" = "off" ]; then
  exit 0
fi
if [ "$FLEET_PREFLIGHT_MODE" != "warn" ] && [ "$FLEET_PREFLIGHT_MODE" != "block" ]; then
  printf '[fleet-preflight] WARN: unknown FLEET_PREFLIGHT_MODE=%s; defaulting to warn\n' \
    "$FLEET_PREFLIGHT_MODE" >&2
  FLEET_PREFLIGHT_MODE=warn
fi

COMMIT_MSG_FILE="${1:-.git/COMMIT_EDITMSG}"
if [ ! -f "$COMMIT_MSG_FILE" ]; then
  printf '[fleet-preflight] WARN: commit message file not found: %s; skipping\n' \
    "$COMMIT_MSG_FILE" >&2
  exit 0
fi

# Strip git-comment lines (starting with #) — these are not part of the message.
TMP_MSG=$(mktemp -t fleet-preflight-msg.XXXXXX)
trap 'rm -f "$TMP_MSG"' EXIT
# Use sed instead of grep -v so we keep ordering exactly (lineno-meaningful).
sed '/^#/d' "$COMMIT_MSG_FILE" > "$TMP_MSG"

# Find first blank line — separates subject from body.
# Anything after the first blank line is body lines.
SUBJECT_LINE_COUNT=1  # subject is line 1
BODY_START=$(awk 'NR>=2 && $0=="" { print NR+1; exit }' "$TMP_MSG")
if [ -z "$BODY_START" ]; then
  # No body — just a subject line. Nothing to preflight against.
  exit 0
fi

# Extract the body into a temp file so we can run multiple greps.
BODY_FILE=$(mktemp -t fleet-preflight-body.XXXXXX)
# shellcheck disable=SC2064
trap "rm -f \"$TMP_MSG\" \"$BODY_FILE\"" EXIT
awk -v start="$BODY_START" 'NR >= start { print }' "$TMP_MSG" > "$BODY_FILE"

# Accumulators
WARNINGS=""
ERRORS_FOUND=0  # count of warning-OR-error events (depends on mode)
RULES_FIRED=""  # space-separated list of rule IDs

# ---------------------------------------------------------------------------
# Append a finding. $1 = rule-id, $2 = severity (info|warn|error), $3 = message
# Increments ERRORS_FOUND. Records the rule for the summary.
# ---------------------------------------------------------------------------
add_finding() {
  rule="$1"
  sev="$2"
  msg="$3"
  ERRORS_FOUND=$((ERRORS_FOUND + 1))
  case " $RULES_FIRED " in
    *" $rule "*) ;;
    *) RULES_FIRED="$RULES_FIRED $rule" ;;
  esac
  WARNINGS="$WARNINGS
[fleet-preflight] $sev [$rule] $msg"
}

# ---------------------------------------------------------------------------
# Rule R1: W#NN workstream-shorthand in body
#
#   The fleet's W#NN beacon shorthand (W#60, W#74) trips wagoid commitlint's
#   footer-leading-blank rule because <word>#<digits> parses as a footer
#   token regardless of position. Cerebrum 2026-05-19 + Engineer's 3-cycle
#   trap on signal-bridge#24 motivated this.
# ---------------------------------------------------------------------------
W_HITS=$(grep -nE '^[^@]*\bW#[0-9]+' "$BODY_FILE" | head -5)
if [ -n "$W_HITS" ]; then
  while IFS= read -r line; do
    [ -z "$line" ] && continue
    lineno=$(printf '%s' "$line" | cut -d: -f1)
    snippet=$(printf '%s' "$line" | cut -d: -f2- | head -c 90)
    add_finding R1-w-shorthand warn \
      "body line $lineno contains 'W#NN' workstream-shorthand: '$snippet'"
  done <<HITS
$W_HITS
HITS
  WARNINGS="$WARNINGS
  -> R1 fix: use 'W60' / 'W23.3' / 'workstream 60' (no '#') in commit bodies; W#NN is OK in beacons + PR descriptions"
fi

# ---------------------------------------------------------------------------
# Rule R2: bare <Word>: at body line start (footer-parser trap)
#
#   wagoid v6 parses ANY `<word>:` at a body line start as a footer token,
#   including bare `Note:`, `Accessibility:`, `Refs:` ... when intermixed
#   with paragraphs it triggers footer-leading-blank-line errors.
#
#   Conventional commits has a small canonical-trailer whitelist that we
#   recognize as legitimate footers: Co-Authored-By, Signed-off-by, Refs,
#   Closes, Fixes, Resolves, See-also, Reviewed-by, BREAKING CHANGE,
#   Reverts. These are token-start positions in the LAST footer block and
#   are fine.
#
#   We flag any other `Word:` (uppercase-led, ASCII letters, no spaces in
#   the token) at line start in the body. Markdown list markers (`- `,
#   `* `, `1. `) are NOT flagged because they don't start with `Word:`.
# ---------------------------------------------------------------------------
# Note: -P (PCRE) is GNU grep, not in BSD/macOS by default. Use POSIX -E.
# Pattern: line starts with [A-Z][a-zA-Z][a-zA-Z\-]+: followed by space
BARE_WORD_HITS=$(grep -nE '^[A-Z][A-Za-z][A-Za-z-]+:[[:space:]]' "$BODY_FILE" | head -10)
if [ -n "$BARE_WORD_HITS" ]; then
  # Filter out the known-safe canonical-trailer tokens.
  CANONICAL_TRAILERS_TOKEN_RE='^(Co-Authored-By|Signed-off-by|Refs|Closes|Fixes|Resolves|See-also|See|Reviewed-by|Reviewed|Acked-by|Cc|Reported-by|Suggested-by|Tested-by|BREAKING-CHANGE|Reverts|Revert):'
  R2_ANY_FIRED=0
  # Use heredoc loop (no subshell) — pipes into while DO subshell in POSIX sh.
  while IFS= read -r line; do
    [ -z "$line" ] && continue
    lineno=$(printf '%s' "$line" | cut -d: -f1)
    body_content=$(printf '%s' "$line" | sed 's/^[0-9]*://')
    # Skip canonical trailer tokens (legitimate footers).
    if printf '%s' "$body_content" | grep -qE "$CANONICAL_TRAILERS_TOKEN_RE"; then
      continue
    fi
    token=$(printf '%s' "$body_content" | cut -d: -f1 | head -c 30)
    add_finding R2-bare-word-trailer warn \
      "body line $lineno starts with '$token:' — wagoid parses this as a footer token"
    R2_ANY_FIRED=1
  done <<HITS
$BARE_WORD_HITS
HITS
  if [ "$R2_ANY_FIRED" -eq 1 ]; then
    WARNINGS="$WARNINGS
  -> R2 fix: rephrase as prose ('For accessibility, ...' instead of 'Accessibility: ...') OR convert to a markdown list ('- accessibility — ...')"
  fi
fi

# ---------------------------------------------------------------------------
# Rule R3: <word>#<digit> inline references (cross-repo PR links in body)
#
#   `shipyard#42`, `(#13)`, `coordination#5` in commit body all parse as
#   footer tokens. Convention: cite in PR description, not commit body.
#
#   Exclude:
#     - lines starting with the canonical trailers (Refs:, Closes: etc.)
#       which is the proper place to put a `repo#NN` reference
#     - URLs (containing `://`) — `#fragment` in a URL is fine
# ---------------------------------------------------------------------------
#   The W#NN case is already covered by R1; this rule targets generic
#   <word>#<digit> patterns (`shipyard#42`, `Refs#9`, `(#13)`, `coordination#5`)
#   and explicitly skips bare `W#NNN` so the two rules don't double-report.
HASH_HITS=$(grep -nE '\b[A-Za-z][A-Za-z0-9._-]*#[0-9]+' "$BODY_FILE" | head -10)
if [ -n "$HASH_HITS" ]; then
  CANONICAL_TRAILERS_RE='^[0-9]+:(Co-Authored-By|Signed-off-by|Refs|Closes|Fixes|Resolves|See-also|See|Reviewed-by|Reviewed|Acked-by|Cc|Reported-by|Suggested-by|Tested-by|BREAKING-CHANGE|Reverts|Revert):'
  HASH_COUNTED=0
  while IFS= read -r line; do
    [ -z "$line" ] && continue
    # Skip canonical trailer lines.
    if printf '%s' "$line" | grep -qE "$CANONICAL_TRAILERS_RE"; then
      continue
    fi
    # Skip URL contexts (line contains `://`)
    if printf '%s' "$line" | grep -q '://'; then
      continue
    fi
    # Skip lines whose ONLY <word>#<digit> hit is the R1-style W#NN pattern.
    # If the line contains a non-W#-style ref anywhere, fall through.
    body_content=$(printf '%s' "$line" | sed 's/^[0-9]*://')
    non_w_refs=$(printf '%s' "$body_content" | grep -oE '\b[A-Za-z][A-Za-z0-9._-]*#[0-9]+' | grep -vE '^W#[0-9]+$' || true)
    if [ -z "$non_w_refs" ]; then
      continue
    fi
    lineno=$(printf '%s' "$line" | cut -d: -f1)
    snippet=$(printf '%s' "$body_content" | head -c 90)
    add_finding R3-repo-hash warn \
      "body line $lineno contains '<word>#<digit>' ref ('$snippet')"
    HASH_COUNTED=$((HASH_COUNTED + 1))
  done <<HITS
$HASH_HITS
HITS
  if [ "$HASH_COUNTED" -gt 0 ]; then
    WARNINGS="$WARNINGS
  -> R3 fix: cite PR refs in PR description; in commit body use prose ('the sibling shipyard PR') or a Refs: trailer at the very end (Refs: shipyard#42)"
  fi
fi

# ---------------------------------------------------------------------------
# Rule R4: body line >100 chars (fleet target)
#
#   wagoid's body-max-line-length is 120 by default; fleet keeps a stricter
#   100-char target so paragraph re-wraps don't accidentally trip the
#   120-char ceiling. Code blocks (lines INSIDE a ``` fenced block) are
#   exempt.
# ---------------------------------------------------------------------------
LONG_LINES=$(awk '
  BEGIN { in_code = 0 }
  /^```/ { in_code = !in_code; next }
  in_code { next }
  length($0) > 100 { print NR ":" length($0) ":" substr($0, 1, 60) "..." }
' "$BODY_FILE" | head -10)
if [ -n "$LONG_LINES" ]; then
  R4_ANY_FIRED=0
  while IFS= read -r line; do
    [ -z "$line" ] && continue
    lineno=$(printf '%s' "$line" | cut -d: -f1)
    body_len=$(printf '%s' "$line" | cut -d: -f2)
    snippet=$(printf '%s' "$line" | cut -d: -f3-)
    add_finding R4-body-long warn \
      "body line $lineno is $body_len chars (>100): '$snippet'"
    R4_ANY_FIRED=1
  done <<HITS
$LONG_LINES
HITS
  if [ "$R4_ANY_FIRED" -eq 1 ]; then
    WARNINGS="$WARNINGS
  -> R4 fix: hard-wrap body paragraphs at ~85 chars; 100-char limit has no headroom against the 120-char wagoid ceiling"
  fi
fi

# ---------------------------------------------------------------------------
# Rule R5: `new <Entity> {` cross-cluster fixture pattern (bug-191 risk)
#
#   When a commit adds `new (Vendor|Invoice|Bill|Payment|WorkOrder|Inspection|
#   PaymentBatch|Lease|JournalEntry) {` to test files, AND a recent commit
#   added a `required` property to that entity, the test will fail with
#   CS9035 in CI. Hard to false-positive-free; emit as warning only.
#
#   This rule is best-effort: it only checks the commit MESSAGE for
#   entity-add tokens (a heuristic for the commit's nature). Surface a
#   reminder, do NOT inspect actual staged-file diff (that's a different
#   tool's job and would slow the hook materially).
# ---------------------------------------------------------------------------
FIXTURE_ENTITIES_RE='\bnew (Vendor|Invoice|Bill|Payment|PaymentBatch|WorkOrder|Inspection|Lease|JournalEntry|Account)[[:space:]]*\{'
FIXTURE_HITS=$(grep -nE "$FIXTURE_ENTITIES_RE" "$BODY_FILE" | head -3)
if [ -n "$FIXTURE_HITS" ]; then
  R5_ANY_FIRED=0
  while IFS= read -r line; do
    [ -z "$line" ] && continue
    lineno=$(printf '%s' "$line" | cut -d: -f1)
    body_content=$(printf '%s' "$line" | sed 's/^[0-9]*://')
    snippet=$(printf '%s' "$body_content" | head -c 80)
    add_finding R5-fixture-init info \
      "body line $lineno mentions '$snippet'; cross-cluster fixture-breakage risk if the entity recently gained a 'required' property (see bug-191)"
    R5_ANY_FIRED=1
  done <<HITS
$FIXTURE_HITS
HITS
  if [ "$R5_ANY_FIRED" -eq 1 ]; then
    WARNINGS="$WARNINGS
  -> R5 reminder: if you added a 'required' property to the entity, grep all fleet repos for 'new <Entity> {' and either set the new prop, switch to factory method, or revert 'required'"
  fi
fi

# ---------------------------------------------------------------------------
# Final emission + mode-gated exit
# ---------------------------------------------------------------------------
if [ "$ERRORS_FOUND" -eq 0 ]; then
  exit 0
fi

# Trim leading newline + emit
SUMMARY="[fleet-preflight] $ERRORS_FOUND finding(s) across rules:$RULES_FIRED"
printf '%s\n' "$SUMMARY" >&2
# WARNINGS already begins with newlines; print as-is to stderr.
printf '%s\n' "$WARNINGS" >&2
printf '[fleet-preflight] mode=%s; canary docs: shipyard/_shared/engineering/commit-message-test-fixtures.md\n' \
  "$FLEET_PREFLIGHT_MODE" >&2

if [ "$FLEET_PREFLIGHT_MODE" = "block" ]; then
  printf '[fleet-preflight] BLOCK mode — commit rejected. Fix above OR re-run with FLEET_PREFLIGHT_MODE=warn for advisory-only, OR --no-verify to bypass.\n' >&2
  exit 1
fi

# warn-mode: advisory; commit proceeds
printf '[fleet-preflight] WARN mode — commit proceeds. Set FLEET_PREFLIGHT_MODE=block after canary period to enforce.\n' >&2
exit 0
