package main

import (
	"fmt"
	"regexp"
	"strconv"
	"strings"
)

// Parser for the hand-authored animation tables in the X-Mat Studio SDL port.
//
// The source declares, per animation, a set of collider rectangles and their
// types, then pushes one frame at a time:
//
//	SDL_Rect punchHitbox[3] = { {35,10,40,60}, {50,60,20,20}, {40,30,80,30} };
//	COLLIDER_TYPE punchCollType[3] = { {COLLIDER_PLAYER},{COLLIDER_PLAYER},{COLLIDER_PLAYER_ATTACK} };
//	punch.PushBack({1,721,86,97}, 3, {28,3}, punchCollider2, punchHitbox2, punchCollType2, punchCallBack2, 12, 7, 7, 3);
//	              |  src rect  | dur| pivot|   count       |  boxes      |   types       |  cb          |dmg|pd|ed|type
//
// Note the original PushBack stores colliders indexed by collider slot rather
// than by frame, so at runtime every frame of an animation ends up sharing the
// last pushed set. We keep the per-call arrays instead, which is what the data
// was clearly meant to express and gives correct per-frame boxes.

type Rect struct{ X, Y, W, H int }

type ColliderKind int

const (
	ColliderIgnored ColliderKind = iota
	ColliderHurt
	ColliderAttack
)

type Frame struct {
	Src        Rect
	Duration   int
	PivotX     int
	PivotY     int
	Hurt       []Rect
	Attack     []Rect
	Damage     int
	AttackType int
}

type Anim struct {
	Name   string
	Frames []Frame
}

type Source struct {
	rects map[string][]Rect
	types map[string][]ColliderKind
	Anims []*Anim
	byName map[string]*Anim
}

var (
	reBlockComment = regexp.MustCompile(`(?s)/\*.*?\*/`)
	reLineComment  = regexp.MustCompile(`//[^\n]*`)

	reRectDecl = regexp.MustCompile(`SDL_Rect\s+(\w+)\s*\[[^\]]*\]\s*=\s*\{(.*)\}`)
	reTypeDecl = regexp.MustCompile(`COLLIDER_TYPE\s+(\w+)\s*\[[^\]]*\]\s*=\s*\{(.*)\}`)
	rePushBack = regexp.MustCompile(
		`(\w+)\s*\.\s*PushBack\s*\(\s*\{([^}]*)\}\s*,\s*([0-9]+)\s*,\s*\{([^}]*)\}\s*,` +
			`\s*(\w+)\s*,\s*(\w+)\s*,\s*(\w+)\s*,\s*(\w+)\s*,` +
			`\s*(-?[0-9]+)\s*,\s*(-?[0-9]+)\s*,\s*(-?[0-9]+)\s*,\s*(-?[0-9]+)\s*\)`)

	reBracedGroup = regexp.MustCompile(`\{([^{}]*)\}`)
	reColliderTok = regexp.MustCompile(`COLLIDER_\w+`)
)

func ParseSource(src string) (*Source, error) {
	src = reBlockComment.ReplaceAllString(src, " ")
	src = reLineComment.ReplaceAllString(src, " ")

	s := &Source{
		rects:  make(map[string][]Rect),
		types:  make(map[string][]ColliderKind),
		byName: make(map[string]*Anim),
	}

	// Statements are separated by semicolons; every construct we care about is
	// a single statement, so this keeps multi-line declarations intact.
	for _, stmt := range strings.Split(src, ";") {
		stmt = strings.Join(strings.Fields(stmt), " ")
		if stmt == "" {
			continue
		}
		switch {
		case strings.Contains(stmt, "SDL_Rect") && strings.Contains(stmt, "="):
			if m := reRectDecl.FindStringSubmatch(stmt); m != nil {
				rs, err := parseRectList(m[2])
				if err != nil {
					return nil, fmt.Errorf("rect list %s: %w", m[1], err)
				}
				s.rects[m[1]] = rs
			}
		case strings.Contains(stmt, "COLLIDER_TYPE") && strings.Contains(stmt, "="):
			if m := reTypeDecl.FindStringSubmatch(stmt); m != nil {
				s.types[m[1]] = parseColliderKinds(m[2])
			}
		case strings.Contains(stmt, "PushBack"):
			if m := rePushBack.FindStringSubmatch(stmt); m != nil {
				if err := s.addFrame(m); err != nil {
					return nil, err
				}
			}
		}
	}
	return s, nil
}

func (s *Source) addFrame(m []string) error {
	animName := m[1]

	src, err := parseRect(m[2])
	if err != nil {
		return fmt.Errorf("%s: source rect: %w", animName, err)
	}
	dur, err := strconv.Atoi(m[3])
	if err != nil {
		return fmt.Errorf("%s: duration: %w", animName, err)
	}
	pivot, err := parseInts(m[4])
	if err != nil || len(pivot) < 2 {
		return fmt.Errorf("%s: pivot %q", animName, m[4])
	}

	boxes := s.rects[m[6]]
	kinds := s.types[m[7]]
	damage, _ := strconv.Atoi(m[9])
	atkType, _ := strconv.Atoi(m[12])

	f := Frame{
		Src:        src,
		Duration:   dur,
		PivotX:     pivot[0],
		PivotY:     pivot[1],
		Damage:     damage,
		AttackType: atkType,
	}
	for i, b := range boxes {
		kind := ColliderHurt
		if i < len(kinds) {
			kind = kinds[i]
		}
		switch kind {
		case ColliderHurt:
			f.Hurt = append(f.Hurt, b)
		case ColliderAttack:
			f.Attack = append(f.Attack, b)
		}
	}

	a, ok := s.byName[animName]
	if !ok {
		a = &Anim{Name: animName}
		s.byName[animName] = a
		s.Anims = append(s.Anims, a)
	}
	a.Frames = append(a.Frames, f)
	return nil
}

func (s *Source) Anim(name string) *Anim { return s.byName[name] }

func parseRectList(body string) ([]Rect, error) {
	var out []Rect
	for _, g := range reBracedGroup.FindAllStringSubmatch(body, -1) {
		r, err := parseRect(g[1])
		if err != nil {
			return nil, err
		}
		out = append(out, r)
	}
	return out, nil
}

func parseRect(body string) (Rect, error) {
	v, err := parseInts(body)
	if err != nil {
		return Rect{}, err
	}
	if len(v) != 4 {
		return Rect{}, fmt.Errorf("expected 4 values, got %d in %q", len(v), body)
	}
	return Rect{v[0], v[1], v[2], v[3]}, nil
}

func parseInts(body string) ([]int, error) {
	var out []int
	for _, part := range strings.Split(body, ",") {
		part = strings.TrimSpace(strings.Trim(strings.TrimSpace(part), "{}"))
		if part == "" {
			continue
		}
		n, err := strconv.Atoi(part)
		if err != nil {
			return nil, err
		}
		out = append(out, n)
	}
	return out, nil
}

func parseColliderKinds(body string) []ColliderKind {
	var out []ColliderKind
	for _, tok := range reColliderTok.FindAllString(body, -1) {
		switch tok {
		case "COLLIDER_PLAYER", "COLLIDER_ENEMY":
			out = append(out, ColliderHurt)
		case "COLLIDER_PLAYER_ATTACK", "COLLIDER_ENEMY_ATTACK",
			"COLLIDER_GRAB", "COLLIDER_PLAYER_SHOT", "COLLIDER_ENEMY_SHOT":
			out = append(out, ColliderAttack)
		default:
			out = append(out, ColliderIgnored)
		}
	}
	return out
}
