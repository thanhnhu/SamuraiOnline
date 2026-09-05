# Samurai Shodown II — Online

*English · [Tiếng Việt](README.vi.md)*

Play Samurai Shodown II over the internet with rollback netcode, matchmaking,
and NAT traversal, without either player forwarding a port.

**Architecture, wire protocols, and current status: [`ARCHITECTURE.md`](ARCHITECTURE.md).**

> This repository ships the **engine and tools only**. Samurai Shodown II is
> still owned and sold by SNK; you must supply your own copy of the game data.
> Same model as ScummVM and OpenRA.

---

## Quick start

```bash
git clone https://github.com/thanhnhu/SamuraiOnline.git
cd SamuraiOnline
./setup.sh --chars
```

`setup.sh` fetches the engine assets, generates the playable characters, and
builds. Then:

```bash
cd Ikemen-GO
./Ikemen_GO_Linux                                          # Linux
DISPLAY=:0 WAYLAND_DISPLAY=wayland-0 \
  XDG_RUNTIME_DIR=/mnt/wslg/runtime-dir ./Ikemen_GO_Linux  # WSLg
```

### What setup.sh does, and why it is needed

The engine **and its runtime assets** are both in this repository, so a clone
is enough to build and run. Upstream Ikemen GO keeps assets in a separate
repository; this fork does not, because a self-contained clone is worth more
here than a small one. They are CC BY 3.0 and CC BY-NC 3.0 — see
`Ikemen-GO/LICENCE.txt` for attribution.

Characters are the exception. `jubei` and `jubei2` are generated from Samurai
Shodown artwork, which is not ours to redistribute, so `--chars` clones
[X-Mat Studio](https://github.com/AaronGCProg/SamuraiShodown-XMatStudio) and
runs `chargen` over its animation tables locally.

> That repository carries **no licence**. Public on GitHub is not the same as
> open source: without one, the default is all rights reserved, and GitHub's
> terms allow viewing and forking on GitHub but not redistribution. Its sprites
> are SNK's in any case, so a licence would not cover them either. Nothing from
> it is committed here — `setup.sh` fetches it on your machine and converts it
> there. Doing more than that would need the authors' permission.

Prebuilt binaries for Linux and Windows are published under
[Releases](https://github.com/thanhnhu/SamuraiOnline/releases) if you would
rather not install a Go and FFmpeg toolchain. They ship without characters for
the same reason, and include `chargen` so you can generate them. The Windows
bundle carries its own FFmpeg and SDL DLLs, so nothing needs installing — but
it does need a GPU that can do OpenGL 3.3.

### Requirements

Building needs Linux; on Windows either use the release build, or build inside
WSL or MSYS2.

```
git go pkg-config nasm yasm build-essential libxmp-dev libsdl2-dev
libgtk-3-dev libavformat-dev libavcodec-dev libavutil-dev
libswresample-dev libswscale-dev libavfilter-dev
```

`setup.sh` also wants `curl` or `wget`, to fetch the community gamepad
database. Without it the game still runs; controllers just need mapping by
hand.

**FFmpeg 7.1 or newer.** Older releases lack
`AVBufferSrcParameters.color_space`, which Ikemen's video layer uses, and the
build fails on it. Debian trixie and Ubuntu 25.04 are new enough; Ubuntu 24.04
is not.

### Playing online

```bash
# On a host both players can reach
cd SamuraiLobby
go run . -addr :8080 -relay-addr :8081 -relay-tcp-addr :8081 -relay-host <public-ip>
```

Point `Netplay.LobbyURL` in `Ikemen-GO/save/config.ini` at that server, then
pick **ONLINE LOBBY** from the main menu. In the room list, **Enter** joins as
a player and **S** joins as a watcher; watching does not use the second player
seat, so a room stays joinable while people spectate.

Modules: `Ikemen-GO/` (engine fork), `ggpo/` (rollback fork), `SamuraiLobby/`
(server), `SamuraiTools/chargen` and `SamuraiTools/sprtool` (asset pipeline).
There is no root `go.mod` — each is a separate module.

### Checking the connection before you blame the game

`Ikemen-GO/cmd/netcheck` runs the engine's own NAT traversal and relay code as
a standalone binary with no dependencies, so it can be dropped onto any machine
and tells you whether the two peers punch through or fall back to the relay:

```bash
cd Ikemen-GO
CGO_ENABLED=0 go build -o netcheck ./cmd/netcheck
CGO_ENABLED=0 GOOS=windows GOARCH=amd64 go build -o netcheck.exe ./cmd/netcheck

./netcheck -lobby http://<lobby-host>:8080 -role host  -room check
./netcheck -lobby http://<lobby-host>:8080 -role guest -room check
```

Running the lobby as a service: see `deploy/samurai-lobby.service` and
`deploy/samurai-lobby.default`. Set `LOBBY_RELAY_HOST` to an address the
**outside world** can reach, not the server's own; getting that wrong makes
hole punching fail silently.

---

## Two possible directions

The project started down a **Unity rewrite** and switched to **forking an
existing engine**. Both are recorded here so either can be picked up.

### A. Fork Ikemen GO — the current direction

Ikemen GO is a mature, MUGEN-compatible fighting engine in Go that already
integrates GGPO rollback netcode. The work becomes: matchmaking, NAT traversal,
and converting assets — not physics, hitboxes, or netcode.

Where it stands: lobby server and NAT traversal work and are tested; hole
punching has been verified between two machines with a NAT between them; the
engine runs; one playable character exists. The gaps are listed honestly in
[`ARCHITECTURE.md` §8](ARCHITECTURE.md#8-status) — the big ones being that
nothing has been tried across the internet, the game itself has never been
played over the network, and there is no Samurai Shodown II roster yet.

### B. Rewrite in Unity — the original plan, still viable

The partial attempt lives in `unity-prototype/`. It uses **Photon (PUN)**, not
Mirror as originally sketched.

Original roadmap:

1. Analyse the decompiled binary: game states, input handling, collision
2. Split into modules: input, render, logic, network
3. Build the offline game first
4. Add networking with a choice of client-server or peer-to-peer, synchronising
   inputs rather than state (the fighting-game standard)
5. Tune for latency and desync; add UI, room browser, matchmaking

Unity setup, as originally specified:

- Unity 2022.3 LTS or newer, **2D Core** template
- Packages: Input System, TextMeshPro, 2D Animation, 2D PSD Importer
- Networking: Photon PUN (already imported) or
  [Mirror](https://github.com/vis2k/Mirror)

**Why it was set aside.** Reimplementing a 1994 fighting game means recreating
its frame data, hitboxes, cancel windows, and physics exactly, or it will not
feel like the original. On top of that, rollback netcode is far harder to
retrofit onto an engine than to inherit from one: it needs deterministic
simulation and cheap full-state save/restore, which has to be designed in from
the first line. Forking an engine that already solved both reaches a playable
online game far sooner.

**What resuming would need.** The work already done here transfers directly:

- `SamuraiTools/sprtool` decodes the original `.SPR` sprites — the format is
  fully reverse-engineered and documented in `ARCHITECTURE.md` §5
- `SamuraiLobby` is a plain HTTP + UDP/TCP service with no engine dependency,
  so it works unchanged as Unity's matchmaking and relay backend
- The NAT traversal design (`Ikemen-GO/src/netpath/`) is engine-agnostic and
  already builds as its own cgo-free package; only the socket plumbing is
  Go-specific

What would still be missing is the part that stopped the sprite work too: the
`.SPR` records carry no pivot, no animation grouping, no frame duration and no
hitboxes. Those live in the game's own logic and would have to be reverse
engineered as well, whichever engine you build on.

---

## Legal

Samurai Shodown II is © SNK. Game data, sprites or audio extracted from it, and
the decompiled binary must never be committed or distributed. `.gitignore` and
a CI check enforce this; do not weaken either.
