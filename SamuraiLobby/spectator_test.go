package main

import (
	"encoding/json"
	"net"
	"testing"
)

func specStore(t *testing.T) (*Store, string, string, string) {
	t.Helper()
	store := NewStore()
	_, hostTok, hostUDP, _ := store.CreateSession("Host", "203.0.113.1", 7500)
	_, guestTok, guestUDP, _ := store.CreateSession("Guest", "203.0.113.2", 7500)
	_, specTok, specUDP, _ := store.CreateSession("Watcher", "203.0.113.3", 7500)

	store.RecordUDPAddr(hostUDP, &net.UDPAddr{IP: net.IPv4(203, 0, 113, 1), Port: 40001})
	store.RecordUDPAddr(guestUDP, &net.UDPAddr{IP: net.IPv4(203, 0, 113, 2), Port: 40002})
	store.RecordUDPAddr(specUDP, &net.UDPAddr{IP: net.IPv4(203, 0, 113, 3), Port: 40003})

	return store, hostTok, guestTok, specTok
}

// A spectator must not consume the player seat, otherwise a watcher would lock
// out the second fighter.
func TestSpectatorDoesNotTakeThePlayerSeat(t *testing.T) {
	store, hostTok, guestTok, specTok := specStore(t)
	room, _ := store.CreateRoom(hostTok, "r", true)

	if _, err := store.JoinRoom(specTok, room.ID, true); err != nil {
		t.Fatalf("spectator join: %v", err)
	}
	if _, err := store.JoinRoom(guestTok, room.ID, false); err != nil {
		t.Fatalf("guest join after spectator: %v", err)
	}

	view := store.ListRooms()[0]
	if view.Players != 2 {
		t.Fatalf("players = %d, want 2", view.Players)
	}
	if view.Spectators != 1 {
		t.Fatalf("spectators = %d, want 1", view.Spectators)
	}
}

// The host is the only peer that streams frames onward, so it is the only one
// that may learn where the watchers are.
func TestOnlyTheHostSeesSpectatorAddresses(t *testing.T) {
	store, hostTok, guestTok, specTok := specStore(t)
	room, _ := store.CreateRoom(hostTok, "r", true)
	store.JoinRoom(guestTok, room.ID, false)
	store.JoinRoom(specTok, room.ID, true)

	hostPoll, _ := store.Poll(hostTok)
	if len(hostPoll.Match.Spectators) != 1 {
		t.Fatalf("host saw %d spectators, want 1", len(hostPoll.Match.Spectators))
	}
	if got := hostPoll.Match.Spectators[0].UDPAddr; got != "203.0.113.3:40003" {
		t.Fatalf("host saw spectator at %q", got)
	}

	guestPoll, _ := store.Poll(guestTok)
	if len(guestPoll.Match.Spectators) != 0 {
		t.Fatal("the guest was told where the spectators are")
	}
	specPoll, _ := store.Poll(specTok)
	if len(specPoll.Match.Spectators) != 0 {
		t.Fatal("a spectator was told where the other spectators are")
	}
}

// A spectator watches the host, so it needs the host's address and the role
// that stops it from trying to play.
func TestSpectatorPollPointsAtTheHost(t *testing.T) {
	store, hostTok, _, specTok := specStore(t)
	room, _ := store.CreateRoom(hostTok, "r", true)
	store.JoinRoom(specTok, room.ID, true)

	poll, _ := store.Poll(specTok)
	if poll.Match.Role != "spectator" {
		t.Fatalf("role = %q, want spectator", poll.Match.Role)
	}
	if poll.Match.PeerUDP != "203.0.113.1:40001" {
		t.Fatalf("spectator aims at %q, want the host", poll.Match.PeerUDP)
	}
}

// The manifest is how a spectator learns which characters and stage to load.
func TestManifestReachesSpectatorsOnly(t *testing.T) {
	store, hostTok, guestTok, specTok := specStore(t)
	room, _ := store.CreateRoom(hostTok, "r", true)
	store.JoinRoom(guestTok, room.ID, false)
	store.JoinRoom(specTok, room.ID, true)

	manifest := []byte(`{"p1":"jubei","p2":"jubei2","stage":"kfm"}`)
	if err := store.PublishManifest(hostTok, manifest); err != nil {
		t.Fatalf("publish: %v", err)
	}

	specPoll, _ := store.Poll(specTok)
	var got map[string]string
	if err := json.Unmarshal(specPoll.Match.Manifest, &got); err != nil {
		t.Fatalf("spectator manifest: %v", err)
	}
	if got["p1"] != "jubei" || got["stage"] != "kfm" {
		t.Fatalf("manifest arrived as %v", got)
	}

	guestPoll, _ := store.Poll(guestTok)
	if len(guestPoll.Match.Manifest) != 0 {
		t.Fatal("the guest received the spectator manifest")
	}
}

// Only the host describes the match; a watcher must not be able to redirect
// what everyone else loads.
func TestOnlyTheHostMayPublishAManifest(t *testing.T) {
	store, hostTok, guestTok, specTok := specStore(t)
	room, _ := store.CreateRoom(hostTok, "r", true)
	store.JoinRoom(guestTok, room.ID, false)
	store.JoinRoom(specTok, room.ID, true)

	if err := store.PublishManifest(specTok, []byte(`{"p1":"evil"}`)); err == nil {
		t.Fatal("a spectator published a manifest")
	}
	if err := store.PublishManifest(guestTok, []byte(`{"p1":"evil"}`)); err == nil {
		t.Fatal("the guest published a manifest")
	}
}

func TestOversizedManifestIsRejected(t *testing.T) {
	store, hostTok, _, _ := specStore(t)
	store.CreateRoom(hostTok, "r", true)

	big := make([]byte, maxManifestBytes+1)
	for i := range big {
		big[i] = 'a'
	}
	if err := store.PublishManifest(hostTok, big); err == nil {
		t.Fatal("an oversized manifest was accepted")
	}
}

// The relay has two slots. Handing one to a spectator would evict the guest.
func TestSpectatorCannotClaimARelaySlot(t *testing.T) {
	store, hostTok, guestTok, specTok := specStore(t)
	room, _ := store.CreateRoom(hostTok, "r", true)
	store.JoinRoom(guestTok, room.ID, false)
	store.JoinRoom(specTok, room.ID, true)

	if _, _, err := store.RelayFor(specTok); err == nil {
		t.Fatal("a spectator was given a relay slot")
	}
	if _, slot, err := store.RelayFor(guestTok); err != nil || slot != 1 {
		t.Fatalf("guest relay slot = %d, err = %v; want slot 1", slot, err)
	}
}

// Watchers coming and going must not disturb the match.
func TestSpectatorLeavingKeepsTheRoom(t *testing.T) {
	store, hostTok, guestTok, specTok := specStore(t)
	room, _ := store.CreateRoom(hostTok, "r", true)
	store.JoinRoom(guestTok, room.ID, false)
	store.JoinRoom(specTok, room.ID, true)

	if err := store.LeaveRoom(specTok); err != nil {
		t.Fatalf("spectator leave: %v", err)
	}
	rooms := store.ListRooms()
	if len(rooms) != 1 {
		t.Fatalf("rooms = %d, want 1", len(rooms))
	}
	if rooms[0].Spectators != 0 || rooms[0].Players != 2 {
		t.Fatalf("room after spectator left: %+v", rooms[0])
	}
	if _, err := store.Poll(guestTok); err != nil {
		t.Fatalf("guest poll after spectator left: %v", err)
	}
}

// When the host leaves the room dissolves, and watchers must be released too,
// or they would be stuck unable to join anything else.
func TestHostLeavingReleasesSpectators(t *testing.T) {
	store, hostTok, guestTok, specTok := specStore(t)
	room, _ := store.CreateRoom(hostTok, "r", true)
	store.JoinRoom(guestTok, room.ID, false)
	store.JoinRoom(specTok, room.ID, true)

	if err := store.LeaveRoom(hostTok); err != nil {
		t.Fatalf("host leave: %v", err)
	}

	other, _ := store.CreateRoom(guestTok, "r2", true)
	if _, err := store.JoinRoom(specTok, other.ID, true); err != nil {
		t.Fatalf("stranded spectator could not join a new room: %v", err)
	}
}

func TestSpectatorSeatsAreCapped(t *testing.T) {
	store := NewStore()
	_, hostTok, _, _ := store.CreateSession("Host", "203.0.113.1", 7500)
	room, _ := store.CreateRoom(hostTok, "r", true)

	for i := 0; i < maxSpectators; i++ {
		_, tok, _, _ := store.CreateSession("W", "203.0.113.9", 7500)
		if _, err := store.JoinRoom(tok, room.ID, true); err != nil {
			t.Fatalf("spectator %d rejected: %v", i, err)
		}
	}
	_, extra, _, _ := store.CreateSession("W", "203.0.113.9", 7500)
	if _, err := store.JoinRoom(extra, room.ID, true); err == nil {
		t.Fatalf("accepted more than %d spectators", maxSpectators)
	}
}
