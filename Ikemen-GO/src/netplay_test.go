package main

import (
	"net"
	"testing"
	"time"
)

// TestConnectDirectThenRelayPrefersDirect proves the guest never touches the
// relay when the host is reachable directly.
func TestConnectDirectThenRelayPrefersDirect(t *testing.T) {
	ln, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		t.Fatalf("listen: %v", err)
	}
	defer ln.Close()
	port := ln.Addr().(*net.TCPAddr).Port

	go func() {
		c, err := ln.Accept()
		if err != nil {
			return
		}
		defer c.Close()
		netHandshake(c, true, 5*time.Second)
	}()

	// A relay plan with no TCP port: if the code fell back to it, the dial
	// would fail and IsConnected would never turn true.
	plan := &RelayPlan{Host: "127.0.0.1"}

	nc := NewNetConnection()
	defer nc.Close()
	if err := nc.ConnectDirectThenRelay("127.0.0.1", port, plan, 2*time.Second); err != nil {
		t.Fatalf("ConnectDirectThenRelay: %v", err)
	}

	deadline := time.Now().Add(5 * time.Second)
	for !nc.IsConnected() {
		if time.Now().After(deadline) {
			t.Fatal("direct connection never completed")
		}
		time.Sleep(10 * time.Millisecond)
	}
}

// TestConnectDirectThenRelayFallsBack proves a dead direct address still ends
// up connected, over the relay, instead of just giving up.
func TestConnectDirectThenRelayFallsBack(t *testing.T) {
	relayPort := startPairingRelay(t)
	relayPlan := &RelayPlan{Host: "127.0.0.1", TCPPort: relayPort, Slot: 0}

	// Nothing listens on this port, so the direct dial must fail fast.
	deadLn, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		t.Fatalf("listen: %v", err)
	}
	deadPort := deadLn.Addr().(*net.TCPAddr).Port
	deadLn.Close()

	// The "host" side of the relay, so the guest's fallback has someone to
	// shake hands with.
	hostConn, err := dialRelayStream(&RelayPlan{Host: "127.0.0.1", TCPPort: relayPort, Slot: 1}, 2*time.Second)
	if err != nil {
		t.Fatalf("host relay dial: %v", err)
	}
	defer hostConn.Close()
	go netHandshake(hostConn, true, 5*time.Second)

	nc := NewNetConnection()
	defer nc.Close()
	if err := nc.ConnectDirectThenRelay("127.0.0.1", deadPort, relayPlan, 300*time.Millisecond); err != nil {
		t.Fatalf("ConnectDirectThenRelay: %v", err)
	}

	deadline := time.Now().Add(5 * time.Second)
	for !nc.IsConnected() {
		if time.Now().After(deadline) {
			t.Fatal("relay fallback never completed")
		}
		time.Sleep(10 * time.Millisecond)
	}
}

// TestAcceptDirectThenRelayFallsBack proves the host side falls back too,
// when nobody dials in during the direct window.
func TestAcceptDirectThenRelayFallsBack(t *testing.T) {
	relayPort := startPairingRelay(t)
	relayPlan := &RelayPlan{Host: "127.0.0.1", TCPPort: relayPort, Slot: 0}

	guestConn, err := dialRelayStream(&RelayPlan{Host: "127.0.0.1", TCPPort: relayPort, Slot: 1}, 2*time.Second)
	if err != nil {
		t.Fatalf("guest relay dial: %v", err)
	}
	defer guestConn.Close()
	go netHandshake(guestConn, false, 5*time.Second)

	// A free port that nothing ever dials, so the accept window has to expire.
	probe, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		t.Fatalf("listen: %v", err)
	}
	port := probe.Addr().(*net.TCPAddr).Port
	probe.Close()

	nc := NewNetConnection()
	defer nc.Close()
	if err := nc.AcceptDirectThenRelay(port, relayPlan, 300*time.Millisecond); err != nil {
		t.Fatalf("AcceptDirectThenRelay: %v", err)
	}

	deadline := time.Now().Add(5 * time.Second)
	for !nc.IsConnected() {
		if time.Now().After(deadline) {
			t.Fatal("relay fallback never completed")
		}
		time.Sleep(10 * time.Millisecond)
	}
}
