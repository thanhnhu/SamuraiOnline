package main

import (
	"fmt"
	"image"
	"image/color"
	"image/draw"
	"image/png"
	"os"
	"sort"
	"strings"
)

// The renderer converts its stored palette with
//
//	out = c&0x1f | (c&0x7ff0)<<1
//
// which is the standard 555 -> 565 widening, so palette RAM holds RGB555 with
// bit 15 unused. That unused bit is the search handle: sixteen consecutive
// entries all having bit 15 clear happens by chance in about one window in
// 65536, so scanning for it finds palette tables in an otherwise opaque blob.

const palEntries = 16

type palCandidate struct {
	Off    int
	Colors [palEntries]color.RGBA
	Words  [palEntries]uint16
}

func rgb555(c uint16) color.RGBA {
	r := (c >> 10) & 0x1f
	g := (c >> 5) & 0x1f
	b := c & 0x1f
	return color.RGBA{expand5(r), expand5(g), expand5(b), 255}
}

func expand5(v uint16) uint8 {
	return uint8(v<<3 | v>>2)
}

func scanPalettes(b []byte, step int) []palCandidate {
	var out []palCandidate
	for off := 0; off+palEntries*2 <= len(b); off += step {
		var words [palEntries]uint16
		ok := true
		for i := 0; i < palEntries; i++ {
			w := uint16(b[off+i*2]) | uint16(b[off+i*2+1])<<8
			if w&0x8000 != 0 {
				ok = false
				break
			}
			words[i] = w
		}
		if !ok {
			continue
		}
		c := palCandidate{Off: off, Words: words}
		for i, w := range words {
			c.Colors[i] = rgb555(w)
		}
		out = append(out, c)
	}
	return out
}

type palRun struct {
	start int
	n     int
}

// paletteRAM is the 4096-entry colour table the blitter indexes. A sprite's
// bank field is an index into it, and pixel bytes are added on top, so a
// sprite's own 16 colours start at exactly that index.
type paletteRAM struct {
	words []uint16
}

func loadPaletteRAM(b []byte, base int) *paletteRAM {
	p := &paletteRAM{}
	for off := base; off+1 < len(b); off += 2 {
		p.words = append(p.words, uint16(b[off])|uint16(b[off+1])<<8)
	}
	return p
}

func (p *paletteRAM) forBank(bank uint16) color.Palette {
	pal := make(color.Palette, palEntries)
	for i := 0; i < palEntries; i++ {
		idx := int(bank) + i
		if idx >= len(p.words) {
			pal[i] = color.RGBA{}
			continue
		}
		pal[i] = rgb555(p.words[idx])
	}
	return pal
}

// findPaletteTables locates palette RAM images by the one invariant the format
// guarantees: entry 0 of every palette is the transparent slot and is always
// zero. A long stretch of 32-byte records each starting with a zero word is
// therefore a palette table, and the phase of that stretch fixes the base
// offset exactly - which scoring heuristics alone cannot do.
func findPaletteTables(b []byte, minPalettes int) []palRun {
	const stride = palEntries * 2
	var runs []palRun
	for phase := 0; phase < stride; phase += 2 {
		off := phase
		for off+stride <= len(b) {
			if b[off] != 0 || b[off+1] != 0 {
				off += stride
				continue
			}
			n := 0
			nonZero := 0
			for off+(n+1)*stride <= len(b) {
				p := off + n*stride
				if b[p] != 0 || b[p+1] != 0 {
					break
				}
				for i := 2; i < stride; i++ {
					if b[p+i] != 0 {
						nonZero++
						break
					}
				}
				n++
			}
			// All-zero padding also satisfies the invariant, so require most
			// palettes to actually carry colour.
			if n >= minPalettes && nonZero*2 >= n {
				runs = append(runs, palRun{off, n})
			}
			if n == 0 {
				n = 1
			}
			off += n * stride
		}
	}
	sort.Slice(runs, func(i, j int) bool { return runs[i].n > runs[j].n })
	return runs
}

// The palette transfer builder at FUN_0041f62a copies 0xf words per palette,
// so the stored form is 15 colours (30 bytes) with the transparent slot
// omitted - the 32-byte stride only applies to palette RAM snapshots. Source
// libraries therefore have no zero-entry marker, and the only invariant left
// is that every word is RGB555 with bit 15 clear. Long unbroken runs of such
// words that are mostly non-zero are the palette libraries.
func findColourRuns(b []byte, minWords int) []palRun {
	var runs []palRun
	for phase := 0; phase < 2; phase++ {
		start, n, nonZero := -1, 0, 0
		flush := func() {
			if start >= 0 && n >= minWords && nonZero*2 >= n {
				runs = append(runs, palRun{start, n})
			}
			start, n, nonZero = -1, 0, 0
		}
		for off := phase; off+1 < len(b); off += 2 {
			w := uint16(b[off]) | uint16(b[off+1])<<8
			if w&0x8000 != 0 {
				flush()
				continue
			}
			if start < 0 {
				start = off
			}
			n++
			if w != 0 {
				nonZero++
			}
		}
		flush()
	}
	sort.Slice(runs, func(i, j int) bool { return runs[i].n > runs[j].n })
	return runs
}

func reportColourRuns(b []byte, name string, top int) string {
	runs := findColourRuns(b, 15*8)
	var sb strings.Builder
	fmt.Fprintf(&sb, "%s: %d runs of RGB555-shaped words\n", name, len(runs))
	if top > len(runs) {
		top = len(runs)
	}
	for _, r := range runs[:top] {
		fmt.Fprintf(&sb, "  0x%06X  %6d words  %6d bytes  = %6.1f palettes of 15\n",
			r.start, r.n, r.n*2, float64(r.n)/15)
	}
	return sb.String()
}

func reportPaletteTables(b []byte, name string, top int) string {
	runs := findPaletteTables(b, 16)
	var sb strings.Builder
	fmt.Fprintf(&sb, "%s: %d palette tables (>=16 palettes, entry 0 always zero)\n", name, len(runs))
	if top > len(runs) {
		top = len(runs)
	}
	for _, r := range runs[:top] {
		fmt.Fprintf(&sb, "  base 0x%06X  %4d palettes  %6d bytes\n",
			r.start, r.n, r.n*palEntries*2)
	}
	return sb.String()
}

// writePaletteSwatches renders candidates as rows of swatches so a human can
// spot skin tones and cloth ramps at a glance.
func writePaletteSwatches(path string, cands []palCandidate, limit int) error {
	if limit > len(cands) {
		limit = len(cands)
	}
	const sw, sh = 16, 16
	img := image.NewRGBA(image.Rect(0, 0, palEntries*sw, limit*sh))
	draw.Draw(img, img.Bounds(), &image.Uniform{color.RGBA{16, 16, 16, 255}}, image.Point{}, draw.Src)
	for row := 0; row < limit; row++ {
		for i, c := range cands[row].Colors {
			r := image.Rect(i*sw, row*sh, (i+1)*sw, (row+1)*sh)
			draw.Draw(img, r, &image.Uniform{c}, image.Point{}, draw.Src)
		}
	}
	f, err := os.Create(path)
	if err != nil {
		return err
	}
	defer f.Close()
	return png.Encode(f, img)
}
