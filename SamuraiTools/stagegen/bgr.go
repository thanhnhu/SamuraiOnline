package main

import (
	"encoding/binary"
	"fmt"
	"image"
	"image/color"
)

// Samurai Shodown II .BGR stage backgrounds.
//
// Read out of the decompiled binary rather than guessed: the loader is
// FUN_004e0850 and the blitter FUN_004daf50.
//
//	container (magic 0x1042)
//	  0x00 u16 magic
//	  0x02 u16 recordCount
//	  0x04 u32 dataWords        -- 16-bit units. .SPR counts bytes here; the
//	                               loader allocates dataWords*2 and reads
//	                               dataWords items of size 2.
//	  0x08 recordCount * 12-byte records
//	  then dataWords*2 bytes of pixel data
//
//	record (12 bytes)
//	  u16 id       -- lookup key, matched by FUN_004e0960
//	  u16 width    -- blitter reads it at record+2
//	  u16 height   -- blitter reads it at record+4; counts SCANLINES
//	  u16 unused   -- the loader never copies this field
//	  u32 offset   -- in 16-bit words: data + offset*2
//
//	rows: the .SPR codec promoted to 16 bits
//	  [u16 rowWordLength including the prefix][control words]
//	  next row = rowStart + rowWordLength, counted in words
//	  c & 0x8000 -> literal run of (c & 0x7FFF) RGB555 pixels
//	  c & 0x8000 == 0 -> skip c pixels, leaving what is underneath
//
// Each output scanline is built from PlanesPerLine consecutive rows composited
// in order, not from one row. The blitter always consumes eight rows per line:
// it draws some of them, then runs `for (i = 7 - N; i--;) row += *row;` to step
// over the rest. Decoding one row per line reads an eighth of the file and
// leaves seven blank lines out of every eight.
const (
	MagicBGR       = 0x1042
	PlanesPerLine  = 8
	recordSize     = 12
	headerSize     = 8
	literalRunFlag = 0x8000
	runLengthMask  = 0x7fff
)

type Record struct {
	ID     uint16
	Width  int
	Height int
	Offset int // in 16-bit words
}

type Archive struct {
	Records []Record
	data    []byte
}

func Parse(b []byte) (*Archive, error) {
	if len(b) < headerSize {
		return nil, fmt.Errorf("only %d bytes, too short for a header", len(b))
	}
	if magic := binary.LittleEndian.Uint16(b[0:]); magic != MagicBGR {
		return nil, fmt.Errorf("magic 0x%04X, want 0x%04X", magic, MagicBGR)
	}
	count := int(binary.LittleEndian.Uint16(b[2:]))
	words := int(binary.LittleEndian.Uint32(b[4:]))

	start := headerSize + count*recordSize
	if want := start + words*2; want != len(b) {
		return nil, fmt.Errorf("size %d, expected %d from %d records and %d words",
			len(b), want, count, words)
	}

	a := &Archive{data: b[start:]}
	for i := 0; i < count; i++ {
		r := b[headerSize+i*recordSize:]
		a.Records = append(a.Records, Record{
			ID:     binary.LittleEndian.Uint16(r[0:]),
			Width:  int(binary.LittleEndian.Uint16(r[2:])),
			Height: int(binary.LittleEndian.Uint16(r[4:])),
			Offset: int(binary.LittleEndian.Uint32(r[8:])),
		})
	}
	return a, nil
}

func (a *Archive) word(i int) (uint16, bool) {
	if i < 0 || i*2+1 >= len(a.data) {
		return 0, false
	}
	return binary.LittleEndian.Uint16(a.data[i*2:]), true
}

// Background pixels are not colours. The renderer's final pass reads the
// framebuffer as 12-bit palette indices:
//
//	*(ushort *)dst = CONCAT11(*(byte *)(pal + (*src & 0xfff) * 2), ...)
//
// 0xfff is 4095, which is exactly the 4096-entry palette table documented for
// sprites. Treating the words as direct colour produces recognisable shapes in
// entirely wrong colours, because index values happen to land in bit patterns
// that read as green.
const PaletteIndexMask = 0xfff

// Palette is one 4096-entry snapshot, RGB555.
type Palette [4096]uint16

// PaletteSnapshots is how many the game ships inside GAME1.PRG.
const (
	PaletteSnapshots   = 6
	PaletteOffsetInPRG = 0x14000
	paletteEntries     = 4096
)

// LoadPalette pulls one snapshot out of GAME1.PRG. Which snapshot a given stage
// wants is not recorded in the .BGR file, so the caller has to choose.
func LoadPalette(prg []byte, snapshot int) (*Palette, error) {
	if snapshot < 0 || snapshot >= PaletteSnapshots {
		return nil, fmt.Errorf("snapshot %d, want 0..%d", snapshot, PaletteSnapshots-1)
	}
	start := PaletteOffsetInPRG + snapshot*paletteEntries*2
	if start+paletteEntries*2 > len(prg) {
		return nil, fmt.Errorf("GAME1.PRG is %d bytes, too small for snapshot %d", len(prg), snapshot)
	}
	var p Palette
	for i := 0; i < paletteEntries; i++ {
		p[i] = binary.LittleEndian.Uint16(prg[start+i*2:])
	}
	return &p, nil
}

// rgb555 is the palette entry format. Bit 15 is unused.
func rgb555(c uint16) color.NRGBA {
	expand := func(v uint16) uint8 { return uint8(v<<3 | v>>2) }
	return color.NRGBA{
		R: expand((c >> 10) & 0x1f),
		G: expand((c >> 5) & 0x1f),
		B: expand(c & 0x1f),
		A: 0xff,
	}
}

func (a *Archive) Decode(r Record, pal *Palette) (*image.NRGBA, error) {
	if r.Width <= 0 || r.Height <= 0 {
		return nil, fmt.Errorf("record %d is %dx%d", r.ID, r.Width, r.Height)
	}
	img := image.NewNRGBA(image.Rect(0, 0, r.Width, r.Height))
	row := r.Offset

	for y := 0; y < r.Height; y++ {
		for plane := 0; plane < PlanesPerLine; plane++ {
			next, err := a.compositeRow(img, row, y, r.Width, pal)
			if err != nil {
				return nil, fmt.Errorf("line %d plane %d: %w", y, plane, err)
			}
			row = next
		}
	}
	return img, nil
}

// compositeRow draws one stored row onto scanline y and returns where the next
// row starts. Skips leave existing pixels alone, which is what makes the eight
// planes stack.
func (a *Archive) compositeRow(img *image.NRGBA, row, y, width int, pal *Palette) (int, error) {
	length, ok := a.word(row)
	if !ok {
		return 0, fmt.Errorf("row header at word %d is past the data", row)
	}
	if length < 1 {
		return 0, fmt.Errorf("row length %d", length)
	}
	end := row + int(length)

	x := 0
	for i := row + 1; i < end; {
		c, ok := a.word(i)
		if !ok {
			return 0, fmt.Errorf("control word at %d is past the data", i)
		}
		i++
		if c&literalRunFlag != 0 {
			n := int(c & runLengthMask)
			for k := 0; k < n; k++ {
				px, ok := a.word(i)
				if !ok {
					return 0, fmt.Errorf("literal run past the data")
				}
				i++
				if x < width {
					img.SetNRGBA(x, y, rgb555(pal[px&PaletteIndexMask]))
				}
				x++
			}
		} else {
			x += int(c)
		}
	}
	if x != width {
		return 0, fmt.Errorf("covered %d pixels, width is %d", x, width)
	}
	return end, nil
}
