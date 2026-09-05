package main

import (
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func loadSource(t *testing.T) *Source {
	t.Helper()
	path := filepath.Join("..", "..", "..", "..",
		"SamuraiShodown-XMatStudio", "SamuraiShodown-1.00", "ModulePlayer.cpp")
	if p := os.Getenv("CHARGEN_SOURCE"); p != "" {
		path = p
	}
	data, err := os.ReadFile(path)
	if err != nil {
		t.Skipf("source not available: %v", err)
	}
	src, err := ParseSource(string(data))
	if err != nil {
		t.Fatalf("parse: %v", err)
	}
	return src
}

func TestParsesTheIdleAnimationExactly(t *testing.T) {
	src := loadSource(t)

	idle := src.Anim("idle")
	if idle == nil {
		t.Fatal("idle animation missing")
	}
	if len(idle.Frames) != 9 {
		t.Fatalf("idle has %d frames, expected 9", len(idle.Frames))
	}

	first := idle.Frames[0]
	if first.Src != (Rect{1, 11, 96, 106}) {
		t.Fatalf("first source rect = %+v", first.Src)
	}
	if first.Duration != 8 {
		t.Fatalf("first duration = %d", first.Duration)
	}
	if first.PivotX != 49 || first.PivotY != 1 {
		t.Fatalf("pivot = (%d,%d)", first.PivotX, first.PivotY)
	}
	if len(first.Hurt) != 2 || len(first.Attack) != 0 {
		t.Fatalf("idle should be 2 hurtboxes and no attack box, got %d/%d",
			len(first.Hurt), len(first.Attack))
	}
	if first.Hurt[0] != (Rect{25, 0, 40, 75}) {
		t.Fatalf("first hurtbox = %+v", first.Hurt[0])
	}
}

// The punch mixes frames with and without an attack box; that distinction is
// the whole point of keeping per-call collider arrays.
func TestPunchHasAttackBoxOnlyOnActiveFrames(t *testing.T) {
	src := loadSource(t)

	punch := src.Anim("punch")
	if punch == nil {
		t.Fatal("punch animation missing")
	}
	if len(punch.Frames) != 5 {
		t.Fatalf("punch has %d frames, expected 5", len(punch.Frames))
	}

	var active []int
	for i, f := range punch.Frames {
		if len(f.Attack) > 0 {
			active = append(active, i)
		}
	}
	if len(active) != 2 || active[0] != 1 || active[1] != 2 {
		t.Fatalf("expected frames 1 and 2 to be active, got %v", active)
	}
	if punch.Frames[1].Attack[0] != (Rect{40, 30, 80, 30}) {
		t.Fatalf("attack box = %+v", punch.Frames[1].Attack[0])
	}
	if punch.Frames[0].Damage != 12 {
		t.Fatalf("damage = %d", punch.Frames[0].Damage)
	}
}

func TestFindsAWideRangeOfAnimations(t *testing.T) {
	src := loadSource(t)

	if len(src.Anims) < 40 {
		t.Fatalf("only %d animations parsed, expected the full set", len(src.Anims))
	}
	for _, want := range []string{"idle", "forward", "backward", "jump", "crouch",
		"punch", "kick", "midpunch", "strongpunch", "fall", "getUp"} {
		if src.Anim(want) == nil {
			t.Errorf("animation %q missing", want)
		}
	}

	// Disarmed variants carry the weapon-loss mechanic and must survive parsing.
	nw := 0
	for _, a := range src.Anims {
		if strings.HasPrefix(a.Name, "NW") {
			nw++
		}
	}
	if nw == 0 {
		t.Error("no disarmed (NW*) animations parsed")
	}
}

func TestEveryFrameHasSaneGeometry(t *testing.T) {
	src := loadSource(t)

	for _, a := range src.Anims {
		for i, f := range a.Frames {
			if f.Src.W <= 0 || f.Src.H <= 0 {
				t.Errorf("%s frame %d: empty source rect %+v", a.Name, i, f.Src)
			}
			if f.Duration <= 0 {
				t.Errorf("%s frame %d: duration %d", a.Name, i, f.Duration)
			}
		}
	}
}
