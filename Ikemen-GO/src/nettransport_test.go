package main

import (
	"io"
	"net"
	"testing"
	"time"
)

// The header written by dialRelayStream has to match what the lobby's TCP
// relay parses. Both sides hardcode the same literal here, so a change to one
// end shows up as a failure rather than as a match that never connects.
const testRelayMagic = "IKTCPRLY"

// startPairingRelay implements the lobby's splice behaviour: read the header,
// pair the two slots of a key, then copy bytes both ways.
func startPairingRelay(t *testing.T) int {
	t.Helper()
	ln, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		t.Fatalf("listen: %v", err)
	}
	t.Cleanup(func() { ln.Close() })

	headers := make(chan net.Conn, 2)
	go func() {
		for {
			c, err := ln.Accept()
			if err != nil {
				return
			}
			go func(c net.Conn) {
				buf := make([]byte, len(testRelayMagic)+relayKeyLen+1)
				if _, err := io.ReadFull(c, buf); err != nil {
					c.Close()
					return
				}
				if string(buf[:len(testRelayMagic)]) != testRelayMagic {
					c.Close()
					return
				}
				headers <- c
			}(c)
		}
	}()

	go func() {
		a, ok := <-headers
		if !ok {
			return
		}
		b, ok := <-headers
		if !ok {
			a.Close()
			return
		}
		go func() { io.Copy(a, b); a.Close() }()
		go func() { io.Copy(b, a); b.Close() }()
	}()

	return ln.Addr().(*net.TCPAddr).Port
}

func TestRelayStreamHandshakeCompletesBothRoles(t *testing.T) {
	port := startPairingRelay(t)
	plan := &RelayPlan{Host: "127.0.0.1", TCPPort: port}

	hostConn, err := dialRelayStream(&RelayPlan{Host: plan.Host, TCPPort: port, Slot: 0}, 2*time.Second)
	if err != nil {
		t.Fatalf("host dial: %v", err)
	}
	defer hostConn.Close()

	guestConn, err := dialRelayStream(&RelayPlan{Host: plan.Host, TCPPort: port, Slot: 1}, 2*time.Second)
	if err != nil {
		t.Fatalf("guest dial: %v", err)
	}
	defer guestConn.Close()

	errs := make(chan error, 2)
	go func() { errs <- netHandshake(hostConn, true, 5*time.Second) }()
	go func() { errs <- netHandshake(guestConn, false, 5*time.Second) }()

	for i := 0; i < 2; i++ {
		select {
		case err := <-errs:
			if err != nil {
				t.Fatalf("handshake: %v", err)
			}
		case <-time.After(10 * time.Second):
			t.Fatal("handshake deadlocked")
		}
	}

	// The stream must stay usable for the setup traffic that follows.
	if _, err := hostConn.Write([]byte{0x00, 0xC7, 0xFF}); err != nil {
		t.Fatalf("post-handshake write: %v", err)
	}
	got := make([]byte, 3)
	guestConn.SetReadDeadline(time.Now().Add(2 * time.Second))
	if _, err := io.ReadFull(guestConn, got); err != nil {
		t.Fatalf("post-handshake read: %v", err)
	}
	if got[0] != 0x00 || got[1] != 0xC7 || got[2] != 0xFF {
		t.Fatalf("payload corrupted: %x", got)
	}
}

func TestRelayStreamRejectsMismatchedToken(t *testing.T) {
	port := startPairingRelay(t)

	hostConn, err := dialRelayStream(&RelayPlan{Host: "127.0.0.1", TCPPort: port, Slot: 0}, 2*time.Second)
	if err != nil {
		t.Fatalf("host dial: %v", err)
	}
	defer hostConn.Close()

	impostor, err := dialRelayStream(&RelayPlan{Host: "127.0.0.1", TCPPort: port, Slot: 1}, 2*time.Second)
	if err != nil {
		t.Fatalf("impostor dial: %v", err)
	}
	defer impostor.Close()

	go func() {
		buf := make([]byte, len(netHandshakeToken))
		io.ReadFull(impostor, buf)
		impostor.Write([]byte("NOTIKEMN"))
	}()

	if err := netHandshake(hostConn, true, 5*time.Second); err == nil {
		t.Fatal("handshake accepted a wrong token")
	}
}

func TestDialRelayStreamRefusesPlanWithoutTCPPort(t *testing.T) {
	if _, err := dialRelayStream(&RelayPlan{Host: "127.0.0.1"}, time.Second); err == nil {
		t.Fatal("dialled a relay that offers no TCP port")
	}
	if _, err := dialRelayStream(nil, time.Second); err == nil {
		t.Fatal("dialled a nil plan")
	}
}
