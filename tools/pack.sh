#!/usr/bin/env bash
#
# Produce the fork-branded NuGet packages (see common_packaging.props).
#
#   ./tools/pack.sh [output-dir]      # default: ./artifacts/nupkgs
#
# Package ids default to StockShark.* (rebrand with -p:ForkPackagePrefix=...),
# and assembly names and namespaces stay StockSharp.*,
# so switching a consumer from upstream packages to these
# is a PackageReference-id change alone.
#
# The version comes from ForkPackageVersion in common_packaging.props;
# override per release with:
#   PACK_ARGS="-p:ForkPackageVersion=5.0.1" ./tools/pack.sh

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
out="${1:-$root/artifacts/nupkgs}"

# ContinuousIntegrationBuild normalizes stored paths,
# and the fixed InformationalVersion replaces the default build timestamp,
# so repeated packs of the same commit produce identical package metadata.
dotnet pack "$root/StockSharp.slnx" \
	--configuration Release \
	--output "$out" \
	-p:ContinuousIntegrationBuild=true \
	-p:InformationalVersion=fork \
	${PACK_ARGS:-}

echo
echo "Packed $(find "$out" -maxdepth 1 -name '*.nupkg' | wc -l) package(s) to: $out"
