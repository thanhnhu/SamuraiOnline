package main

import (
	"fmt"
	"io"
	"net/http"
	"os"
	"testing"
	"time"
)

// Live end-to-end check of the lobby path against a real server, using the
// engine's own client code rather than a reimplementation of it.
//
// It needs two processes, because natSession and natResult are package-level
// singletons: one peer per process, coordinated through the lobby itself.
//
//	SAMURAI_LIVE_LOBBY=http://192.168.2.210:8080 SAMURAI_LIVE_ROLE=host \
//	  go test ./src -run TestLiveLobby -v -count=1 -vet=off
//	SAMURAI_LIVE_LOBBY=http://192.168.2.210:8080 SAMURAI_LIVE_ROLE=guest \
//	  go test ./src -run TestLiveLobby -v -count=1 -vet=off
//
// Skipped unless the environment names a server, so CI never depends on one.
func TestLiveLobbyRoundTrip(t *testing.T) {
	url := os.Getenv("SAMURAI_LIVE_LOBBY")
	if url == "" {
		t.Skip("set SAMURAI_LIVE_LOBBY to run this against a real server")
	}
	role := os.Getenv("SAMURAI_LIVE_ROLE")
	if role != "host" && role != "guest" {
		t.Fatal("set SAMURAI_LIVE_ROLE to host or guest")
	}
	roomName := os.Getenv("SAMURAI_LIVE_ROOM")
	if roomName == "" {
		roomName = "livecheck"
	}
	port := 7500
	if role == "guest" {
		port = 7501
	}

	lc := &LobbyClient{hc: &http.Client{Timeout: lobbyRequestTimeout}}
	t.Cleanup(lc.Disconnect)

	step := func(what string) func() {
		start := time.Now()
		t.Logf("-> %s", what)
		return func() { t.Logf("   %s: %s", what, time.Since(start).Round(time.Millisecond)) }
	}

	// waitFor polls a condition the way the Lua menu does: off the request
	// path, watching the snapshot the client publishes.
	waitFor := func(what string, timeout time.Duration, cond func() bool) {
		t.Helper()
		deadline := time.Now().Add(timeout)
		for {
			if cond() {
				return
			}
			_, _, lastErr := lc.Snapshot()
			if time.Now().After(deadline) {
				t.Fatalf("timed out waiting for %s (last error: %q)", what, lastErr)
			}
			time.Sleep(200 * time.Millisecond)
		}
	}

	done := step("register with the lobby")
	lc.Connect(url, "live-"+role, port)
	waitFor("the session", 30*time.Second, func() bool {
		connected, _, _ := lc.Snapshot()
		return connected
	})
	done()

	done = step("take a seat in the room")
	if role == "host" {
		lc.CreateRoom(roomName, true)
	} else {
		var roomID string
		waitFor("the host's room", 60*time.Second, func() bool {
			for _, r := range lc.Rooms() {
				if r.Name == roomName && r.Players < r.Capacity {
					roomID = r.ID
					return true
				}
			}
			return false
		})
		lc.JoinRoom(roomID)
	}
	waitFor("both seats to fill", 60*time.Second, func() bool {
		m, _, ok := lc.Match()
		return ok && m.State == "ready"
	})
	done()

	m, _, _ := lc.Match()
	t.Logf("   role=%s peer=%q peerUdp=%q hostAddr=%s:%d",
		m.Role, m.PeerName, m.PeerUDP, m.HostAddr, m.HostPort)
	if m.PeerUDP == "" {
		t.Fatal("the lobby never observed the peer's UDP address: " +
			"the address-discovery packet is not reaching the server")
	}

	done = step("negotiate a path to the peer")
	lc.EstablishPath()
	waitFor("hole punching to settle", 60*time.Second, func() bool {
		return lc.NATMode() != ""
	})
	done()

	mode := lc.NATMode()
	t.Logf("   NAT mode: %s", mode)
	switch mode {
	case string(NATPunched):
		t.Log("   peers reached each other directly")
	case string(NATRelayed):
		t.Log("   NOTE: punching failed, gameplay would bounce off the relay")
	default:
		t.Fatalf("unexpected NAT mode %q", mode)
	}

	if !lc.RelayStreamAvailable() {
		t.Log("   no TCP relay offered; skipping the setup-stream check")
		return
	}

	done = step("splice the match-setup stream through the relay")
	plan := lc.RelayPlan()
	conn, err := dialRelayStream(plan, 15*time.Second)
	if err != nil {
		t.Fatalf("relay dial: %v", err)
	}
	defer conn.Close()
	// The host drives the handshake, exactly as AcceptRelayed/ConnectRelayed do.
	if err := netHandshake(conn, role == "host", relayStreamTimeout); err != nil {
		t.Fatalf("relayed handshake: %v", err)
	}
	done()

	// The stream has to survive real traffic, not just the handshake.
	if role == "host" {
		payload := []byte{0x00, 0xC7, 0xFF, 0x42}
		if _, err := conn.Write(payload); err != nil {
			t.Fatalf("write after handshake: %v", err)
		}
	} else {
		got := make([]byte, 4)
		conn.SetReadDeadline(time.Now().Add(15 * time.Second))
		if _, err := io.ReadFull(conn, got); err != nil {
			t.Fatalf("read after handshake: %v", err)
		}
		want := []byte{0x00, 0xC7, 0xFF, 0x42}
		for i := range want {
			if got[i] != want[i] {
				t.Fatalf("payload corrupted: got % x, want % x", got, want)
			}
		}
		t.Log("   payload survived the relay intact")
	}

	fmt.Printf("LIVE CHECK OK role=%s mode=%s\n", role, mode)
}
