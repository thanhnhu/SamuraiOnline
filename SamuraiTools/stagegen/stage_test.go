package main

import (
	"bytes"
	"encoding/binary"
	"image"
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func testLayers() []Layer {
	a := image.NewNRGBA(image.Rect(0, 0, 640, 240))
	b := image.NewNRGBA(image.Rect(0, 0, 320, 64))
	return []Layer{{ID: 40, Image: a}, {ID: 38, Image: b}}
}

// Ikemen reads the layer art out of an .sff, so a stage without one loads as an
// empty screen with no error to explain it.
func TestWriteStageProducesBothFiles(t *testing.T) {
	dir := t.TempDir()
	if err := WriteStage(dir, "B011", testLayers(), StageOptions{Name: "Temple"}); err != nil {
		t.Fatalf("WriteStage: %v", err)
	}
	for _, name := range []string{"B011.def", "B011.sff"} {
		if _, err := os.Stat(filepath.Join(dir, name)); err != nil {
			t.Errorf("%s: %v", name, err)
		}
	}
}

// The .sff must carry one sprite per layer, numbered to match the spriteno
// lines in the .def. A mismatch shows up as a missing background rather than a
// load failure.
func TestSffHoldsOneSpritePerLayer(t *testing.T) {
	dir := t.TempDir()
	layers := testLayers()
	if err := WriteStage(dir, "B011", layers, StageOptions{}); err != nil {
		t.Fatal(err)
	}
	raw, err := os.ReadFile(filepath.Join(dir, "B011.sff"))
	if err != nil {
		t.Fatal(err)
	}
	if !bytes.HasPrefix(raw, []byte(sffSignature)) {
		t.Fatal("not an SFF: signature missing")
	}
	// Header layout: 12 signature, 4 version, 5 reserved u32, then the sprite
	// table offset at 36 and the sprite count at 40.
	if got := binary.LittleEndian.Uint32(raw[40:]); int(got) != len(layers) {
		t.Errorf("sff declares %d sprites, wrote %d layers", got, len(layers))
	}

	def, err := os.ReadFile(filepath.Join(dir, "B011.def"))
	if err != nil {
		t.Fatal(err)
	}
	for i := range layers {
		want := "spriteno = 0, " + itoa(i)
		if !strings.Contains(string(def), want) {
			t.Errorf("def does not reference %q", want)
		}
	}
}

// Without these sections the engine either refuses the stage or drops the
// players through the floor.
func TestDefHasTheSectionsTheEngineNeeds(t *testing.T) {
	def := buildDef("B011", testLayers(), StageOptions{Name: "Temple"})
	for _, section := range []string{
		"[Info]", "[Camera]", "[PlayerInfo]", "[Bound]", "[StageInfo]",
		"[BGdef]", "spr = B011.sff", "zoffset =", "localcoord =",
	} {
		if !strings.Contains(def, section) {
			t.Errorf("generated def is missing %q", section)
		}
	}
}

// A stage with no music must still be a valid stage: SND_0A does not exist, so
// B00A genuinely has none.
func TestDefWithoutMusicOmitsBgmusic(t *testing.T) {
	def := buildDef("B00A", testLayers(), StageOptions{})
	if strings.Contains(def, "bgmusic =") {
		t.Error("wrote a bgmusic line with no music to point at")
	}
	if !strings.Contains(def, "[Music]") {
		t.Error("dropped the [Music] section entirely")
	}
}

func TestDefWithMusicPointsAtIt(t *testing.T) {
	def := buildDef("B011", testLayers(), StageOptions{Music: "stages/samsho2/sound/SND_11.WAV"})
	if !strings.Contains(def, "bgmusic = stages/samsho2/sound/SND_11.WAV") {
		t.Error("bgmusic line missing or wrong")
	}
}

// bgmusic is resolved from the engine root, not from the stage directory, so a
// path recorded relative to the wrong place loads silently as no music.
func TestMusicPathIsRelativeToTheEngineRoot(t *testing.T) {
	got := relativeToEngine(filepath.Join("some", "where", "Ikemen-GO", "stages", "samsho2"))
	if got != "stages/samsho2" {
		t.Errorf("got %q, want %q", got, "stages/samsho2")
	}
}

func TestWriteStageRejectsNoLayers(t *testing.T) {
	if err := WriteStage(t.TempDir(), "empty", nil, StageOptions{}); err == nil {
		t.Error("wrote a stage with no layers")
	}
}

func itoa(i int) string {
	if i == 0 {
		return "0"
	}
	var b []byte
	for i > 0 {
		b = append([]byte{byte('0' + i%10)}, b...)
		i /= 10
	}
	return string(b)
}
