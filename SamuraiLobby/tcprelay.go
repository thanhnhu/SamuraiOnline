package main

import (
	"errors"
	"io"
	"net"
	"sync"
	"time"
)

// TCP side of the lobby. Hole punching only rescues the UDP rollback traffic;
// Ikemen still sets up a match over a plain TCP connection where the host
// listens and the guest dials, which needs a forwarded port. This relay gives
// both peers something they can dial outbound instead.
//
// A client opens a connection and sends a fixed 25-byte header:
//
//	"IKTCPRLY" [16-byte relay key][slot]
//
// The key is the one already handed out by /api/relay/allocate, so no extra
// credential is introduced. When both slots of a key are present the server
// splices the two streams; from the game's point of view the relay simply is
// the peer.
const (
	tcpRelayMagic     = "IKTCPRLY"
	tcpRelayHeaderLen = len(tcpRelayMagic) + relayKeyLen + 1

	// Reading the header is the only unauthenticated work the server does, so
	// it gets a short leash to keep half-open connections from piling up.
	tcpRelayHeaderTimeout = 10 * time.Second
	// How long the first peer waits for the second one to show up.
	tcpRelayPairTimeout = 90 * time.Second
	tcpRelayIdleTimeout = 60 * time.Second
	tcpRelayMaxLifetime = 2 * time.Hour
	tcpRelayMaxBytes    = relayMaxBytes
)

var (
	errTCPRelayBadHeader = errors.New("malformed relay header")
	errTCPRelayUnknown   = errors.New("unknown relay session")
	errTCPRelaySlotTaken = errors.New("relay slot already occupied")
	errTCPRelayTimeout   = errors.New("timed out waiting for the peer")
)

type tcpWaiter struct {
	conn net.Conn
	slot byte
	peer chan net.Conn
}

type TCPRelay struct {
	mu      sync.Mutex
	waiting map[RelayKey]*tcpWaiter
	ln      *net.TCPListener

	// Known reports whether a key was ever allocated. Without it the relay
	// would forward between any two strangers who agree on a random key.
	Known func(RelayKey) bool
}

func NewTCPRelay(known func(RelayKey) bool) *TCPRelay {
	return &TCPRelay{waiting: make(map[RelayKey]*tcpWaiter), Known: known}
}

func (t *TCPRelay) Listen(addr string) (*net.TCPAddr, error) {
	tcpAddr, err := net.ResolveTCPAddr("tcp", addr)
	if err != nil {
		return nil, err
	}
	ln, err := net.ListenTCP("tcp", tcpAddr)
	if err != nil {
		return nil, err
	}
	t.ln = ln
	return ln.Addr().(*net.TCPAddr), nil
}

func (t *TCPRelay) Serve() {
	for {
		conn, err := t.ln.AcceptTCP()
		if err != nil {
			if errors.Is(err, net.ErrClosed) {
				return
			}
			continue
		}
		go t.handle(conn)
	}
}

func (t *TCPRelay) Close() error {
	if t.ln == nil {
		return nil
	}
	err := t.ln.Close()

	t.mu.Lock()
	for k, w := range t.waiting {
		w.conn.Close()
		close(w.peer)
		delete(t.waiting, k)
	}
	t.mu.Unlock()
	return err
}

func (t *TCPRelay) handle(conn net.Conn) {
	key, slot, err := readTCPRelayHeader(conn)
	if err != nil {
		conn.Close()
		return
	}
	if t.Known != nil && !t.Known(key) {
		conn.Close()
		return
	}

	peer, err := t.pair(key, slot, conn)
	if err != nil {
		conn.Close()
		return
	}
	splice(conn, peer)
}

func readTCPRelayHeader(conn net.Conn) (RelayKey, byte, error) {
	var key RelayKey
	if err := conn.SetReadDeadline(time.Now().Add(tcpRelayHeaderTimeout)); err != nil {
		return key, 0, err
	}
	buf := make([]byte, tcpRelayHeaderLen)
	if _, err := io.ReadFull(conn, buf); err != nil {
		return key, 0, err
	}
	if err := conn.SetReadDeadline(time.Time{}); err != nil {
		return key, 0, err
	}
	if string(buf[:len(tcpRelayMagic)]) != tcpRelayMagic {
		return key, 0, errTCPRelayBadHeader
	}
	copy(key[:], buf[len(tcpRelayMagic):len(tcpRelayMagic)+relayKeyLen])
	slot := buf[tcpRelayHeaderLen-1]
	if slot > 1 {
		return key, 0, errTCPRelayBadHeader
	}
	return key, slot, nil
}

// pair blocks until the other slot of the same key connects.
func (t *TCPRelay) pair(key RelayKey, slot byte, conn net.Conn) (net.Conn, error) {
	t.mu.Lock()
	if w, ok := t.waiting[key]; ok {
		if w.slot == slot {
			t.mu.Unlock()
			return nil, errTCPRelaySlotTaken
		}
		delete(t.waiting, key)
		t.mu.Unlock()
		w.peer <- conn
		return w.conn, nil
	}

	self := &tcpWaiter{conn: conn, slot: slot, peer: make(chan net.Conn, 1)}
	t.waiting[key] = self
	t.mu.Unlock()

	select {
	case peer, ok := <-self.peer:
		if !ok {
			return nil, errTCPRelayUnknown
		}
		return peer, nil
	case <-time.After(tcpRelayPairTimeout):
		t.mu.Lock()
		// The peer may have claimed us between the timeout firing and this lock.
		if cur, ok := t.waiting[key]; ok && cur == self {
			delete(t.waiting, key)
			t.mu.Unlock()
			return nil, errTCPRelayTimeout
		}
		t.mu.Unlock()
		if peer, ok := <-self.peer; ok {
			return peer, nil
		}
		return nil, errTCPRelayUnknown
	}
}

// splice copies in both directions until either side stops or a limit trips.
func splice(a, b net.Conn) {
	defer a.Close()
	defer b.Close()

	deadline := time.Now().Add(tcpRelayMaxLifetime)
	done := make(chan struct{}, 2)
	go func() { copyCapped(a, b, deadline); done <- struct{}{} }()
	go func() { copyCapped(b, a, deadline); done <- struct{}{} }()
	<-done
}

func copyCapped(dst, src net.Conn, hard time.Time) {
	buf := make([]byte, 32*1024)
	var total int64
	for {
		deadline := time.Now().Add(tcpRelayIdleTimeout)
		if deadline.After(hard) {
			deadline = hard
		}
		if err := src.SetReadDeadline(deadline); err != nil {
			return
		}
		n, err := src.Read(buf)
		if n > 0 {
			total += int64(n)
			if total > tcpRelayMaxBytes {
				return
			}
			if _, werr := dst.Write(buf[:n]); werr != nil {
				return
			}
		}
		if err != nil {
			return
		}
	}
}
