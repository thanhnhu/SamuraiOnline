package netpath

import (
	"fmt"
	"io"
	"net"
	"time"
)

// HandshakeToken is exchanged in both directions before a match-setup stream is
// trusted. The relayed path needs a much longer window than the direct one,
// because the relay only starts forwarding once the second peer shows up.
const HandshakeToken = "IKEMENGO"

func Handshake(conn net.Conn, sendFirst bool, timeout time.Duration) error {
	if err := conn.SetDeadline(time.Now().Add(timeout)); err != nil {
		return err
	}
	if sendFirst {
		if _, err := conn.Write([]byte(HandshakeToken)); err != nil {
			return err
		}
	}
	buf := make([]byte, len(HandshakeToken))
	if _, err := io.ReadFull(conn, buf); err != nil {
		return err
	}
	if string(buf) != HandshakeToken {
		return fmt.Errorf("unexpected handshake %q", buf)
	}
	if !sendFirst {
		if _, err := conn.Write([]byte(HandshakeToken)); err != nil {
			return err
		}
	}
	return conn.SetDeadline(time.Time{})
}
