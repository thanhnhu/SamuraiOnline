package main

import (
	"os"
	"path/filepath"
	"strings"
	"testing"
)

// Basic movement is driven by the engine itself (char.go, "Perform basic
// actions") against the states in data/common1.cns.zss. A character that
// redefines those states silently replaces working code with its own.
//
// Whether the character *has* every animation those states name is checked in
// commonanims_test.go, which reads the numbers out of common1 rather than
// repeating them here.

// commonMovementStates are owned by common1.cns.zss.
var commonMovementStates = []string{"0", "10", "11", "12", "20", "40", "50", "52"}

func readCharFile(t *testing.T, char, file string) string {
	t.Helper()
	path := filepath.Join("..", "chars", char, file)
	b, err := os.ReadFile(path)
	if err != nil {
		t.Skipf("generated character not present: %v", err)
	}
	return string(b)
}

func TestCharacterDoesNotOverrideCommonMovementStates(t *testing.T) {
	for _, char := range []string{"jubei", "jubei2"} {
		cns := readCharFile(t, char, char+".cns")
		for _, no := range commonMovementStates {
			marker := "[Statedef " + no + "]"
			if strings.Contains(cns, marker) {
				t.Errorf("%s redefines %s, which overrides the engine's working version",
					char, marker)
			}
		}
	}
}

// Crouch, landing and guard-start transitions wait on animTime, so an
// animation that never ends leaves the character stuck in that state.
func TestTransitionAnimationsTerminate(t *testing.T) {
	for _, char := range []string{"jubei", "jubei2"} {
		air := readCharFile(t, char, char+".air")
		for _, no := range []int{10, 12, 40, 47, 120, 121, 122, 140, 141, 142} {
			frames := actionFrameLines(air, no)
			if len(frames) == 0 {
				t.Errorf("%s: action %d has no frames", char, no)
				continue
			}
			for _, line := range frames {
				parts := strings.Split(line, ",")
				dur := strings.TrimSpace(parts[len(parts)-1])
				if dur == "-1" {
					t.Errorf("%s: action %d holds forever, so animTime never reaches 0", char, no)
				}
			}
		}
	}
}

func actionFrameLines(air string, no int) []string {
	marker := "[Begin Action " + itoa(no) + "]"
	i := strings.Index(air, marker)
	if i < 0 {
		return nil
	}
	rest := air[i+len(marker):]
	if end := strings.Index(rest, "[Begin Action"); end >= 0 {
		rest = rest[:end]
	}
	var out []string
	for _, line := range strings.Split(rest, "\n") {
		line = strings.TrimSpace(line)
		if line == "" || strings.HasPrefix(line, ";") || strings.HasPrefix(line, "Clsn") ||
			strings.HasPrefix(line, "[") || strings.Contains(line, "=") {
			continue
		}
		if strings.Count(line, ",") >= 4 {
			out = append(out, line)
		}
	}
	return out
}

func itoa(n int) string {
	if n == 0 {
		return "0"
	}
	var b [8]byte
	i := len(b)
	for n > 0 {
		i--
		b[i] = byte('0' + n%10)
		n /= 10
	}
	return string(b[i:])
}
