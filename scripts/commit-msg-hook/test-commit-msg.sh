#!/bin/sh
# Fixture tests for .githooks/commit-msg
set -eu

HERE=$(CDPATH= cd -- "$(dirname "$0")" && pwd)
ROOT=$(CDPATH= cd -- "$HERE/../.." && pwd)
HOOK="$ROOT/.githooks/commit-msg"
DATA="$HERE/testdata"

if [ ! -f "$HOOK" ]; then
  echo "missing hook: $HOOK" >&2
  exit 1
fi

fail=0
errlog=$(mktemp)
trap 'rm -f "$errlog"' EXIT

for f in "$DATA"/good/*.txt; do
  [ -f "$f" ] || continue
  if sh "$HOOK" "$f" >"$errlog" 2>&1; then
    echo "PASS (accept): $(basename "$f")"
  else
    echo "FAIL (should accept): $(basename "$f")" >&2
    cat "$errlog" >&2
    fail=1
  fi
done

for f in "$DATA"/bad/*.txt; do
  [ -f "$f" ] || continue
  if sh "$HOOK" "$f" >"$errlog" 2>&1; then
    echo "FAIL (should reject): $(basename "$f")" >&2
    fail=1
  else
    echo "PASS (reject): $(basename "$f")"
  fi
done

if [ "$fail" -ne 0 ]; then
  echo "commit-msg hook tests FAILED" >&2
  exit 1
fi
echo "commit-msg hook tests passed"
exit 0
