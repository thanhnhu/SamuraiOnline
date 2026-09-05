package main

import (
	"encoding/binary"
	"io"
	"net"
	"testing"
	"time"
)

func startTCPRelay(t *testing.T, known func(RelayKey) bool) (*TCPRelay, string) {
	t.Helper()
	tr := NewTCPRelay(known)
	addr, err := tr.Listen("127.0.0.1:0")
	if err != nil {
		t.Fatalf("listen: %v", err)
	}
	go tr.Serve()
	t.Cleanup(func() { tr.Close() })
	return tr, addr.String()
}

func tcpRelayHeader(key RelayKey, slot byte) []byte {
	b := make([]byte, 0, tcpRelayHeaderLen)
	b = append(b, tcpRelayMagic...)
	b = append(b, key[:]...)
	b = append(b, slot)
	return b
}

func dialRelay(t *testing.T, addr string, key RelayKey, slot byte) net.Conn {
	t.Helper()
	c, err := net.DialTimeout("tcp", addr, 2*time.Second)
	if err != nil {
		t.Fatalf("dial: %v", err)
	}
	if _, err := c.Write(tcpRelayHeader(key, slot)); err != nil {
		t.Fatalf("write header: %v", err)
	}
	t.Cleanup(func() { c.Close() })
	return c
}

func TestTCPRelayForwardsBothDirections(t *testing.T) {
	key := NewRelayKey()
	_, addr := startTCPRelay(t, func(k RelayKey) bool { return k == key })

	a := dialRelay(t, addr, key, 0)
	b := dialRelay(t, addr, key, 1)

	// Ikemen's own handshake is the first thing that has to survive the relay.
	if _, err := a.Write([]byte("IKEMENGO")); err != nil {
		t.Fatalf("a write: %v", err)
	}
	got := make([]byte, 8)
	b.SetReadDeadline(time.Now().Add(2 * time.Second))
	if _, err := io.ReadFull(b, got); err != nil {
		t.Fatalf("b read: %v", err)
	}
	if string(got) != "IKEMENGO" {
		t.Fatalf("b got %q", got)
	}

	if _, err := b.Write([]byte("IKEMENGO")); err != nil {
		t.Fatalf("b write: %v", err)
	}
	a.SetReadDeadline(time.Now().Add(2 * time.Second))
	if _, err := io.ReadFull(a, got); err != nil {
		t.Fatalf("a read: %v", err)
	}
	if string(got) != "IKEMENGO" {
		t.Fatalf("a got %q", got)
	}
}

func TestTCPRelayCarriesBinaryFrames(t *testing.T) {
	key := NewRelayKey()
	_, addr := startTCPRelay(t, func(k RelayKey) bool { return k == key })
	a := dialRelay(t, addr, key, 0)
	b := dialRelay(t, addr, key, 1)

	// Netplay sends length-prefixed payloads; zero bytes must pass untouched.
	payload := []byte{0x00, 0xC7, 0x7C, 0x00, 0xFF}
	frame := make([]byte, 4+len(payload))
	binary.LittleEndian.PutUint32(frame[:4], uint32(len(payload)))
	copy(frame[4:], payload)

	if _, err := a.Write(frame); err != nil {
		t.Fatalf("write: %v", err)
	}
	got := make([]byte, len(frame))
	b.SetReadDeadline(time.Now().Add(2 * time.Second))
	if _, err := io.ReadFull(b, got); err != nil {
		t.Fatalf("read: %v", err)
	}
	if string(got) != string(frame) {
		t.Fatalf("frame corrupted: %x", got)
	}
}

func TestTCPRelayRejectsUnknownKey(t *testing.T) {
	allowed := NewRelayKey()
	_, addr := startTCPRelay(t, func(k RelayKey) bool { return k == allowed })

	stranger := NewRelayKey()
	c, err := net.DialTimeout("tcp", addr, 2*time.Second)
	if err != nil {
		t.Fatalf("dial: %v", err)
	}
	defer c.Close()
	if _, err := c.Write(tcpRelayHeader(stranger, 0)); err != nil {
		t.Fatalf("write: %v", err)
	}

	c.SetReadDeadline(time.Now().Add(2 * time.Second))
	if _, err := c.Read(make([]byte, 1)); err == nil {
		t.Fatal("relay accepted an unallocated key")
	}
}

func TestTCPRelayRejectsBadMagic(t *testing.T) {
	key := NewRelayKey()
	_, addr := startTCPRelay(t, func(k RelayKey) bool { return k == key })

	c, err := net.DialTimeout("tcp", addr, 2*time.Second)
	if err != nil {
		t.Fatalf("dial: %v", err)
	}
	defer c.Close()
	bad := tcpRelayHeader(key, 0)
	copy(bad, "NOTRELAY")
	if _, err := c.Write(bad); err != nil {
		t.Fatalf("write: %v", err)
	}

	c.SetReadDeadline(time.Now().Add(2 * time.Second))
	if _, err := c.Read(make([]byte, 1)); err == nil {
		t.Fatal("relay accepted a bad magic")
	}
}

func TestTCPRelayRejectsDuplicateSlot(t *testing.T) {
	key := NewRelayKey()
	_, addr := startTCPRelay(t, func(k RelayKey) bool { return k == key })

	first := dialRelay(t, addr, key, 0)
	second := dialRelay(t, addr, key, 0)

	second.SetReadDeadline(time.Now().Add(2 * time.Second))
	if _, err := second.Read(make([]byte, 1)); err == nil {
		t.Fatal("relay accepted two peers in the same slot")
	}

	// The peer that arrived first must still be usable.
	third := dialRelay(t, addr, key, 1)
	if _, err := first.Write([]byte("ok")); err != nil {
		t.Fatalf("first write: %v", err)
	}
	got := make([]byte, 2)
	third.SetReadDeadline(time.Now().Add(2 * time.Second))
	if _, err := io.ReadFull(third, got); err != nil {
		t.Fatalf("third read: %v", err)
	}
	if string(got) != "ok" {
		t.Fatalf("third got %q", got)
	}
}

func TestTCPRelayClosesPeerWhenOneSideHangsUp(t *testing.T) {
	key := NewRelayKey()
	_, addr := startTCPRelay(t, func(k RelayKey) bool { return k == key })

	a := dialRelay(t, addr, key, 0)
	b := dialRelay(t, addr, key, 1)
	if _, err := a.Write([]byte("x")); err != nil {
		t.Fatalf("write: %v", err)
	}
	b.SetReadDeadline(time.Now().Add(2 * time.Second))
	if _, err := io.ReadFull(b, make([]byte, 1)); err != nil {
		t.Fatalf("read: %v", err)
	}

	a.Close()
	b.SetReadDeadline(time.Now().Add(2 * time.Second))
	if _, err := b.Read(make([]byte, 1)); err == nil {
		t.Fatal("peer stayed open after the other side hung up")
	}
}
