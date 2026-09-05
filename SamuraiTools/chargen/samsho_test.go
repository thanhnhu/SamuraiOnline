package main

import (
	"strings"
	"testing"
)

func buildForTest(t *testing.T) (*Source, []AttackSpec, []SpecialSpec) {
	t.Helper()
	src := loadSource(t)
	return src, BuildAttacks(src), BuildSpecials(src)
}

// The source's damage field is animation-scoped and overwritten by every
// PushBack, so a stray per-frame value must not leak into the character.
func TestDamageIgnoresDeadPerFrameValues(t *testing.T) {
	_, attacks, _ := buildForTest(t)

	for _, a := range attacks {
		if a.Damage <= 0 || a.Damage > 120 {
			t.Errorf("%s has implausible damage %d", a.Name, a.Damage)
		}
	}

	var punch *AttackSpec
	for i := range attacks {
		if attacks[i].Name == "punch" {
			punch = &attacks[i]
		}
	}
	if punch == nil {
		t.Fatal("punch not built")
	}
	// The punch table carries a stray 212 on one frame while every other frame
	// says 12; the original engine only ever used the last value.
	if punch.Damage != 12 {
		t.Errorf("punch damage = %d, expected the animation-scoped 12", punch.Damage)
	}
}

func TestButtonsFollowTheFourButtonLayout(t *testing.T) {
	_, attacks, specials := buildForTest(t)

	// Slashes on x/y/z, kicks on a/b/c, mirroring the arcade original's
	// A/B/A+B and C/D/C+D.
	want := map[string]string{
		"punch":       "x",
		"midpunch":    "y",
		"strongpunch": "z",
		"kick":        "a",
		"midkick":     "b",
		"strongkick":  "c",
	}
	for _, a := range attacks {
		if w, ok := want[a.Name]; ok && a.Command != w {
			t.Errorf("%s bound to %q, expected %q", a.Name, a.Command, w)
		}
	}

	cmd := WriteCmd(attacks, specials)
	if !strings.Contains(cmd, `command = "z" || command = "x+y"`) {
		t.Error("heavy slash is missing its two-button shortcut")
	}
	if !strings.Contains(cmd, `command = "c" || command = "a+b"`) {
		t.Error("heavy kick is missing its two-button shortcut")
	}
	if !strings.Contains(cmd, `command = "rearm"`) {
		t.Error("sword recovery has no command")
	}
}

// A button an attack asks for but the command file never defines is a dead
// key: pressing it does nothing, and neither the engine nor the character
// complains. That happened to "c", and the only symptom was in the hands.
func TestEveryAttackButtonHasACommand(t *testing.T) {
	_, attacks, specials := buildForTest(t)
	cmd := WriteCmd(attacks, specials)

	seen := map[string]bool{}
	for _, a := range attacks {
		if seen[a.Command] {
			continue
		}
		seen[a.Command] = true
		if !strings.Contains(cmd, "\nname = \""+a.Command+"\"\n") {
			t.Errorf("attack %q uses button %q, which has no [Command] block",
				a.Name, a.Command)
		}
	}
}

// The header documents the layout for whoever reads the generated file. It
// disagreed with the code once already, and the code was the wrong one.
func TestCmdHeaderMatchesTheBindings(t *testing.T) {
	_, attacks, specials := buildForTest(t)
	cmd := WriteCmd(attacks, specials)

	byName := map[string]string{}
	for _, a := range attacks {
		byName[a.Name] = a.Command
	}
	for _, c := range []struct{ name, documented string }{
		{"kick", "a"},
		{"midkick", "b"},
		{"strongkick", "c"},
		{"punch", "x"},
		{"midpunch", "y"},
		{"strongpunch", "z"},
	} {
		if !strings.Contains(cmd, ";   "+c.documented+" ") &&
			!strings.Contains(cmd, ";   "+c.documented+" /") {
			t.Errorf("header does not document button %q", c.documented)
		}
		if got := byName[c.name]; got != "" && got != c.documented {
			t.Errorf("%s is documented as %q but bound to %q", c.name, c.documented, got)
		}
	}
}

func TestDisarmedAnimationsAreOnlyUsedWhenTheyExist(t *testing.T) {
	src, attacks, _ := buildForTest(t)

	for _, a := range attacks {
		dis, ok := DisarmedFor(src, a.Anim)
		if a.HasDisarmed != ok {
			t.Errorf("%s: HasDisarmed=%v but the source says %v", a.Name, a.HasDisarmed, ok)
		}
		if ok && a.DisarmedAnim != dis {
			t.Errorf("%s: disarmed anim %d, expected %d", a.Name, a.DisarmedAnim, dis)
		}
	}

	// The medium slash has no weaponless art, so it must not claim one.
	for _, a := range attacks {
		if a.Name == "midpunch" && a.HasDisarmed {
			t.Error("midpunch should have no disarmed variant")
		}
	}
}

func TestPowerIsPaidToTheDefender(t *testing.T) {
	src, attacks, specials := buildForTest(t)
	cns := WriteCns(src, attacks, specials)

	if !strings.Contains(cns, "getpower = 0, 0") {
		t.Error("the attacker should gain no POW")
	}
	if !strings.Contains(cns, "givepower =") {
		t.Error("the defender should be paid POW")
	}
	if !strings.Contains(cns, "[Statedef 1500]") {
		t.Error("sword recovery state missing")
	}
	if !strings.Contains(cns, "type = AttackMulSet") {
		t.Error("rage and disarm damage scaling missing")
	}
}

// The disarm roll keys off a damage threshold; a value taken from the stray
// 212 would make weapon loss impossible.
func TestDisarmThresholdIsReachable(t *testing.T) {
	src, attacks, specials := buildForTest(t)
	cns := WriteCns(src, attacks, specials)

	maxDamage := 0
	for _, a := range attacks {
		if a.Damage > maxDamage {
			maxDamage = a.Damage
		}
	}
	for _, line := range strings.Split(cns, "\n") {
		if !strings.Contains(line, "GetHitVar(damage) >=") {
			continue
		}
		var got int
		if _, err := fmtSscan(line, &got); err != nil {
			t.Fatalf("could not read threshold from %q", line)
		}
		if got > maxDamage {
			t.Errorf("disarm threshold %d exceeds the strongest attack %d", got, maxDamage)
		}
		return
	}
	t.Error("no disarm threshold found")
}

func fmtSscan(line string, out *int) (int, error) {
	idx := strings.LastIndex(line, ">=")
	if idx < 0 {
		return 0, errNoThreshold
	}
	var n int
	for _, r := range strings.TrimSpace(line[idx+2:]) {
		if r < '0' || r > '9' {
			break
		}
		n = n*10 + int(r-'0')
	}
	*out = n
	return 1, nil
}

var errNoThreshold = errString("no threshold")

type errString string

func (e errString) Error() string { return string(e) }
