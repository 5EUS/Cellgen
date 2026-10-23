#!/usr/bin/env sh
# Everything CI checks, locally. Run before opening a PR.
set -eu
root=$(cd "$(dirname "$0")/../.." && pwd)
cd "$root"

dotnet restore --locked-mode
dotnet format whitespace --no-restore --verify-no-changes
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
dotnet list package --vulnerable --include-transitive

# Duplicate detection needs PMD (and Java); CI always runs it, locally it is optional.
if command -v pmd >/dev/null 2>&1; then
  sh scripts/development/cpd.sh
else
  echo "skipping CPD: pmd not installed" >&2
fi
