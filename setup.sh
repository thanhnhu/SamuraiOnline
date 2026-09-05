#!/usr/bin/env bash
# Build the engine, and optionally generate the playable characters.
#
# The engine and its runtime assets are both in this repository, so a clone is
# enough to build and run. Characters are not: jubei and jubei2 are generated
# from Samurai Shodown artwork, which is not ours to redistribute, so --chars
# fetches the X-Mat Studio sources and converts them locally.
#
#   ./setup.sh              build only
#   ./setup.sh --chars      generate characters, then build
#   ./setup.sh --chars --no-build
set -euo pipefail

cd "$(dirname "$0")"
REPO_ROOT="$PWD"

XMAT_URL="https://github.com/AaronGCProg/SamuraiShodown-XMatStudio.git"

WANT_CHARS=0
WANT_BUILD=1
for arg in "$@"; do
	case "$arg" in
		--chars) WANT_CHARS=1 ;;
		--no-build) WANT_BUILD=0 ;;
		*) echo "unknown option: $arg" >&2; exit 2 ;;
	esac
done

log() { printf '\n== %s\n' "$1"; }

missing=""
for tool in git go; do
	command -v "$tool" >/dev/null 2>&1 || missing="$missing $tool"
done
if [ -n "$missing" ]; then
	echo "missing required tools:$missing" >&2
	exit 1
fi

# libavfilter 10.x is FFmpeg 7.1. Anything older lacks the field the video
# layer uses, and the failure that produces is far from obvious.
if ! pkg-config --atleast-version=10 libavfilter 2>/dev/null; then
	echo "warning: libavfilter looks older than FFmpeg 7.1, or pkg-config cannot find it." >&2
	echo "         The build will fail on AVBufferSrcParameters.color_space." >&2
fi

# ---------------------------------------------------------------------------
if [ "$WANT_CHARS" = 1 ]; then
	log "Generating characters"
	WORK="$(mktemp -d)"
	trap 'rm -rf "$WORK"' EXIT
	git clone --depth 1 --quiet "$XMAT_URL" "$WORK/xmat"
	XMAT="$WORK/xmat/SamuraiShodown-1.00" "$REPO_ROOT/SamuraiTools/chargen/regen.sh"
	echo "  jubei and jubei2 written to Ikemen-GO/chars"
fi

# ---------------------------------------------------------------------------
# The gamepad database is not ours to redistribute, and the engine only warns
# when it is absent, so a fresh clone silently ends up with unmapped pads.
GAMEPAD_DB="$REPO_ROOT/Ikemen-GO/external/gamecontrollerdb.txt"
if [ ! -f "$GAMEPAD_DB" ]; then
	log "Fetching the gamepad database"
	GAMEPAD_DB_URL="https://raw.githubusercontent.com/mdqinc/SDL_GameControllerDB/refs/heads/master/gamecontrollerdb.txt"
	fetched=0
	if command -v curl >/dev/null 2>&1; then
		curl -fsSL -o "$GAMEPAD_DB" "$GAMEPAD_DB_URL" && fetched=1
	elif command -v wget >/dev/null 2>&1; then
		wget -qO "$GAMEPAD_DB" "$GAMEPAD_DB_URL" && fetched=1
	fi
	if [ "$fetched" = 1 ]; then
		echo "  Ikemen-GO/external/gamecontrollerdb.txt"
	else
		rm -f "$GAMEPAD_DB"
		echo "warning: could not fetch the gamepad database (needs curl or wget);" >&2
		echo "         controllers may need manual mapping. Keyboard play is unaffected." >&2
	fi
fi

# ---------------------------------------------------------------------------
if [ "$WANT_BUILD" = 1 ]; then
	log "Building the engine"
	(
		cd Ikemen-GO
		GOFLAGS=-mod=mod GOEXPERIMENT=arenas CGO_ENABLED=1 \
			go build -o Ikemen_GO_Linux ./src
	)
	echo "  Ikemen-GO/Ikemen_GO_Linux"
fi

log "Done"
cat <<'EOF'
Run the game:

  cd Ikemen-GO
  ./Ikemen_GO_Linux                      # native Linux
  DISPLAY=:0 WAYLAND_DISPLAY=wayland-0 \
    XDG_RUNTIME_DIR=/mnt/wslg/runtime-dir \
    ./Ikemen_GO_Linux                    # WSLg

If the build failed on missing headers, install:

  pkg-config nasm yasm build-essential libxmp-dev libsdl2-dev libgtk-3-dev
  libavformat-dev libavcodec-dev libavutil-dev libswresample-dev
  libswscale-dev libavfilter-dev
EOF
