package main

import (
	"bytes"
	"net"
	"testing"
	"time"
)

func startRelay(t *testing.T) (*Relay, *net.UDPAddr) {
	t.Helper()
	r := NewRelay()
	addr, err := r.Listen("127.0.0.1:0")
	if err != nil {
		t.Fatalf("listen: %v", err)
	}
	go r.Serve()
	t.Cleanup(func() { _ = r.Close() })
	return r, addr
}

func dial(t *testing.T, server *net.UDPAddr) *net.UDPConn {
	t.Helper()
	c, err := net.DialUDP("udp", nil, server)
	if err != nil {
		t.Fatalf("dial: %v", err)
	}
	t.Cleanup(func() { _ = c.Close() })
	if err := c.SetReadDeadline(time.Now().Add(2 * time.Second)); err != nil {
		t.Fatalf("deadline: %v", err)
	}
	return c
}

func TestAddressDiscoveryReportsObservedPort(t *testing.T) {
	_, server := startRelay(t)
	c := dial(t, server)

	if _, err := c.Write(NewAddrRequest("deadbeef")); err != nil {
		t.Fatalf("write: %v", err)
	}
	buf := make([]byte, 64)
	n, err := c.Read(buf)
	if err != nil {
		t.Fatalf("read: %v", err)
	}
	got, err := DecodeAddrResponse(buf[:n])
	if err != nil {
		t.Fatalf("decode: %v", err)
	}

	local := c.LocalAddr().(*net.UDPAddr)
	if got.Port != local.Port {
		t.Fatalf("server saw port %d, client bound %d", got.Port, local.Port)
	}
}

// A tiny request must not produce a larger reply, otherwise the service can be
// abused to amplify traffic towards a spoofed victim.
func TestUnpaddedAddressRequestIsIgnored(t *testing.T) {
	_, server := startRelay(t)
	c := dial(t, server)

	if _, err := c.Write([]byte{msgAddrRequest}); err != nil {
		t.Fatalf("write: %v", err)
	}
	_ = c.SetReadDeadline(time.Now().Add(300 * time.Millisecond))
	if n, err := c.Read(make([]byte, 64)); err == nil {
		t.Fatalf("expected no reply to an unpadded request, got %d bytes", n)
	}
}

func TestRelayForwardsBetweenSlots(t *testing.T) {
	relay, server := startRelay(t)
	key := NewRelayKey()
	relay.Register(key)

	host := dial(t, server)
	guest := dial(t, server)

	// Each peer must announce itself before the relay knows where to forward.
	if _, err := host.Write(EncodeRelayPacket(key, 0, []byte("hello-from-host"))); err != nil {
		t.Fatalf("host write: %v", err)
	}
	if _, err := guest.Write(EncodeRelayPacket(key, 1, []byte("hello-from-guest"))); err != nil {
		t.Fatalf("guest write: %v", err)
	}

	// The host is registered by now, so the guest's packet reaches it.
	buf := make([]byte, 1500)
	n, err := host.Read(buf)
	if err != nil {
		t.Fatalf("host read: %v", err)
	}
	if !bytes.Equal(buf[:n], []byte("hello-from-guest")) {
		t.Fatalf("host got %q", buf[:n])
	}

	// And traffic flows the other way too.
	if _, err := host.Write(EncodeRelayPacket(key, 0, []byte("pong"))); err != nil {
		t.Fatalf("host write 2: %v", err)
	}
	n, err = guest.Read(buf)
	if err != nil {
		t.Fatalf("guest read: %v", err)
	}
	if !bytes.Equal(buf[:n], []byte("pong")) {
		t.Fatalf("guest got %q", buf[:n])
	}
}

// The payload must arrive stripped of the relay header, so the game sees the
// relay as if it were the peer itself.
func TestRelayStripsHeader(t *testing.T) {
	relay, server := startRelay(t)
	key := NewRelayKey()
	relay.Register(key)

	host := dial(t, server)
	guest := dial(t, server)

	_, _ = host.Write(EncodeRelayPacket(key, 0, []byte("x")))
	payload := []byte{0xDE, 0xAD, 0xBE, 0xEF}
	_, _ = guest.Write(EncodeRelayPacket(key, 1, payload))

	buf := make([]byte, 1500)
	n, err := host.Read(buf)
	if err != nil {
		t.Fatalf("read: %v", err)
	}
	if !bytes.Equal(buf[:n], payload) {
		t.Fatalf("expected raw payload %v, got %v", payload, buf[:n])
	}
}

func TestRelayIgnoresUnknownKey(t *testing.T) {
	relay, server := startRelay(t)
	known := NewRelayKey()
	relay.Register(known)

	host := dial(t, server)
	attacker := dial(t, server)

	_, _ = host.Write(EncodeRelayPacket(known, 0, []byte("register-me")))

	// An unregistered key must not be routable, and must not even be answered:
	// a reply would let an attacker probe which sessions exist.
	_, _ = attacker.Write(EncodeRelayPacket(NewRelayKey(), 1, []byte("intrusion")))

	_ = host.SetReadDeadline(time.Now().Add(300 * time.Millisecond))
	if n, err := host.Read(make([]byte, 1500)); err == nil {
		t.Fatalf("packet with an unknown key was forwarded (%d bytes)", n)
	}
}

func TestRelayRejectsInvalidSlot(t *testing.T) {
	relay, server := startRelay(t)
	key := NewRelayKey()
	relay.Register(key)

	host := dial(t, server)
	_, _ = host.Write(EncodeRelayPacket(key, 0, []byte("hi")))

	bad := dial(t, server)
	_, _ = bad.Write(EncodeRelayPacket(key, 7, []byte("bad-slot")))

	_ = host.SetReadDeadline(time.Now().Add(300 * time.Millisecond))
	if n, err := host.Read(make([]byte, 1500)); err == nil {
		t.Fatalf("packet with slot 7 was forwarded (%d bytes)", n)
	}
}

func TestRelaySweepDropsIdleSessions(t *testing.T) {
	relay := NewRelay()
	key := NewRelayKey()
	relay.Register(key)

	relay.mu.Lock()
	relay.sessions[key].lastSeen = time.Now().Add(-2 * relayIdleTimeout)
	relay.mu.Unlock()

	relay.Sweep()
	if got := relay.SessionCount(); got != 0 {
		t.Fatalf("expected idle session to be swept, %d remain", got)
	}
}

func TestRelayEnforcesByteQuota(t *testing.T) {
	relay := NewRelay()
	key := NewRelayKey()
	relay.Register(key)

	src := &net.UDPAddr{IP: net.IPv4(10, 0, 0, 1), Port: 1111}
	peer := &net.UDPAddr{IP: net.IPv4(10, 0, 0, 2), Port: 2222}
	if _, err := relay.route(key, 1, peer, 1); err != nil {
		t.Fatalf("register peer: %v", err)
	}

	relay.mu.Lock()
	relay.sessions[key].bytes = relayMaxBytes
	relay.mu.Unlock()

	if _, err := relay.route(key, 0, src, 1); err == nil {
		t.Fatal("expected the quota to reject further traffic")
	}
}
