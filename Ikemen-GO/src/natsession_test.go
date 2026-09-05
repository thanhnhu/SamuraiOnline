package main

import (
	"net"
	"testing"
	"time"
)

// Closing a NAT session must not hang. Establish() calls stopKeepAlive() before
// it does anything else, so a stall here freezes every match at "NEGOTIATING
// CONNECTION" with no error to show for it.
//
// The bug this guards against was a lost stop signal: runKeepAlive read the
// stop channel out of the struct on every pass, while stopKeepAlive nil'd the
// field before closing it, so the loop could end up selecting on a nil channel
// and never see the close. Repeated because it is a race.
func TestNATSessionCloseDoesNotHang(t *testing.T) {
	// A socket nobody answers: the session only needs somewhere to aim.
	sink, err := net.ListenUDP("udp", &net.UDPAddr{IP: net.IPv4(127, 0, 0, 1)})
	if err != nil {
		t.Fatalf("sink: %v", err)
	}
	defer sink.Close()
	lobbyAddr := sink.LocalAddr().String()

	for i := 0; i < 20; i++ {
		s, err := StartNATSession(0, lobbyAddr, "0123456789abcdef")
		if err != nil {
			t.Fatalf("iteration %d: start: %v", i, err)
		}

		// Let the keep-alive loop get past its first pass, so the stop signal
		// races against a loop that is already running.
		time.Sleep(20 * time.Millisecond)

		closed := make(chan struct{})
		go func() {
			s.Close()
			close(closed)
		}()

		select {
		case <-closed:
		case <-time.After(3 * time.Second):
			t.Fatalf("iteration %d: Close() hung; the keep-alive loop never saw the stop signal", i)
		}
		StopNATSession()
	}
}

// stopKeepAlive is reached from both Close() and Establish(), so calling it
// twice must be safe rather than closing a closed channel.
func TestNATSessionCloseIsIdempotent(t *testing.T) {
	sink, err := net.ListenUDP("udp", &net.UDPAddr{IP: net.IPv4(127, 0, 0, 1)})
	if err != nil {
		t.Fatalf("sink: %v", err)
	}
	defer sink.Close()

	s, err := StartNATSession(0, sink.LocalAddr().String(), "0123456789abcdef")
	if err != nil {
		t.Fatalf("start: %v", err)
	}
	time.Sleep(20 * time.Millisecond)

	done := make(chan struct{})
	go func() {
		s.Close()
		s.Close()
		close(done)
	}()
	select {
	case <-done:
	case <-time.After(3 * time.Second):
		t.Fatal("second Close() hung")
	}
	StopNATSession()
}

// Establish() must always reach a decision. With no reachable peer and no relay
// on offer it has to fail, but it must fail rather than block: the Lua menu
// waits on lobbyNatMode() forever otherwise.
func TestEstablishReturnsWithoutARelay(t *testing.T) {
	sink, err := net.ListenUDP("udp", &net.UDPAddr{IP: net.IPv4(127, 0, 0, 1)})
	if err != nil {
		t.Fatalf("sink: %v", err)
	}
	defer sink.Close()

	s, err := StartNATSession(0, sink.LocalAddr().String(), "0123456789abcdef")
	if err != nil {
		t.Fatalf("start: %v", err)
	}
	t.Cleanup(func() { StopNATSession(); ClearNATResult() })
	time.Sleep(20 * time.Millisecond)

	type result struct {
		res *NATResult
		err error
	}
	ch := make(chan result, 1)
	go func() {
		// 203.0.113.0/24 is reserved for documentation, so nothing answers.
		r, err := s.Establish("203.0.113.7:40000", nil)
		ch <- result{r, err}
	}()

	select {
	case got := <-ch:
		if got.err == nil {
			t.Fatal("Establish claimed success with no peer and no relay")
		}
	case <-time.After(natPunchTimeout + 10*time.Second):
		t.Fatal("Establish never returned; the lobby menu would hang forever")
	}
}
