package main

import (
	"fmt"
)

// Sprite descriptor layout inside a 12-byte record:
//
//	0x00  u16  unused, always zero
//	0x02  u16  group/bank selector, a handful of distinct values per file
//	0x04  u16  width
//	0x06  u16  height
//	0x08  u32  offset of the pixel data, relative to the data section
//
// Offsets ascend across populated records, which is what confirms the field is
// an index rather than a size or an id.
type Desc struct {
	Index      int
	Bank       uint16
	Width      int
	Height     int
	DataOffset uint32
}

func (r Record) Desc() Desc {
	return Desc{
		Index:      r.Index,
		Bank:       u16(r.Raw[:], 2),
		Width:      int(u16(r.Raw[:], 4)),
		Height:     int(u16(r.Raw[:], 6)),
		DataOffset: u32(r.Raw[:], 8),
	}
}

func (d Desc) Plausible() bool {
	return d.Width > 0 && d.Height > 0 &&
		d.Width <= 512 && d.Height <= 512
}

// Descriptors returns the populated, plausible sprite descriptors in file order.
func Descriptors(b []byte, h Header) []Desc {
	var out []Desc
	for _, r := range NonEmpty(ReadRecords(b, h)) {
		d := r.Desc()
		if d.Plausible() {
			out = append(out, d)
		}
	}
	return out
}

// Extract decodes one sprite described by d.
func Extract(b []byte, h Header, d Desc) (*Sprite, error) {
	start := h.DataOffset() + int(d.DataOffset)
	if start >= len(b) {
		return nil, fmt.Errorf("sprite %d: data offset past end of file", d.Index)
	}
	s, _, err := decodeSprite(b, start, d.Width, d.Height, true)
	if err != nil {
		return nil, fmt.Errorf("sprite %d (%dx%d at 0x%X): %w",
			d.Index, d.Width, d.Height, start, err)
	}
	return s, nil
}

func summariseDescriptors(ds []Desc) string {
	sizes := map[[2]int]int{}
	banks := map[uint16]int{}
	for _, d := range ds {
		sizes[[2]int{d.Width, d.Height}]++
		banks[d.Bank]++
	}
	return fmt.Sprintf("  %d sprites, %d distinct sizes, %d banks\n",
		len(ds), len(sizes), len(banks))
}
