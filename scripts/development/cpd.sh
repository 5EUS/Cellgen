#!/usr/bin/env sh
# Copy/paste detection with PMD CPD. Needs `pmd` (https://pmd.github.io) on PATH.
set -eu
root=$(cd "$(dirname "$0")/../.." && pwd)
cd "$root"

if ! command -v pmd >/dev/null 2>&1; then
  echo "pmd not found; install it (e.g. 'brew install pmd') to run duplicate detection." >&2
  exit 1
fi

# Hand-written sources only: build output (obj/, bin/) holds generated code that
# is duplicated per configuration. Exits non-zero when 100+ token duplicates are found.
list=$(mktemp)
trap 'rm -f "$list"' EXIT
find src tests \( -name obj -o -name bin \) -prune -o -name '*.cs' -print > "$list"
pmd cpd --language cs --minimum-tokens 100 --file-list "$list"
