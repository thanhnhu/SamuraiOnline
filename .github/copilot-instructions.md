---
applyTo: "**"
---

# Samurai Shodown II Online

Fork of Ikemen GO plus a matchmaking server. Read `ARCHITECTURE.md` first.

## Never commit game assets

SNK still owns and sells this game. `Game/`, `Decompiled code/`, `*.SPR`,
`*.BGR`, `*.PRG`, extracted sprites, and generated characters are ignored on
purpose. Do not add them, and do not weaken `.gitignore`.

## Multi-module workspace

Five independent Go modules; there is no root `go.mod`. `cd` into a module
before running `go` commands.

## Engine builds only under Linux, with these flags

Run from the repository root, inside WSL on Windows:

```bash
cd Ikemen-GO
GOFLAGS=-mod=mod GOEXPERIMENT=arenas CGO_ENABLED=1 go build -o Ikemen_GO_Linux ./src
```

Tests need `-vet=off`. Running the game needs
`DISPLAY=:0 WAYLAND_DISPLAY=wayland-0 XDG_RUNTIME_DIR=/mnt/wslg/runtime-dir`.

## Character generation gotchas

Both are pinned by tests in `Ikemen-GO/src/`; do not work around a failure.

- The `.def` must contain `st = <base>.cns`. Ikemen's `Compile()` never reads a
  `cns` key, so omitting it silently drops every state.
- Characters must not define states 0/10/11/12/20/40/50/52. They live in
  `data/common1.cns.zss`, and the engine drives transitions into them
  (`char.go`, "Perform basic actions") only while `ctrl` is set.
- Those states name animations by number. `commonanims_test.go` derives the
  required set from `common1.cns.zss`; do not replace it with a hand-written
  list, and justify anything added to `optionalCommonAnims`.
- Any test using key names must call `initLUTs()` first.

## Netplay

Two paths with different goals: UDP carries GGPO inputs and prefers a direct
hole-punched route; TCP carries match setup and always uses the lobby relay,
because a host behind NAT cannot accept inbound connections.

The UDP socket must be the one already used to talk to the lobby — NAT maps per
source port. This is why `ggpo` is forked for `NewUdpWithConn`.

The relay wire formats are in `ARCHITECTURE.md` §3. The magic strings
`IKTCPRLY` and `IKPUNCH1` are duplicated across modules by necessity; change
both sides together.

## PowerShell 5.1

No ternary operator; `$?` gets interpolated; `&&` is rewritten to `;`; git
writes progress to stderr and shows up red. Prefer writing a script file over
long inline commands with nested quotes.

## Style

Explain why, not what. State what the code cannot show on its own; do not
restate the next line or narrate the change for a reviewer.
