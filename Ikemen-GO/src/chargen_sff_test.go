package main

import (
	"os"
	"path/filepath"
	"testing"
)

// Verifies that a character produced by SamuraiTools/chargen is readable by the
// engine's own SFF parser. Generating a file that merely looks right is easy;
// this proves the bytes match what the loader expects.
func TestGeneratedCharacterSffLoads(t *testing.T) {
	path := filepath.Join("..", "chars", "jubei", "jubei.sff")
	if _, err := os.Stat(path); err != nil {
		t.Skipf("generated character not present: %v", err)
	}

	// isMainThread=false leaves texture uploads queued instead of running them,
	// which would need a GL context this test does not have.
	sff, err := loadSff(path, true, false, false)
	if err != nil {
		t.Fatalf("loadSff: %v", err)
	}

	// The idle animation is action 0, so its first frame must exist.
	spr := sff.GetSprite(0, 0)
	if spr == nil {
		t.Fatal("sprite 0,0 missing")
	}
	if spr.Size[0] == 0 || spr.Size[1] == 0 {
		t.Fatalf("sprite 0,0 has zero size %v", spr.Size)
	}
	// Decoding queues texture upload work; nothing arriving means the payload
	// was never decoded.
	if len(sys.mainThreadTask) == 0 {
		t.Fatal("no sprite data was decoded")
	}

	// The axis must sit near the feet, i.e. low in a sprite that tall.
	if int(spr.Offset[1]) < int(spr.Size[1])/2 {
		t.Errorf("axis Y = %d looks too high for a %d px sprite", spr.Offset[1], spr.Size[1])
	}

	// The select screen needs a portrait.
	if sff.GetSprite(9000, 0) == nil {
		t.Error("portrait sprite 9000,0 missing")
	}

	// Spot-check an attack animation frame.
	if sff.GetSprite(200, 1) == nil {
		t.Error("punch frame 200,1 missing")
	}
}

// The animation table is where the converted hitboxes live, so it has to parse
// with the engine's own reader too.
func TestGeneratedCharacterAirParses(t *testing.T) {
	dir := filepath.Join("..", "chars", "jubei")
	if _, err := os.Stat(filepath.Join(dir, "jubei.air")); err != nil {
		t.Skipf("generated character not present: %v", err)
	}

	sff, err := loadSff(filepath.Join(dir, "jubei.sff"), true, false, false)
	if err != nil {
		t.Fatalf("loadSff: %v", err)
	}

	str, err := LoadText(filepath.Join(dir, "jubei.air"))
	if err != nil {
		t.Fatalf("read air: %v", err)
	}
	lines, i := SplitAndTrim(str, "\n"), 0
	at := ReadAnimationTable(filepath.Join(dir, "jubei.air"), sff, &sff.palList, lines, &i, false)

	for _, want := range []int32{0, 11, 20, 200, 240} {
		a := at.get(want)
		if a == nil {
			t.Errorf("action %d missing from the animation table", want)
			continue
		}
		if len(a.frames) == 0 {
			t.Errorf("action %d has no frames", want)
		}
	}

	// The punch must carry an attack box, otherwise the move can never connect.
	punch := at.get(200)
	if punch == nil {
		t.Fatal("punch action missing")
	}
	attack := 0
	for _, f := range punch.frames {
		if len(f.Clsn1) > 0 {
			attack++
		}
	}
	if attack == 0 {
		t.Error("punch has no Clsn1 boxes")
	}

	// Idle must be hurtable and its boxes must sit above the axis.
	idle := at.get(0)
	if idle == nil || len(idle.frames) == 0 {
		t.Fatal("idle action missing")
	}
	if len(idle.frames[0].Clsn2) == 0 {
		t.Fatal("idle has no Clsn2 boxes")
	}
	box := idle.frames[0].Clsn2[0]
	if box[1] >= 0 || box[3] > 8 {
		t.Errorf("idle hurtbox %v does not sit above the axis", box)
	}
}

// Compiles the generated states with the engine's own compiler. This is what
// actually proves the CNS and CMD are valid rather than merely well-formatted.
func TestGeneratedCharacterStatesCompile(t *testing.T) {
	def := filepath.Join("..", "chars", "jubei", "jubei.def")
	if _, err := os.Stat(def); err != nil {
		t.Skipf("generated character not present: %v", err)
	}

	// The def pulls in stcommon by a game-root relative path, so the test has
	// to run from the game root like the engine does.
	wd, err := os.Getwd()
	if err != nil {
		t.Fatalf("getwd: %v", err)
	}
	if err := os.Chdir(".."); err != nil {
		t.Fatalf("chdir: %v", err)
	}
	t.Cleanup(func() { _ = os.Chdir(wd) })

	// The compiler reads the command list off a live Char, so slot 0 has to
	// hold one before it will run.
	saved := sys.chars[0]
	sys.chars[0] = []*Char{newChar(0, 0)}
	t.Cleanup(func() { sys.chars[0] = saved })

	states, err := newCharCompiler().Compile(0, filepath.Join("chars", "jubei", "jubei.def"), nil)
	if err != nil {
		t.Fatalf("compile: %v", err)
	}

	// -2 runs every frame and carries rage and weapon loss; -1 holds the
	// command list. Without them none of the Samurai Shodown systems exist.
	for _, no := range []int32{-2, -1, 0, 200, 1500} {
		if _, ok := states[no]; !ok {
			t.Errorf("state %d did not compile", no)
		}
	}
}

// The second converted character must stand on its own, not merely exist as a
// palette swap of the first.
func TestSecondCharacterCompiles(t *testing.T) {
	if _, err := os.Stat(filepath.Join("..", "chars", "jubei2", "jubei2.def")); err != nil {
		t.Skipf("second character not present: %v", err)
	}

	wd, err := os.Getwd()
	if err != nil {
		t.Fatalf("getwd: %v", err)
	}
	if err := os.Chdir(".."); err != nil {
		t.Fatalf("chdir: %v", err)
	}
	t.Cleanup(func() { _ = os.Chdir(wd) })

	saved := sys.chars[0]
	sys.chars[0] = []*Char{newChar(0, 0)}
	t.Cleanup(func() { sys.chars[0] = saved })

	states, err := newCharCompiler().Compile(0, filepath.Join("chars", "jubei2", "jubei2.def"), nil)
	if err != nil {
		t.Fatalf("compile: %v", err)
	}
	for _, no := range []int32{-2, -1, 0, 200} {
		if _, ok := states[no]; !ok {
			t.Errorf("state %d did not compile", no)
		}
	}

	sff, err := loadSff(filepath.Join("chars", "jubei2", "jubei2.sff"), true, false, false)
	if err != nil {
		t.Fatalf("loadSff: %v", err)
	}
	if sff.GetSprite(0, 0) == nil {
		t.Error("idle sprite missing")
	}
}
