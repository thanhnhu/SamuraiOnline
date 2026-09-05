package main

import (
	"bytes"
	"context"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"net"
	"net/http"
	"net/url"
	"strconv"
	"strings"
	"sync"
	"time"

	lua "github.com/yuin/gopher-lua"
)

// Lobby client for the external matchmaking server.
//
// Every network call runs on a background goroutine and only ever publishes a
// snapshot that the Lua side reads. Nothing here may block the game loop, so
// the Lua API is entirely non-blocking: scripts issue a command, then watch
// lobbyStatus()/lobbyMatch() on later frames to see the result.

const (
	lobbyRequestTimeout = 8 * time.Second
	lobbyPollInterval   = time.Second
	lobbyMaxResponse    = 1 << 20
)

type LobbyRoom struct {
	ID         string `json:"id"`
	Name       string `json:"name"`
	HostName   string `json:"hostName"`
	Players    int    `json:"players"`
	Capacity   int    `json:"capacity"`
	State      string `json:"state"`
	Rollback   bool   `json:"rollback"`
	Spectators int    `json:"spectators"`
}

// LobbySpectator is a watcher the host has to stream confirmed inputs to.
type LobbySpectator struct {
	Name    string `json:"name"`
	UDPAddr string `json:"udpAddr"`
}

type LobbyMatch struct {
	Role       string           `json:"role"`
	State      string           `json:"state"`
	PeerName   string           `json:"peerName"`
	HostAddr   string           `json:"hostAddr"`
	HostPort   int              `json:"hostPort"`
	PeerUDP    string           `json:"peerUdp"`
	Spectators []LobbySpectator `json:"spectators"`
	// Manifest describes the match a spectator is about to watch. It is only
	// ever populated for the spectator role.
	Manifest json.RawMessage `json:"manifest"`
}

type lobbyHTTPError struct {
	Status  int
	Message string
}

func (e *lobbyHTTPError) Error() string {
	if e.Message == "" {
		return fmt.Sprintf("lobby server returned %d", e.Status)
	}
	return e.Message
}

type LobbyClient struct {
	mu sync.Mutex

	baseURL   string
	token     string
	udpToken  string
	relayHost string
	relayPort int
	name      string
	connected bool
	pending   int
	lastErr   string

	rooms []LobbyRoom
	room  *LobbyRoom
	match *LobbyMatch

	nat     *NATSession
	natMode string
	// relayPlan is kept after path negotiation so the match-setup stream can
	// fall back to the relay too, not just GGPO's datagrams.
	relayPlan *RelayPlan

	hc     *http.Client
	cancel context.CancelFunc
}

var lobby = &LobbyClient{
	hc: &http.Client{Timeout: lobbyRequestTimeout},
}

// normalizeLobbyURL rejects anything that is not plain HTTP(S) so a motif
// cannot be tricked into pointing the client at an arbitrary URL scheme.
func normalizeLobbyURL(raw string) (string, error) {
	u, err := url.Parse(strings.TrimSpace(raw))
	if err != nil {
		return "", fmt.Errorf("invalid lobby URL")
	}
	if u.Scheme != "http" && u.Scheme != "https" {
		return "", fmt.Errorf("lobby URL must start with http:// or https://")
	}
	if u.Host == "" {
		return "", fmt.Errorf("lobby URL is missing a host")
	}
	u.Path = strings.TrimRight(u.Path, "/")
	u.RawQuery, u.Fragment = "", ""
	return u.String(), nil
}

func (lc *LobbyClient) setError(err error) {
	lc.mu.Lock()
	defer lc.mu.Unlock()
	if err != nil {
		lc.lastErr = err.Error()
	}
}

// async runs a command off the game loop and records any failure for Lua.
func (lc *LobbyClient) async(fn func() error) {
	lc.mu.Lock()
	lc.pending++
	lc.mu.Unlock()

	go func() {
		err := fn()
		lc.mu.Lock()
		lc.pending--
		if err != nil {
			lc.lastErr = err.Error()
		}
		lc.mu.Unlock()
	}()
}

func (lc *LobbyClient) request(method, path string, body, out any) error {
	lc.mu.Lock()
	base, token := lc.baseURL, lc.token
	lc.mu.Unlock()

	if base == "" {
		return fmt.Errorf("not connected to a lobby server")
	}

	var buf bytes.Buffer
	if body != nil {
		if err := json.NewEncoder(&buf).Encode(body); err != nil {
			return err
		}
	}
	req, err := http.NewRequest(method, base+path, &buf)
	if err != nil {
		return err
	}
	if body != nil {
		req.Header.Set("Content-Type", "application/json")
	}
	if token != "" {
		req.Header.Set("Authorization", "Bearer "+token)
	}

	resp, err := lc.hc.Do(req)
	if err != nil {
		return fmt.Errorf("lobby server unreachable")
	}
	defer resp.Body.Close()

	reader := io.LimitReader(resp.Body, lobbyMaxResponse)
	if resp.StatusCode < 200 || resp.StatusCode > 299 {
		var payload struct {
			Error string `json:"error"`
		}
		_ = json.NewDecoder(reader).Decode(&payload)
		return &lobbyHTTPError{Status: resp.StatusCode, Message: payload.Error}
	}
	if out == nil {
		return nil
	}
	return json.NewDecoder(reader).Decode(out)
}

func (lc *LobbyClient) Connect(rawURL, name string, port int) {
	base, err := normalizeLobbyURL(rawURL)
	if err != nil {
		lc.setError(err)
		return
	}
	lc.Disconnect()

	lc.mu.Lock()
	lc.baseURL, lc.name, lc.lastErr = base, name, ""
	lc.mu.Unlock()

	lc.async(func() error {
		var out struct {
			ID        string `json:"id"`
			Token     string `json:"token"`
			UDPToken  string `json:"udpToken"`
			RelayHost string `json:"relayHost"`
			RelayPort int    `json:"relayPort"`
		}
		if err := lc.request(http.MethodPost, "/api/session",
			map[string]any{"name": name, "port": port}, &out); err != nil {
			return err
		}

		ctx, cancel := context.WithCancel(context.Background())
		lc.mu.Lock()
		lc.token, lc.connected, lc.cancel = out.Token, true, cancel
		lc.udpToken, lc.relayHost, lc.relayPort = out.UDPToken, out.RelayHost, out.RelayPort
		lc.mu.Unlock()

		// Bind an ephemeral port: the peer discovers our real mapping through
		// the lobby, and a fixed port would clash when testing two clients on
		// one machine.
		if out.RelayHost != "" && out.RelayPort != 0 {
			lobbyUDP := net.JoinHostPort(out.RelayHost, strconv.Itoa(out.RelayPort))
			nat, err := StartNATSession(0, lobbyUDP, out.UDPToken)
			if err != nil {
				return err
			}
			lc.mu.Lock()
			lc.nat = nat
			lc.mu.Unlock()
		}

		go lc.run(ctx)
		return nil
	})
}

func (lc *LobbyClient) Disconnect() {
	lc.mu.Lock()
	cancel := lc.cancel
	lc.cancel, lc.token, lc.connected = nil, "", false
	lc.rooms, lc.room, lc.match = nil, nil, nil
	lc.natMode = ""
	lc.mu.Unlock()

	StopNATSession()
	ClearNATResult()
	if cancel != nil {
		cancel()
	}
}

func (lc *LobbyClient) run(ctx context.Context) {
	ticker := time.NewTicker(lobbyPollInterval)
	defer ticker.Stop()
	for {
		select {
		case <-ctx.Done():
			return
		case <-ticker.C:
			lc.refresh()
		}
	}
}

func (lc *LobbyClient) refresh() {
	var poll struct {
		Room  *LobbyRoom  `json:"room"`
		Match *LobbyMatch `json:"match"`
	}
	if err := lc.request(http.MethodPost, "/api/poll", nil, &poll); err != nil {
		// A 404 means the server already expired this session, so the client
		// has to register again rather than keep polling a dead token.
		if he, ok := err.(*lobbyHTTPError); ok && he.Status == http.StatusNotFound {
			lc.mu.Lock()
			lc.connected, lc.token = false, ""
			lc.lastErr = "lobby session expired"
			lc.mu.Unlock()
			return
		}
		lc.setError(err)
		return
	}

	var list struct {
		Rooms []LobbyRoom `json:"rooms"`
	}
	if err := lc.request(http.MethodGet, "/api/rooms", nil, &list); err != nil {
		lc.setError(err)
		return
	}

	lc.mu.Lock()
	lc.room, lc.match, lc.rooms = poll.Room, poll.Match, list.Rooms
	lc.mu.Unlock()
}

func (lc *LobbyClient) CreateRoom(name string, rollback bool) {
	lc.async(func() error {
		return lc.request(http.MethodPost, "/api/rooms/create",
			map[string]any{"name": name, "rollback": rollback}, nil)
	})
}

func (lc *LobbyClient) JoinRoom(id string) {
	lc.async(func() error {
		return lc.request(http.MethodPost, "/api/rooms/join",
			map[string]any{"roomId": id}, nil)
	})
}

// JoinAsSpectator takes a watcher seat, which does not consume the second
// player slot, so the room stays joinable.
func (lc *LobbyClient) JoinAsSpectator(id string) {
	lc.async(func() error {
		return lc.request(http.MethodPost, "/api/rooms/join",
			map[string]any{"roomId": id, "spectator": true}, nil)
	})
}

// PublishManifest is how the host tells watchers which fight to load. It runs
// after the select screen, before the first frame.
func (lc *LobbyClient) PublishManifest(manifest map[string]any) {
	lc.async(func() error {
		return lc.request(http.MethodPost, "/api/match/manifest",
			map[string]any{"manifest": manifest}, nil)
	})
}

func (lc *LobbyClient) LeaveRoom() {
	lc.async(func() error {
		err := lc.request(http.MethodPost, "/api/rooms/leave", nil, nil)
		lc.mu.Lock()
		lc.room, lc.match = nil, nil
		lc.mu.Unlock()
		return err
	})
}

func (lc *LobbyClient) MarkPlaying() {
	lc.async(func() error {
		return lc.request(http.MethodPost, "/api/match/start", nil, nil)
	})
}

// EstablishPath negotiates how the two peers will actually exchange packets.
// It runs off the game loop; watch lobbyNatMode() for the outcome.
func (lc *LobbyClient) EstablishPath() {
	lc.async(func() error {
		lc.mu.Lock()
		nat, match := lc.nat, lc.match
		lc.mu.Unlock()

		if nat == nil {
			return fmt.Errorf("no NAT session; reconnect to the lobby")
		}
		if match == nil {
			return fmt.Errorf("not in a room")
		}

		// Ask for relay credentials up front so the fallback is ready the moment
		// punching fails; allocating one costs nothing until it is used.
		var plan *RelayPlan
		var alloc struct {
			Host    string `json:"host"`
			Port    int    `json:"port"`
			TCPPort int    `json:"tcpPort"`
			Key     string `json:"key"`
			Slot    int    `json:"slot"`
		}
		if err := lc.request(http.MethodPost, "/api/relay/allocate", nil, &alloc); err != nil {
			log.Printf("lobby: relay unavailable, direct only: %v", err)
		} else if raw, err := hex.DecodeString(alloc.Key); err == nil && len(raw) == relayKeyLen {
			var k [relayKeyLen]byte
			copy(k[:], raw)
			plan = &RelayPlan{
				Host:    alloc.Host,
				Port:    alloc.Port,
				TCPPort: alloc.TCPPort,
				Key:     k,
				Slot:    alloc.Slot,
			}
		}

		res, err := nat.Establish(match.PeerUDP, plan)
		if err != nil {
			return err
		}
		lc.mu.Lock()
		lc.natMode = string(res.Mode)
		lc.relayPlan = plan
		lc.mu.Unlock()
		return nil
	})
}

func (lc *LobbyClient) NATMode() string {
	lc.mu.Lock()
	defer lc.mu.Unlock()
	return lc.natMode
}

// RelayPlan returns the credentials negotiated by EstablishPath, or nil if it
// has not run or the lobby offered no relay.
func (lc *LobbyClient) RelayPlan() *RelayPlan {
	lc.mu.Lock()
	defer lc.mu.Unlock()
	if lc.relayPlan == nil {
		return nil
	}
	plan := *lc.relayPlan
	return &plan
}

func (lc *LobbyClient) RelayStreamAvailable() bool {
	p := lc.RelayPlan()
	return p != nil && p.TCPPort != 0
}

func (lc *LobbyClient) Snapshot() (connected bool, pending int, lastErr string) {
	lc.mu.Lock()
	defer lc.mu.Unlock()
	return lc.connected, lc.pending, lc.lastErr
}

func (lc *LobbyClient) Rooms() []LobbyRoom {
	lc.mu.Lock()
	defer lc.mu.Unlock()
	out := make([]LobbyRoom, len(lc.rooms))
	copy(out, lc.rooms)
	return out
}

func (lc *LobbyClient) Match() (LobbyMatch, *LobbyRoom, bool) {
	lc.mu.Lock()
	defer lc.mu.Unlock()
	if lc.match == nil {
		return LobbyMatch{}, nil, false
	}
	// Guard against a server that reports a match without a room.
	var room *LobbyRoom
	if lc.room != nil {
		copied := *lc.room
		room = &copied
	}
	return *lc.match, room, true
}

// splitHostPort parses an address the lobby observed, so a malformed value
// from the server surfaces as an error rather than a silent bad connection.
func splitHostPort(addr string) (string, int, error) {
	if addr == "" {
		return "", 0, fmt.Errorf("empty address")
	}
	host, portStr, err := net.SplitHostPort(addr)
	if err != nil {
		return "", 0, err
	}
	port, err := strconv.Atoi(portStr)
	if err != nil || port < 1 || port > 65535 {
		return "", 0, fmt.Errorf("bad port in %q", addr)
	}
	return host, port, nil
}

// -------------------------------------------------------------------------------------------------
// Lua bindings

func lobbyScriptInit(l *lua.LState) {
	luaRegister(l, "lobbyConnect", func(*lua.LState) int {
		/*Register with a matchmaking server. Returns immediately; watch
		lobbyStatus() on later frames for the outcome.
		@function lobbyConnect
		@tparam string url Base URL of the lobby server, e.g. "http://127.0.0.1:8080".
		@tparam string name Display name shown to other players.
		@tparam[opt] int port Port peers should dial; defaults to Netplay.ListenPort.
		function lobbyConnect(url, name, port) end*/
		port := sys.cfg.Netplay.ListenPort
		if !nilArg(l, 3) {
			port = int(numArg(l, 3))
		}
		lobby.Connect(strArg(l, 1), strArg(l, 2), port)
		return 0
	})

	luaRegister(l, "lobbyDisconnect", func(*lua.LState) int {
		/*Drop the lobby session and stop polling.
		@function lobbyDisconnect
		function lobbyDisconnect() end*/
		lobby.Disconnect()
		return 0
	})

	luaRegister(l, "lobbyStatus", func(L *lua.LState) int {
		/*Current lobby connection state.
		@function lobbyStatus
		@treturn table status Fields: `connected`, `busy`, `error`.
		function lobbyStatus() end*/
		connected, pending, lastErr := lobby.Snapshot()
		t := L.NewTable()
		t.RawSetString("connected", lua.LBool(connected))
		t.RawSetString("busy", lua.LBool(pending > 0))
		t.RawSetString("error", lua.LString(lastErr))
		L.Push(t)
		return 1
	})

	luaRegister(l, "lobbyRooms", func(L *lua.LState) int {
		/*List of rooms known from the last poll.
		@function lobbyRooms
		@treturn table rooms Array of `{id, name, hostName, players, capacity, state, rollback}`.
		function lobbyRooms() end*/
		rooms := lobby.Rooms()
		t := L.NewTable()
		for i, r := range rooms {
			e := L.NewTable()
			e.RawSetString("id", lua.LString(r.ID))
			e.RawSetString("name", lua.LString(r.Name))
			e.RawSetString("hostName", lua.LString(r.HostName))
			e.RawSetString("players", lua.LNumber(r.Players))
			e.RawSetString("capacity", lua.LNumber(r.Capacity))
			e.RawSetString("state", lua.LString(r.State))
			e.RawSetString("rollback", lua.LBool(r.Rollback))
			e.RawSetString("spectators", lua.LNumber(r.Spectators))
			t.RawSetInt(i+1, e)
		}
		L.Push(t)
		return 1
	})

	luaRegister(l, "lobbyCreateRoom", func(*lua.LState) int {
		/*Create a room and become its host.
		@function lobbyCreateRoom
		@tparam string name Room name.
		@tparam[opt] boolean rollback Advertise rollback netcode; defaults to the local config.
		function lobbyCreateRoom(name, rollback) end*/
		rollback := sys.cfg.Netplay.RollbackNetcode
		if !nilArg(l, 2) {
			rollback = boolArg(l, 2)
		}
		lobby.CreateRoom(strArg(l, 1), rollback)
		return 0
	})

	luaRegister(l, "lobbyJoinRoom", func(*lua.LState) int {
		/*Join an existing room as the guest.
		@function lobbyJoinRoom
		@tparam string roomId Room identifier from lobbyRooms().
		function lobbyJoinRoom(roomId) end*/
		lobby.JoinRoom(strArg(l, 1))
		return 0
	})

	luaRegister(l, "lobbySpectateRoom", func(*lua.LState) int {
		/*Join an existing room as a watcher. This does not take the second
		player seat, so the room stays joinable.
		@function lobbySpectateRoom
		@tparam string roomId Room identifier from lobbyRooms().
		function lobbySpectateRoom(roomId) end*/
		lobby.JoinAsSpectator(strArg(l, 1))
		return 0
	})

	luaRegister(l, "lobbyPublishManifest", func(L *lua.LState) int {
		/*Host only: describe the match so watchers can load the same fight.
		Call once the select screen has settled, before the first frame.
		@function lobbyPublishManifest
		@tparam table manifest Flat table of strings and numbers, e.g.
		 `{p1 = 'jubei', p2 = 'jubei2', stage = 'kfm', seed = 12345}`.
		function lobbyPublishManifest(manifest) end*/
		tbl := L.CheckTable(1)
		manifest := map[string]any{}
		tbl.ForEach(func(k, v lua.LValue) {
			key, ok := k.(lua.LString)
			if !ok {
				return
			}
			switch val := v.(type) {
			case lua.LString:
				manifest[string(key)] = string(val)
			case lua.LNumber:
				manifest[string(key)] = float64(val)
			case lua.LBool:
				manifest[string(key)] = bool(val)
			}
		})
		lobby.PublishManifest(manifest)
		return 0
	})

	luaRegister(l, "lobbyManifest", func(L *lua.LState) int {
		/*Spectator only: the host's description of the match to load, or nil
		if the host has not published one yet.
		@function lobbyManifest
		@treturn table|nil manifest
		function lobbyManifest() end*/
		m, _, ok := lobby.Match()
		if !ok || len(m.Manifest) == 0 {
			L.Push(lua.LNil)
			return 1
		}
		var decoded map[string]any
		if err := json.Unmarshal(m.Manifest, &decoded); err != nil {
			L.Push(lua.LNil)
			return 1
		}
		t := L.NewTable()
		for k, v := range decoded {
			switch val := v.(type) {
			case string:
				t.RawSetString(k, lua.LString(val))
			case float64:
				t.RawSetString(k, lua.LNumber(val))
			case bool:
				t.RawSetString(k, lua.LBool(val))
			}
		}
		L.Push(t)
		return 1
	})

	luaRegister(l, "lobbyEnterSpectate", func(L *lua.LState) int {
		/*Start watching the match. Requires lobbyEstablishPath() to have
		completed, so the UDP path to the host is already open.
		@function lobbyEnterSpectate
		function lobbyEnterSpectate() end*/
		if sys.netConnection != nil || sys.rollback.session != nil {
			L.RaiseError("\nConnection already established.\n")
		}
		m, _, ok := lobby.Match()
		if !ok || m.Role != "spectator" {
			L.RaiseError("\nNot a spectator in this room.\n")
		}
		if !sys.cfg.Netplay.RollbackNetcode {
			L.RaiseError("\nSpectating requires rollback netcode.\n")
		}
		host, port, err := splitHostPort(m.PeerUDP)
		if err != nil {
			L.RaiseError("\nNo address for the host: " + err.Error() + "\n")
		}
		rs := NewRollbackSession(sys.cfg.Netplay.Rollback)
		sys.rollback.session = &rs
		rs.InitSpectator(2, sys.cfg.Netplay.ListenPort, host, port)
		return 0
	})

	luaRegister(l, "lobbyLeaveRoom", func(*lua.LState) int {
		/*Leave the current room.
		@function lobbyLeaveRoom
		function lobbyLeaveRoom() end*/
		lobby.LeaveRoom()
		return 0
	})

	luaRegister(l, "lobbyMatch", func(L *lua.LState) int {
		/*Match assignment for the current room, or nil when not in one.
		@function lobbyMatch
		@treturn table|nil match Fields: `role` ("host"/"guest"/"spectator"), `state`,
		  `peerName`, `hostAddr`, `hostPort`, `roomName`, `ready`, `spectators`.
		function lobbyMatch() end*/
		m, room, ok := lobby.Match()
		if !ok {
			L.Push(lua.LNil)
			return 1
		}
		t := L.NewTable()
		t.RawSetString("role", lua.LString(m.Role))
		t.RawSetString("state", lua.LString(m.State))
		t.RawSetString("peerName", lua.LString(m.PeerName))
		t.RawSetString("hostAddr", lua.LString(m.HostAddr))
		t.RawSetString("hostPort", lua.LNumber(m.HostPort))
		t.RawSetString("peerUdp", lua.LString(m.PeerUDP))
		t.RawSetString("spectators", lua.LNumber(len(m.Spectators)))
		// A watcher is "ready" once the fight is actually running; the players
		// are ready as soon as both seats are filled.
		if m.Role == "spectator" {
			t.RawSetString("ready", lua.LBool(m.State == "playing"))
		} else {
			t.RawSetString("ready", lua.LBool(m.State == "ready"))
		}
		if room != nil {
			t.RawSetString("roomName", lua.LString(room.Name))
		}
		L.Push(t)
		return 1
	})

	luaRegister(l, "lobbyMarkPlaying", func(*lua.LState) int {
		/*Tell the server the match launched so the room stops accepting joins.
		@function lobbyMarkPlaying
		function lobbyMarkPlaying() end*/
		lobby.MarkPlaying()
		return 0
	})

	luaRegister(l, "lobbyEstablishPath", func(*lua.LState) int {
		/*Negotiate the network path to the peer: hole punch first, relay if that
		fails. Returns immediately; poll lobbyNatMode().
		@function lobbyEstablishPath
		function lobbyEstablishPath() end*/
		lobby.EstablishPath()
		return 0
	})

	luaRegister(l, "lobbyRelayStream", func(L *lua.LState) int {
		/*Whether the lobby can splice the match-setup stream, which is what
		lets a host without a forwarded port still be joined.
		@function lobbyRelayStream
		@treturn boolean available
		function lobbyRelayStream() end*/
		L.Push(lua.LBool(lobby.RelayStreamAvailable()))
		return 1
	})

	luaRegister(l, "lobbyEnterNetPlay", func(L *lua.LState) int {
		/*Enter netplay over the lobby relay instead of a direct TCP link.
		Requires lobbyEstablishPath() to have completed.
		@function lobbyEnterNetPlay
		@tparam boolean host `true` to take the host role.
		function lobbyEnterNetPlay(host) end*/
		if sys.netConnection != nil {
			L.RaiseError("\nConnection already established.\n")
		}
		plan := lobby.RelayPlan()
		if plan == nil || plan.TCPPort == 0 {
			L.RaiseError("\nNo relay stream available.\n")
		}

		netPlayBegin()
		// GGPO refuses spectators once synchronization starts, so the list is
		// snapshotted here. Anyone who joins later watches the next match.
		if sys.cfg.Netplay.RollbackNetcode {
			if m, _, ok := lobby.Match(); ok {
				for _, sp := range m.Spectators {
					if sp.UDPAddr != "" {
						sys.rollback.session.spectatorAddrs = append(sys.rollback.session.spectatorAddrs, sp.UDPAddr)
					}
				}
			}
		}
		// Hole punching already tells us whether a direct UDP path exists; if
		// it does, a raw TCP accept/dial has a real chance too, and skips the
		// relay's extra hop for the whole match, not just the setup stream.
		// A NAT strict enough to have needed the relay for UDP will just as
		// surely block a direct TCP link, so do not waste time on it there.
		direct := lobby.NATMode() == string(NATPunched)
		var err error
		if L.ToBool(1) {
			if direct {
				err = sys.netConnection.AcceptDirectThenRelay(sys.cfg.Netplay.ListenPort, plan, netDirectTimeout)
			} else {
				err = sys.netConnection.AcceptRelayed(plan)
			}
		} else {
			// GGPO's endpoint comes from NAT traversal, so the host address
			// recorded here only needs to be non-empty to select the P2 role.
			if sys.cfg.Netplay.RollbackNetcode {
				sys.rollback.session.host = plan.Host
			}
			if match, _, ok := lobby.Match(); direct && ok && match.HostAddr != "" && match.HostPort != 0 {
				err = sys.netConnection.ConnectDirectThenRelay(match.HostAddr, match.HostPort, plan, netDirectTimeout)
			} else {
				err = sys.netConnection.ConnectRelayed(plan)
			}
		}
		if err != nil {
			sys.netConnection = nil
			L.RaiseError(err.Error())
		}
		return 0
	})

	luaRegister(l, "lobbyNatMode", func(L *lua.LState) int {
		/*How the peers ended up connected.
		@function lobbyNatMode
		@treturn string mode `""` while negotiating, then `"punched"` or `"relayed"`.
		function lobbyNatMode() end*/
		L.Push(lua.LString(lobby.NATMode()))
		return 1
	})

	luaRegister(l, "lobbyLocalAddr", func(L *lua.LState) int {
		/*The NAT-mapped address the lobby observed for this client.
		@function lobbyLocalAddr
		@treturn string addr Empty until discovery completes.
		function lobbyLocalAddr() end*/
		addr := ""
		if s := natActive(); s != nil {
			addr = s.Observed()
		}
		L.Push(lua.LString(addr))
		return 1
	})
}
