package main

import (
	"fmt"
	"sort"
	"strings"
)

// Mapping from the SDL port's animation variables to MUGEN action numbers.
//
// Ikemen's common states drive a character through well-known action numbers,
// so anything that maps onto the standard set is wired up for free. Animations
// with no standard equivalent (the disarmed "NW" set, sword clashes, taunts)
// are parked in a private range so the sprites still ship and can be referenced
// later from custom states.
var actionMap = map[string]int32{
	"idle":     0,
	"crouch":   11,
	"forward":  20,
	"backward": 21,
	"jump":     50,
	"jumpFw":   55,

	"sprint":       100,
	"sprintEnd":    101,
	"backsprint":   105,
	"jumpBackward": 106,
	"sideStep":     110,

	"blockingIdle": 130,
	"crouchBlock":  131,

	"punch":       200,
	"midpunch":    210,
	"strongpunch": 230,
	"kick":        240,
	"midkick":     250,
	"strongkick":  260,

	"shortPunch":       270,
	"midshortPunch":    271,
	"shortstrongPunch": 272,

	"crouchPunch":       400,
	"crouchmidPunch":    410,
	"crouchstrongPunch": 430,
	"crouchKick":        440,
	"crouchmidKick":     450,
	"crouchstrongKick":  460,

	"jumpPunch":       600,
	"jumpmidPunch":    610,
	"jumpstrongPunch": 630,
	"jumpKick":        640,
	"jumpmidKick":     650,
	"jumpstrongKick":  660,
	"jumpFwPunch":     670,

	"special":  1000,
	"special2": 1010,
	"tornado":  1100,

	"grab":     800,
	"midgrab":  810,
	"getGrab":  820,
	"getGrab2": 821,

	"hurtLow": 5000,
	"fall":    5030,
	"getUp":   5120,

	"lose1": 170,
	"lose2": 171,
	"win1":  180,
	"win2":  181,

	"swordFight":  1200,
	"pickUpSword": 1210,
}

// Disarmed animations keep their own block so they never collide with the
// standard numbering.
const disarmedBase int32 = 20000

var disarmedMap = map[string]int32{
	"NWidle": 0, "NWforward": 20, "NWbackward": 21, "NWjump": 50,
	"NWcrouch": 11, "NWpunch": 200, "NWstrongpunch": 230,
	"NWkick": 240, "NWstrongkick": 260,
	"NWcrouchPunch": 400, "NWstrongcrouchPunch": 430,
	"NWcrouchKick": 440, "NWcrouchmidKick": 450, "NWcrouchstrongKick": 460,
	"NWjumpPunch": 600, "NWjumpstrongPunch": 630,
	"NWjumpKick": 640, "NWjumpstrongKick": 660,
	"NWsprint": 100, "NWsprintEnd": 101, "NWbacksprint": 105,
	"NWblockingIdle": 130, "NwcrouchBlock": 131,
	"NWfall": 5030, "NWgrab": 800, "NWlose1": 170, "NWwin1": 180,
}

// unmappedBase holds anything we did not classify, so no art is silently lost.
const unmappedBase int32 = 30000

// Action is one generated [Begin Action] block.
type Action struct {
	No     int32
	Name   string
	Frames []Frame
}

// BuildActions assigns an action number to every parsed animation.
func BuildActions(src *Source) []Action {
	var out []Action
	next := unmappedBase

	for _, a := range src.Anims {
		if len(a.Frames) == 0 {
			continue
		}
		var no int32
		switch {
		case actionMap[a.Name] != 0 || a.Name == "idle":
			no = actionMap[a.Name]
		case hasKey(disarmedMap, a.Name):
			no = disarmedBase + disarmedMap[a.Name]
		default:
			no = next
			next += 10
		}
		out = append(out, Action{No: no, Name: a.Name, Frames: a.Frames})
	}
	out = append(out, projectileActions(src)...)
	out = applySpecialAttackBoxes(src, out)

	sort.SliceStable(out, func(i, j int) bool { return out[i].No < out[j].No })
	return appendCommonAliases(out)
}

// Ikemen's common1.cns.zss hard-codes animation numbers for the movement and
// guard states it owns. The source art has no separate crouch-transition,
// jump-start, landing or guard animations, so those numbers are aliased onto
// the closest existing frames; without them the character enters a state whose
// animation does not exist.
//
// Where a state waits on animTime to move on, the alias is cut to one frame
// with an explicitly finite duration. Inheriting a looping frame there would
// leave animTime never reaching zero and the character stuck in that state.
func appendCommonAliases(actions []Action) []Action {
	byNo := map[int32]Action{}
	for _, a := range actions {
		byNo[a.No] = a
	}

	const transitionTicks = 4

	aliases := []struct {
		from, to   int32
		transition bool
		name       string
	}{
		{11, 10, true, "stand to crouch"},
		{11, 12, true, "crouch to stand"},
		{50, 40, true, "jump start"},
		{50, 41, false, "jump up"},
		{55, 42, false, "jump forward"},
		{50, 43, false, "jump back"},
		{0, 47, true, "jump land"},

		// Guard. State 120 waits on animTime before handing over to 130, so
		// 120/121/122 must end; the rest only need to exist.
		{130, 120, true, "stand guard start"},
		{131, 121, true, "crouch guard start"},
		{130, 122, true, "air guard start"},
		{130, 132, false, "air guard"},
		{130, 140, true, "stand guard end"},
		{131, 141, true, "crouch guard end"},
		{130, 142, true, "air guard end"},
		{130, 150, false, "stand guard hit"},
		{131, 151, false, "crouch guard hit"},
		{130, 152, false, "air guard hit"},

		// Being knocked down. The source art has one fall animation and one
		// hurt pose, so the whole knockdown sequence reuses them; without
		// these the character has no animation from the moment it is launched
		// until it stands back up.
		{5030, 5040, false, "fall"},
		{5030, 5050, false, "fall down"},
		{5030, 5070, false, "hit the ground"},
		{5030, 5110, false, "lying down"},
		{5000, 5200, false, "hit by throw"},
		{5030, 5210, false, "thrown"},
	}

	for _, al := range aliases {
		if _, taken := byNo[al.to]; taken {
			continue
		}
		src, ok := byNo[al.from]
		if !ok || len(src.Frames) == 0 {
			// Fall back to the idle pose rather than leaving a hole.
			if src, ok = byNo[0]; !ok || len(src.Frames) == 0 {
				continue
			}
		}

		frames := src.Frames
		if al.transition {
			f := frames[0]
			f.Duration = transitionTicks
			frames = []Frame{f}
		}
		clone := Action{No: al.to, Name: al.name + " (alias)", Frames: frames}
		byNo[al.to] = clone
		actions = append(actions, clone)
	}

	sort.SliceStable(actions, func(i, j int) bool { return actions[i].No < actions[j].No })
	return actions
}

// DisarmedFor reports the disarmed action number for a base action, if the
// source actually ships one. Jubei has no disarmed art for every normal, so
// callers must not assume the variant exists.
func DisarmedFor(src *Source, base int32) (int32, bool) {
	for name, mapped := range disarmedMap {
		if mapped != base {
			continue
		}
		if a := src.Anim(name); a != nil && len(a.Frames) > 0 {
			return disarmedBase + mapped, true
		}
	}
	return 0, false
}

func hasKey(m map[string]int32, k string) bool {
	_, ok := m[k]
	return ok
}

// clsn converts a collider from the SDL port's frame-local space into MUGEN's
// axis-relative space.
//
// The port draws at (pos.x - pivotX, pos.y + pivotY - h) and places colliders at
// (pos.x + r.x - pivotX, pos.y + pivotY - r.h - r.y). Both share the same origin
// as a MUGEN axis, and both measure Y downwards, so the conversion is a plain
// translation.
func clsn(r Rect, pivotX, pivotY int) (x1, y1, x2, y2 int) {
	x1 = r.X - pivotX
	x2 = x1 + r.W
	y1 = pivotY - r.H - r.Y
	y2 = pivotY - r.Y
	return
}

// axis is the point inside the sprite that sits on the character's position.
func axis(f Frame) (int16, int16) {
	return int16(f.PivotX), int16(f.Src.H - f.PivotY)
}

func WriteAir(actions []Action, charName string) string {
	var b strings.Builder
	fmt.Fprintf(&b, "; %s - generated from the X-Mat Studio animation tables.\n", charName)
	b.WriteString("; Do not edit by hand: regenerate with SamuraiTools/chargen.\n\n")

	for _, a := range actions {
		fmt.Fprintf(&b, "; %s\n", a.Name)
		fmt.Fprintf(&b, "[Begin Action %d]\n", a.No)
		for i, f := range a.Frames {
			writeClsnGroup(&b, "Clsn2", f.Hurt, f)
			writeClsnGroup(&b, "Clsn1", f.Attack, f)
			fmt.Fprintf(&b, "%d,%d, 0,0, %d\n", a.No, i, f.Duration)
		}
		b.WriteString("\n")
	}
	return b.String()
}

func writeClsnGroup(b *strings.Builder, kind string, boxes []Rect, f Frame) {
	if len(boxes) == 0 {
		return
	}
	fmt.Fprintf(b, "%s: %d\n", kind, len(boxes))
	for i, r := range boxes {
		x1, y1, x2, y2 := clsn(r, f.PivotX, f.PivotY)
		fmt.Fprintf(b, " %s[%d] = %d, %d, %d, %d\n", kind, i, x1, y1, x2, y2)
	}
}
