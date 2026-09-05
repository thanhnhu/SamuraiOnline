package main

import (
	"bytes"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"testing"
	"time"
)

type client struct {
	t      *testing.T
	srv    *httptest.Server
	token  string
	remote string
}

func newHarness(t *testing.T) *httptest.Server {
	t.Helper()
	relay := NewRelay()
	t.Cleanup(func() { _ = relay.Close() })
	srv := httptest.NewServer(NewAPI(NewStore(), relay, "127.0.0.1", 8081, 8081, false).Routes())
	t.Cleanup(srv.Close)
	return srv
}

func (c *client) do(method, path string, body any, out any) int {
	c.t.Helper()

	var buf bytes.Buffer
	if body != nil {
		if err := json.NewEncoder(&buf).Encode(body); err != nil {
			c.t.Fatalf("encode: %v", err)
		}
	}
	req, err := http.NewRequest(method, c.srv.URL+path, &buf)
	if err != nil {
		c.t.Fatalf("request: %v", err)
	}
	if c.token != "" {
		req.Header.Set("Authorization", "Bearer "+c.token)
	}
	resp, err := c.srv.Client().Do(req)
	if err != nil {
		c.t.Fatalf("do: %v", err)
	}
	defer resp.Body.Close()

	if out != nil {
		if err := json.NewDecoder(resp.Body).Decode(out); err != nil {
			c.t.Fatalf("decode %s %s: %v", method, path, err)
		}
	}
	return resp.StatusCode
}

func (c *client) register(name string, port int) {
	c.t.Helper()
	var out struct {
		Token string `json:"token"`
	}
	if code := c.do(http.MethodPost, "/api/session",
		map[string]any{"name": name, "port": port}, &out); code != http.StatusOK {
		c.t.Fatalf("register %s: status %d", name, code)
	}
	c.token = out.Token
}

func TestMatchHandoffGivesGuestTheHostAddress(t *testing.T) {
	srv := newHarness(t)
	host := &client{t: t, srv: srv}
	guest := &client{t: t, srv: srv}

	host.register("Haohmaru", 7500)
	guest.register("Nakoruru", 7500)

	var created struct {
		Room RoomView `json:"room"`
	}
	if code := host.do(http.MethodPost, "/api/rooms/create",
		map[string]any{"name": "Ryuko no Oni", "rollback": true}, &created); code != http.StatusOK {
		t.Fatalf("create room: status %d", code)
	}

	var listed struct {
		Rooms []RoomView `json:"rooms"`
	}
	guest.do(http.MethodGet, "/api/rooms", nil, &listed)
	if len(listed.Rooms) != 1 {
		t.Fatalf("expected 1 room, got %d", len(listed.Rooms))
	}
	if listed.Rooms[0].HostName != "Haohmaru" || listed.Rooms[0].Players != 1 {
		t.Fatalf("unexpected room listing: %+v", listed.Rooms[0])
	}

	if code := guest.do(http.MethodPost, "/api/rooms/join",
		map[string]any{"roomId": created.Room.ID}, nil); code != http.StatusOK {
		t.Fatalf("join: status %d", code)
	}

	var guestPoll PollResponse
	guest.do(http.MethodPost, "/api/poll", nil, &guestPoll)
	if guestPoll.Match == nil {
		t.Fatal("guest received no match info")
	}
	if guestPoll.Match.Role != "guest" || guestPoll.Match.State != StateReady {
		t.Fatalf("unexpected guest match: %+v", guestPoll.Match)
	}
	if guestPoll.Match.HostAddr == "" || guestPoll.Match.HostPort != 7500 {
		t.Fatalf("guest must learn the host address, got %+v", guestPoll.Match)
	}
	if guestPoll.Match.PeerName != "Haohmaru" {
		t.Fatalf("unexpected peer name %q", guestPoll.Match.PeerName)
	}

	var hostPoll PollResponse
	host.do(http.MethodPost, "/api/poll", nil, &hostPoll)
	if hostPoll.Match == nil || hostPoll.Match.Role != "host" {
		t.Fatalf("unexpected host match: %+v", hostPoll.Match)
	}
	// The host listens rather than dials, so handing it an address would be noise.
	if hostPoll.Match.HostAddr != "" {
		t.Fatalf("host should not receive a dial address, got %q", hostPoll.Match.HostAddr)
	}
	if hostPoll.Match.PeerName != "Nakoruru" {
		t.Fatalf("unexpected peer name %q", hostPoll.Match.PeerName)
	}
}

func TestThirdPlayerCannotJoinFullRoom(t *testing.T) {
	srv := newHarness(t)
	host := &client{t: t, srv: srv}
	guest := &client{t: t, srv: srv}
	third := &client{t: t, srv: srv}

	host.register("Host", 7500)
	guest.register("Guest", 7500)
	third.register("Third", 7500)

	var created struct {
		Room RoomView `json:"room"`
	}
	host.do(http.MethodPost, "/api/rooms/create", map[string]any{"name": "r", "rollback": true}, &created)
	guest.do(http.MethodPost, "/api/rooms/join", map[string]any{"roomId": created.Room.ID}, nil)

	if code := third.do(http.MethodPost, "/api/rooms/join",
		map[string]any{"roomId": created.Room.ID}, nil); code != http.StatusConflict {
		t.Fatalf("expected 409 for full room, got %d", code)
	}
}

func TestHostLeavingDissolvesRoom(t *testing.T) {
	srv := newHarness(t)
	host := &client{t: t, srv: srv}
	guest := &client{t: t, srv: srv}

	host.register("Host", 7500)
	guest.register("Guest", 7500)

	var created struct {
		Room RoomView `json:"room"`
	}
	host.do(http.MethodPost, "/api/rooms/create", map[string]any{"name": "r", "rollback": true}, &created)
	guest.do(http.MethodPost, "/api/rooms/join", map[string]any{"roomId": created.Room.ID}, nil)

	host.do(http.MethodPost, "/api/rooms/leave", nil, nil)

	var listed struct {
		Rooms []RoomView `json:"rooms"`
	}
	guest.do(http.MethodGet, "/api/rooms", nil, &listed)
	if len(listed.Rooms) != 0 {
		t.Fatalf("room should be gone, got %+v", listed.Rooms)
	}

	// The guest must be released back to the lobby, not stranded in a dead room.
	var guestPoll PollResponse
	guest.do(http.MethodPost, "/api/poll", nil, &guestPoll)
	if guestPoll.Room != nil {
		t.Fatalf("guest still attached to room %+v", guestPoll.Room)
	}
}

func TestGuestLeavingReopensRoom(t *testing.T) {
	srv := newHarness(t)
	host := &client{t: t, srv: srv}
	guest := &client{t: t, srv: srv}

	host.register("Host", 7500)
	guest.register("Guest", 7500)

	var created struct {
		Room RoomView `json:"room"`
	}
	host.do(http.MethodPost, "/api/rooms/create", map[string]any{"name": "r", "rollback": true}, &created)
	guest.do(http.MethodPost, "/api/rooms/join", map[string]any{"roomId": created.Room.ID}, nil)
	guest.do(http.MethodPost, "/api/rooms/leave", nil, nil)

	var hostPoll PollResponse
	host.do(http.MethodPost, "/api/poll", nil, &hostPoll)
	if hostPoll.Room == nil || hostPoll.Room.Players != 1 {
		t.Fatalf("room should be waiting again, got %+v", hostPoll.Room)
	}
	if hostPoll.Match.State != StateWaiting {
		t.Fatalf("expected waiting state, got %q", hostPoll.Match.State)
	}
}

func TestRequestsWithoutTokenAreRejected(t *testing.T) {
	srv := newHarness(t)
	anon := &client{t: t, srv: srv}

	for _, path := range []string{"/api/rooms/create", "/api/rooms/join", "/api/rooms/leave", "/api/poll", "/api/match/start"} {
		if code := anon.do(http.MethodPost, path, map[string]any{}, nil); code != http.StatusUnauthorized {
			t.Errorf("%s: expected 401, got %d", path, code)
		}
	}
}

func TestSweepDropsStaleSessionsAndRooms(t *testing.T) {
	store := NewStore()
	_, token, _, err := store.CreateSession("Ghost", "10.0.0.1", 7500)
	if err != nil {
		t.Fatalf("create session: %v", err)
	}
	if _, err := store.CreateRoom(token, "abandoned", true); err != nil {
		t.Fatalf("create room: %v", err)
	}

	store.mu.Lock()
	for _, p := range store.players {
		p.LastSeen = time.Now().Add(-2 * sessionTTL)
	}
	store.mu.Unlock()

	store.Sweep()

	if got := len(store.ListRooms()); got != 0 {
		t.Fatalf("expected abandoned room to be swept, %d remain", got)
	}
	if _, err := store.Poll(token); err == nil {
		t.Fatal("expected the stale session to be invalidated")
	}
}

func TestClientSuppliedAddressIsIgnored(t *testing.T) {
	srv := newHarness(t)
	host := &client{t: t, srv: srv}
	guest := &client{t: t, srv: srv}
	host.register("Host", 7500)
	guest.register("Guest", 7500)

	var created struct {
		Room RoomView `json:"room"`
	}
	host.do(http.MethodPost, "/api/rooms/create", map[string]any{"name": "r", "rollback": true}, &created)
	guest.do(http.MethodPost, "/api/rooms/join", map[string]any{"roomId": created.Room.ID}, nil)

	var guestPoll PollResponse
	guest.do(http.MethodPost, "/api/poll", nil, &guestPoll)

	// trustProxy is off, so the address must come from the real connection and
	// never from a header a client controls.
	if guestPoll.Match.HostAddr != "127.0.0.1" {
		t.Fatalf("expected loopback from RemoteAddr, got %q", guestPoll.Match.HostAddr)
	}
}
