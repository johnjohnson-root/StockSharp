#!/usr/bin/env bash
#
# Capture the exact NuGet package closure this repository builds against into
# nuget-mirror/, turning it into a self-contained local feed (see NuGet.config).
#
# Why: every external version is pinned in Directory.Build.targets, but pinning
# does not survive a package being unpublished or delisted upstream. A populated
# mirror does: restore keeps working from the folder feed alone.
#
#   ./tools/mirror-packages.sh [output-dir]     # default: ./nuget-mirror
#
# The mirror is NOT committed to git (see .gitignore) - store the produced
# folder somewhere durable: the mirror.yml workflow uploads it as a CI
# artifact, or attach it to a GitHub release.
#
# Verify a mirror is complete (restores with no network fallback):
#   dotnet restore StockSharp_Tests.slnx --source ./nuget-mirror --packages /tmp/verify-pkgs
# Note: a --packages restore rewrites the projects' obj/ assets to point at that
# folder; run a plain `dotnet restore` afterwards (or verify in a scratch clone)
# before building from the same working tree again.

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
out="${1:-$root/nuget-mirror}"
cache="$(mktemp -d)"
trap 'rm -rf "$cache"' EXIT

# Restore both solutions into a fresh, private package folder so the copy step
# below sees the complete closure (a warm global cache would hide packages).
dotnet restore "$root/StockSharp.slnx" --packages "$cache"
dotnet restore "$root/StockSharp_Tests.slnx" --packages "$cache"

# The restores above rewrote every project's obj/ assets (nuget.g.props etc.)
# to point into the temporary package folder, which is deleted on exit -
# leaving the working tree in a state where builds silently mis-evaluate
# (e.g. test projects stop being recognized as such). Restore normally to
# point the assets back at the regular package folder before finishing.
restore_assets() {
	dotnet restore "$root/StockSharp.slnx"
	dotnet restore "$root/StockSharp_Tests.slnx"
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
