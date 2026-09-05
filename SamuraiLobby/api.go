package main

import (
	"encoding/hex"
	"encoding/json"
	"errors"
	"log"
	"net"
	"net/http"
	"strings"
)

const maxBodyBytes = 4 << 10

type SelfView struct {
	ID   string `json:"id"`
	Name string `json:"name"`
}

// SpectatorView tells the host where to stream a watcher's copy of the match.
type SpectatorView struct {
	Name    string `json:"name"`
	UDPAddr string `json:"udpAddr"`
}

type MatchView struct {
	Role     string    `json:"role"`
	State    RoomState `json:"state"`
	PeerName string    `json:"peerName,omitempty"`
	// HostAddr/HostPort are populated for the guest only; the host learns the
	// guest address from the incoming TCP connection.
	HostAddr string `json:"hostAddr,omitempty"`
	HostPort int    `json:"hostPort,omitempty"`
	// PeerUDP is the peer's NAT-mapped UDP address, the target for hole punching.
	PeerUDP string `json:"peerUdp,omitempty"`
	// Spectators is sent to the host only, because only the host streams to them.
	Spectators []SpectatorView `json:"spectators,omitempty"`
	// Manifest is sent to spectators only: the host's description of the match
	// they are about to watch.
	Manifest json.RawMessage `json:"manifest,omitempty"`
}

type PollResponse struct {
	Self  SelfView   `json:"self"`
	Room  *RoomView  `json:"room,omitempty"`
	Match *MatchView `json:"match,omitempty"`
}

type API struct {
	store *Store
	relay *Relay
	// relayHost/relayPort are what clients are told to dial; they may differ
	// from the bind address when the server sits behind NAT itself.
	relayHost string
	relayPort int
	// relayTCPPort carries the match setup stream when the host cannot accept
	// inbound TCP. Zero disables the fallback.
	relayTCPPort int
	// trustProxy must stay false unless the server sits behind a reverse proxy
	// that overwrites X-Forwarded-For, otherwise clients can spoof their address.
	trustProxy bool
}

func NewAPI(store *Store, relay *Relay, relayHost string, relayPort, relayTCPPort int, trustProxy bool) *API {
	return &API{
		store:        store,
		relay:        relay,
		relayHost:    relayHost,
		relayPort:    relayPort,
		relayTCPPort: relayTCPPort,
		trustProxy:   trustProxy,
	}
}

func (a *API) Routes() http.Handler {
	mux := http.NewServeMux()
	mux.HandleFunc("/healthz", func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusOK)
		_, _ = w.Write([]byte("ok"))
	})
	mux.HandleFunc("/api/session", a.handleSession)
	mux.HandleFunc("/api/rooms", a.handleListRooms)
	mux.HandleFunc("/api/rooms/create", a.handleCreateRoom)
	mux.HandleFunc("/api/rooms/join", a.handleJoinRoom)
	mux.HandleFunc("/api/rooms/leave", a.handleLeaveRoom)
	mux.HandleFunc("/api/match/start", a.handleMatchStart)
	mux.HandleFunc("/api/match/manifest", a.handleMatchManifest)
	mux.HandleFunc("/api/relay/allocate", a.handleRelayAllocate)
	mux.HandleFunc("/api/poll", a.handlePoll)
	return mux
}

func (a *API) handleSession(w http.ResponseWriter, r *http.Request) {
	if !requireMethod(w, r, http.MethodPost) {
		return
	}
	var req struct {
		Name string `json:"name"`
		Port int    `json:"port"`
	}
	if !decode(w, r, &req) {
		return
	}
	if req.Port < 1 || req.Port > 65535 {
		writeErr(w, http.StatusBadRequest, "port must be between 1 and 65535")
		return
	}
	id, token, udpToken, err := a.store.CreateSession(req.Name, a.clientIP(r), req.Port)
	if err != nil {
		writeStoreErr(w, err)
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{
		"id":        id,
		"token":     token,
		"udpToken":  udpToken,
		"relayHost": a.relayHost,
		"relayPort": a.relayPort,
	})
}

func (a *API) handleListRooms(w http.ResponseWriter, r *http.Request) {
	if !requireMethod(w, r, http.MethodGet) {
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{"rooms": a.store.ListRooms()})
}

func (a *API) handleCreateRoom(w http.ResponseWriter, r *http.Request) {
	if !requireMethod(w, r, http.MethodPost) {
		return
	}
	token, ok := bearer(w, r)
	if !ok {
		return
	}
	var req struct {
		Name     string `json:"name"`
		Rollback bool   `json:"rollback"`
	}
	if !decode(w, r, &req) {
		return
	}
	room, err := a.store.CreateRoom(token, req.Name, req.Rollback)
	if err != nil {
		writeStoreErr(w, err)
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{"room": room})
}

func (a *API) handleJoinRoom(w http.ResponseWriter, r *http.Request) {
	if !requireMethod(w, r, http.MethodPost) {
		return
	}
	token, ok := bearer(w, r)
	if !ok {
		return
	}
	var req struct {
		RoomID    string `json:"roomId"`
		Spectator bool   `json:"spectator"`
	}
	if !decode(w, r, &req) {
		return
	}
	room, err := a.store.JoinRoom(token, req.RoomID, req.Spectator)
	if err != nil {
		writeStoreErr(w, err)
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{"room": room})
}

func (a *API) handleMatchManifest(w http.ResponseWriter, r *http.Request) {
	if !requireMethod(w, r, http.MethodPost) {
		return
	}
	token, ok := bearer(w, r)
	if !ok {
		return
	}
	var req struct {
		Manifest json.RawMessage `json:"manifest"`
	}
	if !decode(w, r, &req) {
		return
	}
	if err := a.store.PublishManifest(token, req.Manifest); err != nil {
		writeStoreErr(w, err)
		return
	}
	writeJSON(w, http.StatusOK, map[string]bool{"ok": true})
}

func (a *API) handleLeaveRoom(w http.ResponseWriter, r *http.Request) {
	if !requireMethod(w, r, http.MethodPost) {
		return
	}
	token, ok := bearer(w, r)
	if !ok {
		return
	}
	if err := a.store.LeaveRoom(token); err != nil {
		writeStoreErr(w, err)
		return
	}
	writeJSON(w, http.StatusOK, map[string]bool{"ok": true})
}

func (a *API) handleMatchStart(w http.ResponseWriter, r *http.Request) {
	if !requireMethod(w, r, http.MethodPost) {
		return
	}
	token, ok := bearer(w, r)
	if !ok {
		return
	}
	if err := a.store.MarkPlaying(token); err != nil {
		writeStoreErr(w, err)
		return
	}
	writeJSON(w, http.StatusOK, map[string]bool{"ok": true})
}

func (a *API) handleRelayAllocate(w http.ResponseWriter, r *http.Request) {
	if !requireMethod(w, r, http.MethodPost) {
		return
	}
	token, ok := bearer(w, r)
	if !ok {
		return
	}
	key, slot, err := a.store.RelayFor(token)
	if err != nil {
		writeStoreErr(w, err)
		return
	}
	a.relay.Register(key)
	writeJSON(w, http.StatusOK, map[string]any{
		"host":    a.relayHost,
		"port":    a.relayPort,
		"tcpPort": a.relayTCPPort,
		"key":     hex.EncodeToString(key[:]),
		"slot":    slot,
	})
}

func (a *API) handlePoll(w http.ResponseWriter, r *http.Request) {
	if !requireMethod(w, r, http.MethodPost) {
		return
	}
	token, ok := bearer(w, r)
	if !ok {
		return
	}
	resp, err := a.store.Poll(token)
	if err != nil {
		writeStoreErr(w, err)
		return
	}
	writeJSON(w, http.StatusOK, resp)
}

func (a *API) clientIP(r *http.Request) string {
	if a.trustProxy {
		if xff := r.Header.Get("X-Forwarded-For"); xff != "" {
			first := strings.TrimSpace(strings.Split(xff, ",")[0])
			if net.ParseIP(first) != nil {
				return first
			}
		}
	}
	host, _, err := net.SplitHostPort(r.RemoteAddr)
	if err != nil {
		return r.RemoteAddr
	}
	return host
}

func requireMethod(w http.ResponseWriter, r *http.Request, method string) bool {
	if r.Method != method {
		w.Header().Set("Allow", method)
		writeErr(w, http.StatusMethodNotAllowed, "method not allowed")
		return false
	}
	return true
}

func bearer(w http.ResponseWriter, r *http.Request) (string, bool) {
	h := r.Header.Get("Authorization")
	if !strings.HasPrefix(h, "Bearer ") {
		writeErr(w, http.StatusUnauthorized, "missing bearer token")
		return "", false
	}
	token := strings.TrimSpace(strings.TrimPrefix(h, "Bearer "))
	if token == "" {
		writeErr(w, http.StatusUnauthorized, "missing bearer token")
		return "", false
	}
	return token, true
}

func decode(w http.ResponseWriter, r *http.Request, dst any) bool {
	r.Body = http.MaxBytesReader(w, r.Body, maxBodyBytes)
	dec := json.NewDecoder(r.Body)
	dec.DisallowUnknownFields()
	if err := dec.Decode(dst); err != nil {
		writeErr(w, http.StatusBadRequest, "invalid JSON body")
		return false
	}
	return true
}

func writeJSON(w http.ResponseWriter, status int, v any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.Header().Set("X-Content-Type-Options", "nosniff")
	w.WriteHeader(status)
	if err := json.NewEncoder(w).Encode(v); err != nil {
		log.Printf("write response: %v", err)
	}
}

func writeErr(w http.ResponseWriter, status int, msg string) {
	writeJSON(w, status, map[string]string{"error": msg})
}

func writeStoreErr(w http.ResponseWriter, err error) {
	switch {
	case errors.Is(err, errNotFound):
		writeErr(w, http.StatusNotFound, "session or room not found")
	case errors.Is(err, errRoomFull):
		writeErr(w, http.StatusConflict, "room is full")
	case errors.Is(err, errInRoom):
		writeErr(w, http.StatusConflict, "already in a room")
	case errors.Is(err, errNotInRoom):
		writeErr(w, http.StatusConflict, "not in a room")
	case errors.Is(err, errCapacity):
		writeErr(w, http.StatusServiceUnavailable, "server is at capacity")
	case errors.Is(err, errNotHost):
		writeErr(w, http.StatusForbidden, "only the host may do that")
	case errors.Is(err, errManifestSize):
		writeErr(w, http.StatusRequestEntityTooLarge, "match manifest is too large")
	default:
		log.Printf("unhandled store error: %v", err)
		writeErr(w, http.StatusInternalServerError, "internal error")
	}
}
