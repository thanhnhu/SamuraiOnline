// netcheck exercises the network path between two peers using the engine's own
// NAT traversal and relay code, without needing the engine itself.
//
// The engine links SDL, FFmpeg and GTK, so its test binary needs 200-odd shared
// libraries and cannot be dropped onto an arbitrary machine. This one imports
// only src/netpath, which is pure Go, so it cross-compiles to a single static
// file and runs anywhere:
//
//	GOOS=windows GOARCH=amd64 CGO_ENABLED=0 go build -o netcheck.exe .
//
// Run it on both machines against the same lobby and room name:
//
//	netcheck -lobby http://192.168.2.210:8080 -role host
//	netcheck -lobby http://192.168.2.210:8080 -role guest
//
// It reports whether the peers punched through or fell back to the relay, and
// proves the match-setup stream carries bytes intact.
package main

import (
	"bytes"
	"encoding/hex"
	"encoding/json"
	"errors"
	"flag"
	"fmt"
	"io"
	"net"
	"net/http"
	"os"
	"strconv"
	"time"

	"github.com/ikemen-engine/Ikemen-GO/src/netpath"
)

const (
	requestTimeout = 8 * time.Second
	pollInterval   = time.Second
	maxResponse    = 1 << 20
)

type client struct {
	base  string
	token string
	hc    *http.Client
}

func (c *client) do(method, path string, body, out any) error {
	var buf bytes.Buffer
	if body != nil {
		if err := json.NewEncoder(&buf).Encode(body); err != nil {
			return err
		}
	}
	req, err := http.NewRequest(method, c.base+path, &buf)
	if err != nil {
		return err
	}
	if body != nil {
		req.Header.Set("Content-Type", "application/json")
	}
	if c.token != "" {
		req.Header.Set("Authorization", "Bearer "+c.token)
	}
	resp, err := c.hc.Do(req)
	if err != nil {
		return fmt.Errorf("lobby unreachable: %w", err)
	}
	defer resp.Body.Close()

	reader := io.LimitReader(resp.Body, maxResponse)
	if resp.StatusCode < 200 || resp.StatusCode > 299 {
		var payload struct {
			Error string `json:"error"`
		}
		_ = json.NewDecoder(reader).Decode(&payload)
		return fmt.Errorf("lobby returned %d: %s", resp.StatusCode, payload.Error)
	}
	if out == nil {
		return nil
	}
	return json.NewDecoder(reader).Decode(out)
}

type room struct {
	ID       string `json:"id"`
	Name     string `json:"name"`
	Players  int    `json:"players"`
	Capacity int    `json:"capacity"`
}

type match struct {
	Role     string `json:"role"`
	State    string `json:"state"`
	PeerName string `json:"peerName"`
	HostAddr string `json:"hostAddr"`
	HostPort int    `json:"hostPort"`
	PeerUDP  string `json:"peerUdp"`
}

type poll struct {
	Match *match `json:"match"`
}

func main() {
	lobby := flag.String("lobby", "", "base URL of the lobby server, e.g. http://192.168.2.210:8080")
	role := flag.String("role", "", "host or guest")
	roomName := flag.String("room", "netcheck", "room name both peers agree on")
	name := flag.String("name", "", "player name shown in the lobby; defaults to the role")
	port := flag.Int("port", 7500, "TCP port advertised to the lobby")
	wait := flag.Duration("wait", 90*time.Second, "how long to wait for the other peer")
	flag.Parse()

	if *lobby == "" || (*role != "host" && *role != "guest") {
		flag.Usage()
		os.Exit(2)
	}
	if *name == "" {
		*name = "netcheck-" + *role
	}

	if err := run(*lobby, *role, *roomName, *name, *port, *wait); err != nil {
		fmt.Fprintf(os.Stderr, "\nFAILED: %v\n", err)
		os.Exit(1)
	}
}

func step(format string, a ...any) func() {
	start := time.Now()
	fmt.Printf("-> "+format+"\n", a...)
	return func() { fmt.Printf("   done in %s\n", time.Since(start).Round(time.Millisecond)) }
}

func run(base, role, roomName, name string, port int, wait time.Duration) error {
	c := &client{base: base, hc: &http.Client{Timeout: requestTimeout}}

	done := step("register with the lobby at %s", base)
	var session struct {
		Token     string `json:"token"`
		UDPToken  string `json:"udpToken"`
		RelayHost string `json:"relayHost"`
		RelayPort int    `json:"relayPort"`
	}
	if err := c.do(http.MethodPost, "/api/session",
		map[string]any{"name": name, "port": port}, &session); err != nil {
		return err
	}
	c.token = session.Token
	done()

	if session.RelayHost == "" || session.RelayPort == 0 {
		return errors.New("the lobby advertised no relay address, so there is nothing to punch towards")
	}

	// The match socket must be the same one the lobby saw, or the mapping it
	// reports belongs to a port the game never uses.
	done = step("open the match socket and announce it to %s:%d", session.RelayHost, session.RelayPort)
	nat, err := netpath.StartNATSession(0,
		net.JoinHostPort(session.RelayHost, strconv.Itoa(session.RelayPort)), session.UDPToken)
	if err != nil {
		return err
	}
	defer netpath.StopNATSession()
	defer netpath.ClearNATResult()

	deadline := time.Now().Add(15 * time.Second)
	for nat.Observed() == "" {
		if time.Now().After(deadline) {
			return errors.New("the lobby never told us our own address: UDP is not getting through")
		}
		time.Sleep(200 * time.Millisecond)
	}
	done()
	fmt.Printf("   our address as the lobby sees it: %s\n", nat.Observed())

	done = step("take the %s seat in room %q", role, roomName)
	if role == "host" {
		if err := c.do(http.MethodPost, "/api/rooms/create",
			map[string]any{"name": roomName, "rollback": true}, nil); err != nil {
			return err
		}
	} else {
		id, err := waitForRoom(c, roomName, wait)
		if err != nil {
			return err
		}
		if err := c.do(http.MethodPost, "/api/rooms/join",
			map[string]any{"roomId": id, "spectator": false}, nil); err != nil {
			return err
		}
	}

	m, err := waitForPeer(c, wait)
	if err != nil {
		return err
	}
	done()
	fmt.Printf("   peer=%q peerUdp=%s\n", m.PeerName, m.PeerUDP)
	if m.PeerUDP == "" {
		return errors.New("the lobby never observed the peer's address")
	}

	// Ask for relay credentials before punching, so the fallback is ready.
	var alloc struct {
		Host    string `json:"host"`
		Port    int    `json:"port"`
		TCPPort int    `json:"tcpPort"`
		Key     string `json:"key"`
		Slot    int    `json:"slot"`
	}
	var plan *netpath.RelayPlan
	if err := c.do(http.MethodPost, "/api/relay/allocate", nil, &alloc); err != nil {
		fmt.Printf("   no relay offered (%v); direct only\n", err)
	} else if raw, err := hex.DecodeString(alloc.Key); err == nil && len(raw) == netpath.RelayKeyLen {
		var k [netpath.RelayKeyLen]byte
		copy(k[:], raw)
		plan = &netpath.RelayPlan{
			Host: alloc.Host, Port: alloc.Port, TCPPort: alloc.TCPPort, Key: k, Slot: alloc.Slot,
		}
	}

	done = step("punch a hole to %s", m.PeerUDP)
	res, err := nat.Establish(m.PeerUDP, plan)
	if err != nil {
		return err
	}
	done()

	switch res.Mode {
	case netpath.NATPunched:
		fmt.Println("\nRESULT: PUNCHED - the peers reached each other directly")
	case netpath.NATRelayed:
		fmt.Println("\nRESULT: RELAYED - punching failed, gameplay would bounce off the lobby")
	default:
		fmt.Printf("\nRESULT: %s\n", res.Mode)
	}

	if plan == nil || plan.TCPPort == 0 {
		fmt.Println("no TCP relay on offer; skipping the setup-stream check")
		return nil
	}

	done = step("carry the match-setup stream through the relay")
	conn, err := netpath.DialRelayStream(plan, 15*time.Second)
	if err != nil {
		return fmt.Errorf("relay dial: %w", err)
	}
	defer conn.Close()
	if err := netpath.Handshake(conn, role == "host", 45*time.Second); err != nil {
		return fmt.Errorf("relayed handshake: %w", err)
	}
	done()

	// The handshake alone proves too little: the stream has to survive payload.
	payload := []byte{0x00, 0xC7, 0xFF, 0x42}
	if role == "host" {
		if _, err := conn.Write(payload); err != nil {
			return fmt.Errorf("write after handshake: %w", err)
		}
		fmt.Println("setup stream: sent a probe payload")
	} else {
		got := make([]byte, len(payload))
		_ = conn.SetReadDeadline(time.Now().Add(15 * time.Second))
		if _, err := io.ReadFull(conn, got); err != nil {
			return fmt.Errorf("read after handshake: %w", err)
		}
		if !bytes.Equal(got, payload) {
			return fmt.Errorf("payload corrupted: got % x, want % x", got, payload)
		}
		fmt.Println("setup stream: payload arrived intact")
	}

	fmt.Printf("\nOK role=%s mode=%s\n", role, res.Mode)
	return nil
}

func waitForRoom(c *client, name string, wait time.Duration) (string, error) {
	deadline := time.Now().Add(wait)
	for {
		var list struct {
			Rooms []room `json:"rooms"`
		}
		if err := c.do(http.MethodGet, "/api/rooms", nil, &list); err != nil {
			return "", err
		}
		for _, r := range list.Rooms {
			if r.Name == name && r.Players < r.Capacity {
				return r.ID, nil
			}
		}
		if time.Now().After(deadline) {
			return "", fmt.Errorf("no room called %q showed up; start the host first", name)
		}
		time.Sleep(pollInterval)
	}
}

func waitForPeer(c *client, wait time.Duration) (*match, error) {
	deadline := time.Now().Add(wait)
	for {
		var p poll
		if err := c.do(http.MethodPost, "/api/poll", nil, &p); err != nil {
			return nil, err
		}
		if p.Match != nil && p.Match.State == "ready" {
			return p.Match, nil
		}
		if time.Now().After(deadline) {
			return nil, errors.New("the other peer never joined")
		}
		time.Sleep(pollInterval)
	}
}
