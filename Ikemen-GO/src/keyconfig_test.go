package main

import (
	"os"
	"testing"

	"github.com/veandco/go-sdl2/sdl"
)

// Reads the shipped configuration the same way the engine does and checks the
// bindings actually resolve. A typo in a key name silently becomes K_UNKNOWN,
// which looks like "the controls do nothing" rather than like an error.
func TestPlayerKeyBindingsResolve(t *testing.T) {
	if _, err := os.Stat("../save/config.ini"); err != nil {
		t.Skipf("no config present: %v", err)
	}

	wd, _ := os.Getwd()
	if err := os.Chdir(".."); err != nil {
		t.Fatalf("chdir: %v", err)
	}
	t.Cleanup(func() { _ = os.Chdir(wd) })

	// The name lookup table is built during input setup, not at package init.
	initLUTs()

	cfg, err := loadConfig("save/config.ini")
	if err != nil {
		t.Fatalf("loadConfig: %v", err)
	}

	// Mirrors the button layout of the original PC release: slashes on the
	// lower row, kicks on the upper row.
	for _, tc := range []struct {
		section string
		want    map[string]string
	}{
		{"keys_p1", map[string]string{
			"up": "w", "down": "s", "left": "a", "right": "d",
			"x": "j", "y": "k", "z": "l",
			"a": "u", "b": "i", "c": "o",
		}},
		{"keys_p2", map[string]string{
			"up": "UP", "down": "DOWN", "left": "LEFT", "right": "RIGHT",
			"x": "KP_1", "y": "KP_2", "z": "KP_3",
			"a": "KP_4", "b": "KP_5", "c": "KP_6",
		}},
	} {
		kc, ok := cfg.Keys[tc.section]
		if !ok {
			t.Errorf("%s missing from config", tc.section)
			continue
		}
		got := map[string]string{
			"up": kc.Up, "down": kc.Down, "left": kc.Left, "right": kc.Right,
			"x": kc.X, "y": kc.Y, "z": kc.Z,
			"a": kc.A, "b": kc.B, "c": kc.C,
		}
		for button, wantKey := range tc.want {
			if got[button] != wantKey {
				t.Errorf("%s button %q bound to %q, expected %q",
					tc.section, button, got[button], wantKey)
			}
			if StringToKey(wantKey) == sdl.K_UNKNOWN {
				t.Errorf("%s: key name %q is not recognised by the engine", tc.section, wantKey)
			}
		}
	}
}

// Two players sharing a key means one of them appears to have broken controls.
func TestPlayerKeysDoNotOverlap(t *testing.T) {
	if _, err := os.Stat("../save/config.ini"); err != nil {
		t.Skipf("no config present: %v", err)
	}

	wd, _ := os.Getwd()
	if err := os.Chdir(".."); err != nil {
		t.Fatalf("chdir: %v", err)
	}
	t.Cleanup(func() { _ = os.Chdir(wd) })

	cfg, err := loadConfig("save/config.ini")
	if err != nil {
		t.Fatalf("loadConfig: %v", err)
	}

	owner := map[string]string{}
	for section, kc := range cfg.Keys {
		for _, key := range []string{
			kc.Up, kc.Down, kc.Left, kc.Right,
			kc.A, kc.B, kc.C, kc.X, kc.Y, kc.Z, kc.Start, kc.D, kc.W,
		} {
			if key == "" || key == "Not used" {
				continue
			}
			if prev, clash := owner[key]; clash && prev != section {
				t.Errorf("key %q is bound in both %s and %s", key, prev, section)
			}
			owner[key] = section
		}
	}
}
