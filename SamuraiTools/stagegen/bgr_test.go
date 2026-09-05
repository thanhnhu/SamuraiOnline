package main

import (
	"encoding/binary"
	"os"
	"path/filepath"
	"testing"
)

// These run against a real copy of the game, which is not in this repository
// and never will be. Point SAMSHO_DATA at your own DATA directory to enable
// them; without it they skip, so CI stays green.
func dataDir(t *testing.T) string {
	t.Helper()
	dir := os.Getenv("SAMSHO_DATA")
	if dir == "" {
		t.Skip("set SAMSHO_DATA to the game's DATA directory to run this")
	}
	return dir
}

func bgrFiles(t *testing.T) []string {
	t.Helper()
	files, err := filepath.Glob(filepath.Join(dataDir(t), "*.BGR"))
	if err != nil || len(files) == 0 {
		t.Skipf("no .BGR files: %v", err)
	}
	return files
}

// The container arithmetic is what pins dataWords to 16-bit units. .SPR counts
// bytes in the same field, so getting this wrong silently halves the data.
func TestContainerSizeIsExact(t *testing.T) {
	for _, path := range bgrFiles(t) {
		b, err := os.ReadFile(path)
		if err != nil {
			t.Fatalf("%s: %v", path, err)
		}
		if _, err := Parse(b); err != nil {
			t.Errorf("%s: %v", filepath.Base(path), err)
		}
	}
}

// Every row's skips and literal runs must sum to exactly the declared width.
// Nothing else about the codec has to be assumed: if the run encoding were
// misread, this would not hold across thousands of rows.
func TestEveryRowCoversExactlyTheWidth(t *testing.T) {
	pal := &Palette{} // contents are irrelevant to the geometry
	for _, path := range bgrFiles(t) {
		b, err := os.ReadFile(path)
		if err != nil {
			t.Fatalf("%s: %v", path, err)
		}
		a, err := Parse(b)
		if err != nil {
			t.Fatalf("%s: %v", filepath.Base(path), err)
		}
		for i, rec := range a.Records {
			if _, err := a.Decode(rec, pal); err != nil {
				t.Errorf("%s record %d (%dx%d): %v",
					filepath.Base(path), i, rec.Width, rec.Height, err)
			}
		}
	}
}

// Each scanline is built from eight stored rows. Decoding one row per line
// still produces images, just with seven blank lines in every eight, so the
// only honest check is that the eight-plane walk consumes the file exactly:
// the last record must end on the final word.
func TestPlanesConsumeTheWholeFile(t *testing.T) {
	for _, path := range bgrFiles(t) {
		b, err := os.ReadFile(path)
		if err != nil {
			t.Fatalf("%s: %v", path, err)
		}
		a, err := Parse(b)
		if err != nil {
			t.Fatalf("%s: %v", filepath.Base(path), err)
		}

		end := 0
		for _, rec := range a.Records {
			row := rec.Offset
			for y := 0; y < rec.Height*PlanesPerLine; y++ {
				length, ok := a.word(row)
				if !ok {
					t.Fatalf("%s: row at word %d is past the data", filepath.Base(path), row)
				}
				row += int(length)
			}
			if row > end {
				end = row
			}
		}
		if want := len(a.data) / 2; end != want {
			t.Errorf("%s: the planes end at word %d, the data is %d words. "+
				"A mismatch here means the rows-per-line count is wrong",
				filepath.Base(path), end, want)
		}
	}
}

// Reading one row per scanline instead of eight is the mistake this format
// invites, and it fails quietly. Prove the eight-plane count is not arbitrary
// by showing a single-plane walk leaves most of the file unread.
func TestSinglePlaneWalkLeavesDataUnread(t *testing.T) {
	files := bgrFiles(t)
	b, err := os.ReadFile(files[0])
	if err != nil {
		t.Fatal(err)
	}
	a, err := Parse(b)
	if err != nil {
		t.Fatal(err)
	}
	rec := a.Records[0]

	row := rec.Offset
	for y := 0; y < rec.Height; y++ {
		length, _ := a.word(row)
		row += int(length)
	}
	consumed := row - rec.Offset

	row = rec.Offset
	for y := 0; y < rec.Height*PlanesPerLine; y++ {
		length, _ := a.word(row)
		row += int(length)
	}
	full := row - rec.Offset

	if consumed >= full {
		t.Fatalf("one plane consumed %d words, eight consumed %d; "+
			"the plane count is doing nothing", consumed, full)
	}
}

// The palette is what turns indices into colour. A wrong offset produces
// plausible-looking noise, so check the shape of the table instead: entry 0 of
// every 16-colour group is the transparent slot and is always zero.
func TestPaletteSnapshotLooksLikeAPalette(t *testing.T) {
	prg, err := os.ReadFile(filepath.Join(dataDir(t), "GAME1.PRG"))
	if err != nil {
		t.Skipf("no GAME1.PRG: %v", err)
	}
	pal, err := LoadPalette(prg, 0)
	if err != nil {
		t.Fatal(err)
	}

	var nonZeroFirstEntries int
	for i := 0; i < len(pal); i += 16 {
		if pal[i] != 0 {
			nonZeroFirstEntries++
		}
	}
	if nonZeroFirstEntries > len(pal)/16/4 {
		t.Errorf("%d of %d groups start with a non-zero entry; "+
			"PaletteOffsetInPRG is probably wrong",
			nonZeroFirstEntries, len(pal)/16)
	}

	// Bit 15 is unused in RGB555. A table full of values with it set is not a
	// palette.
	var highBitSet int
	for _, c := range pal {
		if c&0x8000 != 0 {
			highBitSet++
		}
	}
	if highBitSet > len(pal)/10 {
		t.Errorf("%d entries have bit 15 set; this does not look like RGB555", highBitSet)
	}
}

func TestLoadPaletteRejectsBadSnapshots(t *testing.T) {
	prg := make([]byte, PaletteOffsetInPRG+PaletteSnapshots*4096*2)
	for _, s := range []int{-1, PaletteSnapshots, 99} {
		if _, err := LoadPalette(prg, s); err == nil {
			t.Errorf("accepted snapshot %d", s)
		}
	}
	if _, err := LoadPalette(prg[:8], 0); err == nil {
		t.Error("accepted a GAME1.PRG too small to hold a palette")
	}
}

func TestParseRejectsForeignFiles(t *testing.T) {
	// A .SPR header: same shape, different magic, and its dataSize counts
	// bytes. Accepting one would produce garbage rather than an error.
	spr := make([]byte, 8)
	binary.LittleEndian.PutUint16(spr[0:], 0x1053)
	if _, err := Parse(spr); err == nil {
		t.Error("accepted a .SPR file")
	}
	if _, err := Parse(nil); err == nil {
		t.Error("accepted an empty file")
	}
}
