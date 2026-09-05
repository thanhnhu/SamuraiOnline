package main

import (
	"strconv"
	"strings"
	"testing"
)

// The whole reason specials.go exists: the source gives every special hurt
// boxes and no attack box, so converting the tables alone produced three moves
// that animated beautifully and could never touch the opponent. Nothing in the
// generated character complained, and neither did the engine.
func TestEverySpecialCanConnect(t *testing.T) {
	src := loadSource(t)
	specials := BuildSpecials(src)
	if len(specials) == 0 {
		t.Fatal("no specials were built")
	}

	byNo := map[int32]Action{}
	for _, a := range BuildActions(src) {
		byNo[a.No] = a
	}

	for _, s := range specials {
		// A projectile move does no damage with the body; its shot does.
		anim := s.Anim
		if s.Projectile {
			anim = animProjectile
		}
		a, ok := byNo[anim]
		if !ok {
			t.Errorf("%s uses animation %d, which is never generated", s.Name, anim)
			continue
		}
		armed := false
		for _, f := range a.Frames {
			if len(f.Attack) > 0 {
				armed = true
				break
			}
		}
		if !armed {
			t.Errorf("%s: animation %d carries no attack box, so the move cannot hit",
				s.Name, anim)
		}
	}
}

// Checking that an animation has some attack box somewhere is not enough. A
// HitDef issued on a frame with no box is not an error anywhere: it simply
// passes through the opponent. A move can then look like it works while quietly
// dropping half its hits, which is what happened when two specials shared one
// animation and the later spec overwrote the earlier one's strike frames.
func TestEveryStrikeFrameIsArmed(t *testing.T) {
	src := loadSource(t)

	byNo := map[int32]Action{}
	for _, a := range BuildActions(src) {
		byNo[a.No] = a
	}

	for _, s := range BuildSpecials(src) {
		if s.Projectile {
			continue
		}
		a, ok := byNo[s.Anim]
		if !ok {
			t.Errorf("%s uses animation %d, which is never generated", s.Name, s.Anim)
			continue
		}
		for _, e := range s.HitElems {
			if e > len(a.Frames) {
				t.Errorf("%s strikes on element %d of animation %d, which has %d frames",
					s.Name, e, s.Anim, len(a.Frames))
				continue
			}
			if len(a.Frames[e-1].Attack) == 0 {
				t.Errorf("%s hits on element %d of animation %d, which carries no attack box",
					s.Name, e, s.Anim)
			}
		}
	}
}

// A quarter-circle finishes on a slash button. Test the normals first and the
// button is consumed before the motion is ever considered, which leaves the
// special unreachable no matter how correct its state is.
func TestSpecialsAreTestedBeforeNormals(t *testing.T) {
	_, attacks, specials := buildForTest(t)
	cmd := WriteCmd(attacks, specials)

	firstNormal := len(cmd)
	for _, a := range attacks {
		if i := strings.Index(cmd, "[State -1, "+a.Name+"]"); i >= 0 && i < firstNormal {
			firstNormal = i
		}
	}
	for _, s := range specials {
		i := strings.Index(cmd, "[State -1, "+s.Name+"]")
		if i < 0 {
			t.Errorf("%s has no entry in Statedef -1", s.Name)
			continue
		}
		if i > firstNormal {
			t.Errorf("%s is tested after the normals, so its motion can never fire", s.Name)
		}
	}
}

// A trigger naming a command that was never defined is not an error anywhere:
// it is simply never true. That is exactly how the "c" button stayed dead.
func TestEverySpecialMotionIsDefined(t *testing.T) {
	_, attacks, specials := buildForTest(t)
	cmd := WriteCmd(attacks, specials)

	for _, s := range specials {
		if !strings.Contains(cmd, "\nname = \""+s.Command+"\"\n") {
			t.Errorf("%s uses motion %q, which has no [Command] block", s.Name, s.Command)
		}
	}
}

// Strike frames are chosen by hand, so they must land on frames the animation
// really has and that have some reach past the body to arm.
func TestSpecialStrikeFramesAreArmed(t *testing.T) {
	src := loadSource(t)

	for _, s := range BuildSpecials(src) {
		if s.Projectile {
			continue
		}
		anim := src.Anim(s.Source)
		if anim == nil {
			t.Errorf("%s names source animation %q, which does not exist", s.Name, s.Source)
			continue
		}
		if len(s.HitElems) == 0 {
			t.Errorf("%s has no strike frames", s.Name)
		}
		for _, e := range s.HitElems {
			if e < 1 || e > len(anim.Frames) {
				t.Errorf("%s strikes on element %d but the animation has %d frames",
					s.Name, e, len(anim.Frames))
				continue
			}
			if _, ok := bladeBox(anim.Frames[e-1]); !ok {
				t.Errorf("%s element %d reaches no further than the body", s.Name, e)
			}
		}
	}
}

// The shot's art is transcribed from a different file to the rest, so a typo
// here would surface only as an invisible projectile at runtime.
func TestProjectileArtIsGenerated(t *testing.T) {
	src := loadSource(t)

	wantsShot := false
	for _, s := range BuildSpecials(src) {
		if s.Projectile {
			wantsShot = true
		}
	}
	if !wantsShot {
		t.Skip("this character has no projectile special")
	}

	byNo := map[int32]Action{}
	for _, a := range BuildActions(src) {
		byNo[a.No] = a
	}
	for _, n := range []int32{animProjectile, animProjectileImpact} {
		frames := byNo[n].Frames
		if len(frames) == 0 {
			t.Errorf("animation %d is empty, so the shot would be invisible", n)
			continue
		}
		for i, f := range frames {
			if f.Src.W <= 0 || f.Src.H <= 0 {
				t.Errorf("animation %d frame %d has an empty source rect %+v", n, i, f.Src)
			}
			if f.Duration <= 0 {
				t.Errorf("animation %d frame %d never advances", n, i)
			}
		}
	}
}

// Every special the command file can reach needs a state to land in.
func TestEverySpecialHasAState(t *testing.T) {
	src, attacks, specials := buildForTest(t)
	cns := WriteCns(src, attacks, specials)

	for _, s := range specials {
		if !strings.Contains(cns, "[Statedef "+strconv.Itoa(int(s.State))+"]") {
			t.Errorf("%s has no [Statedef %d]", s.Name, s.State)
		}
	}
}
