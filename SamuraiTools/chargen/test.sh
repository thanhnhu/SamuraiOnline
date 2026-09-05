#!/usr/bin/env bash
# Run the chargen test suite against the X-Mat Studio sources. Without
# CHARGEN_SOURCE the parsing tests skip themselves, which is easy to mistake
# for a pass.
set -euo pipefail
cd "$(dirname "$0")"

XMAT="${XMAT:-../../../SamuraiShodown-XMatStudio/SamuraiShodown-1.00}"
if [ ! -f "$XMAT/ModulePlayer.cpp" ]; then
	echo "cannot find ModulePlayer.cpp under $XMAT" >&2
	exit 1
fi

export CHARGEN_SOURCE="$XMAT/ModulePlayer.cpp"
go vet ./...
go test ./... -count=1 "$@"
