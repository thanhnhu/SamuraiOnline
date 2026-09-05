package main

import (
	"fmt"
	"strings"
)

// Special moves need data the source tables do not carry.
//
// Every special in ModulePlayer.cpp declares COLLIDER_PLAYER boxes and nothing
// else, never the COLLIDER_PLAYER_ATTACK that the normals use. The SDL port did
// the damage in code instead: the tornado spawned a tornadoHao particle
// ("if (tornading)"), and the rush move merely ran a 150-tick timer. Converting
// the animation tables alone therefore produces specials that play and can
// never connect, which is what the first pass shipped.
//
// Two things are supplied here. The projectile art is transcribed from
// ModuleParticles.cpp, where it sits on the character's own sheet. The melee
// attack boxes are derived from each frame's hurt box by bladeBox, so they
// follow the art rather than being typed in by hand.

// Action numbers for the projectile. The source parks nothing between the
// specials at 1000..1100 and the sword clash at 1200.
const (
	animProjectile       int32 = 1050
	animProjectileImpact int32 = 1051
)

// Motion inputs. The original mapped its projectile to a quarter-circle
// forward, which the README records as "Down, DownRight + Right".
const (
	cmdQcfSlash   = "qcf_slash"
	cmdQcbSlash   = "qcb_slash"
	cmdSuperSlash = "super_slash"
)

// projectileFrames is tornadoHao, projectileImpactFrames is tornadoHaoImpact.
// A projectile hits with its own animation's Clsn1, so unlike the character
// frames these carry an attack box covering the whole sprite.
var projectileFrames = []Frame{
	{
		Src: Rect{1012, 1740, 75, 39}, PivotX: 31, PivotY: 2, Duration: 4,
		Attack: []Rect{{X: 0, Y: 0, W: 75, H: 39}},
	},
	{
		Src: Rect{1094, 1740, 75, 39}, PivotX: 31, PivotY: 2, Duration: 2,
		Attack: []Rect{{X: 0, Y: 0, W: 75, H: 39}},
	},
}

var projectileImpactFrames = []Frame{
	{Src: Rect{1283, 1769, 34, 18}, PivotX: 17, PivotY: 9, Duration: 3},
	{Src: Rect{1251, 1769, 33, 17}, PivotX: 16, PivotY: 8, Duration: 3},
	{Src: Rect{1316, 1769, 33, 17}, PivotX: 16, PivotY: 8, Duration: 3},
}

// SpecialSpec is one motion-input move.
type SpecialSpec struct {
	State, Anim int32
	Name        string
	// Source is the animation variable in ModulePlayer.cpp.
	Source  string
	Command string
	Damage  int
	// HitElems are the 1-based animation elements that strike, so a rush move
	// hits several times from one animation.
	HitElems []int
	// Projectile moves damage nothing with the body; the spawned shot does it.
	Projectile bool
	SpawnElem  int
	// PowerCost, when set, makes this a super: the gauge must hold that much
	// and the move spends it.
	PowerCost int
}

// specialStates wires the special animations the source ships.
//
// Only the tornado's input is documented: the README gives the projectile as a
// quarter-circle forward. The rush move was reachable in the original but its
// input was a ten-press mash, which is miserable on a pad, so it gets a motion
// instead.
//
// special2 is deliberately absent. It was already unreachable in the original,
// where ST_SECOND_SPECIAL sets firstSpecial by what looks like a copy-paste
// slip, and its art rises to 209 pixels across four frames while the hurt box
// stays 65 tall. Nothing in the data says where that move hits, so shipping it
// would mean inventing a hitbox and calling it a conversion.
//
// Order matters: a double quarter-circle also completes a single one, so the
// super has to be tested first or it can never come out.
var specialStates = []SpecialSpec{
	{
		State: 1020, Anim: 1000, Name: "rageRush", Source: "special",
		Command: cmdSuperSlash, Damage: 16, PowerCost: powRageStart,
		HitElems: []int{3, 5, 7, 9, 11, 13},
	},
	{
		State: 1100, Anim: 1100, Name: "bladeWave", Source: "tornado",
		Command: cmdQcfSlash, Damage: 28, Projectile: true, SpawnElem: 5,
	},
	{
		State: 1000, Anim: 1000, Name: "swordRush", Source: "special",
		Command: cmdQcbSlash, Damage: 10, HitElems: []int{3, 6, 9, 12},
	},
}

// BuildSpecials keeps the moves whose art the sheet actually ships, and drops
// any strike frame the animation is too short to reach.
func BuildSpecials(src *Source) []SpecialSpec {
	var out []SpecialSpec
	for _, s := range specialStates {
		anim := src.Anim(s.Source)
		if anim == nil || len(anim.Frames) == 0 {
			continue
		}
		n := len(anim.Frames)
		spec := s
		spec.HitElems = nil
		for _, e := range s.HitElems {
			if e >= 1 && e <= n {
				spec.HitElems = append(spec.HitElems, e)
			}
		}
		if spec.Projectile {
			if spec.SpawnElem < 1 || spec.SpawnElem > n {
				spec.SpawnElem = 1
			}
		} else if len(spec.HitElems) == 0 {
			continue
		}
		out = append(out, spec)
	}
	return out
}

// bladeBox turns a frame's hurt box into the attack box for a sword sweep: the
// reach is whatever the art extends past the body, which is the blade. Deriving
// it means a frame whose sprite is wider gets a longer box for free, instead of
// one constant standing in for fifteen different poses.
func bladeBox(f Frame) (Rect, bool) {
	if len(f.Hurt) == 0 {
		return Rect{}, false
	}
	body := f.Hurt[0]
	x := body.X + body.W
	w := f.Src.W - x
	if w < 20 {
		// Nothing meaningful sticks out, so this pose is not a strike.
		return Rect{}, false
	}
	return Rect{X: x, Y: body.Y, W: w, H: body.H * 3 / 5}, true
}

// applySpecialAttackBoxes gives the special animations the attack boxes their
// tables never defined, on exactly the frames the generated states strike.
//
// Several moves can share one animation - the rage rush reuses the sword rush's
// frames - so the strike frames are unioned. Keying by animation and taking the
// last spec instead would silently leave half the super's hits unarmed.
func applySpecialAttackBoxes(src *Source, actions []Action) []Action {
	strikes := map[int32]map[int]bool{}
	for _, s := range BuildSpecials(src) {
		if strikes[s.Anim] == nil {
			strikes[s.Anim] = map[int]bool{}
		}
		for _, e := range s.HitElems {
			strikes[s.Anim][e] = true
		}
	}

	for i := range actions {
		elems := strikes[actions[i].No]
		if len(elems) == 0 {
			continue
		}
		frames := append([]Frame(nil), actions[i].Frames...)
		for e := range elems {
			if e < 1 || e > len(frames) {
				continue
			}
			f := frames[e-1]
			box, ok := bladeBox(f)
			if !ok {
				continue
			}
			f.Attack = append(append([]Rect(nil), f.Attack...), box)
			frames[e-1] = f
		}
		actions[i].Frames = frames
	}
	return actions
}

// projectileActions ships the shot's art, but only when the move that fires it
// exists.
func projectileActions(src *Source) []Action {
	a := src.Anim("tornado")
	if a == nil || len(a.Frames) == 0 {
		return nil
	}
	return []Action{
		{No: animProjectile, Name: "projectile", Frames: projectileFrames},
		{No: animProjectileImpact, Name: "projectileImpact", Frames: projectileImpactFrames},
	}
}

// writeSpecialStates emits one state per special.
func writeSpecialStates(b *strings.Builder, specials []SpecialSpec) {
	if len(specials) == 0 {
		return
	}
	b.WriteString("; ----------------------------------------------------------------------------\n")
	b.WriteString("; Specials\n\n")

	for _, s := range specials {
		fmt.Fprintf(b, "; %s\n[Statedef %d]\n", s.Name, s.State)
		fmt.Fprintf(b, `type = S
movetype = A
physics = S
juggle = 2
velset = 0, 0
ctrl = 0
anim = %d
poweradd = %d
sprpriority = 2

`, s.Anim, -s.PowerCost)

		if s.Projectile {
			// Offsets and speed come straight from the spawn call in
			// ModulePlayer.cpp: 18 forward, 44 up, 3 pixels a frame.
			fmt.Fprintf(b, `[State %d, Shot]
type = Projectile
trigger1 = AnimElem = %d
projanim = %d
projhitanim = %d
projremanim = %d
projid = %d
projshadow = 0
offset = 18, -44
velocity = 3, 0
projremove = 1
projremovetime = -1
projhits = 1
projpriority = 1
projsprpriority = 3
attr = S, SP
damage = %d, %d
animtype = Hard
guardflag = MA
hitflag = MAF
pausetime = 0, 12
sparkno = -1
hitsound = 5, 2
guardsound = 6, 0
ground.type = High
ground.slidetime = 12
ground.hittime = 18
ground.velocity = -6
air.velocity = -3, -4
getpower = 0, 0
givepower = %d, %d

`, s.State, s.SpawnElem, animProjectile, animProjectileImpact,
				animProjectileImpact, s.State, s.Damage, s.Damage/6,
				s.Damage*6, s.Damage*2)
		} else {
			// One HitDef re-issued on each strike frame, which is how a
			// multi-hit move works without a helper per hit.
			fmt.Fprintf(b, "[State %d, HitDef]\ntype = HitDef\n", s.State)
			for i, e := range s.HitElems {
				fmt.Fprintf(b, "trigger%d = AnimElem = %d\n", i+1, e)
			}
			fmt.Fprintf(b, `attr = S, SA
damage = %d, %d
animtype = Hard
guardflag = MA
hitflag = MAF
pausetime = 4, 4
sparkno = 2
sparkxy = -10, -60
hitsound = 5, 2
guardsound = 6, 0
ground.type = High
ground.slidetime = 10
ground.hittime = 14
ground.velocity = -3
air.velocity = -2.5, -3.5
getpower = 0, 0
givepower = %d, %d

`, s.Damage, s.Damage/5, s.Damage*6, s.Damage*2)
		}

		fmt.Fprintf(b, `[State %d, End]
type = ChangeState
trigger1 = AnimTime = 0
value = 0
ctrl = 1

`, s.State)
	}
}

// writeSpecialCommands defines the motions, ahead of the plain button commands
// by convention. What actually decides which move wins is the order of the
// Statedef -1 entries, not this one.
func writeSpecialCommands(b *strings.Builder, specials []SpecialSpec) {
	motions := map[string]string{
		cmdQcfSlash:   "~D, DF, F, " + btnLightSlash,
		cmdQcbSlash:   "~D, DB, B, " + btnLightSlash,
		cmdSuperSlash: "~D, DF, F, D, DF, F, " + btnLightSlash,
	}
	seen := map[string]bool{}
	for _, s := range specials {
		if seen[s.Command] {
			continue
		}
		seen[s.Command] = true
		fmt.Fprintf(b, `[Command]
name = "%s"
command = %s
time = 20

`, s.Command, motions[s.Command])
	}
}

// writeSpecialTriggers puts the specials ahead of the normals in Statedef -1.
// A quarter-circle ends on a slash button, so testing the normals first would
// eat every special before its motion was ever considered.
func writeSpecialTriggers(b *strings.Builder, specials []SpecialSpec) {
	for _, s := range specials {
		fmt.Fprintf(b, `[State -1, %s]
type = ChangeState
value = %d
triggerall = command = "%s"
triggerall = var(%d) = 0
`, s.Name, s.State, s.Command, varDisarmed)
		if s.PowerCost > 0 {
			fmt.Fprintf(b, "triggerall = Power >= %d\n", s.PowerCost)
		}
		b.WriteString("trigger1 = StateType != A && Ctrl\n\n")
	}
}
