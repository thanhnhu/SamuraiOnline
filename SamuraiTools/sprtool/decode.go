package main

import (
	"errors"
	"fmt"
)

// Row-oriented sprite codec, transcribed from the game's own blitter
// (FUN_004dc8e0 in the decompiled executable).
//
// Each row is self-delimiting: it opens with a little-endian uint16 giving the
// row's total byte length, prefix included. The blitter walks rows with
//
//	next = current + *(uint16*)current
//
// so there is no terminator byte at all. Inside a row, control bytes alternate
// between transparent skips and literal pixel runs:
//
//	b & 0x80 != 0   literal run: (b & 0x7F) palette indices follow
//	b & 0x80 == 0   skip b transparent pixels
//
// Solid colour is stored as a literal run of identical bytes; there is no
// fill-with-value opcode. Every row's skips and runs sum to exactly the width.
const (
	rowLengthPrefix = 2
	literalFlag     = 0x80
	countMask       = 0x7F
)

type Sprite struct {
	Width  int
	Height int
	Pix    []byte // palette indices, meaningful only where Opaque is set
	Opaque []bool // skips are transparent; index 0 is a real colour
}

var errRowOverflow = errors.New("row overflowed the declared width")

// decodeSprite reads height rows starting at off, returning the bytes consumed
// so the caller can walk to the next sprite.
func decodeSprite(b []byte, off, width, height int, tolerant bool) (*Sprite, int, error) {
	s := &Sprite{
		Width:  width,
		Height: height,
		Pix:    make([]byte, width*height),
		Opaque: make([]bool, width*height),
	}
	rowStart := off

	for y := 0; y < height; y++ {
		if rowStart+rowLengthPrefix > len(b) {
			return nil, rowStart - off, fmt.Errorf("row %d: length prefix past end of data", y)
		}
		rowLen := int(u16(b, rowStart))
		if rowLen < rowLengthPrefix {
			return nil, rowStart - off, fmt.Errorf("row %d: implausible length %d", y, rowLen)
		}
		rowEnd := rowStart + rowLen
		if rowEnd > len(b) {
			return nil, rowStart - off, fmt.Errorf("row %d: extends past end of data", y)
		}

		if err := decodeRow(b[rowStart+rowLengthPrefix:rowEnd],
			s.Pix[y*width:(y+1)*width], s.Opaque[y*width:(y+1)*width], tolerant); err != nil {
			return nil, rowStart - off, fmt.Errorf("row %d: %w", y, err)
		}
		rowStart = rowEnd
	}
	return s, rowStart - off, nil
}

func decodeRow(src, dst []byte, opaque []bool, tolerant bool) error {
	width := len(dst)
	x, p := 0, 0

	for p < len(src) {
		c := src[p]
		p++
		n := int(c & countMask)

		if c&literalFlag == 0 {
			x += n
			if x > width && !tolerant {
				return errRowOverflow
			}
			continue
		}
		if p+n > len(src) {
			return errors.New("literal run past end of row")
		}
		for i := 0; i < n; i++ {
			if x >= 0 && x < width {
				dst[x] = src[p+i]
				opaque[x] = true
			} else if !tolerant {
				return errRowOverflow
			}
			x++
		}
		p += n
	}
	return nil
}

// coverage reports how much of the sprite ended up opaque, which is the
// quickest signal that a guessed layout is wrong: a misparsed sprite is either
// almost empty or almost full.
func (s *Sprite) coverage() float64 {
	n := 0
	for _, v := range s.Opaque {
		if v {
			n++
		}
	}
	return float64(n) / float64(len(s.Opaque))
}
