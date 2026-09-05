package main

import (
	"crypto/rand"
	"encoding/hex"
	"errors"
	"net"
	"sort"
	"strings"
	"sync"
	"time"
)

// Tunables. A player that stops polling for longer than sessionTTL is dropped
// from its room, which is what lets abandoned rooms disappear on their own.
const (
	sessionTTL  = 20 * time.Second
	maxPlayers  = 500
	maxRooms    = 200
	maxNameRune = 24
	// maxSpectators matches ggpo.MaxSpectators; the host adds one UDP endpoint
	// per spectator, so the engine cannot accept more than this anyway.
	maxSpectators = 32
	// maxManifestBytes bounds the match description the host publishes for
	// spectators. It is echoed to every watcher, so it must stay small.
	maxManifestBytes = 2 << 10
)

var (
	errNotFound     = errors.New("not found")
	errRoomFull     = errors.New("room is full")
	errCapacity     = errors.New("server is at capacity")
	errInRoom       = errors.New("already in a room")
	errNotInRoom    = errors.New("not in a room")
	errNotHost      = errors.New("only the host may do that")
	errManifestSize = errors.New("match manifest is too large")
)

type RoomState string

const (
	StateWaiting RoomState = "waiting"
	StateReady   RoomState = "ready"
	StatePlaying RoomState = "playing"
)

type Player struct {
	ID   string
	Name string
	// token is the session secret; it is never included in any response body
	// other than the one that creates the session.
	token string
	// udpToken authenticates the address-discovery packet. It is deliberately
	// separate from token because it travels over plain UDP, so stealing it
	// must not hand over the HTTP session too.
	udpToken string
	// IP is always taken from the TCP peer address, never from the request
	// body, so a client cannot make the server advertise a third party.
	IP   string
	Port int
	// UDPAddr is what the server observed on the UDP socket, which is the
	// address a peer must aim at to punch through NAT.
	UDPAddr  string
	RoomID   string
	LastSeen time.Time
}

type Room struct {
	ID        string
	Name      string
	HostID    string
	GuestID   string
	State     RoomState
	Rollback  bool
	CreatedAt time.Time
	// SpectatorIDs are watchers, in join order. They never occupy a player
	// seat, so a room stays joinable as a player while people watch.
	SpectatorIDs []string
	// manifest is the host's description of the match (characters, stage,
	// seed). Spectators need it to set up the same fight locally before the
	// first GGPO input arrives.
	manifest []byte
	// relayKey is allocated lazily, the first time either peer falls back to
	// the relay, so rooms that punch through never consume one.
	relayKey RelayKey
	relaySet bool
}

func (r *Room) hasSpectator(id string) bool {
	for _, sid := range r.SpectatorIDs {
		if sid == id {
			return true
		}
	}
	return false
}

func (r *Room) dropSpectator(id string) {
	for i, sid := range r.SpectatorIDs {
		if sid == id {
			r.SpectatorIDs = append(r.SpectatorIDs[:i], r.SpectatorIDs[i+1:]...)
			return
		}
	}
}

func (r *Room) occupants() int {
	n := 0
	if r.HostID != "" {
		n++
	}
	if r.GuestID != "" {
		n++
	}
	return n
}

// RoomView is the lock-free snapshot handed to HTTP handlers.
type RoomView struct {
	ID         string    `json:"id"`
	Name       string    `json:"name"`
	HostName   string    `json:"hostName"`
	Players    int       `json:"players"`
	Capacity   int       `json:"capacity"`
	State      RoomState `json:"state"`
	Rollback   bool      `json:"rollback"`
	Spectators int       `json:"spectators"`
}

type Store struct {
	mu        sync.Mutex
	players   map[string]*Player
	sessions  map[string]*Player
	udpTokens map[string]*Player
	rooms     map[string]*Room
}

func NewStore() *Store {
	return &Store{
		players:   make(map[string]*Player),
		sessions:  make(map[string]*Player),
		udpTokens: make(map[string]*Player),
		rooms:     make(map[string]*Room),
	}
}

func newID(n int) string {
	b := make([]byte, n)
	if _, err := rand.Read(b); err != nil {
		// A failing CSPRNG means we cannot issue safe tokens at all.
		panic("lobby: crypto/rand unavailable: " + err.Error())
	}
	return hex.EncodeToString(b)
}

func sanitizeName(s string, fallback string) string {
	s = strings.TrimSpace(s)
	var b strings.Builder
	n := 0
	for _, r := range s {
		if r < 0x20 || r == 0x7f {
			continue
		}
		b.WriteRune(r)
		n++
		if n >= maxNameRune {
			break
		}
	}
	if b.Len() == 0 {
		return fallback
	}
	return b.String()
}

func (s *Store) CreateSession(name, ip string, port int) (id, token, udpToken string, err error) {
	s.mu.Lock()
	defer s.mu.Unlock()

	if len(s.players) >= maxPlayers {
		return "", "", "", errCapacity
	}
	p := &Player{
		ID:       newID(8),
		Name:     sanitizeName(name, "Player"),
		token:    newID(32),
		udpToken: newID(16),
		IP:       ip,
		Port:     port,
		LastSeen: time.Now(),
	}
	s.players[p.ID] = p
	s.sessions[p.token] = p
	s.udpTokens[p.udpToken] = p
	return p.ID, p.token, p.udpToken, nil
}

// RecordUDPAddr stores the address the UDP service observed for a player. It is
// driven by the address-discovery packet, so the value is always what the
// server actually saw rather than anything the client claimed.
func (s *Store) RecordUDPAddr(udpToken string, addr *net.UDPAddr) {
	if addr == nil {
		return
	}
	s.mu.Lock()
	defer s.mu.Unlock()
	if p, ok := s.udpTokens[udpToken]; ok {
		p.UDPAddr = addr.String()
		p.LastSeen = time.Now()
	}
}

// touch refreshes the session clock. Caller must hold the lock.
func (s *Store) lookup(token string) (*Player, bool) {
	p, ok := s.sessions[token]
	if !ok {
		return nil, false
	}
	p.LastSeen = time.Now()
	return p, true
}

func (s *Store) CreateRoom(token, roomName string, rollback bool) (RoomView, error) {
	s.mu.Lock()
	defer s.mu.Unlock()

	p, ok := s.lookup(token)
	if !ok {
		return RoomView{}, errNotFound
	}
	if p.RoomID != "" {
		return RoomView{}, errInRoom
	}
	if len(s.rooms) >= maxRooms {
		return RoomView{}, errCapacity
	}

	r := &Room{
		ID:        newID(6),
		Name:      sanitizeName(roomName, p.Name+"'s room"),
		HostID:    p.ID,
		State:     StateWaiting,
		Rollback:  rollback,
		CreatedAt: time.Now(),
	}
	s.rooms[r.ID] = r
	p.RoomID = r.ID
	return s.viewOf(r), nil
}

func (s *Store) JoinRoom(token, roomID string, asSpectator bool) (RoomView, error) {
	s.mu.Lock()
	defer s.mu.Unlock()

	p, ok := s.lookup(token)
	if !ok {
		return RoomView{}, errNotFound
	}
	if p.RoomID != "" {
		return RoomView{}, errInRoom
	}
	r, ok := s.rooms[roomID]
	if !ok {
		return RoomView{}, errNotFound
	}
	if r.HostID == "" {
		return RoomView{}, errRoomFull
	}
	if asSpectator {
		if len(r.SpectatorIDs) >= maxSpectators {
			return RoomView{}, errRoomFull
		}
		r.SpectatorIDs = append(r.SpectatorIDs, p.ID)
		p.RoomID = r.ID
		return s.viewOf(r), nil
	}
	if r.GuestID != "" {
		return RoomView{}, errRoomFull
	}
	r.GuestID = p.ID
	r.State = StateReady
	p.RoomID = r.ID
	return s.viewOf(r), nil
}

func (s *Store) LeaveRoom(token string) error {
	s.mu.Lock()
	defer s.mu.Unlock()

	p, ok := s.lookup(token)
	if !ok {
		return errNotFound
	}
	if p.RoomID == "" {
		return errNotInRoom
	}
	s.detach(p)
	return nil
}

// detach removes a player from its room, dissolving the room if the host left.
// Caller must hold the lock.
func (s *Store) detach(p *Player) {
	r, ok := s.rooms[p.RoomID]
	p.RoomID = ""
	if !ok {
		return
	}
	switch p.ID {
	case r.HostID:
		// The host owns the room: hand everyone else back to the lobby and
		// drop it.
		if r.GuestID != "" {
			if g, ok := s.players[r.GuestID]; ok {
				g.RoomID = ""
			}
		}
		for _, sid := range r.SpectatorIDs {
			if sp, ok := s.players[sid]; ok {
				sp.RoomID = ""
			}
		}
		delete(s.rooms, r.ID)
	case r.GuestID:
		r.GuestID = ""
		r.State = StateWaiting
	default:
		r.dropSpectator(p.ID)
	}
}

// PublishManifest stores the host's description of the match so spectators can
// set up the same fight before any input arrives.
func (s *Store) PublishManifest(token string, manifest []byte) error {
	if len(manifest) > maxManifestBytes {
		return errManifestSize
	}
	s.mu.Lock()
	defer s.mu.Unlock()

	p, ok := s.lookup(token)
	if !ok {
		return errNotFound
	}
	r, ok := s.rooms[p.RoomID]
	if !ok {
		return errNotInRoom
	}
	if p.ID != r.HostID {
		return errNotHost
	}
	r.manifest = append([]byte(nil), manifest...)
	return nil
}

// MarkPlaying is called once a peer has actually launched the match, so the
// room stops showing up as joinable.
func (s *Store) MarkPlaying(token string) error {
	s.mu.Lock()
	defer s.mu.Unlock()

	p, ok := s.lookup(token)
	if !ok {
		return errNotFound
	}
	r, ok := s.rooms[p.RoomID]
	if !ok {
		return errNotInRoom
	}
	if r.State == StateReady {
		r.State = StatePlaying
	}
	return nil
}

// RelayFor lazily allocates the room's relay key and reports which slot the
// caller owns. Both peers get the same key but opposite slots.
func (s *Store) RelayFor(token string) (RelayKey, int, error) {
	s.mu.Lock()
	defer s.mu.Unlock()

	p, ok := s.lookup(token)
	if !ok {
		return RelayKey{}, 0, errNotFound
	}
	r, ok := s.rooms[p.RoomID]
	if !ok {
		return RelayKey{}, 0, errNotInRoom
	}
	if !r.relaySet {
		r.relayKey = NewRelayKey()
		r.relaySet = true
	}
	slot := 0
	switch p.ID {
	case r.HostID:
	case r.GuestID:
		slot = 1
	default:
		// The relay only has two slots, so a spectator asking for one would
		// steal the guest's. Spectators watch over the punched UDP path only.
		return RelayKey{}, 0, errNotInRoom
	}
	return r.relayKey, slot, nil
}

func (s *Store) ListRooms() []RoomView {
	s.mu.Lock()
	defer s.mu.Unlock()

	out := make([]RoomView, 0, len(s.rooms))
	for _, r := range s.rooms {
		out = append(out, s.viewOf(r))
	}
	sort.Slice(out, func(i, j int) bool { return out[i].Name < out[j].Name })
	return out
}

// Poll refreshes the session and reports what the client should do next.
func (s *Store) Poll(token string) (PollResponse, error) {
	s.mu.Lock()
	defer s.mu.Unlock()

	p, ok := s.lookup(token)
	if !ok {
		return PollResponse{}, errNotFound
	}
	resp := PollResponse{Self: SelfView{ID: p.ID, Name: p.Name}}

	r, ok := s.rooms[p.RoomID]
	if !ok {
		return resp, nil
	}
	rv := s.viewOf(r)
	resp.Room = &rv

	m := MatchView{State: r.State}
	switch {
	case p.ID == r.HostID:
		m.Role = "host"
		if g, ok := s.players[r.GuestID]; ok {
			m.PeerName = g.Name
			m.PeerUDP = g.UDPAddr
		}
		// The host is the only one that streams to spectators, so only the
		// host is told where they are. GGPO needs these before it starts.
		for _, sid := range r.SpectatorIDs {
			sp, ok := s.players[sid]
			if !ok || sp.UDPAddr == "" {
				continue
			}
			m.Spectators = append(m.Spectators, SpectatorView{Name: sp.Name, UDPAddr: sp.UDPAddr})
		}
	case r.hasSpectator(p.ID):
		m.Role = "spectator"
		// A spectator only ever listens to the host.
		if h, ok := s.players[r.HostID]; ok {
			m.PeerName = h.Name
			m.HostAddr = h.IP
			m.HostPort = h.Port
			m.PeerUDP = h.UDPAddr
		}
		m.Manifest = r.manifest
	default:
		m.Role = "guest"
		// Only the guest dials out, so only the guest needs an address.
		if h, ok := s.players[r.HostID]; ok {
			m.PeerName = h.Name
			m.HostAddr = h.IP
			m.HostPort = h.Port
			m.PeerUDP = h.UDPAddr
		}
	}
	resp.Match = &m
	return resp, nil
}

func (s *Store) viewOf(r *Room) RoomView {
	hostName := ""
	if h, ok := s.players[r.HostID]; ok {
		hostName = h.Name
	}
	return RoomView{
		ID:         r.ID,
		Name:       r.Name,
		HostName:   hostName,
		Players:    r.occupants(),
		Capacity:   2,
		State:      r.State,
		Rollback:   r.Rollback,
		Spectators: len(r.SpectatorIDs),
	}
}

// Sweep drops sessions that stopped polling and any room left without a host.
func (s *Store) Sweep() {
	s.mu.Lock()
	defer s.mu.Unlock()

	cutoff := time.Now().Add(-sessionTTL)
	for _, p := range s.players {
		if p.LastSeen.After(cutoff) {
			continue
		}
		if p.RoomID != "" {
			s.detach(p)
		}
		delete(s.sessions, p.token)
		delete(s.udpTokens, p.udpToken)
		delete(s.players, p.ID)
	}
	for id, r := range s.rooms {
		if _, ok := s.players[r.HostID]; !ok {
			delete(s.rooms, id)
		}
	}
}
