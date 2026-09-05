package main

import (
	"net"
	"testing"
	"time"
)

// The address a peer must aim at when punching is the one the server observed,
// so the discovery packet has to feed it straight back into the store.
func TestAddrRequestRegistersObservedAddress(t *testing.T) {
	store := NewStore()
	_, token, udpToken, err := store.CreateSession("Genjuro", "203.0.113.9", 7500)
	if err != nil {
		t.Fatalf("create session: %v", err)
	}

	relay := NewRelay()
	relay.OnAddrRequest = store.RecordUDPAddr
	server, err := relay.Listen("127.0.0.1:0")
	if err != nil {
		t.Fatalf("listen: %v", err)
	}
	go relay.Serve()
	t.Cleanup(func() { _ = relay.Close() })

	c := dial(t, server)
	if _, err := c.Write(NewAddrRequest(udpToken)); err != nil {
		t.Fatalf("write: %v", err)
	}
	if _, err := c.Read(make([]byte, 64)); err != nil {
		t.Fatalf("read: %v", err)
	}

	deadline := time.Now().Add(time.Second)
	var recorded string
	for time.Now().Before(deadline) {
		resp, err := store.Poll(token)
		if err != nil {
			t.Fatalf("poll: %v", err)
		}
		_ = resp
		store.mu.Lock()
		for _, p := range store.players {
			recorded = p.UDPAddr
		}
		store.mu.Unlock()
		if recorded != "" {
			break
		}
		time.Sleep(10 * time.Millisecond)
	}

	if recorded == "" {
		t.Fatal("the observed UDP address was never recorded")
	}
	local := c.LocalAddr().(*net.UDPAddr)
	expected := (&net.UDPAddr{IP: net.IPv4(127, 0, 0, 1), Port: local.Port}).String()
	if recorded != expected {
		t.Fatalf("recorded %q, expected %q", recorded, expected)
	}
}

// A wrong token must not let anyone overwrite another player's address.
func TestAddrRequestWithUnknownTokenIsIgnored(t *testing.T) {
	store := NewStore()
	_, _, _, err := store.CreateSession("Ukyo", "203.0.113.9", 7500)
	if err != nil {
		t.Fatalf("create session: %v", err)
	}

	relay := NewRelay()
	relay.OnAddrRequest = store.RecordUDPAddr
	server, err := relay.Listen("127.0.0.1:0")
	if err != nil {
		t.Fatalf("listen: %v", err)
	}
	go relay.Serve()
	t.Cleanup(func() { _ = relay.Close() })

	c := dial(t, server)
	_, _ = c.Write(NewAddrRequest("not-a-real-token"))
	_, _ = c.Read(make([]byte, 64))

	time.Sleep(100 * time.Millisecond)
	store.mu.Lock()
	defer store.mu.Unlock()
	for _, p := range store.players {
		if p.UDPAddr != "" {
			t.Fatalf("an unknown token set an address: %q", p.UDPAddr)
		}
	}
}

// Both peers need each other's punch target, not just the guest.
func TestPollExposesPeerUDPAddressToBothSides(t *testing.T) {
	store := NewStore()
	_, hostTok, hostUDP, _ := store.CreateSession("Host", "203.0.113.1", 7500)
	_, guestTok, guestUDP, _ := store.CreateSession("Guest", "203.0.113.2", 7500)

	store.RecordUDPAddr(hostUDP, &net.UDPAddr{IP: net.IPv4(203, 0, 113, 1), Port: 40001})
	store.RecordUDPAddr(guestUDP, &net.UDPAddr{IP: net.IPv4(203, 0, 113, 2), Port: 40002})

	room, err := store.CreateRoom(hostTok, "r", true)
	if err != nil {
		t.Fatalf("create room: %v", err)
	}
	if _, err := store.JoinRoom(guestTok, room.ID, false); err != nil {
		t.Fatalf("join: %v", err)
	}

	hostPoll, _ := store.Poll(hostTok)
	if hostPoll.Match.PeerUDP != "203.0.113.2:40002" {
		t.Fatalf("host saw peer UDP %q", hostPoll.Match.PeerUDP)
	}
	guestPoll, _ := store.Poll(guestTok)
	if guestPoll.Match.PeerUDP != "203.0.113.1:40001" {
		t.Fatalf("guest saw peer UDP %q", guestPoll.Match.PeerUDP)
	}
}

// Both peers share one relay key but must sit in opposite slots.
func TestRelayAllocationGivesOppositeSlots(t *testing.T) {
	store := NewStore()
	_, hostTok, _, _ := store.CreateSession("Host", "203.0.113.1", 7500)
	_, guestTok, _, _ := store.CreateSession("Guest", "203.0.113.2", 7500)

	room, _ := store.CreateRoom(hostTok, "r", true)
	if _, err := store.JoinRoom(guestTok, room.ID, false); err != nil {
		t.Fatalf("join: %v", err)
	}

	hostKey, hostSlot, err := store.RelayFor(hostTok)
	if err != nil {
		t.Fatalf("host relay: %v", err)
	}
	guestKey, guestSlot, err := store.RelayFor(guestTok)
	if err != nil {
		t.Fatalf("guest relay: %v", err)
	}

	if hostKey != guestKey {
		t.Fatal("peers in the same room must share a relay key")
	}
	if hostSlot != 0 || guestSlot != 1 {
		t.Fatalf("slots were host=%d guest=%d", hostSlot, guestSlot)
	}
}
