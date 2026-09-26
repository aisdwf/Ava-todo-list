#!/bin/sh
# Install versioned hooks into the shared git dir (all worktrees).
set -eu

ROOT=$(git rev-parse --show-toplevel)
COMMON=$(git rev-parse --git-common-dir)
case "$COMMON" in
  /*) ;;
  *) COMMON="$ROOT/$COMMON" ;;
esac

SRC="$ROOT/.githooks/commit-msg"
DEST_DIR="$COMMON/hooks"
DEST="$DEST_DIR/commit-msg"

if [ ! -f "$SRC" ]; then
  echo "missing versioned hook: $SRC" >&2
  exit 1
fi

mkdir -p "$DEST_DIR"
# Normalize to LF.
tr -d '\r' < "$SRC" > "$DEST"
chmod +x "$DEST"

echo "Installed commit-msg hook -> $DEST"
echo "Source: $SRC"
