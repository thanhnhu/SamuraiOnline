package main

import (
	"fmt"
	"testing"

	ggpo "github.com/ikemen-engine/ggpo"
)

func TestSplitHostPortRejectsGarbage(t *testing.T) {
	for _, bad := range []string{"", "203.0.113.1", "203.0.113.1:", "203.0.113.1:0", "203.0.113.1:70000", "203.0.113.1:abc"} {
		if _, _, err := splitHostPort(bad); err == nil {
			t.Fatalf("accepted %q", bad)
		}
	}
	host, port, err := splitHostPort("203.0.113.1:40001")
	if err != nil || host != "203.0.113.1" || port != 40001 {
		t.Fatalf("got %q %d %v", host, port, err)
	}
}

// A watcher with a broken address must be skipped without taking the match
// down, and without consuming one of the backend's spectator slots.
func TestAttachSpectatorsSkipsBadAddressesAndKeepsGoodOnes(t *testing.T) {
	rs := NewRollbackSession(RollbackProperties{})

	rs.spectatorAddrs = []string{"not-an-address", "203.0.113.9:notaport"}
	for i := 0; i < ggpo.MaxSpectators; i++ {
		rs.spectatorAddrs = append(rs.spectatorAddrs, fmt.Sprintf("203.0.113.9:%d", 40000+i))
	}

	peer := ggpo.NewPeer(&rs, 0, 2, 4)
	if err := peer.InitializeConnection(); err != nil {
		t.Fatalf("init connection: %v", err)
	}

	rs.attachSpectators(&peer)

	// If the two malformed entries had been registered, or any valid one had
	// been dropped, this count would be wrong. Exactly MaxSpectators must have
	// gone in, so one more has to be refused.
	if err := peer.AddSpectator("203.0.113.9:41000", 41000); err == nil {
		t.Fatal("backend accepted more than MaxSpectators, so bad addresses were registered")
	}
}

// The host must survive being handed no spectators at all, which is the normal
// case for a private match.
func TestAttachSpectatorsWithNoneIsANoOp(t *testing.T) {
	rs := NewRollbackSession(RollbackProperties{})
	peer := ggpo.NewPeer(&rs, 0, 2, 4)
	if err := peer.InitializeConnection(); err != nil {
		t.Fatalf("init connection: %v", err)
	}
	rs.attachSpectators(&peer)

	if err := peer.AddSpectator("203.0.113.9:41000", 41000); err != nil {
		t.Fatalf("no slots were left despite attaching nothing: %v", err)
	}
}
