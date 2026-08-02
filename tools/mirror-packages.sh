#!/usr/bin/env bash
#
# Capture the exact NuGet package closure this repository builds against
# into nuget-mirror/,
# turning it into a self-contained local feed (see NuGet.config).
#
#   ./tools/mirror-packages.sh [output-dir]     # default: ./nuget-mirror
#
# Directory.Build.targets pins every external version,
# which holds a restore steady against version drift.
# A populated mirror holds it steady against a package
# being unpublished or delisted upstream:
# restore keeps working from the folder feed alone.
#
# Git ignores the mirror's .nupkg files (see .gitignore),
# so store the produced folder somewhere durable:
# the mirror.yml workflow uploads it as a CI artifact,
# or attach it to a GitHub release.
#
# Verify a mirror is complete, restoring from the folder feed alone:
#   dotnet restore StockSharp_Tests.slnx --source ./nuget-mirror --packages /tmp/verify-pkgs
# A --packages restore rewrites the projects' obj/ assets to point at that folder,
# so run a plain `dotnet restore` afterwards (or verify in a scratch clone)
# before building from the same working tree again.

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
out="${1:-$root/nuget-mirror}"
cache="$(mktemp -d)"
trap 'rm -rf "$cache"' EXIT

# Restore all three solutions into a fresh, private package folder,
# so the copy step below sees the complete closure
# that a warm global cache would hide.
# The samples solution contributes packages the library solutions never
# reach (Ecng.Interop, StockSharp.Samples.HistoryData - see
# docs/ecng-surface.md), so skipping it leaves the mirror incomplete.
dotnet restore "$root/StockSharp.slnx" --packages "$cache"
dotnet restore "$root/StockSharp_Tests.slnx" --packages "$cache"
dotnet restore "$root/StockSharp_Samples.slnx" --packages "$cache"

# Point every project's obj/ assets back at the regular package folder.
# The restores above aimed those assets at the temporary folder deleted on exit,
# so a later build in this tree silently mis-evaluates:
# test projects evaluate as ordinary libraries.

restore_assets() {
	dotnet restore "$root/StockSharp.slnx"
	dotnet restore "$root/StockSharp_Tests.slnx"
	dotnet restore "$root/StockSharp_Samples.slnx"
}
trap 'restore_assets; rm -rf "$cache"' EXIT

mkdir -p "$out"

count=0
while IFS= read -r -d '' nupkg; do
	dest="$out/$(basename "$nupkg")"

	if [ ! -e "$dest" ]; then
		cp "$nupkg" "$dest"
		count=$((count + 1))
	fi
done < <(find "$cache" -name '*.nupkg' -print0)

total="$(find "$out" -maxdepth 1 -name '*.nupkg' | wc -l)"
echo "Added $count new package(s); mirror now holds $total at: $out"
