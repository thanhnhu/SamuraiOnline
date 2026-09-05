package netpath

import (
	"bytes"
	"encoding/binary"
	"errors"
	"fmt"
	"log"
	"net"
	"sync"
	"time"

	ggpo "github.com/ikemen-engine/ggpo"
	"github.com/ikemen-engine/ggpo/transport"
)

// NAT traversal for the rollback transport.
//
// The socket used for the match has to be the same one that talked to the
// lobby's discovery service, because a NAT maps each source port separately:
// probing from a throwaway socket would reveal a mapping the game never uses.
// So the socket is opened when the player enters the lobby, kept warm with
// periodic discovery packets, and only handed to GGPO once a path is chosen.
//
// Order of preference:
//  1. direct   - peers already reachable (LAN, public IP, forwarded port)
//  2. punched  - both sides opened a mapping towards each other
//  3. relayed  - everything else, traffic bounces off the lobby relay
const (
	natPunchMagic    = "IKPUNCH1"
	natPunchProbe    = 0x00
	natPunchAck      = 0x01
	natPunchInterval = 50 * time.Millisecond
	PunchTimeout  = 4 * time.Second
	natKeepAlive     = 2 * time.Second
	natMaxPacket     = 2048

	relayMsgData   = 0x02
	RelayKeyLen    = 16
	relayHeaderLen = 1 + RelayKeyLen + 1

	// Magic for the TCP relay, distinct from the UDP framing above.
	RelayStreamMagic = "IKTCPRLY"

	addrMsgRequest  = 0x01
	addrMsgResponse = 0x81
	addrMinRequest  = 64
)

type NATMode string

const (
	NATDirect  NATMode = "direct"
	NATPunched NATMode = "punched"
	NATRelayed NATMode = "relayed"
)

// relayConn frames every datagram for the relay. Because the relay strips the
// header before forwarding, the peer receives untouched payloads and GGPO can
// treat the relay address as if it were the opponent.
type relayConn struct {
	inner     *net.UDPConn
	relayAddr *net.UDPAddr
	key       [RelayKeyLen]byte
	slot      byte
}

func (c *relayConn) ReadFrom(p []byte) (int, net.Addr, error) {
	buf := make([]byte, natMaxPacket)
	for {
		n, src, err := c.inner.ReadFromUDP(buf)
		if err != nil {
			return 0, nil, err
		}
		// Anything not coming from the relay is stale punch traffic.
		if !src.IP.Equal(c.relayAddr.IP) || src.Port != c.relayAddr.Port {
			continue
		}
		n = copy(p, buf[:n])
		return n, c.relayAddr, nil
	}
}

func (c *relayConn) WriteTo(p []byte, _ net.Addr) (int, error) {
	frame := make([]byte, 0, relayHeaderLen+len(p))
	frame = append(frame, relayMsgData)
	frame = append(frame, c.key[:]...)
	frame = append(frame, c.slot)
	frame = append(frame, p...)
	if _, err := c.inner.WriteToUDP(frame, c.relayAddr); err != nil {
		return 0, err
	}
	// Report the caller's length; the header is our business, not GGPO's.
	return len(p), nil
}

func (c *relayConn) Close() error                       { return c.inner.Close() }
func (c *relayConn) LocalAddr() net.Addr                { return c.inner.LocalAddr() }
func (c *relayConn) SetDeadline(t time.Time) error      { return c.inner.SetDeadline(t) }
func (c *relayConn) SetReadDeadline(t time.Time) error  { return c.inner.SetReadDeadline(t) }
func (c *relayConn) SetWriteDeadline(t time.Time) error { return c.inner.SetWriteDeadline(t) }

// NATSession owns the game socket for the whole lobby-to-match lifetime.
type NATSession struct {
	mu sync.Mutex

	conn      *net.UDPConn
	localPort int

	lobbyUDP *net.UDPAddr
	udpToken string

	observed  *net.UDPAddr
	keepalive chan struct{}
	keepDone  chan struct{}

	mode NATMode
	peer *net.UDPAddr
}

var natSession *NATSession

func Active() *NATSession {
	return natSession
}

// StartNATSession opens the match socket and begins announcing it to the lobby.
func StartNATSession(localPort int, lobbyUDPAddr, udpToken string) (*NATSession, error) {
	lobbyAddr, err := net.ResolveUDPAddr("udp", lobbyUDPAddr)
	if err != nil {
		return nil, fmt.Errorf("bad lobby UDP address: %w", err)
	}
	conn, err := net.ListenUDP("udp", &net.UDPAddr{Port: localPort})
	if err != nil {
		return nil, fmt.Errorf("cannot bind UDP port %d: %w", localPort, err)
	}

	s := &NATSession{
		conn:      conn,
		localPort: conn.LocalAddr().(*net.UDPAddr).Port,
		lobbyUDP:  lobbyAddr,
		udpToken:  udpToken,
		keepalive: make(chan struct{}),
		keepDone:  make(chan struct{}),
	}
	go s.runKeepAlive()

	StopNATSession()
	natSession = s
	return s, nil
}

func StopNATSession() {
	if natSession == nil {
		return
	}
	natSession.Close()
	natSession = nil
}

func (s *NATSession) Close() {
	s.stopKeepAlive()
	if s.conn != nil {
		_ = s.conn.Close()
	}
}

// runKeepAlive both discovers our mapped address and stops the NAT from
// dropping the mapping while the player sits in the lobby.
func (s *NATSession) runKeepAlive() {
	defer close(s.keepDone)

	// Read the stop channel once. stopKeepAlive clears the field before it
	// closes the channel, so re-reading it here would leave this loop
	// selecting on a nil channel and never seeing the close.
	s.mu.Lock()
	stop := s.keepalive
	s.mu.Unlock()
	if stop == nil {
		return
	}

	ticker := time.NewTicker(natKeepAlive)
	defer ticker.Stop()
	buf := make([]byte, natMaxPacket)

	s.sendAddrRequest()
	for {
		select {
		case <-stop:
			return
		case <-ticker.C:
			s.sendAddrRequest()
		default:
		}

		_ = s.conn.SetReadDeadline(time.Now().Add(100 * time.Millisecond))
		n, src, err := s.conn.ReadFromUDP(buf)
		if err != nil {
			continue
		}
		if n > 0 && buf[0] == addrMsgResponse && src.IP.Equal(s.lobbyUDP.IP) {
			if addr, err := decodeAddrResponse(buf[:n]); err == nil {
				s.mu.Lock()
				s.observed = addr
				s.mu.Unlock()
			}
		}
	}
}

func (s *NATSession) stopKeepAlive() {
	s.mu.Lock()
	ch := s.keepalive
	s.keepalive = nil
	s.mu.Unlock()
	if ch == nil {
		return
	}
	close(ch)
	<-s.keepDone
	_ = s.conn.SetReadDeadline(time.Time{})
}

func (s *NATSession) sendAddrRequest() {
	size := 2 + len(s.udpToken)
	if size < addrMinRequest {
		size = addrMinRequest
	}
	pkt := make([]byte, size)
	pkt[0] = addrMsgRequest
	pkt[1] = byte(len(s.udpToken))
	copy(pkt[2:], s.udpToken)
	_, _ = s.conn.WriteToUDP(pkt, s.lobbyUDP)
}

func (s *NATSession) Observed() string {
	s.mu.Lock()
	defer s.mu.Unlock()
	if s.observed == nil {
		return ""
	}
	return s.observed.String()
}

func (s *NATSession) Mode() NATMode {
	s.mu.Lock()
	defer s.mu.Unlock()
	return s.mode
}

func decodeAddrResponse(pkt []byte) (*net.UDPAddr, error) {
	if len(pkt) < 4 || pkt[0] != addrMsgResponse {
		return nil, errors.New("not an address response")
	}
	ipLen := int(pkt[1])
	if ipLen != net.IPv4len && ipLen != net.IPv6len {
		return nil, errors.New("bad address length")
	}
	if len(pkt) < 2+ipLen+2 {
		return nil, errors.New("truncated address response")
	}
	ip := make(net.IP, ipLen)
	copy(ip, pkt[2:2+ipLen])
	return &net.UDPAddr{
		IP:   ip,
		Port: int(binary.BigEndian.Uint16(pkt[2+ipLen:])),
	}, nil
}

func punchPacket(kind byte) []byte {
	return append([]byte(natPunchMagic), kind)
}

func isPunchPacket(b []byte) (byte, bool) {
	if len(b) != len(natPunchMagic)+1 || !bytes.HasPrefix(b, []byte(natPunchMagic)) {
		return 0, false
	}
	return b[len(natPunchMagic)], true
}

// punch alternates probes and acks until the peer proves it can reach us.
// Receiving an ack means our probe arrived, which is the direction that matters
// for opening our own NAT mapping.
func (s *NATSession) punch(peer *net.UDPAddr, timeout time.Duration) bool {
	deadline := time.Now().Add(timeout)
	buf := make([]byte, natMaxPacket)
	nextProbe := time.Now()

	for time.Now().Before(deadline) {
		if time.Now().After(nextProbe) {
			_, _ = s.conn.WriteToUDP(punchPacket(natPunchProbe), peer)
			nextProbe = time.Now().Add(natPunchInterval)
		}

		_ = s.conn.SetReadDeadline(time.Now().Add(natPunchInterval))
		n, src, err := s.conn.ReadFromUDP(buf)
		if err != nil {
			continue
		}
		if !src.IP.Equal(peer.IP) || src.Port != peer.Port {
			continue
		}
		kind, ok := isPunchPacket(buf[:n])
		if !ok {
			continue
		}
		if kind == natPunchProbe {
			_, _ = s.conn.WriteToUDP(punchPacket(natPunchAck), peer)
			continue
		}
		if kind == natPunchAck {
			// Drain any probes still in flight so GGPO never sees them.
			_ = s.conn.SetReadDeadline(time.Now().Add(150 * time.Millisecond))
			for {
				n, src, err := s.conn.ReadFromUDP(buf)
				if err != nil {
					break
				}
				if src.IP.Equal(peer.IP) && src.Port == peer.Port {
					if k, ok := isPunchPacket(buf[:n]); ok && k == natPunchProbe {
						_, _ = s.conn.WriteToUDP(punchPacket(natPunchAck), peer)
					}
				}
			}
			_ = s.conn.SetReadDeadline(time.Time{})
			return true
		}
	}
	_ = s.conn.SetReadDeadline(time.Time{})
	return false
}

// RelayPlan carries the relay credentials handed out by the lobby.
type RelayPlan struct {
	Host string
	Port int
	// TCPPort relays the match-setup stream. Zero means the lobby offers no
	// TCP fallback, so the host must be directly reachable.
	TCPPort int
	Key     [RelayKeyLen]byte
	Slot    int
}

// DialRelayStream opens the match-setup stream through the lobby relay. Hole
// punching only rescues GGPO's UDP traffic; Ikemen still negotiates the match
// over TCP with the host listening, which needs a forwarded port. Both peers
// dialling outbound to the relay removes that requirement.
func DialRelayStream(plan *RelayPlan, timeout time.Duration) (net.Conn, error) {
	if plan == nil || plan.TCPPort == 0 {
		return nil, errors.New("lobby offers no TCP relay")
	}
	addr := net.JoinHostPort(plan.Host, fmt.Sprint(plan.TCPPort))
	conn, err := net.DialTimeout("tcp", addr, timeout)
	if err != nil {
		return nil, fmt.Errorf("relay dial: %w", err)
	}

	header := make([]byte, 0, len(RelayStreamMagic)+RelayKeyLen+1)
	header = append(header, RelayStreamMagic...)
	header = append(header, plan.Key[:]...)
	header = append(header, byte(plan.Slot))

	if err := conn.SetWriteDeadline(time.Now().Add(timeout)); err != nil {
		conn.Close()
		return nil, err
	}
	if _, err := conn.Write(header); err != nil {
		conn.Close()
		return nil, fmt.Errorf("relay header: %w", err)
	}
	if err := conn.SetWriteDeadline(time.Time{}); err != nil {
		conn.Close()
		return nil, err
	}
	return conn, nil
}

// NATResult is the negotiated path, consumed by the rollback session when it
// builds the GGPO backend.
type NATResult struct {
	Conn      net.PacketConn
	Peer      *net.UDPAddr
	Mode      NATMode
	LocalPort int
}

var natResult *NATResult

func Established() *NATResult { return natResult }

func ClearNATResult() { natResult = nil }

// Remote swaps in the endpoint chosen by NAT traversal. With a relay this is
// the relay itself, which is exactly what GGPO should send to.
func Remote(remoteIp string, remotePort int) (string, int) {
	if r := Established(); r != nil && r.Peer != nil {
		return r.Peer.IP.String(), r.Peer.Port
	}
	return remoteIp, remotePort
}

// InitGGPOConnection hands GGPO our own socket when NAT traversal produced one,
// otherwise lets it bind its own as before.
func InitGGPOConnection(peer *ggpo.Peer, localPort int) {
	if r := Established(); r != nil {
		if err := peer.InitializeConnection(transport.NewUdpWithConn(peer, r.Conn, localPort)); err != nil {
			log.Printf("NAT: custom transport rejected, falling back: %v", err)
			_ = peer.InitializeConnection()
		}
		return
	}
	_ = peer.InitializeConnection()
}

// Establish picks a path to the peer and returns the connection GGPO should
// use, along with the address it must treat as the remote endpoint.
func (s *NATSession) Establish(peerUDP string, relay *RelayPlan) (*NATResult, error) {
	s.stopKeepAlive()

	if peerUDP != "" {
		peer, err := net.ResolveUDPAddr("udp", peerUDP)
		if err == nil {
			if s.punch(peer, PunchTimeout) {
				s.mu.Lock()
				s.mode, s.peer = NATPunched, peer
				s.mu.Unlock()
				log.Printf("NAT: direct path to %s", peer)
				natResult = &NATResult{Conn: s.conn, Peer: peer, Mode: NATPunched, LocalPort: s.localPort}
				return natResult, nil
			}
			log.Printf("NAT: could not punch through to %s", peer)
		}
	}

	if relay == nil {
		return nil, errors.New("hole punching failed and no relay was offered")
	}
	relayAddr, err := net.ResolveUDPAddr("udp", net.JoinHostPort(relay.Host, fmt.Sprint(relay.Port)))
	if err != nil {
		return nil, fmt.Errorf("bad relay address: %w", err)
	}

	rc := &relayConn{inner: s.conn, relayAddr: relayAddr, key: relay.Key, slot: byte(relay.Slot)}
	// The relay only learns where to forward once it has heard from us.
	if _, err := rc.WriteTo([]byte{}, relayAddr); err != nil {
		return nil, fmt.Errorf("relay registration failed: %w", err)
	}

	s.mu.Lock()
	s.mode, s.peer = NATRelayed, relayAddr
	s.mu.Unlock()
	log.Printf("NAT: falling back to relay %s", relayAddr)
	natResult = &NATResult{Conn: rc, Peer: relayAddr, Mode: NATRelayed, LocalPort: s.localPort}
	return natResult, nil
}
