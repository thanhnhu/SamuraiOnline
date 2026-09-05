package main

import (
	"crypto/rand"
	"encoding/binary"
	"errors"
	"log"
	"net"
	"sync"
	"time"
)

// UDP side of the lobby: address discovery (STUN-like) plus a relay that
// forwards packets between the two peers of a room when hole punching fails.
//
// Wire format, first byte is the message type:
//
//	0x01 [padding...]                      client -> server, address request
//	0x81 [len][ip bytes][port BE16]        server -> client, observed address
//	0x02 [16-byte key][slot][payload...]   client -> server, relay data
//	     payload only                      server -> peer, relay data
//
// Relayed payloads reach the peer stripped of the header, so from the game's
// point of view the relay simply *is* the remote address.
const (
	msgAddrRequest  byte = 0x01
	msgAddrResponse byte = 0x81
	msgRelayData    byte = 0x02

	relayKeyLen    = 16
	relayHeaderLen = 1 + relayKeyLen + 1
	relayMaxPacket = 1500

	// An address request must be at least as large as the reply it triggers,
	// otherwise the server becomes a traffic amplifier for spoofed sources.
	minAddrRequestLen = 64

	relayIdleTimeout = 60 * time.Second
	relayMaxLifetime = 2 * time.Hour
	// Bandwidth ceiling per session; a fighting game needs a tiny fraction of
	// this, so anything approaching it is abuse.
	relayMaxBytes int64 = 64 << 20
)

var (
	errRelayUnknownSession = errors.New("unknown relay session")
	errRelayQuota          = errors.New("relay session exceeded its quota")
)

type RelayKey [relayKeyLen]byte

type relaySession struct {
	slots    [2]*net.UDPAddr
	created  time.Time
	lastSeen time.Time
	bytes    int64
}

type Relay struct {
	mu       sync.Mutex
	sessions map[RelayKey]*relaySession
	conn     *net.UDPConn
	// OnAddrRequest receives the token carried by an address request together
	// with the address the server observed. Optional.
	OnAddrRequest func(udpToken string, addr *net.UDPAddr)
}

func NewRelay() *Relay {
	return &Relay{sessions: make(map[RelayKey]*relaySession)}
}

func NewRelayKey() RelayKey {
	var k RelayKey
	if _, err := rand.Read(k[:]); err != nil {
		panic("lobby: crypto/rand unavailable: " + err.Error())
	}
	return k
}

// Register makes a key routable. Calling it repeatedly for the same room is
// safe, since both peers allocate independently.
func (r *Relay) Register(key RelayKey) {
	r.mu.Lock()
	defer r.mu.Unlock()
	if _, ok := r.sessions[key]; ok {
		return
	}
	now := time.Now()
	r.sessions[key] = &relaySession{created: now, lastSeen: now}
}

// Known reports whether a key was ever allocated, so other transports can
// refuse to forward between peers who simply guessed one.
func (r *Relay) Known(key RelayKey) bool {
	r.mu.Lock()
	defer r.mu.Unlock()
	_, ok := r.sessions[key]
	return ok
}

func (r *Relay) Listen(addr string) (*net.UDPAddr, error) {
	udpAddr, err := net.ResolveUDPAddr("udp", addr)
	if err != nil {
		return nil, err
	}
	conn, err := net.ListenUDP("udp", udpAddr)
	if err != nil {
		return nil, err
	}
	r.conn = conn
	return conn.LocalAddr().(*net.UDPAddr), nil
}

func (r *Relay) Serve() {
	buf := make([]byte, relayMaxPacket)
	for {
		n, src, err := r.conn.ReadFromUDP(buf)
		if err != nil {
			// A closed socket is the normal shutdown path.
			if errors.Is(err, net.ErrClosed) {
				return
			}
			continue
		}
		r.handle(buf[:n], src)
	}
}

func (r *Relay) Close() error {
	if r.conn == nil {
		return nil
	}
	return r.conn.Close()
}

func (r *Relay) handle(pkt []byte, src *net.UDPAddr) {
	if len(pkt) == 0 {
		return
	}
	switch pkt[0] {
	case msgAddrRequest:
		if len(pkt) < minAddrRequestLen {
			return
		}
		if token, ok := decodeAddrToken(pkt); ok && r.OnAddrRequest != nil {
			r.OnAddrRequest(token, src)
		}
		if reply := encodeAddrResponse(src); reply != nil {
			_, _ = r.conn.WriteToUDP(reply, src)
		}
	case msgRelayData:
		r.forward(pkt, src)
	}
}

func encodeAddrResponse(addr *net.UDPAddr) []byte {
	ip := addr.IP.To4()
	if ip == nil {
		ip = addr.IP.To16()
	}
	if ip == nil {
		return nil
	}
	out := make([]byte, 0, 2+len(ip)+2)
	out = append(out, msgAddrResponse, byte(len(ip)))
	out = append(out, ip...)
	return binary.BigEndian.AppendUint16(out, uint16(addr.Port))
}

// DecodeAddrResponse parses a reply produced by encodeAddrResponse.
func DecodeAddrResponse(pkt []byte) (*net.UDPAddr, error) {
	if len(pkt) < 4 || pkt[0] != msgAddrResponse {
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
	port := binary.BigEndian.Uint16(pkt[2+ipLen:])
	return &net.UDPAddr{IP: ip, Port: int(port)}, nil
}

func (r *Relay) forward(pkt []byte, src *net.UDPAddr) {
	if len(pkt) <= relayHeaderLen {
		return
	}
	var key RelayKey
	copy(key[:], pkt[1:1+relayKeyLen])
	slot := pkt[1+relayKeyLen]
	if slot > 1 {
		return
	}
	payload := pkt[relayHeaderLen:]

	dst, err := r.route(key, int(slot), src, len(payload))
	if err != nil || dst == nil {
		// Silence on failure: replying would confirm which keys exist.
		return
	}
	_, _ = r.conn.WriteToUDP(payload, dst)
}

// route records the sender's current address for its slot and returns the peer
// address to forward to, if the peer has checked in yet.
func (r *Relay) route(key RelayKey, slot int, src *net.UDPAddr, payloadLen int) (*net.UDPAddr, error) {
	r.mu.Lock()
	defer r.mu.Unlock()

	s, ok := r.sessions[key]
	if !ok {
		return nil, errRelayUnknownSession
	}
	if s.bytes+int64(payloadLen) > relayMaxBytes {
		return nil, errRelayQuota
	}
	s.bytes += int64(payloadLen)
	s.lastSeen = time.Now()

	// Learn the address on first contact and follow NAT rebinding. Possession
	// of the secret key is what authorises this, so it must stay secret.
	if s.slots[slot] == nil || s.slots[slot].String() != src.String() {
		cp := *src
		s.slots[slot] = &cp
	}
	return s.slots[1-slot], nil
}

func (r *Relay) Sweep() {
	r.mu.Lock()
	defer r.mu.Unlock()
	now := time.Now()
	for key, s := range r.sessions {
		if now.Sub(s.lastSeen) > relayIdleTimeout || now.Sub(s.created) > relayMaxLifetime {
			delete(r.sessions, key)
		}
	}
}

func (r *Relay) SessionCount() int {
	r.mu.Lock()
	defer r.mu.Unlock()
	return len(r.sessions)
}

// EncodeRelayPacket builds a client-side relay frame. Exported for tests and
// for the engine-side transport shim.
func EncodeRelayPacket(key RelayKey, slot int, payload []byte) []byte {
	out := make([]byte, 0, relayHeaderLen+len(payload))
	out = append(out, msgRelayData)
	out = append(out, key[:]...)
	out = append(out, byte(slot))
	return append(out, payload...)
}

// NewAddrRequest builds a padded address request carrying the caller's UDP
// token. Padding keeps the request at least as large as the reply.
func NewAddrRequest(udpToken string) []byte {
	size := 2 + len(udpToken)
	if size < minAddrRequestLen {
		size = minAddrRequestLen
	}
	pkt := make([]byte, size)
	pkt[0] = msgAddrRequest
	pkt[1] = byte(len(udpToken))
	copy(pkt[2:], udpToken)
	return pkt
}

func decodeAddrToken(pkt []byte) (string, bool) {
	if len(pkt) < 2 {
		return "", false
	}
	n := int(pkt[1])
	if n == 0 || len(pkt) < 2+n {
		return "", false
	}
	return string(pkt[2 : 2+n]), true
}

func logRelayListening(addr *net.UDPAddr) {
	log.Printf("relay/STUN listening on udp %s", addr)
}
