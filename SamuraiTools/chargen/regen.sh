#!/usr/bin/env bash
# Regenerate the playable characters from the X-Mat Studio animation tables.
#
# XMAT defaults to the sibling checkout in the workspace; override it if your
# copy lives elsewhere. That project is not part of this repository.
set -euo pipefail

cd "$(dirname "$0")"
XMAT="${XMAT:-../../../SamuraiShodown-XMatStudio/SamuraiShodown-1.00}"

if [ ! -f "$XMAT/ModulePlayer.cpp" ]; then
	echo "cannot find ModulePlayer.cpp under $XMAT" >&2
	echo "set XMAT to the X-Mat Studio checkout" >&2
	exit 1
fi

go run . -src "$XMAT/ModulePlayer.cpp" \
	-sheet "$XMAT/Game/Assets/Sprites/jubei.png" \
	-out ../../Ikemen-GO/chars/jubei -name Jubei -base jubei

go run . -src "$XMAT/ModulePlayer2.cpp" \
	-sheet "$XMAT/Game/Assets/Sprites/Jubei2.png" \
	-out ../../Ikemen-GO/chars/jubei2 -name "Jubei 2P" -base jubei2
