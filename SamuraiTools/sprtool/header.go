package main

import (
	"fmt"
	"strings"
)

// Container layout, confirmed against every .SPR in the game directory:
//
//	0x00  u16  magic 0x1053
//	0x02  u16  sprite count
//	0x04  u32  size of the pixel data that follows the table
//	0x08  n    12-byte sprite records
//	       -   pixel data
//
// The relationship file_size = 8 + count*12 + dataSize holds exactly, which is
// what pins the layout down rather than leaving it a guess.
const (
	sprMagic      = 0x1053
	sprRecordSize = 12
	sprHeaderSize = 8
)

type Header struct {
	Magic    uint16
	Count    int
	DataSize uint32
}

type Record struct {
	Index int
	Raw   [sprRecordSize]byte
}

func ParseHeader(b []byte) (Header, error) {
	h := Header{
		Magic:    u16(b, 0),
		Count:    int(u16(b, 2)),
		DataSize: u32(b, 4),
	}
	if h.Magic != sprMagic {
		return h, fmt.Errorf("bad magic 0x%04X, expected 0x%04X", h.Magic, sprMagic)
	}
	return h, nil
}

// DataOffset is where pixel data begins, immediately after the record table.
func (h Header) DataOffset() int {
	return sprHeaderSize + h.Count*sprRecordSize
}

func (h Header) Check(fileSize int) error {
	want := h.DataOffset() + int(h.DataSize)
	if want != fileSize {
		return fmt.Errorf("size mismatch: header implies %d bytes, file is %d", want, fileSize)
	}
	return nil
}

func ReadRecords(b []byte, h Header) []Record {
	out := make([]Record, 0, h.Count)
	for i := 0; i < h.Count; i++ {
		off := sprHeaderSize + i*sprRecordSize
		if off+sprRecordSize > len(b) {
			break
		}
		var r Record
		r.Index = i
		copy(r.Raw[:], b[off:off+sprRecordSize])
		out = append(out, r)
	}
	return out
}

// describeRecords prints records in several interpretations at once, so the
// field layout can be read off rather than guessed one field at a time.
func describeRecords(recs []Record, from, n int) string {
	var sb strings.Builder
	sb.WriteString("  idx  raw bytes                                     u32[0]     u32[1]     u32[2]     u16s\n")
	for i := from; i < from+n && i < len(recs); i++ {
		r := recs[i]
		hexes := make([]string, sprRecordSize)
		for j, c := range r.Raw {
			hexes[j] = fmt.Sprintf("%02X", c)
		}
		fmt.Fprintf(&sb, "  %4d  %s  %-10d %-10d %-10d %v\n",
			r.Index,
			strings.Join(hexes, " "),
			u32(r.Raw[:], 0), u32(r.Raw[:], 4), u32(r.Raw[:], 8),
			[]uint16{u16(r.Raw[:], 0), u16(r.Raw[:], 2), u16(r.Raw[:], 4),
				u16(r.Raw[:], 6), u16(r.Raw[:], 8), u16(r.Raw[:], 10)},
		)
	}
	return sb.String()
}

// countNonEmpty reports how many records carry anything beyond the idle
// {1,0,0} pattern that fills unused slots.
func countNonEmpty(recs []Record) int {
	n := 0
	for _, r := range recs {
		if !r.IsEmpty() {
			n++
		}
	}
	return n
}

func (r Record) IsEmpty() bool {
	return u32(r.Raw[:], 0) == 1 && u32(r.Raw[:], 4) == 0 && u32(r.Raw[:], 8) == 0
}

// NonEmpty returns only the populated slots, which is where the real sprite
// descriptors live.
func NonEmpty(recs []Record) []Record {
	var out []Record
	for _, r := range recs {
		if !r.IsEmpty() {
			out = append(out, r)
		}
	}
	return out
}
