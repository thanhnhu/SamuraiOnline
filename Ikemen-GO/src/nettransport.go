package main

// The NAT traversal and relay transport used to live here. It moved to
// src/netpath so it can be compiled without cgo: SamuraiTools/netcheck builds
// it into a standalone binary that runs on machines which cannot build the
// engine, which is the only practical way to test hole punching between two
// real hosts.
//
// These aliases keep the engine's call sites unchanged.

import (
	"github.com/ikemen-engine/Ikemen-GO/src/netpath"
	ggpo "github.com/ikemen-engine/ggpo"
)

type (
	NATMode    = netpath.NATMode
	NATSession = netpath.NATSession
	NATResult  = netpath.NATResult
	RelayPlan  = netpath.RelayPlan
)

const (
	NATDirect  = netpath.NATDirect
	NATPunched = netpath.NATPunched
	NATRelayed = netpath.NATRelayed

	relayKeyLen       = netpath.RelayKeyLen
	natPunchTimeout   = netpath.PunchTimeout
	netHandshakeToken = netpath.HandshakeToken
)

var (
	StartNATSession = netpath.StartNATSession
	StopNATSession  = netpath.StopNATSession
	ClearNATResult  = netpath.ClearNATResult
	natActive       = netpath.Active
	natEstablished  = netpath.Established
	natRemote       = netpath.Remote
	dialRelayStream = netpath.DialRelayStream
	netHandshake    = netpath.Handshake
)

func initGGPOConnection(peer *ggpo.Peer, localPort int) {
	netpath.InitGGPOConnection(peer, localPort)
}
