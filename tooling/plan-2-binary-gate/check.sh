#!/usr/bin/env bash
set -euo pipefail

# Plan 2 Task 3.6 binary gate (now a permanent CI assertion per Plan 5 Task 8).
# Two assertions:
#   1. At least 14 blocks-* packages have Resources/Localization/SharedResource.resx
#   2. AddLocalization() call sites ≥ 3
#
# Note: avoid `find packages/blocks-*` glob expansion — under bash set -e + nullglob
# not set, an unmatched glob (e.g. sparse checkout with packages/ absent) exits 2.
# Use `find packages/ -name blocks-*` depth-limited instead.

EXPECTED_BLOCKS_RESX=14
MIN_ADDLOC_CALLSITES=3

BLOCKS_RESX_COUNT=$(find packages/ -mindepth 3 -maxdepth 5 -name SharedResource.resx -path '*/Resources/Localization/*' 2>/dev/null | wc -l | tr -d ' ')
if [ "$BLOCKS_RESX_COUNT" -lt "$EXPECTED_BLOCKS_RESX" ]; then
  echo "SUNFISH_PLAN_2_GATE: blocks-* SharedResource.resx count $BLOCKS_RESX_COUNT < expected $EXPECTED_BLOCKS_RESX" >&2
  echo "Missing packages:" >&2
  for d in packages/blocks-*/; do
    [ -d "$d" ] || continue
    if [ ! -f "$d/Resources/Localization/SharedResource.resx" ]; then
      echo "  $d" >&2
    fi
  done
  exit 1
fi

# Build search dirs list — skip dirs that don't exist (e.g. accelerators/ not in all checkouts)
SEARCH_DIRS=()
for d in packages/ apps/ accelerators/; do [ -d "$d" ] && SEARCH_DIRS+=("$d"); done
ADDLOC_COUNT=$(grep -r 'services\.AddLocalization()' "${SEARCH_DIRS[@]}" --include='*.cs' -l 2>/dev/null | wc -l | tr -d ' ')
if [ "$ADDLOC_COUNT" -lt "$MIN_ADDLOC_CALLSITES" ]; then
  echo "SUNFISH_PLAN_2_GATE: services.AddLocalization() call sites $ADDLOC_COUNT < min $MIN_ADDLOC_CALLSITES" >&2
  exit 1
fi

echo "Plan 2 binary gate: PASS"
echo "  blocks-* SharedResource.resx: $BLOCKS_RESX_COUNT / $EXPECTED_BLOCKS_RESX"
echo "  services.AddLocalization() call sites: $ADDLOC_COUNT (min $MIN_ADDLOC_CALLSITES)"
