package main

import (
	"os"
	"path/filepath"
	"regexp"
	"sort"
	"strconv"
	"strings"
	"testing"
)

// The engine drives basic actions into states owned by data/common1.cns.zss,
// and those states name animations by hard-coded number. A character missing
// one enters a state whose animation does not exist; when the state also waits
// on animTime, it never leaves. That has already happened twice here, once for
// walking and once for guarding, and both times it was found by playing rather
// than by testing.
//
// So rather than keeping a hand-written list that drifts, this reads the
// numbers straight out of common1.cns.zss. Anything it finds must either exist
// in the character or be listed below with a reason.

var (
	reStateDefAnim = regexp.MustCompile(`\[\s*StateDef\b[^\]]*\banim:\s*(\d+)`)
	reChangeAnim   = regexp.MustCompile(`changeAnim2?\{\s*value:\s*([^}]*)\}`)
	reAllDigits    = regexp.MustCompile(`^\d+$`)
	// "120 + (stateType = C) + (stateType = A) * 2" selects one of three
	// consecutive animations, so all three are required.
	reStateTypeTriple = regexp.MustCompile(`^(\d+)\s*\+\s*\(stateType = C\)\s*\+\s*\(stateType = A\)\s*\*\s*2$`)
	// "cond(vel x = 0, 41, ifElse(vel x > 0, 42, 43))" picks between literals.
	reBareInt = regexp.MustCompile(`\b(\d+)\b`)
)

// optionalCommonAnims are referenced but not required. Each is either guarded
// by selfAnimExist at the point of use, or belongs to a system this project
// does not use.
var optionalCommonAnims = map[int]string{
	44:   "air jump, guarded by selfAnimExist",
	115:  "Z-axis run, marked deprecated in common1 and never entered by the engine",
	175:  "custom dizzy pose, guarded by selfAnimExist",
	190:  "custom win pose, guarded by selfAnimExist",
	5300: "continue screen pose, arcade continue flow only",
	5500: "continue screen, only used by the arcade continue flow",
}

func readCommonStates(t *testing.T) string {
	t.Helper()
	path := filepath.Join("..", "data", "common1.cns.zss")
	b, err := os.ReadFile(path)
	if err != nil {
		t.Skipf("common1.cns.zss not present: %v", err)
	}
	return string(b)
}

// requiredCommonAnims returns every animation number common1 names as a
// literal outside a selfAnimExist guard.
func requiredCommonAnims(src string) map[int]bool {
	out := map[int]bool{}
	guardDepth := -1
	depth := 0

	for _, line := range strings.Split(src, "\n") {
		code := line
		if i := strings.Index(code, "#"); i >= 0 {
			code = code[:i]
		}

		guarded := guardDepth >= 0 || strings.Contains(code, "selfAnimExist")
		if !guarded {
			for _, m := range reStateDefAnim.FindAllStringSubmatch(code, -1) {
				add(out, m[1])
			}
			for _, m := range reChangeAnim.FindAllStringSubmatch(code, -1) {
				addExpr(out, strings.TrimSpace(m[1]))
			}
		}

		// A guard opened on this line covers the block it opens.
		if guardDepth < 0 && strings.Contains(code, "selfAnimExist") &&
			strings.Count(code, "{") > strings.Count(code, "}") {
			guardDepth = depth
		}
		depth += strings.Count(code, "{") - strings.Count(code, "}")
		if guardDepth >= 0 && depth <= guardDepth {
			guardDepth = -1
		}
	}
	return out
}

func add(out map[int]bool, s string) {
	if n, err := strconv.Atoi(s); err == nil {
		out[n] = true
	}
}

func addExpr(out map[int]bool, expr string) {
	switch {
	case reAllDigits.MatchString(expr):
		add(out, expr)
	case reStateTypeTriple.MatchString(expr):
		m := reStateTypeTriple.FindStringSubmatch(expr)
		base, _ := strconv.Atoi(m[1])
		out[base], out[base+1], out[base+2] = true, true, true
	case strings.HasPrefix(expr, "cond(") || strings.HasPrefix(expr, "ifElse("):
		// Only the branch values matter, and animation numbers are the large
		// ones; comparison operands like 0 are not animations.
		for _, m := range reBareInt.FindAllStringSubmatch(expr, -1) {
			if n, err := strconv.Atoi(m[1]); err == nil && n >= 10 {
				out[n] = true
			}
		}
	default:
		// "$anim", "anim + 3", "sysVar(2)" and friends cannot be resolved
		// statically, and are all reached through selfAnimExist anyway.
	}
}

func TestCharactersHaveEveryAnimationCommonStatesName(t *testing.T) {
	required := requiredCommonAnims(readCommonStates(t))
	if len(required) < 20 {
		t.Fatalf("only found %d animation references; the scanner is not reading common1 properly", len(required))
	}

	for _, char := range []string{"jubei", "jubei2"} {
		air := readCharFile(t, char, char+".air")

		var missing []int
		for no := range required {
			if _, ok := optionalCommonAnims[no]; ok {
				continue
			}
			if !strings.Contains(air, "[Begin Action "+strconv.Itoa(no)+"]") {
				missing = append(missing, no)
			}
		}
		sort.Ints(missing)
		if len(missing) > 0 {
			t.Errorf("%s is missing animations that common1.cns.zss uses: %v", char, missing)
		}
	}
}

// An entry that no longer appears in common1 is stale and hides nothing, so it
// should be deleted rather than left to reassure the next reader.
func TestOptionalAnimListHasNoStaleEntries(t *testing.T) {
	src := readCommonStates(t)
	for no, reason := range optionalCommonAnims {
		if !strings.Contains(src, strconv.Itoa(no)) {
			t.Errorf("anim %d (%q) is no longer referenced by common1.cns.zss", no, reason)
		}
	}
}
